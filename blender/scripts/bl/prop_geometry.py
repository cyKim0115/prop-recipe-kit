"""Build one low-poly prop mesh from a JSON spec, inside Blender.

Runs only in Blender's Python (bpy, bmesh). Uses the data API, never bpy.ops,
so it works the same in background mode and needs no UI context.

Spec units are meters. Colors are sRGB ('#RRGGBB'). Blender axes: Z up, the prop's front faces -Y.
Every part is baked into one mesh (no live modifiers), so the mesh that is
validated is the mesh that is exported.
"""
import math

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

PRIMITIVES = ("box", "cylinder", "cone", "sphere", "torus")


def _part_matrix(part):
    loc = Vector(part.get("location", (0.0, 0.0, 0.0)))
    rot = Euler([math.radians(a) for a in part.get("rotation", (0.0, 0.0, 0.0))], "XYZ")
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4()


def _faces_of(verts):
    faces = set()
    for v in verts:
        faces.update(v.link_faces)
    return list(faces)


def _add_box(bm, part, mat):
    sx, sy, sz = part["size"]
    scale = Matrix.Diagonal((sx, sy, sz, 1.0))
    res = bmesh.ops.create_cube(bm, size=1.0, matrix=mat @ scale)
    verts = res["verts"]
    bevel = part.get("bevel")
    if bevel:
        width = float(bevel.get("width", 0.0))
        if width > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            out = bmesh.ops.bevel(
                bm, geom=edges, offset=width, offset_type="OFFSET",
                segments=int(bevel.get("segments", 1)), profile=0.5, affect="EDGES",
                clamp_overlap=True)
            verts = list({v for f in out["faces"] for v in f.verts} | set(verts))
            verts = [v for v in verts if v.is_valid]
    return verts


def _add_cylinder(bm, part, mat, cone=False):
    seg = int(part.get("segments", 12))
    depth = float(part["depth"])
    if cone:
        r1, r2 = float(part["radius_bottom"]), float(part["radius_top"])
    else:
        r1 = r2 = float(part["radius"])
    res = bmesh.ops.create_cone(
        bm, cap_ends=part.get("caps", True), cap_tris=False, segments=seg,
        radius1=r1, radius2=r2, depth=depth, matrix=mat)
    return res["verts"]


def _add_sphere(bm, part, mat):
    res = bmesh.ops.create_uvsphere(
        bm, u_segments=int(part.get("segments", 12)), v_segments=int(part.get("rings", 6)),
        radius=float(part["radius"]), matrix=mat)
    return res["verts"]


def _add_torus(bm, part, mat):
    big_r, small_r = float(part["major_radius"]), float(part["minor_radius"])
    m, n = int(part.get("segments", 16)), int(part.get("minor_segments", 6))
    grid = []
    for i in range(m):
        a = 2.0 * math.pi * i / m
        ring = []
        for j in range(n):
            b = 2.0 * math.pi * j / n
            r = big_r + small_r * math.cos(b)
            co = Vector((r * math.cos(a), r * math.sin(a), small_r * math.sin(b)))
            ring.append(bm.verts.new(mat @ co))
        grid.append(ring)
    for i in range(m):
        for j in range(n):
            a, b = grid[i][j], grid[(i + 1) % m][j]
            c, d = grid[(i + 1) % m][(j + 1) % n], grid[i][(j + 1) % n]
            bm.faces.new((a, b, c, d))
    return [v for ring in grid for v in ring]


def parse_color(c):
    """'#RRGGBB' or [r, g, b] in 0..1, both sRGB (what an artist picks). Returns sRGB floats."""
    if isinstance(c, str):
        h = c.lstrip("#")
        if len(h) != 6:
            raise ValueError(f"color {c!r}: use '#RRGGBB'")
        return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return tuple(float(v) for v in c[:3])


def srgb_to_linear(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def _material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    lin = [srgb_to_linear(v) for v in parse_color(color)]
    rgba = (lin[0], lin[1], lin[2], 1.0)
    mat.diffuse_color = rgba
    tree = mat.node_tree
    if tree is not None:
        bsdf = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None and "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = rgba
    return mat


def build(spec):
    """Create the prop object in the current (empty) scene. Returns (object, info)."""
    name = spec["name"]
    palette = spec.get("materials", {})
    mat_names = list(palette.keys())
    if not mat_names:
        raise ValueError("spec.materials is empty: give at least one named color")

    bm = bmesh.new()
    for idx, part in enumerate(spec["parts"]):
        kind = part.get("type")
        if kind not in PRIMITIVES:
            raise ValueError(f"part {idx}: unknown type {kind!r}, use one of {PRIMITIVES}")
        mat_key = part.get("material", mat_names[0])
        if mat_key not in palette:
            raise ValueError(f"part {idx}: material {mat_key!r} is not in spec.materials")
        before = set(bm.faces)
        m = _part_matrix(part)
        if kind == "box":
            _add_box(bm, part, m)
        elif kind == "cylinder":
            _add_cylinder(bm, part, m)
        elif kind == "cone":
            _add_cylinder(bm, part, m, cone=True)
        elif kind == "sphere":
            _add_sphere(bm, part, m)
        else:
            _add_torus(bm, part, m)
        bm.faces.ensure_lookup_table()
        new_faces = [f for f in bm.faces if f not in before]
        smooth = part.get("shade", "flat") == "smooth"
        for f in new_faces:
            f.material_index = mat_names.index(mat_key)
            f.smooth = smooth
        # Caps stay flat on smooth cylinders: a face whose normal is parallel to the
        # part axis is a cap.
        if smooth and kind in ("cylinder", "cone"):
            axis = (m.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
            for f in new_faces:
                f.normal_update()
                if abs(f.normal.normalized().dot(axis)) > 0.999:
                    f.smooth = False

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    # Origin: bottom center of the bounding box (Unity placement convention).
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    shift = Vector((-(min(xs) + max(xs)) / 2.0, -(min(ys) + max(ys)) / 2.0, -min(zs)))
    bmesh.ops.translate(bm, vec=shift, verts=bm.verts[:])

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    for key in mat_names:
        mesh.materials.append(_material(f"M_{name}_{key}", palette[key]))

    if spec.get("uv", "palette") == "palette":
        _palette_uv(mesh, len(mat_names))

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    info = {"origin_shift_m": [round(c, 6) for c in shift], "materials": mat_names}
    return obj, info


def _palette_uv(mesh, count):
    """Every corner of a face gets the texel center of its material's palette pixel.

    Palette texture = count x 1 pixels, pixel i = material i. No unwrap needed.
    """
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        u = (poly.material_index + 0.5) / count
        for li in poly.loop_indices:
            uv.data[li].uv = (u, 0.5)

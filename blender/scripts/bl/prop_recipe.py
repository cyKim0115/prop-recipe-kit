"""Rebuild a prop from a recipe dumped by the Unity prototype generator ("frame": "unity"), inside Blender.

Input (from the Unity package, PropSpecJson.Write): parts = [{mat, group, matrix (Unity 4x4 row-major), gen: {type, ...}}],
palette = {mat: {hex, glow}}. gen types mirror the C# PropMeshKit generators:
  roundbox {size, radius, seg} / lathe {profile [[r, y]], segments, lobes, amp, sharp}
  extrude {outline [[x, y]], depth, bevel, arc} / utorus {major, minor, seg_major, seg_minor, arc}
Each part is generated in Unity local space, moved by its Unity matrix, then converted to Blender space
(Unity (x, y, z) -> Blender (-x, -z, y)), so the Unity front (+Z) becomes the Blender front (-Y) and the
exported FBX lands back on the prototype's prefab pivot.

spec["reduce"] (all optional): seg_scale, seg_min, box_seg, profile_step, outline_step, cull_hidden,
merge_distance, planar_angle_deg, target_tris, sharp_angle_deg, single_material.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from prop_geometry import parse_color, srgb_to_linear

U2B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def _seg(n, red):
    s = red.get("seg_scale", 1.0)
    return n if abs(s - 1.0) < 1e-6 else max(int(red.get("seg_min", 3)), int(round(n * s)))


def _simplify(pts, rel):
    """Douglas-Peucker; tolerance = bbox diagonal x rel. Same rule as PropLod.Simplify (C#)."""
    if len(pts) <= 4:
        return list(pts)
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    tol = math.hypot(max(xs) - min(xs), max(ys) - min(ys)) * rel
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = pts[a]; bx, by = pts[b]
        dx, dy = bx - ax, by - ay
        L2 = dx * dx + dy * dy
        best, idx = -1.0, -1
        for i in range(a + 1, b):
            px, py = pts[i]
            t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
            d = math.hypot(px - (ax + dx * t), py - (ay + dy * t))
            if d > best:
                best, idx = d, i
        if idx >= 0 and best > tol:
            keep[idx] = True
            stack += [(a, idx), (idx, b)]
    out = [p for p, k in zip(pts, keep) if k]
    return out if len(out) >= 3 else list(pts)


def _thin(pts, step, rel=0.0):
    if rel > 0:
        return _simplify(pts, rel)
    if step <= 1 or len(pts) <= 4:
        return list(pts)
    out = pts[::step]
    if (len(pts) - 1) % step:
        out.append(pts[-1])
    return out


def _roundbox(bm, g, red):
    sx, sy, sz = g["size"]
    res = bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((sx, sy, sz, 1.0)))
    verts = res["verts"]
    seg = min(int(g.get("seg", 4)), int(red.get("box_seg", 99)))
    r = min(float(g.get("radius", 0.0)), min(sx, sy, sz) * 0.499)
    if r > 0.002 and seg >= 1:
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=r, offset_type="OFFSET", segments=seg, profile=0.5,
                        affect="EDGES", clamp_overlap=True)


def _lathe(bm, g, red):
    prof = _thin([tuple(p) for p in g["profile"]], int(red.get("profile_step", 1)), float(red.get("simplify", 0)))
    n = _seg(int(g.get("segments", 32)), red)
    lobes, amp, sharp = int(g.get("lobes", 0)), float(g.get("amp", 0.0)), bool(g.get("sharp", False))
    if lobes > 0 and n != int(g.get("segments", 32)):
        # keep every lobe tip: at least 4 samples per lobe, a multiple of the lobe count
        n = max(n, lobes * 4)
        n = int(math.ceil(n / lobes) * lobes)
    rings = []
    for r0, y in prof:
        if r0 < 1e-4:
            rings.append([bm.verts.new((0.0, y, 0.0))])
            continue
        ring = []
        for s in range(n):
            t = 2 * math.pi * s / n
            k = 1.0
            if lobes > 0:
                c = math.cos(lobes * t)
                if sharp:
                    c = abs(math.cos(lobes * t * 0.5)) ** 0.6 * 2 - 1
                k = 1 + amp * c
            ring.append(bm.verts.new((math.sin(t) * r0 * k, y, math.cos(t) * r0 * k)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for s in range(n):
            if len(a) == 1:
                f = (a[0], b[(s + 1) % n], b[s])
            elif len(b) == 1:
                f = (a[s], a[(s + 1) % n], b[0])
            else:
                f = (a[s], a[(s + 1) % n], b[(s + 1) % n], b[s])
            try:
                bm.faces.new(f)
            except ValueError:
                pass


def _signed_area(pts):
    return 0.5 * sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))


def _extrude(bm, g, red):
    pts = _thin([tuple(p) for p in g["outline"]], int(red.get("outline_step", 1)), float(red.get("simplify", 0)))
    clean = []
    for p in pts:
        if not clean or (abs(p[0] - clean[-1][0]) + abs(p[1] - clean[-1][1])) > 1e-5:
            clean.append(p)
    if len(clean) > 2 and abs(clean[0][0] - clean[-1][0]) + abs(clean[0][1] - clean[-1][1]) < 1e-5:
        clean.pop()
    if _signed_area(clean) < 0:
        clean.reverse()
    n = len(clean)
    h = max(float(g["depth"]) * 0.5, 0.001)
    xs, ys = [p[0] for p in clean], [p[1] for p in clean]
    shape_min = min(max(xs) - min(xs), max(ys) - min(ys))
    b = min(max(min(float(g.get("bevel", 0.02)), shape_min * 0.1), 0.0005), h * 0.95)
    # outward offset by b at mid depth (miter, clamped): approximates the rounded rim of PropMeshKit.Extrude
    mid = []
    for i in range(n):
        p0, p1, p2 = Vector(clean[i - 1]), Vector(clean[i]), Vector(clean[(i + 1) % n])
        e0, e1 = (p1 - p0).normalized(), (p2 - p1).normalized()
        n0, n1 = Vector((e0.y, -e0.x)), Vector((e1.y, -e1.x))
        m = (n0 + n1)
        m = m.normalized() if m.length > 1e-6 else n1
        miter = min(b / max(m.dot(n1), 0.35), b * 2.5)
        mid.append(p1 + m * miter)
    top = [bm.verts.new((p[0], p[1], h)) for p in clean]
    bot = [bm.verts.new((p[0], p[1], -h)) for p in clean]
    ring = [bm.verts.new((p.x, p.y, 0.0)) for p in mid]
    cap_t = bm.faces.new(top)
    cap_b = bm.faces.new(list(reversed(bot)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((top[i], ring[i], ring[j], top[j]))
        bm.faces.new((ring[i], bot[i], bot[j], ring[j]))
    bmesh.ops.triangulate(bm, faces=[cap_t, cap_b], quad_method="BEAUTY", ngon_method="BEAUTY")


def _utorus(bm, g, red):
    big, small = float(g["major"]), float(g["minor"])
    m, n = _seg(int(g.get("seg_major", 32)), red), _seg(int(g.get("seg_minor", 10)), red)
    arc = math.radians(max(1.0, min(360.0, float(g.get("arc", 360.0)))))
    closed = arc > math.radians(359.9)
    cols = m if closed else m + 1
    grid = []
    for i in range(cols):
        u = arc * i / m
        grid.append([bm.verts.new((math.cos(u) * (big + small * math.cos(2 * math.pi * j / n)),
                                   small * math.sin(2 * math.pi * j / n),
                                   math.sin(u) * (big + small * math.cos(2 * math.pi * j / n)))) for j in range(n)])
    for i in range(m if closed else m):
        a, c = grid[i], grid[(i + 1) % cols]
        for j in range(n):
            bm.faces.new((a[j], c[j], c[(j + 1) % n], a[(j + 1) % n]))


GEN = {"roundbox": _roundbox, "lathe": _lathe, "extrude": _extrude, "utorus": _utorus}


def _part_bmesh(part, red):
    bm = bmesh.new()
    GEN[part["gen"]["type"]](bm, part["gen"], red)
    mu = Matrix([part["matrix"][r * 4:(r + 1) * 4] for r in range(4)])
    bmesh.ops.transform(bm, matrix=U2B @ mu, verts=bm.verts[:])
    md = float(red.get("merge_distance", 0.0002))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=md)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def _inside(bvh, p, max_hits=64):
    """Ray parity along +Z through a BVH; a point exactly on the surface may go either way."""
    d = Vector((0.0013, 0.0007, 1.0)).normalized()
    origin, hits = p.copy(), 0
    for _ in range(max_hits):
        loc, _n, _i, _dist = bvh.ray_cast(origin, d)
        if loc is None:
            break
        hits += 1
        origin = loc + d * 1e-5
    return hits % 2 == 1


def build(spec):
    name = spec.get("name") or ("SM_" + spec["id"])
    red = spec.get("reduce", {})
    palette = spec["palette"]
    keys = list(palette.keys())
    parts = [p for p in spec["parts"] if p.get("group", -1) == -1]
    bms, closed = [], []
    for p in parts:
        bm = _part_bmesh(p, red)
        bms.append(bm)
        closed.append(all(e.is_manifold for e in bm.edges) and len(bm.edges) > 0)
    tris_before = sum(sum(len(f.verts) - 2 for f in bm.faces) for bm in bms)
    culled = 0
    if red.get("cull_hidden", False):
        trees = [BVHTree.FromBMesh(bm) if c else None for bm, c in zip(bms, closed)]
        bounds = []
        for bm in bms:
            cs = [v.co for v in bm.verts]
            bounds.append((Vector([min(c[i] for c in cs) for i in range(3)]), Vector([max(c[i] for c in cs) for i in range(3)])))
        for i, bm in enumerate(bms):
            dead = []
            for f in bm.faces:
                g = f.calc_center_median()
                probes = [g] + [v.co.lerp(g, 0.03) for v in f.verts]
                for j, tree in enumerate(trees):
                    if j == i or tree is None:
                        continue
                    lo, hi = bounds[j]
                    if not all(lo[k] - 1e-6 <= q[k] <= hi[k] + 1e-6 for q in probes for k in range(3)):
                        continue
                    if all(_inside(tree, q) for q in probes):
                        dead.append(f)
                        break
            culled += sum(len(f.verts) - 2 for f in dead)
            bmesh.ops.delete(bm, geom=dead, context="FACES")
    # join into one mesh, material index = palette key order
    out = bmesh.new()
    for p, bm in zip(parts, bms):
        tmp = bpy.data.meshes.new("_tmp")
        bm.to_mesh(tmp)
        bm.free()
        mi = keys.index(p["mat"])
        for poly in tmp.polygons:
            poly.material_index = mi
        out.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    bmesh.ops.remove_doubles(out, verts=out.verts[:], dist=float(red.get("merge_distance", 0.0002)))
    loose = [v for v in out.verts if not v.link_faces]
    bmesh.ops.delete(out, geom=loose, context="VERTS")
    planar = red.get("planar_angle_deg")
    if planar:
        bmesh.ops.dissolve_limit(out, angle_limit=math.radians(float(planar)), use_dissolve_boundaries=False,
                                 verts=out.verts[:], edges=out.edges[:], delimit={"MATERIAL"})
    mesh = bpy.data.meshes.new(name)
    out.to_mesh(mesh)
    out.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.set_sharp_from_angle(angle=math.radians(float(red.get("sharp_angle_deg", 50))))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    decimated = None
    target = red.get("target_tris")
    mesh.calc_loop_triangles()
    if target and len(mesh.loop_triangles) > int(target):
        ratio = int(target) / len(mesh.loop_triangles) * 0.98
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = ratio
        mod.use_collapse_triangulate = False
        dg = bpy.context.evaluated_depsgraph_get()
        newm = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
        obj.modifiers.clear()
        old = obj.data
        obj.data = newm
        newm.name = name
        bpy.data.meshes.remove(old)
        mesh = newm
        decimated = round(ratio, 4)
    # palette UVs + materials
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        u = (poly.material_index + 0.5) / len(keys)
        for li in poly.loop_indices:
            uv.data[li].uv = (u, 0.5)
    mesh.materials.clear()
    if red.get("single_material", False):
        mesh.materials.append(_palette_material(name, keys, palette))
        for poly in mesh.polygons:
            poly.material_index = 0
    else:
        for k in keys:
            mat = bpy.data.materials.get(f"M_{name}_{k}") or bpy.data.materials.new(f"M_{name}_{k}")
            lin = [srgb_to_linear(c) for c in parse_color(palette[k]["hex"])]
            mat.diffuse_color = (*lin, 1.0)
            mesh.materials.append(mat)
    mesh.calc_loop_triangles()
    info = {"tris_generated": tris_before, "tris_culled": culled, "decimate_ratio": decimated,
            "tris_final": len(mesh.loop_triangles), "parts": len(parts), "closed_parts": sum(closed),
            "materials": keys, "single_material": bool(red.get("single_material", False))}
    return obj, info


def _palette_material(name, keys, palette):
    img = bpy.data.images.new(f"PT_{name}", width=len(keys), height=1, alpha=False)
    px = []
    for k in keys:
        px += [*parse_color(palette[k]["hex"]), 1.0]
    img.pixels.foreach_set(px)
    img.pack()
    mat = bpy.data.materials.new(f"M_{name}_palette")
    tree = mat.node_tree
    bsdf = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    if bsdf is not None:
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    tree.nodes.active = tex
    return mat

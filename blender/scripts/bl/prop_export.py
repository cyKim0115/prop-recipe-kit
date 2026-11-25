"""FBX export preset for Unity static props and a read-back check, inside Blender.

Preset (verified 2026-11-25 by parsing the exported FBX and importing it into Unity 6000.6):
- bake_space_transform=True + apply_scale_options='FBX_SCALE_UNITS' writes Y-up
  meter vertices with an identity node and UnitScaleFactor 100, so Unity's default
  importer (Bake Axis Conversion off, Use File Scale on) gives root rotation 0,
  scale 1 and exact dimensions. Blender front (-Y) becomes Unity +Z.
  Check your own project's existing FBX with `blender_run.py inspect-fbx` first.
  "Apply Transform" is documented as broken with armatures/animation and nested
  hierarchies; a single static mesh avoids both.
- mesh_smooth_type='OFF' (normals only): normals carry the shading; Unity imports
  file normals by default. Smoothing-group warnings reported online are Unreal's.
"""
import os

import bpy

FBX_PRESET = dict(
    use_selection=True,
    object_types={"MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    use_space_transform=True,
    bake_space_transform=True,
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_triangles=False,
    use_tspace=False,
    use_custom_props=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
    embed_textures=False,
)


def export(obj, path):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, **FBX_PRESET)
    return os.path.getsize(path)


def _props70(elem):
    out = {}
    for c in elem.elems:
        if c.id == b"Properties70":
            for p in c.elems:
                out[p.props[0].decode()] = list(p.props[4:])
    return out


def inspect(path):
    """Read the FBX back with Blender's own parser: what Unity will see."""
    import io_scene_fbx.parse_fbx as pf

    root, version = pf.parse(path)
    info = {"fbx_version": version, "models": [], "geometry_extent": None}
    for e in root.elems:
        if e.id == b"GlobalSettings":
            g = _props70(e)
            info["global"] = {k: g[k][0] for k in
                              ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign",
                               "CoordAxis", "CoordAxisSign", "UnitScaleFactor") if k in g}
        if e.id == b"Objects":
            for o in e.elems:
                if o.id == b"Model":
                    p = _props70(o)
                    info["models"].append({
                        "name": o.props[1].split(b"\x00")[0].decode(),
                        "rotation": p.get("Lcl Rotation") or [0.0, 0.0, 0.0],
                        "scaling": p.get("Lcl Scaling") or [1.0, 1.0, 1.0],
                        "translation": p.get("Lcl Translation") or [0.0, 0.0, 0.0],
                    })
                if o.id == b"Geometry":
                    for c in o.elems:
                        if c.id == b"Vertices":
                            v = c.props[0]
                            info["geometry_extent"] = [
                                round(max(v[i::3]) - min(v[i::3]), 6) for i in range(3)]
    return info


def check(info, dims_blender_m, tol):
    """FBX must hold: identity node, unit scale 100 (1 unit = 1 m), Y up,
    and Blender (x, y, z) dims show up as FBX (x, z, y)."""
    checks = []
    g = info.get("global", {})
    checks.append({"name": "fbx_unit_meter", "ok": g.get("UnitScaleFactor") == 100.0,
                   "detail": f"UnitScaleFactor {g.get('UnitScaleFactor')}"})
    checks.append({"name": "fbx_y_up", "ok": g.get("UpAxis") == 1 and g.get("UpAxisSign") == 1,
                   "detail": f"UpAxis {g.get('UpAxis')} sign {g.get('UpAxisSign')}"})
    ident = all(all(abs(a) < 1e-4 for a in m["rotation"]) and all(abs(s - 1) < 1e-6 for s in m["scaling"])
                for m in info["models"])
    checks.append({"name": "fbx_identity_nodes", "ok": ident and len(info["models"]) == 1,
                   "detail": f"{len(info['models'])} model(s): "
                             + "; ".join(f"{m['name']} rot {m['rotation']} scl {m['scaling']}" for m in info["models"])})
    ext = info.get("geometry_extent")
    want = [dims_blender_m[0], dims_blender_m[2], dims_blender_m[1]]
    ok = ext is not None and all(abs(ext[i] - want[i]) <= tol for i in range(3))
    checks.append({"name": "fbx_geometry_dims", "ok": ok,
                   "detail": f"FBX extent {ext} vs expected (x, z, y) {want}"})
    return checks

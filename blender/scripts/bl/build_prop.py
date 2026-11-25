"""Entry point run inside Blender: spec -> mesh -> checks -> .blend / FBX -> FBX read-back -> views.

Usage (through blender_run.py, not directly):
  blender -b --factory-startup --offline-mode --python-exit-code 1 --python build_prop.py -- \
      <spec.json> <out_dir> [--no-render] [--no-blend]

Writes into out_dir: <name>.fbx, <name>.blend, <name>_palette.png, <name>_views.webp,
<name>_report.json. Exit code 1 when any check fails (files are still written so the
failure can be inspected).
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import prop_export  # noqa: E402
import prop_geometry  # noqa: E402
import prop_recipe  # noqa: E402
import prop_render  # noqa: E402
import prop_validate  # noqa: E402


def main(argv):
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    flags = {a for a in args if a.startswith("--")}
    pos = [a for a in args if not a.startswith("--")]
    if len(pos) != 2:
        print("usage: build_prop.py -- <spec.json> <out_dir> [--no-render] [--no-blend]")
        return 2
    spec_path, out_dir = pos
    with open(spec_path, encoding="utf-8") as f:
        spec = json.load(f)
    name = spec.get("name") or ("SM_" + spec["id"])
    os.makedirs(out_dir, exist_ok=True)
    t0 = time.time()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    if spec.get("frame") == "unity":
        spec.setdefault("name", "SM_" + spec["id"])
        name = spec["name"]
        obj, build_info = prop_recipe.build(spec)
    else:
        obj, build_info = prop_geometry.build(spec)
    metrics, checks = prop_validate.check(obj, spec)
    tol = float(spec.get("expect", {}).get("tolerance_m", prop_validate.DEFAULT_TOLERANCE_M))

    files = {}
    if "--no-blend" not in flags:
        blend = os.path.join(out_dir, name + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend, copy=True, compress=True)
        files["blend"] = blend
    fbx = os.path.join(out_dir, name + ".fbx")
    fbx_bytes = prop_export.export(obj, fbx)
    files["fbx"] = fbx
    fbx_info = prop_export.inspect(fbx)
    checks += prop_export.check(fbx_info, metrics["dims_m"], tol)

    if spec.get("uv", "palette") == "palette" and spec.get("frame") != "unity":
        pal = os.path.join(out_dir, name + "_palette.png")
        prop_render.palette_png(spec, pal)
        files["palette"] = pal
    render_info = None
    if "--no-render" not in flags:
        sheet = os.path.join(out_dir, name + "_views.webp")
        render_info = prop_render.render_sheet(obj, sheet)
        files["views"] = sheet

    failed = [c for c in checks if not c["ok"]]
    report = {
        "name": name,
        "spec": os.path.abspath(spec_path),
        "result": "fail" if failed else "pass",
        "blender": bpy.app.version_string,
        "metrics": metrics,
        "build": build_info,
        "checks": checks,
        "fbx": {"bytes": fbx_bytes, **fbx_info},
        "fbx_preset": {k: (sorted(v) if isinstance(v, set) else v) for k, v in prop_export.FBX_PRESET.items()},
        "render": render_info,
        "files": files,
        "seconds": round(time.time() - t0, 2),
    }
    rep = os.path.join(out_dir, name + "_report.json")
    with open(rep, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    print(f"PROP {name}: {report['result'].upper()}  tris={metrics['triangles']} "
          f"dims={metrics['dims_m']} materials={metrics['material_slots']} "
          f"fbx={fbx_bytes}B  {report['seconds']}s")
    for c in checks:
        print(f"  [{'ok' if c['ok'] else 'FAIL'}] {c['name']}: {c['detail']}")
    print(f"REPORT {rep}")
    return 1 if failed else 0


if __name__ == "__main__":
    code = main(sys.argv)
    if code:
        sys.exit(code)

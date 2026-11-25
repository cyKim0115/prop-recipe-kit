"""Run the prop kit in a fresh, isolated, headless Blender. Host Python 3.10+, stdlib only.

  python blender_run.py [--blender PATH] [--expect 5.2] [--strict-version] build <spec.json> --out <dir> [--no-render] [--no-blend]
  python blender_run.py [...] introspect bpy.ops.export_scene.fbx
  python blender_run.py [...] inspect-fbx <a.fbx> [<b.fbx> ...]
  python blender_run.py [...] version

Blender is found in this order: --blender PATH, env BLENDER_EXE, standard install locations
(newest version wins), `blender` on PATH. The kit was verified with Blender 5.2 LTS; another
version prints a warning (or stops with exit 2 when --strict-version is given).

Every run:
  -b --factory-startup   no UI, no user startup file, user add-ons not loaded
  --offline-mode         no internet access from Blender
  --python-exit-code 1   an uncaught Python error makes Blender exit 1 (otherwise it exits 0)
  BLENDER_USER_RESOURCES points at a kit-owned temp folder, so the user's Blender
                         preferences and add-ons are never read or written
Exit code: 0 pass, 1 check failed or Python error, 2 usage or setup error.
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
BL = os.path.join(HERE, "bl")
VERIFIED = "5.2"


def _version_key(path):
    m = re.search(r"Blender[ _-]?(\d+)\.(\d+)", path)
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


def find_blender(explicit=None):
    if explicit:
        return explicit if os.path.isfile(explicit) else None
    env = os.environ.get("BLENDER_EXE")
    if env:
        return env if os.path.isfile(env) else None
    candidates = []
    if os.name == "nt":
        for root in filter(None, [os.environ.get("ProgramFiles"), os.environ.get("ProgramW6432"),
                                  os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs")]):
            candidates += glob.glob(os.path.join(root, "Blender Foundation", "Blender*", "blender.exe"))
        candidates += glob.glob(r"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe")
    elif sys.platform == "darwin":
        candidates += glob.glob("/Applications/Blender*.app/Contents/MacOS/Blender")
    else:
        candidates += glob.glob("/opt/blender*/blender") + glob.glob(os.path.expanduser("~/blender*/blender"))
    candidates = sorted(set(candidates), key=_version_key, reverse=True)
    if candidates:
        return candidates[0]
    return shutil.which("blender")


class Ctx:
    exe = None
    expect = VERIFIED
    strict = False


def blender_version(exe):
    out = subprocess.run([exe, "--version"], capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    return out.splitlines()[0] if out else ""


def check_version():
    first = blender_version(Ctx.exe)
    print(first, "|", Ctx.exe)
    if not first.startswith(f"Blender {Ctx.expect}"):
        msg = f"[blender_run] this kit was verified with Blender {Ctx.expect}.x, found: {first!r}"
        if Ctx.strict:
            print(msg + " (stopping: --strict-version)")
            return 2
        print(msg + " (continuing; check results, or pass --expect / --strict-version)")
    return 0


def run(script, script_args, timeout=900):
    user = os.path.join(tempfile.gettempdir(), "prop-recipe-kit", "blender-user")
    os.makedirs(user, exist_ok=True)
    env = dict(os.environ, BLENDER_USER_RESOURCES=user, PYTHONIOENCODING="utf-8")
    cmd = [Ctx.exe, "-q", "-b", "--factory-startup", "--offline-mode",
           "--python-exit-code", "1", "--python", os.path.join(BL, script), "--", *script_args]
    proc = subprocess.run(cmd, env=env, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", timeout=timeout)
    for line in ((proc.stdout or "") + (proc.stderr or "")).splitlines():
        if line == "Blender quit" or (line.startswith("Blender ") and "(hash " in line):
            continue
        print(line)
    return proc.returncode


def main(argv):
    explicit = None
    rest = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--blender" and i + 1 < len(argv):
            explicit = argv[i + 1]; i += 2; continue
        if a == "--expect" and i + 1 < len(argv):
            Ctx.expect = argv[i + 1]; i += 2; continue
        if a == "--strict-version":
            Ctx.strict = True; i += 1; continue
        rest.append(a); i += 1
    if not rest or rest[0] in ("-h", "--help"):
        print(__doc__)
        return 2
    Ctx.exe = find_blender(explicit)
    if not Ctx.exe:
        print("[blender_run] Blender not found: pass --blender PATH or set BLENDER_EXE")
        return 2
    cmd, rest = rest[0], rest[1:]
    if cmd == "version":
        return check_version()
    if cmd == "build":
        if "--out" not in rest or len(rest) < 3:
            print("usage: blender_run.py build <spec.json> --out <dir> [--no-render] [--no-blend]")
            return 2
        k = rest.index("--out")
        out_dir = os.path.abspath(rest[k + 1])
        others = rest[:k] + rest[k + 2:]
        spec = [a for a in others if not a.startswith("--")]
        flags = [a for a in others if a.startswith("--")]
        if len(spec) != 1 or not os.path.isfile(spec[0]):
            print(f"[blender_run] spec file not found: {spec}")
            return 2
        if check_version():
            return 2
        return run("build_prop.py", [os.path.abspath(spec[0]), out_dir, *flags])
    if cmd == "introspect" and len(rest) == 1:
        return run("introspect.py", rest, timeout=120)
    if cmd == "inspect-fbx" and rest:
        return run("inspect_fbx.py", [os.path.abspath(p) for p in rest], timeout=300)
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

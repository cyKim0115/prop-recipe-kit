"""Print what an FBX holds (axes, unit, node transforms, geometry extent), inside Blender.

Read-only. Works on any binary FBX (e.g. an artist's existing files) to compare conventions.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import prop_export  # noqa: E402

if __name__ == "__main__":
    paths = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bad = 0
    for p in paths:
        try:
            info = prop_export.inspect(p)
            print(f"FBX {os.path.basename(p)} " + json.dumps(info, ensure_ascii=False))
        except Exception as e:  # ASCII FBX and broken files end up here
            bad += 1
            print(f"FBX {os.path.basename(p)} PARSE_FAIL {e}")
    if bad or not paths:
        sys.exit(1)

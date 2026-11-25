"""Print the real API of the installed Blender for one operator or type, inside Blender.

Use before writing bpy code you are not sure about: LLM memory mixes Blender versions.
  -- bpy.ops.export_scene.fbx     operator arguments, defaults, enum values
  -- bpy.types.Mesh               properties and functions of a type
  -- bmesh.ops.create_cone        docstring of a bmesh operator
"""
import sys

import bpy


def _enum(p):
    return [i.identifier for i in p.enum_items]


def describe_rna(rna, label):
    print(f"== {label}  (Blender {bpy.app.version_string})")
    if getattr(rna, "description", ""):
        print("   " + rna.description)
    for p in rna.properties:
        if p.identifier == "rna_type":
            continue
        line = f"  {p.identifier}: {p.type}"
        if p.type == "ENUM":
            line += f" {'set' if p.is_enum_flag else ''} {_enum(p)} default={p.default_flag if p.is_enum_flag else p.default!r}"
        elif p.type in ("FLOAT", "INT", "BOOLEAN") and getattr(p, "array_length", 0) == 0:
            line += f" default={p.default!r}"
        elif p.type == "STRING":
            line += f" default={p.default!r}"
        print(line)
    for fn in getattr(rna, "functions", []):
        args = ", ".join(a.identifier for a in fn.parameters if not a.is_output)
        print(f"  def {fn.identifier}({args})")


def main(target):
    if target.startswith("bpy.ops."):
        mod, op = target[len("bpy.ops."):].split(".", 1)
        try:
            rna = getattr(getattr(bpy.ops, mod), op).get_rna_type()
        except KeyError:
            print(f"MISSING operator {target} in Blender {bpy.app.version_string}")
            return 1
        describe_rna(rna, target)
    elif target.startswith("bpy.types."):
        t = getattr(bpy.types, target[len("bpy.types."):], None)
        if t is None:
            print(f"MISSING type {target} in Blender {bpy.app.version_string}")
            return 1
        describe_rna(t.bl_rna, target)
    elif target.startswith("bmesh.ops."):
        import bmesh

        fn = getattr(bmesh.ops, target[len("bmesh.ops."):], None)
        if fn is None:
            print(f"MISSING {target}")
            return 1
        print(f"== {target}\n{fn.__doc__}")
    else:
        print("target must start with bpy.ops., bpy.types. or bmesh.ops.")
        return 2
    return 0


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    code = main(a[0]) if a else 2
    if code:
        sys.exit(code)

"""Numeric acceptance checks for a built prop, inside Blender.

Measures the evaluated mesh in world space (what the exporter writes), not the
source parameters. Each check returns pass/fail with the measured value.
"""
import bpy

DEFAULT_TOLERANCE_M = 0.001


def _evaluated_mesh(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    return ev, ev.to_mesh()


def measure(obj):
    ev, mesh = _evaluated_mesh(obj)
    try:
        mw = ev.matrix_world
        cos = [mw @ v.co for v in mesh.vertices]
        mins = [min(c[i] for c in cos) for i in range(3)]
        maxs = [max(c[i] for c in cos) for i in range(3)]
        mesh.calc_loop_triangles()
        tris = len(mesh.loop_triangles)
        used_slots = sorted({p.material_index for p in mesh.polygons})
        edge_faces = {}
        for p in mesh.polygons:
            for ek in p.edge_keys:
                edge_faces[ek] = edge_faces.get(ek, 0) + 1
        loose_edges = sum(1 for e in mesh.edges if e.key not in edge_faces)
        verts_in_edges = {i for e in mesh.edges for i in e.vertices}
        loose_verts = sum(1 for v in mesh.vertices if v.index not in verts_in_edges)
        non_manifold_edges = sum(1 for n in edge_faces.values() if n != 2)
        degenerate = sum(1 for p in mesh.polygons if p.area < 1e-10)
        ngons = sum(1 for p in mesh.polygons if p.loop_total > 4)
        return {
            "dims_m": [round(maxs[i] - mins[i], 6) for i in range(3)],
            "min_m": [round(v, 6) for v in mins],
            "max_m": [round(v, 6) for v in maxs],
            "triangles": tris,
            "vertices": len(mesh.vertices),
            "faces": len(mesh.polygons),
            "material_slots": len(obj.material_slots),
            "material_slots_used": len(used_slots),
            "loose_vertices": loose_verts,
            "loose_edges": loose_edges,
            "degenerate_faces": degenerate,
            "ngons": ngons,
            "non_manifold_edges": non_manifold_edges,
            "location": [round(v, 6) for v in obj.location],
            "rotation_deg": [round(v * 57.29577951308232, 4) for v in obj.rotation_euler],
            "scale": [round(v, 6) for v in obj.scale],
            "has_modifiers": len(obj.modifiers) > 0,
            "uv_layers": len(mesh.uv_layers),
        }
    finally:
        ev.to_mesh_clear()


def check(obj, spec):
    """Return (metrics, checks). checks = list of {name, ok, detail}."""
    m = measure(obj)
    exp = spec.get("expect", {})
    tol = float(exp.get("tolerance_m", DEFAULT_TOLERANCE_M))
    checks = []

    def add(name, ok, detail):
        checks.append({"name": name, "ok": bool(ok), "detail": detail})

    if "dims_m" in exp:
        want = exp["dims_m"]
        diffs = [abs(m["dims_m"][i] - float(want[i])) for i in range(3)]
        add("dims", all(d <= tol for d in diffs),
            f"measured {m['dims_m']} vs expected {want} (tol {tol} m, max diff {max(diffs):.6f})")
    if "max_triangles" in exp:
        add("triangles", m["triangles"] <= int(exp["max_triangles"]),
            f"{m['triangles']} <= {exp['max_triangles']}")
    if "max_materials" in exp:
        add("materials", m["material_slots"] <= int(exp["max_materials"]),
            f"{m['material_slots']} slots <= {exp['max_materials']}")
    add("unused_material_slots", m["material_slots_used"] == m["material_slots"],
        f"{m['material_slots_used']} of {m['material_slots']} slots used")
    if spec.get("frame") == "unity":
        # recipe parts keep the prototype prefab pivot (footprint bottom centre), not the bbox centre
        add("origin_on_floor", abs(m["min_m"][2]) <= max(tol, 0.005), f"min z {m['min_m'][2]} (pivot kept from the prototype)")
    else:
      add("origin_bottom_center",
        abs(m["min_m"][2]) <= tol
        and abs(m["min_m"][0] + m["max_m"][0]) / 2 <= tol
        and abs(m["min_m"][1] + m["max_m"][1]) / 2 <= tol,
        f"min z {m['min_m'][2]}, center xy ({(m['min_m'][0] + m['max_m'][0]) / 2:.6f}, "
        f"{(m['min_m'][1] + m['max_m'][1]) / 2:.6f})")
    add("identity_transform",
        all(abs(v) < 1e-6 for v in m["location"])
        and all(abs(v) < 1e-4 for v in m["rotation_deg"])
        and all(abs(v - 1.0) < 1e-6 for v in m["scale"]),
        f"loc {m['location']} rot {m['rotation_deg']} scale {m['scale']}")
    add("no_live_modifiers", not m["has_modifiers"], "modifiers must be baked into the mesh")
    add("no_loose_geometry", m["loose_vertices"] == 0 and m["loose_edges"] == 0,
        f"loose verts {m['loose_vertices']}, loose edges {m['loose_edges']}")
    add("no_degenerate_faces", m["degenerate_faces"] == 0, f"{m['degenerate_faces']} faces with ~0 area")
    if exp.get("allow_ngons", True) is False:
        add("no_ngons", m["ngons"] == 0, f"{m['ngons']} faces with more than 4 corners")
    return m, checks

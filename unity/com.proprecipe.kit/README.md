# Prop Recipe Kit (`com.proprecipe.kit`)

Editor-only Unity package that builds low-poly props from code recipes.

- **Recipe DSL** (`PropParts`): rounded boxes, extruded plates (star, heart, flower, cloud, arch, half ring, round rect, shield, crescent, scallop, wave, polygon, bat, any outline), lathes (cylinder/cone, ball, dome, any profile, petal lobes), tori.
- **Bake** (`PropBaker`): generate every part at a chosen quality (`PropLod`), move it into prop space, drop triangles buried inside other closed parts, then build one mesh with a submesh per material or a single palette submesh with palette UVs.
- **Spec JSON** (`PropSpecJson`): write/read the same recipe format the Blender side (`blender/scripts/bl/prop_recipe.py`) reads. Floats round-trip bit-exactly.
- **Measure / capture** (`PropMeasure`, `PropCapture`): counts, memory, bounds, SHA1 geometry hash; 4-view ortho color + mask PNGs and turntable frames.
- **Materials** (`IPropMaterialProvider`): plug in your own shader. `PropDefaultMaterialProvider` uses the current render pipeline's default shader (or `Standard`).

Unity 6000.0+. Everything lives in the `PropRecipe` namespace inside the `PropRecipe.Editor` assembly (Editor platform only).

## Install

`Packages/manifest.json`:

```json
"com.proprecipe.kit": "file:../../prop-recipe-kit/unity/com.proprecipe.kit"
```

or a git URL with `?path=unity/com.proprecipe.kit`.

## Quick example

```csharp
using System.Collections.Generic;
using PropRecipe;
using UnityEngine;

var parts = new PropParts()
    .Cyl("wood", Vector3.zero, 0.26f, 0.44f, bevel: 0.06f, rTop: 0.228f, lobes: 9, amp: 0.04f, segments: 36)
    .Box("cream", new Vector3(0f, 0.47f, 0f), new Vector3(0.5f, 0.06f, 0.5f), r: 0.03f)
    .Star("gold", new Vector3(0f, 0.25f, 0.27f), 0.06f, 0.02f)
    .Point("Sit", new Vector3(0f, 0.5f, 0f));

var palette = new Dictionary<string, PropPaletteEntry>
{
    { "wood", PropPaletteEntry.FromHex("D98A55") },
    { "cream", PropPaletteEntry.FromHex("FFF1D6") },
    { "gold", PropPaletteEntry.FromHex("FFD13F", glow: 1.4f) },
};
PropSpec spec = parts.ToSpec("stool", palette);

// Bake at the reduced preset, one submesh per material
Mesh mesh = PropBaker.Bake(spec, PropLod.Reduced(), PropBakeMode.PerMaterial, out PropBakeReport report);
Debug.Log(report);
mesh = PropAssetUtil.SaveMesh(mesh, "Assets/Props/Meshes/PM_stool.asset");

// Materials and a capture
var provider = new PropDefaultMaterialProvider("Assets/Props/Materials");
Material[] mats = PropMaterials.ForBake(provider, report, spec.Palette, spec.Id);
PropCapture.Capture(mesh, mats, "Recordings/props", "stool__reduced");
Debug.Log(PropMeasure.Measure(mesh).ToJson("stool reduced"));

// Palette variant: one submesh + N x 1 texture
Mesh pm = PropBaker.Bake(spec, PropLod.Reduced(), PropBakeMode.Palette, out PropBakeReport pr);
Texture2D tex = PropPaletteTexture.Create(spec.Id, pr.MaterialOrder, spec.Palette, "Assets/Props/Textures");
Material[] pmats = PropMaterials.ForBake(provider, pr, spec.Palette, spec.Id, tex);

// Spec JSON (for Blender or to rebuild later)
PropSpecJson.WriteFile(spec, "Recordings/props/stool.json");
PropSpec again = PropSpecJson.ReadFile("Recordings/props/stool.json");
```

## API summary

| Type | What |
|---|---|
| `PropParts` | DSL. `Box`, `Plate`, `Star`, `Heart`, `FlowerPlate`, `Cloud`, `Arch`, `HalfRing`, `RoundRect`, `Shield`, `Crescent`, `Scallop`, `Wave`, `Polygon`, `Bat`, `Ring`, `Lathe`, `Cyl`, `Ball` (x2), `Dome`, `Custom`, `BeginGroup`/`EndGroup`, `Point` (`IP` alias), `ToSpec`. Material = string key. |
| `PropPart` | `Mat`, `Group` (-1 = body), `Matrix` (exact TRS), `Gen` (`PropGen`). |
| `PropGen*` | `PropGenRoundBox`, `PropGenExtrude`, `PropGenTorus`, `PropGenLathe`; `Build(PropLod)` returns a new mesh. |
| `PropMeshKit` | Raw generators `RoundBox`, `Lathe`, `Extrude`, `Torus` (all take an optional `PropLod`) and outline/profile helpers (`RoundCylProfile`, `SphereProfile`, `DomeProfile`, `StarOutline`, `PolygonOutline`, `HeartOutline`, `FlowerOutline`, `CloudOutline`, `ScallopStrip`, `WaveStrip`, `ArchOutline`, `HalfRingOutline`, `RoundRectOutline`, `ShieldOutline`, `CrescentOutline`, `BatOutline`, `Smooth`, `FitBox`). |
| `PropLod` | Reduction knobs (instance). `new PropLod()` = none, `PropLod.Reduced()` = pilot preset. |
| `PropBaker.Bake(parts, lod, mode, materialOrder, out report, meshName, includeGroups)` | Bake to one mesh. `materialOrder` null = first appearance. Group parts are skipped unless `includeGroups`. |
| `PropBaker.Combine(parts, lod, group, pivot, out materialOrder, meshName)` | Prototype prefab-builder path: `CombineMeshes` per material for one group (-1 = body), pivot-relative, no culling. |
| `PropBakeReport` | Tris before/after cull, culled, verts, submeshes, ms, material order. |
| `PropSpec` / `PropSpecJson` | Spec container and JSON `ToJson`/`FromJson`/`WriteFile`/`ReadFile`/`ReadGen`. |
| `PropMeasure.Measure(mesh)` | `PropMeasureResult` (tris, verts, submeshes, attributes, index format, runtime/file bytes, bounds size/center, hash) + `ToJson(label)`. |
| `PropCapture` | `Capture` (4 views color + mask), `Turntable`, `CreateMaskMaterial`. |
| `IPropMaterialProvider` | `GetMaterial(key, color, glow)`, `GetPaletteMaterial(propId, palette)`. `PropDefaultMaterialProvider(assetFolder)` is shader-neutral. |
| `PropPaletteTexture.Create` | N x 1 PNG (point, no mips, uncompressed, sRGB, clamp) in a given `Assets/...` folder. |
| `PropMaterials.ForBake` | Material array matching a bake's submeshes. |

## LOD presets

`PropLod` is passed into generation; nothing is global, so the same parts can be baked at several qualities.

| Field | None (default) | `Reduced()` | Effect |
|---|---|---|---|
| `BoxSegMax` | `int.MaxValue` | 1 | Rounded-box corner segments cap |
| `SegScale` / `SegMin` | 1 / 3 | 0.375 / 6 | Lathe and torus segments `max(SegMin, round(seg * SegScale))` |
| `ExtrudeArc` | -1 (keep) | 1 | Plate edge rounding steps |
| `ProfileStep` / `OutlineStep` | 1 / 1 | 1 / 1 | Keep every n-th profile/outline point (only when `Simplify` is 0) |
| `Simplify` | 0 | 0.01 | Douglas-Peucker on profiles and outlines, tolerance = bbox diagonal x value |

Lobed lathes keep at least 4 segments per lobe and a multiple of the lobe count when the scaled segment count differs from the requested one.

## Spec JSON

```json
{
  "id": "stool",
  "frame": "unity",
  "parts": [
    {
      "mat": "wood", "group": -1,
      "matrix": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1],
      "gen": {"type": "lathe", "profile": [[0,0],[0.26,0]], "segments": 36, "lobes": 9, "amp": 0.04, "sharp": false}
    }
  ],
  "palette": {"wood": {"hex": "#D98A55", "glow": 0}},
  "groups": [{"name": "Lid", "pivot": [0, 0.4, -0.2]}],
  "points": [{"name": "Sit", "pos": [0, 0.5, 0], "yaw": 0}]
}
```

- `matrix`: 16 numbers, row-major (`m[row, col]`), Unity frame (left-handed, Y up, front +Z).
- `gen` types and fields:
  - `roundbox` `{size:[x,y,z], radius, seg}`
  - `extrude` `{outline:[[x,y]...], depth, bevel, arc}`
  - `utorus` `{major, minor, seg_major, seg_minor, arc}` (`arc` in degrees)
  - `lathe` `{profile:[[r,y]...], segments, lobes, amp, sharp}`
- `palette` lists the keys used by the parts, in first-appearance order. Missing keys are written as white.
- `groups` and `points` are written only when present.
- Floats use the shortest invariant-culture string that parses back to the same bits (`-0` stays negative zero).

## Notes

- Geometry code is a verbatim port of the prototype generator. Do not "clean up" the math: bake output is compared by hash.
- `PropParts.ShareByKey` (default true) reproduces the prototype mesh cache: parts whose rounded key matches (for example two boxes whose sizes agree to 3 decimals) reuse the first part's generator. Turn it off to give every part its own exact generator.

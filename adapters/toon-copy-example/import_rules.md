# Import rules (example project: toon-shaded mobile game, Unity 6000.6, URP)

Verified 2026-11-25 with the kit's calibration mesh (`blender/examples/calib_axis.json`).

## FBX from the Blender kit
- Unity defaults are correct: Scale Factor 1, Use File Scale on, Bake Axis Conversion off, Normals = Import.
  Result: root rotation 0, scale 1, bounds = Blender (x, z, y) within 1 mm. Blender -Y becomes Unity +Z.
- Static props: Tangents None (no normal maps), Animation Type None, Import Animation off, Material Import None
  (materials are remapped by the adapter's material provider). Read/Write off, Mesh Compression off.
- FBX exported from Blender without Apply Transform can carry a root X=270 rotation; the kit exports
  identity nodes (UnitScaleFactor 100), so no correction child is needed.

## Facing
- Prefab convention: pivot = bottom centre of the footprint, front = +Z.
- If the game camera looks toward +Z, props placed in the world are rotated Y=180 to face it.

## Palette texture (single-material props)
- If the project's default TextureImporter preset is Sprite, override per file: Default, Point filter, no mipmaps,
  uncompressed, sRGB, Clamp. Otherwise colours bleed.

## Materials
- Never create new shaders. Copy an existing toon material and change colours only
  (see `ToonCopyMaterialProvider.cs`).
- Known limit: the toon shader takes one shaded (shadow) colour per material, so merging colours into one
  palette material shifts the look (one study: mean CIE76 dE 14-20 against per-colour materials). Keep
  per-colour materials when the look matters more than draw calls.

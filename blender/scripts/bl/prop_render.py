"""Contact sheet of a prop (6 views) rendered headless with Workbench, inside Blender.

Sheet layout (3 x 2), each cell CELL px:
  top row:    front (ortho, from -Y) | right (ortho, from +X) | top (ortho, from +Z)
  bottom row: perspective (front-right-above) | wireframe (same camera) | back (perspective, from +Y)
Saved as WebP quality 82 (media rule: compress on save). Cells are not labeled;
the order above is fixed.
"""
import math
import os
import tempfile

import bpy
import numpy as np
from mathutils import Vector

CELL = 480
VIEWS = ("front", "right", "top", "persp", "wire", "back")


def _camera(scene, name, ortho_scale=None):
    cam = bpy.data.cameras.new(name)
    if ortho_scale is not None:
        cam.type = "ORTHO"
        cam.ortho_scale = ortho_scale
    else:
        cam.lens = 50
    obj = bpy.data.objects.new(name, cam)
    scene.collection.objects.link(obj)
    return obj


def _aim(cam_obj, target, direction, distance):
    cam_obj.location = target + direction.normalized() * distance
    cam_obj.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()


def render_sheet(obj, out_path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    one_tex = len(obj.material_slots) == 1 and obj.material_slots[0].material is not None and any(
        n.type == "TEX_IMAGE" for n in obj.material_slots[0].material.node_tree.nodes)
    shading.color_type = "TEXTURE" if one_tex else "MATERIAL"
    shading.show_object_outline = True
    shading.show_cavity = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.62, 0.65, 0.63)
    scene.render.resolution_x = scene.render.resolution_y = CELL
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    # Blender 4.x+ defaults to AgX, which dulls flat colors; Standard shows the spec sRGB colors as given.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    dims = obj.dimensions
    center = Vector((0.0, 0.0, dims.z / 2.0))
    size = max(dims) or 1.0
    pad = 1.18

    wire = obj.copy()
    wire.data = obj.data.copy()
    wire.name = obj.name + "_wire"
    scene.collection.objects.link(wire)
    mod = wire.modifiers.new("Wire", "WIREFRAME")
    mod.thickness = max(size * 0.006, 0.002)
    mod.use_replace = True
    wire.hide_render = True

    tmp = tempfile.mkdtemp(prefix="prop_views_")
    paths = {}
    persp_dir = Vector((1.0, -1.4, 0.9))
    dist = size * 2.1
    setups = {
        "front": (Vector((0.0, -1.0, 0.0)), max(dims.x, dims.z)),
        "right": (Vector((1.0, 0.0, 0.0)), max(dims.y, dims.z)),
        "top": (Vector((0.0, 0.0, 1.0)), max(dims.x, dims.y)),
        "persp": (persp_dir, None),
        "wire": (persp_dir, None),
        "back": (Vector((-0.6, 1.4, 0.7)), None),
    }
    scene.render.image_settings.file_format = "PNG"
    for view in VIEWS:
        direction, extent = setups[view]
        cam = _camera(scene, "cam_" + view, None if extent is None else extent * pad)
        _aim(cam, center, direction, dist)
        if direction.z > 0.99:  # top view: keep +Y up in the image
            cam.rotation_euler = (0.0, 0.0, 0.0)
        scene.camera = cam
        obj.hide_render = view == "wire"
        wire.hide_render = view != "wire"
        scene.render.filepath = os.path.join(tmp, view + ".png")
        bpy.ops.render.render(write_still=True)
        paths[view] = scene.render.filepath
    obj.hide_render = False
    bpy.data.objects.remove(wire)

    sheet = np.zeros((2 * CELL, 3 * CELL, 4), dtype=np.float32)
    for i, view in enumerate(VIEWS):
        img = bpy.data.images.load(paths[view])
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(h, w, 4)
        row, col = divmod(i, 3)
        # Blender image rows go bottom-up: the top row of the sheet is the last block.
        y0 = (1 - row) * CELL
        sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px[:CELL, :CELL]
        bpy.data.images.remove(img)
        os.remove(paths[view])
    os.rmdir(tmp)

    out_img = bpy.data.images.new("sheet", width=3 * CELL, height=2 * CELL, alpha=False)
    out_img.pixels.foreach_set(sheet.ravel())
    scene.render.image_settings.file_format = "WEBP"
    scene.render.image_settings.quality = 82
    scene.render.image_settings.color_mode = "RGB"
    out_img.save_render(out_path, scene=scene)
    bpy.data.images.remove(out_img)
    return {"views": list(VIEWS), "cell_px": CELL, "bytes": os.path.getsize(out_path)}


def palette_png(spec, out_path):
    """count x 1 px palette texture matching the palette UVs (pixel-exact PNG on purpose)."""
    from prop_geometry import parse_color

    colors = [parse_color(c) for c in spec["materials"].values()]
    img = bpy.data.images.new("palette", width=len(colors), height=1, alpha=False)
    px = []
    for c in colors:
        px += [float(c[0]), float(c[1]), float(c[2]), 1.0]
    img.pixels.foreach_set(px)
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return os.path.getsize(out_path)

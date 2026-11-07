"""
Shared building blocks for the Blender generator scripts (make_models.py, make_starter_room.py).

Conventions (see docs/blender-export.md):
- 1 unit = 1 metre, Z up, origin at the floor contact point.
- Models face -Y (Blender's Front view); that becomes "forward" in the game.
- Wall-mounted props have their origin on the wall surface and extend towards -Y.
- Every helper applies rotation/scale, so exported models have clean transforms.
"""
import math
import random
import re
import json

import bpy

PALETTE = {
    # wood
    "log": (0.42, 0.26, 0.15, 1), "log_dark": (0.33, 0.20, 0.11, 1), "log_end": (0.62, 0.45, 0.28, 1),
    "chinking": (0.70, 0.63, 0.52, 1),
    "plank_a": (0.56, 0.38, 0.23, 1), "plank_b": (0.50, 0.33, 0.20, 1), "plank_c": (0.60, 0.42, 0.26, 1),
    "floor_gap": (0.18, 0.11, 0.07, 1), "beam": (0.27, 0.16, 0.09, 1), "ceiling": (0.36, 0.23, 0.14, 1),
    "bench_top": (0.64, 0.46, 0.29, 1), "frame": (0.25, 0.15, 0.08, 1),
    "crate_light": (0.92, 0.88, 0.80, 1), "crate_edge": (0.74, 0.68, 0.58, 1),  # neutral: tinted per toy in game
    # stone and fire
    "stone_a": (0.47, 0.45, 0.43, 1), "stone_b": (0.38, 0.37, 0.36, 1), "stone_c": (0.55, 0.52, 0.48, 1),
    "soot": (0.08, 0.06, 0.05, 1), "flame": (0.98, 0.55, 0.15, 1), "flame_core": (1.0, 0.85, 0.35, 1),
    # metal and glass
    "iron": (0.15, 0.15, 0.16, 1), "steel": (0.62, 0.64, 0.66, 1), "glass": (0.70, 0.84, 0.92, 1),
    "lamp_glow": (1.0, 0.82, 0.45, 1), "jar": (0.75, 0.82, 0.78, 1),
    # festive
    "berry": (0.73, 0.18, 0.20, 1), "gold": (0.91, 0.73, 0.34, 1), "snow": (0.96, 0.96, 0.98, 1),
    "pine": (0.12, 0.40, 0.25, 1), "pine_dark": (0.08, 0.30, 0.19, 1),
    "rug_a": (0.62, 0.20, 0.18, 1), "rug_b": (0.80, 0.66, 0.42, 1), "rug_c": (0.22, 0.38, 0.30, 1),
    "gift_a": (0.22, 0.45, 0.70, 1), "gift_b": (0.85, 0.85, 0.80, 1), "straw": (0.86, 0.72, 0.38, 1),
    "burlap": (0.66, 0.55, 0.38, 1),
    # characters and toys
    "skin": (0.95, 0.79, 0.63, 1), "cheek": (0.93, 0.58, 0.55, 1), "ink": (0.13, 0.16, 0.20, 1),
    "elf_green": (0.13, 0.42, 0.28, 1), "boot": (0.30, 0.18, 0.10, 1),
    "goblin_skin": (0.47, 0.62, 0.24, 1), "goblin_dark": (0.32, 0.45, 0.16, 1), "eye_white": (0.96, 0.94, 0.82, 1),
    "tunic": (0.50, 0.31, 0.16, 1), "patch": (0.78, 0.58, 0.28, 1),
    "ribbon_white": (0.97, 0.96, 0.95, 1), "ribbon_shade": (0.74, 0.73, 0.75, 1),  # neutral: tinted per colour in game
    "coal": (0.07, 0.07, 0.08, 1), "coal_shine": (0.16, 0.16, 0.18, 1), "ember": (1.0, 0.42, 0.12, 1),
    "bear_fur": (0.55, 0.36, 0.20, 1), "bear_light": (0.80, 0.64, 0.45, 1),
    "robot_body": (0.56, 0.79, 0.81, 1), "robot_dark": (0.38, 0.58, 0.62, 1),
    "train_red": (0.73, 0.25, 0.27, 1),
}


# Materials that glow (exported as glTF emission, so the game lights them from inside). Value = glow strength 0..1.
EMISSIVE = {"flame": 1.0, "flame_core": 1.0, "lamp_glow": 0.9, "glass": 0.35, "ember": 0.9}


def read_json(path):
    with open(path, encoding="utf-8") as f:
        text = re.sub(r"^\s*//.*$", "", f.read(), flags=re.MULTILINE)  # the game's JSON allows // comments
    return json.loads(text)


def new_file(collection_name):
    """Starts an empty file with one collection that holds the model (used for linking into the room)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    random.seed(7)
    col = bpy.data.collections.new(collection_name)
    bpy.context.scene.collection.children.link(col)
    return col


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    color = PALETTE[name]
    mat.diffuse_color = color
    if hasattr(mat, "use_nodes"):
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.9
        glow = EMISSIVE.get(name)
        if glow:
            bsdf.inputs["Emission Color"].default_value = tuple(c * glow for c in color[:3]) + (1,)
            bsdf.inputs["Emission Strength"].default_value = 1.0
    return mat


def light(name, x, y, z, color, intensity, radius, flicker, col):
    """
    A point light the game uses (room_layout.json). intensity ~1 is a lantern, ~3 a fireplace; radius is how far it
    reaches in metres; flicker 0..1 makes it waver (fire). Stored as custom properties, so Blender's own render
    settings don't matter; energy is also set so it looks similar in Blender's viewport.
    """
    data = bpy.data.lights.new(name, "POINT")
    data.color = color
    data.energy = intensity * 50
    data.shadow_soft_size = 0.2
    ob = bpy.data.objects.new(name, data)
    ob.location = (x, y, z)
    ob["intensity"], ob["radius"], ob["flicker"] = intensity, radius, flicker
    col.objects.link(ob)
    return ob


def finish(ob, name, mat, col):
    """Names the object, applies rotation and scale, adds a material and files it in a collection."""
    ob.name = name
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if mat:
        ob.data.materials.append(material(mat))
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    col.objects.link(ob)
    return ob


def box(name, x, y, z, w, d, h, mat, col, rot=(0, 0, 0)):
    """Box with its bottom at z, centred on x/y. w along X, d along Y, h along Z."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2), rotation=rot)
    ob = bpy.context.active_object
    ob.scale = (w, d, h)
    return finish(ob, name, mat, col)


def cyl(name, x, y, z, r, length, axis, mat, col, verts=8):
    """Cylinder centred at x/y/z, lying along 'X', 'Y' or standing along 'Z'."""
    rot = {"X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0), "Z": (0, 0, 0)}[axis]
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=length, location=(x, y, z), rotation=rot)
    return finish(bpy.context.active_object, name, mat, col)


def cone(name, x, y, z, r1, r2, depth, mat, col, verts=8, rot=(0, 0, 0)):
    """Cone/taper centred at x/y/z, pointing up (+Z) unless rotated."""
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=(x, y, z), rotation=rot)
    return finish(bpy.context.active_object, name, mat, col)


def sphere(name, x, y, z, r, mat, col, segments=10, rings=7, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=(x, y, z))
    ob = bpy.context.active_object
    ob.scale = scale
    return finish(ob, name, mat, col)


def torus(name, x, y, z, major, minor, mat, col, rot=(0, 0, 0), scale=(1, 1, 1), segments=(12, 6)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=segments[0],
                                     minor_segments=segments[1], location=(x, y, z), rotation=rot)
    ob = bpy.context.active_object
    ob.scale = scale
    return finish(ob, name, mat, col)


def disc(name, x, y, z, rx, ry, thickness, mat, col, verts=24):
    """A flat oval (rugs, lids)."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=1, depth=thickness, location=(x, y, z + thickness / 2))
    ob = bpy.context.active_object
    ob.scale = (rx, ry, 1)
    return finish(ob, name, mat, col)


def save(path):
    bpy.ops.wm.save_as_mainfile(filepath=path)

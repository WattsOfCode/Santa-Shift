"""
Builds a starter room.blend that matches the current layout, ready to redesign in Blender.

    blender --background --factory-startup --python make_starter_room.py -- <stations.json> <tuning.json> <out.blend>

Creates the floor, four 3 m walls with windows, a decorative stripe, a rug, a Christmas tree with an obstacle,
and the layout markers the export tool reads (floor_bounds, player_start, st_<id>, ip_<id>, col_<name>).
Markers go in a "Layout markers" collection and display as wireframes so they are easy to see and move.
"""
import json
import re
import sys

import bpy

ROOM_HEIGHT = 3.0
WALL = 0.3

PALETTE = {
    "floor": (0.79, 0.65, 0.49, 1), "plank": (0.74, 0.59, 0.44, 1),
    "wall": (0.13, 0.37, 0.30, 1), "wall_side": (0.11, 0.30, 0.24, 1),
    "berry": (0.73, 0.25, 0.27, 1), "gold": (0.91, 0.73, 0.34, 1),
    "ice": (0.56, 0.79, 0.81, 1), "wood_dark": (0.42, 0.27, 0.19, 1),
    "pine_tree": (0.10, 0.42, 0.27, 1), "snow": (0.96, 0.94, 0.86, 1),
}


def read_json(path):
    with open(path, encoding="utf-8") as f:
        text = re.sub(r"^\s*//.*$", "", f.read(), flags=re.MULTILINE)  # the game's JSON allows // comments
    return json.loads(text)


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
    return mat


def box(name, x, y, z, w, d, h, mat=None, collection=None):
    """A box whose bottom sits at z, centred on x/y. Transforms are applied (export rule 4)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (w, d, h)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if mat:
        ob.data.materials.append(material(mat))
    if collection:
        move_to(ob, collection)
    return ob


def empty(name, x, y, collection):
    bpy.ops.object.empty_add(type="SINGLE_ARROW", location=(x, y, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.empty_display_size = 0.4
    move_to(ob, collection)
    return ob


def move_to(ob, collection):
    for c in ob.users_collection:
        c.objects.unlink(ob)
    collection.objects.link(ob)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    stations_path, tuning_path, out_path = argv[0], argv[1], argv[2]
    stations = read_json(stations_path)["stations"]
    tuning = read_json(tuning_path)
    ppm = tuning.get("pixelsPerMetre", 40)
    origin = tuning.get("worldOrigin", {"x": 480, "y": 300})
    floor = tuning.get("floor", {"left": 20, "top": 120, "right": 940, "bottom": 480})

    def blender_xy(px, py):
        """Game pixels -> Blender metres. Game +Z (pixel y down) is Blender -Y."""
        return (px - origin["x"]) / ppm, -(py - origin["y"]) / ppm

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scenery = bpy.data.collections.new("Room")
    markers = bpy.data.collections.new("Layout markers")
    scene.collection.children.link(scenery)
    scene.collection.children.link(markers)

    x0, y1 = blender_xy(floor["left"], floor["top"])
    x1, y0 = blender_xy(floor["right"], floor["bottom"])
    w, d = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2

    # Floor with plank stripes (thin boxes so they export as real geometry).
    box("Floor", cx, cy, -0.02, w, d, 0.02, "floor", scenery)
    stripe_y = y0 + 0.5
    i = 0
    while stripe_y < y1:
        box(f"Plank.{i:02d}", cx, stripe_y, 0.0, w, 0.5, 0.002, "plank", scenery)
        stripe_y += 1.0
        i += 1

    # Walls (back = +Y, away from the starting camera).
    box("Wall back", cx, y1 + WALL / 2, 0, w + 2 * WALL, WALL, ROOM_HEIGHT, "wall", scenery)
    box("Wall front", cx, y0 - WALL / 2, 0, w + 2 * WALL, WALL, ROOM_HEIGHT, "wall", scenery)
    box("Wall left", x0 - WALL / 2, cy, 0, WALL, d, ROOM_HEIGHT, "wall_side", scenery)
    box("Wall right", x1 + WALL / 2, cy, 0, WALL, d, ROOM_HEIGHT, "wall_side", scenery)
    box("Stripe back", cx, y1 - 0.01, 1.3, w, 0.02, 0.25, "berry", scenery)
    box("Stripe front", cx, y0 + 0.01, 1.3, w, 0.02, 0.25, "berry", scenery)
    for n, wx in enumerate((-8.0, -3.0, 3.0, 8.0)):  # windows on the back wall
        box(f"Window frame.{n}", wx, y1 - 0.02, 1.7, 1.6, 0.04, 0.9, "wood_dark", scenery)
        box(f"Window pane.{n}", wx, y1 - 0.05, 1.78, 1.4, 0.02, 0.74, "ice", scenery)

    # Rug in the open walking space (flat, no obstacle).
    sx, sy = blender_xy(470, 400)
    box("Rug", sx, sy, 0.0, 4.0, 2.2, 0.01, "berry", scenery)
    box("Rug border", sx, sy, 0.0, 4.3, 2.5, 0.005, "gold", scenery)

    # Christmas tree in the back-right corner, with an obstacle around it.
    tx, ty = x1 - 1.0, y1 - 1.0
    box("Tree trunk", tx, ty, 0, 0.25, 0.25, 0.4, "wood_dark", scenery)
    for n, (r, z, h) in enumerate(((0.85, 0.35, 0.9), (0.65, 0.95, 0.8), (0.45, 1.5, 0.7))):
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=r, radius2=0, depth=h, location=(tx, ty, z + h / 2))
        cone = bpy.context.active_object
        cone.name = f"Tree.{n}"
        cone.data.materials.append(material("pine_tree"))
        move_to(cone, scenery)
    box("Tree star", tx, ty, 2.2, 0.18, 0.18, 0.18, "gold", scenery)
    box("col_tree", tx, ty, 0, 1.7, 1.7, 1.0, None, markers)

    # Layout markers from the current stations.json.
    box("floor_bounds", cx, cy, 0, w, d, 0.05, None, markers)
    px, py = tuning.get("playerStart", {"x": 470, "y": 410}).values()
    empty("player_start", *blender_xy(px, py), markers)
    for s in stations:
        b, ip = s["body"], s["interact"]
        bx, by = blender_xy(b["x"], b["y"])
        box("st_" + s["id"], bx, by, 0, b["w"] / ppm, b["h"] / ppm, 0.5, None, markers)
        empty("ip_" + s["id"], *blender_xy(ip["x"], ip["y"]), markers)

    for ob in markers.objects:  # wireframes: visible while editing, never exported as scenery
        ob.display_type = "WIRE"
        ob.hide_render = True

    bpy.ops.wm.save_as_mainfile(filepath=out_path)
    print(f"RESULT starter room saved -> {out_path} ({len(stations)} stations)")


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        print(f"ERROR {ex}")
        sys.exit(1)

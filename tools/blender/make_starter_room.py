"""
Builds room.blend: the log cabin shell, with every piece of furniture placed as a linked copy of its own model file.

    blender --background --factory-startup --python make_starter_room.py -- <stations.json> <tuning.json> <out.blend>

Run make_models.py first: the furniture (prop_*.blend) must exist next to room.blend.

What's in room.blend:
- "Cabin" collection: the shell only (log walls with notched corners, chinking, plank floor, beams, plank ceiling).
- "Props" collection: empties named pr_<model>.<n> that show a linked prop_<model>.blend. Move, rotate or duplicate
  them freely (Shift+D); editing prop_tree.blend updates every tree. Solid props block the elf where they stand.
- "Layout markers" collection: floor_bounds, player_start, st_<id> / ip_<id> per station (wireframes, never drawn).
Everything is deterministic (fixed random seed), so re-running gives the same room.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from helpers import box, cyl, read_json, save  # noqa: E402

import bpy  # noqa: E402

ROOM_HEIGHT = 3.6
LOG_R = 0.15


def log_walls(x0, x1, y0, y1, col):
    """Stacked logs on all four walls. Courses alternate which walls stick out past the corners (notched look)."""
    w, d = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    overhang = 0.35
    box("Chinking back", cx, y1 + LOG_R * 2, 0, w + 1, 0.05, ROOM_HEIGHT, "chinking", col)
    box("Chinking front", cx, y0 - LOG_R * 2, 0, w + 1, 0.05, ROOM_HEIGHT, "chinking", col)
    box("Chinking left", x0 - LOG_R * 2, cy, 0, 0.05, d + 1, ROOM_HEIGHT, "chinking", col)
    box("Chinking right", x1 + LOG_R * 2, cy, 0, 0.05, d + 1, ROOM_HEIGHT, "chinking", col)
    z, course = LOG_R, 0
    while z < ROOM_HEIGHT - LOG_R * 0.5:
        long_out = overhang if course % 2 == 0 else 0
        side_out = overhang if course % 2 == 1 else 0
        mat = "log" if course % 3 else "log_dark"
        cyl(f"Log back.{course:02d}", cx, y1 + LOG_R, z, LOG_R, w + 2 * LOG_R + 2 * long_out, "X", mat, col)
        cyl(f"Log front.{course:02d}", cx, y0 - LOG_R, z, LOG_R, w + 2 * LOG_R + 2 * long_out, "X", mat, col)
        cyl(f"Log left.{course:02d}", x0 - LOG_R, cy, z, LOG_R, d + 2 * LOG_R + 2 * side_out, "Y", mat, col)
        cyl(f"Log right.{course:02d}", x1 + LOG_R, cy, z, LOG_R, d + 2 * LOG_R + 2 * side_out, "Y", mat, col)
        z += LOG_R * 1.9
        course += 1


def plank_floor(x0, x1, y0, y1, col):
    box("Floor base", (x0 + x1) / 2, (y0 + y1) / 2, -0.03, x1 - x0, y1 - y0, 0.025, "floor_gap", col)
    width, y, row = 0.3, y0 + 0.15, 0
    while y < y1:
        x = x0 + random.uniform(0, 1.5) if row % 2 else x0
        seg = 0
        while x < x1 - 0.05:
            length = min(random.uniform(2.0, 4.0), x1 - x)
            box(f"Plank.{row:02d}.{seg}", x + length / 2, y, -0.02, length - 0.015, width - 0.02, 0.02,
                random.choice(("plank_a", "plank_b", "plank_c")), col)
            x += length
            seg += 1
        y += width
        row += 1


def ceiling(x0, x1, y0, y1, col):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    box("Ceiling", cx, cy, ROOM_HEIGHT, x1 - x0 + 1, y1 - y0 + 1, 0.08, "ceiling", col)
    box("Ridge beam", cx, cy, ROOM_HEIGHT - 0.35, x1 - x0 + 0.6, 0.3, 0.3, "beam", col)
    for n, bx in enumerate((-9.0, -6.0, -3.0, 0.0, 3.0, 6.0, 9.0)):
        box(f"Beam.{n}", bx, cy, ROOM_HEIGHT - 0.3, 0.25, y1 - y0 + 0.6, 0.3, "beam", col)


class PropPlacer:
    """Links prop_<name>.blend collections from the room's folder and places instances of them."""

    def __init__(self, folder, col):
        self.folder, self.col, self.cache, self.counts = folder, col, {}, {}

    def place(self, model, x, y, yaw_deg=0.0):
        coll = self.cache.get(model)
        if coll is None:
            path = os.path.join(self.folder, model + ".blend")
            if not os.path.exists(path):
                raise FileNotFoundError(f"{path} is missing; run make_models.py first")
            with bpy.data.libraries.load(path, link=True, relative=True) as (src, dst):
                dst.collections = [model]
            coll = self.cache[model] = dst.collections[0]
        n = self.counts.get(model, 0)
        self.counts[model] = n + 1
        ob = bpy.data.objects.new(f"pr_{model}.{n:02d}", None)
        ob.instance_type = "COLLECTION"
        ob.instance_collection = coll
        ob.location = (x, y, 0)
        ob.rotation_euler = (0, 0, math.radians(yaw_deg))
        self.col.objects.link(ob)
        return ob


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    stations_path, tuning_path, out_path = argv[0], argv[1], os.path.abspath(argv[2])
    stations = read_json(stations_path)["stations"]
    tuning = read_json(tuning_path)
    ppm = tuning.get("pixelsPerMetre", 40)
    origin = tuning.get("worldOrigin", {"x": 480, "y": 300})
    floor = tuning.get("floor", {"left": 20, "top": 120, "right": 940, "bottom": 480})

    def blender_xy(px, py):
        """Game pixels -> Blender metres. Game +Z (pixel y down) is Blender -Y."""
        return (px - origin["x"]) / ppm, -(py - origin["y"]) / ppm

    bpy.ops.wm.read_factory_settings(use_empty=True)
    random.seed(7)
    save(out_path)  # saved first so linked props get paths relative to room.blend
    scene = bpy.context.scene
    shell = bpy.data.collections.new("Cabin")
    props = bpy.data.collections.new("Props")
    markers = bpy.data.collections.new("Layout markers")
    for c in (shell, props, markers):
        scene.collection.children.link(c)

    x0, y1 = blender_xy(floor["left"], floor["top"])
    x1, y0 = blender_xy(floor["right"], floor["bottom"])
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2

    plank_floor(x0, x1, y0, y1, shell)
    log_walls(x0, x1, y0, y1, shell)
    ceiling(x0, x1, y0, y1, shell)

    # Furniture. Wall props face into the room: back wall 0, front 180, left 90, right -90 degrees.
    p = PropPlacer(os.path.dirname(out_path), props)
    p.place("prop_fireplace", x0, 0, 90)
    p.place("prop_firewood", x0 + 0.35, -2.1)
    p.place("prop_tree", x1 - 1.05, y1 - 1.05)
    p.place("prop_barrels", x0 + 0.55, y0 + 0.95)
    p.place("prop_sled", x1 - 0.7, y0 + 1.2)
    p.place("prop_rug", *blender_xy(470, 390))
    for wx in (-4.5, 1.5, 7.5):
        p.place("prop_window", wx, y1, 0)
    for wx in (-4.5, 0.0, 4.5):
        p.place("prop_window", wx, y0, 180)
    for (lx, ly, yaw) in ((-3.0, y1, 0), (6.0, y1, 0), (-2.25, y0, 180), (2.25, y0, 180), (x0, 2.4, 90), (x1, -1.5, -90)):
        p.place("prop_lantern", lx, ly, yaw)
    p.place("prop_wreath", -1.5, y1, 0)
    p.place("prop_shelf", -7.5, y1, 0)
    p.place("prop_shelf", 4.5, y1, 0)
    p.place("prop_clock", x1, 1.5, -90)

    # Layout markers from the current stations.json.
    box("floor_bounds", cx, cy, 0, x1 - x0, y1 - y0, 0.05, None, markers)
    start = tuning.get("playerStart", {"x": 470, "y": 410})
    bpy.ops.object.empty_add(type="SINGLE_ARROW", location=(*blender_xy(start["x"], start["y"]), 0))
    marker_objects = [bpy.context.active_object]
    marker_objects[0].name = "player_start"
    for s in stations:
        b, ip = s["body"], s["interact"]
        bx, by = blender_xy(b["x"], b["y"])
        box("st_" + s["id"], bx, by, 0, b["w"] / ppm, b["h"] / ppm, 0.5, None, markers)
        bpy.ops.object.empty_add(type="SINGLE_ARROW", location=(*blender_xy(ip["x"], ip["y"]), 0))
        e = bpy.context.active_object
        e.name = "ip_" + s["id"]
        marker_objects.append(e)
    for e in marker_objects:
        e.empty_display_size = 0.4
        for c in list(e.users_collection):
            c.objects.unlink(e)
        markers.objects.link(e)
    for ob in markers.objects:  # wireframes: visible while editing, never exported as scenery
        ob.display_type = "WIRE"
        ob.hide_render = True

    save(out_path)
    print(f"RESULT cabin room saved -> {out_path} ({len(shell.objects)} shell pieces, {len(props.objects)} props, "
          f"{len(stations)} stations)")


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print(f"ERROR {ex}")
        sys.exit(1)

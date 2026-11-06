"""
Builds room.blend: a rustic log cabin workshop around the current station layout, ready to redesign in Blender.

    blender --background --factory-startup --python make_starter_room.py -- <stations.json> <tuning.json> <out.blend>

Scenery: log walls with notched corners, plank floor, exposed beams and plank ceiling, a stone fireplace with fire,
mantel and stockings, firewood, framed windows with snowy sills, a wreath, shelves, a braided rug, a decorated tree
with presents, barrels, a sled, a cuckoo clock and wall lanterns.

Layout markers (read by the export tool, never drawn) go in the "Layout markers" collection as wireframes:
floor_bounds, player_start, st_<id> / ip_<id> per station, and col_<name> around furniture.
Everything is deterministic (fixed random seed), so re-running gives the same room.
"""
import json
import math
import random
import re
import sys

import bpy

ROOM_HEIGHT = 3.6
LOG_R = 0.15
random.seed(7)

PALETTE = {
    "log": (0.42, 0.26, 0.15, 1), "log_dark": (0.33, 0.20, 0.11, 1), "log_end": (0.62, 0.45, 0.28, 1),
    "chinking": (0.70, 0.63, 0.52, 1),
    "plank_a": (0.56, 0.38, 0.23, 1), "plank_b": (0.50, 0.33, 0.20, 1), "plank_c": (0.60, 0.42, 0.26, 1),
    "floor_gap": (0.18, 0.11, 0.07, 1),
    "beam": (0.27, 0.16, 0.09, 1), "ceiling": (0.36, 0.23, 0.14, 1),
    "stone_a": (0.47, 0.45, 0.43, 1), "stone_b": (0.38, 0.37, 0.36, 1), "stone_c": (0.55, 0.52, 0.48, 1),
    "soot": (0.08, 0.06, 0.05, 1), "flame": (0.98, 0.55, 0.15, 1), "flame_core": (1.0, 0.85, 0.35, 1),
    "frame": (0.25, 0.15, 0.08, 1), "glass": (0.70, 0.84, 0.92, 1), "snow": (0.96, 0.96, 0.98, 1),
    "berry": (0.73, 0.18, 0.20, 1), "gold": (0.91, 0.73, 0.34, 1), "pine": (0.12, 0.40, 0.25, 1),
    "pine_dark": (0.08, 0.30, 0.19, 1), "rug_a": (0.62, 0.20, 0.18, 1), "rug_b": (0.80, 0.66, 0.42, 1),
    "rug_c": (0.22, 0.38, 0.30, 1), "iron": (0.15, 0.15, 0.16, 1), "lamp_glow": (1.0, 0.82, 0.45, 1),
    "jar": (0.75, 0.82, 0.78, 1), "gift_a": (0.22, 0.45, 0.70, 1), "gift_b": (0.85, 0.85, 0.80, 1),
}


# ---------------------------------------------------------------- helpers

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


def finish(ob, name, mat, collection):
    """Name it, apply rotation/scale (export rule 4), give it a material, file it in a collection."""
    ob.name = name
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if mat:
        ob.data.materials.append(material(mat))
    for c in ob.users_collection:
        c.objects.unlink(ob)
    collection.objects.link(ob)
    return ob


def box(name, x, y, z, w, d, h, mat, col):
    """Box with its bottom at z, centred on x/y. w along X, d along Y, h along Z."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2))
    ob = bpy.context.active_object
    ob.scale = (w, d, h)
    return finish(ob, name, mat, col)


def cyl(name, x, y, z, r, length, axis, mat, col, verts=8, r2=None):
    """Cylinder centred at x/y/z along axis 'X', 'Y' or 'Z'. r2 makes it a cone/taper (Z only)."""
    rot = {"X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0), "Z": (0, 0, 0)}[axis]
    if r2 is not None:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=length, location=(x, y, z), rotation=rot)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=length, location=(x, y, z), rotation=rot)
    return finish(bpy.context.active_object, name, mat, col)


def sphere(name, x, y, z, r, mat, col, segments=8, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=(x, y, z))
    return finish(bpy.context.active_object, name, mat, col)


def empty(name, x, y, col):
    bpy.ops.object.empty_add(type="SINGLE_ARROW", location=(x, y, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.empty_display_size = 0.4
    for c in ob.users_collection:
        c.objects.unlink(ob)
    col.objects.link(ob)
    return ob


# ---------------------------------------------------------------- room parts

def log_walls(x0, x1, y0, y1, col):
    """Stacked logs on all four walls. Courses alternate which walls stick out past the corners (notched look)."""
    w, d = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    overhang = 0.35
    # Filler behind the logs, so gaps between them read as chinking.
    box("Chinking back", cx, y1 + LOG_R * 2, 0, w + 1, 0.05, ROOM_HEIGHT, "chinking", col)
    box("Chinking front", cx, y0 - LOG_R * 2, 0, w + 1, 0.05, ROOM_HEIGHT, "chinking", col)
    box("Chinking left", x0 - LOG_R * 2, cy, 0, 0.05, d + 1, ROOM_HEIGHT, "chinking", col)
    box("Chinking right", x1 + LOG_R * 2, cy, 0, 0.05, d + 1, ROOM_HEIGHT, "chinking", col)

    step = LOG_R * 1.9
    course = 0
    z = LOG_R
    while z < ROOM_HEIGHT - LOG_R * 0.5:
        long_out = overhang if course % 2 == 0 else 0
        side_out = overhang if course % 2 == 1 else 0
        mat = "log" if course % 3 else "log_dark"
        cyl(f"Log back.{course:02d}", cx, y1 + LOG_R, z, LOG_R, w + 2 * LOG_R + 2 * long_out, "X", mat, col)
        cyl(f"Log front.{course:02d}", cx, y0 - LOG_R, z, LOG_R, w + 2 * LOG_R + 2 * long_out, "X", mat, col)
        cyl(f"Log left.{course:02d}", x0 - LOG_R, cy, z, LOG_R, d + 2 * LOG_R + 2 * side_out, "Y", mat, col)
        cyl(f"Log right.{course:02d}", x1 + LOG_R, cy, z, LOG_R, d + 2 * LOG_R + 2 * side_out, "Y", mat, col)
        z += step
        course += 1


def plank_floor(x0, x1, y0, y1, col):
    box("Floor base", (x0 + x1) / 2, (y0 + y1) / 2, -0.03, x1 - x0, y1 - y0, 0.025, "floor_gap", col)
    width = 0.3
    y = y0 + width / 2
    row = 0
    while y < y1:
        x = x0 + random.uniform(0, 1.5) if row % 2 else x0
        seg = 0
        while x < x1 - 0.05:
            length = min(random.uniform(2.0, 4.0), x1 - x)
            mat = random.choice(("plank_a", "plank_b", "plank_c"))
            box(f"Plank.{row:02d}.{seg}", x + length / 2, y, -0.02, length - 0.015, width - 0.02, 0.02, mat, col)
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


def lanterns(x0, x1, y0, y1, col):
    """Wall lanterns on iron brackets. Kept on the walls, out of the camera's path through the room."""
    spots = [  # (x, y, direction into the room as (dx, dy))
        (-3.0, y1, (0, -1)), (6.0, y1, (0, -1)),            # back wall, between windows and shelves
        (-2.25, y0, (0, 1)), (2.25, y0, (0, 1)),            # front wall, between windows
        (x0, 2.4, (1, 0)), (x1, -1.5, (-1, 0)),             # side walls
    ]
    for n, (x, y, (dx, dy)) in enumerate(spots):
        arm = 0.28
        ax, ay = x + dx * arm / 2, y + dy * arm / 2
        lx, ly = x + dx * arm, y + dy * arm
        box(f"Lantern bracket.{n}", ax, ay, 2.55, abs(dx) * arm + 0.04, abs(dy) * arm + 0.04, 0.04, "iron", col)
        box(f"Lantern cap.{n}", lx, ly, 2.42, 0.18, 0.18, 0.06, "iron", col)
        box(f"Lantern glass.{n}", lx, ly, 2.18, 0.14, 0.14, 0.24, "lamp_glow", col)
        box(f"Lantern base.{n}", lx, ly, 2.13, 0.18, 0.18, 0.05, "iron", col)


def window(name, x, y, z, w, h, facing_y, col):
    """A framed window on a back/front wall. facing_y: -1 if the room is towards -Y (back wall), +1 for the front wall."""
    t, inset = 0.08, facing_y * 0.04
    yy = y + inset
    box(f"{name} glass", x, yy - facing_y * 0.01, z, w, 0.02, h, "glass", col)
    box(f"{name} frame top", x, yy, z + h - t, w + t, 0.08, t, "frame", col)
    box(f"{name} frame bottom", x, yy, z, w + t, 0.08, t, "frame", col)
    box(f"{name} frame left", x - w / 2, yy, z, t, 0.08, h, "frame", col)
    box(f"{name} frame right", x + w / 2, yy, z, t, 0.08, h, "frame", col)
    box(f"{name} mullion v", x, yy, z, 0.05, 0.09, h, "frame", col)
    box(f"{name} mullion h", x, yy, z + h / 2, w, 0.09, 0.05, "frame", col)
    box(f"{name} sill", x, y + facing_y * 0.14, z - 0.06, w + 0.3, 0.25, 0.06, "frame", col)
    box(f"{name} snow", x, yy - facing_y * 0.02, z + t, w - 0.05, 0.03, 0.06, "snow", col)


def fireplace(x0, col, markers):
    """Stone fireplace against the left wall, centred on y=0."""
    depth, width = 0.8, 2.6
    fx = x0 + depth / 2
    box("Chimney core", fx, 0, 0, depth, width, ROOM_HEIGHT, "stone_b", col)
    # Stones on the room-facing side.
    face = x0 + depth + 0.03
    z = 0.0
    row = 0
    while z < ROOM_HEIGHT - 0.1:
        h = random.uniform(0.22, 0.32)
        y = -width / 2 + (0.15 if row % 2 else 0)
        while y < width / 2 - 0.05:
            sw = min(random.uniform(0.3, 0.55), width / 2 - y)
            box(f"Stone.{row:02d}.{y:+.2f}", face, y + sw / 2, z, 0.08, sw - 0.03, h - 0.03,
                random.choice(("stone_a", "stone_b", "stone_c")), col)
            y += sw
        z += h
        row += 1
    # Firebox opening, logs and flames.
    box("Firebox", face + 0.05, 0, 0, 0.05, 1.2, 0.9, "soot", col)
    box("Firebox arch", face + 0.06, 0, 0.9, 0.08, 1.4, 0.12, "stone_c", col)
    cyl("Fire log.0", face + 0.2, -0.1, 0.1, 0.07, 0.8, "Y", "log_dark", col)
    cyl("Fire log.1", face + 0.3, 0.1, 0.1, 0.07, 0.8, "Y", "log", col)
    for n, (fy, fh) in enumerate(((-0.25, 0.45), (0.0, 0.6), (0.25, 0.4))):
        cyl(f"Flame.{n}", face + 0.25, fy, 0.15 + fh / 2, 0.12, fh, "Z", "flame", col, verts=6, r2=0.0)
        cyl(f"Flame core.{n}", face + 0.28, fy, 0.15 + fh * 0.35, 0.06, fh * 0.6, "Z", "flame_core", col, verts=6, r2=0.0)
    # Hearth slab, mantel and stockings.
    box("Hearth", face + 0.25, 0, 0, 0.55, width + 0.2, 0.06, "stone_c", col)
    box("Mantel", face + 0.12, 0, 1.3, 0.3, width + 0.3, 0.12, "beam", col)
    for n, sy in enumerate((-0.8, 0.0, 0.8)):
        box(f"Stocking.{n}", face + 0.1, sy, 0.9, 0.06, 0.18, 0.38, "berry" if n != 1 else "pine", col)
        box(f"Stocking cuff.{n}", face + 0.1, sy, 1.22, 0.07, 0.2, 0.08, "snow", col)
    sphere("Mantel clock", face + 0.1, 0.0, 1.5, 0.1, "gold", col)
    box("col_fireplace", x0 + 0.65, 0, 0, 1.3, width + 0.3, 1.0, None, markers)


def firewood(x0, y_start, col, markers):
    for row in range(4):
        for n in range(5 - row % 2):
            cyl(f"Firewood.{row}.{n}", x0 + 0.3, y_start - n * 0.17 - (row % 2) * 0.085, 0.08 + row * 0.15,
                0.075, 0.6, "X", random.choice(("log", "log_dark", "log_end")), col, verts=7)
    box("col_firewood", x0 + 0.3, y_start - 0.35, 0, 0.7, 1.0, 1.0, None, markers)


def tree(tx, ty, col, markers):
    cyl("Tree trunk", tx, ty, 0.2, 0.12, 0.4, "Z", "log_dark", col)
    tiers = ((0.95, 0.35, 0.9), (0.75, 0.95, 0.8), (0.55, 1.5, 0.7), (0.35, 1.95, 0.55))
    for n, (r, z, h) in enumerate(tiers):
        cyl(f"Tree.{n}", tx, ty, z + h / 2, r, h, "Z", "pine" if n % 2 else "pine_dark", col, verts=10, r2=0.0)
    box("Tree star", tx, ty, 2.5, 0.2, 0.2, 0.2, "gold", col)
    for n in range(14):
        a = n * 2.4
        z = 0.45 + (n % 7) * 0.27
        r = 0.95 - (z - 0.35) * 0.42
        sphere(f"Ornament.{n}", tx + math.cos(a) * r, ty + math.sin(a) * r, z, 0.06,
               ("berry", "gold", "gift_a")[n % 3], col, segments=6, rings=4)
    for n, (gx, gy, s, m) in enumerate(((-0.7, -0.6, 0.35, "gift_a"), (0.0, -0.85, 0.3, "berry"), (-0.9, 0.1, 0.28, "gift_b"))):
        box(f"Gift.{n}", tx + gx, ty + gy, 0, s, s, s * 0.8, m, col)
        box(f"Gift ribbon.{n}", tx + gx, ty + gy, 0, s + 0.01, 0.05, s * 0.8 + 0.01, "gold", col)
    box("col_tree", tx - 0.1, ty - 0.1, 0, 2.1, 2.1, 1.0, None, markers)


def barrels(bx, by, col, markers):
    for n, (dx, dy) in enumerate(((0, 0), (0, 0.85))):
        cyl(f"Barrel.{n}", bx + dx, by + dy, 0.45, 0.36, 0.9, "Z", "log", col, verts=12)
        for k, hz in enumerate((0.15, 0.75)):
            cyl(f"Barrel hoop.{n}.{k}", bx + dx, by + dy, hz, 0.37, 0.05, "Z", "iron", col, verts=12)
        cyl(f"Barrel lid.{n}", bx + dx, by + dy, 0.9, 0.33, 0.02, "Z", "log_end", col, verts=12)
    box("Crate on barrels", bx, by + 0.4, 0.9, 0.5, 0.5, 0.4, "plank_c", col)
    box("col_barrels", bx, by + 0.42, 0, 0.9, 1.8, 1.0, None, markers)


def sled(sx, sy, col, markers):
    for n, dx in enumerate((-0.22, 0.22)):
        box(f"Sled runner.{n}", sx + dx, sy, 0, 0.05, 1.4, 0.06, "berry", col)
        cyl(f"Sled curl.{n}", sx + dx, sy + 0.72, 0.15, 0.12, 0.05, "X", "berry", col, verts=10)
    box("Sled deck", sx, sy, 0.2, 0.6, 1.25, 0.05, "plank_a", col)
    for n, dy in enumerate((-0.45, 0.0, 0.45)):
        box(f"Sled post.{n}", sx, sy + dy, 0.05, 0.5, 0.05, 0.15, "plank_b", col)
    box("col_sled", sx, sy + 0.05, 0, 0.8, 1.6, 1.0, None, markers)


def wall_decor(x0, x1, y1, col):
    # Wreath above the middle kit bin.
    bpy.ops.mesh.primitive_torus_add(major_radius=0.35, minor_radius=0.09, major_segments=12, minor_segments=6,
                                     location=(-1.5, y1 - 0.05, 2.45), rotation=(math.pi / 2, 0, 0))
    finish(bpy.context.active_object, "Wreath", "pine", col)
    box("Wreath bow", -1.5, y1 - 0.12, 2.0, 0.22, 0.05, 0.14, "berry", col)
    # Shelves with jars above the outer kit bins.
    for n, sx in enumerate((-7.5, 4.5)):
        box(f"Shelf.{n}", sx, y1 - 0.18, 2.05, 1.6, 0.3, 0.05, "beam", col)
        for k in range(4):
            cyl(f"Jar.{n}.{k}", sx - 0.55 + k * 0.37, y1 - 0.18, 2.18, 0.07, 0.22, "Z",
                ("jar", "berry", "gold", "jar")[k], col, verts=8)
    # Cuckoo clock on the right wall.
    box("Clock body", x1 - 0.06, 1.5, 1.9, 0.12, 0.4, 0.5, "beam", col)
    box("Clock roof", x1 - 0.09, 1.5, 2.4, 0.18, 0.55, 0.08, "log_dark", col)
    cyl("Clock face", x1 - 0.13, 1.5, 2.12, 0.12, 0.02, "X", "snow", col, verts=12)
    cyl("Clock weight.0", x1 - 0.08, 1.42, 1.55, 0.03, 0.3, "Z", "gold", col, verts=6)
    cyl("Clock weight.1", x1 - 0.08, 1.58, 1.6, 0.03, 0.25, "Z", "gold", col, verts=6)


def braided_rug(rx, ry, col):
    for n, (sx, sy, mat) in enumerate(((2.3, 1.5, "rug_b"), (1.95, 1.2, "rug_a"), (1.55, 0.92, "rug_c"),
                                       (1.15, 0.65, "rug_a"), (0.7, 0.38, "rug_b"))):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1, depth=0.006, location=(rx, ry, 0.003 + n * 0.002))
        ob = bpy.context.active_object
        ob.scale = (sx, sy, 1)
        finish(ob, f"Rug.{n}", mat, col)


# ---------------------------------------------------------------- main

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
    scenery = bpy.data.collections.new("Cabin")
    markers = bpy.data.collections.new("Layout markers")
    scene.collection.children.link(scenery)
    scene.collection.children.link(markers)

    x0, y1 = blender_xy(floor["left"], floor["top"])
    x1, y0 = blender_xy(floor["right"], floor["bottom"])
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2

    plank_floor(x0, x1, y0, y1, scenery)
    log_walls(x0, x1, y0, y1, scenery)
    ceiling(x0, x1, y0, y1, scenery)
    lanterns(x0, x1, y0, y1, scenery)

    for n, wx in enumerate((-4.5, 1.5, 7.5)):          # back wall, between the kit bins
        window(f"Window back.{n}", wx, y1, 1.35, 1.3, 1.1, -1, scenery)
    for n, wx in enumerate((-4.5, 0.0, 4.5)):          # front wall
        window(f"Window front.{n}", wx, y0, 1.35, 1.3, 1.1, +1, scenery)

    fireplace(x0, scenery, markers)
    firewood(x0, -1.75, scenery, markers)
    tree(x1 - 1.05, y1 - 1.05, scenery, markers)
    barrels(x0 + 0.55, y0 + 0.55, scenery, markers)
    sled(x1 - 0.7, y0 + 1.2, scenery, markers)
    wall_decor(x0, x1, y1, scenery)
    braided_rug(*blender_xy(470, 390), scenery)

    # Layout markers from the current stations.json.
    box("floor_bounds", cx, cy, 0, x1 - x0, y1 - y0, 0.05, None, markers)
    start = tuning.get("playerStart", {"x": 470, "y": 410})
    empty("player_start", *blender_xy(start["x"], start["y"]), markers)
    for s in stations:
        b, ip = s["body"], s["interact"]
        bx, by = blender_xy(b["x"], b["y"])
        box("st_" + s["id"], bx, by, 0, b["w"] / ppm, b["h"] / ppm, 0.5, None, markers)
        empty("ip_" + s["id"], *blender_xy(ip["x"], ip["y"]), markers)

    for ob in markers.objects:  # wireframes: visible while editing, never exported as scenery
        ob.display_type = "WIRE"
        ob.hide_render = True

    bpy.ops.wm.save_as_mainfile(filepath=out_path)
    count = len(scenery.objects)
    print(f"RESULT cabin room saved -> {out_path} ({count} scenery objects, {len(stations)} stations)")


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print(f"ERROR {ex}")
        sys.exit(1)

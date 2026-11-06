"""
Builds a .blend file for every model in the game: the elf, toys, kit crate, ribbon bow, stations and room props.

    blender --background --factory-startup --python make_models.py -- <output folder> [model names...]

Each file holds one collection named after the model (so room.blend can link it), and is saved as <name>.blend.
These are low-poly starting points made from simple shapes; open any file in Blender to refine it, then export
with tools/blender/export.ps1. Re-running this script OVERWRITES the files it builds, so pass model names to
rebuild only some of them once you've started editing by hand.

Collection custom properties read by the room export:
    solid = False      the elf can walk through it (rugs, wall decorations)
Scene custom property read by the model export:
    wall_mounted = True   origin is on the wall surface, so it may float above the floor
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from helpers import box, cone, cyl, disc, new_file, save, sphere, torus  # noqa: E402

import bpy  # noqa: E402

TAU = math.pi * 2


# ================================================================ character

def elf(c):
    """About 1.5 m with the hat. Faces -Y."""
    for side in (-1, 1):
        x = side * 0.09
        box(f"Boot.{side}", x, -0.03, 0, 0.12, 0.2, 0.09, "boot", c)
        cone(f"Boot tip.{side}", x, -0.14, 0.06, 0.05, 0.0, 0.1, "boot", c, rot=(math.radians(-70), 0, 0))
        cyl(f"Leg.{side}", x, 0, 0.19, 0.055, 0.2, "Z", "berry", c)
        for k, z in enumerate((0.14, 0.22)):
            cyl(f"Stripe.{side}.{k}", x, 0, z, 0.058, 0.03, "Z", "snow", c)
    cone("Tunic", 0, 0, 0.53, 0.26, 0.17, 0.5, "elf_green", c, verts=12)
    for n in range(8):  # zigzag hem
        a = n * TAU / 8
        cone(f"Hem.{n}", math.cos(a) * 0.24, math.sin(a) * 0.24, 0.27, 0.05, 0.0, 0.08, "elf_green", c,
             verts=4, rot=(math.pi, 0, 0))
    cyl("Belt", 0, 0, 0.5, 0.215, 0.06, "Z", "ink", c, verts=12)
    box("Buckle", 0, -0.215, 0.47, 0.08, 0.02, 0.06, "gold", c)
    cyl("Collar", 0, 0, 0.79, 0.15, 0.05, "Z", "gold", c, verts=12)
    for side in (-1, 1):
        cone(f"Arm.{side}", side * 0.22, 0, 0.6, 0.05, 0.06, 0.32, "elf_green", c, rot=(0, side * 0.25, 0))
        sphere(f"Hand.{side}", side * 0.26, 0, 0.43, 0.055, "skin", c)
    sphere("Head", 0, 0, 0.98, 0.19, "skin", c, segments=14, rings=9)
    for side in (-1, 1):
        cone(f"Ear.{side}", side * 0.22, 0, 1.02, 0.05, 0.0, 0.16, "skin", c, rot=(0, side * math.radians(65), 0))
        sphere(f"Eye.{side}", side * 0.07, -0.165, 1.0, 0.028, "ink", c, segments=8, rings=5)
        sphere(f"Cheek.{side}", side * 0.11, -0.15, 0.93, 0.03, "cheek", c, segments=8, rings=5)
    sphere("Nose", 0, -0.19, 0.95, 0.035, "cheek", c, segments=8, rings=5)
    torus("Hat brim", 0, 0, 1.1, 0.18, 0.045, "snow", c)
    cone("Hat", 0, 0.05, 1.31, 0.19, 0.0, 0.44, "berry", c, verts=12, rot=(0.22, 0, 0))
    sphere("Pom-pom", 0, 0.1, 1.52, 0.06, "snow", c)


# ================================================================ toys and items

def toy_train(c):
    box("Chassis", 0, 0, 0.05, 0.46, 0.2, 0.05, "ink", c)
    cyl("Boiler", -0.06, 0, 0.2, 0.1, 0.3, "X", "train_red", c, verts=12)
    cyl("Boiler front", -0.215, 0, 0.2, 0.09, 0.02, "X", "gold", c, verts=12)
    box("Cab", 0.15, 0, 0.1, 0.16, 0.22, 0.22, "train_red", c)
    box("Cab roof", 0.15, 0, 0.32, 0.2, 0.26, 0.03, "ink", c)
    box("Cab window", 0.15, -0.112, 0.2, 0.08, 0.01, 0.06, "glass", c)
    cyl("Chimney", -0.17, 0, 0.33, 0.04, 0.1, "Z", "ink", c)
    cyl("Chimney top", -0.17, 0, 0.39, 0.055, 0.03, "Z", "gold", c)
    sphere("Dome", -0.04, 0, 0.3, 0.045, "gold", c)
    cone("Cowcatcher", -0.25, 0, 0.08, 0.08, 0.0, 0.06, "ink", c, verts=4, rot=(0, math.radians(-90), 0))
    for x in (-0.15, -0.02, 0.13):
        for side in (-1, 1):
            cyl(f"Wheel.{x}.{side}", x, side * 0.11, 0.065, 0.065, 0.03, "Y", "ink", c, verts=12)
            cyl(f"Hub.{x}.{side}", x, side * 0.127, 0.065, 0.025, 0.01, "Y", "gold", c)


def toy_bear(c):
    sphere("Body", 0, 0, 0.19, 0.16, "bear_fur", c, scale=(1.0, 0.9, 1.15))
    sphere("Belly", 0, -0.09, 0.18, 0.1, "bear_light", c, scale=(1.0, 0.6, 1.1))
    sphere("Head", 0, 0, 0.43, 0.13, "bear_fur", c)
    for side in (-1, 1):
        sphere(f"Ear.{side}", side * 0.09, 0, 0.54, 0.05, "bear_fur", c)
        sphere(f"Inner ear.{side}", side * 0.09, -0.03, 0.54, 0.028, "bear_light", c, segments=8, rings=5)
        sphere(f"Eye.{side}", side * 0.045, -0.115, 0.46, 0.018, "ink", c, segments=8, rings=5)
        sphere(f"Arm.{side}", side * 0.15, -0.03, 0.24, 0.055, "bear_fur", c, scale=(1, 1, 1.5))
        sphere(f"Leg.{side}", side * 0.09, -0.09, 0.06, 0.065, "bear_fur", c, scale=(1, 1.3, 0.9))
        sphere(f"Paw.{side}", side * 0.09, -0.16, 0.06, 0.035, "bear_light", c, segments=8, rings=5)
    sphere("Muzzle", 0, -0.11, 0.41, 0.055, "bear_light", c)
    sphere("Nose", 0, -0.16, 0.42, 0.02, "ink", c, segments=8, rings=5)
    torus("Scarf", 0, 0, 0.32, 0.1, 0.03, "berry", c)


def toy_robot(c):
    for side in (-1, 1):
        box(f"Leg.{side}", side * 0.07, 0, 0, 0.08, 0.1, 0.12, "robot_dark", c)
        box(f"Foot.{side}", side * 0.07, -0.02, 0, 0.1, 0.14, 0.03, "ink", c)
        cyl(f"Arm.{side}", side * 0.18, 0, 0.24, 0.03, 0.18, "Z", "steel", c)
        sphere(f"Hand.{side}", side * 0.18, 0, 0.14, 0.04, "robot_dark", c)
    box("Body", 0, 0, 0.12, 0.3, 0.2, 0.26, "robot_body", c)
    box("Chest panel", 0, -0.101, 0.18, 0.16, 0.01, 0.12, "ink", c)
    for n, m in enumerate(("berry", "gold", "pine")):
        sphere(f"Button.{n}", -0.04 + n * 0.04, -0.11, 0.24, 0.015, m, c, segments=6, rings=4)
    box("Neck", 0, 0, 0.38, 0.06, 0.06, 0.03, "steel", c)
    box("Head", 0, 0, 0.41, 0.22, 0.18, 0.15, "robot_body", c)
    for side in (-1, 1):
        box(f"Eye.{side}", side * 0.05, -0.091, 0.47, 0.045, 0.01, 0.04, "lamp_glow", c)
        cyl(f"Ear bolt.{side}", side * 0.115, 0, 0.48, 0.025, 0.02, "X", "steel", c)
    box("Mouth", 0, -0.091, 0.43, 0.09, 0.01, 0.015, "ink", c)
    cyl("Antenna", 0, 0, 0.6, 0.008, 0.08, "Z", "steel", c, verts=6)
    sphere("Antenna tip", 0, 0, 0.65, 0.025, "berry", c)


def kit_crate(c):
    """0.42 m crate in light neutral wood; the game tints it with the toy's colour."""
    s, t = 0.42, 0.04
    box("Crate core", 0, 0, 0.01, s - 0.02, s - 0.02, s - 0.02, "crate_edge", c)
    for n in range(3):  # slats on four sides
        z = 0.03 + n * 0.13
        box(f"Slat front.{n}", 0, -s / 2, z, s, t, 0.1, "crate_light", c)
        box(f"Slat back.{n}", 0, s / 2, z, s, t, 0.1, "crate_light", c)
        box(f"Slat left.{n}", -s / 2, 0, z, t, s, 0.1, "crate_light", c)
        box(f"Slat right.{n}", s / 2, 0, z, t, s, 0.1, "crate_light", c)
    for x in (-1, 1):
        for y in (-1, 1):
            box(f"Corner.{x}.{y}", x * s / 2, y * s / 2, 0, 0.06, 0.06, s, "crate_edge", c)
    box("Lid", 0, 0, s - 0.03, s, s, 0.03, "crate_light", c)
    box("Label", 0, -s / 2 - 0.022, 0.17, 0.2, 0.005, 0.1, "snow", c)


def ribbon_bow(c):
    for side in (-1, 1):
        torus(f"Loop.{side}", side * 0.065, 0, 0.08, 0.05, 0.015, "gold", c, rot=(math.pi / 2, 0, 0), scale=(1.2, 1, 0.8))
        box(f"Tail.{side}", side * 0.04, -0.02, 0.01, 0.025, 0.01, 0.07, "gold", c, rot=(0, side * 0.4, 0))
    sphere("Knot", 0, 0, 0.08, 0.025, "berry", c)


# ================================================================ stations (footprints match stations.json)

def station_bin(c):
    """Kit bin, 2.25 x 1.0, rim at 0.8 m (the game places a kit crate on top)."""
    w, d, h = 2.25, 1.0, 0.8
    for x in (-1, 1):
        for y in (-1, 1):
            box(f"Post.{x}.{y}", x * (w / 2 - 0.05), y * (d / 2 - 0.05), 0, 0.1, 0.1, h, "log_dark", c)
    for n in range(4):
        z = 0.05 + n * 0.19
        box(f"Plank front.{n}", 0, -d / 2 + 0.03, z, w - 0.1, 0.04, 0.16, "plank_c" if n % 2 else "plank_a", c)
        box(f"Plank back.{n}", 0, d / 2 - 0.03, z, w - 0.1, 0.04, 0.16, "plank_b", c)
        box(f"Plank left.{n}", -w / 2 + 0.03, 0, z, 0.04, d - 0.1, 0.16, "plank_a", c)
        box(f"Plank right.{n}", w / 2 - 0.03, 0, z, 0.04, d - 0.1, 0.16, "plank_c", c)
    box("Bin floor", 0, 0, 0.05, w - 0.1, d - 0.1, 0.04, "plank_b", c)
    box("Straw", 0, 0, 0.55, w - 0.14, d - 0.14, 0.1, "straw", c)
    for n, x in enumerate((-0.7, -0.25, 0.3, 0.75)):  # crates resting in the straw
        box(f"Spare crate.{n}", x, 0.12 * (1 if n % 2 else -1), 0.58, 0.3, 0.3, 0.2, "crate_light", c,
            rot=(0, 0, 0.2 * (n - 1.5)))
    box("Rim", 0, -d / 2 + 0.03, h - 0.04, w, 0.08, 0.06, "log_dark", c)
    box("Sign board", 0, -d / 2 - 0.02, 0.35, 0.9, 0.03, 0.25, "bench_top", c)


def station_bench(c):
    """Workbench, 3.0 x 1.0, top at 0.85 m, worked from the -Y side."""
    w, d, h = 3.0, 1.0, 0.85
    box("Top", 0, 0, h - 0.1, w, d, 0.1, "bench_top", c)
    for x in (-1, 1):
        for y in (-1, 1):
            box(f"Leg.{x}.{y}", x * (w / 2 - 0.1), y * (d / 2 - 0.1), 0, 0.12, 0.12, h - 0.1, "log", c)
    box("Stretcher", 0, 0, 0.15, w - 0.2, 0.08, 0.08, "log_dark", c)
    box("Lower shelf", 0, 0.05, 0.25, w - 0.3, d - 0.3, 0.04, "plank_b", c)
    for n, x in enumerate((-0.9, -0.3, 0.5)):  # boxes of parts on the shelf
        box(f"Parts box.{n}", x, 0.05, 0.29, 0.4, 0.35, 0.18, "crate_edge", c)
    # Vice on the front right.
    box("Vice body", 1.15, -0.45, h, 0.22, 0.18, 0.12, "iron", c)
    cyl("Vice screw", 1.15, -0.6, h + 0.06, 0.015, 0.2, "Y", "steel", c, verts=6)
    # Tools on top.
    cyl("Hammer handle", -0.8, -0.1, h + 0.025, 0.018, 0.32, "X", "log_end", c, verts=6)
    box("Hammer head", -0.62, -0.1, h, 0.06, 0.12, 0.05, "iron", c)
    box("Saw blade", 0.1, 0.15, h, 0.5, 0.12, 0.01, "steel", c)
    box("Saw handle", 0.4, 0.15, h, 0.12, 0.1, 0.03, "berry", c)
    for n in range(5):
        cyl(f"Nail.{n}", -0.2 + n * 0.05, -0.3, h + 0.01, 0.006, 0.05, "Y", "steel", c, verts=4)
    box("Plank offcut", 0.75, 0.2, h, 0.5, 0.15, 0.04, "plank_c", c)
    cyl("Paint pot", -1.25, 0.25, h + 0.06, 0.07, 0.12, "Z", "train_red", c, verts=10)
    # Pegboard of tools behind the bench.
    box("Pegboard", 0, d / 2 - 0.03, h, 1.6, 0.04, 0.6, "plank_a", c)
    for n, x in enumerate((-0.6, -0.2, 0.2, 0.6)):
        box(f"Hung tool.{n}", x, d / 2 - 0.07, h + 0.15, 0.06, 0.03, 0.35, ("iron", "steel", "berry", "iron")[n], c)


def station_ribbon(c):
    """Ribbon table, 2.5 x 1.0, with a rack of spools."""
    w, d, h = 2.5, 1.0, 0.85
    box("Table top", 0, 0, h - 0.08, w, d, 0.08, "pine_dark", c)
    box("Table trim", 0, -d / 2, h - 0.12, w, 0.04, 0.05, "gold", c)
    for x in (-1, 1):
        for y in (-1, 1):
            box(f"Leg.{x}.{y}", x * (w / 2 - 0.1), y * (d / 2 - 0.1), 0, 0.1, 0.1, h - 0.08, "log", c)
        box(f"Rack post.{x}", x * (w / 2 - 0.15), 0.25, h, 0.07, 0.07, 0.5, "log_dark", c)
    cyl("Rack rod", 0, 0.25, h + 0.42, 0.02, w - 0.25, "X", "steel", c, verts=6)
    colors = ("berry", "gold", "pine", "gift_a", "snow", "berry", "gold")
    for n, m in enumerate(colors):
        x = -0.9 + n * 0.3
        cyl(f"Spool.{n}", x, 0.25, h + 0.42, 0.1, 0.16, "X", m, c, verts=12)
        box(f"Ribbon tail.{n}", x, 0.15, h + 0.08, 0.08, 0.01, 0.25, m, c)
    box("Scissors blade a", -0.5, -0.25, h, 0.2, 0.03, 0.01, "steel", c, rot=(0, 0, 0.3))
    box("Scissors blade b", -0.5, -0.25, h + 0.01, 0.2, 0.03, 0.01, "steel", c, rot=(0, 0, -0.3))
    torus("Bow sample", 0.6, -0.2, h + 0.04, 0.06, 0.02, "gold", c, rot=(math.pi / 2, 0, 0))


def station_shipping(c):
    """Shipping chute, 1.75 x 1.5, opening on the -X side where the elf stands."""
    w, d, h = 1.75, 1.5, 1.1
    box("Body", 0, 0, 0, w, d, h, "plank_a", c)
    for n in range(5):
        box(f"Board.{n}", 0, -d / 2 - 0.01, 0.1 + n * 0.2, w, 0.02, 0.04, "plank_b", c)
    box("Roof", 0, 0, h, w + 0.1, d + 0.1, 0.08, "beam", c, rot=(0.12, 0, 0))
    box("Chute frame", -w / 2 - 0.03, 0, 0.45, 0.06, 0.9, 0.55, "log_dark", c)
    box("Chute mouth", -w / 2 - 0.035, 0, 0.5, 0.05, 0.7, 0.42, "soot", c)
    box("Chute lip", -w / 2 - 0.15, 0, 0.42, 0.3, 0.8, 0.04, "steel", c, rot=(0, -0.25, 0))
    box("Sign", 0, -d / 2 - 0.05, h + 0.12, 1.0, 0.04, 0.3, "berry", c)
    box("Sign trim", 0, -d / 2 - 0.06, h + 0.1, 1.06, 0.03, 0.34, "gold", c)
    for n, (x, y) in enumerate(((0.55, 0.85), (0.15, 0.95))):  # mail sacks behind
        sphere(f"Sack.{n}", x, y - 0.2, 0.25, 0.22, "burlap", c, scale=(1, 0.8, 1.1))
        cyl(f"Sack tie.{n}", x, y - 0.2, 0.49, 0.06, 0.06, "Z", "berry", c)


def station_recycle(c):
    """Scrap barrel, 1.25 x 1.25, height 0.75."""
    r, h = 0.5, 0.75
    cyl("Barrel", 0, 0, h / 2, r, h, "Z", "log", c, verts=16)
    for n, z in enumerate((0.1, 0.38, 0.66)):
        cyl(f"Hoop.{n}", 0, 0, z, r + 0.015, 0.05, "Z", "iron", c, verts=16)
    disc("Inside", 0, 0, h - 0.12, r - 0.05, r - 0.05, 0.02, "soot", c, verts=16)
    for n in range(6):  # scraps poking out
        a = n * TAU / 6
        box(f"Scrap.{n}", math.cos(a) * 0.2, math.sin(a) * 0.2, h - 0.15, 0.12, 0.05, 0.25,
            ("plank_c", "crate_light", "berry", "plank_a", "steel", "gold")[n], c, rot=(0.3, 0.2 * n, a))
    box("Sign post", r + 0.12, -0.3, 0, 0.04, 0.04, 0.9, "log_dark", c)
    box("Sign", r + 0.12, -0.32, 0.72, 0.35, 0.03, 0.18, "pine", c)


# ================================================================ room props

def prop_tree(c):
    cyl("Trunk", 0, 0, 0.2, 0.12, 0.4, "Z", "log_dark", c)
    tiers = ((0.95, 0.35, 0.9), (0.75, 0.95, 0.8), (0.55, 1.5, 0.7), (0.35, 1.95, 0.55))
    for n, (r, z, h) in enumerate(tiers):
        cone(f"Tier.{n}", 0, 0, z + h / 2, r, 0.0, h, "pine" if n % 2 else "pine_dark", c, verts=10)
    box("Star", 0, 0, 2.48, 0.2, 0.06, 0.2, "gold", c, rot=(0, math.radians(45), 0))
    for n in range(16):
        a = n * 2.4
        z = 0.45 + (n % 8) * 0.24
        r = 0.97 - (z - 0.35) * 0.43
        sphere(f"Ornament.{n}", math.cos(a) * r, math.sin(a) * r, z, 0.06,
               ("berry", "gold", "gift_a", "snow")[n % 4], c, segments=6, rings=4)
    for n, (gx, gy, s, m) in enumerate(((-0.6, -0.55, 0.35, "gift_a"), (0.1, -0.8, 0.3, "berry"),
                                        (-0.85, 0.15, 0.28, "gift_b"), (0.55, -0.45, 0.22, "pine"))):
        box(f"Gift.{n}", gx, gy, 0, s, s, s * 0.8, m, c)
        box(f"Gift ribbon.{n}", gx, gy, 0, s + 0.01, 0.05, s * 0.8 + 0.01, "gold", c)


def prop_fireplace(c):
    """Stone fireplace, 2.6 wide. Wall-mounted: the back is on the wall (y=0), the front faces -Y."""
    width, depth, height = 2.6, 0.8, 3.6
    box("Chimney", 0, -depth / 2, 0, width, depth, height, "stone_b", c)
    face = -depth - 0.03
    z, row = 0.0, 0
    while z < height - 0.1:
        h = random.uniform(0.22, 0.32)
        x = -width / 2 + (0.15 if row % 2 else 0)
        while x < width / 2 - 0.05:
            sw = min(random.uniform(0.3, 0.55), width / 2 - x)
            box(f"Stone.{row:02d}.{x:+.2f}", x + sw / 2, face, z, sw - 0.03, 0.08, h - 0.03,
                random.choice(("stone_a", "stone_b", "stone_c")), c)
            x += sw
        z += h
        row += 1
    box("Firebox", 0, face - 0.05, 0, 1.2, 0.05, 0.9, "soot", c)
    box("Arch", 0, face - 0.06, 0.9, 1.4, 0.08, 0.12, "stone_c", c)
    cyl("Fire log.0", -0.1, face - 0.2, 0.1, 0.07, 0.8, "X", "log_dark", c)
    cyl("Fire log.1", 0.1, face - 0.3, 0.1, 0.07, 0.8, "X", "log", c)
    for n, (fx, fh) in enumerate(((-0.25, 0.45), (0.0, 0.6), (0.25, 0.4))):
        cone(f"Flame.{n}", fx, face - 0.25, 0.15 + fh / 2, 0.12, 0.0, fh, "flame", c, verts=6)
        cone(f"Flame core.{n}", fx, face - 0.28, 0.15 + fh * 0.35, 0.06, 0.0, fh * 0.6, "flame_core", c, verts=6)
    box("Hearth", 0, face - 0.25, 0, width + 0.2, 0.55, 0.06, "stone_c", c)
    box("Mantel", 0, face - 0.12, 1.3, width + 0.3, 0.3, 0.12, "beam", c)
    for n, sx in enumerate((-0.8, 0.0, 0.8)):
        box(f"Stocking.{n}", sx, face - 0.1, 0.9, 0.18, 0.06, 0.38, "berry" if n != 1 else "pine", c)
        box(f"Stocking cuff.{n}", sx, face - 0.1, 1.22, 0.2, 0.07, 0.08, "snow", c)
    sphere("Mantel clock", 0, face - 0.1, 1.5, 0.1, "gold", c)
    for n, sx in enumerate((-1.0, 1.0)):
        cyl(f"Candle.{n}", sx, face - 0.1, 1.5, 0.03, 0.14, "Z", "snow", c, verts=8)
        cone(f"Candle flame.{n}", sx, face - 0.1, 1.6, 0.015, 0.0, 0.05, "flame_core", c, verts=5)


def prop_firewood(c):
    """Stack of split logs, about 0.6 x 0.9."""
    for row in range(4):
        for n in range(5 - row % 2):
            cyl(f"Log.{row}.{n}", 0, -0.34 + n * 0.17 + (row % 2) * 0.085, 0.08 + row * 0.15, 0.075, 0.6, "X",
                random.choice(("log", "log_dark", "log_end")), c, verts=7)
    box("Hatchet handle", 0.4, 0.3, 0.0, 0.04, 0.04, 0.45, "log_end", c, rot=(0.3, 0, 0))
    box("Hatchet head", 0.4, 0.36, 0.38, 0.03, 0.12, 0.08, "steel", c, rot=(0.3, 0, 0))


def prop_barrels(c):
    for n, y in enumerate((-0.42, 0.42)):
        cyl(f"Barrel.{n}", 0, y, 0.45, 0.36, 0.9, "Z", "log", c, verts=12)
        for k, z in enumerate((0.15, 0.75)):
            cyl(f"Hoop.{n}.{k}", 0, y, z, 0.37, 0.05, "Z", "iron", c, verts=12)
        disc(f"Lid.{n}", 0, y, 0.9, 0.33, 0.33, 0.02, "log_end", c, verts=12)
    box("Crate", 0, 0, 0.92, 0.5, 0.5, 0.4, "plank_c", c, rot=(0, 0, 0.15))
    sphere("Apple", 0.1, -0.5, 0.97, 0.05, "berry", c)


def prop_sled(c):
    for side in (-1, 1):
        box(f"Runner.{side}", side * 0.22, 0, 0, 0.05, 1.4, 0.06, "berry", c)
        torus(f"Curl.{side}", side * 0.22, 0.72, 0.15, 0.12, 0.025, "berry", c, rot=(0, math.pi / 2, 0))
    box("Deck", 0, 0, 0.2, 0.6, 1.25, 0.05, "plank_a", c)
    for n, y in enumerate((-0.45, 0.0, 0.45)):
        box(f"Post.{n}", 0, y, 0.05, 0.5, 0.05, 0.15, "plank_b", c)
    box("Gift on sled", 0, -0.15, 0.25, 0.3, 0.3, 0.25, "gift_a", c)
    box("Gift ribbon", 0, -0.15, 0.25, 0.31, 0.05, 0.26, "gold", c)


def prop_rug(c):
    for n, (rx, ry, m) in enumerate(((2.3, 1.5, "rug_b"), (1.95, 1.2, "rug_a"), (1.55, 0.92, "rug_c"),
                                     (1.15, 0.65, "rug_a"), (0.7, 0.38, "rug_b"))):
        disc(f"Ring.{n}", 0, 0, n * 0.002, rx, ry, 0.006, m, c)


def prop_window(c):
    """1.3 x 1.1 window, bottom at 1.35 m. Wall-mounted, faces -Y."""
    w, h, z, t = 1.3, 1.1, 1.35, 0.08
    y = -0.04
    box("Glass", 0, y + 0.01, z, w, 0.02, h, "glass", c)
    box("Frame top", 0, y, z + h - t, w + t, 0.08, t, "frame", c)
    box("Frame bottom", 0, y, z, w + t, 0.08, t, "frame", c)
    box("Frame left", -w / 2, y, z, t, 0.08, h, "frame", c)
    box("Frame right", w / 2, y, z, t, 0.08, h, "frame", c)
    box("Mullion v", 0, y, z, 0.05, 0.09, h, "frame", c)
    box("Mullion h", 0, y, z + h / 2, w, 0.09, 0.05, "frame", c)
    box("Sill", 0, -0.14, z - 0.06, w + 0.3, 0.25, 0.06, "frame", c)
    box("Snow", 0, y + 0.02, z + t, w - 0.05, 0.03, 0.06, "snow", c)
    for side in (-1, 1):
        box(f"Shutter.{side}", side * (w / 2 + 0.3), y, z, 0.45, 0.04, h, "pine_dark", c)


def prop_lantern(c):
    """Wall lantern on an iron bracket. Wall-mounted, sticks out towards -Y."""
    box("Wall plate", 0, -0.01, 2.45, 0.1, 0.02, 0.2, "iron", c)
    box("Bracket", 0, -0.15, 2.55, 0.03, 0.28, 0.03, "iron", c)
    cyl("Hook", 0, -0.28, 2.49, 0.008, 0.1, "Z", "iron", c, verts=4)
    box("Cap", 0, -0.28, 2.4, 0.18, 0.18, 0.06, "iron", c)
    box("Glass", 0, -0.28, 2.16, 0.14, 0.14, 0.24, "lamp_glow", c)
    box("Base", 0, -0.28, 2.11, 0.18, 0.18, 0.05, "iron", c)


def prop_wreath(c):
    torus("Wreath", 0, -0.08, 2.45, 0.35, 0.09, "pine", c, rot=(math.pi / 2, 0, 0))
    for n in range(8):
        a = n * TAU / 8
        sphere(f"Berry.{n}", math.cos(a) * 0.35, -0.16, 2.45 + math.sin(a) * 0.35, 0.035, "berry", c, segments=6, rings=4)
    box("Bow", 0, -0.15, 2.0, 0.22, 0.05, 0.14, "berry", c)
    cyl("Nail", 0, -0.01, 2.82, 0.01, 0.04, "Y", "iron", c, verts=4)


def prop_shelf(c):
    box("Board", 0, -0.15, 2.05, 1.6, 0.3, 0.05, "beam", c)
    for side in (-1, 1):
        box(f"Bracket.{side}", side * 0.6, -0.08, 1.85, 0.04, 0.16, 0.2, "iron", c)
    for k in range(4):
        cyl(f"Jar.{k}", -0.55 + k * 0.37, -0.15, 2.18, 0.07, 0.22, "Z", ("jar", "berry", "gold", "jar")[k], c, verts=8)
        cyl(f"Jar lid.{k}", -0.55 + k * 0.37, -0.15, 2.3, 0.072, 0.03, "Z", "iron", c, verts=8)


def prop_clock(c):
    box("Body", 0, -0.06, 1.65, 0.4, 0.12, 0.5, "beam", c)
    box("Roof left", -0.12, -0.09, 2.13, 0.32, 0.18, 0.06, "log_dark", c, rot=(0, 0.5, 0))
    box("Roof right", 0.12, -0.09, 2.13, 0.32, 0.18, 0.06, "log_dark", c, rot=(0, -0.5, 0))
    cyl("Face", 0, -0.13, 1.92, 0.12, 0.02, "Y", "snow", c, verts=12)
    box("Hand hour", 0, -0.145, 1.92, 0.015, 0.005, 0.07, "ink", c)
    box("Hand minute", 0.02, -0.145, 1.92, 0.08, 0.005, 0.012, "ink", c)
    box("Bird door", 0, -0.125, 1.75, 0.08, 0.01, 0.08, "berry", c)
    for n, x in enumerate((-0.08, 0.08)):
        cyl(f"Chain.{n}", x, -0.06, 1.35, 0.006, 0.6, "Z", "gold", c, verts=4)
        cone(f"Weight.{n}", x, -0.06, 1.0, 0.035, 0.035, 0.18, "gold", c, verts=6)


# ================================================================ catalogue

MODELS = {
    # name: (builder, solid, wall_mounted)
    "elf": (elf, True, False),
    "toy_train": (toy_train, True, False),
    "toy_bear": (toy_bear, True, False),
    "toy_robot": (toy_robot, True, False),
    "kit_crate": (kit_crate, True, False),
    "ribbon_bow": (ribbon_bow, True, False),
    "station_bin": (station_bin, True, False),
    "station_bench": (station_bench, True, False),
    "station_ribbon": (station_ribbon, True, False),
    "station_shipping": (station_shipping, True, False),
    "station_recycle": (station_recycle, True, False),
    "prop_tree": (prop_tree, True, False),
    "prop_fireplace": (prop_fireplace, True, False),
    "prop_firewood": (prop_firewood, True, False),
    "prop_barrels": (prop_barrels, True, False),
    "prop_sled": (prop_sled, True, False),
    "prop_rug": (prop_rug, False, False),
    "prop_window": (prop_window, False, True),
    "prop_lantern": (prop_lantern, False, True),
    "prop_wreath": (prop_wreath, False, True),
    "prop_shelf": (prop_shelf, False, True),
    "prop_clock": (prop_clock, False, True),
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = os.path.abspath(argv[0])
    names = argv[1:] or list(MODELS)
    for name in names:
        if name not in MODELS:
            print(f"ERROR unknown model '{name}'. Known: {', '.join(MODELS)}")
            continue
        builder, solid, wall = MODELS[name]
        col = new_file(name)
        builder(col)
        col["solid"] = solid
        bpy.context.scene["wall_mounted"] = wall
        path = os.path.join(out_dir, name + ".blend")
        save(path)
        print(f"RESULT built {name} ({len(col.objects)} parts) -> {path}")


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print(f"ERROR {ex}")
        sys.exit(1)

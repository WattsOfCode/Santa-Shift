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
from helpers import box, cone, cyl, disc, glow, light, new_file, save, sphere, torus  # noqa: E402

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

TAU = math.pi * 2


# ================================================================ character

def limb(name, p0, p1, r0, r1, mat, col, verts=8):
    """A tapered cone running from point p0 (radius r0) to point p1 (radius r1)."""
    from mathutils import Vector
    a, b = Vector(p0), Vector(p1)
    rot = Vector((0, 0, 1)).rotation_difference(b - a).to_euler()
    mid = (a + b) / 2
    return cone(name, mid.x, mid.y, mid.z, r0, r1, (b - a).length, mat, col, verts=verts, rot=tuple(rot))


class Rig:
    """Remembers which bone each new part of the elf follows (rigid skinning: every part moves with one bone)."""

    def __init__(self, c):
        self.c = c
        self.bone_of = {}

    def claim(self, bone):
        for ob in self.c.objects:
            if ob.type == "MESH" and ob not in self.bone_of:
                self.bone_of[ob] = bone


def face_y(x, z, hy, hz):
    """Where the front of the elf's face is (its Y) at a point (x, z): the nearer of the head and chin surfaces.
    Used to set face details into the skin instead of floating in front of it."""
    def front(cx, cy, cz, rx, ry, rz):
        k = 1 - ((x - cx) / rx) ** 2 - ((z - cz) / rz) ** 2
        return cy - ry * math.sqrt(k) if k > 0 else None
    head = front(0, hy, hz, 0.17 * 1.12, 0.17, 0.17 * 0.95)
    chin = front(0, hy - 0.08, hz - 0.12, 0.07, 0.07, 0.07 * 1.3)
    return min(v for v in (head, chin) if v is not None)


def side_name(side):
    """Blender rig naming: the elf faces -Y, so its left is +X (.L) and its right is -X (.R)."""
    return "L" if side > 0 else "R"


def elf(c):
    """A short, scowling, pot-bellied workshop elf: about 1.15 m with the floppy hat. Faces -Y.
    Rigged with a simple skeleton and three actions (Idle, Walk, Work); Work is a hammer swing with the right hand."""
    rig = Rig(c)
    bones = []  # (name, head, tail, parent)

    def add_bone(name, head, tail, parent):
        bones.append((name, head, tail, parent))

    add_bone("root", (0, 0, 0), (0, 0, 0.12), None)
    add_bone("hips", (0, 0, 0.27), (0, 0, 0.42), "root")

    # Long curly-toed shoes with a bell on each tip; skinny striped legs.
    for side in (-1, 1):
        s = side_name(side)
        x = side * 0.08
        hip, ankle = (x * 0.85, 0, 0.28), (x, -0.01, 0.05)
        limb(f"Leg.{side}", hip, ankle, 0.032, 0.026, "berry", c)
        for k, z in enumerate((0.1, 0.17)):
            torus(f"Stripe.{side}.{k}", x * (0.95 - k * 0.04), -0.005, z, 0.029, 0.009, "snow", c, segments=(8, 4))
        rig.claim(f"leg.{s}")
        sphere(f"Shoe.{side}", x, -0.05, 0.03, 0.05, "boot", c, scale=(0.85, 2.0, 0.6))
        limb(f"Toe.{side}", (x, -0.13, 0.035), (x, -0.21, 0.1), 0.035, 0.012, "boot", c)
        limb(f"Toe curl.{side}", (x, -0.21, 0.1), (x, -0.175, 0.15), 0.013, 0.005, "boot", c, verts=6)
        sphere(f"Bell.{side}", x, -0.175, 0.155, 0.018, "gold", c, segments=8, rings=5)
        rig.claim(f"foot.{s}")
        add_bone(f"leg.{s}", hip, ankle, "hips")
        add_bone(f"foot.{s}", ankle, (x, -0.15, 0.03), f"leg.{s}")

    # Pot belly in a scruffy patched tunic, rope belt sagging under it.
    sphere("Belly", 0, -0.03, 0.4, 0.17, "tunic", c, segments=14, rings=9)
    for n in range(9):  # ragged hem
        a = n * TAU / 9 + 0.2
        cone(f"Hem.{n}", math.cos(a) * 0.15, math.sin(a) * 0.15 - 0.02, 0.27, 0.045, 0.0, 0.07 + 0.02 * (n % 2),
             "tunic", c, verts=4, rot=(math.pi, 0, 0))
    torus("Belt", 0, -0.03, 0.33, 0.155, 0.015, "straw", c, rot=(math.radians(-8), 0, 0), segments=(14, 4))
    box("Patch belly", 0.07, -0.19, 0.4, 0.07, 0.02, 0.07, "patch", c, rot=(math.radians(15), 0, math.radians(20)))
    rig.claim("hips")
    cone("Chest", 0, 0, 0.53, 0.15, 0.095, 0.2, "tunic", c, verts=12)
    box("Patch side", -0.13, -0.08, 0.47, 0.02, 0.05, 0.05, "patch", c)
    torus("Collar", 0, 0, 0.62, 0.09, 0.022, "patch", c, segments=(10, 4))
    cyl("Neck", 0, -0.01, 0.635, 0.045, 0.06, "Z", "goblin_skin", c)
    rig.claim("chest")
    add_bone("chest", (0, 0, 0.42), (0, 0, 0.63), "hips")

    # Slouched shoulders and long skinny arms with long fingers.
    for side in (-1, 1):
        s = side_name(side)
        sh = (side * 0.13, 0.0, 0.58)
        el = (side * 0.2, -0.04, 0.42)
        wr = (side * 0.22, -0.08, 0.28)
        sphere(f"Shoulder.{side}", *sh, 0.05, "tunic", c, segments=8, rings=5)
        limb(f"Sleeve.{side}", sh, el, 0.045, 0.035, "tunic", c)
        rig.claim(f"upper_arm.{s}")
        limb(f"Forearm.{side}", el, wr, 0.024, 0.02, "goblin_skin", c)
        rig.claim(f"forearm.{s}")
        sphere(f"Hand.{side}", *wr, 0.03, "goblin_skin", c, segments=8, rings=5)
        for f, (dx, dy) in enumerate(((-0.02, -0.015), (0.0, -0.025), (0.02, -0.015))):
            limb(f"Finger.{side}.{f}", wr, (wr[0] + dx * side + side * 0.01, wr[1] + dy, wr[2] - 0.085),
                 0.009, 0.003, "goblin_skin", c, verts=5)
        rig.claim(f"hand.{s}")
        add_bone(f"upper_arm.{s}", sh, el, "chest")
        add_bone(f"forearm.{s}", el, wr, f"upper_arm.{s}")
        # The game holds the hammer in the right hand, 4 cm below the wrist (the fist), handle pointing forward.
        add_bone(f"hand.{s}", wr, (wr[0], wr[1] - 0.01, wr[2] - 0.08), f"forearm.{s}")

    # Big head pushed forward.
    hz, hy = 0.74, -0.03
    sphere("Head", 0, hy, hz, 0.17, "goblin_skin", c, segments=16, rings=10, scale=(1.12, 1.0, 0.95))
    sphere("Chin", 0, hy - 0.08, hz - 0.12, 0.07, "goblin_skin", c, segments=10, rings=6, scale=(1.0, 1.0, 1.3))
    for side in (-1, 1):
        # Long droopy pointed ears.
        limb(f"Ear.{side}", (side * 0.17, hy + 0.01, hz + 0.01), (side * 0.37, hy + 0.04, hz - 0.03), 0.055, 0.0,
             "goblin_skin", c)
        limb(f"Inner ear.{side}", (side * 0.18, hy - 0.005, hz + 0.0), (side * 0.32, hy + 0.02, hz - 0.025), 0.03, 0.0,
             "goblin_dark", c, verts=6)
        # Half-lidded eyes glancing sideways, heavy scowling brows.
        # The pupil is a sphere sunk into the eyeball, so only a flat dark cap shows on its surface (no floating dot).
        ex, ey, ez, er = side * 0.065, hy - 0.14, hz + 0.02, 0.042
        sphere(f"Eye.{side}", ex, ey, ez, er, "eye_white", c, segments=12, rings=8)
        look = Vector((0.38, -1.0, -0.12)).normalized()
        pr = 0.021
        p = Vector((ex, ey, ez)) + look * (er - pr + 0.007)  # shows a 7 mm cap
        sphere(f"Pupil.{side}", p.x, p.y, p.z, pr, "ink", c, segments=10, rings=6)
        sphere(f"Lid.{side}", ex, ey - 0.002, ez + 0.018, er + 0.004, "goblin_dark", c, segments=12, rings=8,
               scale=(1.0, 1.0, 0.55))
        # Scowling brow: a stroke that follows the curve of the forehead, low at the nose and high at the temple.
        for n in range(8):
            t = n / 7
            bx, bz = side * (0.025 + 0.09 * t), hz + 0.05 + 0.022 * t
            sphere(f"Brow.{side}.{n}", bx, face_y(bx, bz, hy, hz) + 0.004, bz, 0.02, "goblin_dark", c,
                   segments=8, rings=5, scale=(1.5, 1.0, 0.8 - 0.3 * t))
        sphere(f"Hair tuft.{side}", side * 0.15, hy + 0.04, hz + 0.08, 0.035, "goblin_dark", c, segments=6, rings=4,
               scale=(0.7, 0.7, 1.5))
    # Long pointy nose and a crooked smirk with one snaggle tooth.
    limb("Nose", (0, hy - 0.15, hz - 0.0), (0, hy - 0.29, hz - 0.05), 0.034, 0.006, "goblin_skin", c)
    # The smirk is a line of small dark beads sunk into the skin along the face's surface, rising on the right side
    # of the face (+X from the front), so it reads as a crooked grin painted on rather than a black blob in front.
    for n in range(16):
        t = n / 15
        mx = -0.06 + 0.14 * t
        mz = hz - 0.095 + 0.04 * t * t
        sphere(f"Smirk.{n}", mx, face_y(mx, mz, hy, hz) + 0.008, mz, 0.0115, "ink", c, segments=8, rings=4,
               scale=(1.8, 1.0, 0.9 - 0.35 * abs(t - 0.45)))
    tx, tz = -0.025, hz - 0.104
    box("Tooth", tx, face_y(tx, tz, hy, hz) + 0.002, tz - 0.022, 0.02, 0.012, 0.022, "snow", c)
    torus("Hat brim", 0, hy + 0.01, hz + 0.11, 0.155, 0.032, "snow", c, segments=(14, 6))
    rig.claim("head")
    add_bone("head", (0, -0.01, 0.64), (0, hy, hz + 0.12), "chest")

    # Floppy hat that rises, bends back and droops down behind the head. Its own bone, so it sways.
    path = [(0, hy + 0.01, hz + 0.1), (0, hy + 0.05, hz + 0.23), (0, hy + 0.14, hz + 0.31),
            (0, hy + 0.25, hz + 0.3), (0, hy + 0.33, hz + 0.2), (0, hy + 0.35, hz + 0.1)]
    radii = [0.155, 0.11, 0.075, 0.05, 0.032, 0.018]
    for i in range(len(path) - 1):
        limb(f"Hat.{i}", path[i], path[i + 1], radii[i], radii[i + 1], "berry", c, verts=12)
        if 0 < i:
            sphere(f"Hat joint.{i}", *path[i], radii[i], "berry", c, segments=10, rings=6)
    sphere("Pom-pom", path[-1][0], path[-1][1] + 0.01, path[-1][2] - 0.04, 0.05, "snow", c)
    box("Hat patch", 0.085, hy + 0.03, hz + 0.16, 0.05, 0.05, 0.05, "patch", c, rot=(0.3, 0, 0.4))
    rig.claim("hat")
    add_bone("hat", (0, hy + 0.01, hz + 0.12), (0, hy + 0.2, hz + 0.32), "head")

    arm = build_armature(c, bones, rig.bone_of)
    animate_elf(arm)


def build_armature(c, bones, bone_of):
    """Makes the skeleton and binds each part fully to its bone (vertex group weight 1 + Armature modifier).
    Every bone's roll is set so its local Z axis points forward (-Y): a positive X rotation then swings any
    bone forward (arms reach forward, legs step forward, the spine leans forward)."""
    from mathutils import Vector
    data = bpy.data.armatures.new("Elf rig")
    arm = bpy.data.objects.new("Elf rig", data)
    c.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in bones:
        b = data.edit_bones.new(name)
        b.head, b.tail = head, tail
        along = (Vector(tail) - Vector(head)).normalized()
        b.align_roll(Vector((0, 0, 1)) if abs(along.y) > 0.7 else Vector((0, -1, 0)))
        if parent:
            b.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")

    for ob, bone in bone_of.items():
        group = ob.vertex_groups.new(name=bone)
        group.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
        ob.parent = arm
        ob.modifiers.new("Armature", "ARMATURE").object = arm
    return arm


# Elf actions: {frame: {bone: (x, y, z) degrees}}. Bones left out of a key are at rest (0, 0, 0).
# "root.z" is a height offset in metres (the walk bounce). 30 frames per second.
ELF_ACTIONS = {
    # Breathing, a grumpy head tilt and a lazy hat sway. Loops over 2 s.
    "Idle": {
        0: {},
        30: {"chest": (3, 0, 0), "head": (-4, 0, 5), "hat": (-8, 0, -4), "upper_arm.L": (4, 0, 0), "upper_arm.R": (4, 0, 0)},
        60: {},
    },
    # Stomping walk: legs and arms swing, the body bounces and the hat flops. Loops every 0.67 s.
    "Walk": {
        0: {"leg.L": (28, 0, 0), "leg.R": (-28, 0, 0), "foot.L": (-10, 0, 0), "upper_arm.L": (-25, 0, 0),
            "upper_arm.R": (25, 0, 0), "forearm.L": (20, 0, 0), "forearm.R": (20, 0, 0), "chest": (4, 0, 6), "hat": (-6, 0, 0)},
        5: {"forearm.L": (20, 0, 0), "forearm.R": (20, 0, 0), "chest": (4, 0, 0), "hat": (-14, 0, 0), "root.z": 0.025},
        10: {"leg.L": (-28, 0, 0), "leg.R": (28, 0, 0), "foot.R": (-10, 0, 0), "upper_arm.L": (25, 0, 0),
             "upper_arm.R": (-25, 0, 0), "forearm.L": (20, 0, 0), "forearm.R": (20, 0, 0), "chest": (4, 0, -6), "hat": (-6, 0, 0)},
        15: {"forearm.L": (20, 0, 0), "forearm.R": (20, 0, 0), "chest": (4, 0, 0), "hat": (-14, 0, 0), "root.z": 0.025},
        20: {"leg.L": (28, 0, 0), "leg.R": (-28, 0, 0), "foot.L": (-10, 0, 0), "upper_arm.L": (-25, 0, 0),
             "upper_arm.R": (25, 0, 0), "forearm.L": (20, 0, 0), "forearm.R": (20, 0, 0), "chest": (4, 0, 6), "hat": (-6, 0, 0)},
    },
    # One hammer swing with the right hand, 0.6 s. Frame 0 is the "ready" pose held between swings (hammer raised);
    # the left hand steadies the toy. The hammer points forward from the fist when the arm hangs down, so the sum of
    # the arm's three X angles is the hammer's tilt: 90 = straight up, 180+ = back over the shoulder, 0 = level.
    # The upper arm is also swung out 45 degrees (Y), so the hammer comes down beside the head, where the shoulder
    # camera can see it, onto the toy at the elf's right.
    # The strike levels it out at shoulder height, which is the low bench's top plus a toy, half a metre ahead.
    "Work": {
        0: {"upper_arm.R": (100, 45, 0), "forearm.R": (40, 0, 0), "hand.R": (-60, 0, 0),
            "upper_arm.L": (75, 0, 0), "forearm.L": (20, 0, 0), "chest": (6, 0, 0), "head": (6, 0, 0)},
        6: {"upper_arm.R": (160, 45, 0), "forearm.R": (30, 0, 0), "hand.R": (0, 0, 0),
            "upper_arm.L": (75, 0, 0), "forearm.L": (20, 0, 0), "chest": (-6, 0, 0), "head": (-8, 0, 0), "hat": (12, 0, 0)},
        10: {"upper_arm.R": (95, 45, 0), "forearm.R": (0, 0, 0), "hand.R": (-95, 0, 0),
             "upper_arm.L": (75, 0, 0), "forearm.L": (20, 0, 0), "chest": (14, 0, 0), "head": (10, 0, 0), "hat": (-16, 0, 0)},
        13: {"upper_arm.R": (95, 45, 0), "forearm.R": (0, 0, 0), "hand.R": (-95, 0, 0),
             "upper_arm.L": (75, 0, 0), "forearm.L": (20, 0, 0), "chest": (12, 0, 0), "head": (8, 0, 0), "hat": (-8, 0, 0)},
        18: {"upper_arm.R": (100, 45, 0), "forearm.R": (40, 0, 0), "hand.R": (-60, 0, 0),
             "upper_arm.L": (75, 0, 0), "forearm.L": (20, 0, 0), "chest": (6, 0, 0), "head": (6, 0, 0)},
    },
    # One scoop of coal with the shovel in the right hand, 0.8 s: hold it ready, bend and drive the blade down into
    # the scuttle, then heave it up with the coal. The left hand helps on the shaft. The shovel points forward-down
    # from the fist when the arm hangs, so lower arm angles dig and higher ones lift.
    "Shovel": {
        0: {"upper_arm.R": (25, 0, 0), "forearm.R": (35, 0, 0), "hand.R": (-10, 0, 0),
            "upper_arm.L": (40, 0, -10), "forearm.L": (45, 0, 0), "chest": (8, 0, 0)},
        8: {"upper_arm.R": (45, 0, 0), "forearm.R": (5, 0, 0), "hand.R": (5, 0, 0),
            "upper_arm.L": (55, 0, -10), "forearm.L": (20, 0, 0), "chest": (28, 0, 0), "head": (12, 0, 0),
            "hat": (-10, 0, 0), "leg.L": (-12, 0, 0), "leg.R": (18, 0, 0), "root.z": -0.03},
        12: {"upper_arm.R": (40, 0, 0), "forearm.R": (5, 0, 0), "hand.R": (12, 0, 0),
             "upper_arm.L": (50, 0, -10), "forearm.L": (20, 0, 0), "chest": (30, 0, 0), "head": (12, 0, 0),
             "hat": (-14, 0, 0), "leg.L": (-12, 0, 0), "leg.R": (18, 0, 0), "root.z": -0.035},
        19: {"upper_arm.R": (75, 0, 0), "forearm.R": (55, 0, 0), "hand.R": (-35, 0, 0),
             "upper_arm.L": (60, 0, -10), "forearm.L": (60, 0, 0), "chest": (-4, 0, 0), "head": (-6, 0, 0),
             "hat": (10, 0, 0)},
        24: {"upper_arm.R": (60, 0, 0), "forearm.R": (60, 0, 0), "hand.R": (-40, 0, 0),
             "upper_arm.L": (50, 0, -10), "forearm.L": (60, 0, 0), "chest": (2, 0, 0)},
    },
    # Holding an item up on the right hand beside the head, like a tray (held in front, the big head would hide it from
    # the camera behind). Only the right arm is used: the game lays it over Walk or Idle, so the legs keep going.
    # Z swings the right arm out to the side (negative = outwards); the forearm carries on up and the hand bends in flat
    # under the item. A slow sway; loops every 2 s.
    "Carry": {
        0: {"upper_arm.R": (10, 0, -100), "forearm.R": (0, 0, -60), "hand.R": (0, 0, 75)},
        30: {"upper_arm.R": (14, 0, -97), "forearm.R": (0, 0, -63), "hand.R": (0, 0, 75)},
        60: {"upper_arm.R": (10, 0, -100), "forearm.R": (0, 0, -60), "hand.R": (0, 0, 75)},
    },
}


def animate_elf(arm):
    """Keys every action and pushes each onto its own NLA track, so the glTF export includes all of them."""
    bpy.context.scene.render.fps = 30
    arm.animation_data_create()
    for name, keys in ELF_ACTIONS.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        arm.animation_data.action = action
        used = {b for pose in keys.values() for b in pose if b != "root.z"}
        for frame, pose in keys.items():
            for bone in used:
                pb = arm.pose.bones[bone]
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = [math.radians(a) for a in pose.get(bone, (0, 0, 0))]
                pb.keyframe_insert("rotation_euler", frame=frame)
            root = arm.pose.bones["root"]
            root.location = (0, 0, 0)
            # Bone-local axes: the root bone points up, so its local Y is world up.
            root.location.y = pose.get("root.z", 0.0)
            root.keyframe_insert("location", frame=frame)
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 0, action)
        arm.animation_data.action = None
    for pb in arm.pose.bones:  # leave the file in the rest pose
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)


# ================================================================ toys and items

# Each toy is built from three parts (the same ids as in toys.json). A part model holds just that piece, placed
# where it sits on the finished toy, so the game can stack the pieces up on the bench as they are hammered on.
# The finished toy is all of its parts; the botched toy is the same parts put together wrong.

def train_wheels(c):
    box("Chassis", 0, 0, 0.05, 0.46, 0.2, 0.05, "ink", c)
    cone("Cowcatcher", -0.25, 0, 0.08, 0.08, 0.0, 0.06, "ink", c, verts=4, rot=(0, math.radians(-90), 0))
    for x in (-0.15, -0.02, 0.13):
        for side in (-1, 1):
            cyl(f"Wheel.{x}.{side}", x, side * 0.11, 0.065, 0.065, 0.03, "Y", "ink", c, verts=12)
            cyl(f"Hub.{x}.{side}", x, side * 0.127, 0.065, 0.025, 0.01, "Y", "gold", c)


def train_boiler(c):
    cyl("Boiler", -0.06, 0, 0.2, 0.1, 0.3, "X", "train_red", c, verts=12)
    cyl("Boiler front", -0.215, 0, 0.2, 0.09, 0.02, "X", "gold", c, verts=12)
    cyl("Chimney", -0.17, 0, 0.33, 0.04, 0.1, "Z", "ink", c)
    cyl("Chimney top", -0.17, 0, 0.39, 0.055, 0.03, "Z", "gold", c)
    sphere("Dome", -0.04, 0, 0.3, 0.045, "gold", c)


def train_cab(c):
    box("Cab", 0.15, 0, 0.1, 0.16, 0.22, 0.22, "train_red", c)
    box("Cab roof", 0.15, 0, 0.32, 0.2, 0.26, 0.03, "ink", c)
    box("Cab window", 0.15, -0.112, 0.2, 0.08, 0.01, 0.06, "glass", c)


def bear_body(c):
    sphere("Body", 0, 0, 0.19, 0.16, "bear_fur", c, scale=(1.0, 0.9, 1.15))
    sphere("Belly", 0, -0.09, 0.18, 0.1, "bear_light", c, scale=(1.0, 0.6, 1.1))
    for side in (-1, 1):
        sphere(f"Leg.{side}", side * 0.09, -0.09, 0.06, 0.065, "bear_fur", c, scale=(1, 1.3, 0.9))
        sphere(f"Paw.{side}", side * 0.09, -0.16, 0.06, 0.035, "bear_light", c, segments=8, rings=5)


def bear_arms(c):
    for side in (-1, 1):
        sphere(f"Arm.{side}", side * 0.15, -0.03, 0.24, 0.055, "bear_fur", c, scale=(1, 1, 1.5))
    torus("Scarf", 0, 0, 0.32, 0.1, 0.03, "berry", c)


def bear_head(c):
    sphere("Head", 0, 0, 0.43, 0.13, "bear_fur", c)
    for side in (-1, 1):
        sphere(f"Ear.{side}", side * 0.09, 0, 0.54, 0.05, "bear_fur", c)
        sphere(f"Inner ear.{side}", side * 0.09, -0.03, 0.54, 0.028, "bear_light", c, segments=8, rings=5)
        sphere(f"Eye.{side}", side * 0.045, -0.115, 0.46, 0.018, "ink", c, segments=8, rings=5)
    sphere("Muzzle", 0, -0.11, 0.41, 0.055, "bear_light", c)
    sphere("Nose", 0, -0.16, 0.42, 0.02, "ink", c, segments=8, rings=5)


def robot_legs(c):
    for side in (-1, 1):
        box(f"Leg.{side}", side * 0.07, 0, 0, 0.08, 0.1, 0.12, "robot_dark", c)
        box(f"Foot.{side}", side * 0.07, -0.02, 0, 0.1, 0.14, 0.03, "ink", c)


def robot_torso(c):
    for side in (-1, 1):
        cyl(f"Arm.{side}", side * 0.18, 0, 0.24, 0.03, 0.18, "Z", "steel", c)
        sphere(f"Hand.{side}", side * 0.18, 0, 0.14, 0.04, "robot_dark", c)
    box("Body", 0, 0, 0.12, 0.3, 0.2, 0.26, "robot_body", c)
    box("Chest panel", 0, -0.101, 0.18, 0.16, 0.01, 0.12, "ink", c)
    for n, m in enumerate(("berry", "gold", "pine")):
        sphere(f"Button.{n}", -0.04 + n * 0.04, -0.11, 0.24, 0.015, m, c, segments=6, rings=4)
    box("Neck", 0, 0, 0.38, 0.06, 0.06, 0.03, "steel", c)


def robot_head(c):
    box("Head", 0, 0, 0.41, 0.22, 0.18, 0.15, "robot_body", c)
    for side in (-1, 1):
        box(f"Eye.{side}", side * 0.05, -0.091, 0.47, 0.045, 0.01, 0.04, "lamp_glow", c)
        cyl(f"Ear bolt.{side}", side * 0.115, 0, 0.48, 0.025, 0.02, "X", "steel", c)
    box("Mouth", 0, -0.091, 0.43, 0.09, 0.01, 0.015, "ink", c)
    cyl("Antenna", 0, 0, 0.6, 0.008, 0.08, "Z", "steel", c, verts=6)
    sphere("Antenna tip", 0, 0, 0.65, 0.025, "berry", c)


TOY_PARTS = {
    "toy_train": [("wheels", train_wheels), ("boiler", train_boiler), ("cab", train_cab)],
    "toy_bear": [("body", bear_body), ("arms", bear_arms), ("head", bear_head)],
    "toy_robot": [("legs", robot_legs), ("torso", robot_torso), ("head", robot_head)],
}

# How each part ends up on a botched toy: (move in metres, turn in degrees), around the part's own centre.
BOTCHED = {
    "toy_train": {"wheels": ((0.02, 0.0, 0.3), (180, 0, 14)),       # wheels upside down on top
                  "boiler": ((0.0, 0.0, -0.08), (0, 0, 75)),         # boiler crosswise
                  "cab": ((0.12, 0.08, 0.0), (0, -60, 20))},        # cab knocked over
    "toy_bear": {"body": ((0.0, 0.0, 0.0), (0, 0, 0)),
                 "arms": ((0.0, 0.0, 0.1), (0, 0, 90)),             # arms on back to front, scarf slipped up
                 "head": ((0.2, -0.02, -0.28), (0, 110, 0))},       # head on sideways at the hip
    "toy_robot": {"legs": ((0.0, 0.0, 0.5), (180, 0, 0)),           # legs on top
                  "torso": ((0.0, 0.0, 0.0), (0, 18, 0)),
                  "head": ((0.18, 0.0, -0.36), (0, 0, 35))},        # head at the bottom
}


def built(c, fn):
    """Runs a builder and returns the objects it added."""
    before = set(c.objects)
    fn(c)
    return [o for o in c.objects if o not in before]


def move_around_centre(objects, offset, turn_degrees):
    from mathutils import Euler, Matrix, Vector
    corners = [o.matrix_world @ Vector(v) for o in objects for v in o.bound_box]
    centre = sum(corners, Vector()) / len(corners)
    turn = Euler([math.radians(a) for a in turn_degrees]).to_matrix().to_4x4()
    m = Matrix.Translation(centre + Vector(offset)) @ turn @ Matrix.Translation(-centre)
    for o in objects:
        o.matrix_world = m @ o.matrix_world
    apply_transforms(objects)


def apply_transforms(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def drop_to_floor(c):
    meshes = [o for o in c.objects if o.type == "MESH"]
    lowest = min((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices)
    for o in meshes:
        o.location.z -= lowest


def whole_toy(name):
    def build(c):
        for _, part in TOY_PARTS[name]:
            part(c)
    return build


def toy_part(fn):
    def build(c):
        fn(c)
        bpy.context.scene["toy_part"] = True  # sits on the part below it, so it may float above the floor
    return build


def botched_toy(name):
    def build(c):
        for part_id, part in TOY_PARTS[name]:
            offset, turn = BOTCHED[name][part_id]
            move_around_centre(built(c, part), offset, turn)
        drop_to_floor(c)
        for n in range(4):  # bent nails sticking out of the mess
            from mathutils import Vector
            limb(f"Bent nail.{n}", (0.1 * (n - 1.5), -0.1 + 0.05 * n, 0.15 + 0.06 * n),
                 (0.1 * (n - 1.5) + 0.04, -0.14 + 0.05 * n, 0.2 + 0.07 * n), 0.008, 0.004, "steel", c, verts=4)
    return build


def shovel(c):
    """Small coal shovel, 0.78 m, standing on its blade. The elf holds it 0.62 m up the shaft (just under the D-grip);
    the blade's scoop opens towards -Y."""
    box("Blade", 0, 0, 0.0, 0.17, 0.02, 0.2, "iron", c)
    for side in (-1, 1):
        box(f"Blade side.{side}", side * 0.085, -0.03, 0.0, 0.012, 0.07, 0.17, "iron", c)
    box("Blade lip", 0, -0.035, 0.0, 0.17, 0.05, 0.012, "steel", c)
    cone("Socket", 0, 0, 0.23, 0.028, 0.018, 0.07, "iron", c, verts=8)
    cyl("Shaft", 0, 0, 0.45, 0.016, 0.42, "Z", "log_end", c, verts=8)
    cyl("Grip wrap", 0, 0, 0.62, 0.02, 0.08, "Z", "berry", c, verts=8)
    torus("D-grip", 0, 0, 0.72, 0.05, 0.012, "log_end", c, rot=(math.pi / 2, 0, 0), scale=(1, 1, 0.75), segments=(10, 4))


def hammer(c):
    """Claw hammer, 0.34 m, standing on the end of its handle. The elf holds it by the grip (6 cm up the handle);
    the striking face points -Y and the claw +Y."""
    cyl("Handle", 0, 0, 0.15, 0.016, 0.3, "Z", "log_end", c, verts=8)
    cyl("Grip", 0, 0, 0.06, 0.021, 0.11, "Z", "berry", c, verts=8)
    box("Head", 0, 0, 0.29, 0.045, 0.15, 0.05, "iron", c)
    cyl("Face", 0, -0.085, 0.315, 0.027, 0.03, "Y", "steel", c, verts=10)
    for side in (-1, 1):
        limb(f"Claw.{side}", (side * 0.012, 0.07, 0.325), (side * 0.014, 0.135, 0.285), 0.013, 0.003, "iron", c, verts=6)


def coal_lumps(c, x, y, z, count, spread, size, embers=0):
    """A heap of chunky, faceted coal lumps (low-poly spheres squashed at random), some glowing as embers."""
    for n in range(count):
        a = n * 2.39996
        r = spread * math.sqrt((n + 0.5) / count)
        lx, ly = x + math.cos(a) * r, y + math.sin(a) * r
        lz = z + (1 - r / max(spread, 1e-6)) * size * 1.2  # higher in the middle: a heap
        s = size * random.uniform(0.75, 1.2)
        mat = "ember" if n < embers else ("coal_shine" if n % 3 == 0 else "coal")
        sphere(f"Coal.{n}", lx, ly, lz, s, mat, c, segments=6, rings=4,
               scale=(random.uniform(0.8, 1.25), random.uniform(0.8, 1.25), random.uniform(0.6, 0.9)))


def coal_lump(c):
    """What the elf carries for a naughty child: a small heap of coal, about 0.25 m across."""
    coal_lumps(c, 0, 0, 0.05, 7, 0.08, 0.055)
    drop_to_floor(c)


def station_coal(c):
    """Iron coal scuttle on the hearth, 0.6 x 0.6, about 0.35 m high, heaped with coal and a few glowing embers."""
    cone("Scuttle", 0, 0, 0.14, 0.2, 0.25, 0.28, "iron", c, verts=12)
    torus("Rim", 0, 0, 0.28, 0.25, 0.018, "iron", c, segments=(14, 4))
    cyl("Foot", 0, 0, 0.012, 0.21, 0.024, "Z", "iron", c, verts=12)
    torus("Handle", 0, 0, 0.36, 0.17, 0.012, "steel", c, rot=(0, math.pi / 2, 0), scale=(1, 1, 0.7), segments=(12, 4))
    coal_lumps(c, 0, 0, 0.27, 14, 0.19, 0.055, embers=3)
    # The shovel is its own model (shovel.blend): the game leans it here and puts it in the elf's hands to dig.


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


def supply_crate(c):
    """A crate of spare kits from the supply sled, about 0.44 x 0.38 x 0.36 m: dark wood, green straps, a gold star and
    rope handles, so it never looks like a toy kit."""
    w, d, h = 0.44, 0.38, 0.36
    box("Supply body", 0, 0, 0, w, d, h, "plank_b", c)
    box("Supply lid", 0, 0, h - 0.035, w + 0.02, d + 0.02, 0.045, "log_dark", c)
    for x in (-1, 1):
        box(f"Supply strap.{x}", x * 0.12, 0, -0.002, 0.05, d + 0.012, h + 0.006, "pine", c)
        torus(f"Rope handle.{x}", x * (w / 2 + 0.01), 0, h * 0.62, 0.06, 0.012, "burlap", c, rot=(0, math.pi / 2, 0),
              scale=(1, 1, 0.6), segments=(10, 4))
    cone("Star", 0, -d / 2 - 0.006, h * 0.45, 0.085, 0.0, 0.012, "gold", c, verts=5, rot=(math.pi / 2, 0, 0))


def ribbon_bow(c):
    """Bow on a ribboned toy. Near-white: the game tints it with the ribbon's colour (gold when there is none)."""
    for side in (-1, 1):
        torus(f"Loop.{side}", side * 0.065, 0, 0.08, 0.05, 0.015, "ribbon_white", c, rot=(math.pi / 2, 0, 0), scale=(1.2, 1, 0.8))
        box(f"Tail.{side}", side * 0.04, -0.02, 0.01, 0.025, 0.01, 0.07, "ribbon_white", c, rot=(0, side * 0.4, 0))
    sphere("Knot", 0, 0, 0.08, 0.025, "ribbon_shade", c)


def guide_arrow(c):
    """
    One tutorial arrow on the floor: a wide chevron pointing forward (-Y), about 0.45 m across and only 4 cm tall, with
    a raised bright ridge along each arm so it reads as 3D. The game draws a trail of them, see-through and unlit (they
    glow gold), from the elf to where it should go next.
    """
    w, t, h = 0.32, 0.1, 0.03  # arm length, arm width, height
    a = math.radians(50)       # each arm runs back and out, 50 degrees off straight back
    for side in (-1, 1):
        # The arm's front end overlaps the other's at the tip (x = 0), so the point is solid.
        cx = side * (math.sin(a) * w / 2 - t * 0.25)
        cy = -0.12 + math.cos(a) * w / 2
        rot = (0, 0, -side * a)
        box(f"Arm.{side}", cx, cy, 0, t, w, h, "guide", c, rot=rot)
        box(f"Ridge.{side}", cx, cy, h, t * 0.45, w * 0.88, 0.014, "guide_edge", c, rot=rot)


def ribbon_spool(c):
    """A spool of ribbon lying on its side, 0.12 m across. Near-white: the game tints it with each ribbon colour."""
    r, w = 0.06, 0.09
    for side in (-1, 1):
        cyl(f"Flange.{side}", side * w / 2, 0, r, r, 0.012, "X", "ribbon_shade", c, verts=14)
    cyl("Wound ribbon", 0, 0, r, r * 0.82, w - 0.012, "X", "ribbon_white", c, verts=14)
    cyl("Hole", 0, 0, r, r * 0.22, w + 0.004, "X", "ribbon_shade", c, verts=8)
    box("Loose end", 0, -r * 0.95, 0, w * 0.75, 0.004, r, "ribbon_white", c)
    box("Loose tip", 0, -r * 1.25, 0, w * 0.75, r * 0.55, 0.004, "ribbon_white", c)


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


def station_supply(c):
    """The supply sled, 2.0 x 1.2 (front -Y): crates of spare kits stacked on a red sled with a gold-star sign. The elf
    takes a crate here to restock an empty kit bin. Up against a wall, so the stack is tallest at the back."""
    for side in (-1, 1):
        box(f"Runner.{side}", 0, side * 0.5, 0, 1.9, 0.07, 0.07, "berry", c)
        torus(f"Curl.{side}", -0.95, side * 0.5, 0.18, 0.15, 0.03, "berry", c, rot=(math.pi / 2, 0, 0))
        for x in (-0.6, 0.0, 0.6):
            box(f"Strut.{side}.{x}", x, side * 0.5, 0.06, 0.06, 0.06, 0.16, "plank_b", c)
    box("Deck", 0, 0, 0.2, 1.85, 1.1, 0.06, "plank_a", c)
    # Crates: a row of three at the front, two on top behind, one on top of those.
    stack = [(-0.55, -0.22, 0.26, 0.1), (0.0, -0.25, 0.26, -0.06), (0.55, -0.2, 0.26, 0.05),
             (-0.5, 0.25, 0.26, -0.04), (0.05, 0.27, 0.26, 0.08), (0.58, 0.24, 0.26, -0.1),
             (-0.25, 0.22, 0.62, 0.12), (0.32, 0.24, 0.62, -0.05), (0.05, 0.25, 0.98, 0.2)]
    for n, (x, y, z, turn) in enumerate(stack):
        w, d, h = 0.44, 0.38, 0.36
        box(f"Crate.{n}", x, y, z, w, d, h, "plank_b", c, rot=(0, 0, turn))
        box(f"Crate lid.{n}", x, y, z + h - 0.03, w + 0.02, d + 0.02, 0.04, "log_dark", c, rot=(0, 0, turn))
        box(f"Crate strap.{n}", x, y, z - 0.002, 0.05, d + 0.012, h + 0.006, "pine", c, rot=(0, 0, turn))
    # A tarp folded back over the side and a rope.
    box("Tarp", 0.82, 0.05, 0.26, 0.08, 1.0, 0.3, "burlap", c, rot=(0, 0.2, 0))
    torus("Rope coil", -0.75, 0.32, 0.64, 0.12, 0.022, "burlap", c, segments=(12, 4))
    # Sign on a post at the front corner: a gold star on green.
    box("Sign post", -0.85, -0.5, 0.0, 0.06, 0.06, 1.25, "log_dark", c)
    box("Sign board", -0.85, -0.53, 1.0, 0.5, 0.04, 0.36, "pine_dark", c)
    box("Sign trim", -0.85, -0.535, 0.98, 0.54, 0.035, 0.4, "gold", c)
    cone("Sign star", -0.85, -0.565, 1.18, 0.11, 0.0, 0.015, "gold", c, verts=5, rot=(math.pi / 2, 0, 0))


def station_bench(c):
    """Low elf-height workbench, 3.0 x 1.0, top at 0.55 m (so the short elf can swing a hammer over it),
    worked from the -Y side. The hammer is its own model (hammer.blend); the game puts it on the bench."""
    workbench(c)


def prop_npc_bench(c):
    """Another workbench where a workshop elf builds trains. The game draws the elf at an npc_<n> marker in room.blend,
    standing on the mat on this bench's -Y side. A finished train and a half-built one sit on top; no pegboard, so the
    elf's hammering can be seen from across the room."""
    h = workbench(c, pegboard=False)
    # The elf (at x -0.15 on the mat, facing +Y) hammers at its right: a low train chassis sits there so it never hides
    # the elf from the room. A finished train waits further along, and a loose cab on the other side.
    move_around_centre(built(c, train_wheels), (0.1, -0.3, h), (0, 0, 80))
    move_around_centre(built(c, whole_toy("toy_train")), (0.95, 0.1, h), (0, 0, 25))
    move_around_centre(built(c, train_cab), (-0.75, 0.0, h - 0.1), (0, 0, -20))
    disc("Work mat", 0, -0.82, 0, 0.5, 0.32, 0.01, "rug_a", c)


def workbench(c, pegboard=True):
    """The bench shared by the assembly station and the elves' benches. Returns the top height."""
    w, d, h = 3.0, 1.0, 0.55
    box("Top", 0, 0, h - 0.1, w, d, 0.1, "bench_top", c)
    for x in (-1, 1):
        for y in (-1, 1):
            box(f"Leg.{x}.{y}", x * (w / 2 - 0.1), y * (d / 2 - 0.1), 0, 0.12, 0.12, h - 0.1, "log", c)
    box("Lower shelf", 0, 0.05, 0.1, w - 0.3, d - 0.3, 0.04, "plank_b", c)
    for n, x in enumerate((-0.9, -0.3, 0.5)):  # boxes of parts on the shelf
        box(f"Parts box.{n}", x, 0.05, 0.14, 0.4, 0.35, 0.16, "crate_edge", c)
    # Vice on the front right.
    box("Vice body", 1.15, -0.45, h, 0.22, 0.18, 0.12, "iron", c)
    cyl("Vice screw", 1.15, -0.6, h + 0.06, 0.015, 0.2, "Y", "steel", c, verts=6)
    # Tools on top.
    box("Saw blade", 0.1, 0.15, h, 0.5, 0.12, 0.01, "steel", c)
    box("Saw handle", 0.4, 0.15, h, 0.12, 0.1, 0.03, "berry", c)
    for n in range(5):
        cyl(f"Nail.{n}", -0.2 + n * 0.05, -0.3, h + 0.01, 0.006, 0.05, "Y", "steel", c, verts=4)
    box("Plank offcut", 0.75, 0.2, h, 0.5, 0.15, 0.04, "plank_c", c)
    cyl("Paint pot", -1.25, 0.25, h + 0.06, 0.07, 0.12, "Z", "train_red", c, verts=10)
    if pegboard:  # board of tools behind the bench
        box("Pegboard", 0, d / 2 - 0.03, h, 1.6, 0.04, 0.6, "plank_a", c)
        for n, x in enumerate((-0.6, -0.2, 0.2, 0.6)):
            box(f"Hung tool.{n}", x, d / 2 - 0.07, h + 0.15, 0.06, 0.03, 0.35, ("iron", "steel", "berry", "iron")[n], c)
    return h


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

TREE_TOP = 2.5  # the tip under the star


def tree_radius(z):
    """How far the tree's boughs reach at height z (a cone from 1.0 m at the bottom to nothing at the tip)."""
    return max(0.06, 1.0 * (TREE_TOP - z) / (TREE_TOP - 0.3))


def tree_point(z, angle, out=0.0):
    """A point on the tree's surface at height z, around it by angle (radians), pushed out by `out` metres."""
    r = tree_radius(z) * 0.93 + out
    return (math.cos(angle) * r, math.sin(angle) * r, z)


def prop_tree(c):
    """
    A full Christmas tree, 2.6 m: layered boughs, a gold tinsel garland spiralling down, a string of big old-fashioned
    (C9) incandescent bulbs in pink, orange, blue, green and yellow, a modest scatter of glass baubles (not covering
    it), a glowing star and presents. Each bulb and the star carry a glow marker: the game draws a soft twinkling halo
    there. Coloured point lights spill pink and orange onto the room.
    """
    rnd = random.Random(1225)
    cyl("Trunk", 0, 0, 0.18, 0.1, 0.36, "Z", "log_dark", c)
    cyl("Tree stand", 0, 0, 0.05, 0.28, 0.1, "Z", "berry", c, verts=12)

    # Boughs: overlapping tiers, each a drooping skirt (a wide flat cone) under a taller cone, turned at random so the
    # silhouette isn't too regular.
    tiers = [(0.3, 1.0), (0.62, 0.86), (0.92, 0.72), (1.2, 0.6), (1.48, 0.48), (1.75, 0.37), (2.0, 0.26), (2.22, 0.16)]
    shades = ("pine_dark", "pine", "pine_light")
    for n, (z, r) in enumerate(tiers):
        h = 0.5 if n < len(tiers) - 1 else 0.38
        turn = (0, 0, rnd.uniform(0, math.tau))
        cone(f"Bough skirt.{n}", 0, 0, z + 0.07, r * 1.04, r * 0.72, 0.14, shades[n % 3], c, verts=12, rot=turn)
        cone(f"Bough.{n}", 0, 0, z + h / 2 + 0.05, r * 0.92, r * 0.15 if n < len(tiers) - 1 else 0.0, h,
             shades[(n + 1) % 3], c, verts=12, rot=turn)
    cone("Tip", 0, 0, 2.47, 0.09, 0.0, 0.2, "pine_light", c, verts=10)

    # Gold tinsel garland: a thick, slightly twisting tube spiralling three and a half times down the tree.
    turns, z0, z1, segs = 3.5, 2.2, 0.45, 56
    prev = None
    for i in range(segs + 1):
        t = i / segs
        z = z0 + (z1 - z0) * t
        p = tree_point(z, t * turns * math.tau, out=0.04 + 0.015 * math.sin(t * 40))
        if prev:
            limb(f"Tinsel.{i}", prev, p, 0.022, 0.022, "tinsel", c, verts=5)
        prev = p

    # A string of C9 bulbs on a dark wire, the other way round the tree. Each bulb is a fat teardrop pointing outwards
    # and a little down, on a brass base; colours cycle so no two neighbours match.
    colours = (("bulb_pink", (1.0, 0.45, 0.68)), ("bulb_orange", (1.0, 0.55, 0.18)), ("bulb_blue", (0.35, 0.55, 1.0)),
               ("bulb_green", (0.35, 0.9, 0.45)), ("bulb_yellow", (1.0, 0.85, 0.35)))
    turns, z0, z1, segs = 4.5, 2.3, 0.4, 48
    prev = None
    bulb = 0
    for i in range(segs + 1):
        t = i / segs
        z = z0 + (z1 - z0) * t
        a = -t * turns * math.tau + 1.3
        p = tree_point(z, a, out=0.02)
        if prev:
            limb(f"Light wire.{i}", prev, p, 0.006, 0.006, "light_wire", c, verts=4)
        prev = p
        if i < segs:  # a bulb at every joint of the wire
            mat, rgb = colours[bulb % len(colours)]
            out = Vector((math.cos(a), math.sin(a), -0.35)).normalized()
            base = Vector(p)
            tip = base + out * 0.075
            limb(f"Bulb base.{i}", base, base + out * 0.018, 0.013, 0.013, "bulb_base", c, verts=4)
            limb(f"Bulb.{i}", base + out * 0.012, tip, 0.024, 0.004, mat, c, verts=6)
            glow(f"Bulb glow.{i}", *(base + out * 0.04), rgb, 0.26, 0.55, c)
            bulb += 1

    # Baubles, sprinkled sparingly: a mix of colours and sizes, a few teardrops, kept off the very top.
    picks = ("berry", "gold", "silver", "gift_a", "berry", "gold", "silver", "snow")
    for n in range(15):
        z = rnd.uniform(0.5, 2.05)
        a = n * 2.399 + rnd.uniform(-0.3, 0.3)  # golden angle: evenly spread, never in a column
        x, y, zz = tree_point(z, a, out=0.035)
        mat = picks[n % len(picks)]
        if n % 5 == 4:  # a glass teardrop
            cone(f"Teardrop.{n}", x, y, zz - 0.05, 0.045, 0.0, 0.13, mat, c, verts=8, rot=(math.pi, 0, 0))
        else:
            sphere(f"Bauble.{n}", x, y, zz - 0.04, rnd.uniform(0.045, 0.07), mat, c, segments=7, rings=4)
        cyl(f"Bauble cap.{n}", x, y, zz + 0.02, 0.012, 0.02, "Z", "gold", c, verts=5)

    # The star: five gold points and a glowing heart, facing the front (-Y).
    for k in range(5):
        a = math.pi / 2 + k * math.tau / 5
        d = Vector((math.cos(a), 0, math.sin(a)))
        limb(f"Star point.{k}", Vector((0, -0.02, 2.62)), Vector((0, -0.02, 2.62)) + d * 0.16, 0.05, 0.0, "star_glow", c, verts=4)
    sphere("Star heart", 0, -0.02, 2.62, 0.055, "star_glow", c, segments=8, rings=5)
    glow("Star glow", 0, -0.06, 2.62, (1.0, 0.86, 0.45), 0.75, 0.15, c)

    light("Tree lights", 0, -0.2, 1.3, (1.0, 0.82, 0.55), 0.6, 3.0, 0.15, c)    # warm twinkle from the decorations
    light("Tree pink", 0.5, -0.5, 1.0, (1.0, 0.45, 0.7), 0.35, 2.4, 0.12, c)      # the old bulbs' coloured spill
    light("Tree orange", -0.5, -0.5, 1.6, (1.0, 0.55, 0.2), 0.35, 2.4, 0.12, c)
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
    box("Hearth", 0, face - 0.25, 0, width + 0.2, 0.55, 0.06, "stone_c", c)
    box("Mantel", 0, face - 0.12, 1.3, width + 0.3, 0.3, 0.12, "beam", c)
    for n, sx in enumerate((-0.8, 0.0, 0.8)):
        box(f"Stocking.{n}", sx, face - 0.1, 0.9, 0.18, 0.06, 0.38, "berry" if n != 1 else "pine", c)
        box(f"Stocking cuff.{n}", sx, face - 0.1, 1.22, 0.2, 0.07, 0.08, "snow", c)
    sphere("Mantel clock", 0, face - 0.1, 1.5, 0.1, "gold", c)
    light("Fire light", 0, face - 0.6, 0.6, (1.0, 0.55, 0.25), 3.2, 8.0, 0.35, c)
    for n, sx in enumerate((-1.0, 1.0)):
        cyl(f"Candle.{n}", sx, face - 0.1, 1.5, 0.03, 0.14, "Z", "snow", c, verts=8)

    # Everything so far stays still (the "root" bone); each flame gets its own bone so the Burn action can make
    # it lick, stretch and sway. The game loops a prop's animation.
    rig = Rig(c)
    bones = [("root", (0, 0, 0), (0, 0, 0.3), None)]
    rig.claim("root")
    for n, (fx, fh) in enumerate(((-0.25, 0.45), (0.0, 0.6), (0.25, 0.4))):
        cone(f"Flame.{n}", fx, face - 0.25, 0.15 + fh / 2, 0.12, 0.0, fh, "flame", c, verts=6)
        cone(f"Flame core.{n}", fx, face - 0.28, 0.15 + fh * 0.35, 0.06, 0.0, fh * 0.6, "flame_core", c, verts=6)
        rig.claim(f"flame.{n}")
        bones.append((f"flame.{n}", (fx, face - 0.26, 0.15), (fx, face - 0.26, 0.15 + fh), "root"))
    for n, sx in enumerate((-1.0, 1.0)):
        cone(f"Candle flame.{n}", sx, face - 0.1, 1.6, 0.015, 0.0, 0.05, "flame_core", c, verts=5)
        rig.claim(f"candle.{n}")
        bones.append((f"candle.{n}", (sx, face - 0.1, 1.575), (sx, face - 0.1, 1.625), "root"))
    animate_fire(build_armature(c, bones, rig.bone_of))


def animate_fire(arm):
    """Burn: each flame stretches, squashes and sways on its own rhythm, looping every 2 s (60 frames at 30 fps).
    Candle flames just flicker a little."""
    bpy.context.scene.render.fps = 30
    arm.animation_data_create()
    action = bpy.data.actions.new("Burn")
    action.use_fake_user = True
    arm.animation_data.action = action
    rng = random.Random(11)
    for pb in arm.pose.bones:
        if pb.name == "root":
            continue
        candle = pb.name.startswith("candle")
        pb.rotation_mode = "XYZ"
        step = rng.choice((5, 6, 7, 8, 10)) if not candle else 4
        frames = list(range(0, 60, step))
        first = None
        for f in frames + [60]:
            if f == 60 and first is not None:
                rot, scl = first
            else:
                stretch = rng.uniform(0.75, 1.3) if not candle else rng.uniform(0.8, 1.2)
                width = 1.0 + (1.0 - stretch) * 0.4  # taller flames get thinner
                sway = 9 if not candle else 4
                rot = (math.radians(rng.uniform(-sway, sway)), 0, math.radians(rng.uniform(-sway, sway)))
                scl = (width, stretch, width)
                if first is None:
                    first = (rot, scl)
            pb.rotation_euler = rot
            pb.scale = scl
            pb.keyframe_insert("rotation_euler", frame=f)
            pb.keyframe_insert("scale", frame=f)
    track = arm.animation_data.nla_tracks.new()
    track.name = "Burn"
    track.strips.new("Burn", 0, action)
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


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
    light("Moonlight", 0, -0.7, 1.9, (0.55, 0.66, 0.95), 0.55, 4.0, 0.0, c)  # cool light spilling in


def prop_lantern(c):
    """Wall lantern on an iron bracket. Wall-mounted, sticks out towards -Y."""
    box("Wall plate", 0, -0.01, 2.45, 0.1, 0.02, 0.2, "iron", c)
    box("Bracket", 0, -0.15, 2.55, 0.03, 0.28, 0.03, "iron", c)
    cyl("Hook", 0, -0.28, 2.49, 0.008, 0.1, "Z", "iron", c, verts=4)
    box("Cap", 0, -0.28, 2.4, 0.18, 0.18, 0.06, "iron", c)
    box("Glass", 0, -0.28, 2.16, 0.14, 0.14, 0.24, "lamp_glow", c)
    box("Base", 0, -0.28, 2.11, 0.18, 0.18, 0.05, "iron", c)
    light("Lantern light", 0, -0.4, 2.2, (1.0, 0.74, 0.42), 1.1, 5.5, 0.06, c)


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


def station_clock(c):
    """
    The punch clock by the door, 1.0 x 0.45 (front -Y, back against the wall): a red time clock on an iron post, with a
    clock face, the elf's card in the slot on top and a punch lever, between two wall racks of punch cards. The face is
    at the elf's eye height. The elf punches its card here to end the shift early.
    """
    back = 0.2
    box("Backboard", 0, back, 0.75, 1.0, 0.04, 0.95, "plank_b", c)
    box("Backboard cap", 0, back - 0.01, 1.7, 1.04, 0.06, 0.05, "log_dark", c)
    for side in (-1, 1):
        x = side * 0.36
        box(f"Rack.{side}", x, back - 0.04, 0.95, 0.22, 0.04, 0.45, "log_dark", c)
        for n in range(4):
            z = 1.0 + n * 0.1
            if (n + (side > 0)) % 3 != 2:  # a few pockets are empty: those elves have clocked in
                box(f"Card.{side}.{n}", x + 0.01 * side, back - 0.065, z - 0.04, 0.14, 0.006, 0.1, "snow", c)
            box(f"Pocket.{side}.{n}", x, back - 0.075, z - 0.05, 0.2, 0.012, 0.05, "beam", c)

    box("Foot", 0, 0.02, 0, 0.36, 0.3, 0.04, "iron", c)
    box("Post", 0, 0.02, 0.04, 0.12, 0.12, 0.68, "iron", c)
    box("Trim bottom", 0, 0.02, 0.7, 0.46, 0.34, 0.04, "gold", c)
    box("Clock body", 0, 0.02, 0.72, 0.42, 0.3, 0.5, "berry", c)
    box("Trim top", 0, 0.02, 1.22, 0.46, 0.34, 0.05, "gold", c)
    cyl("Bezel", 0, -0.128, 1.02, 0.15, 0.012, "Y", "gold", c, verts=20)
    cyl("Face", 0, -0.137, 1.02, 0.13, 0.012, "Y", "snow", c, verts=20)
    for n in range(12):  # hour marks
        a = n * TAU / 12
        big = n % 3 == 0
        box(f"Mark.{n}", math.sin(a) * 0.105, -0.145, 1.02 + math.cos(a) * 0.105 - 0.01, 0.012 if big else 0.007, 0.004,
            0.02, "ink", c, rot=(0, a, 0))
    for name, length, width, a, y in (("Hand hour", 0.065, 0.016, 2.1, -0.148), ("Hand minute", 0.095, 0.01, 0.0, -0.151)):
        # Each hand runs out from the centre of the face (box() turns it about its own middle).
        cx, cz = math.sin(a) * length / 2, 1.02 + math.cos(a) * length / 2
        box(name, cx, y, cz - length / 2, width, 0.005, length, "ink", c, rot=(0, a, 0))
    sphere("Hand pin", 0, -0.153, 1.02, 0.012, "gold", c, segments=8, rings=5)
    box("Name plate", 0, -0.135, 0.77, 0.26, 0.012, 0.07, "gold", c)

    # The elf's card, half in the slot on top, with a row of punched holes.
    box("Card slot", 0, 0.02, 1.27, 0.2, 0.03, 0.012, "ink", c)
    box("Punch card", 0, 0.02, 1.2, 0.15, 0.008, 0.2, "crate_light", c)
    for n in range(4):
        box(f"Hole.{n}", -0.045 + n * 0.03, 0.015, 1.36, 0.012, 0.004, 0.012, "ink", c)

    # The punch lever on the elf's right, pulled down to stamp the time.
    cyl("Lever pivot", 0.225, 0.02, 1.0, 0.035, 0.04, "X", "iron", c, verts=10)
    box("Lever arm", 0.245, -0.07, 0.94, 0.025, 0.2, 0.03, "iron", c, rot=(0.45, 0, 0))
    sphere("Lever knob", 0.245, -0.165, 0.9, 0.035, "ink", c, segments=8, rings=6)

    # A sprig of holly on top.
    for side in (-1, 1):
        box(f"Holly leaf.{side}", -0.13 + side * 0.035, -0.08, 1.27, 0.07, 0.03, 0.012, "pine", c, rot=(0, side * 0.5, 0))
    for n, (x, z) in enumerate(((-0.14, 1.29), (-0.12, 1.3), (-0.13, 1.315))):
        sphere(f"Holly berry.{n}", x, -0.085, z, 0.012, "berry", c, segments=6, rings=4)


def prop_door(c):
    """
    The workshop door, 1.1 x 2.1 m in a log frame: vertical planks with a Z-brace, iron strap hinges, a ring handle and a
    wreath. Wall-mounted (origin on the wall, faces -Y) and solid. Shut for now; a later update can open it.
    """
    w, h = 1.1, 2.1
    box("Door shadow", 0, -0.01, 0, w, 0.02, h, "floor_gap", c)
    for n in range(5):
        x = -w / 2 + w / 10 + n * w / 5
        box(f"Plank.{n}", x, -0.045, 0.01, w / 5 - 0.012, 0.05, h - 0.02, ("plank_a", "plank_c", "plank_b")[n % 3], c)
    for z in (0.28, 1.68):
        box(f"Ledge.{z}", 0, -0.08, z, w - 0.08, 0.03, 0.14, "log_dark", c)
    rise, run = 1.26, w - 0.26
    length = math.hypot(rise, run)
    box("Brace", 0, -0.08, 0.35 + rise / 2 - length / 2, 0.12, 0.03, length, "log_dark", c, rot=(0, math.atan2(run, rise), 0))
    for z in (0.33, 1.73):
        box(f"Hinge strap.{z}", -w / 2 + 0.24, -0.1, z, 0.48, 0.012, 0.045, "iron", c)
        cyl(f"Hinge knuckle.{z}", -w / 2 + 0.02, -0.09, z + 0.022, 0.018, 0.09, "Z", "iron", c, verts=8)
    box("Latch plate", w / 2 - 0.15, -0.098, 0.95, 0.09, 0.012, 0.16, "iron", c)
    torus("Ring handle", w / 2 - 0.15, -0.115, 0.98, 0.05, 0.01, "iron", c, rot=(math.pi / 2, 0, 0), segments=(12, 4))
    torus("Wreath", 0, -0.12, 1.45, 0.16, 0.05, "pine", c, rot=(math.pi / 2, 0, 0), segments=(14, 6))
    for n in range(6):
        a = n * TAU / 6 + 0.3
        sphere(f"Wreath berry.{n}", math.cos(a) * 0.16, -0.17, 1.45 + math.sin(a) * 0.16, 0.022, "berry", c, segments=6, rings=4)
    box("Wreath bow", 0, -0.16, 1.24, 0.12, 0.03, 0.08, "berry", c)
    for side in (-1, 1):
        box(f"Jamb.{side}", side * (w / 2 + 0.06), -0.06, 0, 0.12, 0.12, h + 0.08, "frame", c)
    box("Lintel", 0, -0.065, h, w + 0.28, 0.13, 0.14, "frame", c)
    box("Threshold", 0, -0.07, 0, w + 0.2, 0.14, 0.03, "log_dark", c)


# ================================================================ catalogue

MODELS = {
    # name: (builder, solid, wall_mounted)
    "elf": (elf, True, False),
    **{toy: (whole_toy(toy), True, False) for toy in TOY_PARTS},
    **{f"{toy}_{pid}": (toy_part(fn), True, False) for toy, parts in TOY_PARTS.items() for pid, fn in parts},
    **{f"{toy}_botched": (botched_toy(toy), True, False) for toy in TOY_PARTS},
    "hammer": (hammer, True, False),
    "coal_lump": (coal_lump, True, False),
    "shovel": (shovel, True, False),
    "prop_npc_bench": (prop_npc_bench, True, False),
    "station_coal": (station_coal, True, False),
    "kit_crate": (kit_crate, True, False),
    "supply_crate": (supply_crate, True, False),
    "station_supply": (station_supply, True, False),
    "ribbon_bow": (ribbon_bow, True, False),
    "ribbon_spool": (ribbon_spool, True, False),
    "guide_arrow": (guide_arrow, False, False),
    "station_bin": (station_bin, True, False),
    "station_bench": (station_bench, True, False),
    "station_ribbon": (station_ribbon, True, False),
    "station_shipping": (station_shipping, True, False),
    "station_recycle": (station_recycle, True, False),
    "station_clock": (station_clock, True, False),
    "prop_door": (prop_door, True, True),
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

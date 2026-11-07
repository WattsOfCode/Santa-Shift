"""
Adds one prop (a linked copy of <model>.blend) to an existing room.blend at a spot, leaving everything else in the file
as it is. Does nothing if the room already has that prop, so it is safe to run again.

    blender --background source-assets/blender/room.blend --python add_prop.py -- <model> <x> <y> [yaw degrees]

x and y are Blender metres (game x, and minus game z). Build the model first with make_models.py. Afterwards move it in
Blender if you like, save, and export the room (export.ps1 room).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from make_starter_room import PropPlacer  # noqa: E402


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    model, x, y = argv[0], float(argv[1]), float(argv[2])
    yaw = float(argv[3]) if len(argv) > 3 else 0.0
    if any(ob.name.startswith(f"pr_{model}.") for ob in bpy.data.objects):
        print(f"RESULT the room already has {model}; nothing added")
        return
    props = bpy.data.collections.get("Props")
    if props is None:
        print("ERROR room.blend has no 'Props' collection")
        sys.exit(1)
    ob = PropPlacer(os.path.dirname(bpy.data.filepath), props).place(model, x, y, yaw)
    bpy.ops.wm.save_mainfile()
    print(f"RESULT added {ob.name} at ({x}, {y}), turned {yaw} degrees")


if __name__ == "__main__":
    main()

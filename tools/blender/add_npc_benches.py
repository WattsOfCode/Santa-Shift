"""
Adds the elves' workbenches (two linked prop_npc_bench copies with an npc_<n> marker each) to an existing room.blend,
leaving everything else in the file as it is. Does nothing if the room already has them.

    blender --background source-assets/blender/room.blend --python add_npc_benches.py

Afterwards, move the benches and their npc markers in Blender as you like, save, and export the room.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from make_starter_room import PropPlacer, place_npc_benches  # noqa: E402


def main():
    if any(ob.name.startswith("npc_") for ob in bpy.data.objects):
        print("RESULT the room already has elves at benches; nothing added")
        return
    props = bpy.data.collections.get("Props")
    markers = bpy.data.collections.get("Layout markers")
    if props is None or markers is None:
        print("ERROR room.blend needs 'Props' and 'Layout markers' collections")
        sys.exit(1)
    place_npc_benches(PropPlacer(os.path.dirname(bpy.data.filepath), props), markers)
    bpy.ops.wm.save_mainfile()
    print("RESULT added 2 elf workbenches (pr_prop_npc_bench.*) and markers npc_0, npc_1")


if __name__ == "__main__":
    main()

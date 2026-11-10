"""
Adds the elf actions from make_models.py (ELF_ACTIONS) that elf.blend doesn't have yet, without touching anything
else in the file (so hand edits to the elf are kept). Then export it with tools/blender/export.ps1 elf.

    blender --background source-assets/blender/elf.blend --python add_elf_actions.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from make_models import ELF_ACTIONS, animate_elf  # noqa: E402


def main():
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if arm is None:
        print("ERROR no armature in this file")
        return
    have = {a.name for a in bpy.data.actions}
    missing = [name for name in ELF_ACTIONS if name not in have]
    if not missing:
        print("RESULT elf already has every action")
        return
    animate_elf(arm, only=set(missing))
    bpy.ops.wm.save_mainfile()
    print(f"RESULT added elf actions: {', '.join(missing)}")


try:
    main()
except Exception as ex:
    import traceback
    traceback.print_exc()
    print(f"ERROR {ex}")
    sys.exit(1)

"""
Exports the open .blend file to a game-ready .glb and checks it against docs/blender-export.md.

Run through Blender (export.ps1 does this for you):
    blender --background model.blend --python export_glb.py -- out.glb

Prints one line starting with RESULT, then any WARNING lines. Exit code 1 if the export failed.
"""
import os
import sys

import bpy

# Triangle budgets from docs/blender-export.md, picked by file name.
BUDGETS = [("elf", 10_000), ("toy_", 2_000), ("", 5_000)]
EXPECTED_ACTIONS = {"Idle", "Walk", "Work"}


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("ERROR usage: blender --background file.blend --python export_glb.py -- out.glb")
        return 1
    out_path = os.path.abspath(argv[0])
    name = os.path.splitext(os.path.basename(out_path))[0]
    warnings = []

    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    lowest_z = None
    for ob in bpy.context.scene.objects:
        if ob.type != "MESH":
            continue
        if any(abs(s - 1) > 1e-4 for s in ob.scale) or any(abs(r) > 1e-4 for r in ob.rotation_euler):
            warnings.append(f"'{ob.name}' has unapplied scale/rotation (Ctrl+A > All Transforms)")
        evaluated = ob.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        for v in mesh.vertices:
            z = (evaluated.matrix_world @ v.co).z
            lowest_z = z if lowest_z is None else min(lowest_z, z)
        for mat in ob.data.materials:
            tree = getattr(mat, "node_tree", None) if mat else None
            if tree:
                for node in tree.nodes:
                    if node.type == "NORMAL_MAP":
                        warnings.append(f"'{mat.name}' uses a normal map; the game ignores it (bake into base colour)")
                    if node.type == "TEX_IMAGE" and node.image and max(node.image.size) > 1024:
                        warnings.append(f"texture '{node.image.name}' is {node.image.size[0]}x{node.image.size[1]}; limit is 1024")
        evaluated.to_mesh_clear()

    budget = next(b for prefix, b in BUDGETS if name.startswith(prefix))
    if triangles > budget:
        warnings.append(f"{triangles} triangles is over the {budget} budget for '{name}'")
    if lowest_z is not None and abs(lowest_z) > 0.01:
        warnings.append(f"lowest point is at z={lowest_z:.3f} m; the origin should sit on the floor (z=0)")

    if name == "elf":
        actions = {a.name for a in bpy.data.actions}
        missing = EXPECTED_ACTIONS - actions
        if missing:
            warnings.append(f"elf is missing actions: {', '.join(sorted(missing))}")

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format="GLB",
        export_yup=True,
        export_apply=True,       # apply modifiers
        export_animations=True,
        export_materials="EXPORT",
    )

    size_kb = os.path.getsize(out_path) // 1024
    print(f"RESULT {name}: {triangles} triangles, {size_kb} KB -> {out_path}")
    for w in warnings:
        print(f"WARNING {name}: {w}")
    return 0


if __name__ == "__main__":
    try:
        code = main()
    except Exception as ex:  # report clearly instead of a Blender traceback wall
        print(f"ERROR {ex}")
        code = 1
    sys.exit(code)

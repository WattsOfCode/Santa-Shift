"""
Exports the open .blend file to a game-ready .glb and checks it against docs/blender-export.md.

Run through Blender (export.ps1 does this for you):
    blender --background model.blend --python export_glb.py -- out.glb [data_dir]

For room.blend, marker objects (see MARKER_PREFIXES) are left out of the .glb and written instead to
<data_dir>/room_layout.json: the floor, obstacles, station positions and the player start.

Prints one line starting with RESULT, then any WARNING lines. Exit code 1 if the export failed.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

# Triangle budgets from docs/blender-export.md, picked by file name.
BUDGETS = [("elf", 10_000), ("toy_", 2_000), ("room", 30_000), ("", 5_000)]
EXPECTED_ACTIONS = {"Idle", "Walk", "Work"}

# Layout markers in room.blend. They are read for room_layout.json and never drawn in the game.
#   floor_bounds   box mesh: the walkable area
#   player_start   empty:    where the elf starts
#   st_<id>        box mesh: a station's solid footprint (rotation = model turn)
#   ip_<id>        empty:    where the elf stands to use station <id>
#   col_<name>     box mesh: an extra invisible obstacle
#   pr_<model>.<n> empty showing a linked prop_<model>.blend: drawn by the game as its own model;
#                  solid props also become obstacles, sized from the prop's actual shape
MARKER_PREFIXES = ("floor_bounds", "player_start", "st_", "ip_", "col_", "pr_")


def is_marker(ob) -> bool:
    return ob.name.startswith(MARKER_PREFIXES)


def world_bbox(ob):
    """Axis-aligned bounds of an object in world space: (min Vector, max Vector)."""
    corners = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def join_by_material(objects):
    """Joins mesh objects that use the same materials. Returns the remaining objects."""
    groups, others = {}, []
    for ob in objects:
        if ob.type != "MESH":
            others.append(ob)
            continue
        key = tuple(m.name if m else "" for m in ob.data.materials)
        groups.setdefault(key, []).append(ob)
    result = list(others)
    for group in groups.values():
        if len(group) > 1:
            bpy.ops.object.select_all(action="DESELECT")
            for ob in group:
                ob.select_set(True)
            bpy.context.view_layer.objects.active = group[0]
            bpy.ops.object.join()
        result.append(group[0])
    return result


def to_game(x, y):
    """Blender (x, y) on the floor -> game (x, z). Blender +Y points away from the camera; game +Z points towards it."""
    return round(x, 4), round(-y, 4)


def box_entry(ob):
    lo, hi = world_bbox(ob)
    cx, cz = to_game((lo.x + hi.x) / 2, (lo.y + hi.y) / 2)
    return {"x": cx, "z": cz, "w": round(hi.x - lo.x, 4), "d": round(hi.y - lo.y, 4)}


def build_layout(source_name, warnings):
    objects = bpy.context.scene.objects
    layout = {"schemaVersion": 1, "source": source_name, "colliders": [], "stations": {}}

    bounds = objects.get("floor_bounds")
    if bounds:
        lo, hi = world_bbox(bounds)
        x0, z0 = to_game(lo.x, hi.y)
        x1, z1 = to_game(hi.x, lo.y)
        layout["floor"] = {"minX": x0, "minZ": z0, "maxX": x1, "maxZ": z1}
    else:
        warnings.append("no 'floor_bounds' box; the game keeps its default floor size")

    start = objects.get("player_start")
    if start:
        x, z = to_game(start.matrix_world.translation.x, start.matrix_world.translation.y)
        layout["playerStart"] = {"x": x, "z": z}

    for ob in objects:
        if ob.name.startswith("col_"):
            entry = box_entry(ob)
            entry["name"] = ob.name[4:]
            layout["colliders"].append(entry)

    for ob in objects:
        if not ob.name.startswith("st_"):
            continue
        station_id = ob.name[3:]
        entry = box_entry(ob)
        entry["yaw"] = round(math.degrees(ob.matrix_world.to_euler().z), 2)
        ip = objects.get("ip_" + station_id)
        if ip is None:
            warnings.append(f"station '{station_id}' has no 'ip_{station_id}' marker; it was skipped")
            continue
        entry["interactX"], entry["interactZ"] = to_game(ip.matrix_world.translation.x, ip.matrix_world.translation.y)
        layout["stations"][station_id] = entry

    for ob in objects:
        if ob.name.startswith("ip_") and ("st_" + ob.name[3:]) not in objects:
            warnings.append(f"'{ob.name}' has no matching 'st_{ob.name[3:]}' box")

    # Props: each pr_ empty shows a linked model file. The game draws that model; solid ones block the elf.
    layout["props"] = []
    for ob in objects:
        if not ob.name.startswith("pr_"):
            continue
        coll = ob.instance_collection if ob.instance_type == "COLLECTION" else None
        model = coll.name if coll else ob.name[3:].split(".")[0]
        x, z = to_game(ob.matrix_world.translation.x, ob.matrix_world.translation.y)
        layout["props"].append({
            "model": model, "x": x, "z": z,
            "yaw": round(math.degrees(ob.matrix_world.to_euler().z), 2),
            "scale": round(ob.matrix_world.to_scale().x, 4),
        })
        if coll is None:
            warnings.append(f"'{ob.name}' does not show a linked model; it gets no obstacle")
            continue
        if not coll.get("solid", True):
            continue
        footprint = instance_bbox(ob, coll)
        if footprint is None:
            continue
        lo, hi = footprint
        cx, cz = to_game((lo.x + hi.x) / 2, (lo.y + hi.y) / 2)
        layout["colliders"].append({"name": ob.name, "x": cx, "z": cz,
                                    "w": round(hi.x - lo.x, 4), "d": round(hi.y - lo.y, 4)})
    layout["lights"] = collect_lights(objects)
    return layout


def light_entry(ob, matrix):
    """One game light: position in game metres (y = height), colour, intensity, radius, flicker."""
    p = matrix.translation
    x, z = to_game(p.x, p.y)
    energy_intensity = ob.data.energy / 50 if ob.data else 1.0
    return {
        "name": ob.name, "x": x, "y": round(p.z, 4), "z": z,
        "color": [round(c, 4) for c in ob.data.color],
        "intensity": round(float(ob.get("intensity", energy_intensity)), 4),
        "radius": round(float(ob.get("radius", 5.0)), 4),
        "flicker": round(float(ob.get("flicker", 0.0)), 4),
    }


def collect_lights(objects):
    """Point lights placed directly in room.blend plus the lights that come with each linked prop."""
    lights = []
    for ob in objects:
        if ob.type == "LIGHT" and ob.data.type == "POINT":
            lights.append(light_entry(ob, ob.matrix_world))
        if ob.name.startswith("pr_") and ob.instance_type == "COLLECTION" and ob.instance_collection:
            coll = ob.instance_collection
            base = ob.matrix_world @ Matrix.Translation(-coll.instance_offset)
            for inner in coll.all_objects:
                if inner.type == "LIGHT" and inner.data.type == "POINT":
                    entry = light_entry(inner, base @ inner.matrix_world)
                    entry["name"] = f"{ob.name}/{inner.name}"
                    lights.append(entry)
    return lights


def instance_bbox(empty, coll):
    """World-space bounds of everything an instance empty shows."""
    base = empty.matrix_world @ Matrix.Translation(-coll.instance_offset)
    corners = [base @ ob.matrix_world @ Vector(c)
               for ob in coll.all_objects if ob.type == "MESH" for c in ob.bound_box]
    if not corners:
        return None
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), 0))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), 0))
    return lo, hi


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("ERROR usage: blender --background file.blend --python export_glb.py -- out.glb [data_dir]")
        return 1
    out_path = os.path.abspath(argv[0])
    data_dir = os.path.abspath(argv[1]) if len(argv) > 1 else None
    name = os.path.splitext(os.path.basename(out_path))[0]
    is_room = name == "room"
    warnings = []

    # Measure rigged models in their rest pose, not in whatever pose the animations leave them in.
    rigs = [ob.data for ob in bpy.context.scene.objects if ob.type == "ARMATURE"]
    for rig in rigs:
        rig.pose_position = "REST"
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    lowest_z = None
    for ob in bpy.context.scene.objects:
        if ob.type != "MESH" or (is_room and is_marker(ob)):
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
    # Wall props hang on the wall; toy parts sit on the part below them. Both float above the floor on purpose.
    wall_mounted = bool(bpy.context.scene.get("wall_mounted", False)) or bool(bpy.context.scene.get("toy_part", False))
    if not is_room and lowest_z is not None and (lowest_z < -0.01 or (lowest_z > 0.01 and not wall_mounted)):
        warnings.append(f"lowest point is at z={lowest_z:.3f} m; the origin should sit on the floor (z=0)")

    if name == "elf":
        actions = {a.name for a in bpy.data.actions}
        missing = EXPECTED_ACTIONS - actions
        if missing:
            warnings.append(f"elf is missing actions: {', '.join(sorted(missing))}")

    for rig in rigs:
        rig.pose_position = "POSE"

    # Export only visible scenery (never the room's layout markers), and merge pieces that share materials
    # so the game draws a few meshes per model instead of dozens. (The .blend file itself is not changed.)
    scenery = [ob for ob in bpy.context.scene.objects if not (is_room and is_marker(ob))]
    meshes_before = sum(1 for ob in scenery if ob.type == "MESH")
    scenery = join_by_material(scenery)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in scenery:
        ob.select_set(True)
    print(f"RESULT {name}: merged {meshes_before} pieces into {sum(1 for ob in scenery if ob.type == 'MESH')} meshes")

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format="GLB",
        export_yup=True,
        export_apply=True,       # apply modifiers
        export_animations=True,
        export_materials="EXPORT",
        use_selection=True,
    )

    size_kb = os.path.getsize(out_path) // 1024
    print(f"RESULT {name}: {triangles} triangles, {size_kb} KB -> {out_path}")

    if is_room:
        layout = build_layout(os.path.basename(bpy.data.filepath), warnings)
        if data_dir:
            layout_path = os.path.join(data_dir, "room_layout.json")
            with open(layout_path, "w", encoding="utf-8") as f:
                json.dump(layout, f, indent=2)
            print(f"RESULT room layout: {len(layout['stations'])} stations, {len(layout['colliders'])} obstacles, "
                  f"{len(layout['props'])} props, {len(layout['lights'])} lights -> {layout_path}")
        else:
            warnings.append("no data folder given; room_layout.json was not written")

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

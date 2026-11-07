"""
Adds layout markers to room.blend for stations that are in stations.json but not yet in the room, without touching
anything else in the file (so hand edits to the room are kept).

    blender --background source-assets/blender/room.blend --python sync_station_markers.py -- <stations.json> <tuning.json>

For each new station <id> it adds a box "st_<id>" (the solid footprint) and an arrow "ip_<id>" (where the elf stands)
to the "Layout markers" collection, placed from stations.json. Move them in Blender afterwards if you like, save, and
export the room (export.ps1 room).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from helpers import box, read_json  # noqa: E402

import bpy  # noqa: E402


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    stations = read_json(argv[0])["stations"]
    tuning = read_json(argv[1])
    ppm = tuning.get("pixelsPerMetre", 40)
    origin = tuning.get("worldOrigin", {"x": 480, "y": 300})

    def blender_xy(px, py):
        """Game pixels -> Blender metres. Game +Z (pixel y down) is Blender -Y."""
        return (px - origin["x"]) / ppm, -(py - origin["y"]) / ppm

    markers = bpy.data.collections.get("Layout markers")
    if markers is None:
        print("ERROR room.blend has no 'Layout markers' collection")
        sys.exit(1)

    added = []
    for s in stations:
        sid = s["id"]
        if ("st_" + sid) in bpy.data.objects:
            continue
        b, ip = s["body"], s["interact"]
        bx, by = blender_xy(b["x"], b["y"])
        # The body is the footprint as placed; a station turned a quarter turn ("yaw": 90 or -90) has its model's width
        # along the other axis, so the box is made in the model's own axes and turned.
        yaw = s.get("yaw", 0)
        quarter = round(abs(yaw)) % 180 == 90
        w, d = (b["h"], b["w"]) if quarter else (b["w"], b["h"])
        st = box("st_" + sid, bx, by, 0, w / ppm, d / ppm, 0.5, None, markers)
        st.rotation_euler.z = math.radians(yaw)  # kept as the object's turn: the export reads it as the station's yaw
        bpy.ops.object.empty_add(type="SINGLE_ARROW", location=(*blender_xy(ip["x"], ip["y"]), 0))
        e = bpy.context.active_object
        e.name = "ip_" + sid
        e.empty_display_size = 0.4
        for c in list(e.users_collection):
            c.objects.unlink(e)
        markers.objects.link(e)
        for ob in (st, e):
            ob.display_type = "WIRE"
            ob.hide_render = True
        added.append(sid)

    if added:
        bpy.ops.wm.save_mainfile()
    print(f"RESULT station markers added: {', '.join(added) if added else 'none (room is up to date)'}")


if __name__ == "__main__":
    main()

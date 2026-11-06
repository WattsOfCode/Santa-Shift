# Blender export rules

Approved rules for getting models from Blender into Santa Shift. Following them means a model works the moment
its file is dropped into `src/SantaShift.Game/Content/models/`, with no code changes.

## Export

1. **File > Export > glTF 2.0 (.glb/.gltf)**, format **glTF Binary (.glb)**.
2. Include: selected objects (or the whole scene for a single-model file). Textures are embedded automatically in `.glb`.
3. Leave **+Y Up** checked (the exporter default).

## Before exporting

4. **Apply all transforms** (select all, Ctrl+A > All Transforms). Scale must be 1, rotation 0.
5. **1 Blender unit = 1 metre.** The elf is about 1.35 m tall; a bench is about 0.85 m tall.
6. **Origin at the floor contact point**, centred under the object. The game places models by their origin.
7. Model fixtures facing **+Y in Blender** (towards the camera in the game).

## Materials and textures

8. **Base colour texture only.** The game uses a simple shader: normal, roughness and metallic maps are ignored.
   Bake ambient occlusion and soft lighting into the base colour instead (Cycles > Bake > Diffuse, or AO multiplied in).
9. Textures **512 x 512**, **1024 x 1024** at most. One material per object where possible.

## Budget (keeps the game light on any device)

10. Props and stations: **under 5,000 triangles**. Elf: **under 10,000**. Toys: **under 2,000**.

## Elf animation

11. One armature. Actions named exactly **`Idle`**, **`Walk`**, **`Work`**. Push each to an NLA track so it is exported.
    (Animation playback is not wired up yet; the elf model will display in its rest pose until it is.)

## File names

| File | Used for |
|---|---|
| `elf.glb` | Player character |
| `kit_crate.glb` | Every toy kit (tinted per toy by the game) |
| `ribbon_bow.glb` | Ribbon placed on finished toys |
| `toy_train.glb`, `toy_bear.glb`, `toy_robot.glb` | Finished toys (names set in `toys.json`) |
| `station_bin.glb`, `station_bench.glb`, `station_ribbon.glb`, `station_shipping.glb`, `station_recycle.glb` | Fixtures (names set in `stations.json`) |

Keep `.blend` sources in `source-assets/blender/`. Only exported `.glb` files go in `Content/models/`.

## Export tool (recommended)

Name each `.blend` after its model (`toy_train.blend`, `elf.blend`...) and save it in `source-assets/blender/`. Then run:

```
.\tools\blender\export.ps1              # every .blend
.\tools\blender\export.ps1 toy_train    # just one
```

This runs Blender in the background, exports with the settings above, and checks the rules: unapplied transforms,
triangle budget, origin not on the floor, normal maps, oversized textures, and missing elf actions. Problems print as
yellow `WARNING` lines; the model is still exported so you can look at it in the game.

Blender is expected at `D:\Blender\blender.cmd` (portable Blender 5.2.3 LTS; its settings live in
`D:\Blender\blender-5.2.3-windows-x64\portable`). To use another copy, set `SANTASHIFT_BLENDER` to its `blender.exe`.

## How models look in the game

Models get **one soft directional light plus ambient light**, with no shadows. Baked ambient occlusion in the base
colour still helps a lot. Kit crates are tinted by the toy's colour, so model them in light, neutral tones.
(Grey-box placeholder shapes are drawn unlit; that's expected.)

## Designing the room (room.blend)

`source-assets/blender/room.blend` is the whole workshop. The export tool turns it into two files:
- `Content/models/room.glb`: the scenery you see (floor, walls, windows, decorations).
- `Content/data/room_layout.json`: where things are, read by the game's rules.

The layout comes from **marker objects** in the "Layout markers" collection. They show as wireframes in Blender and are
never drawn in the game:

| Marker | Type | Meaning |
|---|---|---|
| `floor_bounds` | box | The walkable area. The elf can't leave it, and the camera stays inside it. |
| `player_start` | empty | Where the elf starts each round. |
| `st_<id>` | box | A station's solid footprint, e.g. `st_bench`. Rotate it to turn the station's model. |
| `ip_<id>` | empty | Where the elf stands to use that station, e.g. `ip_bench`. Keep it just outside the box. |
| `col_<name>` | box | An obstacle the elf can't walk through. Put one around any furniture you add. |

`<id>` must match an id in `stations.json` (kits_train, kits_bear, kits_robot, bench, ribbon, recycle, shipping).

**To change the layout:** move the `st_` boxes and their `ip_` empties together, add decorations and `col_` boxes,
save, then run `.\tools\blender\export.ps1 room`. No code changes are needed. To start again from the default layout:

```
D:\Blender\blender.cmd --background --factory-startup --python tools\blender\make_starter_room.py -- src\SantaShift.Game\Content\data\stations.json src\SantaShift.Game\Content\data\tuning.json source-assets\blender\room.blend
```

To go back to the built-in grey-box room, delete `room.glb` and `room_layout.json`.

Keep walls at least 3 m tall: the camera follows behind the elf inside the room. The camera also stays below
`maxHeight` in `render.json` (3.0 m), so ceiling beams and hanging decorations above that never block the view.
Keep hanging things out of the middle of the room, or above 3 m; wall-mounted decorations are safest.

The current room is a rustic log cabin (made by `make_starter_room.py`): log walls, plank floor, beams, stone
fireplace, tree, barrels, sled, windows and wall lanterns. When exporting, pieces that share a material are merged,
so hundreds of logs and planks still draw as about 30 meshes.

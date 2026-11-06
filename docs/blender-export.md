# Blender models and export rules

Every visible thing in the game is its own Blender model: the elf, the toys, the kit crate, the ribbon bow, the five
station types, each piece of furniture, and the cabin shell. Sources live in `source-assets/blender/*.blend`; the
export tool turns them into `src/SantaShift.Game/Content/models/*.glb`. If a `.glb` is missing, the game falls back
to a grey-box shape (or skips a decoration), so nothing ever breaks.

## Export

1. **File > Export > glTF 2.0 (.glb/.gltf)**, format **glTF Binary (.glb)**. (The export tool does this for you.)
2. Include the whole scene for a single-model file. Textures are embedded automatically in `.glb`.
3. Leave **+Y Up** checked (the exporter default).

## Before exporting

4. **Apply all transforms** (select all, Ctrl+A > All Transforms). Scale must be 1, rotation 0.
5. **1 Blender unit = 1 metre.** The elf is about 1.5 m tall with its hat; a bench top is 0.85 m.
6. **Origin at the floor contact point**, centred under the object. The game places models by their origin.
   Wall-mounted props (windows, lanterns, shelves...) have their origin **on the wall surface** instead.
7. **Front faces -Y** (Blender's Front view, numpad 1). That is the side players see first: the elf's face, a station's
   working side. Wall-mounted props stick out towards -Y from the wall.

## Materials and textures

8. **Base colour only.** The game uses a simple shader: normal, roughness and metallic maps are ignored.
   Bake ambient occlusion and soft lighting into the base colour (Cycles > Bake > Diffuse, or AO multiplied in).
9. Textures **512 x 512**, **1024 x 1024** at most.

## Budget (keeps the game light on any device)

10. Elf: **under 10,000 triangles**. Toys: **under 2,000**. Props and stations: **under 5,000**. Room shell: **under 30,000**.
    On export, pieces that share a material are merged, so a model draws as a handful of meshes however many parts it has.

## Elf animation

11. One armature. Actions named exactly **`Idle`**, **`Walk`**, **`Work`**. Push each to an NLA track so it is exported.
    Not done yet: the current elf is a static model (the export warns about this). The game bobs it while walking.

## The models

| File | What it is | Notes |
|---|---|---|
| `elf` | Player character | Static for now |
| `toy_train`, `toy_bear`, `toy_robot` | Finished toys | Names set in `toys.json` |
| `kit_crate` | Every toy kit | Light neutral wood: the game tints it with the toy's colour |
| `ribbon_bow` | Bow on a ribboned toy | Origin is where it sits on the toy |
| `station_bin` | Kit bins (all three) | 2.25 x 1.0 m, rim at 0.8 m (a kit crate sits on top) |
| `station_bench` | Assembly bench | 3.0 x 1.0 m, top at 0.85 m |
| `station_ribbon` | Ribbon station | 2.5 x 1.0 m |
| `station_shipping` | Shipping chute | 1.75 x 1.5 m, chute opening on the -X side |
| `station_recycle` | Scrap barrel | 1.25 x 1.25 m |
| `prop_tree`, `prop_fireplace`, `prop_firewood`, `prop_barrels`, `prop_sled` | Furniture | Solid: the elf walks around them |
| `prop_rug` | Braided rug | Not solid |
| `prop_window`, `prop_lantern`, `prop_wreath`, `prop_shelf`, `prop_clock` | Wall decorations | Not solid, wall-mounted |
| `room` | Cabin shell and the layout | See below |

Station footprints match `stations.json`; keep models inside them.

## Export tool

```
.\tools\blender\export.ps1                          # every .blend
.\tools\blender\export.ps1 toy_train prop_tree      # just these
```

This runs Blender in the background, exports with the settings above, and checks the rules: unapplied transforms,
triangle budget, origin not on the floor, normal maps, oversized textures, and missing elf actions. Problems print as
yellow `WARNING` lines; the model is still exported so you can look at it in the game.

Blender is expected at `D:\Blender\blender.cmd` (portable Blender 5.2.3 LTS; its settings live in
`D:\Blender\blender-5.2.3-windows-x64\portable`). To use another copy, set `SANTASHIFT_BLENDER` to its `blender.exe`.

## Making a new model or regenerating one

The starting models were generated from simple shapes by `tools/blender/make_models.py`. Each file holds one collection
named after the model. To add a new model, make a `.blend` the same way (one collection with the model's name), save it
in `source-assets/blender/`, and export it.

To regenerate models from the script (this **overwrites** those `.blend` files, so name only the ones you haven't edited):

```
D:\Blender\blender.cmd --background --factory-startup --python tools\blender\make_models.py -- source-assets\blender prop_tree elf
```

A collection custom property `solid = False` makes a prop walk-through (rugs, wall decorations). A scene custom property
`wall_mounted = True` tells the export that the model floats above the floor on purpose.

## Lighting

The room is lit by its own light sources, in the style of ELFX-type interior lighting: the fireplace, lanterns, windows
and tree each carry a **point light**, and the rest of the room falls into soft, darker ambient light.

- **Lights live in Blender.** Each prop file can contain point lights (the fireplace, lantern, window and tree already
  do). Moving or duplicating a prop in room.blend moves or duplicates its light. You can also add point lights directly
  to room.blend. The room export writes all of them to `room_layout.json`.
- **Light settings** are custom properties on the light object: `intensity` (about 1 = lantern, 3 = fireplace),
  `radius` (metres it reaches, fading smoothly to zero), `flicker` (0 = steady, 0.35 = fire). Colour comes from the
  light's colour. Without the custom properties, Blender's Power / 50 is used as the intensity.
- **Glowing materials:** a material with **Emission Color** set glows in the game (flames, lantern glass, window panes).
  `make_models.py` sets this for the `flame`, `flame_core`, `lamp_glow` and `glass` materials.
- **Limits:** up to 16 lights are used at once (high quality); the game picks the most important ones near the camera.
  There are no shadow maps, to keep it light on weak devices. Baked ambient occlusion in textures still helps a lot.
- **Tuning** is in `render.json` under `lighting`: quality (high 16 lights / medium 8 / low 4 with no haze or
  vignette), exposure, glow strength, ambient colours, haze, vignette, saturation, and the soft fill light that follows
  the elf so it stays readable. Flicker stops when Reduced Motion is on.

Grey-box placeholder shapes are drawn unlit; that's expected. Carried items are drawn at 75% size above the elf.

## Designing the room (room.blend)

`source-assets/blender/room.blend` is the whole workshop. Its export writes:
- `Content/models/room.glb`: the cabin shell (log walls, floor, beams, ceiling).
- `Content/data/room_layout.json`: where everything is: floor, station positions, props and obstacles.

The room has three collections:

**Cabin**: the shell. Edit freely; it all becomes `room.glb`.

**Props**: furniture and decorations. Each is an empty named `pr_<model>.<n>` that shows a **linked copy** of
`<model>.blend`. Move, rotate, scale or duplicate them (Shift+D). Edit `prop_tree.blend` itself to change every tree.
The game draws each prop from its own `.glb`; solid props block the elf over their actual footprint, so moving one in
Blender moves its obstacle too. Wall props face into the room with these turns: back wall 0, front wall 180, left
wall 90, right wall -90 degrees.

To add a new kind of prop: make `prop_<name>.blend` (one collection named `prop_<name>`), export it, then in room.blend
use **File > Link**, pick `prop_<name>.blend > Collection > prop_<name>`, and rename the new empty `pr_prop_<name>.00`.

**Layout markers**: wireframes, never drawn:

| Marker | Type | Meaning |
|---|---|---|
| `floor_bounds` | box | The walkable area. The elf can't leave it, and the camera stays inside it. |
| `player_start` | empty | Where the elf starts each round. |
| `st_<id>` | box | A station's solid footprint, e.g. `st_bench`. Rotate it to turn the station's model. |
| `ip_<id>` | empty | Where the elf stands to use that station, e.g. `ip_bench`. Keep it just outside the box. |
| `col_<name>` | box | An extra invisible obstacle, if a prop's footprint isn't enough. |

`<id>` must match an id in `stations.json` (kits_train, kits_bear, kits_robot, bench, ribbon, recycle, shipping).

After any change, save and run `.\tools\blender\export.ps1 room`. No code changes are needed. To start again from the
default cabin (run `make_models.py` first if the prop files are missing):

```
D:\Blender\blender.cmd --background --factory-startup --python tools\blender\make_starter_room.py -- src\SantaShift.Game\Content\data\stations.json src\SantaShift.Game\Content\data\tuning.json source-assets\blender\room.blend
```

To go back to the built-in grey-box room, delete `room.glb` and `room_layout.json`.

Keep walls at least 3 m tall: the camera follows behind the elf inside the room. The camera also stays below
`maxHeight` in `render.json` (3.0 m). Keep hanging things out of the middle of the room or above 3 m; wall-mounted
decorations are safest.

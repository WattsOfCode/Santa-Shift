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

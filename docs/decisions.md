# Decision log

Format (GDD section 28): date, decision, reason, affected systems, validation, superseded rule.

## 2026-11-06: C# / .NET 10 + Raylib-cs instead of JavaScript + Phaser
- **Reason:** Developer preference for C#, everything built in-house, no engine, 3D with Blender models.
- **Affects:** GDD sections 1, 18, 20, 27 (architecture, browser requirements, packaging).
- **Validation:** Window, grey-box workshop and full collect, assemble, deliver loop running; 71 unit tests pass.
- **Supersedes:** "JavaScript with the free Phaser framework; desktop browser."

## 2026-11-06: 3D models with a fixed angled camera; gameplay stays on a flat plane
- **Reason:** Use Blender for art while keeping the GDD's top-down readability and 2D rules unchanged.
- **Affects:** GDD sections 2, 5, 13, 16. "3D models" is removed from the out-of-scope list.
- **Details:** Rules keep using GDD logical pixels; 40 px = 1 m in the 3D world. Camera pitch 70 degrees, fixed.
- **Supersedes:** "Fixed top-down 2D camera" and "code-drawn shapes". Grey-box shapes remain as the fallback for any missing model.

## 2026-11-06: Windows desktop build instead of browser
- **Reason:** Follows from the C# / Raylib choice. Game Off allows downloadable entries.
- **Affects:** GDD sections 2, 20, 27. `dotnet publish` will be set up once approved.
- **Risk noted:** downloadable jam games tend to get fewer plays than browser games.

## 2026-11-06: JSON files for settings and best scores; no SQLite
- **Reason:** The GDD needs no mid-shift saves, only settings and personal bests.
- **Affects:** GDD section 19 (`localStorage` becomes `%AppData%/SantaShift/*.json`, with an in-memory fallback).

## 2026-11-06: Children's letters as data (letters.json)
- **Reason:** Letters can be added or removed without code changes.
- **Affects:** GDD sections 9 and 15. Each new order takes the next letter for its toy; cards show the child's name,
  and the full line appears in the Santa strip. Mechanics are unchanged.

## 2026-11-06: Recycling and shipping moved 25 px inward
- **Reason:** With the 3D camera's perspective, the near corners of the floor were cut off at the screen edges.
- **Affects:** GDD section 13 layout. Recycling interaction point (140, 420) becomes (165, 420); shipping (800, 420) becomes (780, 420).

## 2026-11-06: Shipping to an order past its deadline is refused in the same step
- **Reason:** GDD section 11: a delivery on the exact deadline is accepted, one after it is not. Without this check, a
  shipment one tick late would be accepted, because expiry runs after interactions in each step.
- **Validation:** `TimingTests.Delivery_one_tick_late_is_not_accepted`.

## Noted for tuning (not changed)
- In standard mode, if nothing is delivered, every order expires before the shift ends (the last one, created at 252 s, has a 45 s deadline).
  "Unfinished" only happens when orders are still live at 300 s.
- Relaxed mode gives the 252 s rush order 67.5 s, more than the 48 s left in the shift; it ends as unfinished.

## 2026-11-06: Third-person camera with mouse look
- **Reason:** User wants a closer view from behind the elf.
- **Details:** The camera follows 3.8 m behind the elf; the mouse turns and tilts it; WASD moves relative to the camera.
  The mouse is captured during play and released for pause and menus. Settings are in `render.json` under `chaseCamera`.
  The game rules are unchanged: the camera only turns key input into a floor direction.
- **Supersedes:** "Fixed top-down 2D camera" (and the earlier fixed angled camera) and "keyboard only" (GDD sections 2, 7, 21).
  A mouse is now required. Note for the jam page: list mouse + keyboard in the controls.

## 2026-11-06: Rounds with game over
- **Reason:** User decision: a run continues as long as each round's quota is met.
- **Details:** Meeting the quota starts the next round with quota +1 and deadlines 5% shorter (never below 70%).
  Missing the quota is game over. Score carries across rounds; the best score and furthest round are saved per mode.
  All values are in `tuning.json` under `rounds`.
- **Supersedes:** GDD section 12, "There is no early firing or game over", and the single five-minute session (section 2).

## 2026-11-06: Room designed in Blender
- **Reason:** User wants the room built in Blender, with more for the player to move around and interact with.
- **Details:** `room.blend` exports to `room.glb` (scenery) and `room_layout.json` (floor, obstacles, station positions,
  player start), using marker objects described in `docs/blender-export.md`. The layout file overrides `stations.json`
  positions and the tuning floor. A starter room matching the original layout was generated, with walls, windows, a rug
  and a Christmas tree obstacle.

## 2026-11-06: Simple lighting for Blender models
- **Reason:** Unlit models looked flat once the camera came close.
- **Details:** One directional light plus ambient, with no shadows. It applies to loaded models only; grey-box shapes stay unlit.

## 2026-11-06: Rustic log cabin room
- **Reason:** User wants the workshop to look like a rustic cabin.
- **Details:** `make_starter_room.py` now builds a 3.6 m log cabin: notched log walls with filler between the logs, a staggered
  plank floor, beams and a plank ceiling, a stone fireplace with fire, mantel and stockings, firewood, framed windows with
  snowy sills, a wreath, shelves, a braided rug, a decorated tree with presents, barrels, a sled, a cuckoo clock and wall lanterns.
  The fireplace, firewood, barrels, sled and tree are obstacles. Station positions are unchanged. Lighting is now warmer.
- **Performance:** the export merges pieces that share a material (578 pieces into 31 meshes, about 9,400 triangles).
- **Camera:** capped at 3.0 m (`maxHeight`) so it stays under the beams; lanterns are wall-mounted to keep them out of its path.

## 2026-11-06: Every object is its own Blender model
- **Reason:** User wants each object (character, tree, everything in the room) to be a Blender model.
- **Details:** 22 model files generated by `tools/blender/make_models.py`: elf, 3 toys, kit crate, ribbon bow,
  5 station types, and 11 props (tree, fireplace, firewood, barrels, sled, rug, window, lantern, wreath, shelf, clock).
  `room.blend` now holds only the cabin shell and places props as linked copies (`pr_<model>.<n>` empties).
  The room export writes each prop's position, turn and scale to `room_layout.json`; solid props get obstacles sized from
  their real footprint. The game draws props from their own `.glb` files.
- **Changed rule:** models face -Y in Blender (Blender's Front view), not +Y as first written; wall props have their
  origin on the wall. Every export now merges pieces by material.
- **Not done:** the elf is a static model; Idle/Walk/Work animations still need rigging. Carried items are drawn at 75% size.

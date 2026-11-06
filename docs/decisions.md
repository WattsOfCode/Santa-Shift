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

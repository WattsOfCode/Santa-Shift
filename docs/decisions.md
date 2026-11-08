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

## 2026-11-06: Source-based lighting overhaul (ELFX-style)
- **Reason:** User asked for an ELFX-style lighting overhaul. Built as our own system in that style; nothing is copied
  from the Skyrim mods. "TXS" was unclear, so it was not interpreted as a specific feature.
- **Details:** Point lights come from Blender: the fireplace (warm, flickering), six lanterns, six windows (cool
  moonlight spill) and the tree, 14 in all, exported to `room_layout.json` and moving with their props. One forward
  shader pass does per-pixel point lights with smooth falloff, warm/cool ambient, glTF emission (flames, lantern glass,
  windows), filmic tone mapping and warm haze; a post pass adds a vignette and slight saturation. A soft fill light
  follows the elf for readability. Flicker stops under Reduced Motion.
- **Performance:** no shadow maps or extra geometry passes. Quality presets: high 16 lights, medium 8, low 4 with no
  haze or post effects. Lights are ranked by importance (reach and brightness against distance), so low quality keeps
  the fireplace. Not yet measured on low-end hardware.
- **Settings:** `render.json` > `lighting`.

## 2026-11-06: Assembly minigame, toy parts, botched toys, animated elf with a hammer
- **Reason:** User asked for a more engaging build step: 3 parts per toy, a quick minigame, a separate model for a toy
  built in the wrong order, hammer-swing animations, a closer over-the-shoulder camera while building, and difficulty
  that randomises the stages over time.
- **User's choices:** key-sequence minigame (parts on number keys, pressed in the order shown before a timer runs out);
  a botched toy can only be scrapped (no points lost, only time).
- **Replaces the GDD's timed hold** (`assembleSeconds` is gone). Each toy in `toys.json` lists 3 parts in its natural
  order, each with its own model, plus a `botchedModel`. A wrong part or running out of time botches the toy. Botched
  toys can't get a ribbon or be shipped; the scrap barrel takes them without the usual confirmation. Moving cancels the
  build and gives back a plain kit. Pausing freezes the minigame clock instead of cancelling it.
- **Difficulty** (`tuning.json` > `assembly`): 6 s in round 1, 0.5 s less each round, at least 2.5 s. From round 2 the
  order is shuffled every build; from round 4 the keys are shuffled too (never back to plain 1-2-3). Relaxed mode plays
  one round behind with 1.5x the time. Shuffles use a seeded random number generator: tests are repeatable, the game
  seeds each shift fresh.
- **Art:** 9 part models and 3 botched models (generated from the same pieces as the toys), a `hammer` model, and the elf
  is now rigged (15 bones, each part follows one bone) with Idle, Walk and Work actions. The game plays them with Raylib
  6's skeletal animation, crossfading between them, and attaches the hammer to the right hand bone. The bench is now
  0.55 m tall (was 0.85) so the 1.15 m elf can swing over it.
- **Camera:** while building, the camera eases in over the elf's right shoulder (`render.json` > `shoulderCamera`) and
  mouse look pauses; afterwards the chase camera carries on from behind the elf.
- **Not done:** no sound files yet for `hammer_hit` / `assembly_botched` (the game plays nothing if they're missing).

## 2026-11-06: Naughty and nice list, coal, smoother movement, camera auto-follow, face fix
- **Naughty and nice list (user request):** each run starts with Santa's list on an old parchment scroll (drawn from
  shapes: unrolling rods with candy-cane knobs, holly, ticks, a COAL! stamp, a wax seal). Every child with a letter is
  listed; `tuning.json` > `naughtyPerRun` (3) of them are picked at random to be naughty for the whole run. Their
  orders show as "Coal" with a NAUGHTY badge and Santa comments. The elf grabs a lump of coal from a new coal scuttle
  on the fireplace hearth (station type `coal`) and ships it; it scores like a toy, never needs a ribbon, and goes to
  the most urgent naughty order. A naughty child won't take a toy, and coal can't be built, ribboned or sent to a nice
  child. Without a coal station nobody is naughty, so older layouts keep working.
- **Smoother movement:** the elf's heading turns towards the pressed direction (720 degrees/s) and it reaches full speed
  over 0.12 s; on a sharp turn it slows while coming round, so it U-turns instead of snapping. Releasing the keys
  still stops it at once, so it never drifts off a station. View/controller only: the rules are unchanged.
- **Camera auto-follow:** after the elf has faced one way for 2 s with the mouse untouched, the camera eases round
  behind it. While it swings, held movement keys keep their direction (they are read relative to the camera as it was
  when pressed), so the elf doesn't curve. Settings in `render.json` (`autoFollowSeconds`, `autoFollowSpeed`,
  `movement`).
- **Face:** the pupils, brows and smirk now sit in the skin (sunk into the eyeballs and the face surface) instead of
  floating in front of it.

## 2026-11-06: Shovel, hammer in view, animated fire, elves at their own benches
- **Shovel (user request):** taking coal is now a short action (0.8 s, `tuning.json` > `shovelSeconds`, cancelled by
  moving, like the ribbon) with a new `shovel` model in the elf's right hand and a `Shovel` scoop animation. The shovel
  leans on the scuttle when not in use (it was removed from the scuttle model).
- **Hammer in view (user request: "right hand ... so players can see it in the camera frame"):** the hammer was always
  in the right hand, but the swing crossed in front of the face, where the head and hat hid it from the shoulder camera.
  The upper arm now swings out 45 degrees and toys are built 0.25 m to the elf's right, so the swing and the strike are
  in frame beside the head.
- **Animated fire:** the fireplace's flames are rigged (one bone each) with a looping `Burn` action. The game loops the
  first animation of any prop model that has one.
- **Elves at their benches (user request):** two `prop_npc_bench` workbenches near the front wall, each with a
  workshop elf (the player's model, its own animator, a swing every 0.9 to 1.4 s so they're never in step) and trains
  on top. Elves are `npc_<n>` markers in room.blend, exported to `room_layout.json` > `npcs` with a small obstacle each.
  Scenery only: they don't take orders. Added to the existing room with `add_npc_benches.py`, keeping hand edits.

## 2026-11-06: Frame rate, held items, supply sled, festive title
- **Frame rate (user report: "FPS start dropping"):** measured with a new debug `--profile` flag (prints per-section
  frame times every 2 s, uncapped). Nearly all of the frame (5.2 of 6.2 ms) was Raylib skinning the elf mesh on the
  CPU, once per elf, and re-uploading it: three elves since the workshop elves were added. Skinning now runs on the
  GPU: a skinned variant of the lit shader, the game working out the bone matrices itself (`SkeletonPoser`, checked
  against Raylib's to within 0.0007) and uploading the bone index and weight buffers Raylib's glTF loader leaves on the
  CPU. Elf animation went from 5.2 ms to 0.05 ms per frame (about 160 to 380+ fps uncapped). Models whose bone data
  can't be uploaded fall back to Raylib's CPU path. A long idle run showed no slowdown over time.
- **Held items (user request):** the elf holds what it carries instead of floating it overhead. A new `Carry` action
  raises the right arm, laid over Walk or Idle (only the right arm, kept relative to the chest) so the legs keep
  walking; the item stands on the wrist at 60% size. It is held up beside the head, not in front, because the elf's
  big head would hide it from the camera behind. Without a Carry action in elf.glb it floats overhead as before.
- **Supply sled (user request: "limited amount ... for the exact amount of toys requested"):** with a `supply`
  station in the room, each kit bin holds one kit per (nice) letter for its toy so far, so a botched or scrapped kit
  leaves a bin short. The elf fetches a `supply_crate` from the sled against the right wall and carries it to a bin to
  restock it (`resupplyKits`, 2 per crate). Bin labels show what's left and turn red, visible from anywhere, when a
  waiting order needs a kit the bin doesn't have. Crates can't be built, ribboned or shipped; the barrel takes them
  without asking. Without a supply station, bins never run out (older layouts and tests unchanged).
- **Festive title (user request):** a holly garland along the top with glass baubles swaying on threads, falling snow,
  and the title between two candy canes wrapped in holly vines. Everything is drawn from shapes (`Festive`), shared
  with Santa's list; still under Reduced Motion.

## 2026-11-06: Music
- Two tracks by **blueeyedmonkey**, supplied by the project owner: "Toy Factory Jingle" plays during a shift,
  "Cozy Christmas Game Ambience" on the title, Santa's list and results. Each screen names its track
  (`IScreen.Music`); the audio service streams them (never loaded whole), loops them and crossfades over 1.2 s at the
  existing `MusicVolume` setting. A missing file or no audio device means silence, never an error.
- Credited in README and ASSET_CREDITS.md. The license terms still need confirming before release.

## 2026-11-06: Menus, settings, rebinding and controller
- **Title (user request):** the controls list is gone; the title is a four-item menu (Start, Mode, Settings, Quit)
  that works with arrows or a controller's D-pad. The pause menu (Resume, Restart, Settings, Quit to title) now shows
  the player's current controls beside it, for keyboard and controller.
- **Settings (user request):** one screen, opened from either menu, with three tabs: Sound (master, music, effects,
  mute; heard at once), Controls and Game (reduced motion, mouse and stick look speed). Saved to settings.json on close.
- **Rebinding:** two keys and one controller button for each of move up/down/left/right, use, and parts 1 to 3.
  Bindings are plain names in Core (`ControlBindings`, unit-tested): binding a key takes it off whatever had it, and a
  broken file is repaired on load. Pause and the menu keys (Esc / Start, Enter / A, arrows / D-pad, B back) are fixed,
  so a bad binding can never lock the player out. Prompts and the bench panel show the bound key for the device used
  last; dialogue.json writes the use key as `[use]`.
- **Controller:** the first connected pad. Left stick moves (with dead zone), right stick looks, Xbox-style buttons
  by default (A use, X / Y / RB parts, Start pause, B back). Input presses still latch, but expire after 0.25 s if
  nobody reads them, so a press in play can't fire later in a menu.

## 2026-11-06: Ribbon colours and perks between rounds
- **Ribbon colours (user request: "zoom in while the player can click on the color ribbon"):** `tuning.json >
  ribbonColors` (red, gold, green). Ribbon orders ask for one (taking turns, shifted per round) and the card names it.
  Using the ribbon station now starts a choice (`RibbonPick`): the toy goes on the station, the camera leans in over the
  shoulder as at the bench, the mouse pointer is freed, and the player clicks a spool (it lifts under the pointer) or
  presses its part key (1-3 / X Y RB). Then the ribbon is tied as before, a strand running from the spool to the toy.
  A coloured order only takes its colour (`wrong_ribbon_colour`); an order without a colour takes any ribbon. With no
  colours configured, tying starts at once as before. Spools and the bow are near-white models tinted in the game.
- **Perks (the first progression step agreed with the user):** after each passed round Santa offers 3 cards from
  `perks.json` (4 when the round beat the quota by 2+) and the player keeps one for the rest of the run. Seven perks:
  walk speed, bench time, ribbon speed, shovel speed, supply crate size, deadlines, points. Each stacks up to a limit and
  a maxed perk isn't offered. They combine into `RunModifiers`, which the rules read; the default changes nothing, so
  tests and runs without perks are unchanged. Next candidates: a Christmas Spirit meter and unlocks across runs.

## 2026-11-06: Christmas Spirit, unlocks, second optimisation pass
- **Christmas Spirit meter:** lasts the whole run (`tuning.json > spirit`): +4 per delivery (toy or coal), -20 per
  missed order, -8 per botched toy, +15 between rounds. At 75% or more deliveries are JOLLY (+10% points); at 0 the
  shift ends there and the run is over, even with the quota met. Shown as a star bar in the clock panel. The rules
  apply it from each step's events; off by default in code, so older tests are unchanged. The points perk was renamed
  Santa's Favour so the two don't share a name.
- **Unlocks across runs:** `PlayerProgress` (progress.json) keeps lifetime totals (toys, coal, furthest round, rounds
  cleared, runs played) and what's unlocked. `unlocks.json` rewards: three perks held back from Santa's offers until
  earned, four hat colours (the hat material is found by its red and recoloured for the player only; the workshop
  elves keep red), and Head Start (a perk pick before round 1). New unlocks show on the results screen; an Unlocks
  screen on the title lists progress and picks the hat.
- **Optimisation:** the profiler now reports allocations per frame (about 4 KB, one small GC every couple of seconds:
  fine). The title's UI cost halved (2.0 to 1.1 ms) by drawing each 2D triangle once in the right winding instead of in
  both orders, and coarser candy-cane segments. Gameplay frames are about 1.8 ms uncapped.

## 2026-11-07: Guided first round, smoother camera, sharper picture, a proper tree
- **Guided first round (user request):** round 1 is a lesson until the player clears a round (Settings > Game >
  Tutorial guide: until then / every run / off). The first six orders are arranged (`tuning.json > tutorial`): the
  3rd is coal for a naughty child, the 5th needs a ribbon, none are rush, and no other one turns into coal.
  `TutorialGuide` works out the next station from what the elf holds and the soonest order; `FloorPath` (A* on a 0.4 m
  grid, kept clear by the elf's width, then straightened) routes around the furniture. The arrows are low (4 cm)
  gold chevrons, drawn unlit and see-through, flowing towards the goal, with a ring where to stand. No arrows while
  working at a station. A hint and "GUIDE n / 6" sit under Santa's strip; a toast marks the end.
- **Camera (user request: smoother, and trackpads):** the 2-second follow now swings on a critically damped spring
  (eases in and out, never jerks) and glides to a stop when interrupted. Pointer movement under 1.5 px a frame is
  ignored, so a resting finger on a trackpad no longer keeps cancelling it. New: Camera follows the elf = after a
  moment / always (0.35 s) / off, and rebindable camera-turn keys (Z / C, triggers on a controller).
- **Grainy edges (user report):** the 3D picture was 960 x 540 stretched to the window. It now renders at the window's
  resolution (Settings > Game > 3D resolution 50 to 150%, capped at 2560 wide; render.json > followWindow off for the
  old fixed size), and an FXAA pass in the post shader smooths stair-stepped edges (Settings > Game > Smooth jagged
  edges). Frames stay about 2 ms uncapped.
- **Christmas tree (user request):** rebuilt: layered boughs, a gold tinsel garland, a string of 48 C9 bulbs in pink,
  orange, blue, green and yellow, 15 baubles and teardrops spread thinly, a glowing star, and two coloured point
  lights (16 room lights in all). New glow markers (`glow()` in helpers.py, `room_layout.json > glows`) draw soft
  additive halos on each bulb and the star, each breathing on its own rhythm. Snow flurries drift past the windows
  in gusts. All of it holds still under Reduced Motion.

## 2026-11-07: GDD brought up to date (version 0.2)
- **Reason:** `docs/GDD.md` was a placeholder. The owner supplied the original 0.1 text; it is now version 0.2, rewritten
  to state the current rules (every earlier entry in this log is folded in), so the document no longer contradicts the build.
- **Adds:** a sound-effect inventory with descriptors and wiring status (section 17), a proposed pool of Santa rule
  changes drawn per round from round 2 (section 10.3), backlog status (section 23), a revised plan to the December 1 deadline
  (section 22), and open decisions (section 28: licenses, music terms, event pool, AI-assistance disclosure).
- **Gaps recorded:** no sound-effect files, no Credits screen, release packaging not set up, no external playtests yet.

## 2026-11-07: Kit bins stocked once per shift; the lesson teaches the sled
- **Reason:** Bins gained a kit each time an order arrived, so they refilled all through the shift and the supply sled
  rarely mattered. Owner decision: no restocking in the middle of play, and the guided first round should run one kit
  short so its last lesson order teaches resupplying.
- **Details:** When a shift starts, `OrderSystem.PlanShift` fixes the order plan (toy, letter and naughty flag for every
  scheduled order) and fills the bins once with one kit per nice child's toy. Orders are created from the plan; nothing
  else adds kits except supply crates. Each shift gets a fresh sack. In the lesson round, the sack is one kit short for the
  6th lesson order's toy, and that toy is left out of the rest of the round. Its bin runs dry exactly at the 6th order, so
  the guide sends the elf to the sled. `tuning.json > tutorial.shortKit` turns this off.
- **Affects:** GDD sections 8, 9 and 19; `RunState.OrderPlan`, `OrderSystem`, `TutorialTuning.ShortKit`.
- **Validation:** `SupplyTests` (stock fixed at the start, not topped up, a full no-waste shift ends at zero), `TutorialTests`
  and `ShippedLessonTests` (the shortfall with the real data and every naughty list); 179 tests pass.
- **Supersedes:** "Santa sends one kit per toy asked for, as the letters arrive."

## 2026-11-07: Punch clock and door, toy pictures, 120 fps, clearer text
- **Reason:** Owner requests: a punch card machine so the player can end the shift early; a door (solid for now, usable
  later); a picture of each toy on its kit boxes; a tidy-up with a solid 120 fps; clearer text, especially the key labels
  at the bench and ribbon station.
- **Punch clock:** a new `clock` station (left wall, back corner). Use asks first, use again punches out: the shift ends
  scored as if time had run out (live orders unfinished, unwritten letters never arrive). With the quota met Santa signs
  off (`punched_out` cue) and the review shows the time left; short of quota it is a missed shift and the question says
  so. No early-finish bonus yet (proposed in GDD section 12).
- **Door:** `prop_door` on the back wall beside the clock, a solid prop with a collider, not a station. New tool
  `tools/blender/add_prop.py` adds a prop to room.blend; the clock's markers came from `sync_station_markers.py`.
- **Toy pictures:** painted once at startup from the order-card icons (`ToyPictures`), drawn as lit squares on all four
  sides of every kit crate and on each bin's sign. No image files: a new toy gets its picture automatically.
- **120 fps:** the rules still step at 60 Hz; frames between steps draw the elf and camera part of the way between steps
  (`FrameClock`). Frame rate is now a setting (60, 120 default, 144, match display, unlimited) instead of render.json
  `vsync`/`targetFps`; `FramePacer` holds it exactly (Raylib's limiter gave 118-119). Measured: 737 fps uncapped,
  120 fps capped with every frame within 9.1 ms. The 30 fps seen first was NVIDIA Battery Boost on battery, not the game.
- **Text:** the UI font is now an outline font loaded large with mipmaps (Windows Bahnschrift, else Segoe UI Bold or
  Arial Bold; or `Content/fonts/ui.ttf`) instead of Raylib's 10-pixel bitmap font scaled up. Bench key chips are bigger;
  the ribbon spool labels sit in one row with a line to each spool, so they no longer overlap.
- **Tidy:** doubled or misplaced doc comments fixed, an unused constructor parameter removed, the review cue chosen in one
  place (`Simulation.ReviewCueId`), profiler sections for stations and frame pacing (`--profile-capped`).
- **Affects:** GDD sections 5 to 8, 12 to 15, 17 to 20, 24; `stations.json`, `dialogue.json`, `render.json`, `room_layout.json`.
- **Validation:** `PunchClockTests` (8), frame rate settings tests; 191 tests pass. Screenshots of the clock, door,
  crates, bin signs, bench panel, ribbon labels and settings.
## 2026-11-08: Credits in Settings; all-rights-reserved licence
- **Reason:** Owner decisions: credits go in a Settings tab so they are easy to find; nobody may use the game without
  the owner's permission, but players should be able to donate safely; the composer wants credit under the name
  "blueeyedmonkey".
- **Credits:** a fourth Settings tab, Credits, drawn from `Content/data/credits.json` (two columns plus a copyright
  footer): the game by WattsOfCode, the music by blueeyedmonkey (used with permission), raylib, Raylib-cs, .NET, Blender,
  the font, AI assistance (Claude), and a thank-you for donations. Reachable from the title and the pause menu.
- **Licence:** `LICENSE.txt`, all rights reserved and source-available. Allowed without asking: playing, reading the code
  on GitHub (and forking there under GitHub's terms), streaming and videos with a link. Needs written permission:
  copying, modifying, redistributing, reuse in other projects, AI training. A donation buys no rights or promises.
  Game Off requires only a public repository, not an open-source licence (2025 FAQ; recheck the 2026 page). The licence,
  notices and asset credits are copied next to the executable in every build.
- **Music:** permission given by the composer on condition of credit; ASSET_CREDITS.md records it. The owner should keep
  the composer's message and confirm it covers a free game that accepts donations.
- **Supersedes:** "MIT proposed for code" (GDD section 26).
- **Validation:** a content test checks the credits name the composer and the copyright holder; screenshot of the tab.## 2026-11-08: Sound effects
- **Reason:** Owner request ("lets add some sound effects"). The game already triggered 12 effects by name but no files
  existed, so it was silent apart from the music.
- **Source:** every effect is synthesised by `tools/audio/make_sfx.py` (Python standard library: sine waves, filtered
  noise, bells, envelopes; fixed seeds so reruns are identical). No samples or sound packs, so they are the project's own
  work under LICENSE.txt and there are no outside licences to track. Chosen over jsfxr (manual, one file at a time) and
  CC0 packs (credit bookkeeping, a mixed sound).
- **Wired:** all 26 GDD section 17 effects except `fire_loop` (it needs a positional loop by the hearth; later), plus 4
  footstep variants. New in the game: menu move ticks, hammer pitch rising per part (`IAudioService.Play` takes a pitch),
  separate crate, restock, shovel, spool, recycle, coal-delivery and new-letter sounds, a tick each of the last 10
  seconds, JOLLY and low-Spirit cues, footsteps by distance walked, and the results screen's fanfare or slide whistle
  after the end-of-shift whistle, then an unlock sparkle.
- **Levels:** peaks 0.2 (ticks) to 0.75 (fanfare); long held tones (whistles, squeak) set lower so they don't sound
  louder than the short ones. All go through the Effects volume.
- **Affects:** GDD section 17 and the roadmap rows; `AudioService`, `InteractionController`, `WorkshopController`,
  `ResultsController`, the menus; `credits.json`, ASSET_CREDITS.md, README.
## 2026-11-08: Credits trimmed; licence review
- **Reason:** Owner: the credits page should show only what is legally required (plus the music credit), not how the game
  was made; check nothing needs royalties or blocks commercial use. blueeyedmonkey allows free use, asking only for credit.
- **Credits page:** the game by WattsOfCode, the music by blueeyedmonkey, a thank-you, and a footer pointing to
  LICENSE.txt and THIRD_PARTY_NOTICES.md. Removed: libraries, Blender, the font, AI assistance, the sound-effect note.
- **Review:** raylib, Raylib-cs, GLFW (zlib: no credit required, keep the notice in source); raylib's MIT parts (cgltf,
  QOI, QOA, par_shapes, tinyobj_loader_c, vox_loader, m3d, glad/Khronos) and the .NET runtime (MIT: notice must ship with
  copies, now in THIRD_PARTY_NOTICES.md with the full licence texts, copied next to the game); stb, miniaudio, dr_*,
  sdefl/sinfl, jar_* (public domain option); Blender, Python, the AI assistant (tools only; their output is the owner's,
  no credit needed); the Windows font (used in place, not shipped). Nothing charges royalties or limits commercial use.
- **Release step:** a self-contained publish must also include the .NET runtime pack's THIRD-PARTY-NOTICES.TXT.
- **Supersedes:** the 2026-11-08 credits list above.
## 2026-11-08: Shipped interface font; Mac launcher
- **Reason:** Owner: the game must look good on Windows and macOS. The Windows system font (Bahnschrift) doesn't exist on
  a Mac, so text there fell back to Raylib's 10-pixel bitmap font.
- **Font:** Barlow SemiBold, from Google Fonts' repository, unmodified, shipped as `Content/fonts/Barlow-SemiBold.ttf`
  with `OFL.txt`. Similar DIN-style look to Bahnschrift. SIL Open Font License 1.1: bundling and commercial use free, no
  royalties, no on-screen credit; the licence file must travel with it (a test checks this). System fonts per OS (Windows,
  macOS, Linux) remain as fallbacks only.
- **Mac launcher:** `Play Santa Shift.command` builds and runs the DLL through `dotnet` (Apple Silicon and Intel).
  `.gitattributes` keeps it LF and git records it executable.
- **Not done:** Retina (high-DPI) drawing; untested on a real Mac so far.

# Santa Shift

Finish your shift in Santa's workshop while your boss keeps shifting the rules.
A small third-person 3D time-management game for **Game Off 2026** (theme: SHIFT).
Meet each round's quota to move on to a harder round; miss it and the run is over.

Built in C# with .NET 10 and [Raylib-cs](https://github.com/ChrisDill/Raylib-cs). No engine. Models are made in Blender.

## Controls

| Keyboard | Controller | Action |
|---|---|---|
| Mouse | Right stick | Look around (the camera follows behind the elf) |
| WASD / Arrows | Left stick / D-pad | Move (relative to the camera) |
| E / Space | A | Collect, start building, add ribbon, ship, recycle |
| 1 / 2 / 3 | X / Y / RB | At the bench: hammer on the part on that key. Follow the order shown; a wrong part or running out of time botches the toy (scrap it in the barrel) |
| Esc | Start | Pause (Esc also cancels the recycle prompt) |
| Arrows, Enter, Esc, or the mouse (click; right click backs out) | D-pad, A, B | Menus |

Every key and button above (except pause and the menu keys) can be changed in **Settings > Controls**, from the
title screen or the pause menu; prompts show whatever you bound. Settings also has volumes (master, music, effects,
mute), reduced motion and look speed for the mouse and the stick. The pause menu lists your current controls.

Building gets harder each round: less time, then the parts go on in a random order, then they move to different keys.

Each run starts with **Santa's naughty and nice list**. A few children are naughty: their orders want a lump of coal
instead of a toy. Shovel coal from the scuttle at the fireplace (E, hold still for a moment) and ship it like a toy.

The camera swings round behind the elf once it has faced one way for 2 seconds with the mouse left alone (smoothly,
and trackpad jitter doesn't count). On a trackpad, set Settings > Game > Camera follows the elf to Always, or turn it
with Z / C (the triggers on a controller).

The elf holds whatever it carries up on its right hand, beside its head, so you can always see it from behind.

**Ribbons come in colours.** A ribbon order asks for red, gold or green (shown on its card). At the ribbon station the
camera leans in over the spools: click the one you want, or press its key (1 / 2 / 3, X / Y / RB on a controller),
then hold still while it is tied. A toy with the wrong colour won't ship to that child. Colours are in tuning.json.

**Perks between rounds.** After each round you pass, Santa offers three perk cards (four if you beat the quota by 2 or
more) and you keep one for the rest of the run: walk faster, more time at the bench, quicker ribbons and shovelling,
bigger supply crates, longer deadlines, more points. Each can stack a few times. They are listed in `perks.json`.

**Guided first round.** Round 1 of every run is a lesson: glowing arrows on the floor lead from the
elf to the next station (around the furniture), with a hint under Santa's strip and a small card in the bottom-left corner showing how to move and look, and that
every control can be changed in Settings > Controls. The first six orders show every job:
plain toys, one lump of coal for a naughty child, one ribbon and one trip to the supply sled. Know the job already? Turn it off
with the Tutorial button on Santa's list (Tab, or Y on a controller), or in Settings > Game > Tutorial helpers.

**Christmas Spirit.** The star bar under the clock lasts the whole run. Deliveries raise it; missed orders and botched
toys lower it, and it recovers a little between rounds. Keep it above 75% for JOLLY: 10% more points. If it runs out,
the shift ends there and so does the run. Tuned in tuning.json > spirit.

**Unlocks.** Lifetime totals (toys and coal delivered, furthest round, runs played) unlock more perks for Santa to
offer, hat colours for your elf (pick one in **Unlocks** on the title screen), and a Head Start perk before round 1.
They are listed in `unlocks.json` and saved in progress.json next to your settings.

**Kit bins run out.** When a shift starts, Santa fills the bins once with one kit for every toy the shift's letters
will ask for (the label says how many are left). Nothing tops them up during the shift. Botch or scrap a kit and that
bin comes up short: its label turns red, and you have to fetch a supply crate from the **supply sled** against the right
wall and carry it to the bin (E) to restock it (2 kits per crate, `resupplyKits` in tuning.json). The guided first round
is one kit short on purpose, so its last lesson order teaches the sled. Without a supply station in `stations.json`, bins
never run out. Every kit crate and bin sign shows its toy's picture (painted at startup from the order-card icons).

**Punch out early.** The punch clock on the left wall, by the workshop door, ends the shift: press E there, then E again
to confirm (move or Esc to cancel), or click Punch out / Keep working. The shift is scored as if time had run out, so with the quota met it's a safe pass;
short of it, the run ends. The door beside it is shut for now (a solid fixture a later update can open).

**Frame rate.** The game draws at 120 fps by default (Settings > Game > Frame rate: 60, 120, 144, match display or
unlimited) while the rules step at 60 Hz, with movement smoothed between steps. On a laptop running on battery, NVIDIA
Battery Boost can hold games to 30 fps: plug in or turn it off.

## Build and run

Requires the .NET 10 SDK. Double-click **`Play Santa Shift.cmd`** (Windows) or **`Play Santa Shift.command`** (Mac) to
build the latest code and play. On Windows, double-click **`Add Desktop Shortcut.cmd`** once for a Santa Shift
shortcut on your desktop with the game's candy cane icon. On a Mac, macOS blocks a downloaded script the first time: open System Settings >
Privacy & Security and click Open Anyway, or run `bash "Play Santa Shift.command"` in Terminal from the game folder.
If double-clicking says it can't be run, run `chmod +x "Play Santa Shift.command"` once. Tested on an Intel Mac
(macOS 15). Or, on any system:

```
dotnet build
dotnet run --project src/SantaShift.Game
dotnet test
```

Release packaging (`dotnet publish`) is not set up yet.

## Project layout

```
src/SantaShift.Core/    Models and rules. Plain C#, no Raylib, fully unit-tested.
  Models/               Data: Item, Order, Toy, ChildLetter, Station, RunState, Settings...
  Rules/                Simulation (one 60 Hz step; drawn at up to 120+ fps), OrderSystem, CraftingSystem, EventSystem, Scoring
  Content/              Loads and validates the JSON data files
  Persistence/          Settings and best scores (JSON in %AppData%/SantaShift)
src/SantaShift.Game/    Views and controllers. All Raylib code lives here.
  App/                  Window, fixed-step game loop, screen switching
  Controllers/          Title, Workshop, Results screens, and player / interaction / pause input
  Views/World/          3D drawing: room, stations, elf, toys (Blender model or grey-box fallback)
  Views/UI/             HUD, order cards, Santa strip, menus, results
  Services/             Input, audio, asset library (each behind an interface, so it can be swapped)
  Content/data/         All tunable data: tuning, toys, letters, stations, events, dialogue, render
  Content/models/       Exported .glb files from Blender
tests/SantaShift.Tests/ xUnit tests for Core, including the GDD's scoring examples
docs/                   Design document, decision log, Blender export rules
source-assets/blender/  .blend source files (not shipped)
```

**MVC rule:** views only read state, controllers turn input into commands, and only `Core/Rules` changes the game.

## Changing content without code

Everything in `src/SantaShift.Game/Content/data/` is plain JSON (comments allowed):

- **letters.json**: add or remove children's letters. A letter for an unknown toy is skipped with a warning.
- **audio/**: `music_menu.mp3` (title, list, results) and `music_workshop.mp3` (the shift) loop and crossfade; `.ogg` or
  `.wav` work too. Volume is the Music setting (`MusicVolume`, 0.25 by default). Missing files just mean silence.
  The sound effects (`<id>.wav`, listed in GDD section 17) are synthesised by `py tools/audio/make_sfx.py`; edit a
  sound's recipe there and rerun it, or drop in your own `.wav` with the same name.
- **perks.json**: the perks Santa offers between rounds (effect, amount, how many times each can stack).
- **unlocks.json**: what lifetime totals unlock (a perk, a hat colour, or the head start).
- **tuning.json**: deadlines, quotas, speeds, spawn times, score values.
- **toys.json / stations.json**: add a toy, give it a dispenser, move fixtures.
- **events.json / dialogue.json**: Santa's schedule and every line of text.
- **render.json**: window size, internal resolution (lower it for weak devices), camera.

The tests load the real data files, so `dotnet test` catches a broken edit.

## Adding Blender models

The room itself is `source-assets/blender/room.blend`; see "Designing the room" in the export guide.
For other models, save the `.blend` in `source-assets/blender/` named after the model, then run `.\tools\blender\export.ps1`.
It exports to `Content/models/` and checks the rules in [docs/blender-export.md](docs/blender-export.md).
Or export by hand and drop the `.glb` into `Content/models/`
with the name used in `toys.json` / `stations.json` (or `elf.glb`, `kit_crate.glb`, `ribbon_bow.glb`).
A missing model simply shows the grey-box shape.

## Credits and licenses

Music: "Toy Factory Jingle" and "Cozy Christmas Game Ambience" by **blueeyedmonkey** (free to use; credit requested).
Sound effects: made for the game, synthesised from scratch by `tools/audio/make_sfx.py`.

Interface font: Barlow SemiBold (SIL Open Font License, `Content/fonts/OFL.txt`), shipped so text looks the same on
Windows and macOS. To try another, put a font whose licence allows shipping it at `Content/fonts/ui.ttf`.

The credits are in the game under **Settings > Credits** (edit `Content/data/credits.json`).
See [ASSET_CREDITS.md](ASSET_CREDITS.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

**License: all rights reserved.** Santa Shift is source-available, not open source: you may play it, read the code
here, and stream or record it, but not copy, modify, redistribute or reuse any of it without written permission from
WattsOfCode. Donations are welcome and buy no rights. Full terms in [LICENSE.txt](LICENSE.txt).

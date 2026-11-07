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
| Arrows, Enter, Esc | D-pad, A, B | Menus |

Every key and button above (except pause and the menu keys) can be changed in **Settings > Controls**, from the
title screen or the pause menu; prompts show whatever you bound. Settings also has volumes (master, music, effects,
mute), reduced motion and look speed for the mouse and the stick. The pause menu lists your current controls.

Building gets harder each round: less time, then the parts go on in a random order, then they move to different keys.

Each run starts with **Santa's naughty and nice list**. A few children are naughty: their orders want a lump of coal
instead of a toy. Shovel coal from the scuttle at the fireplace (E, hold still for a moment) and ship it like a toy.

The camera swings round behind the elf once it has faced one way for 2 seconds with the mouse left alone.

The elf holds whatever it carries up on its right hand, beside its head, so you can always see it from behind.

**Ribbons come in colours.** A ribbon order asks for red, gold or green (shown on its card). At the ribbon station the
camera leans in over the spools: click the one you want, or press its key (1 / 2 / 3, X / Y / RB on a controller),
then hold still while it is tied. A toy with the wrong colour won't ship to that child. Colours are in tuning.json.

**Perks between rounds.** After each round you pass, Santa offers three perk cards (four if you beat the quota by 2 or
more) and you keep one for the rest of the run: walk faster, more time at the bench, quicker ribbons and shovelling,
bigger supply crates, longer deadlines, more points. Each can stack a few times. They are listed in `perks.json`.

**Christmas Spirit.** The star bar under the clock lasts the whole run. Deliveries raise it; missed orders and botched
toys lower it, and it recovers a little between rounds. Keep it above 75% for JOLLY: 10% more points. If it runs out,
the shift ends there and so does the run. Tuned in tuning.json > spirit.

**Unlocks.** Lifetime totals (toys and coal delivered, furthest round, runs played) unlock more perks for Santa to
offer, hat colours for your elf (pick one in **Unlocks** on the title screen), and a Head Start perk before round 1.
They are listed in `unlocks.json` and saved in progress.json next to your settings.

**Kit bins run out.** Santa sends one kit per toy asked for, so each bin only holds what the letters so far need
(the label says how many are left). Botch or scrap a kit and that bin comes up short: its label turns red, and you have
to fetch a supply crate from the **supply sled** against the right wall and carry it to the bin (E) to restock it
(2 kits per crate, `resupplyKits` in tuning.json). Without a supply station in `stations.json`, bins never run out.

## Build and run

Requires the .NET 10 SDK.

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
  Rules/                Simulation (one 60 Hz step), OrderSystem, CraftingSystem, EventSystem, Scoring
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

Music: "Toy Factory Jingle" and "Cozy Christmas Game Ambience" by **blueeyedmonkey**.

See [ASSET_CREDITS.md](ASSET_CREDITS.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
The project's own code license has not been chosen yet (GDD section 26).

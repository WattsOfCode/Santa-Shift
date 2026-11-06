# Santa Shift

Finish your shift in Santa's workshop while your boss keeps shifting the rules.
A small 3D time-management game for **Game Off 2026** (theme: SHIFT).

Built in C# with .NET 10 and [Raylib-cs](https://github.com/ChrisDill/Raylib-cs). No engine. Models are made in Blender.

## Controls

| Key | Action |
|---|---|
| WASD / Arrows | Move |
| E / Space | Collect, assemble, add ribbon, ship, recycle |
| Esc | Pause (or cancel the recycle prompt) |
| Tab (title screen) | Switch Standard / Relaxed mode |

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
- **tuning.json**: deadlines, quotas, speeds, spawn times, score values.
- **toys.json / stations.json**: add a toy, give it a dispenser, move fixtures.
- **events.json / dialogue.json**: Santa's schedule and every line of text.
- **render.json**: window size, internal resolution (lower it for weak devices), camera.

The tests load the real data files, so `dotnet test` catches a broken edit.

## Adding Blender models

Save the `.blend` in `source-assets/blender/` named after the model, then run `.\tools\blender\export.ps1`.
It exports to `Content/models/` and checks the rules in [docs/blender-export.md](docs/blender-export.md).
Or export by hand and drop the `.glb` into `Content/models/`
with the name used in `toys.json` / `stations.json` (or `elf.glb`, `kit_crate.glb`, `ribbon_bow.glb`).
A missing model simply shows the grey-box shape.

## Credits and licenses

See [ASSET_CREDITS.md](ASSET_CREDITS.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
The project's own code license has not been chosen yet (GDD section 26).

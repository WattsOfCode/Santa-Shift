# Asset credits

Record every model, texture, sound and music file here (GDD section 26).

For original assets: creator, method/tool, editable source path, export path, date.
For outside assets: file name, author, source URL, license, required credit, modifications, local license file.

| File | Type | Creator | Tool / Source | Source file | License | Date |
|---|---|---|---|---|---|---|
| `Content/audio/music_workshop.mp3` ("Toy Factory Jingle") | Music, plays during a shift | blueeyedmonkey | Supplied by the project owner, 2026-11-06 | - | Composer's permission to use freely (commercial use and donations included); only condition: credit as "blueeyedmonkey" (Settings > Credits, README, THIRD_PARTY_NOTICES.md, game page). No fee or royalty. Owner keeps the composer's message as the record | 2026-11-08 |
| `Content/audio/music_menu.mp3` ("Cozy Christmas Game Ambience") | Music, title, list and results screens | blueeyedmonkey | Supplied by the project owner, 2026-11-06 | - | As above | 2026-11-08 |
| All `Content/models/*.glb` | 3D models | Project (generated) | Blender 5.2, `tools/blender/make_models.py` | `source-assets/blender/*.blend` | All rights reserved, WattsOfCode (LICENSE.txt) | 2026-11 |
| Toy pictures on kit crates and bins | Textures, painted at startup | Project (code) | `Views/World/ToyPictures.cs` from the order-card icons | - | All rights reserved, WattsOfCode (LICENSE.txt) | 2026-11-07 |
| Sound effects note | - | - | No credit required (own work); not listed in the in-game credits | - | - | - |
| Interface font | Font, **not shipped** | Microsoft (Bahnschrift / Segoe UI / Arial) | Loaded from the player's Windows Fonts folder at runtime | - | Windows system font, used in place, not redistributed. To ship a font, add an OFL one as `Content/fonts/ui.ttf` and record it here | 2026-11-07 |
| `Content/audio/*.wav` (all sound effects) | Sound effects | Project (generated) | Synthesised by `tools/audio/make_sfx.py` (Python, standard library: sine waves, filtered noise, envelopes; no samples) | `tools/audio/make_sfx.py` | All rights reserved, WattsOfCode (LICENSE.txt) | 2026-11-08 |

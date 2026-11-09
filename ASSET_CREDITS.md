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
| `Content/fonts/Barlow-SemiBold.ttf` | Interface font (Windows and macOS) | The Barlow Project Authors | https://github.com/google/fonts/tree/main/ofl/barlow (unmodified) | - | SIL Open Font License 1.1, full text shipped as `Content/fonts/OFL.txt`; free for commercial use, no credit on screen required | 2026-11-08 |
| System fonts (fallback only) | Font, **not shipped** | Microsoft / Apple | Read from the player's own Fonts folder only if the shipped font is missing | - | Used in place, not redistributed | 2026-11-08 |
| `Content/audio/*.wav` (all sound effects) | Sound effects | Project (generated) | Synthesised by `tools/audio/make_sfx.py` (Python, standard library: sine waves, filtered noise, envelopes; no samples) | `tools/audio/make_sfx.py` | All rights reserved, WattsOfCode (LICENSE.txt) | 2026-11-08 |
| `Icon/santa_shift.ico`, `Icon/santa_shift.icns`, `Content/textures/icon_*.png` | App and window icon (candy cane and holly) | Project (generated) | Drawn by `tools/icon/make_icon.py` (Python, standard library: signed-distance shapes, GDD palette; no outside art) | `tools/icon/make_icon.py` (preview `source-assets/icon/santa_shift_1024.png`) | All rights reserved, WattsOfCode (LICENSE.txt) | 2026-11-09 |

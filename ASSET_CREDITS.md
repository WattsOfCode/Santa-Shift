# Asset credits

Record every model, texture, sound and music file here (GDD section 26).

For original assets: creator, method/tool, editable source path, export path, date.
For outside assets: file name, author, source URL, license, required credit, modifications, local license file.

| File | Type | Creator | Tool / Source | Source file | License | Date |
|---|---|---|---|---|---|---|
| `Content/audio/music_workshop.mp3` ("Toy Factory Jingle") | Music, plays during a shift | blueeyedmonkey | Supplied by the project owner, 2026-11-06 | - | Credit: blueeyedmonkey. License terms to confirm before release | 2026-11-06 |
| `Content/audio/music_menu.mp3` ("Cozy Christmas Game Ambience") | Music, title, list and results screens | blueeyedmonkey | Supplied by the project owner, 2026-11-06 | - | Credit: blueeyedmonkey. License terms to confirm before release | 2026-11-06 |
| All `Content/models/*.glb` | 3D models | Project (generated) | Blender 5.2, `tools/blender/make_models.py` | `source-assets/blender/*.blend` | Project license (not chosen yet) | 2026-11 |
| Toy pictures on kit crates and bins | Textures, painted at startup | Project (code) | `Views/World/ToyPictures.cs` from the order-card icons | - | Project license (not chosen yet) | 2026-11-07 |
| Interface font | Font, **not shipped** | Microsoft (Bahnschrift / Segoe UI / Arial) | Loaded from the player's Windows Fonts folder at runtime | - | Windows system font, used in place, not redistributed. To ship a font, add an OFL one as `Content/fonts/ui.ttf` and record it here | 2026-11-07 |

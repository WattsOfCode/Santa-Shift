"""Builds the Mac player downloads from Windows (or anywhere with the .NET 10 SDK): Santa Shift.app for Apple Silicon
and for Intel, each zipped with a How to play note.

    py tools/release/make_mac_apps.py

Output: publish/SantaShift-macOS-AppleSilicon.zip and publish/SantaShift-macOS-Intel.zip.

The apps are not signed (no Apple Developer ID). Apple Silicon Macs refuse to start a program with no signature at
all, so the note gives players one Terminal command that signs it on their own Mac (an ad-hoc signature) and lifts
the download quarantine. Intel Macs only need System Settings > Privacy & Security > Open Anyway.

Zips made with Windows tools drop the Unix "executable" bit, and the app then won't start; this script writes the
zip itself and marks the program and the graphics library executable. Standard library only.
"""
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PUBLISH = ROOT / "publish"
GAME = ROOT / "src" / "SantaShift.Game"
ICON = GAME / "Icon" / "santa_shift.icns"
NOTICES = ["LICENSE.txt", "ASSET_CREDITS.md", "THIRD_PARTY_NOTICES.md"]
EXECUTABLES = {"SantaShift", "libraylib.dylib"}

TARGETS = [("osx-arm64", "AppleSilicon", "Apple Silicon (M1 and later)"), ("osx-x64", "Intel", "Intel")]

INFO_PLIST = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Santa Shift</string>
  <key>CFBundleDisplayName</key><string>Santa Shift</string>
  <key>CFBundleIdentifier</key><string>io.github.wattsofcode.santashift</string>
  <key>CFBundleExecutable</key><string>SantaShift</string>
  <key>CFBundleIconFile</key><string>santa_shift</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>{version}</string>
  <key>CFBundleVersion</key><string>{version}</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.games</string>
  <!-- The game draws at the logical window size; Retina support is not built yet. -->
  <key>NSHighResolutionCapable</key><false/>
</dict>
</plist>
"""

HOW_TO_PLAY = """SANTA SHIFT for Mac ({label})
===========================

1. Drag "Santa Shift.app" into your Applications folder.

2. The app isn't signed by Apple, so macOS won't open it straight away.
{steps}

After that, open Santa Shift like any other app. Nothing else to install.

Controls: WASD or the arrow keys to move, the mouse or trackpad to look, E to use a station, 1 2 3 at the bench,
Esc to pause. A controller works too. Change any control in Settings > Controls.

Your settings, unlocks and best scores are saved in your user folder, so replacing the app with a newer version
keeps them (Settings > Game > Start over erases them).

Credits and licences: ASSET_CREDITS.md, THIRD_PARTY_NOTICES.md and LICENSE.txt in this folder.
"""

STEPS_ARM = """   Apple Silicon Macs need the app signed on your own Mac first. Open Terminal (Applications > Utilities) and
   paste these two lines, pressing Return after each:

     xattr -cr "/Applications/Santa Shift.app"
     codesign --force --deep --sign - "/Applications/Santa Shift.app"

   The first line removes the "downloaded from the internet" block; the second gives the app a local signature.
   If you're asked to install the command line developer tools, accept, then run the second line again."""

STEPS_INTEL = """   Double-click it once; macOS says it can't be opened. Then open System Settings > Privacy & Security, scroll
   down and click "Open Anyway" next to Santa Shift, and confirm.

   Or, in Terminal: xattr -cr "/Applications/Santa Shift.app" """


def version() -> str:
    """The app's version number (set SANTASHIFT_VERSION to override)."""
    return os.environ.get("SANTASHIFT_VERSION", "4.0")


def publish(rid: str, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    cmd = ["dotnet", "publish", str(GAME), "-c", "Release", "-r", rid, "--self-contained", "true",
           "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=false",
           "-p:EnableCompressionInSingleFile=true", "-p:DebugType=none", "-o", str(out), "--nologo", "-v", "quiet"]
    subprocess.run(cmd, check=True)


def build_app(published: Path, folder: Path, label: str, arm: bool) -> None:
    if folder.exists():
        shutil.rmtree(folder)
    contents = folder / "Santa Shift.app" / "Contents"
    macos, resources = contents / "MacOS", contents / "Resources"
    shutil.copytree(published, macos)  # the program, the graphics library and Content, side by side
    resources.mkdir(parents=True)
    shutil.copy2(ICON, resources / "santa_shift.icns")
    (contents / "Info.plist").write_bytes(INFO_PLIST.format(version=version()).encode("utf-8"))
    (contents / "PkgInfo").write_bytes(b"APPL????")
    for name in NOTICES:
        shutil.copy2(macos / name, folder / name)
    note = HOW_TO_PLAY.format(label=label, steps=STEPS_ARM if arm else STEPS_INTEL)
    (folder / "How to play.txt").write_bytes(note.encode("utf-8"))  # Unix line endings, for the Mac


def zip_folder(folder: Path, zip_path: Path, top: str) -> None:
    """Zips with Unix permissions, so the program stays executable when macOS unzips it."""
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in sorted(folder.rglob("*")):
            rel = Path(top) / path.relative_to(folder)
            name = rel.as_posix() + ("/" if path.is_dir() else "")
            info = zipfile.ZipInfo(name, date_time=(2026, 11, 9, 12, 0, 0))
            info.create_system = 3  # Unix, so external_attr holds a mode
            if path.is_dir():
                info.external_attr = (0o40755 << 16) | 0x10
                z.writestr(info, b"")
            else:
                mode = 0o100755 if path.name in EXECUTABLES else 0o100644
                info.external_attr = mode << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(info, path.read_bytes())


def main() -> int:
    PUBLISH.mkdir(exist_ok=True)
    for rid, short, label in TARGETS:
        print(f"Building the Mac app for {label}...")
        published = PUBLISH / "_build" / rid
        publish(rid, published)
        folder = PUBLISH / f"Santa Shift (macOS {short})"
        build_app(published, folder, label, arm=rid.endswith("arm64"))
        zip_path = PUBLISH / f"SantaShift-macOS-{short}.zip"
        zip_folder(folder, zip_path, "Santa Shift")
        print(f"  {zip_path.relative_to(ROOT)}  ({zip_path.stat().st_size / 1e6:.0f} MB)")
    shutil.rmtree(PUBLISH / "_build", ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())

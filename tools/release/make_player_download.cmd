@echo off
rem Makes the player downloads: ready-to-play Santa Shift with no source code and no .NET needed.
rem Output in publish\: SantaShift-Windows.zip, SantaShift-macOS-AppleSilicon.zip and SantaShift-macOS-Intel.zip
rem (and the unzipped folders). None of them is signed; each has a How to play note for the first launch.
rem Run from anywhere; needs the .NET 10 SDK on this computer only.
cd /d "%~dp0..\.."
if exist "publish\Santa Shift" rmdir /s /q "publish\Santa Shift"
dotnet publish src\SantaShift.Game -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o "publish\Santa Shift" --nologo -v quiet
if errorlevel 1 (
  echo.
  echo Publish failed - see the errors above.
  pause
  exit /b 1
)
copy /y "tools\release\How to play.txt" "publish\Santa Shift\" >nul
if exist "publish\SantaShift-Windows.zip" del "publish\SantaShift-Windows.zip"
powershell -NoProfile -Command "Compress-Archive -Path 'publish\Santa Shift' -DestinationPath 'publish\SantaShift-Windows.zip'"
echo Made publish\SantaShift-Windows.zip

rem The Mac apps (Apple Silicon and Intel), zipped with their permissions kept. Needs Python (py).
py tools\release\make_mac_apps.py
if errorlevel 1 (
  echo.
  echo The Mac apps failed - see the errors above. The Windows zip is fine.
  pause
  exit /b 1
)
echo.
echo Done: the Windows and Mac zips are in the publish folder.
timeout /t 5 >nul 2>&1

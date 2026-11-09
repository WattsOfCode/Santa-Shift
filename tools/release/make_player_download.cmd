@echo off
rem Makes the player downloads: ready-to-play Santa Shift with no source code and no .NET needed.
rem Output in publish\:
rem   SantaShift.exe                      Windows: the whole game in one file. Download, double-click, play.
rem   SantaShift-macOS-AppleSilicon.zip   Mac (M1 and later): Santa Shift.app and a How to play note
rem   SantaShift-macOS-Intel.zip          Mac (Intel): the same
rem None of them is signed. Needs the .NET 10 SDK and Python (py) on this computer only.
cd /d "%~dp0..\.."
if not exist publish mkdir publish
if exist "publish\_windows" rmdir /s /q "publish\_windows"

rem Everything (code, Content, the graphics library) goes inside the one exe. On first launch it unpacks itself to a
rem hidden temp folder (about 2 seconds; later launches reuse it). Saves and the error log live in %AppData%\SantaShift.
dotnet publish src\SantaShift.Game -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "publish\_windows" --nologo -v quiet
if errorlevel 1 (
  echo.
  echo Publish failed - see the errors above.
  pause
  exit /b 1
)
move /y "publish\_windows\SantaShift.exe" "publish\SantaShift.exe" >nul
rmdir /s /q "publish\_windows"
rem Older downloads made by earlier versions of this script.
if exist "publish\Santa Shift" rmdir /s /q "publish\Santa Shift"
if exist "publish\SantaShift-Windows.zip" del "publish\SantaShift-Windows.zip"
echo Made publish\SantaShift.exe

rem The Mac apps (Apple Silicon and Intel), zipped with their permissions kept.
py tools\release\make_mac_apps.py
if errorlevel 1 (
  echo.
  echo The Mac apps failed - see the errors above. SantaShift.exe is fine.
  pause
  exit /b 1
)
echo.
echo Done: SantaShift.exe and the two Mac zips are in the publish folder.
timeout /t 5 >nul 2>&1

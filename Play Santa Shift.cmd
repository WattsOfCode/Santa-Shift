@echo off
rem Builds the latest code (Release) and starts Santa Shift. Double-click to play.
rem It works out of sight: this window reopens minimised while it builds, then closes when the game starts.
rem If the build fails, a message says so and the details open in Notepad.
if not "%~1"=="--hidden" (
  start "Santa Shift" /min cmd /c ""%~f0" --hidden"
  exit /b
)
cd /d "%~dp0"
set "LOG=%TEMP%\santashift-build.log"
dotnet build src\SantaShift.Game -c Release --nologo -v quiet > "%LOG%" 2>&1
if errorlevel 1 (
  powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('Santa Shift could not be built. The details will open in Notepad.', 'Santa Shift')"
  start "" notepad "%LOG%"
  exit /b 1
)
start "" "src\SantaShift.Game\bin\Release\net10.0\SantaShift.exe"

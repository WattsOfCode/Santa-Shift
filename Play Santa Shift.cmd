@echo off
rem Builds the latest code (Release) and starts Santa Shift. Double-click to play.
cd /d "%~dp0"
dotnet build src\SantaShift.Game -c Release --nologo -v quiet
if errorlevel 1 (
  echo.
  echo Build failed - see the errors above.
  pause
  exit /b 1
)
start "" "src\SantaShift.Game\bin\Release\net10.0\SantaShift.exe"

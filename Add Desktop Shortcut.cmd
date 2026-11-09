@echo off
rem Puts a "Santa Shift" shortcut on your desktop, with the game's candy cane icon. Double-click once.
rem The shortcut runs "Play Santa Shift.cmd" in this folder (builds the latest code, then plays). If you move the
rem game folder, run this again.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$here = '%~dp0';" ^
  "$desktop = [Environment]::GetFolderPath('Desktop');" ^
  "$link = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $desktop 'Santa Shift.lnk'));" ^
  "$link.TargetPath = Join-Path $here 'Play Santa Shift.cmd';" ^
  "$link.WorkingDirectory = $here;" ^
  "$link.IconLocation = (Join-Path $here 'src\SantaShift.Game\Icon\santa_shift.ico') + ',0';" ^
  "$link.Description = 'Play Santa Shift';" ^
  "$link.WindowStyle = 7;" ^
  "$link.Save();" ^
  "Write-Host ('Made the shortcut: ' + (Join-Path $desktop 'Santa Shift.lnk'))"
if errorlevel 1 (
  echo.
  echo Could not make the shortcut - see the message above.
  pause
  exit /b 1
)
timeout /t 4 >nul 2>&1

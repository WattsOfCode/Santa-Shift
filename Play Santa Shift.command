#!/bin/bash
# Builds the latest code (Release) and starts Santa Shift on a Mac. Double-click to play.
# Needs the .NET 10 SDK: https://dotnet.microsoft.com/download
# First time only: if macOS says it can't be opened, right-click it and choose Open, or run
#   chmod +x "Play Santa Shift.command"
cd "$(dirname "$0")" || exit 1

# A double-clicked script doesn't always see the shell's PATH, so look in the usual install places too.
export PATH="$PATH:/usr/local/share/dotnet:/opt/homebrew/bin:/usr/local/bin:$HOME/.dotnet"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET 10 SDK is not installed. Get it from https://dotnet.microsoft.com/download"
  read -r -p "Press Enter to close."
  exit 1
fi

if ! dotnet build src/SantaShift.Game -c Release --nologo -v quiet; then
  echo
  echo "Build failed - see the errors above."
  read -r -p "Press Enter to close."
  exit 1
fi

# Run the DLL through dotnet: it works on Apple Silicon and Intel Macs alike.
dotnet "src/SantaShift.Game/bin/Release/net10.0/SantaShift.dll" &
disown

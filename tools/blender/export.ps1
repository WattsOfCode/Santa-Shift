<#
.SYNOPSIS
  Exports Blender files in source-assets/blender to game models in src/SantaShift.Game/Content/models.

.EXAMPLE
  .\tools\blender\export.ps1                 # export every .blend
  .\tools\blender\export.ps1 toy_train       # export one file (name without .blend)

.NOTES
  Each .blend becomes a .glb with the same name, so name the .blend files after the model
  names in docs/blender-export.md (toy_train.blend -> toy_train.glb).
  Blender location: $env:SANTASHIFT_BLENDER, or D:\Blender\blender.cmd by default.
#>
param([string[]]$Names)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$blender = if ($env:SANTASHIFT_BLENDER) { $env:SANTASHIFT_BLENDER } else { 'D:\Blender\blender.cmd' }
$sourceDir = Join-Path $root 'source-assets\blender'
$outDir = Join-Path $root 'src\SantaShift.Game\Content\models'
$script = Join-Path $PSScriptRoot 'export_glb.py'

if (-not (Test-Path $blender)) { throw "Blender not found at $blender. Set SANTASHIFT_BLENDER to blender.exe." }

$files = Get-ChildItem $sourceDir -Filter '*.blend' | Where-Object { -not $Names -or $Names -contains $_.BaseName }
if (-not $files) { Write-Host "No .blend files to export in $sourceDir"; exit 0 }

$failed = 0
foreach ($file in $files) {
    $out = Join-Path $outDir ($file.BaseName + '.glb')
    $lines = & $blender --background --factory-startup $file.FullName --python $script -- $out 2>&1 |
        ForEach-Object { "$_" } | Where-Object { $_ -match '^(RESULT|WARNING|ERROR)' }
    if ($LASTEXITCODE -ne 0 -or -not ($lines -match '^RESULT')) { $failed++ }
    $lines | ForEach-Object {
        $color = if ($_ -match '^ERROR') { 'Red' } elseif ($_ -match '^WARNING') { 'Yellow' } else { 'Green' }
        Write-Host $_ -ForegroundColor $color
    }
}
if ($failed) { Write-Host "$failed export(s) failed." -ForegroundColor Red; exit 1 }

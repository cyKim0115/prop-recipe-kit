<#
.SYNOPSIS
  Check what prop-recipe-kit needs on this machine: Blender (and its version), Python, uv. Read-only.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\check.ps1
#>
[CmdletBinding()]
param([string]$Blender = $env:BLENDER_EXE, [string]$Expect = '5.2')

$ErrorActionPreference = 'Continue'
$kit = Split-Path -Parent $PSScriptRoot
$ok = $true
function Show-Line([string]$name, [bool]$good, [string]$detail) {
    $tag = if ($good) { 'OK     ' } else { 'MISSING' }
    Write-Output ("[{0}] {1,-10} {2}" -f $tag, $name, $detail)
}

Write-Output "prop-recipe-kit at $kit"

# Blender: argument/env first, then standard install folders (newest), then PATH
$exe = $null
if ($Blender -and (Test-Path $Blender)) { $exe = $Blender }
if (-not $exe) {
    $roots = @($env:ProgramFiles, $env:ProgramW6432, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
    $found = foreach ($r in $roots) { Get-ChildItem -Path (Join-Path $r 'Blender Foundation') -Filter 'blender.exe' -Recurse -Depth 2 -ErrorAction SilentlyContinue }
    $exe = $found | Sort-Object { if ($_.DirectoryName -match 'Blender\s*(\d+)\.(\d+)') { [int]$matches[1] * 1000 + [int]$matches[2] } else { 0 } } -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $exe) { $cmd = Get-Command blender -ErrorAction SilentlyContinue; if ($cmd) { $exe = $cmd.Source } }
if ($exe) {
    $ver = (& $exe --version 2>$null | Select-Object -First 1)
    $match = $ver -like "Blender $Expect*"
    Show-Line 'blender' $true "$ver | $exe"
    if (-not $match) { Write-Output "          note: kit verified with Blender $Expect.x; other versions print a warning when building" }
} else {
    Show-Line 'blender' $false 'not found: install Blender or set BLENDER_EXE'
    $ok = $false
}

$py = Get-Command python -ErrorAction SilentlyContinue
if ($py) { Show-Line 'python' $true ((& python --version 2>&1) + ' | ' + $py.Source) } else { Show-Line 'python' $false 'needed to run blender/scripts/blender_run.py (3.10+)'; $ok = $false }

$uv = Get-Command uv -ErrorAction SilentlyContinue
if ($uv) { Show-Line 'uv' $true ((& uv --version 2>&1) + ' (optional: only for a Blender MCP server)') } else { Show-Line 'uv' $true 'not installed (optional: only for a Blender MCP server)' }

$pkg = Join-Path $kit 'unity\com.proprecipe.kit\package.json'
Show-Line 'unity-pkg' (Test-Path $pkg) $pkg

if ($ok) { Write-Output 'ready'; exit 0 } else { Write-Output 'not ready'; exit 1 }

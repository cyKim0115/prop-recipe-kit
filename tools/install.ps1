<#
.SYNOPSIS
  Attach prop-recipe-kit to one Unity project: copy (or link) the 3 agent skills into <Project>\.claude\skills
  and, optionally, copy the Unity package into <Project>\Packages. Use -WhatIf first.
.DESCRIPTION
  - Skills: copied by default; -Link makes directory junctions instead (the kit stays the single source).
    Each installed skill gets kit_root.txt with this kit's path (skills read it to find the scripts).
  - Unity package: not touched unless -CopyPackage. Without it the script prints the two ways to add it
    (manifest "file:" reference, or an embedded copy under Packages\). Editing manifest.json is left to you.
  - Nothing global is changed (no user-level skills, no environment variables, no MCP registration).
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Project D:\repo\MyGame -WhatIf
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Project D:\repo\MyGame -Link
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [switch]$Link,
    [switch]$CopyPackage,
    [string[]]$Skills = @('prop-blender-headless', 'prop-recipe-from-image', 'prop-kit-new-project')
)

$ErrorActionPreference = 'Stop'
$kit = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $Project 'Assets'))) { throw "Not a Unity project (no Assets folder): $Project" }
$skillRoot = Join-Path $Project '.claude\skills'

foreach ($name in $Skills) {
    $src = Join-Path $kit "skills\$name"
    $dst = Join-Path $skillRoot $name
    if (-not (Test-Path $src)) { Write-Warning "skill not found in kit: $name"; continue }
    if (Test-Path $dst) { Write-Output "skip (already exists): $dst"; continue }
    if ($Link) {
        if ($PSCmdlet.ShouldProcess($dst, "junction -> $src")) {
            New-Item -ItemType Directory -Force -Path $skillRoot | Out-Null
            New-Item -ItemType Junction -Path $dst -Target $src | Out-Null
            Set-Content -Path (Join-Path $src 'kit_root.txt') -Value $kit -Encoding ascii
            Write-Output "linked: $dst -> $src"
        }
    } else {
        if ($PSCmdlet.ShouldProcess($dst, "copy skill from $src and write kit_root.txt")) {
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Exclude 'kit_root.txt'
            Set-Content -Path (Join-Path $dst 'kit_root.txt') -Value $kit -Encoding ascii
            Write-Output "copied: $dst"
        }
    }
}

$pkgSrc = Join-Path $kit 'unity\com.proprecipe.kit'
$pkgDst = Join-Path $Project 'Packages\com.proprecipe.kit'
if ($CopyPackage) {
    if (Test-Path $pkgDst) { Write-Output "skip (already exists): $pkgDst" }
    elseif ($PSCmdlet.ShouldProcess($pkgDst, "copy Unity package (embedded package, no manifest change)")) {
        Copy-Item -Path $pkgSrc -Destination $pkgDst -Recurse
        Write-Output "copied package: $pkgDst"
    }
} else {
    $uri = 'file:' + ($pkgSrc -replace '\\', '/')
    Write-Output ''
    Write-Output 'Unity package (choose one, not done by this script):'
    Write-Output "  a) Packages\manifest.json  ->  `"com.proprecipe.kit`": `"$uri`""
    Write-Output "  b) rerun with -CopyPackage  ->  embedded copy at $pkgDst"
}
Write-Output ''
Write-Output 'Next: copy adapters\_template into the project and fill it (see skill prop-kit-new-project).'

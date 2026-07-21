<#
.SYNOPSIS
  Installs the hid-bridge Claude Code skill for the current user.

.DESCRIPTION
  Copies skill/hid-bridge/SKILL.md into ~/.claude/skills/hid-bridge/, then populates
  bin/hidbridge.exe either from a local build (dist/hidbridge.exe, if present) or by
  downloading the latest GitHub Release asset.

.EXAMPLE
  ./scripts/install.ps1
#>

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$skillSrc   = Join-Path $repoRoot 'skill\hid-bridge\SKILL.md'
$localBuild = Join-Path $repoRoot 'dist\hidbridge.exe'
$skillDest  = Join-Path $env:USERPROFILE '.claude\skills\hid-bridge'
$binDest    = Join-Path $skillDest 'bin'

New-Item -ItemType Directory -Force -Path $binDest | Out-Null
Copy-Item -Path $skillSrc -Destination $skillDest -Force

if (Test-Path $localBuild) {
    Write-Host "Using local build: $localBuild"
    Copy-Item -Path $localBuild -Destination (Join-Path $binDest 'hidbridge.exe') -Force
}
else {
    Write-Host 'No local build found, downloading latest release from GitHub...'
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/PageMastr/AIID/releases/latest'
    $asset = $release.assets | Where-Object { $_.name -eq 'hidbridge.exe' } | Select-Object -First 1
    if (-not $asset) { throw 'No hidbridge.exe asset found on the latest GitHub release.' }
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile (Join-Path $binDest 'hidbridge.exe')
}

Write-Host "Installed to $skillDest"
Write-Host 'Start (or restart) a Claude Code session for the hid-bridge skill to be picked up.'

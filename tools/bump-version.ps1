#requires -Version 5.1
<#
.SYNOPSIS
    Bumps the ClipboardApp semantic version in exactly one place and keeps the
    documentation release matrix in sync.

.DESCRIPTION
    The version lives only in /Directory.Build.props. This script:

      1. Parses the current MAJOR.MINOR.PATCH.
      2. Applies the requested part (major | minor | patch) or an explicit
         -Set "x.y.z".
      3. Resets the lower parts to zero (1.4.2 + minor => 1.5.0).
      4. Rewrites Directory.Build.props.
      5. Replaces the previous version string in README.md, docs/VISUAL_GUIDE.html
         and docs/HANDOFF.md so downloads, badges and handoff notes never point
         at an old build.

    Semantic versioning policy (also documented in AGENTS.md section 5):
      major = breaking / architectural change
      minor = moderate feature or non-breaking behaviour change
      patch = minor improvement, small fix, polish, docs correction

.PARAMETER Part
    Which component to increment: major, minor, or patch.

.PARAMETER Set
    Explicit version to apply instead of incrementing (e.g. -Set 4.0.0).

.PARAMETER DryRun
    Print the resulting version without writing any files.

.EXAMPLE
    .\tools\bump-version.ps1 -Part minor

.EXAMPLE
    .\tools\bump-version.ps1 -Part patch -DryRun
#>
param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Part = "patch",

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Set,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repoRoot "Directory.Build.props"

if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at $propsPath"
}

$propsText = Get-Content $propsPath -Raw
$match = [regex]::Match($propsText, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) {
    throw "Could not find <Version>MAJOR.MINOR.PATCH</Version> in Directory.Build.props"
}

$oldMajor = [int]$match.Groups[1].Value
$oldMinor = [int]$match.Groups[2].Value
$oldPatch = [int]$match.Groups[3].Value
$oldVersion = "$oldMajor.$oldMinor.$oldPatch"

if ($Set) {
    $newVersion = $Set
}
else {
    switch ($Part) {
        "major" { $newVersion = "$($oldMajor + 1).0.0" }
        "minor" { $newVersion = "$oldMajor.$($oldMinor + 1).0" }
        default { $newVersion = "$oldMajor.$oldMinor.$($oldPatch + 1)" }
    }
}

if ($newVersion -eq $oldVersion) {
    Write-Host "Version is already $newVersion. Nothing to do." -ForegroundColor Yellow
    return
}

Write-Host "ClipboardApp version: $oldVersion -> $newVersion" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "(dry run: no files written)" -ForegroundColor Yellow
    return
}

# 1. Rewrite the single source of truth.
$updated = $propsText -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
Set-Content -Path $propsPath -Value $updated -Encoding UTF8 -NoNewline
Write-Host "  updated Directory.Build.props" -ForegroundColor Green

# 2. Keep the release matrix in sync with the new version string.
$docTargets = @(
    (Join-Path $repoRoot "README.md"),
    (Join-Path $repoRoot "docs\VISUAL_GUIDE.html"),
    (Join-Path $repoRoot "docs\HANDOFF.md")
)
foreach ($doc in $docTargets) {
    if (-not (Test-Path $doc)) { continue }
    $text = Get-Content $doc -Raw
    if ($text -notmatch [regex]::Escape($oldVersion)) { continue }
    $text = $text.Replace($oldVersion, $newVersion)
    Set-Content -Path $doc -Value $text -Encoding UTF8 -NoNewline
    Write-Host "  updated $(Split-Path -Leaf $doc)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Add a '$newVersion' section to docs/CHANGELOG.md (what changed and why)."
Write-Host "  2. Run .\tools\build_releases.ps1                 (portable only)"
Write-Host "     or .\tools\build_releases.ps1 -BuildInstaller  (portable + setup)"
Write-Host "  3. Publish the release so GitHub Actions attaches the portable .exe and SHA256SUMS.txt."

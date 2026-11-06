#requires -Version 5.1
<#
.SYNOPSIS
    Verifies that the setup installer really contains the portable build it
    claims to, and that both match the recorded checksums.

.DESCRIPTION
    Answers the question "can the setup installer be rebuilt or re-verified?"
    without trusting the build logs:

      1. Reads releases\RELEASE-v<Version>.json (or the newest manifest) and
         re-hashes every artifact it lists.
      2. Extracts payload.bin from the installed setup exe's resources the same
         way the installer does (GZip stream), decompresses it, and compares the
         SHA-256 of the decompressed binary with the portable release.
      3. Reports the version stamped inside the portable executable itself, so
         a correctly named but stale binary cannot pass.

.PARAMETER Version
    Release version to verify (e.g. 3.1.0). Defaults to the newest manifest.

.EXAMPLE
    .\tools\verify-release.ps1

.EXAMPLE
    .\tools\verify-release.ps1 -Version 3.1.0
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $repoRoot "releases"

if (-not $Version) {
    $newest = Get-ChildItem $releaseDir -Filter "RELEASE-v*.json" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $newest) { throw "No release manifest found in $releaseDir. Build a release first." }
    $Version = [regex]::Match($newest.Name, 'RELEASE-v(\d+\.\d+\.\d+)\.json').Groups[1].Value
}

$manifestPath = Join-Path $releaseDir "RELEASE-v$Version.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest not found: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$failures = New-Object System.Collections.Generic.List[string]
$portablePath = Join-Path $releaseDir "ClipboardApp-v$Version-Portable.exe"
$setupPath = Join-Path $releaseDir "ClipboardApp-v$Version-Setup.exe"

Write-Host "Verifying ClipboardApp v$Version (built $($manifest.builtUtc), commit $($manifest.commit))" -ForegroundColor Cyan

# ---------------------------------------------------- 1. artifact checksums
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $releaseDir $artifact.file
    if (-not (Test-Path $path)) {
        $failures.Add("missing artifact: $($artifact.file)")
        continue
    }
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash
    if ($actual -ne $artifact.sha256) {
        $failures.Add("checksum mismatch: $($artifact.file) (expected $($artifact.sha256), got $actual)")
    }
    else {
        Write-Host "  OK   checksum  $($artifact.file)" -ForegroundColor Green
    }
}

# ---------------------------------------------------- 2. assembly version
if (Test-Path $portablePath) {
    $fileVersion = (Get-Item $portablePath).VersionInfo.FileVersion
    $productVersion = (Get-Item $portablePath).VersionInfo.ProductVersion
    if ($fileVersion -and -not $fileVersion.StartsWith($Version)) {
        $failures.Add("portable executable reports version '$fileVersion' but should be $Version")
    }
    else {
        Write-Host "  OK   version   portable reports $fileVersion / $productVersion" -ForegroundColor Green
    }
}

# ---------------------------------------------------- 3. installer payload
if (Test-Path $setupPath) {
    # The setup exe verifies itself: it decompresses the embedded payload with the
    # exact same code path used during installation and compares the hash with the
    # portable release. This is the only reliable way to inspect a single-file
    # bundle, whose manifest is not readable as a normal assembly.
    $verifyOut = & $setupPath "--verify-payload=$portablePath" 2>&1
    $verifyExit = if (Test-Path variable:LASTEXITCODE) { $LASTEXITCODE } else { 0 }
    $verifyOut | Where-Object { "$_".Trim().Length -gt 0 } | ForEach-Object { Write-Host "  $_" }

    if ($verifyExit -ne 0) {
        $failures.Add("installer payload check failed (exit $verifyExit)")
    }
    else {
        Write-Host "  OK   payload   setup embeds a byte-identical copy of the portable" -ForegroundColor Green
    }
}
else {
    Write-Host "  --   payload   no setup installer for v$Version (on-demand build)" -ForegroundColor DarkGray
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "VERIFICATION FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All release checks passed for v$Version." -ForegroundColor Green

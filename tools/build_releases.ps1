#requires -Version 5.1
<#
.SYNOPSIS
    ClipboardApp release pipeline: verify, publish the portable binary, and
    (on demand) rebuild the setup installer from that exact binary.

.DESCRIPTION
    Portable by default. The setup installer is a one-time packaging step and is
    only built when -BuildInstaller is passed.

    Steps:
      1. Read the version from Directory.Build.props (single source of truth).
      2. dotnet test -c Release and enforce a minimum test count, so a shrinking
         suite can never ship silently.
      3. Publish the self-contained single-file portable app.
      4. Stage releases\ClipboardApp-v<Version>-Portable.exe (+ repo-root copy).
      5. Optionally: regenerate payload.bin from the portable just published,
         publish the setup exe, stage releases\ClipboardApp-v<Version>-Setup.exe.
      6. Write a machine-readable releases\SHA256SUMS.txt plus a human-readable
         releases\RELEASE-v<Version>.json manifest recording the exact hashes,
         the version, and the git commit the artifacts were built from.

.PARAMETER BuildInstaller
    Also build the on-demand setup installer from the freshly built portable.

.PARAMETER MinTests
    Minimum number of tests that must run for packaging to proceed.

.EXAMPLE
    .\tools\build_releases.ps1

.EXAMPLE
    .\tools\build_releases.ps1 -BuildInstaller
#>
param(
    [switch]$BuildInstaller,
    [int]$MinTests = 90
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {

# ---------------------------------------------------------------- 0. version
$propsPath = Join-Path $repoRoot "Directory.Build.props"
$versionMatch = [regex]::Match((Get-Content $propsPath -Raw), '<Version>(\d+\.\d+\.\d+)</Version>')
if (-not $versionMatch.Success) { throw "Could not read the version from Directory.Build.props." }
$Version = $versionMatch.Groups[1].Value

$gitCommit = "unknown"
try { $gitCommit = (git rev-parse --short HEAD 2>$null) } catch { }

Write-Host "=========================================================="
Write-Host "  ClipboardApp Release Pipeline: v$Version  (commit $gitCommit)"
Write-Host "  Mode: $(if ($BuildInstaller) { 'Portable + Setup Installer' } else { 'Portable only (default)' })"
Write-Host "=========================================================="

# ---------------------------------------------------------------- 1. tests
Write-Host "`n[1/5] Running automated tests..." -ForegroundColor Cyan
$testOutput = & dotnet test --nologo -c Release 2>&1
$testOutput | Select-Object -Last 4 | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed. Aborting release build."
}

$passedMatch = [regex]::Match(($testOutput -join "`n"), 'Passed:\s+(\d+)')
if (-not $passedMatch.Success) { throw "Could not parse the test count from dotnet test output." }
$passed = [int]$passedMatch.Groups[1].Value
if ($passed -lt $MinTests) {
    throw "Only $passed tests ran but at least $MinTests are required. Refusing to package a shrunken suite."
}
Write-Host "  $passed tests passed (minimum $MinTests)." -ForegroundColor Green

# ---------------------------------------------------------------- 2. portable
Write-Host "`n[2/5] Publishing portable single-file executable..." -ForegroundColor Cyan
& dotnet publish src/ClipboardApp.WinForms/ClipboardApp.WinForms.csproj `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed." }

$releaseDir = Join-Path $repoRoot "releases"
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

# ---------------------------------------------------------------- 3. stage
Write-Host "`n[3/5] Staging versioned deliverables..." -ForegroundColor Cyan
$publishedExe = Join-Path $repoRoot "publish\ClipboardApp.exe"
$portableRelease = Join-Path $releaseDir "ClipboardApp-v$Version-Portable.exe"
Copy-Item $publishedExe $portableRelease -Force
Write-Host "  $([System.IO.Path]::GetFileName($portableRelease))"

$deliverables = @($portableRelease)
$setupRelease = $null

# ---------------------------------------------------------------- 4. installer
if ($BuildInstaller) {
    Write-Host "`n[4/5] Rebuilding the setup installer from the portable just published..." -ForegroundColor Cyan

    # pack.ps1 compresses publish\ClipboardApp.exe, which is exactly the
    # binary staged above, so the installer payload can never drift from the
    # portable release that shipped alongside it.
    & (Join-Path $PSScriptRoot "pack.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Payload compression failed." }

    & dotnet publish src/ClipboardApp.Installer/ClipboardApp.Installer.csproj `
        -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o publish/installer `
        -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw "Installer publish failed." }

    $setupRelease = Join-Path $releaseDir "ClipboardApp-v$Version-Setup.exe"
    Copy-Item (Join-Path $repoRoot "publish\installer\ClipboardApp-Setup.exe") $setupRelease -Force
    $deliverables += $setupRelease
    Write-Host "  $([System.IO.Path]::GetFileName($setupRelease))"
}
else {
    Write-Host "`n[4/5] Skipping the on-demand setup installer." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- 5. checksums
Write-Host "`n[5/5] Writing checksums and the release manifest..." -ForegroundColor Cyan

$hashes = @{}
foreach ($file in $deliverables) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    $hashes[[System.IO.Path]::GetFileName($file)] = $hash
}

# Machine-readable, `sha256sum -c` compatible (relative file names only).
$sumLines = foreach ($file in $deliverables) {
    "{0}  {1}" -f $hashes[[System.IO.Path]::GetFileName($file)], [System.IO.Path]::GetFileName($file)
}
Set-Content -Path (Join-Path $releaseDir "SHA256SUMS.txt") -Value $sumLines -Encoding UTF8

$manifest = [ordered]@{
    version   = $Version
    commit    = $gitCommit
    builtUtc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    tests     = $passed
    artifacts = @(
        foreach ($file in $deliverables) {
            [ordered]@{
                file   = [System.IO.Path]::GetFileName($file)
                bytes  = (Get-Item $file).Length
                sha256 = $hashes[[System.IO.Path]::GetFileName($file)]
            }
        }
    )
}
$manifestPath = Join-Path $releaseDir "RELEASE-v$Version.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
Write-Host "  $(Split-Path -Leaf $manifestPath)"

Write-Host "`nDeliverables:" -ForegroundColor Green
Write-Host "  Portable:  $(Split-Path -Leaf $portableRelease)  ($([math]::Round((Get-Item $portableRelease).Length / 1MB, 2)) MB)"
if ($setupRelease) {
    Write-Host "  Installer: $(Split-Path -Leaf $setupRelease)  ($([math]::Round((Get-Item $setupRelease).Length / 1MB, 2)) MB)"
}
Write-Host "  SHA-256:"
foreach ($name in $hashes.Keys) { Write-Host "    $($hashes[$name])  $name" }
Write-Host "=========================================================="

}
finally {
    Pop-Location
}

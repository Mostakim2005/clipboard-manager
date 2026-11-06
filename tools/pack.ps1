#requires -Version 5.1
<#
.SYNOPSIS
    Compresses publish\ClipboardApp.exe into the installer payload.

.DESCRIPTION
    The installer embeds a GZip-compressed copy of the portable executable as
    src\ClipboardApp.Installer\Resources\payload.bin. This script always runs
    after the portable has been published by tools/build_releases.ps1, so the
    payload can never be older than the portable release it accompanies.

    The SHA-256 of both the source and the payload are printed so the packaging
    log always records which portable went into which installer.
#>
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $repoRoot "publish\ClipboardApp.exe"
$destDir = Join-Path $repoRoot "src\ClipboardApp.Installer\Resources"
$destPath = Join-Path $destDir "payload.bin"

if (-not (Test-Path $srcPath)) {
    throw "Source file $srcPath not found. Publish the WinForms project first (tools/build_releases.ps1 does this)."
}

New-Item -ItemType Directory -Path $destDir -Force | Out-Null

Write-Host "Compressing publish\ClipboardApp.exe -> Resources\payload.bin ..."
$inStream = [System.IO.File]::OpenRead($srcPath)
try {
    $outStream = [System.IO.File]::Create($destPath)
    try {
        $gzipStream = New-Object System.IO.Compression.GZipStream($outStream, [System.IO.Compression.CompressionLevel]::Optimal)
        try { $inStream.CopyTo($gzipStream) }
        finally { $gzipStream.Dispose() }
    }
    finally { $outStream.Dispose() }
}
finally { $inStream.Dispose() }

$srcInfo = Get-Item $srcPath
$destInfo = Get-Item $destPath
Write-Host ("Compression complete: {0:N2} MB -> {1:N2} MB" -f ($srcInfo.Length / 1MB), ($destInfo.Length / 1MB))
Write-Host ("  portable SHA-256: {0}" -f (Get-FileHash $srcPath -Algorithm SHA256).Hash)
Write-Host ("  payload  SHA-256: {0}" -f (Get-FileHash $destPath -Algorithm SHA256).Hash)

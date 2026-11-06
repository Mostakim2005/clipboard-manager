#requires -Version 5.1
<#
.SYNOPSIS
    Repairs double-encoded UTF-8 ("mojibake") in text files.

.DESCRIPTION
    A file that was UTF-8 but got read as Windows-1252 and re-saved as UTF-8
    contains sequences like "Ã°Å¸â€œ" instead of "📥". This is HANDOFF.md landmine
    #16 and it silently corrupts emoji, curly quotes, box-drawing and dashes.

    Repair = reverse the damage: map every character back to the Windows-1252
    byte that produced it, then decode those bytes as UTF-8.

    A whole file is only rewritten when the repair strictly reduces mojibake
    markers and introduces no U+FFFD replacement characters, so clean files and
    files that merely contain legitimate accented text are left untouched.

.PARAMETER Path
    Files to repair. Defaults to every markdown file in the repo and docs/.

.PARAMETER DryRun
    Report what would change without writing.

.EXAMPLE
    .\tools\fix-encoding.ps1 -DryRun

.EXAMPLE
    .\tools\fix-encoding.ps1 -Path README.md
#>
param(
    [string[]]$Path,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $Path) {
    $Path = @()
    $Path += Get-ChildItem $repoRoot -Filter *.md -File | ForEach-Object { $_.FullName }
    $Path += Get-ChildItem (Join-Path $repoRoot "docs") -Filter *.md -File -Recurse |
        ForEach-Object { $_.FullName }
}

# Windows-1252 is the code page Windows uses when it misreads UTF-8. Characters
# 0x80-0x9F are the give-away: they decode to €, ‚, ƒ, „, …, †, ‡, ˆ, ‰, Š, ‹, Œ, Ž,
# ‘, ’, “, ”, •, –, —, ˜, ™, š, ›, œ, ž, Ÿ — which is exactly what we see.
$cp1252 = [System.Text.Encoding]::GetEncoding(1252)
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)

function Get-MojibakeScore {
    param([string]$Text)
    # Count characters that only appear when UTF-8 bytes are read as 1252.
    return ([regex]::Matches($Text, '[\u00C2-\u00F4][\u0080-\u00BF\u0152-\u0178\u2013-\u203A\u02C6\u02DC\u20AC]')).Count
}

function Repair-Text {
    param([string]$Text)

    if ((Get-MojibakeScore -Text $Text) -eq 0) { return $null }

    try {
        $bytes = $cp1252.GetBytes($Text)
        $repaired = $utf8Strict.GetString($bytes)   # throws if the bytes are not valid UTF-8
    }
    catch {
        return $null
    }

    if ($repaired.Contains([char]0xFFFD)) { return $null }

    $before = Get-MojibakeScore -Text $Text
    $after = Get-MojibakeScore -Text $repaired
    if ($after -lt $before) { return $repaired }

    return $null
}

$fixed = 0
foreach ($file in $Path) {
    if (-not (Test-Path $file)) { continue }
    $name = [System.IO.Path]::GetFileName($file)

    $original = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
    $repaired = Repair-Text -Text $original

    if (-not $repaired) {
        Write-Host "  ok    $name" -ForegroundColor DarkGray
        continue
    }

    $before = Get-MojibakeScore -Text $original
    $after = Get-MojibakeScore -Text $repaired

    if ($DryRun) {
        Write-Host "  FIX   $name  ($before -> $after mojibake markers)" -ForegroundColor Yellow
        $fixed++
        continue
    }

    $encoder = New-Object System.Text.UTF8Encoding($true)  # UTF-8 with BOM
    [System.IO.File]::WriteAllText($file, $repaired, $encoder)
    Write-Host "  fixed $name  ($before -> $after mojibake markers)" -ForegroundColor Green
    $fixed++
}

if ($fixed -eq 0) {
    Write-Host "No encoding problems found." -ForegroundColor Green
}
elseif ($DryRun) {
    Write-Host "`n$fixed file(s) would be repaired. Re-run without -DryRun to apply." -ForegroundColor Yellow
}
else {
    Write-Host "`nRepaired $fixed file(s)." -ForegroundColor Green
}

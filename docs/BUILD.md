[Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Decisions & Traps](DECISIONS_AND_TRAPS.md) · [Visual Guide](VISUAL_GUIDE.html) · [AI Bootstrap](../AI_BOOTSTRAP.md)

# Build & Packaging Pipeline

How ClipboardApp is compiled, tested, and packaged. Portable first; installer on demand.

---

## Prerequisites

- **OS:** Windows 10/11 (the app and installer target `net8.0-windows` / `win-x64`)
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Shell:** PowerShell 7+ or Windows PowerShell 5.1

---

## Quick Build And Test

```powershell
dotnet build ClipboardApp.sln -c Release      # expect 0 warnings, 0 errors
dotnet test ClipboardApp.sln -c Release       # expect 116/116
dotnet run --project src/ClipboardApp.WinForms
```

---

## Release Packaging

`tools/build_releases.ps1` reads the version from `Directory.Build.props`, so there is no `-Version` argument to pass.

### Portable only (default)

```powershell
.\tools\build_releases.ps1
```

1. Runs `dotnet test -c Release` and enforces a minimum test count (default 90). A shrunken suite halts packaging.
2. Publishes `src/ClipboardApp.WinForms` as a self-contained single-file executable.
3. Stages `releases/ClipboardApp-v<Version>-Portable.exe`.
4. Writes `releases/SHA256SUMS.txt` (sha256sum-compatible) and `releases/RELEASE-v<Version>.json` with version, commit, test count, byte sizes, and hashes.

### Portable plus setup installer

```powershell
.\tools\build_releases.ps1 -BuildInstaller
```

Adds:

5. `tools/pack.ps1` compresses the **just-published** `publish/ClipboardApp.exe` into `payload.bin`, so the installer payload cannot drift from the portable.
6. Publishes `src/ClipboardApp.Installer` and stages `releases/ClipboardApp-v<Version>-Setup.exe`.
7. Extends the checksum manifest to cover both artifacts.

### Verify a release

```powershell
.\tools\verify-release.ps1
```

Extracts and decompresses `payload.bin` from the installer, proves it is byte-identical to the shipped portable, re-checks every SHA-256, and confirms the version stamped inside the executable matches `Directory.Build.props`.

> **Run from the repository root.** `tools/pack.ps1` resolves its payload paths through the process working directory.

---

## Manual Compilation

### Portable executable

```powershell
dotnet publish src/ClipboardApp.WinForms/ClipboardApp.WinForms.csproj `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish
```

Output: `publish/ClipboardApp.exe` — rename to `ClipboardApp-v<Version>-Portable.exe`.

### Setup installer

```powershell
.\tools\pack.ps1

dotnet publish src/ClipboardApp.Installer/ClipboardApp.Installer.csproj `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish/installer
```

Output: `publish/installer/ClipboardApp-Setup.exe`.

---

## Cloud Builds (GitHub Actions)

`.github/workflows/build.yml` runs on every push and pull request to `main`, and on any `v*` tag:

| Step | What it does |
| :--- | :--- |
| Restore + build | `dotnet build ClipboardApp.sln -c Release` |
| Test | `dotnet test ClipboardApp.sln -c Release` with TRX results uploaded as an artifact |
| Publish | Single-file win-x64 portable, renamed `ClipboardApp-v<Version>-Portable.exe` |
| Checksums | Generates `SHA256SUMS.txt` for the artifact |
| Upload | Portable `.exe` and checksums as a workflow artifact |
| Release | On a `v*` tag, creates the GitHub Release if missing and uploads the artifacts |

Binaries are never committed. `releases/`, `publish/`, `dist/`, and `*.exe` are gitignored.

---

## Version Bumping

```powershell
.\tools\bump-version.ps1 -Part patch     # 3.1.0 -> 3.1.1
.\tools\bump-version.ps1 -Part minor     # 3.1.1 -> 3.2.0
.\tools\bump-version.ps1 -Part major     # 3.2.0 -> 4.0.0
.\tools\bump-version.ps1 -Set 3.5.0      # explicit
.\tools\bump-version.ps1 -Part patch -DryRun
```

Semantic policy: **major** for breaking or architectural change, **minor** for features or non-breaking behaviour change, **patch** for fixes, polish, and documentation corrections. Add a `docs/CHANGELOG.md` section after bumping.

---

## Runtime Notes

- Expected working set after launch: **60–67 MB**.
- The portable executable is large (~158 MB) because it carries the complete self-contained .NET runtime.
- Crash log: `%LocalAppData%\ClipboardApp\crash.log`.
- Data: `%LocalAppData%\ClipboardApp\data.db` (WAL mode, DPAPI-encrypted payload columns).

---

## Troubleshooting

- **File in use during build or copy** — another instance is running:
  ```powershell
  Get-Process -Name ClipboardApp -ErrorAction SilentlyContinue | Stop-Process -Force
  ```
- **Hotkey unavailable** — another app reserved the combination; reconfigure it in Settings.
- **Mojibake in docs or UI strings** — run `tools/fix-encoding.ps1`.
- **Installer verification fails** — rebuild with `-BuildInstaller`; never hand-edit `payload.bin`.

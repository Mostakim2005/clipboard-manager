# Project Operating Instructions (`AGENTS.md`)
> **Notice:** This project inherits user persona, confirmation gates, and documentation blueprints from the global master `AGENTS.md`. Do not duplicate those rules here.

## 1. Project Tech Stack & Environment
* **Runtime / Language:** C# on .NET 8 (`net8.0-windows`), WinForms, SQLite, Windows DPAPI
* **Framework / Host:** Windows Forms (WinForms) with native owner-drawn controls
* **Target OS / Build Output:** Windows 10/11 x64 portable `.exe` (default) & Setup Installer `.exe`

## 2. Mandatory Verification & Build Commands
* **Build (Release):** `dotnet build ClipboardApp.sln -c Release`
* **Test Suite:** `dotnet test ClipboardApp.sln -c Release`
* **Package Portable & Setup:** `powershell -ExecutionPolicy Bypass -File .\tools\build_releases.ps1 -BuildInstaller`
* **Verify Deliverables:** `powershell -ExecutionPolicy Bypass -File .\tools\verify-release.ps1`

## 3. Repository Directory Structure & Conventions
* `src/ClipboardApp.Core/` - Data models, SQLite repository, DPAPI encryption, API vault, clipboard monitoring, paste services.
* `src/ClipboardApp.WinForms/` - Windows Forms application entry, owner-drawn card lists, inspector, settings, themes.
* `src/ClipboardApp.Installer/` - Standalone self-verifying setup installer embedding GZip payload.
* `tests/ClipboardApp.Tests/` - xUnit test suite (storage, encryption, interop structs, UI contracts, filters).
* `tools/` - Release packaging, payload compression, version bumping, and release verification scripts.
* `docs/` - Core documentation suite (`PRODUCT.md`, `ARCHITECTURE.md`, `DECISIONS_AND_TRAPS.md`, `VISUAL_GUIDE.html`).
* `.github/workflows/` - Cloud CI/CD workflows producing portable and installer executables on version tags.

## 4. Repository-Specific Engineering Traps
* **TRAP 1:** Never set `ControlStyles.UserPaint` on `ModernCardList` (suppresses `WM_DRAWITEM` and blanks list).
* **TRAP 2:** `INPUT` / `InputUnion` struct size must remain exactly 40 bytes on x64 or `SendInput` silently fails.
* **TRAP 3:** Never reload snapshot on clipboard capture events; only confirmed copy triggers a coalesced FD refresh.
* **TRAP 4:** Version number is defined in exactly one place: `Directory.Build.props`. Never hardcode versions in project files or scripts.
* **TRAP 5:** Installer payload must be generated directly from the newly published portable binary and verified with byte-identity.
* **TRAP 6:** Releases and publish outputs are gitignored; never commit `.exe` binaries or `.db` files to Git.

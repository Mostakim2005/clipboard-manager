# Engineering Handoff — ClipboardApp

> **1-Line Summary:** The current state of the codebase, where every subsystem lives, what is verified, and what an incoming agent should do next.

**Last updated:** 2026-11-06 · **Version:** 3.1.0 · **Branch:** `main` · **Remote:** `github.com/Mostakim2005/clipboard-manager`

---

## 1. Current State

| Item | Status |
| :--- | :--- |
| Version | `3.1.0` in `Directory.Build.props` (single source) |
| Build | `dotnet build ClipboardApp.sln -c Release` — 0 warnings, 0 errors |
| Tests | `dotnet test ClipboardApp.sln -c Release` — 116/116 |
| Portable release | Built by `.github/workflows/build.yml`, attached to GitHub Releases |
| Installer | On demand via `tools/build_releases.ps1 -BuildInstaller`, verified by `tools/verify-release.ps1` |
| Documentation | Rebuilt around `PRODUCT.md`, `ARCHITECTURE.md`, `DECISIONS_AND_TRAPS.md`, `VISUAL_GUIDE.html` |
| Known blockers | None |

---

## 2. Source Map

### Entry points and shell

| File | Owns |
| :--- | :--- |
| `src/ClipboardApp.WinForms/Program.cs` | Single-instance mutex, DPI awareness, startup, crash log |
| `src/ClipboardApp.WinForms/AppVersion.cs` | Version as displayed in title, tray, Settings |
| `src/ClipboardApp.WinForms/HotkeyManager.cs` | Global hotkey registration and probing |
| `src/ClipboardApp.WinForms/Settings.cs` | All persisted user settings and their defaults |
| `src/ClipboardApp.WinForms/MainForm.cs` | Lifecycle, clipboard capture, snapshot, selection set |
| `src/ClipboardApp.WinForms/MainForm.Ui.cs` | Layout, theme application, view switch, split ratio |
| `src/ClipboardApp.WinForms/MainForm.Paste.cs` | Paste, batch actions, drag, target resolution, keyboard commands |
| `src/ClipboardApp.WinForms/MainForm.Dialogs.cs` | Context menu, editor, options, tags, transforms |
| `src/ClipboardApp.WinForms/MainForm.Filters.cs` | Date/type/app filters, select-all-filtered, delete-filtered |

### Controls and UI layer

| File | Owns |
| :--- | :--- |
| `Controls/ModernCardList.cs` | Native owner-drawn card list, selection gestures, drag arming, serials |
| `Controls/GridCardList.cs` | Tile grid renderer with the same command contract |
| `Controls/RichPreviewPanel.cs` | Inspector, transform preview staging, Save/Cancel/Undo |
| `Controls/FilterPillBar.cs` | Category pills |
| `Controls/ImageViewer` / `ImagePreview.cs` | Zoom and pan for image clips |
| `Controls/ThumbnailCache.cs` | LRU-bounded, aspect-true thumbnails |
| `Controls/HotkeyCaptureBox.cs` | Press-to-capture shortcut control |
| `Controls/TokenAutoComplete.cs` | Tag/alias suggestion popup |
| `Controls/TrayIconManager.cs` | Tray icon, balloon hints, menu |
| `Controls/DragDropHelper.cs` | Drop payload construction |
| `UI/UiStyler.cs` | Fluent/Acrylic/Classic/Native tokens applied to forms, menus, controls |
| `UI/ModernPalette.cs` | Colour palette |
| `UI/ContextMenuCommands.cs` | Command registry, order/hidden normalisation |
| `UI/ModernMenuRenderer.cs` | Rounded hover pill menu rendering |
| `Dialogs/*` | Options, entry editor, vault, key editor, target picker, context-menu customiser, message dialog |

### Core services

| File | Owns |
| :--- | :--- |
| `ClipboardMonitor.cs` | `WM_CLIPBOARDUPDATE` listener and self-write suppression |
| `Services/PasteService.cs` | Clipboard write, rich formats, `SendInput`, key-release wait, result model |
| `Services/BatchPasteService.cs` | Sequential paste plan |
| `Services/BatchCopyService.cs` | Merged payload formats (Plain / .env / JSON / CSV / Rich) |
| `Services/RichMergeService.cs` | CF_HTML/RTF with embedded PNGs |
| `Services/ClipboardContentIdentity.cs` | Type-prefixed SHA-256 identity |
| `Services/SearchService.cs` | Text and regex matching |
| `Services/FilterService.cs` | Date/type/app filter application |
| `Services/TagSuggestionService.cs` | Frequency-ranked vocabulary suggestions |
| `Services/TextTransformService.cs` | Case/URL/Base64/JSON/slug transforms |
| `Services/ExportImportService.cs` | JSON/CSV export and import, CSV injection defence |
| `Services/HotkeyParser.cs` | Shortcut parsing and formatting |
| `Services/ImageHelper.cs` | DIB decoding and image encoding |
| `Interop/NativeMethods.cs` | P/Invoke surface, `INPUT`, clipboard formats, DWM |
| `Interop/TargetWindowTracker.cs` | Foreground/root-window resolution and validation |
| `Interop/PasteTargetInspector.cs` | Target classification (rich vs plain) |

### Storage and security

| File | Owns |
| :--- | :--- |
| `Storage/SqliteClipboardRepository.cs` | Schema, `InsertEntry`, ordering, dedupe, pruning, encryption |
| `Storage/ApiKeyRepository.cs` | Encrypted API-key records |
| `Storage/DeduplicationMode.cs` | KeepPosition / MoveToTop / AllowDuplicates |
| `Security/DataProtector.cs` | DPAPI `Protect` / `Unprotect` |
| `Security/ApiVaultService.cs` | Vault unlock, timed window, idle lock, zeroing |
| `Security/ClipboardPrivacyFilter.cs` | Ignored apps, privacy atoms, sensitive detection |

### Installer and tools

| File | Owns |
| :--- | :--- |
| `src/ClipboardApp.Installer/Program.cs` | Setup entry, `--verify-payload` |
| `src/ClipboardApp.Installer/SetupEngine.cs` | Extraction, shortcuts, startup |
| `tools/build_releases.ps1` | Test floor, portable publish, installer, checksums, manifest |
| `tools/verify-release.ps1` | Payload byte-identity and checksum verification |
| `tools/pack.ps1` | Installer payload compression |
| `tools/bump-version.ps1` | One-command semantic version bump |
| `tools/fix-encoding.ps1` | Repairs Windows-1252 mojibake |

---

## 3. Tests

`tests/ClipboardApp.Tests/` — 116 tests across 21 files:

`ApiVaultTests`, `AppIconTests`, `BatchPasteTests`, `CardGestureTests`, `ClipboardIdentityTests`, `ContextMenuTests`, `FilterServiceTests`, `HotkeyParserTests`, `InteropLayoutTests`, `MainWindowTests`, `RichMergeTests`, `SearchServiceTests`, `SecurityTests`, `SelectionModelTests`, `StorageTests`, `TagSuggestionTests`, `TargetWindowTrackerTests`, `TransformAndCleanupTests`, `UiFeatureSmokeTests`, `UiSmokeTests`, `VersionTests`.

The suite is the release gate. `build_releases.ps1` enforces a minimum test count, so deleting tests fails the build rather than silently weakening coverage.

---

## 4. Verification

```powershell
dotnet build ClipboardApp.sln -c Release      # expect 0 warnings, 0 errors
dotnet test ClipboardApp.sln -c Release       # expect 116/116
.\tools\build_releases.ps1                    # portable + checksums
.\tools\build_releases.ps1 -BuildInstaller    # adds the setup wizard
.\tools\verify-release.ps1                    # payload byte-identity
```

Manual smoke checks that automated tests cannot cover:

1. Copy text in three different apps; confirm three cards with correct source labels.
2. Copy from a password manager; confirm nothing is captured.
3. Press `Enter` on a card with Notepad focused; confirm the text lands in Notepad.
4. Open the app with no valid target; confirm it copies and stays visible.
5. Arm drag, drop into Explorer; confirm a real file drop.
6. Toggle list/grid; confirm selection, filters, and shortcuts behave identically.
7. Transform a clip, then Cancel; confirm the stored clip is unchanged.

---

## 5. Open Work

| Item | Notes |
| :--- | :--- |
| Cloud sync | Wishlist only. Must be end-to-end encrypted and opt-in before any code. |
| Plugin/script actions | Wishlist. Needs a permissioned action runner; no remote code execution. |
| Local OCR | Wishlist. Local-only, explicit opt-in, derived text stored separately. |
| Per-app capture rules | Wishlist. Extends `ClipboardPrivacyFilter` beyond a simple ignore list. |
| Code-file size | `MainForm.Paste.cs` and `ModernCardList.cs` are large; split by responsibility when touched, not in a standalone refactor. |

---

## 6. Working Agreement

- Follow [`../AGENTS.md`](../AGENTS.md) and [`../AI_BOOTSTRAP.md`](../AI_BOOTSTRAP.md).
- Never create `INDEX.md`, never anchor documentation with line numbers.
- Update [`PRODUCT.md`](PRODUCT.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`DECISIONS_AND_TRAPS.md`](DECISIONS_AND_TRAPS.md), and [`VISUAL_GUIDE.html`](VISUAL_GUIDE.html) when behaviour, structure, or landmines change.
- Never commit `.env`, keys, `node_modules/`, `releases/`, `publish/`, or any `.exe`.
- Record every shipped change in [`CHANGELOG.md`](CHANGELOG.md).

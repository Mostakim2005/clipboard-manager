# ClipboardApp for Windows

A fast, keyboard-first, security-hardened clipboard manager for Windows, built with **C# (.NET 8)** and WinForms. It pairs a modern **Raycast / Fluent** inspired card interface with **Windows DPAPI encryption at rest**, target-verified pasting, and zero telemetry.

> **Documentation:** [Product Intent](docs/PRODUCT.md) · [Architecture](docs/ARCHITECTURE.md) · [Decisions & Traps](docs/DECISIONS_AND_TRAPS.md) · [Visual Guide](docs/VISUAL_GUIDE.html) · [Build](docs/BUILD.md) · [Changelog](docs/CHANGELOG.md). AI agents start at [`AI_BOOTSTRAP.md`](AI_BOOTSTRAP.md).

---

## Download

The portable single-file executable is the default deliverable. Builds are produced from source by GitHub Actions and attached to [Releases](https://github.com/Mostakim2005/clipboard-manager/releases) — binaries are never committed to this repository.

| Package | Target | Notes |
| :--- | :--- | :--- |
| **Portable** (default) | Windows 10/11 x64 | Zero install. No registry writes. Data stays in `%LocalAppData%\ClipboardApp\`. |
| **Setup Installer** | Windows 10/11 x64 | Built on demand. Payload is verified byte-identical to the portable. |
| **Checksums** | SHA-256 | `SHA256SUMS.txt` alongside each release. |

To build it yourself, see [`docs/BUILD.md`](docs/BUILD.md).

---

## What's New in 3.1.1

- **Versioning foundation.** One version source of truth in `Directory.Build.props`, stamped into the app, installer, and tests.
- **Verifiable installer.** `tools/verify-release.ps1` proves the setup payload is byte-identical to the shipped portable and re-checks every checksum.
- **Tray-first startup and hotkey toggle.** Starts quietly in the tray; one global shortcut shows or hides the window.
- **List and grid views.** Full-width tile grid without losing selection, filters, or keyboard behaviour.
- **Transform preview.** Text transforms stage a preview with Save, Cancel, and Undo instead of writing immediately.

---

## Features

### Capture and history

- **Stable snapshot history.** New copies are saved without reordering or reselecting the list you are reading. The FD (fresh data) refresh is ON by default and updates once after a confirmed copy, preserving selection and scroll position.
- **Stable serial numbers.** Cards show `#1`, `#2`, … from the current visual order; the first nine also drive `1`–`9` quick paste.
- **Multi-format capture.** Text, links, colors, file lists, HTML, RTF, and images, each tagged with its source application.
- **Type-prefixed SHA-256 identity.** Dedupe modes: Keep position (default), Move to top, Allow duplicates.
- **History limits.** Bound by item count and age, with automatic pruning.

### Finding and organising

- **Instant search.** Debounced text search, regular expressions, and `#tag` search.
- **Filters that stay put.** Category pills plus a permanent Date / Type / App row with **Select all filtered** and **Delete filtered** that touch only visible rows.
- **Tags, aliases, and groups.** Multiple per entry, rendered as pills, with frequency-ranked autocomplete.
- **Cards plus a rich inspector.** Draggable 50/50 split, code and JSON preview, color swatch with RGB/Hex, zoomable image.
- **List and grid parity.** One command surface (`MainForm.Accessor`) for both views.

### Pasting

- **Target-verified paste.** Writes the clipboard, verifies the target owns the foreground, then sends `Ctrl+V`. If no safe target exists, it copies and stays visible.
- **Opt-in double-click paste** (OFF by default). When enabled, every double-click opens an active-app chooser first.
- **Armed OLE drag and drop.** `✋ Drag` is off by default; dragging requires an explicit arm and disarms after one use or on blur.
- **Batch merge and sequential paste.** Merge checked cards as Plain, `.env`, JSON, CSV, or Rich — or paste them one after another.
- **Opt-in mixed text + image merge** (OFF by default) with a plain-target warning and automatic image deselection.

### Security

- **DPAPI encryption at rest** for all clipboard text and image blobs (`DataProtectionScope.CurrentUser`).
- **Password-manager protection.** Honors `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`, and `Clipboard Viewer Ignore`.
- **Sensitive data detection.** Luhn card validation, API-key patterns, masked cards, and configurable auto-expiry.
- **Encrypted API key vault.** Separate AES-256-GCM store behind a DPAPI-wrapped key, optional master password, timed auto-lock, and zeroed key material on close.
- **Per-app exclusion list** and single-instance enforcement.

### Interface

- **Switchable app-wide styling:** Fluent, Acrylic, Classic, or Native, applied to windows, menus, dialogs, and inline forms.
- **Persistent custom context menu.** Hide, show, and reorder commands; `Customize this menu...` can never be hidden.
- **Adaptive theme** that follows Windows light/dark mode, with manual override.
- **Keyboard and contrast.** Every primary action reachable by keyboard, WCAG AA contrast, visible focus, per-monitor V2 DPI.

### Keyboard shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Enter` | Paste the selected item (or merged batch when several are checked) |
| `Shift + Enter` | Paste as plain text |
| `1` – `9` | Quick-paste items #1 – #9 |
| `Ctrl + C` | Copy the selected item without pasting |
| `Ctrl + P` | Pin / unpin (pinned items are never auto-deleted) |
| `F2` | Edit title, aliases, and tags |
| `F5` | Reload the history snapshot |
| `Delete` | Delete the active item or all checked items |
| `Esc` | Clear multi-selection, or hide the window |

---

## Building

Prerequisites: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build ClipboardApp.sln -c Release
dotnet test ClipboardApp.sln -c Release
dotnet run --project src/ClipboardApp.WinForms
```

Portable publish, packaging, checksums, and installer verification are documented in [`docs/BUILD.md`](docs/BUILD.md).

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  UI (WinForms, owner-drawn cards, DWM chrome)               │
│  MainForm (.Ui / .Paste / .Dialogs / .Filters),             │
│  ModernCardList, GridCardList, RichPreviewPanel, UiStyler   │
├─────────────────────────────────────────────────────────────┤
│  Core services                                              │
│  ClipboardMonitor, PasteService, BatchPaste/BatchCopy,      │
│  RichMergeService, SearchService, FilterService,            │
│  ClipboardContentIdentity, TextTransformService             │
├─────────────────────────────────────────────────────────────┤
│  Storage and security                                       │
│  SqliteClipboardRepository, ApiKeyRepository,               │
│  DataProtector (DPAPI), ApiVaultService, PrivacyFilter      │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure                                             │
│  NativeMethods, TargetWindowTracker, HotkeyManager,         │
│  Settings, AppVersion                                       │
└─────────────────────────────────────────────────────────────┘
```

Full diagrams, flows, and extension recipes: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## Documentation

| File | Purpose |
| :--- | :--- |
| [`docs/PRODUCT.md`](docs/PRODUCT.md) | Plain-English product intent and active feature checklist |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Tech stack, diagrams, flows, security boundaries |
| [`docs/DECISIONS_AND_TRAPS.md`](docs/DECISIONS_AND_TRAPS.md) | What was rejected, who decided, and landmines |
| [`docs/VISUAL_GUIDE.html`](docs/VISUAL_GUIDE.html) | Self-contained interactive dashboard |
| [`docs/HANDOFF.md`](docs/HANDOFF.md) | Current engineering state and open work |
| [`docs/BUILD.md`](docs/BUILD.md) | Build and release pipeline |
| [`docs/FEATURES.md`](docs/FEATURES.md) | Detailed feature matrix |
| [`docs/UI_UX.md`](docs/UI_UX.md) | Interface behaviour and shortcuts |
| [`docs/CHANGELOG.md`](docs/CHANGELOG.md) | Version history |
| [`docs/APPROACH.md`](docs/APPROACH.md) | Security and stability audit log |

---

## License

**PolyForm Noncommercial 1.0.0** — see [`LICENSE`](LICENSE).

Free to use, modify, and share for any **noncommercial** purpose: personal study, hobby projects, research, experimentation, private entertainment, and use by charitable, educational, public research, public safety, health, environmental, or government organisations.

**Commercial use is not granted** by this license. If you want to use ClipboardApp in a commercial product or paid service, contact the author for a separate commercial license.

Third-party components keep their own licenses: MIT (Microsoft.Data.Sqlite, System.Memory, the .NET runtime, and the test tooling), Apache-2.0 (SQLitePCLRaw and xUnit), and public domain (SQLite itself). All are permissively licensed — there are no copyleft dependencies. The full attribution table is in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

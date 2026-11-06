# AI Agent Bootstrap & Context Router — ClipboardApp

> **Mandatory agent rule:** Do not read the whole repository or all documentation at once. Read this router (~1 page) first, then open only the document or dossier needed for the immediate task.

---

## 1. Orientation

- **Project:** ClipboardApp v3.1.0 — privacy-first Windows clipboard manager with stable snapshot history, tray-first startup, sensitive-content masking, List/Grid views and manual drag arming.
- **Stack:** C# / .NET 8 (`net8.0-windows`), Windows Forms, SQLite (`Microsoft.Data.Sqlite`), Windows DPAPI, Win32 P/Invoke. Tests: xUnit.
- **Default deliverable:** portable single-file `.exe`, built from source by GitHub Actions and attached to a GitHub Release. `releases/` and `publish/` are gitignored.
- **Installer:** built on demand only (`tools/build_releases.ps1 -BuildInstaller`) and must be proven byte-identical to the portable.

## 2. Where to Look

| Question | Open |
| :--- | :--- |
| What should the app do, and why? | [`docs/PRODUCT.md`](docs/PRODUCT.md) |
| How is it built? Layers, flows, data model | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| What did we reject, and what will break if touched? | [`docs/DECISIONS_AND_TRAPS.md`](docs/DECISIONS_AND_TRAPS.md) |
| Visual dashboard for a non-coder | [`docs/VISUAL_GUIDE.html`](docs/VISUAL_GUIDE.html) |
| Current engineering state and open work | [`docs/HANDOFF.md`](docs/HANDOFF.md) |
| Version history | [`docs/CHANGELOG.md`](docs/CHANGELOG.md) |
| Build/packaging commands | [`docs/BUILD.md`](docs/BUILD.md) |
| UI behaviour and shortcuts | [`docs/UI_UX.md`](docs/UI_UX.md) |
| Feature-by-feature detail | [`docs/FEATURES.md`](docs/FEATURES.md) |
| Security audit history | [`docs/APPROACH.md`](docs/APPROACH.md) |

**Grep before reading.** Header format is `### [TYPE: Name] | Aliases: ...` followed by a `> **1-Line Summary:**` line.

```bash
grep -n "^### \[" docs/
grep -A 1 "^### \[" docs/
```

## 3. Subsystem Routing

| Domain | Dossier | Primary code |
| :--- | :--- | :--- |
| Pasting, target focus, batch/drag | [`docs/subsystems/01-pasting-and-focus.md`](docs/subsystems/01-pasting-and-focus.md) | `src/ClipboardApp.Core/Services/PasteService.cs`, `BatchPasteService.cs`, `Interop/TargetWindowTracker.cs`, `src/ClipboardApp.WinForms/MainForm.Paste.cs` |
| Storage, identity, dedupe, ordering | [`docs/subsystems/02-multi-format-storage.md`](docs/subsystems/02-multi-format-storage.md) | `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs`, `Services/ClipboardContentIdentity.cs`, `Models/ClipboardEntry.cs` |
| Cards, selection, shortcuts, menus | [`docs/subsystems/03-ui-and-shortcuts.md`](docs/subsystems/03-ui-and-shortcuts.md) | `src/ClipboardApp.WinForms/Controls/ModernCardList.cs`, `GridCardList.cs`, `MainForm.Ui.cs`, `MainForm.Dialogs.cs` |
| Security, DPAPI, vault, privacy | [`docs/subsystems/04-security-and-privacy.md`](docs/subsystems/04-security-and-privacy.md) | `src/ClipboardApp.Core/Security/DataProtector.cs`, `ApiVaultService.cs`, `ClipboardPrivacyFilter.cs` |

## 4. Golden Invariants — Never Break

1. **Clean build, full suite.** Zero warnings and zero errors; all tests pass (currently **116/116**). A shrinking test suite blocks release.
2. **Stable snapshot.** Capture writes SQLite only. `MainForm.ReloadSnapshot` is the only full read path (startup, `F5`, reopen-when-dirty, tray clear, or the one coalesced FD refresh).
3. **Immutable ordering key.** Sort is `Pinned DESC, Seq DESC`. A recopy under `KeepPosition` updates `CopyCount`/`LastCopiedAt` only — it never moves or duplicates the row.
4. **Manual drag arming.** `RequireManualDragArm` defaults on. Disarm on deactivate, minimize, hide/close, after one drag, and when showing near the cursor. Double-click, Ctrl/Shift selection, checkbox and marquee must never depend on drag state.
5. **Paste hygiene.** Never inject `Ctrl+V` while a physical modifier or mouse button is down — call `PasteService.WaitForKeysReleased()` first. Verify the clipboard write and foreground ownership before sending keys. No valid target = copy and stay visible. Clear multi-selection only after success.
6. **One dedupe authority.** Only `SqliteClipboardRepository.InsertEntry` decides duplicates, using the type-prefixed SHA-256 from `ClipboardContentIdentity`.
7. **Format fidelity.** Do not flatten rich clips prematurely. Preserve `CF_UNICODETEXT`, `CF_TEXT`, `CF_DIB`, `PNG`, `CF_HDROP`. HTML writes a valid `CF_HTML` header with a plain-text fallback; RTF likewise.
8. **Encryption at rest.** `Content` and `ImageData` are DPAPI-encrypted (`CurrentUser`). The API vault is a separate AES-256-GCM store.
9. **Rendering hygiene.** `ModernCardList` is a native owner-drawn `ListBox` — never set `ControlStyles.UserPaint`. Cache and dispose fonts as fields; never allocate fonts inside `OnDrawItem`. Invalidate single rows for hover/checkbox; bulk operations invalidate the whole list.
10. **Single version source.** The version lives only in `Directory.Build.props`. Bump with `tools/bump-version.ps1` and record it in `docs/CHANGELOG.md`.
11. **View parity.** `MainForm.Accessor` is the only command surface. Add a method there instead of branching on `ViewMode`.
12. **Transforms are previews.** Text transforms stage a pending result in `RichPreviewPanel`; only **Save Changes** writes. Keep Cancel and Undo working.
13. **Thumbnails never distort.** `ThumbnailCache` letterboxes and is LRU-bounded. Never stretch.
14. **Double-click timing.** `CardActivated` fires on the second mouse-up; one double-click = one activation. Never hide the form on mouse-down.

## 5. Verification

```powershell
dotnet build ClipboardApp.sln -c Release
dotnet test ClipboardApp.sln -c Release
.\tools\build_releases.ps1
.\tools\verify-release.ps1          # only when an installer was built
```

Expected: 0 warnings / 0 errors, 116/116 tests.

## 6. Working Rules

- Follow [`AGENTS.md`](AGENTS.md): plain-English intent for a non-coder architect, no raw code diffs in explanations, descriptive `###` headers with aliases, and function/variable anchors instead of line numbers.
- After a feature or architectural change, update `PRODUCT.md`, `ARCHITECTURE.md`, `DECISIONS_AND_TRAPS.md`, and `VISUAL_GUIDE.html` if the change affects them, then commit and push.
- Never create `INDEX.md`, never use line-number anchors, never commit binaries or secrets.

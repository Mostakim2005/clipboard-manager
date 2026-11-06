# Product Intent & Feature Map (`PRODUCT.md`)

> **1-Line Summary:** The permanent source of truth for what ClipboardApp should do and feel like for its user, independent of how the current code is organized.

## 1. Core Vision & User Experience Intent

* **What This App Does (Plain English):** ClipboardApp keeps a private, searchable history of everything you copy on Windows — text, links, colors, files, and images — so you can find it again and paste it into the right window without losing your place.
* **How It Must Feel to Use:** Fast, quiet, keyboard-first, and safe. It should open where your attention already is, never reorder your history while you are working, never paste into the wrong window, and never leak clipboard contents to a password manager it does not understand.
* **Default Posture:** Local-only. No account, no telemetry, no cloud sync. Encryption at rest is mandatory. Heavy or risky actions (double-click paste, mixed image merging, native API access) are opt-in.
* **Deliverable:** A portable Windows 10/11 x64 executable by default. The setup wizard is built on request. GitHub Actions builds the portable binary from source; release binaries are never committed.

---

## 2. Active Features (Do Not Break)

### [FEATURE: Clipboard-Capture-And-Source-Tracking] | Aliases: copy history, monitor, source app, ignored apps
> **1-Line Summary:** Every copy is captured once, tagged with the app that produced it, and filtered for privacy before it enters history.
* **User Intention (Non-Negotiable):** If the user copies text, an image, a file list, HTML, RTF, or a color, it should appear once and be attributable to the source application. Password managers and apps on the ignore list must remain invisible.
* **Visual / UI Behavior:** New entries appear after the next refresh or confirmed-copy FD refresh. Each card shows its format badge, source app, timestamp, and stable serial.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/ClipboardMonitor.cs` -> `ClipboardMonitor`, `ClipboardChanged`
  * `src/ClipboardApp.WinForms/MainForm.cs` -> `HandleClipboardUpdate`, `ReloadSnapshot`, `_snapshot`
  * `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs` -> `InsertEntry`
  * `src/ClipboardApp.Core/Security/ClipboardPrivacyFilter.cs` -> `ShouldIgnore`, `IsSensitive`

### [FEATURE: Stable-Snapshot-History] | Aliases: no reorder, frozen list, FD refresh, fresh data
> **1-Line Summary:** Capturing a new clip must never move, rebuild, or reselect the list the user is currently reading.
* **User Intention (Non-Negotiable):** The visible list is a snapshot. Background captures are saved but do not disturb the current view. Optional FD refresh updates it once after a confirmed copy.
* **Visual / UI Behavior:** Footer shows a capture counter when the snapshot is stale. `F5` / `⟳ Refresh` reloads. The FD toggle is ON by default and preserves selection and `TopIndex`.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.cs` -> `ReloadSnapshot`, `RebuildList`, `_snapshotDirty`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `LiveRefresh`
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> FD refresh timer callbacks

### [FEATURE: Stable-Serial-Numbers] | Aliases: card number, #1, quick paste index
> **1-Line Summary:** Each visible card shows a stable `#N` serial derived from its current position, so keyboard users can refer to “item 3” without guessing.
* **User Intention (Non-Negotiable):** Copying or pasting an existing clip must not renumber the list. Only add, delete, or pin changes the order.
* **Visual / UI Behavior:** Serials appear on cards and in the inspector. The first nine also drive `1`–`9` quick paste.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.cs` -> `RebuildList`, serial assignment
  * `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> `OnDrawItem`, serial rendering
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `OnCardListKeyDown`

### [FEATURE: Search-Filter-And-Grouping] | Aliases: find, regex, date filter, type filter, app filter, tags
> **1-Line Summary:** The user can narrow history by text, regular expression, format pill, date, type, source app, tag, group, or paste-ready selection.
* **User Intention (Non-Negotiable):** Search must feel instant and must not require a network call. Filters must remain available even when the search box is empty.
* **Visual / UI Behavior:** Debounced search box, type pill bar, permanent Date/Type/App row, group headers, and tag search. `Select all filtered` and `Delete filtered` operate only on visible rows.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Services/SearchService.cs` -> `Filter`, `Matches`
  * `src/ClipboardApp.WinForms/MainForm.Filters.cs` -> `BuildFilterBar`, `DeleteFiltered`, `OnFilterChanged`
  * `src/ClipboardApp.Core/Services/FilterService.cs` -> `Apply`
  * `src/ClipboardApp.WinForms/Controls/FilterPillBar.cs` -> `SelectedFilter`

### [FEATURE: Card-List-And-Rich-Inspector] | Aliases: preview pane, split view, text preview, image zoom, color swatch
> **1-Line Summary:** The left side is a fast card list; the right side is a context-aware inspector that previews the selected clip without opening another app.
* **User Intention (Non-Negotiable):** The inspector must be readable, useful, and safe. Large text is not truncated into uselessness; images preserve aspect ratio; JSON and colors are human-readable.
* **Visual / UI Behavior:** Draggable 50/50 split, format-specific preview, copy/save/open actions, color swatch with RGB/Hex, zoomable image, and a clear empty state.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Ui.cs` -> `BuildModernUi`
  * `src/ClipboardApp.WinForms/Controls/RichPreviewPanel.cs` -> `ShowEntry`, `UpdatePreview`
  * `src/ClipboardApp.WinForms/Controls/ImagePreview.cs` -> zoom and pan behavior

### [FEATURE: List-And-Grid-View-Parity] | Aliases: grid mode, tiles, view switch, columns
> **1-Line Summary:** The user can switch between the classic list-plus-preview layout and a full-width grid without losing selection, filtering, or keyboard behavior.
* **User Intention (Non-Negotiable):** Every command must behave identically in both views. Switching views must be instant and remembered.
* **Visual / UI Behavior:** Footer button labels the mode it switches *to*. Grid tiles auto-fit or use a fixed 1–10 columns. Image tiles are taller so thumbnails remain visible.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Ui.cs` -> `ToggleViewMode`, `ApplyViewMode`
  * `src/ClipboardApp.WinForms/Controls/GridCardList.cs` -> tile layout, `GetItemAt`
  * `src/ClipboardApp.WinForms/MainForm.cs` -> `Accessor`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `ViewMode`, `GridColumns`

### [FEATURE: Paste-With-Verified-Target] | Aliases: Enter paste, target verification, no blind paste
> **1-Line Summary:** Pasting writes the clipboard, verifies the target owns the foreground, and only then sends `Ctrl+V`; if no safe target exists, it copies instead.
* **User Intention (Non-Negotiable):** Never paste into the wrong window. Never send synthetic keys while physical modifier keys are still held. Never clear a selection before a successful paste.
* **Visual / UI Behavior:** `Enter` pastes, `Shift+Enter` pastes plain text, footer shows the resolved target, and a failure reopens the target picker instead of guessing.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Services/PasteService.cs` -> `PasteEntry`, `CopyEntry`, `WaitForKeysReleased`, `PasteAttemptResult`
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `PasteSelectedEntry`, `HandlePasteFailure`, `ResolveTargetWindow`
  * `src/ClipboardApp.Core/Interop/TargetWindowTracker.cs` -> `GetTargetInfo`, `IsValidTarget`
  * `src/ClipboardApp.Core/Interop/NativeMethods.cs` -> `INPUT`, `SendInput`

### [FEATURE: Opt-In-Double-Click-Paste] | Aliases: double-click toggle, target chooser, mouse paste
> **1-Line Summary:** Double-click paste is a separate OFF-by-default gesture; when enabled, every double-click opens the active-app chooser before pasting.
* **User Intention (Non-Negotiable):** A double-click must never paste into a stale or unintended window. OFF means double-click only selects.
* **Visual / UI Behavior:** Settings → Interactions checkbox; forced `TargetPickerDialog` before the main window hides; multi-selection uses the same chooser.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `OnCardActivated`, `PasteEntry(..., forcePicker: true)`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `EnableDoubleClickPaste`, `PasteTrigger`
  * `src/ClipboardApp.WinForms/Dialogs/TargetPickerDialog.cs` -> window list and selection

### [FEATURE: Batch-Merge-And-Sequential-Paste] | Aliases: multi-select, merge, paste each, separator, rich format
> **1-Line Summary:** Several checked cards can be copied or pasted as one merged payload, or pasted one after another with `Paste Each`.
* **User Intention (Non-Negotiable):** Multi-select must be predictable, and a failed paste must not silently discard the selection. Format choice must be explicit.
* **Visual / UI Behavior:** Floating batch bar with `Select All`, `Clear`, `Delete Selected`, `Assign Tags & Groups`, `Merge & Paste`, and optional `⇉ Paste Each`. Merge formats: Plain, `.env`, JSON, CSV, Rich.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `BatchMergeAndPaste`, `SequentialBatchPaste`, `CopySelectedMerged`
  * `src/ClipboardApp.Core/Services/BatchCopyService.cs` -> `Build`, `NormalizeFormat`
  * `src/ClipboardApp.Core/Services/BatchPasteService.cs` -> `Build`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `BatchSeparator`, `SequentialBatchPaste`, `BatchCopyFormat`

### [FEATURE: Mixed-Text-And-Image-Rich-Merge] | Aliases: embedded images, HTML merge, RTF merge, image fallback
> **1-Line Summary:** When explicitly enabled, text and images can travel together in one CF_HTML/RTF payload; incompatible targets get a warning and text only.
* **User Intention (Non-Negotiable):** This is OFF by default. A merge that cannot carry images must say so, uncheck the images, and never pretend the images were pasted.
* **Visual / UI Behavior:** Settings toggle; context-menu `Copy Selected as → Rich text + images`; warning status/balloon; images deselected after fallback.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Services/RichMergeService.cs` -> `Build`, `PreparePng`
  * `src/ClipboardApp.Core/Services/PasteService.cs` -> `CopyRich`, `PasteRichDetailed`
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `BuildRichMerge`, `WarnMixedFallback`
  * `src/ClipboardApp.Core/Interop/PasteTargetInspector.cs` -> `GetKind`, `Classify`

### [FEATURE: Armed-Ole-Drag-And-Drop] | Aliases: drag out, drag arming, file drop, rich drag
> **1-Line Summary:** Drag-out is deliberately armed, never accidental, and can carry text, images, HTML, RTF, and file lists into other apps.
* **User Intention (Non-Negotiable):** Drag must not steal clicks, checkbox toggles, marquee selection, or double-click. It disarms after one drag or when the window loses focus.
* **Visual / UI Behavior:** Footer `✋ Drag: Off/Armed`; card drag starts only after an 8 px threshold; drop payload includes stable temp file paths.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `StartOleDrag`, `OnDragArmToggled`
  * `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> `DragArmed`, `OnMouseMove`
  * `src/ClipboardApp.WinForms/Controls/DragDropHelper.cs` -> payload construction
  * `src/ClipboardApp.WinForms/Settings.cs` -> `RequireManualDragArm`, `EnableDragDrop`

### [FEATURE: Tags-Aliases-And-Autocomplete] | Aliases: labels, rename, tag pills, suggestions
> **1-Line Summary:** Entries can have multiple aliases and tags, and the editor remembers the user's vocabulary to suggest it while typing.
* **User Intention (Non-Negotiable):** Tagging must be fast and forgiving; suggestions must never modify text until accepted.
* **Visual / UI Behavior:** Tag pills on cards, inline `Assign Tags` prompt, `EntryEditDialog` aliases/tags, API-key tags, popup suggestions ranked by frequency.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Models/ClipboardEntry.cs` -> `Alias`, `Tags`
  * `src/ClipboardApp.Core/Services/TagSuggestionService.cs` -> `CollectTags`, `Suggest`
  * `src/ClipboardApp.WinForms/Controls/TokenAutoComplete.cs` -> `UpdateSuggestions`, `Accept`
  * `src/ClipboardApp.WinForms/Dialogs/EntryEditDialog.cs` -> tag/alias fields

### [FEATURE: Persistent-Custom-Context-Menu] | Aliases: right-click menu, hide commands, reorder, customize
> **1-Line Summary:** The card right-click menu can be hidden, shown, and reordered, and the layout survives restarts.
* **User Intention (Non-Negotiable):** `Customize this menu...` must always remain available; the user can never lock themselves out of recovery.
* **Visual / UI Behavior:** Right-click → Customize; checked visibility list, Move Up/Down, Show All, Reset Order, Save/Cancel; Settings also exposes Customize.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/UI/ContextMenuCommands.cs` -> `All`, `NormalizeOrder`, `NormalizeHidden`
  * `src/ClipboardApp.WinForms/Dialogs/ContextMenuCustomizeDialog.cs` -> list, move, save
  * `src/ClipboardApp.WinForms/MainForm.Dialogs.cs` -> `ShowContextMenu`, `CustomizeContextMenu`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `ContextMenuOrder`, `ContextMenuHidden`

### [FEATURE: Image-Thumbnails] | Aliases: screenshot preview, letterbox, thumbnail cache
> **1-Line Summary:** Image cards show a real thumbnail with the original aspect ratio, decoded lazily and bounded in memory.
* **User Intention (Non-Negotiable):** Never stretch or distort a screenshot; never let a long image history grow memory without limit.
* **Visual / UI Behavior:** List and grid image cards paint letterboxed thumbnails; inspector offers a larger zoomable view.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/Controls/ThumbnailCache.cs` -> `Get`, LRU eviction
  * `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> thumbnail paint path
  * `src/ClipboardApp.WinForms/Controls/GridCardList.cs` -> image tile paint path

### [FEATURE: Transform-Preview-Save-Cancel-Undo] | Aliases: uppercase, slug, camel, snake, JSON prettify
> **1-Line Summary:** Text transforms stage a preview instead of silently rewriting the stored clip; the user explicitly saves, cancels, or undoes.
* **User Intention (Non-Negotiable):** A transform is a proposed edit, not an immediate database write. Switching entries discards an unsaved preview.
* **Visual / UI Behavior:** Header shows `PREVIEW — changes not saved`; `Save Changes`, `Cancel`, `Undo` (up to 20 steps). Transform menu includes UPPER, lower, Title Case, Trim, Remove Breaks, URL, Base64, JSON, slug, camel, snake.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/Controls/RichPreviewPanel.cs` -> pending preview, Save/Cancel/Undo
  * `src/ClipboardApp.Core/Services/TextTransformService.cs` -> transform functions
  * `src/ClipboardApp.WinForms/MainForm.Dialogs.cs` -> transform menu wiring

### [FEATURE: Dpapi-Encryption-And-Privacy-Filters] | Aliases: encryption at rest, sensitive data, password managers, auto-expiry
> **1-Line Summary:** Clipboard text and image bytes are encrypted at rest with Windows DPAPI, and known sensitive content is masked, expired, or excluded.
* **User Intention (Non-Negotiable):** The database must be useless without the current Windows user. Password-manager clips must be ignored. Sensitive clips must expire.
* **Visual / UI Behavior:** Masked cards/preview with reveal toggle; configurable auto-clear minutes; ignored-app list; privacy-atom handling.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Security/DataProtector.cs` -> `Protect`, `Unprotect`
  * `src/ClipboardApp.Core/Security/ClipboardPrivacyFilter.cs` -> privacy flags, sensitive detection
  * `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs` -> encrypted `Content`, `ImageData`
  * `src/ClipboardApp.WinForms/Settings.cs` -> `CheckSensitiveData`, `AutoClearSensitiveMinutes`, `IgnoredApps`

### [FEATURE: Encrypted-Api-Key-Vault] | Aliases: API keys, secrets, vault, master password
> **1-Line Summary:** API keys live in a separate encrypted store, never in clipboard history, with an optional master password and timed unlock.
* **User Intention (Non-Negotiable):** A copied API key must not leak into history or search. The vault must lock when idle and zero key material on close.
* **Visual / UI Behavior:** Modeless movable vault window, provider/name/secret/tag fields, reveal/copy actions, search, multi-key merge, status-bar unlock countdown.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Security/ApiVaultService.cs` -> `Unlock`, `ExtendUnlockWindow`, `LockIfIdle`
  * `src/ClipboardApp.Core/Storage/ApiKeyRepository.cs` -> encrypted key records
  * `src/ClipboardApp.WinForms/Dialogs/ApiVaultDialog.cs` -> modeless window, list, copy
  * `src/ClipboardApp.WinForms/Dialogs/ApiKeyEditDialog.cs` -> key editor

### [FEATURE: History-Limits-Backup-And-Restore] | Aliases: max entries, max age, export, import, JSON, CSV
> **1-Line Summary:** History is bounded by count and age, and the user can export or restore an encrypted backup without a cloud account.
* **User Intention (Non-Negotiable):** Old data must not grow forever. Backups must be portable and optionally DPAPI-encrypted; imports must not execute spreadsheet formulas.
* **Visual / UI Behavior:** Settings limits; export/import JSON and CSV; encrypted-backup option; DDE-injection defense for CSV.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.Core/Services/ExportImportService.cs` -> export/import, CSV escaping
  * `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs` -> `Prune`, count/age cleanup
  * `src/ClipboardApp.WinForms/Settings.cs` -> `MaxHistoryCount`, `MaxHistoryAgeDays`

### [FEATURE: Tray-Hotkey-And-Startup] | Aliases: global shortcut, start minimized, toggle window, shortcut capture
> **1-Line Summary:** The app starts quietly in the tray and appears or disappears with one global shortcut, configured by pressing the keys rather than typing them.
* **User Intention (Non-Negotiable):** The hotkey must toggle the window, not launch duplicates. Startup must be reliable and not steal focus. The shortcut capture must reject conflicts before saving.
* **Visual / UI Behavior:** `StartMinimizedToTray` ON by default; tray menu; `HotkeyCaptureBox` with Set/Reset, live preview, `Esc` cancel; window title shows version and hotkey.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/Program.cs` -> single-instance mutex, startup
  * `src/ClipboardApp.WinForms/MainForm.cs` -> `OnShown`, `ToggleAppWindow`, `ShowAppNearCursor`
  * `src/ClipboardApp.WinForms/HotkeyManager.cs` -> register/probe
  * `src/ClipboardApp.WinForms/Controls/HotkeyCaptureBox.cs` -> key capture
  * `src/ClipboardApp.Core/Services/HotkeyParser.cs` -> parse/format

### [FEATURE: Switchable-App-Wide-Styling] | Aliases: Fluent, Acrylic, Classic, Native, theme
> **1-Line Summary:** Every app-owned window, menu, inline form, and message box follows one switchable visual style, with a native fallback.
* **User Intention (Non-Negotiable):** No white-on-white or dark-on-dark inversions. The master toggle must restore native Windows colors without a restart.
* **Visual / UI Behavior:** Settings → Appearance: custom styling toggle + style selector. Native restores standard chrome. Dialogs and menus use the same tokens.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/UI/UiStyler.cs` -> tokens, `ApplyForm`, `ApplyMenu`, `StyleButton`
  * `src/ClipboardApp.WinForms/UI/ModernPalette.cs` -> color palette
  * `src/ClipboardApp.WinForms/Dialogs/MessageDialog.cs` -> themed dialogs

### [FEATURE: Accessible-Keyboard-And-Visual-Contrast] | Aliases: shortcuts, WCAG, contrast, focus, DPI
> **1-Line Summary:** The app is usable without a mouse and must remain legible at standard and high-DPI settings.
* **User Intention (Non-Negotiable):** Keyboard navigation must reach every primary action. Text contrast must meet WCAG AA (4.5:1 normal, 3:1 large). Focus must be visible.
* **Visual / UI Behavior:** Arrow-key navigation, Enter/Shift+Enter, 1–9, Ctrl+C, Ctrl+P, F2, F5, Delete, Esc; per-monitor V2 DPI; visible focus states.
* **Code Anchors (Grep Targets):**
  * `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `OnCardListKeyDown`
  * `src/ClipboardApp.WinForms/Program.cs` -> DPI awareness
  * `src/ClipboardApp.WinForms/UI/UiStyler.cs` -> contrast tokens
  * `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> focus paint

### [FEATURE: Reproducible-Build-And-Versioning] | Aliases: version source, checksums, verify release, bump version
> **1-Line Summary:** The version is defined once, release artifacts are reproducible and verifiable, and the same source builds the portable binary in CI.
* **User Intention (Non-Negotiable):** Never hard-code a version in multiple places. Never ship a setup installer whose embedded payload differs from the portable. A shrinking test suite must block release.
* **Visual / UI Behavior:** Window title, Settings, tray, and installer all show the real version. Release hashes are generated, not typed.
* **Code Anchors (Grep Targets):**
  * `Directory.Build.props` -> `Version`
  * `tools/build_releases.ps1` -> test floor, checksums, manifest
  * `tools/verify-release.ps1` -> payload byte-identity
  * `tools/bump-version.ps1` -> version bump
  * `src/ClipboardApp.WinForms/AppVersion.cs` -> displayed version

---

## 3. Planned Features & App-Merge Wishlist

### [WISHLIST: Cloud-Sync-With-End-To-End-Encryption] | Aliases: sync, multi-device, encrypted backup
> **1-Line Summary:** Optional encrypted sync across the user's Windows devices without a plaintext server copy.
* **User Intention:** Keep history available on a second PC without giving up local-first privacy.
* **Target Integration Point:** `ExportImportService` + a new transport layer; must reuse `DataProtector` and never upload plaintext.

### [WISHLIST: Plugin-Or-Script-Actions] | Aliases: automation, custom commands, webhooks
> **1-Line Summary:** Let the user add safe, explicit actions to selected clips (open in tool, send to script) without turning the app into a scripting host by default.
* **User Intention:** Extend the app for developer workflows while keeping the default surface small and secure.
* **Target Integration Point:** `ContextMenuCommands` + a permissioned action runner; no arbitrary code execution from remote content.

### [WISHLIST: OCR-For-Image-Clips] | Aliases: text from screenshot, search image text
> **1-Line Summary:** Search text inside screenshots locally, with an explicit opt-in and no cloud OCR.
* **User Intention:** Find a screenshot by the words it contains.
* **Target Integration Point:** Capture pipeline after image decode; store extracted text separately and mark it as derived.

### [WISHLIST: Per-App Capture Rules] | Aliases: app profiles, format rules, ignore rules
> **1-Line Summary:** Allow richer per-app rules than a simple ignore list (e.g. only images from a design tool, never text from a terminal).
* **User Intention:** Reduce noise without globally disabling useful capture.
* **Target Integration Point:** `ClipboardPrivacyFilter` + Settings rule editor; keep a simple default.

---

## 4. Product Guardrails

* Do not add telemetry, analytics, account creation, or network calls without an explicit product decision.
* Do not make risky gestures (double-click paste, mixed image merging) default-on.
* Do not remove the manual drag arming safety.
* Do not make the right-click menu impossible to recover.
* Do not ship a build with compiler warnings or failing tests.
* Do not add a dependency with a copyleft license (GPL, LGPL, AGPL, MPL) or any license that
  conflicts with the project's PolyForm Noncommercial 1.0.0 terms. Record every new dependency
  in `THIRD-PARTY-NOTICES.md`.
* Do not commit `.exe`, `releases/`, `publish/`, `node_modules/`, secrets, or local absolute paths to the public repository.

[Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Decisions & Traps](DECISIONS_AND_TRAPS.md) · [Visual Guide](VISUAL_GUIDE.html) · [AI Bootstrap](../AI_BOOTSTRAP.md)

# Features Specification & Catalog

## Complete Feature Matrix

| # | Feature | Status | Implementation Details |
|---|---------|--------|------------------------|
| 1 | Add clipboard entries + aliases | Done | `ClipboardEntry.Alias` field; editable via context menu & `EntryEditDialog` |
| 2 | Pin / favorite items | Done | Dedicated `📌 Pinned` group at top; never auto-deleted by cleanup |
| 3 | Single-click + opt-in double-click paste | Done | `PasteTrigger` offers `EnterOnly` (default) or `SingleClick`; `EnableDoubleClickPaste` is a separate toggle, **OFF by default**. When ON, every double-click opens the active-app chooser first, then pastes into the chosen window |
| 4 | Drag to paste (rich format) | Done | Manual `✋ Drag` arming; `MainForm.StartOleDrag` builds Unicode/RTF/HTML/FileDrop/Image payloads with stable temp files |
| 5 | Drag multi-select (marquee) | Done | Blank-area rubber-band selection on `ModernCardList` with baseline/Ctrl semantics |
| 6 | Bulk delete & Clear unpinned | Done | Delete key, context menu, and quick action bar |
| 7 | Groups + tags | Done | Full SQLite repository support for tags and groups |
| 8 | Copy / paste images | Done | DPAPI encrypted BLOB storage, lazy loading, stable temp drop files |
| 9 | File drop support | Done | Captures `DataFormats.FileDrop`; pastes an HDROP list back |
| 10 | DPAPI Encrypted Storage | Done | SQLite with Windows DPAPI encryption on Content and ImageData |
| 11 | Date-based grouping | Done | Today, Yesterday, Past 7 Days, Older |
| 12 | System tray icon | Done | `TrayIconManager` with pause/resume, clear history, settings, no ghost icon |
| 13 | Always-on-top toggle | Done | Persisted in atomic `settings.json` |
| 14 | Global hotkey with Win & Caret positioning | Done | Native Win32 `RegisterHotKey` (MOD_NOREPEAT, Win key) popping near cursor |
| 15 | Keyboard-first navigation | Done | Arrows skip headers, Enter, Shift+Enter, 1-9, Ctrl+C, F5, Esc |
| 16 | Instant filter with Debounce & Regex | Done | 160 ms debounce, regex mode, text/image/file/link/color filters |
| 17 | Preview pane | Done | SplitContainer with text/image preview, format and app metadata |
| 18 | Password manager protection | Done | Honors `ExcludeClipboardContentFromMonitorProcessing` & `CanIncludeInClipboardHistory` |
| 19 | Sensitive data auto-expiry | Done | Luhn credit card, API key, and private key regex; auto-expiry cleanup |
| 20 | Source-app tracking & per-app exclusion | Done | `GetClipboardOwner` / PID resolution; configurable `IgnoredApps` list |
| 21 | Text transforms | Done | UPPER, lower, Title Case, Trim, Remove Breaks, URL, Base64, JSON Prettify/Minify |
| 22 | Merge & Paste / Paste Each | Done | Configurable separator merged batch; optional sequential `⇉ Paste Each` |
| 23 | Edit before pasting | Done | Modal `EntryEditDialog` before pasting |
| 24 | History auto-cleanup / limit pruning | Done | Configurable count & age limits; stable `Seq` ordering |
| 25 | Windows startup integration | Done | HKCU Run toggle; registry only written when value changes |
| 26 | Start minimized to tray | Done | Single-instance Mutex with IPC wake up |
| 27 | Dark / Light theme | Done | Follows Windows Personalize registry setting or manual override |
| 28 | Audio / Toast feedback | Done | Configurable capture beep and balloon tip |
| 29 | Export / Import history | Done | JSON & CSV export with optional DPAPI encryption |
| 30 | Stable snapshot history | Done | No automatic SQLite reload/reorder on clipboard capture; F5/Refresh/reopen only |
| 31 | Stable serial numbers | Done | `#N` from visual order; copy/paste never changes it |
| 32 | Dedupe modes | Done | Keep position (default), Move to top, Allow duplicates; type-prefixed SHA-256 |
| 33 | Copy without pasting | Done | `Ctrl+C` and context menu; keeps window visible and suppresses self-capture |
| 34 | Target-verified paste | Done | Clipboard write success + foreground verification before SendInput |
| 35 | Settings tooltips & conflict warnings | Done | Options dialog tooltips + dynamic Interactions warning panel |
| 36 | Image size limit | Done | `MaxImageSizeMb` enforced during capture (default 15 MB) |
| 37 | FD confirmed-copy refresh | Done | `LiveRefresh` (FD: fresh data) default ON in v2.3.1; one coalesced 150 ms refresh after a confirmed copy while visible; existing rows keep `Seq`. OFF = frozen snapshot until F5/reopen |
| 38 | Merged multi-select copy | Done | `BatchCopyService` builds one payload from checked entries in Plain (default), `.env`, JSON, CSV, or `Rich`; `Ctrl+C`, context menu, and `Copy Merged` |
| 39 | Progressive / range selection | Done | `ModernCardList`: Ctrl+Arrow progressive add, Shift / Ctrl+Shift ranges, Space toggle, Home/End/PageUp/PageDown |
| 40 | Persistent selection | Done | Checked IDs survive refresh/filter/grouping; optional persistence across restarts (`RememberSelection`) |
| 41 | Manual paste-target picker | Done | `TargetPickerDialog` lists recent windows (app + state); footer `Target:` button and failed-paste path; `PasteTargetMode` Auto/Ask + badge |
| 42 | Encrypted API-key vault | Done | AES-256-GCM with DPAPI-wrapped vault key and optional PBKDF2 master password; separate store, multi-key merged copy, clipboard suppression + auto-clear |
| 43 | Branded app / window / tray icon | Done | Embedded multi-frame BMP/DIB `app.ico` loaded via `AppIcon`; used as `ApplicationIcon`, window icon, and tray image |
| 44 | Reliable paste (x64 `SendInput` fix) | Done | `INPUT`/`InputUnion` sized correctly (40 bytes x64); verified synthetic keystroke injection |
| 45 | New toggleable options | Done | Paste target mode/badge, batch copy format, clear-selection-after-paste, remember selection, enable vault, vault auto-clear, double-click paste, mixed merge, custom UI styling |
| 46 | Persisted customizable right-click menu | Done | `ContextMenuCommands` registry + `ContextMenuOrder`/`ContextMenuHidden`; `ContextMenuCustomizeDialog` hide/show/reorder; `Customize this menu...` is always last and non-hideable |
| 47 | Forced target chooser for double-click | Done | `MainForm.OnCardActivated` gates on `EnableDoubleClickPaste` and passes `forcePicker: true`; multi-selection uses `BatchMergeAndPaste(..., forcePicker: true)` |
| 48 | Opt-in mixed text + image merging | Done | `EnableMixedMerge` **OFF by default**; `RichMergeService` embeds PNGs in CF_HTML + RTF with UnicodeText fallback; plain targets/fallback warn, uncheck images, and copy text only |
| 49 | Switchable app-wide UI styling | Done | `UiStyler` Fluent/Acrylic/Classic/Native; `EnableCustomUiStyling` toggle; dialogs, menus, inline forms, message boxes, main chrome, filter/footer/batch bars |
| 50 | Always-available Date / Type / App filters | Done | Permanent filter row with Date/Type/App combos, `Select all filtered (N)`, `Delete filtered`, `Clear filters`; `FilterService` applied in `RebuildList`; learned App list |
| 51 | Tag & alias autocomplete | Done | `TagSuggestionService` frequency ranking + `TokenAutoComplete` popup in `EntryEditDialog`, inline Assign Tags, and API-key tags |
| 52 | Rich clipboard payload | Done | `PasteService.CopyRich`/`PasteRichDetailed` write HTML + RTF + UnicodeText; `PasteTargetInspector` classifies plain vs rich destinations |
| 53 | Tray-first startup + hotkey toggle | Done | `StartMinimizedToTray` defaults ON and is enforced in `MainForm.OnShown` after full init. The global hotkey **toggles**: first press shows and focuses the search box, second press hides back to the tray. Re-shows at the last on-screen position. Window title shows `Clipboard App v<Version> (<hotkey>)` |
| 54 | Key-capture hotkey setup | Done | `HotkeyCaptureBox` replaces manual text entry in Settings → General. Click **Set**, press the combination, and it registers immediately: live modifier preview, bare-key rejection, `HotkeyManager.TryProbe` conflict detection before commit, `Esc` cancels, **Reset** restores the default. `HotkeyParser` (Core) is the single parse/format authority used by both the UI and the Win32 registration |
| 55 | Timed vault unlock | Done | `ApiVaultService.ExtendUnlockWindow` keeps the vault unlocked for `ApiVaultAutoLockMinutes` (default 5) after one unlock, so reopening it does not re-prompt. `LockIfIdle` honours the window. The vault status bar shows the remaining time; manual **Lock** clears it immediately |
| 56 | Non-blocking, movable API vault | Done | `ApiVaultDialog` is modeless (`Show`, not `ShowDialog`) with its own taskbar entry, so it can stay open and be moved while the main list keeps working. Re-invoking Vault focuses the existing window instead of opening a second one |
| 57 | Image thumbnails in the list | Done | Image cards paint a real letterboxed screenshot thumbnail via `ThumbnailCache` (aspect ratio preserved, never stretched; LRU-bounded at 160 entries). Decoded lazily from already-decrypted bytes |
| 58 | List/Grid view switch | Done | Footer button toggles **List + Preview** ⇄ **Grid** live and persists `ViewMode`. Grid uses `GridCardList`: text tiles ~1 row, image tiles ~2 rows with a thumbnail; columns auto-fit or fixed 1–10 (`GridColumns`). Selection, filtering, grouping, drag arming, context menu and keyboard all behave identically because commands route through `MainForm.Accessor` |
| 59 | Transform preview with Save/Cancel/Undo | Done | UPPER, lower, slug, camel, snake and Prettify JSON no longer rewrite the stored clip. They stage a pending result (`PREVIEW — changes not saved`) with **Save Changes** (single repository write), **Cancel**, and **Undo** (up to 20 steps). Switching entries discards the unsaved preview |
| 60 | Reproducible, verifiable installer | Done | `tools/build_releases.ps1` regenerates `payload.bin` from the portable it just published; `ClipboardApp-Setup.exe --verify-payload=<portable>` decompresses its embedded payload with the install code path and proves byte-identity. `tools/verify-release.ps1` re-checks all checksums, the stamped version, and the payload match |
| 61 | Semantic versioning + changelog | Done | Version lives only in `Directory.Build.props`; `tools/bump-version.ps1 -Part major|minor|patch` updates it and the release matrix. Policy documented in `AGENTS.md` § 2A; every change recorded in `docs/CHANGELOG.md` |
| 62 | Encoding repair tool | Done | `tools/fix-encoding.ps1` reverses Windows-1252 mojibake (HANDOFF landmine #16) across markdown files, reducing detected marker sequences to zero |
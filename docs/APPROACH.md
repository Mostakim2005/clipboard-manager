[Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Decisions & Traps](DECISIONS_AND_TRAPS.md) · [Visual Guide](VISUAL_GUIDE.html) · [AI Bootstrap](../AI_BOOTSTRAP.md)

# Implementation Approach & Audit Log

This document explains the current implementation decisions and the root causes of the bugs fixed in **v2.5.0**, with the v2.4.0 and v2.3.0 audits retained below. It supersedes the older `GroupedListView`/`SendKeys` notes that no longer match the codebase.

---

## 0. End-to-End Data Flow

```
Windows clipboard change
  └─ ClipboardMonitor (message-only HWND, WM_CLIPBOARDUPDATE)
      └─ MainForm.HandleClipboardUpdate()
          ├─ privacy flags / ignored apps / capture toggles
          ├─ file drop first → image (DIB → PNG) → Unicode text → HTML → RTF
          ├─ sensitive-data detection + expiry
          ├─ SqliteClipboardRepository.InsertEntry(entry)
          │    ├─ ClipboardContentIdentity.Compute (type-prefixed SHA-256)
          │    └─ DeduplicationMode: KeepPosition | MoveToTop | AllowDuplicates
          └─ _snapshotDirty = true; status counter
             (visible list is NOT rebuilt)

User opens the window / presses F5 / clicks Refresh
  └─ MainForm.ReloadSnapshot(force: true)
      ├─ one SQLite read (metadata only; no image BLOBs)
      └─ MainForm.RebuildList()
          ├─ filter + group from _snapshot
          ├─ preserve selected entry ID + TopIndex
          ├─ ModernCardList.ResetInteractionState()
          └─ prune checked IDs to visible rows

User pastes (Enter / double-click / batch bar)
  └─ MainForm.PasteEntry()
      ├─ resolve valid target (ring buffer → last active → topmost client)
      ├─ no target → copy to clipboard + status, stay visible
      ├─ suppress self-capture, hide, yield ~50 ms
      ├─ PasteService.CopyEntry (Unicode / CF_HTML / RTF / DIB+PNG / HDROP)
      ├─ SetClipboardWithRetry → abort if false
      ├─ activate + verify foreground (up to 4 attempts)
      └─ SendInput Ctrl+V only if foreground is verified
```

---

## 2. Core Design Decisions

### 2.0 Stable Snapshot Instead of Live Reload
**Files:** `MainForm.cs` (`_snapshot`, `ReloadSnapshot`, `RebuildList`), `MainForm.Ui.cs`.

- Clipboard capture is a **write-only** path for the UI. It increments `_pendingCaptureCount` and sets `_snapshotDirty`; it never calls `Items.Clear()`, changes selection, or resets scroll.
- `ReloadSnapshot` is the **only** SQLite read path. It runs at startup, on F5/Refresh, on reopen when dirty, after tray clear, and when the confirmed-copy (`FD`) refresh is enabled and dirty.
- `RebuildList` restores the selected entry **by ID**, then `TopIndex`.
- `LiveRefresh` (FD: fresh data) defaults to **ON** in v2.3.0: after a confirmed copy (repository insert returns), one coalesced 050 ms refresh runs while the window is visible. Existing rows keep their `Seq`; selection and `TopIndex` are preserved. OFF keeps a frozen snapshot until F5/reopen.

**Why:** the old design queried and DPAPI-decrypted the entire history on every clipboard event, then rebuilt the owner-drawn list and forced `SelectedIndex = first` / `TopIndex = 0`. That was the flicker, selection-reset, and reorder source.

### 2.2 Permanent Ordering with `Seq`
**Files:** `SqliteClipboardRepository.cs`, `ClipboardEntry.cs`.

- New rows receive `Seq = MAX(Seq) + 0` at insert. Existing databases are backfilled with `Seq = rowid`.
- Queries sort `Pinned DESC, Seq DESC`; pruning uses `Seq`.
- `KeepPosition` (default) updates only `CopyCount` and `LastCopiedAt`; it never touches `Seq` or `CreatedAt`.
- `MoveToTop` assigns a new `Seq`; `AllowDuplicates` inserts a new row.
- UI serials `#N` are derived from the current visual order and are not stored.

**Why:** users expect a copied item to stay put unless they explicitly pin, delete, or add. The old newest-only dedupe path called `UpdateEntry`, which did not update `CreatedAt`/`CopyCount`, so recopy silently did nothing and could block the repository's correct dedupe.

### 2.3 Single Dedupe Authority
**Files:** `ClipboardContentIdentity.cs`, `SqliteClipboardRepository.InsertEntry`.

- Identity is type-prefixed SHA-256:
  - `txt:` normalized text
  - `html:` / `rtf:` / `file:` respective payloads
  - `img:` PNG bytes
- Line endings are normalized (`\r\n`/`\r` → `\n`) before hashing.
- The UI no longer compares against `GetNewestEntry()`; only the repository decides duplicates.

**Why:** a single authority prevents the UI and repository from disagreeing, and type prefixes prevent `<b>x</b>` as text from colliding with `<b>x</b>` as HTML.

### 2.4 Manual Drag Arming
**Files:** `ModernCardList.cs`, `MainForm.Paste.cs`, `MainForm.Ui.cs`.

- `DragEnabled` is the master setting; `DragArmed` is session state.
- `RequireManualDragArm` defaults to `true`.
- The footer `✋ Drag` button toggles `_manualDragArmed`.
- Disarm points: `OnDeactivate`, minimize, hide/close, `ShowAppNearCursor`, and `DragSessionFinished`.
- A drag starts only when: armed, enabled, left press on a card body, movement ≥ 8 px, no Ctrl/Shift, and not a double-click.
- Checkbox presses never start a drag.

**Why:** the old control started a drag on 4 px of movement from any card, which stole double-clicks, Ctrl/Shift selection, and checkbox gestures. Drag now cannot interfere unless explicitly enabled for the session.

### 2.5 Gesture State Machine & Double-Click
**File:** `ModernCardList.cs`.

- `OnMouseDown` records the press index, entry ID, checkbox hit, click count/time/point.
- Double-click is detected manually with `TickCount64`, `SystemInformation.DoubleClickTime`, and `DoubleClickSize`; `ListBox.DoubleClick` is not used.
- `CardActivated` fires for the exact hit-tested card. MainForm's single-click timer delays optional single-click paste so a double-click cannot paste twice.
- `_movedDuringPress` suppresses the synthetic click after any movement/drag.
- `ResetInteractionState()` clears stale anchors and hover indexes after a rebuild.
- Marquee starts only from blank/gutter space and uses a baseline set for Ctrl.
- Hover/checkbox changes invalidate only the affected row.
- Fonts are cached fields and disposed in `Dispose`; no `new Font` inside `OnDrawItem`.

**Why:** owner-drawn `ListBox` plus the old 4 px drag threshold made double-click and selection unreliable. Cached fonts/invalidation remove GDI churn and repaint flicker.

### 2.6 Verified Paste, Not Blind Keystrokes
**Files:** `PasteService.cs`, `MainForm.Paste.cs`.

- `SetClipboardWithRetry` returns `false` on failure; callers abort.
- `SendPasteToWindow` attaches thread input, activates the target, and checks `GetForegroundWindow()` / `GA_ROOT` up to 4 times before sending keys.
- `MainForm.ResolveTargetWindow` rejects own/shell/invalid HWNDs.
- No valid target → `CopyEntry` only, status message, window stays visible.
- `MainForm.ForceForeground` uses `AttachThreadInput` + `SetForegroundWindow` so the hotkey window actually receives focus.
- HTML writes a valid CF_HTML header + plain fallback; RTF has a plain fallback; images set DIB + PNG; files set HDROP + text.
- Batch selection clears only after success.

**Why:** the old code ignored clipboard-write failures and sent Ctrl+V unconditionally, often into the previous window or the taskbar. It also accepted shell HWNDs as paste targets.

### 2.7 Batch Paste
**Files:** `BatchPasteService.cs`, `MainForm.Paste.cs`.

- `BatchPasteService.Build` takes entries in visual order and produces:
  - merged text with a configurable separator (`DoubleNewline` default),
  - a file list,
  - a `FileOnly` flag.
- Image entries resolve to stable `%TEMP%\ClipboardApp\Drops\clip_<id>.png` paths.
- File-only selections paste as one `HDROP`; mixed selections paste merged text/paths.
- `SequentialBatchPaste` (optional) sends one paste per clip with 080 ms spacing.

**Why:** deterministic, testable plans avoid UI-specific duplication and make merged vs sequential behavior explicit.

### 2.8 Settings and Conflict Prevention
**Files:** `Settings.cs`, `OptionsDialog.cs`.

- New toggles: `LiveRefresh` (confirmed-copy / FD refresh), `RequireManualDragArm`, `ShowSerials`, `SequentialBatchPaste`, `BatchSeparator`, dedupe mode.
- Legacy values are normalized on load (`UpdateTimestamp` → `MoveToTop`, `Exact` → `KeepPosition`, `None` → `AllowDuplicates`).
- Tooltips on checkboxes/fields; a dynamic warning panel explains blocking interactions.
- Splitter saves are debounced 400 ms.
- Startup registry writes are skipped when the value already matches.
- `MaxImageSizeMb` is enforced during capture.

### 2.9 The “FD” Toggle (Confirmed-Copy / Fresh-Data Refresh)
**File:** `Settings.cs` (`LiveRefresh`), `MainForm.cs` (`HandleClipboardUpdate`, `_refreshTimer`), `OptionsDialog.cs`.

FD means **fresh data**. There are two similarly named concepts in the UI; both are documented with examples in the persistent help panel:

0. **Refresh list after a confirmed copy (FD: fresh data)** — the main toggle.
   - ON (default): `HandleClipboardUpdate` writes the clip, then starts a 050 ms single-shot timer. When it fires, `ReloadSnapshot(force: true)` reads metadata once and `RebuildList` restores the selected entry ID and `TopIndex`.
   - OFF: the write still happens; the list stays frozen and the footer says `"N clipboard captures since refresh — press F5"`.
   - Existing rows never move: `Seq` is permanent. A new entry gets `MAX(Seq)+0`; a recopy under `KeepPosition` only increments `CopyCount`.
2. **Capture copied files (File Drop / FD)** — the file-drop capture toggle.
   - ON: Explorer copies are stored as one file-list clip; OFF: file copies are ignored.

**Concrete example:** the list has clips #0–#8; #5 is selected. You copy new text `X`.
- FD refresh ON: after the insert, the list refreshes once. `X` becomes #0, the old #0–#8 keep their relative positions, and the selection stays on the same card (now #6). No duplicates, no scroll jump.
- FD refresh OFF: `X` is saved, the footer counts the capture, and the visible list is unchanged until F5/reopen.
- File Drop FD ON: selecting `report.pdf` + `photo.png` in Explorer and pressing Ctrl+C creates one card containing both paths; pasting into Explorer restores the selection.
- File Drop FD OFF: that file copy is ignored; a text or image copy still works.

---

## 3a. v2.5.0 Implementation Decisions

| Area | Decision | Rationale |
| :--- | :--- | :--- |
| Right-click menu | Persist `ContextMenuOrder` / `ContextMenuHidden` in settings; rebuild items through `ContextMenuCommands`; `customize` is non-hideable and always last | The menu must survive restarts, and a user must always be able to recover a hidden command |
| Double-click paste | Separate `EnableDoubleClickPaste` toggle, **OFF by default**; when ON `OnCardActivated` always requests the target picker | Prevents accidental pastes and stale-target surprises; double-click no longer conflicts with `PasteTrigger` |
| Mixed rich merge | Separate `EnableMixedMerge` toggle, **OFF by default**; `RichMergeService` writes CF_HTML + RTF + UnicodeText with embedded PNGs; plain targets/fallback warn, uncheck images, and copy text | Images cannot be represented in plain text/ENV/JSON/CSV, so the fallback must be explicit and lossless for text |
| Target capability probe | `PasteTargetInspector` classifies the focused control before a mixed paste | Avoids writing image payloads to editors that will discard or garble them |
| UI styling | `UiStyler` tokens (Fluent/Acrylic/Classic/Native) + master `EnableCustomUiStyling` toggle; applied to every app-owned form, menu, inline form, and message box | Consistent appearance without rebuilding the UI; Native restores Windows colors |
| Filters | Permanent Date/Type/App row; `FilterService` in Core; `Select all filtered` and `Delete filtered` operate only on visible rows | Filtering must be always available and testable; hidden checked items must not be deleted accidentally |
| Autocomplete | `TagSuggestionService` ranks learned tokens by frequency; `TokenAutoComplete` popup accepts only via Enter/Tab/click | Learns the user's vocabulary without modifying text until accepted |

---

## 3. Root-Cause Audit (v2.4.0)

| Reported symptom | Actual root cause | Resolution |
| :--- | :--- | :--- |
| **“Paste not confirmed — the target window did not take focus” / “Windows blocked synthetic keyboard input”** | `NativeMethods.INPUT` was **32 bytes on x64** because `InputUnion` only declared `KEYBDINPUT`; Windows requires **40**. `SendInput` returned `0` and no keystroke was injected. | Added `MOUSEINPUT` to `InputUnion` at `FieldOffset(0)` so the union sizes to the largest member. `InteropLayoutTests` asserts 40 (x64) / 28 (x86). Verified end-to-end. |
| Double-click paste mis-targeted with many windows open | A manual target picked earlier (`_preferredTarget`) pinned a stale window for the app's lifetime; auto-detection never ran. | `_preferredTarget` is runtime-only and validated before use; the target picker and footer badge make the resolved target visible; failed pastes reopen the picker. |
| “Copy as one merged copy” / multi-paste | A clipboard holds one payload; the UI copied only the clicked row. | `BatchCopyService` builds one merged payload (Plain/`.env`/JSON/CSV); `Ctrl+C`, context menu, and `Copy Merged` copy it; `Merge & Paste` and `⇉ Paste Each` dispatch it. |
| No tray logo | `NotifyIcon` used the default application icon. | Generated a multi-frame BMP/DIB `app.ico`, embedded it, and loaded it via `AppIcon` for the window and tray. |
| API keys stored insecurely | No vault; keys would have landed in history. | AES-256-GCM + DPAPI-wrapped vault key + optional PBKDF2 master password, in a **separate** store that never enters history/search/batch; copies suppress capture and auto-clear. |
| Selection lost on refresh/filter | Selection lived only in the `ListBox` item state. | Persistent checked-ID set on `ModernCardList`, restored after `RebuildList`, with pruning to existing entries and optional cross-restart persistence. |
| Garbled dialog text | `ApiVaultDialog.cs` contained double-encoded UTF-8 (mojibake). | Targeted replacement of known mojibake sequences, saved UTF-8 without BOM; verify with a code-point scan. |

---

## 3b. Root-Cause Audit (v2.3.0)

| Reported symptom | Actual root cause | Resolution |
| :--- | :--- | :--- |
| **Blank list until hover/click** | `ModernCardList` set `ControlStyles.UserPaint` on a native owner-drawn `ListBox`, suppressing the native list paint path. Hover/click invalidated individual rows, which briefly revealed them. | Removed `UserPaint`/`StandardDoubleClick`; native `WM_DRAWITEM` painting restored. |
| **Double-click sometimes closed/duplicated** | Activation fired on the second mouse-down, hiding the form mid-gesture; the second mouse-up could leak to the target. | Activation deferred to the second mouse-up; one `CardActivated` per double-click; regression test counts activations. |
| Flicker / jank | Full SQLite read + DPAPI decrypt + `Items.Clear` + full repaint on every clipboard event; whole-list hover invalidation; per-paint font allocation | Snapshot cache, explicit reload, row-level invalidation, cached fonts, debounced splitter save |
| Drag-and-drop repeats | 4 px drag threshold hijacked clicks; no re-entry/suppression; timestamped drop file every drag; click-after-drag could paste | Manual arming, 8 px threshold, gesture state machine, stable drop paths, capture suppression, click suppression |
| Double-click paste did not work | Drag started before `DoubleClick`; ListBox event unreliable; first click may not activate window | Manual double-click detection inside the control; `CardActivated`; `ForceForeground` |
| Multi-select / multi-paste failed | Checkbox hit rect differed from draw; any movement started drag; marquee unreachable over cards; list rebuild cleared selection | Shared `GetCheckboxRect`, drag gating, blank-area marquee, snapshot selection restore, checked-ID pruning |
| Entries rearranged on copy | Local newest-only dedupe called `UpdateEntry` but did not update `CreatedAt`; list forced `SelectedIndex`/`TopIndex` | Removed UI dedupe; repository `Seq` ordering; KeepPosition default; selection/TopIndex preservation |
| Duplicate entries | UI/repo dedupe disagreement; image hash only on full insert; no stable identity prefix | `ClipboardContentIdentity`, single repository authority, `AllowDuplicates` opt-in |
| Stale/wrong paste target | `_lastActiveWindow` accepted shell windows; clipboard write result ignored; no foreground verification | Target validation, write-success check, foreground verification, no-target copy fallback |
| UI freeze on copy | UI-thread `Thread.Sleep` retries up to 300 ms while clipboard locked | Non-blocking single-shot retry timer |
| Fake paste / duplicated history from preview copy | Preview copy buttons wrote the clipboard without suppression | `ClipboardWriteStarted` event + MainForm suppression window |
| GDI handle growth | Fonts allocated per item/paint and never disposed | Cached font fields disposed with the control |
| Splitter jitter and disk writes | `OnResize` reset splitter; `SplitterMoved` saved per pixel; registry rewritten every save | Layout guard, `_applyingSplitter`, 400 ms debounce, registry value comparison |
| Bottom bars overlapped | Batch bar and footer both docked `Bottom` in the same layout slot | Single `_bottomArea` with explicit 34/72 px height |

---

## 4. Invariants for Future Work

0. Clipboard capture must never rebuild or reorder the visible list synchronously. The only allowed automatic refresh is the coalesced confirmed-copy (FD) reload after `InsertEntry` returns.
2. `ReloadSnapshot` is the only read path; do not query SQLite from `HandleClipboardUpdate`, hover, or paint. The FD timer is the one deliberate exception.
3. `Seq` is immutable for a row; recopy under KeepPosition must not move or duplicate it.
4. Double-click detection belongs to `ModernCardList`, not `ListBox.DoubleClick`.
5. Drag requires explicit arming by default and must never start from a checkbox or modifier gesture.
6. Never send Ctrl+V if the clipboard write failed or foreground cannot be verified.
7. Never clear a batch selection before a successful paste.
8. Only the repository decides duplicate identity.
9. Cache and dispose GDI fonts; invalidate rows, not the world.
00. All tests must stay green (currently 116/116) and the build must remain warning-free.

---

## 5. Architecture Decisions

| Decision | Rationale |
| :--- | :--- |
| WinForms over WPF | Lightweight, fast startup, no XAML dependency |
| Self-contained single-file `.exe` | Zero-install portability; ~60–67 MB working set |
| SQLite + DPAPI | Structured querying with encryption at rest |
| Repository pattern | Testable storage; single dedupe authority |
| Stable snapshot | Predictable UI, no copy-triggered reorder or flicker |
| Permanent `Seq` | User-controlled order; only pin/delete/add change it |
| Manual drag arming | Drag cannot steal click/selection/double-click gestures |
| Verified send-input paste | Avoids blind pastes into the wrong window |
| `ModernCardList` owner-drawn `ListBox` | Rich cards, checkboxes, marquee, row-level invalidation |
| Crash log in `%LocalAppData%\ClipboardApp\crash.log` | Diagnosable failures without a debugger |

---

## 6. Verification

```powershell
dotnet build ClipboardApp.sln -c Release
dotnet test ClipboardApp.sln -c Release
.\tools\build_releases.ps1                  # portable only (default)
.\tools\build_releases.ps1 -BuildInstaller  # portable + setup wizard
.\tools\verify-release.ps1                  # payload byte-identity + checksums
```

- Build: **0 warnings, 0 errors**
- Tests: **116/116 passing**
- Portable: `releases/ClipboardApp-v<Version>-Portable.exe` (built from source, attached to GitHub Releases)
- Setup (on-demand): `releases/ClipboardApp-v<Version>-Setup.exe`
- Hashes are generated by the pipeline into `releases/SHA256SUMS.txt`; never hand-typed.

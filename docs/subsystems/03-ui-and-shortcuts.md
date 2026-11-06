[Architecture](../ARCHITECTURE.md) · [Decisions & Traps](../DECISIONS_AND_TRAPS.md) · [AI Bootstrap](../../AI_BOOTSTRAP.md)

# Subsystem Dossier 03: UI, Cards & Keyboard Shortcuts

> **Component Scope:** `src/ClipboardApp.WinForms/Controls/ModernCardList.cs`, `src/ClipboardApp.WinForms/MainForm.cs`, `src/ClipboardApp.WinForms/Dialogs/EntryEditDialog.cs`, and `src/ClipboardApp.WinForms/Theming/ThemeEngine.cs`.

---

## 1. Multi-Select System Architecture

The card list supports multi-selection mechanisms comparable to Windows File Explorer:

### A. Selection Modes
1. **Per-Card Checkbox:**
   - Dedicated 16x16 checkbox drawn on the left side of every card.
   - A single shared `GetCheckboxRect()` defines both painting and hit-testing, so the clickable area always matches the visual.
   - Toggling a checkbox does not force a single-item focus change.
2. **Shift + Click Contiguous Range Selection:**
   - Clicking an anchor card and holding `Shift` while clicking a destination card checks all cards within `[MinIndex, MaxIndex]`.
3. **Ctrl + Click Discrete Toggle:**
   - Toggles an individual card's checked state while preserving all previously selected cards.
4. **Blank-Area Marquee Selection:**
   - Dragging from empty space/gutter (not over a card, checkbox, or group header) draws a semi-transparent rectangle (`Color.FromArgb(40, 59, 130, 246)`).
   - A baseline set is captured at drag start; holding `Ctrl` preserves the existing selection, otherwise the marquee replaces it.
   - Only the union of the old/new marquee rectangles is invalidated.
5. **Select All & Invert Selection:**
   - `Ctrl + A`: Checks/selects all currently displayed cards.
   - `Invert Selection`: Swaps checked states across all displayed items.
   - `Space`: Toggles the highlighted card.
   - `Esc` (when batch active): Clears checked items and dismisses the floating batch bar.

### B. Manual Drag Arming
- Drag is **OFF by default** (`RequireManualDragArm = true`).
- Click the footer `✋ Drag: Off` button to arm it for the current window session.
- It disarms automatically on window deactivate, minimize, hide/close, after a completed drag, and whenever the window is shown.
- Drag starts only when all conditions hold: armed, `DragEnabled`, left button on a card body, movement ≥ 8 px, no Ctrl/Shift, and not the second click of a double-click.
- While unarmed, mouse movement can never start a drag. Double-click, Ctrl/Shift selection, checkbox clicks, and marquee are unaffected.
- `EnableDragDrop` remains the master switch; disabling it disables both arming and the toggle.

### C. Double-Click Contract
- Double-click is detected in the control via `Environment.TickCount64`, `SystemInformation.DoubleClickTime`, and `DoubleClickSize`.
- `ListBox.DoubleClick` is not used; this avoids owner-drawn/ListBox event quirks and the old drag-vs-click collision.
- `CardActivated` pastes the exact hit-tested card. If 2+ cards are checked, it invokes the merged batch path.
- A fast click on a different card is not treated as a double-click.

---

## 2. Floating Batch Actions Bar

Whenever one or more items are checked (`checkedCount > 0`), the floating batch bar appears above the footer:
- **Count Indicator:** e.g., *"3 items selected"*
- **Select All Button:** Checks all visible items.
- **Invert Selection Button:** Reverses selection state.
- **Delete Button (Danger):** Deletes checked items with optional confirmation dialog.
- **Tags Button:** Opens tag assigner dialog to apply tags to all checked items.
- **Merge & Paste Button:** Merges checked text entries with the configured separator and pastes into the target window.
- **Paste Each Button (optional):** Visible only when `SequentialBatchPaste` is enabled; sends one paste per checked clip.
- Checked IDs are pruned to visible rows after every rebuild. Hidden/filtered rows can never be pasted or deleted accidentally.
- Selection clears only after the clipboard write/paste succeeds.

---

## 3. Split Layout & Stable Snapshot

- **Default Layout:** 50% Card List / 50% Inspector Preview pane.
- **Mouse Draggable Splitter:** Built on `SplitContainer` with double-buffered rendering; the ratio save is debounced 400 ms to avoid per-pixel registry/disk writes.
- **Persistence:** `AppSettings.SplitterRatio` is restored on launch.
- **Snapshot:** `MainForm._snapshot` is loaded on startup, F5/Refresh, reopen when dirty, tray clear, and the FD confirmed-copy refresh (default ON). Copy events write the database; the automatic refresh is a single coalesced 150 ms reload that preserves selection and `TopIndex`.
- **Stable serials:** Cards display `#N` from the current visual order. Copy/paste does not change it; adding, deleting, or pinning does.
- **Ordering:** `Pinned DESC, Seq DESC`. `Seq` is permanent per row. Recopying under KeepPosition does not move the row.

---

## 4. Keyboard Shortcuts Specification

| Shortcut | Context | Action |
| :--- | :--- | :--- |
| `Ctrl + Backtick` | Global Windows Hook | Toggle ClipboardApp window near cursor or center |
| `Enter` | Search Box / Card List | Paste selected entry (or merged batch if multiple checked) |
| `Shift + Enter` | Search Box / Card List | Paste selected entry as plain text (formatting stripped) |
| `1` through `9` | Search Box / Card List | Instant Quick-Paste for top items #1 through #9 |
| `Ctrl + A` | Card List | Select all visible items |
| `Ctrl + C` | Card List | Copy selected entry to clipboard without pasting |
| `Ctrl + P` | Card List | Toggle Pin / `Never Auto Delete` |
| `F2` | Card List | Open `EntryEditDialog` to edit title, aliases, and tags |
| `F5` | Search Box / Card List | Reload the history snapshot from SQLite |
| `Space` | Card List | Toggle the highlighted card's checkbox |
| `Delete` | Card List | Delete active or all checked entries |
| `Esc` | Anywhere | Clear batch selection if active; otherwise hide window |

---

## 5. WinForms Context Menu & Dialog Invariants

- **`ShortcutKeyDisplayString`:**
  WinForms context menu items must NEVER concatenate shortcut strings manually into `Text` (e.g. `"Paste\tEnter"`), which causes corrupt Unicode music note glyphs (`♫`). Always assign clean accelerator text to `ToolStripMenuItem.ShortcutKeyDisplayString = "Enter"`.
- **Modal Dialog Ownership:**
  Every dialog (`OptionsDialog`, `EntryEditDialog`) must execute:
  ```csharp
  bool wasTopMost = TopMost;
  try
  {
      TopMost = false;
      using var dialog = new EntryEditDialog(entry) { Owner = this };
      dialog.ShowDialog(this);
  }
  finally
  {
      TopMost = wasTopMost;
  }
  ```

---

## 6. v2.3.1 Interaction Invariants

- **Never rebuild the list from a clipboard event.** `HandleClipboardUpdate` writes to SQLite, sets `_snapshotDirty`, and updates a status counter. `ReloadSnapshot` is the only read path.
- **Never reorder on recopy.** `DeduplicationMode.KeepPosition` updates `CopyCount`/`LastCopiedAt` only. `Seq` is immutable for the row.
- **Never auto-select or reset `TopIndex`** during a rebuild when the previous entry still exists.
- **Never start drag when unarmed**, when Ctrl/Shift is held, from a checkbox, or on the second click of a double-click.
- **Never hide the app and send a blind Ctrl+V** when no valid target HWND exists; copy instead.
- **Never clear a batch selection before** the clipboard write has succeeded.
- **Dispose cached GDI fonts** in `ModernCardList.Dispose`; do not allocate fonts inside `OnDrawItem`.
- **Invalidate rows, not the whole list**, for hover/checkbox changes; only bulk operations may invalidate all.

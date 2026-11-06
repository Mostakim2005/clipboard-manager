[Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Decisions & Traps](DECISIONS_AND_TRAPS.md) · [Visual Guide](VISUAL_GUIDE.html) · [AI Bootstrap](../AI_BOOTSTRAP.md)

# UI / UX Design & Architecture Guide

ClipboardApp features a modern, fluid, keyboard-first desktop experience inspired by **Raycast**, **Maccy**, **Paste**, and the **Windows 11 Fluent Design System**.

---

## 1. Design Principles

* **Keyboard-First Efficiency:** Every common operation (search, filter, paste, quick paste, transform, delete, pin, copy, refresh, escape) can be performed without lifting your hands from the keyboard.
* **Stable, Predictable History:** The visible list is a snapshot. New copies are saved silently; the list does not reorder, reselect, or rebuild until you press F5/Refresh or reopen. Records have a permanent `Seq`, and cards show stable `#N` serials.
* **Tray-First by Default:** the app starts in the tray. The global hotkey toggles the window — first press shows and focuses the search box, second press hides it back to the tray. Closing with the X also hides to the tray; only tray → Exit quits.
* **Manual Drag Arming:** `✋ Drag` is OFF by default. It is a deliberate, session-only mode and disarms when you leave/minimize/hide/close, so it can never fight double-click or selection gestures.
* **Context-Aware Cards:** Instead of a rigid raw table, clipboard entries render as rich, rounded cards with type badges, quick paste indicators `[1]` to `[9]`, stable `#N` serials, source application attribution, relative timestamps, and formatted snippets.
* **Instant Filter Navigation:** Category pills at the top allow instant switching between formats (`All`, `Text`, `Images`, `Links`, `Files`, `Colors`, `Pinned`) with live item counts. A permanent Date / Type / App row adds precise filtering plus **Select all filtered** and **Delete filtered**.
* **Predictable, Opt-In Gestures:** Double-click paste is a separate toggle, OFF by default; when ON it always opens the target chooser first. Mixed text + image rich merging is also OFF by default and falls back to text with a clear warning when the destination cannot carry images.
* **Consistent, Switchable Styling:** All app-owned windows, menus, inline forms, and message boxes share one Fluent / Acrylic / Classic / Native style, controlled by a master toggle.
* **Live Inspector & Quick Actions:** The right panel immediately displays syntax-formatted text, interactive color swatches, image thumbnails with dimensions, and quick one-click action buttons.
* **Native Windows 11 Polish:** Immersive dark title bars (`DWMWA_USE_IMMERSIVE_DARK_MODE`), rounded corners (`DWMWA_WINDOW_CORNER_PREFERENCE`), and adaptive system theme detection.

---

## 2. Layout Structure

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│  📋 Clipboard App (Ctrl+`)                                              —  □  ✕        │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  🔍  Type to search history (↑↓ navigate, ↵ paste, Esc close)...    [ .* Regex ] [ ✕ ] │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [ All (24) ] [ 📝 Text (14) ] [ 🖼️ Images (4) ] [ 🔗 Links (3) ] [ 📁 Files (2) ] [ 📌 (3) ] │
├────────────────────────────────────┬───────────────────────────────────────────────────┤
│  CARDS LIST (50% Split, Checkboxes)│  RICH PREVIEW & INSPECTOR (50% Split)             │
│                                    │                                                   │
│  ┌──────────────────────────────┐  │  TEXT • VS Code • 2m ago • 4:12 PM                │
│  │ [✓] 📝 TEXT  Code Snippet [1]│  │  🏷️ #backend  #csharp                            │
│  │ export function calculate... │  │  ┌─────────────────────────────┐                  │
│  │ Code.exe • 2m ago • 📌       │  │  │ [📋 Copy] [⚡ Prettify JSON] │                  │
│  │ 🏷️ backend  🏷️ csharp       │  │  └─────────────────────────────┘                  │
│  └──────────────────────────────┘  │  ┌─────────────────────────────┐                  │
│  ┌──────────────────────────────┐  │  │                             │                  │
│  │ [ ] 🖼️ IMAGE Screenshot   [2]│  │  │                             │                  │
│  │ SnippingTool.exe • 1h ago    │  │  │   Zoomable High-Res Image   │                  │
│  │ 🏷️ screenshot                │  │  │                             │                  │
│  └──────────────────────────────┘  │  └─────────────────────────────┘                  │
├────────────────────────────────────┴───────────────────────────────────────────────────┤
│  [✓ All] [☐ Clear] [🗑️ Delete] [🏷️ Tags] [📋 Merge & Paste] [⇉ Paste Each] (Batch Bar) │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  ↵ Paste | ⇧↵ Plain | 1-9 Quick | F5 Refresh | Ctrl+C Copy | Del | Ctrl+P | Esc        │
│                                               [ ⟳ Refresh ] [ ✋ Drag: Off ] [ ⚙ ]   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Keyboard Shortcut Reference

| Shortcut | Action | Scope |
|---|---|---|
| **Global Hotkey** (`Ctrl + Backtick` by default) | Opens ClipboardApp centered near the active cursor / caret position | System-wide |
| **`Enter`** | Pastes the selected entry (or merged batch when 2+ checked) | In App |
| **`Shift + Enter`** | Pastes the selected entry as plain text (strips formatting) | In App |
| **`1` through `9`** | Instantly pastes items #1 through #9 | In App |
| **`Ctrl + C`** | Copies the selected entry to the clipboard without pasting/hiding | In App |
| **`Ctrl + P`** | Pin / Favorite item (pinned items never get auto-deleted) | In App |
| **`F2`** | Opens the resizable Edit dialog for title, aliases, and tags | In App |
| **`F5`** | Reloads the history snapshot from SQLite | In App |
| **`Delete`** | Deletes active item (or batch deletes all checked items) | In App |
| **`Shift + Click`** | Checks a contiguous range from the last anchor | In App |
| **`Ctrl + Click`** | Toggles an individual card's checkbox without clearing others | In App |
| **`Space`** | Toggles the highlighted card's checkbox | In App |
| **Mouse Drag on Blank Area** | Marquee/rubberband multi-selection (Ctrl preserves the existing set) | In App |
| **`✋ Drag` footer toggle** | Arms OLE drag-out; auto-disarms on deactivate/minimize/hide/close and after one drag | In App |
| **Grid / List + Preview footer button** | Switches presentation live; the label shows the mode you will switch *to*. The choice is remembered across restarts | In App |
| **Settings → Global Hotkey → Set** | Enters key-capture mode; press the combination you want. `Esc` cancels, **Reset** restores the default | Settings |
| **`Esc`** | Clears the active multi-selection; otherwise hides the window | In App |
| **`Up / Down Arrows`** | Navigates through cards (group headers are skipped) | In App |
| **Right-Click** | Context menu: Copy, Paste, Paste plain, Pin, Tags, Edit, Transforms, Delete, **Customize this menu...** | In App |

---

## 3.1 The “FD” Toggle (Fresh Data / Confirmed-Copy Refresh)

**Where:** Settings → General → **“Refresh list after a confirmed copy (FD: fresh data)”**.

**What it does:** after the clipboard monitor writes a new clip to SQLite, the app refreshes the visible list **once** (150 ms coalesced). It is not a live poll.

**Example:**
- The list has 8 clips; card #5 is selected.
- You copy new text `X` in another app.
- ON: `X` is saved, the list refreshes once, `X` appears at `#1`, the previous #1–#8 keep their relative order, and your selection stays on the same card (now #6).
- OFF: `X` is still saved, but the list does not change. The footer says `"1 clipboard capture since refresh — press F5"`; press F5 or reopen to see it.
- Recopying an existing clip under **Keep position** does not move or duplicate it either way.
- File Drop is a separate **FD**: **“Capture copied files (File Drop / FD)”**. ON stores an Explorer file selection as one clip; OFF ignores file copies.

The persistent help panel at the bottom of the Settings dialog shows the same example when the toggle is hovered or focused.

---

## 3.2 Permanent Filters, Custom Menus & Styling (v2.5.0)

**Permanent filter row.** Under the type pills, the Date (`Any`, `Today`, `Yesterday`, `Past 7 days`, `Past 30 days`, `Older`), Type (`Any`, `Text`, `Image`, `Link`, `File`, `Color`), and App selectors are always visible. When a filter is active, the row exposes:
- **Select all filtered (N)** — checks only currently visible rows.
- **Delete filtered** — deletes only currently visible rows; hidden checked items are left alone and confirmed first when batch-delete confirmation is enabled.
- **Clear filters** — resets all three selectors.

The App list is learned from `SourceApp` and refreshes after reload, capture, and filtered deletion.

**Custom right-click menu.** Right-click → **Customize this menu...** opens the persisted editor. Check/uncheck commands to show or hide them, use **Move Up/Down** to reorder, **Show All** to restore visibility, and **Reset Order** to return to the default. `Customize this menu...` is always last and cannot be hidden. Settings also exposes a Customize button under Interactions.

**UI styles.** Settings → Appearance → **Apply custom styling to app windows and menus** plus the **Visual style** selector (`Fluent`, `Acrylic`, `Classic`, `Native`). Fluent is the default. `Native` restores standard Windows chrome/colors for the main window and app-owned dialogs and menus. The master toggle restores native colors without restarting.

**Tag / alias autocomplete.** As you type in the editor's Tags or Aliases fields, the inline Assign Tags prompt, or the API-key Tags field, a small suggestion popup ranks previously used tokens by frequency. `Enter`/`Tab` accepts, `Up`/`Down` cycles, `Esc` dismisses, and clicking a suggestion inserts it. Text is never modified until you accept a suggestion.

---

## 4. Modern Color Palette

### Dark Mode (Zinc / Slate Theme)
* **Window Surface:** `#18181B` (Zinc-900)
* **Card Normal:** `#27272A` (Zinc-800) with `#36363C` border
* **Card Hover:** `#3F3F46` (Zinc-700)
* **Card Selected:** `#312E81` (Indigo-900) with `#38BDF8` (Sky-400) border
* **Search / Text Area:** `#202024` with `#F4F4F5` primary text
* **Active Category Pill:** `#0284C7` (Sky-600) with white text

### Light Mode (Clean Slate Theme)
* **Window Surface:** `#F8FAFC` (Slate-50)
* **Card Normal:** `#FFFFFF` (Pure white) with `#E2E8F0` border
* **Card Hover:** `#F1F5F9` (Slate-100)
* **Card Selected:** `#E0F2FE` (Sky-100) with `#0284C7` border
* **Search / Text Area:** `#FFFFFF` with `#0F172A` primary text
* **Active Category Pill:** `#0284C7` (Sky-600) with white text

---

## 5. Rich Inspector Capabilities

1. **Text & JSON Inspection:**
   * Clean monospace rendering with word wrap.
   * "⚡ Prettify JSON" action button formats unindented JSON in place.
   * Quick "🔠 UPPER" and "🔡 lower" transform actions.
2. **Color Swatch Inspector:**
   * Auto-detects 3, 6, and 8 digit hex color codes (e.g. `#0284C7`).
   * Renders a large live color swatch tile showing Hex, RGB, and contrasting values.
   * One-click "📋 Copy Hex" button.
3. **Image Inspector:**
   * Renders scaled thumbnail against a dark background.
   * Displays dimensions (`W × H px`) and file size.
   * One-click "📋 Copy Image" and "💾 Save As..." (PNG, JPEG, BMP).
4. **Link Inspector:**
   * Clickable link button that opens URLs in the default system browser with domain attribution.
5. **File Drop Inspector:**
   * Displays copied files with file paths and line breaks.
---

## 3.3 View Modes (v3.1.0)

Two presentations share one snapshot, one selection model, and one set of commands.

**List + Preview** (default) — the classic layout: card list on the left, inspector on the right, splitter ratio remembered.

**Grid** — tiles across the full window, no inspector pane:
- Text tiles are one row tall; **image tiles are about twice as tall** so a real thumbnail fits.
- Thumbnails are **letterboxed**: the source aspect ratio is preserved and the image is never stretched or cropped.
- Columns auto-fit to the window width, or fix them at 1–10 in **Settings → Appearance → Grid Columns**.
- Group headers (Today / Yesterday / by app / by type) still render as full-width separators.
- Checkboxes, `Ctrl`/`Shift`/marquee selection, `Space`, `Ctrl+A`, drag-out, the right-click menu, and every keyboard shortcut behave exactly as in the list.

Switch with the footer button; its label always shows the mode you will switch **to**. The choice is saved immediately and restored on next launch.

---

## 3.4 Transform Preview (v3.1.0)

The inspector's transform buttons (`UPPER`, `lower`, `slug`, `camel`, `snake`, `Prettify JSON`) no longer rewrite the stored clip the moment you click them.

1. Clicking a transform stages the result and the header shows **PREVIEW — changes not saved**.
2. You can stack several transforms; **Undo** walks back one step at a time (up to 20).
3. **Save Changes** commits once, through the repository. **Cancel** discards and restores the stored text.
4. Selecting a different entry abandons an unsaved preview — nothing was ever written, so there is nothing to recover.

Copy actions (`Copy Text`, `Copy Link`) copy whatever is currently previewed, so you can use a transform without saving it.
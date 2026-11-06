[Architecture](../ARCHITECTURE.md) · [Decisions & Traps](../DECISIONS_AND_TRAPS.md) · [AI Bootstrap](../../AI_BOOTSTRAP.md)

# Subsystem Dossier 01: Pasting & Target Window Focus

> **Component Scope:** `src/ClipboardApp.Core/Services/PasteService.cs`, `src/ClipboardApp.Core/Interop/TargetWindowTracker.cs`, `src/ClipboardApp.Core/Interop/NativeMethods.cs`, and `src/ClipboardApp.WinForms/MainForm.cs` (pasting pipeline).

---

## 1. High-Level Architectural Flow

```
User Action (Enter / Double-Click / Merge & Paste)
  │
  ├── 1. Resolve Target (TargetWindowTracker ring → _lastActiveWindow → topmost client)
  │       Reject own/shell/invalid HWNDs
  ├── 2. No target?  → CopyEntry to clipboard, show status, stay visible (never blind paste)
  ├── 3. Suppress Internal Clipboard Capture for 1.8 s
  ├── 4. Hide MainForm and yield ~50 ms for the deactivation transition
  ├── 5. Write the correct payload (Unicode / CF_HTML / RTF / DIB+PNG / HDROP)
  │       Abort if SetClipboardWithRetry fails
  └── 6. Activate target, verify foreground, then SendInput Ctrl+V
          If focus cannot be verified, do not send keys; notify via tray
```

---

## 0. Interaction Contract (v2.3.1)

- **Double-click** is detected in `ModernCardList.OnMouseDown` using `TickCount64`, `SystemInformation.DoubleClickTime`, and `DoubleClickSize`. It does not depend on `ListBox.DoubleClick`, and it cannot be hijacked by the drag threshold.
- **Manual drag arming** is required by default. The footer `✋ Drag: Off/Armed` toggle is the only way to enable drag-out. It disarms on `Deactivate`, minimize, hide/close, after one drag, and whenever the window is shown.
- **Stable history snapshot:** pasting never reloads SQLite or rearranges the list. Only `F5`/`⟳ Refresh`/reopen (when dirty) reloads the snapshot. See `MainForm.ReloadSnapshot`.
- **Selection is never cleared until the paste payload has been written successfully.**

---

## 2. Critical Failure Modes & Engineering Mitigations

### A. Physical Key State Collision (The "Stuck Enter / Shift" Bug)
* **Root Cause:** When the user triggers paste by pressing `Enter` in the search box or list, or `Shift+Enter` for plain text, the user's physical finger is still pressing the physical key while `SendInput` executes `Ctrl+V`. Modern Windows keyboard drivers combine physical key states with synthetic input, resulting in `Ctrl+Enter` or `Shift+Ctrl+V`, which target applications (Notepad, Chrome, VS Code) either ignore or interpret as newlines instead of pasting.
* **Mitigation:**
  `PasteService.WaitForKeysReleased(VK_RETURN, VK_SHIFT, VK_CONTROL, VK_MENU, VK_LBUTTON)` continuously checks `GetAsyncKeyState()` with a small spin-wait (up to 350 ms) until physical keys are released before any synthetic input is sent.

### B. Shell & Tray Focus Stealing
* **Root Cause:** When ClipboardApp is opened from the system tray icon, Windows activates `Shell_TrayWnd`. If the user immediately clicks an entry, `GetForegroundWindow()` returns the Windows Taskbar rather than the actual document window.
* **Mitigation:**
  `TargetWindowTracker` maintains an 8-slot ring-buffer populated by `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`. It filters out known shell classes:
  - `Shell_TrayWnd`, `Shell_SecondaryTrayWnd` (Taskbar)
  - `Progman`, `WorkerW` (Desktop)
  - `TaskSwitcherWnd` (Alt-Tab overlay)
  - `Windows.UI.Core.CoreWindow` (Start menu / search overlay)
  When queried, `GetLastActiveClientWindow()` returns the most recent *verified non-shell client window*.

### C. Asynchronous UI Thread Deactivation
* **Root Cause:** Calling `this.Hide()` marks the form as hidden, but the WinForms message loop has not yet processed `WM_KILLFOCUS` and `WM_ACTIVATE`. Running synchronous `SetForegroundWindow` on the target immediately locks the input queue.
* **Mitigation:**
  `MainForm` uses `BeginInvoke` or a small asynchronous delay (30–50ms) to allow the OS to complete the window hiding transition before attempting to bring the target window to the foreground.

---

## 3. Multi-Item Group Pasting Architecture

When multiple items are checked via checkboxes, `Ctrl+Click`, `Shift+Click`, or blank-area marquee:

### A. Merged Batch Paste (Default)
- `BatchPasteService.Build` extracts entries in **visual list order** and joins text with the configured separator:
  1. `Double Newline` (`\r\n\r\n`) — default.
  2. `Single Newline` (`\r\n`).
  3. `Comma Space` (`, `).
- **Pure file selection:** all files are aggregated into one `HDROP` list; pasted as a file drop.
- **Images:** existing image entries are written once to `%TEMP%\ClipboardApp\Drops\clip_<entryId>.png` (stable path; repeated drags/pastes reuse the same file) and included as paths.
- **Mixed content:** paths and text are merged deterministically; file-only fallback is not used when any text entry is present.
- **No duplicates:** identity is type-prefixed SHA-256 (`ClipboardContentIdentity`); merged payloads are temporary and never inserted into history.

### B. Sequential Multi-Paste (Opt-in)
- Enable `SequentialBatchPaste` in Settings > Interactions to show `⇉ Paste Each`.
- Writes each checked entry to the clipboard, sends one Ctrl+V, waits 180 ms, then advances in visual order.
- Default remains merged single-paste; sequential mode is not hard-coded because some apps mishandle repeated Ctrl+V.

### C. Selection Contract
- Checked IDs are pruned to visible rows after every rebuild.
- `Enter` with 2+ checked items performs merged batch paste; `Enter` with one checked item pastes that item.
- Double-click pastes the exact hit-tested card (or the checked batch when 2+ are checked).
- Checks clear only after a successful clipboard write/paste.

### D. Order Stability
- SQLite `Seq` assigns a permanent sequence at insert. `GetAllEntries` sorts `Pinned DESC, Seq DESC`.
- Recopying an entry under `KeepPosition` updates `CopyCount`/`LastCopiedAt` only; it never moves the card or creates a duplicate.
- `MoveToTop` and `AllowDuplicates` are explicit settings; `KeepPosition` is the default.

---

## 4. Win32 Keystroke Synthesis Specification

Synthetic pasting uses four `INPUT` packets with virtual-key codes plus scan-code metadata:

```csharp
ushort ctrlScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_CONTROL, NativeMethods.MAPVK_VK_TO_VSC);
ushort vScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_V, NativeMethods.MAPVK_VK_TO_VSC);

// 4 INPUT packets: Ctrl Down -> V Down -> V Up -> Ctrl Up
// KEYEVENTF_KEYUP is set for the release packets.
```

`dwFlags` intentionally stays at virtual-key mode (not `KEYEVENTF_SCANCODE`) for IME/Electron/Win32 compatibility; scan values are still populated for drivers that inspect them. The result of `SendInput` must equal 4, otherwise the paste is treated as failed.

**Foreground verification:** after `SetForegroundWindow`/`BringWindowToTop`/`SetActiveWindow`, `IsForegroundTarget()` compares the foreground HWND and its `GA_ROOT` ancestor. Up to four 45 ms attempts are made. If focus cannot be verified, no keystrokes are sent.

*Note: Never use `SendKeys.SendWait("^v")` in a production clipboard manager; `SendKeys` relies on Windows journal hooks which can be blocked by UAC elevation levels.*

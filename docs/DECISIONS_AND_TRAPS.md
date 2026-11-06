# Decisions & Traps (`DECISIONS_AND_TRAPS.md`)

> **1-Line Summary:** The engineering memory of ClipboardApp — what was tried and rejected, who made each call, and the non-obvious landmines that will silently break the app if touched.

**How to read this file:** `DECISION` = a fork in the road we already resolved. `TRAP` = a piece of code or behaviour that looks safe to change but is not. `VERIFY` = a check that must pass before release. Every entry carries a `[DECIDED BY: HUMAN]` (product owner) or `[DECIDED BY: AGENT]` (engineering) tag.

---

## 1. Product & UX Decisions

### [DECISION: Local-Only-No-Telemetry] | Aliases: privacy, offline, no cloud, no account
> **1-Line Summary:** ClipboardApp is local-only by design; no network calls, accounts, analytics, or cloud sync exist in the shipping product.
- **What We Tried / Considered:** A hosted sync backend and optional usage analytics to understand feature adoption.
- **Why It Was Rejected:** The product's entire value proposition is that clipboard contents never leave the machine. A sync server would add breach surface, cost, and a login flow for zero current user demand.
- **Final Call:** Local SQLite + Windows DPAPI only. Cloud sync remains a documented wishlist item that must be end-to-end encrypted and opt-in before any code is written.
- **Consequence Today:** Any new dependency that phones home is a release blocker.
- [DECIDED BY: HUMAN]

### [DECISION: Portable-Exe-Is-The-Default-Deliverable] | Aliases: no installer, zero install, single file, releases
> **1-Line Summary:** The portable single-file `.exe` is the primary release; the setup wizard is built on demand and must be provably identical to the portable payload.
- **What We Tried / Considered:** Shipping only an installer, or shipping both with equal status.
- **Why It Was Rejected:** Users want to run the app from a USB stick or a downloads folder without admin rights. The installer adds friction and a second artifact to verify.
- **Final Call:** Portable first. The installer, when built, must embed the exact portable bytes and self-verify with `--verify-payload`.
- **Consequence Today:** `tools/build_releases.ps1` builds portable by default; `-BuildInstaller` is opt-in. `releases/` is gitignored and GitHub Actions produces the portable in the cloud.
- [DECIDED BY: HUMAN]

### [DECISION: Risky-Gestures-Are-Opt-In] | Aliases: double-click paste, mixed image merge, safe defaults
> **1-Line Summary:** Double-click paste and mixed text+image rich merging are OFF by default and must be explicitly enabled by the user.
- **What We Tried / Considered:** Making double-click paste the default card activation, and always embedding images in rich merges.
- **Why It Was Rejected:** Both silently change what lands in another application. A mis-timed double-click can paste into the wrong window; not every rich-text target accepts embedded images.
- **Final Call:** Separate toggles, default OFF. Double-click, when enabled, always opens the target chooser first. Mixed merge warns and deselects images when the target cannot carry them.
- **Consequence Today:** Settings holds `EnableDoubleClickPaste` and the mixed-merge toggle; both are covered by UI smoke tests.
- [DECIDED BY: HUMAN]

### [DECISION: Stable-Snapshot-List-Over-Live-Reordering] | Aliases: list stability, no jump, frozen view, FD refresh
> **1-Line Summary:** The visible list is a snapshot. New captures are saved in the database but never reorder or rebuild the list the user is reading.
- **What We Tried / Considered:** Live re-sorting the list on every clipboard event, which is what most clipboard managers do.
- **Why It Was Rejected:** The list jumped under the cursor mid-selection and mid-read, causing wrong-item pastes. That was a real usability failure, not a theoretical one.
- **Final Call:** Capture writes only. `ReloadSnapshot` is the single full-read path. A single coalesced "fresh data" refresh after a confirmed copy is the one deliberate exception, and it preserves selection and `TopIndex`.
- **Consequence Today:** Any new feature that calls `ReloadSnapshot` on a timer or on every capture breaks the core contract.
- [DECIDED BY: HUMAN]

### [DECISION: Manual-Drag-Arming] | Aliases: drag safety, armed drag, accidental drag
> **1-Line Summary:** Drag-out requires an explicit `✋ Drag` arm and disarms after one drag or when the window loses focus.
- **What We Tried / Considered:** Native drag on mouse-down, like Explorer.
- **Why It Was Rejected:** It stole clicks, checkbox toggles, marquee selection, and double-click activation. Accidental drags also carried data into the wrong app.
- **Final Call:** Manual arming plus an 8 px movement threshold before a drag begins.
- **Consequence Today:** `ModernCardList.DragArmed` and `MainForm.Paste.StartOleDrag` are load-bearing. Do not remove the arm gate to "simplify" drag.
- [DECIDED BY: HUMAN]

### [DECISION: Target-Verified-Paste-Instead-Of-SendKeys] | Aliases: no blind paste, focus verification, SendInput
> **1-Line Summary:** Pasting writes the clipboard, hides the window, verifies the target owns the foreground, and only then sends `Ctrl+V` through `SendInput`; otherwise it copies instead.
- **What We Tried / Considered:** `SendKeys.SendWait("^v")` after hiding the window, with no foreground check.
- **Why It Was Rejected:** `SendKeys` depends on journal hooks and message pumping, and it pasted into whatever window happened to be focused — including the wrong document.
- **Final Call:** Raw `SendInput` with a 40-byte x64 `INPUT` struct, explicit foreground/root-window verification, and a wait for physical modifier keys to be released. Uncertain outcome = copy, keep window visible, reopen the target picker.
- **Consequence Today:** `PasteService.PasteEntry`, `TargetWindowTracker.IsValidTarget`, and `MainForm.Paste.ResolveTargetWindow` form the safety chain.
- [DECIDED BY: AGENT]

---

## 2. Architecture Decisions

### [DECISION: WinForms-Owner-Drawn-UI-Not-Webview] | Aliases: no Electron, no WebView2, native rendering
> **1-Line Summary:** The UI is Windows Forms with owner-drawn cards and a small custom styling layer, not a web view or Electron shell.
- **What We Tried / Considered:** A WebView2 or Electron-style shell for easier theming.
- **Why It Was Rejected:** Startup time, memory footprint, and a second runtime to patch. The app must open instantly from a hotkey and sit quietly in the tray.
- **Final Call:** WinForms plus `ModernCardList`, `GridCardList`, `RichPreviewPanel`, and `UiStyler`.
- **Consequence Today:** Visual changes must be done through the palette/styler layer, not by introducing HTML.
- [DECIDED BY: HUMAN]

### [DECISION: One-Version-Source-Of-Truth] | Aliases: version bump, Directory.Build.props, no hard-coded versions
> **1-Line Summary:** The version number exists once, in `Directory.Build.props`, and flows into the app, installer, and tests.
- **What We Tried / Considered:** Hard-coding `3.0.0` in three project files and a default in the packaging script.
- **Why It Was Rejected:** The app, installer, and tests drifted apart, and release notes disagreed with the shipped binary.
- **Final Call:** Single `Version` property. `tools/bump-version.ps1` is the only supported way to change it.
- **Consequence Today:** `VersionTests` guards the wiring. Adding a version string anywhere else is a defect.
- [DECIDED BY: AGENT]

### [DECISION: Separate-Encrypted-Api-Key-Vault] | Aliases: API keys, secrets store, master password
> **1-Line Summary:** API keys live in their own AES-256-GCM store behind a DPAPI-wrapped key with an optional master password and timed unlock — never in clipboard history.
- **What We Tried / Considered:** Storing keys as ordinary clipboard entries with a "sensitive" flag.
- **Why It Was Rejected:** History is searchable, exportable, and batch-copyable. A key stored there leaks through normal use.
- **Final Call:** Dedicated repository and modeless dialog, idle auto-lock, key material zeroed on close, and capture suppression when a key is copied.
- **Consequence Today:** `ApiVaultService` and `ApiKeyRepository` are isolated from `SqliteClipboardRepository` on purpose.
- [DECIDED BY: HUMAN]

### [DECISION: Type-Prefixed-Content-Hash-For-Dedupe] | Aliases: SHA-256 identity, duplicate detection, dedupe modes
> **1-Line Summary:** Duplicate detection uses a type-prefixed SHA-256 identity, and the user chooses Keep Position, Move To Top, or Allow Duplicates.
- **What We Tried / Considered:** Raw MD5 over text only, and a single forced dedupe behaviour.
- **Why It Was Rejected:** Text-only hashing collapsed distinct image/file clips, and one forced policy annoyed both kinds of user.
- **Final Call:** `ClipboardContentIdentity` produces a format-prefixed hash; the repository owns the ordering policy.
- **Consequence Today:** Only the repository decides duplicates. Do not dedupe in the UI or in `ClipboardMonitor`.
- [DECIDED BY: AGENT]

---

## 3. Engineering Traps (Do Not Touch)

### [TRAP: Never-Set-UserPaint-On-ModernCardList] | Aliases: blank list, invisible items, WM_DRAWITEM
> **1-Line Summary:** Setting `ControlStyles.UserPaint` on `ModernCardList` suppresses the native owner-draw path and renders a blank list until the mouse hovers.
- **Symptom:** Cards vanish; hovering or resizing makes them reappear.
- **Root Cause:** The control relies on the native `ListBox` `WM_DRAWITEM` pipeline. `UserPaint` replaces it with a paint path the override does not implement.
- **Rule:** Never add `SetStyle(ControlStyles.UserPaint, true)` to `ModernCardList`. This bug already shipped once and was fixed in v2.3.1.
- **Anchor:** `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> constructor / `OnDrawItem`
- [DECIDED BY: AGENT]

### [TRAP: Input-Struct-Must-Be-40-Bytes-On-x64] | Aliases: SendInput fails, paste does nothing, INPUT size
> **1-Line Summary:** `INPUT`/`InputUnion` must size to 40 bytes on x64; a 32-byte layout makes `SendInput` silently return 0.
- **Symptom:** Paste appears to succeed but nothing is pasted; `SendInput` returns 0 with no exception.
- **Root Cause:** The union's largest member (`MOUSEINPUT`) determines the struct size. Getting the layout wrong corrupts the call.
- **Rule:** Keep `InteropLayoutTests` passing. Do not reorder or shrink the union.
- **Anchor:** `src/ClipboardApp.Core/Interop/NativeMethods.cs` -> `INPUT`, `InputUnion`, `SendInput`
- [DECIDED BY: AGENT]

### [TRAP: Self-Capture-Loop] | Aliases: duplicate paste, infinite clipboard event, echo
> **1-Line Summary:** Writing the clipboard inside the app triggers `WM_CLIPBOARDUPDATE`; without suppression it re-captures the entry and can loop.
- **Symptom:** Duplicate entries after every paste, or a steadily growing copy count / history.
- **Root Cause:** Windows reports the app's own clipboard writes as normal changes.
- **Rule:** Always set the self-write suppression flag around `Clipboard.SetDataObject` / `SetText`, and clear it on failure too. `ClipboardMonitor` must honour the flag.
- **Anchor:** `src/ClipboardApp.Core/ClipboardMonitor.cs` -> suppression state; `src/ClipboardApp.Core/Services/PasteService.cs` -> `CopyEntry`, `CopyRich`
- [DECIDED BY: AGENT]

### [TRAP: Double-Click-Activation-Timing] | Aliases: form hides mid-click, duplicate activation, mouse-up
> **1-Line Summary:** Double-click must be detected inside the card control and activated on the second mouse-up, not on `MouseDown`.
- **Symptom:** The window hides mid-gesture, the click is lost, or the entry pastes twice.
- **Root Cause:** Activating on the first event hides the window before the OS finishes the double-click sequence.
- **Rule:** Keep the detection in `ModernCardList`/`GridCardList` and the activation on mouse-up. Do not move it into a form-level `DoubleClick` handler.
- **Anchor:** `src/ClipboardApp.WinForms/Controls/ModernCardList.cs` -> mouse handling; `src/ClipboardApp.WinForms/MainForm.Paste.cs` -> `OnCardActivated`
- [DECIDED BY: AGENT]

### [TRAP: Selection-Must-Survive-Rebuild] | Aliases: checked items lost, filter clears selection, batch bar
> **1-Line Summary:** Checked IDs are kept outside the list and pruned to existing rows, so refresh and filtering never silently clear a multi-selection.
- **Symptom:** The batch bar disappears, or `Delete selected` deletes nothing after a refresh.
- **Root Cause:** Rebuilding the list recreates item objects; storing selection on the item loses it.
- **Rule:** Keep selection as IDs in the form/accessor, and prune against the current snapshot. `Delete filtered` must only touch visible filtered IDs.
- **Anchor:** `src/ClipboardApp.WinForms/MainForm.cs` -> selection set; `src/ClipboardApp.WinForms/MainForm.Filters.cs` -> `DeleteFiltered`
- [DECIDED BY: AGENT]

### [TRAP: Context-Menu-Must-Always-Be-Recoverable] | Aliases: hidden commands, customize last, lockout
> **1-Line Summary:** `Customize this menu...` is non-hideable and always last, so a user can never lock themselves out of the right-click menu.
- **Symptom:** A user hides commands and has no way to restore them.
- **Root Cause:** Persisted order/hidden lists are applied blindly on startup.
- **Rule:** `ContextMenuCommands.NormalizeHidden` must force `customize` visible, and `NormalizeOrder` must force it last. Any new command must be added to `All`.
- **Anchor:** `src/ClipboardApp.WinForms/UI/ContextMenuCommands.cs` -> `All`, `NormalizeOrder`, `NormalizeHidden`
- [DECIDED BY: HUMAN]

### [TRAP: Views-Must-Stay-Command-Identical] | Aliases: list vs grid, parity, accessor
> **1-Line Summary:** Every command goes through `MainForm.Accessor`; branching on `ViewMode` at each call site causes list/grid behaviour drift.
- **Symptom:** A command works in list view but does nothing, or acts on the wrong item, in grid view.
- **Root Cause:** Two different controls with different item models.
- **Rule:** Add a method to the accessor instead of an `if (ViewMode == ...)` at the call site. Any new card action must be implemented for both controls and covered by a test.
- **Anchor:** `src/ClipboardApp.WinForms/MainForm.cs` -> `Accessor`; `src/ClipboardApp.WinForms/Controls/GridCardList.cs`
- [DECIDED BY: AGENT]

### [TRAP: Encoding-Drift-Corrupts-Emoji-And-Quotes] | Aliases: mojibake, UTF-8, Windows-1252, box drawing
> **1-Line Summary:** Every source and doc file is UTF-8; a Windows-1252 round-trip corrupts emoji, curly quotes, and box-drawing characters used in the UI and diagrams.
- **Symptom:** Garbled symbols in the UI, Mermaid diagrams, or docs; tests comparing strings start failing.
- **Root Cause:** PowerShell or an editor reading/writing with the legacy ANSI code page.
- **Rule:** Keep UTF-8. Run `tools/fix-encoding.ps1` to repair damage; do not hand-edit the corrupted bytes.
- **Anchor:** `tools/fix-encoding.ps1`
- [DECIDED BY: AGENT]

### [TRAP: Release-Folder-Is-Not-Source] | Aliases: no binaries in git, releases, publish, gitignore
> **1-Line Summary:** `releases/` and `publish/` are build outputs and stay out of Git; the portable binary is produced by CI and attached to a GitHub Release.
- **Symptom:** Repo size explodes; clones slow; a stale binary gets shipped.
- **Root Cause:** Committing locally built artifacts because they were convenient.
- **Rule:** Never `git add -f` a `.exe`, `.dll`, `.zip`, or `releases/`. Build from source or download from Releases.
- **Anchor:** `.gitignore`; `.github/workflows/build.yml`
- [DECIDED BY: HUMAN]

---

## 4. Verification Gates

### [VERIFY: Build-Clean-And-Test-Floor] | Aliases: 116 tests, zero warnings, release gate
> **1-Line Summary:** A release requires a warning-free Release build and the full test suite (currently 116 tests) passing — a shrinking suite blocks the build.
- **Command:** `dotnet build ClipboardApp.sln -c Release` then `dotnet test ClipboardApp.sln -c Release`
- **Expected:** 0 warnings, 0 errors, 116/116 passed.
- **Why It Is A Gate:** Silent test deletion and warning creep were both observed before this rule existed.
- **Anchor:** `tools/build_releases.ps1` -> test-count floor; `tests/ClipboardApp.Tests/`
- [DECIDED BY: HUMAN]

### [VERIFY: Installer-Payload-Byte-Identity] | Aliases: verify release, payload.bin, checksums
> **1-Line Summary:** The setup installer must embed the exact portable binary, and the release must verify with byte-identical payload and matching SHA-256 checksums.
- **Command:** `.\tools\verify-release.ps1`
- **Expected:** Payload byte-identity confirmed; stamped version matches `Directory.Build.props`; checksums match `SHA256SUMS.txt`.
- **Why It Is A Gate:** A rebuilt installer previously carried a stale payload, so the "portable" and "installed" builds behaved differently.
- **Anchor:** `tools/verify-release.ps1`; `tools/pack.ps1`
- [DECIDED BY: AGENT]

### [VERIFY: No-Secrets-No-Binaries-In-Git] | Aliases: secret audit, staged files, push safety
> **1-Line Summary:** Before every push, confirm no `.env`, key file, credential, or heavy binary is staged.
- **Command:** `git status --short` plus a targeted file search for `.env`, `*.pem`, `*.key`, `*.pfx`, `*.exe`, `node_modules/`, `releases/`, `publish/`.
- **Expected:** Only source, configuration, `.gitignore`, `AGENTS.md`, and `docs/` are staged.
- **Why It Is A Gate:** The repository is public and permanent; history cannot be un-published safely.
- **Anchor:** `.gitignore`; `AGENTS.md` § 4
- [DECIDED BY: HUMAN]

---

*Related: [Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Visual Guide](VISUAL_GUIDE.html) · [Build Pipeline](BUILD.md) · [Handoff](HANDOFF.md)*

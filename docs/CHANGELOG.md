[Product Intent](PRODUCT.md) · [Architecture](ARCHITECTURE.md) · [Decisions & Traps](DECISIONS_AND_TRAPS.md) · [Visual Guide](VISUAL_GUIDE.html) · [AI Bootstrap](../AI_BOOTSTRAP.md)

# Changelog

Every released version is recorded here. The version number itself is defined once, in
[`Directory.Build.props`](../Directory.Build.props), and follows the semantic versioning policy
in [`BUILD.md`](BUILD.md): **MAJOR** for breaking/architectural changes, **MINOR** for moderate
features or non-breaking behaviour changes, **PATCH** for minor improvements, small fixes, and
corrections.

---

## License change — 2026-11-06

- **Project license is now PolyForm Noncommercial 1.0.0** (was MIT). Personal, hobby,
  research, educational, charity, and government use remain free. Commercial use now
  requires a separate license from the author.
- **`LICENSE` now carries the third-party attribution record.** It lists every NuGet
  component resolved by the build (Microsoft.Data.Sqlite, SQLitePCLRaw, SQLite, System.Memory,
  the .NET runtime, Microsoft.NET.Test.Sdk, Newtonsoft.Json, xUnit, and their transitive
  packages), states which ones ship inside the executable and which are test-only, and
  includes the full MIT and Apache-2.0 texts so the file is self-contained.
- **No copyleft dependencies.** Every third-party component is MIT, Apache-2.0, or public
  domain, so the noncommercial terms on ClipboardApp's own code do not conflict with them.
- `README.md` explains the permitted uses and the commercial-license path.

---

## 3.1.0 — Versioning foundation & UI batch 1

### Versioning, build and release integrity

- **Single version source of truth.** The version now lives only in `Directory.Build.props` and
  flows into the app, setup installer and tests. Removed hard-coded `3.0.0` values from three
  project files and the `2.5.0` default from the packaging script.
- **`tools/bump-version.ps1`** — one command bumps `major` / `minor` / `patch`, resets lower
  components (1.4.2 + minor → 1.5.0), and keeps the version strings in `README.md`,
  `docs/VISUAL_GUIDE.html` and `docs/HANDOFF.md` in sync.
- **Release pipeline hardened** (`tools/build_releases.ps1`): reads the version from the props
  file, enforces a minimum test count so a shrunken suite cannot ship, writes a
  `sha256sum`-compatible `SHA256SUMS.txt` (was an unparseable PowerShell table) plus a
  `RELEASE-v<Version>.json` manifest recording hashes, version, test count and git commit.
- **The setup installer is now reproducible and verifiable.** `tools/verify-release.ps1` extracts
  `payload.bin` from the installer, decompresses it, and proves it is byte-identical to the
  shipped portable; it also re-checks every checksum and the version stamped inside the exe.
  Previously the payload was gitignored, so a fresh clone could not rebuild the installer and
  nothing could prove the shipped one matched the portable.
- **Fixed the MSB3277 warning.** `Microsoft.Data.Sqlite` was pinned as `8.0.*`, which resolved to
  different versions on different machines and produced an assembly-unification warning. Now
  pinned to a single version; the solution builds with **0 warnings**.
- **Version stamped in the UI.** The window title, Settings dialog and tray now show the real
  build version (`Clipboard App v3.1.0 (Ctrl+F1)`) instead of nothing.

### UI batch 1

- **Starts in the tray.** `StartMinimizedToTray` now defaults to ON and is enforced after the
  window is fully initialised, so the clipboard monitor, tray icon and hotkey are all live
  before the window disappears.
- **Hotkey toggles the window.** The global shortcut now alternates: first press shows and
  focuses (search box focused), second press returns the window to the tray without quitting.
  The window reappears at the last position you used it, if that position is still on screen.
- **Shortcut capture replaces manual typing.** Settings → General no longer asks you to type
  `Ctrl+F1`. Click **Set**, press the combination, and it registers immediately; modifier keys
  update a live preview, a bare key is rejected, an already-taken combination is reported before
  you commit, and `Esc` cancels. A **Reset** button restores the default.
- **Vault: timed unlock + non-blocking window.** After one unlock the vault stays unlocked for
  `ApiVaultAutoLockMinutes` (default 5) so reopening it does not re-prompt. The vault is now a
  modeless, movable window: you can keep it open and work behind it. Re-invoking the Vault button
  focuses the existing window instead of opening a second one. The status bar shows the remaining
  unlock time, and manual **Lock** still clears the window immediately.
- **Image thumbnails in the list.** Image cards now paint a real, letterboxed screenshot
  thumbnail (aspect ratio preserved, never stretched) instead of only showing the file name.
  Thumbnails use an LRU-bounded cache, so scrolling a long image history cannot grow memory
  without limit.
- **New Grid view.** A footer button switches live between **List + Preview** (the existing card
  list with inspector) and **Grid** (tiles across the full width). Image tiles are about twice the
  height of text tiles so the thumbnail is visible; column count is auto-fit or configurable from
  1 to 10 in Settings → Appearance. The choice is remembered, and the button is labelled with the
  mode it switches *to*. Selection, filtering, grouping, drag arming, right-click menu and
  keyboard commands all behave identically in both views.
- **Text transforms are previews, not silent edits.** UPPER, lower, slug, camel, snake and
  Prettify JSON previously rewrote the stored clip immediately with no way back. They now stage
  the result: the header shows **PREVIEW — changes not saved**, and you get **Save Changes**
  (commits once, from the repository), **Cancel** (discards), and **Undo** (walks back through
  several applied transforms, up to 20 steps). Switching entries discards an unsaved preview.

### Verification

- `dotnet build` — **0 warnings, 0 errors**.
- `dotnet test` — **116/116 passing**, including new tests for hotkey parsing round-trips,
  version/assembly consistency, required settings defaults, the start-hidden/toggle-to-tray
  contract, grid/list parity, grid column clamping, and aspect-preserving thumbnails.

---

---

## Documentation restructure — 2026-11-06

- **Universal agent protocol adopted.** `AGENTS.md` is now the shared multi-agent operating
  directive (triage paths, grep-first docs, GitHub-first archiving, cloud builds).
- **`docs/INDEX.md` removed.** The numbered "Level 0–3" index is gone in favour of four
  self-describing documents plus subsystem dossiers. No line-number anchors remain.
- **New docs set.** `PRODUCT.md` (user intent and feature checklist), `ARCHITECTURE.md`
  (diagrams, flows, extension recipes), `DECISIONS_AND_TRAPS.md` (rejected paths, decision
  ownership, landmines), and `VISUAL_GUIDE.html` (self-contained interactive dashboard).
- **`HANDOFF.md` rewritten** as a concise current-state map instead of an accumulating release log.
- **`.gitignore` hardened** against secrets, dependencies, build outputs, editor state, and OS files.
- **GitHub Actions added** (`.github/workflows/build.yml`): builds and tests on every push and
  attaches the portable `.exe` plus `SHA256SUMS.txt` to a release on `v*` tags.

---

## 3.0.0 — API vault, grouping and security baseline

Baseline release: encrypted API-key vault (AES-256-GCM with a DPAPI-wrapped key and optional
PBKDF2 master password), group-header selection checkboxes, sensitive-content masking with
expiry, expanded AI key detection (DeepSeek, Claude, Gemini), and the Fluent UI refresh.

---

## Earlier versions

Older release notes (2.5.0, 2.4.0, 2.3.x, 2.2.0) live in the Git history and release tags. Run
`git log --oneline --decorate` or open the tagged releases on GitHub.

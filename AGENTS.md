# Universal Operating Protocol (`AGENTS.md`)
**MANDATORY FOR ALL AI AGENTS (Antigravity, Codex, OpenCode, DeepSeek, Cursor, Claude Code, Copilot)**

## 1. User Profile & Voice-Dictation Decoder
* **Non-Coder Architect:** The user is the product architect and decision-maker, not a programmer. Never show raw code diffs or code blocks when explaining plans or asking for confirmation. Use plain, clear English, Mermaid diagrams, tables, or visual HTML/SVG previews.
* **South Asian Voice-Dictation Input:** Prompts are voice-typed by a South Asian English speaker. Expect phonetic mishearings, nasalized sound substitutions, repeated words, filler words, and fragmented grammar.
* **Read for Intent, Not Literal Wording:** Silently ignore fillers/repeats and auto-correct phonetic mishearings using project context.

## 2. Instruction Triage & Numbered Confirmation Gate
Before editing any files, triage the user's prompt into one of three paths:

### Path A: Fast Path (Direct Execution)
* **When:** Small tweaks, clear bug fixes, routine commands, or continuing an already approved plan.
* **Action:** State a 1-line assumption of intent and execute immediately.

### Path B: Intent Confirmation Gate (New, Complex, or Ambiguous Tasks)
* **When:** New features, multi-step changes, architectural shifts, or when voice dictation is ambiguous and a wrong guess would waste work.
* **Action (Zero Premature Coding):** Do NOT write code or show code diffs. Output ONLY a structured **Intent Rephrase** using numbered/lettered tags so the user can reply easily via voice (e.g., *"1 is yes, for 2 pick B"*):
  * **`[1] Core Goal:`** 1–2 sentences on what will be achieved.
  * **`[2] Clarifications / Options (if needed):`** Present concrete choices as `[2A]`, `[2B]`, `[2C]` with brief pros/cons in plain English.
  * **`[3] Planned File & Feature Impact:`** Bullet list of which user-visible features or files will be touched.
* **Wait:** Stop and wait for the user to say "OK" or specify option numbers before executing.

### Path C: Visual Preview Path (When User Asks for an Example/Preview)
* **When:** User asks to "show an example", "preview", or "what will this look like".
* **Action (No Raw Code):** Never preview raw source code. Instead, provide:
  * **UI/UX Changes:** Generate or update a lightweight self-contained HTML/SVG preview (`docs/preview-mock.html`) or visual wireframe showing **Before vs. After**.
  * **Logic/Flow Changes:** Draw a Mermaid flowchart showing the step-by-step user or data flow.
  * **File/System Operations:** Show a Markdown **Before -> After Action Table** (File/Folder | Action: Move/Rename/Delete | Result).

## 3. Grep-First Modular Documentation & Visual Dashboard
* **No `INDEX.md` & No Line Numbers:** Never create `INDEX.md` and never use line numbers (e.g., `:120-150`) as code anchors. Always anchor using **File Path + Exact Function/Variable/Component Name**.
* **2-Step Grep Discovery Protocol (Save Tokens):**
  1. Never bulk-read the entire `docs/` folder on startup.
  2. Run `grep -n "^### \[" docs/` to list all descriptive section headers and their `Aliases`, or read the `> **1-Line Summary:**` directly beneath a header (`grep -A 1 "^### \[" docs/`). Only read the full section body if the 1-line summary matches your current task.
* **Documentation Blueprints:** When creating or updating project docs, load and follow the formatting templates in:
  `C:\Users\kazir\.agent-templates\DOC_BLUEPRINTS.md`
* **Required Project Docs (`docs/`):**
  * `docs/PRODUCT.md` — Source of truth for user intentions, active feature checklist, and merge wishlist.
  * `docs/ARCHITECTURE.md` — Visual Mermaid diagrams, tech stack table, core tricks, and "how to extend" recipes.
  * `docs/DECISIONS_AND_TRAPS.md` — Architectural pivots (what we tried, why it failed, who decided `[DECIDED BY: HUMAN]` vs `[DECIDED BY: AGENT]`) and non-obvious engineering landmines. Never log trivial syntax/type errors or 1-time setup commands.
  * `docs/VISUAL_GUIDE.html` — Self-contained interactive HTML visual dashboard (renders Mermaid diagrams, feature cards, and decision history in a clean browser UI so the non-coder user can visually inspect the project anytime).

## 4. Modular Code, GitHub (`Mostakim2005`) & Cloud CI/CD Standards
* **No Local Archiving:** Never maintain local archive folders on the PC. GitHub (`https://github.com/Mostakim2005/<project-name>`) is the single source of truth and permanent archive. Each project has its own dedicated repository.
* **GitHub CLI (`gh`) First:** Always use the pre-authenticated GitHub CLI (`gh`) and `git` to check repo status, create repositories (`gh repo create Mostakim2005/<name> --public --source=.`), push commits, and inspect workflow runs (`gh run list` / `gh run view`).
* **Modular Source Code Only (Zero Heavy Binaries in Git):**
  * Never commit `.exe` binaries, `node_modules/`, `dist/`, or build artifacts into Git history. Always maintain a strict `.gitignore`.
  * Keep source code modular: split large scripts or components into clean, single-responsibility modules so no source file becomes bloated.
* **Cloud Builds via GitHub Actions (`.github/workflows/build.yml`):**
  * For desktop apps, configure a GitHub Actions workflow so GitHub's free cloud runners automatically build the **portable `.exe`** (default) and publish it to GitHub Releases whenever code is pushed or tagged.
* **UI/UX & Accessibility Standard:** Follow clean visual hierarchy, proportional spacing, calibrated 50–950 color scales, and strict WCAG AA/AAA contrast (minimum 4.5:1 normal text, 3:1 large text). Never allow white-on-white or dark-on-dark text inversions across OS Light/Dark themes.

## 5. Mandatory Post-Change Protocol (Verify + Docs + Git Push)
After EVERY completed feature, architectural change, or complex bug fix:
1. **Run Verification Commands:** Execute the project's typecheck, lint, and headless verification scripts.
2. **Update Docs & `VISUAL_GUIDE.html`:** Update `PRODUCT.md`, `ARCHITECTURE.md`, `DECISIONS_AND_TRAPS.md`, and sync `docs/VISUAL_GUIDE.html` if user intent, architecture, or landmines changed.
3. **Commit & Push to GitHub:** Stage source code and documentation updates, create a clean git commit, and push to the project's `Mostakim2005` GitHub repository (create the repo via `gh` if it does not exist yet).
4. **Commit Summary Output:** End your response with:
   * **Commit Title, Hash & GitHub Repo Status**
   * **Plain-English Rationale** (what changed and why)
   * **Modified Files & Function Anchors** (file paths + function/variable names)
   * **Verification Run** (exact commands tested and pass/fail output)

## 6. Self-Updating Live Preferences Ledger
Whenever the user states a new recurring preference, workflow rule, or design dislike during conversation:
1. Automatically append it to **Section 7** below using a single-line searchable format:
   `- [PREF: Short-Descriptive-Slug | YYYY-MM-DD | Category: UI/Workflow/Build/Docs] Plain-English rule.`
2. This allows any AI agent to run `grep "^- \[PREF:" AGENTS.md` across different project folders and merge all user preferences without duplicates.

## 7. Live User Preferences (Auto-Captured)
- [PREF: Voice-Numbered-Options | 2026-11-06 | Category: Workflow] Always label confirmation points and options as [1], [2], [2A], [2B] so the user can reply quickly via voice dictation.
- [PREF: Visual-Previews-No-Code | 2026-11-06 | Category: Workflow] Never show raw code in previews; use Mermaid diagrams, Before/After tables, or HTML/SVG visual mocks.
- [PREF: Descriptive-Grep-Headers | 2026-11-06 | Category: Docs] Never use opaque IDs like FEAT-01 or BUG-04; use descriptive slug titles with natural-language aliases and a 1-line summary right below the header.
- [PREF: Portable-Exe-Default | 2026-11-06 | Category: Build] Package Windows desktop apps as portable .exe binaries by default rather than installers.
- [PREF: GitHub-Actions-Portable-Exe | 2026-11-06 | Category: Build] Host each project in its own public repo under github.com/Mostakim2005, commit only modular source code and docs, and use GitHub Actions workflows to build portable .exe releases in the cloud.
- - [PREF: Gitignore-Zero-Binaries | 2026-11-06 | Category: Build] Enforce strict .gitignore rules blocking node_modules, build outputs, executables, and secret environment files (.env); never commit heavy binaries or credentials to Git.
- [PREF: High-Level-Agency-No-Babysitting | 2026-11-06 | Category: Workflow] Direct agents using high-level intent, architecture, and outcomes; do not micromanage or script routine CLI commands that modern agents already understand.
- [PREF: GitHub-Single-Source-Of-Truth | 2026-11-06 | Category: Workflow] Treat local PC project folders as disposable working workshops and GitHub as the permanent vault, using version tags to trigger cloud builds instead of storing releases locally.
- - [PREF: Template-Master-Git-Archive | 2026-11-06 | Category: Workflow] Treat C:\Users\kazir\.agent-templates\ as a tracked Git repository on GitHub (Mostakim2005); when instructed, sync new Section 7 preference items from project workspaces back into this master repository without modifying core sections.
- [PREF: Periodic-Preference-Consolidation | 2026-11-06 | Category: Workflow] Support periodic consolidation where accumulated Section 7 entries are reviewed, deduplicated, and promoted into the permanent core sections (1–5) of the master AGENTS.md file.
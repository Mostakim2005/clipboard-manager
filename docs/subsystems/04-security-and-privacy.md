[Architecture](../ARCHITECTURE.md) · [Decisions & Traps](../DECISIONS_AND_TRAPS.md) · [AI Bootstrap](../../AI_BOOTSTRAP.md)

# Subsystem Dossier 04: Security & Privacy Model

> **Component Scope:** `src/ClipboardApp.Core/Security/DataProtector.cs`, `src/ClipboardApp.Core/Security/ClipboardPrivacyFilter.cs`, `src/ClipboardApp.Core/Interop/NativeMethods.cs`, and `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs`.

---

## 1. Cryptographic Protection at Rest (Windows DPAPI)

All sensitive data stored by ClipboardApp is protected using **Windows Data Protection API (DPAPI)**:
- **Scope:** `DataProtectionScope.CurrentUser`
- **Key Derivation:** Tied directly to the Windows user's login credentials and hardware TPM/security provider.
- **Encrypted Columns:** `Content` and `ImageData` in the SQLite `Entries` table under `%LocalAppData%\ClipboardApp\data.db`.
- **Entropy:** `DataProtector` uses the DPAPI CurrentUser scope; no cross-user recovery is possible.

---

## 2. Sensitive Content Detection & Redaction

### A. Real-Time Pattern Inspection
Whenever new content arrives on the clipboard, `ClipboardPrivacyFilter.ContainsSensitiveData` inspects the text during capture:
1. **Payment Cards:**
   - Evaluates 13–19 digit sequences matching Visa, MasterCard, Amex, Discover.
   - Executes the **Luhn Algorithm (Mod 10 checksum)** to eliminate false positive numeric sequences.
2. **API Keys & Secrets:**
   - Detects AWS access keys (`AKIA[0-9A-Z]{16}`), GitHub personal access tokens (`ghp_[a-zA-Z0-9]{36}`), private SSH keys (`-----BEGIN OPENSSH PRIVATE KEY-----`), JWT tokens, and connection strings.
3. **Sensitive Treatment (current behavior):**
   - The entry is marked `IsSensitive = true` and the card renders a `🔒 SENSITIVE` badge.
   - If `AutoClearSensitiveMinutes > 0`, `ExpiresAt` is set and `ClearExpired` removes the row after expiry.
   - **Masking is implemented.** A sensitive card renders `••••••••  (Sensitive)` in the card list (see `ModernCardList`), and the inspector shows a masked body with a **Reveal** button that swaps to **Mask** once revealed. Reveal state is per entry and shared between the list and the inspector (`SensitiveVisibilityToggled`).
   - Masking is a display concern only: the row is still DPAPI-encrypted at rest, and the stored text is never altered.

---

## 3. Password Manager Privacy Flags

ClipboardApp supports Windows Clipboard Privacy Formats introduced by Windows 10 and adopted by password managers:
- **`ExcludeClipboardContentFromMonitorProcessing`** (Registered clipboard format atom):
  Set by 1Password, Bitwarden, KeePass, and LastPass to instruct clipboard monitors to ignore temporary credential copies.
- **`CanIncludeInClipboardHistory`**:
  Controls whether an entry may be persisted across sessions.

### Dictation Tool Invariant:
By default, `AppSettings.RespectPrivacyFlags` is set to `false` to ensure compatibility with speech-to-text dictation engines (OpenWhispr, Dragon NaturallySpeaking), which frequently set internal clipboard flags when injecting transcribed text. When users enable high-security mode, privacy flag filtering is strictly enforced.

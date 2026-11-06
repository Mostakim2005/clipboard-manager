[Architecture](../ARCHITECTURE.md) · [Decisions & Traps](../DECISIONS_AND_TRAPS.md) · [AI Bootstrap](../../AI_BOOTSTRAP.md)

# Subsystem Dossier 02: Multi-Format Clip Storage & Data Architecture

> **Component Scope:** `src/ClipboardApp.Core/Storage/SqliteClipboardRepository.cs`, `src/ClipboardApp.Core/Services/ClipboardContentIdentity.cs`, `src/ClipboardApp.Core/Models/ClipboardEntry.cs`, `src/ClipboardApp.Core/Services/ImageHelper.cs`, and `src/ClipboardApp.Core/Security/DataProtector.cs`.

---

## 1. Implemented Format Model

ClipboardApp stores the **primary representation** of each clip plus enough metadata to paste it back faithfully:

| Capture source | Stored `Format` | Stored payload | Paste path |
| :--- | :--- | :--- | :--- |
| Plain text / URL / code | `text` | UTF-16 string | `CF_UNICODETEXT` |
| Hex / `rgb()` / `rgba()` | `color` | string | `CF_UNICODETEXT` |
| Browser/code HTML (when no clean text/image) | `html` | HTML fragment | CF_HTML + plain fallback |
| Rich text (when no HTML/text) | `rtf` | RTF string | RTF + plain fallback |
| Image / screenshot | `image` | PNG bytes (DPAPI encrypted) | DIB + PNG |
| Explorer file drop | `file` | newline-separated paths | HDROP + text |

**Image priority:** `DataFormats.FileDrop` is checked first because Explorer file copies can also expose a bitmap thumbnail. If no file drop is present, `CF_DIB` / `System.Drawing.Bitmap` is checked before HTML/text so browser image copies do not degrade into `<!--StartFragment-->` strings (`ImageHelper.TryGetImageFromClipboard`).

**Not implemented (be explicit):** per-clip custom hotkeys, per-clip quick-paste abbreviations, per-format raw stream deletion, and a generic `MultiFormatClip` JSON payload. The model stores one primary format plus the original bytes needed for that format. Do not document these as shipped features.

---

## 2. Actual SQLite Schema

Database: `%LocalAppData%\ClipboardApp\data.db` (SQLite).

```sql
Entries (
  Id           TEXT PRIMARY KEY,
  Content      TEXT,          -- DPAPI ciphertext
  Alias        TEXT,
  Pinned       INTEGER,       -- 0/1, "Never Auto Delete"
  ImageData    BLOB,          -- DPAPI ciphertext (PNG)
  CreatedAt    TEXT,          -- ISO-8601
  Format       TEXT,          -- text | color | html | rtf | image | file
  SourceApp    TEXT,
  IsSensitive  INTEGER,
  ExpiresAt    TEXT,
  Tags         TEXT,          -- comma-separated
  ContentHash  TEXT,          -- type-prefixed SHA-256 identity
  CopyCount    INTEGER,
  Seq          INTEGER,       -- permanent ordering key
  LastCopiedAt TEXT
)

Groups (Id TEXT PRIMARY KEY, Name TEXT)
EntryGroups (EntryId TEXT, GroupId TEXT)
Tags (Id TEXT PRIMARY KEY, Name TEXT, Color TEXT)
EntryTags (EntryId TEXT, TagId TEXT)
```

Indexes: `idx_entries_pinned_created`, `idx_entries_created`, `idx_entries_hash`, `idx_entries_seq`.

**Migration:** `EnsureColumnExists` adds `Tags`, `ContentHash`, `CopyCount`, `Seq`, and `LastCopiedAt` to older databases. `UPDATE Entries SET Seq = rowid WHERE Seq IS NULL OR Seq = 0` backfills ordering once, preserving original insertion order.

---

## 3. Identity & Deduplication

### A. Identity (`ClipboardContentIdentity`)
- Text-like content → SHA-256 of normalized UTF-8 (`\r\n`/`\r` → `\n`), prefix `txt:`.
- HTML → prefix `html:`; RTF → `rtf:`; file list → `file:`; image → `img:` over PNG bytes.
- Type prefixes prevent collisions between identical bytes with different semantics.

### B. Dedupe Authority
Only `SqliteClipboardRepository.InsertEntry` decides duplicates:

| Mode | Behavior |
| :--- | :--- |
| `KeepPosition` (default) | Existing row keeps `Seq`. `CopyCount++`, `LastCopiedAt = now`, `SourceApp` filled only if blank. |
| `MoveToTop` | Existing row receives `Seq = MAX(Seq)+1`; `CopyCount++`; `CreatedAt` refreshed. |
| `AllowDuplicates` | Skips the identity probe; always inserts a new row. |

The UI never performs its own newest-entry comparison.

### C. Ordering
- `GetAllEntries` / `GetEntryById` / search queries sort `Pinned DESC, Seq DESC`.
- `PruneEntries` orders unpinned rows by `Seq DESC` before applying count limits.
- Pinning is an explicit order change; normal copy/paste is not.

---

## 4. Retention & Memory Safety

- **Metadata-only list loads:** `GetAllEntries(includeImageData: false)` returns `HasImage` as an expression; image BLOBs are loaded lazily via `GetImageData(id)` only for preview/paste/drag.
- **DPAPI at rest:** `Content` and `ImageData` columns are encrypted with `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`.
- **Image size guard:** `MaxImageSizeMb` (default 15) is enforced before insertion; oversized images are skipped with a status message.
- **Pruning:** configurable max count and age; pinned rows are never pruned. Sensitive rows with `ExpiresAt` are removed by `ClearExpired`.
- **Stable temp files:** image drag/batch drops use `%TEMP%\ClipboardApp\Drops\clip_<entryId>.png`, reused rather than creating a new timestamped file per drag.
- **No per-format compression UI:** raw DIB is converted to PNG at capture time; there is no user-facing per-format editor.

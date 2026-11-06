using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using ClipboardApp.Core.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ClipboardApp.Core.Storage
{
    public class SqliteClipboardRepository : IClipboardRepository
    {
        private readonly SqliteConnection _connection;
        private readonly string _dbPath;

        /// <summary>
        /// Controls what happens when the same content hash is observed again.
        /// Default deliberately preserves the user's current visual order.
        /// </summary>
        public DeduplicationMode DeduplicationMode { get; set; } = DeduplicationMode.KeepPosition;

        public SqliteClipboardRepository() : this(null) { }

        public SqliteClipboardRepository(string? connectionString)
        {
            var cs = connectionString ?? GetDefaultConnectionString();
            _dbPath = cs;
            _connection = new SqliteConnection(cs);
            _connection.Open();
            InitializeSchema();
        }

        private static string GetDefaultConnectionString()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "ClipboardApp");
            Directory.CreateDirectory(dir);
            return $"Data Source={Path.Combine(dir, "data.db")}";
        }

        private void InitializeSchema()
        {
            try
            {
                using var pragmaCmd = _connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;";
                pragmaCmd.ExecuteNonQuery();
            }
            catch { }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Entries (
                        Id TEXT PRIMARY KEY,
                        Content TEXT,
                        Alias TEXT,
                        Pinned INTEGER DEFAULT 0,
                        ImageData BLOB,
                        CreatedAt TEXT,
                        Format TEXT,
                        SourceApp TEXT,
                        IsSensitive INTEGER DEFAULT 0,
                        ExpiresAt TEXT,
                        Tags TEXT,
                        ContentHash TEXT,
                        CopyCount INTEGER DEFAULT 1,
                        Seq INTEGER DEFAULT 0,
                        LastCopiedAt TEXT
                    );
                    CREATE TABLE IF NOT EXISTS Groups (
                        Id TEXT PRIMARY KEY,
                        Name TEXT
                    );
                    CREATE TABLE IF NOT EXISTS EntryGroups (
                        EntryId TEXT, GroupId TEXT
                    );
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id TEXT PRIMARY KEY, Name TEXT, Color TEXT
                    );
                    CREATE TABLE IF NOT EXISTS EntryTags (
                        EntryId TEXT, TagId TEXT
                    );
                    CREATE INDEX IF NOT EXISTS idx_entries_pinned_created ON Entries(Pinned DESC, CreatedAt DESC);
                    CREATE INDEX IF NOT EXISTS idx_entries_created ON Entries(CreatedAt DESC);
                ";
                cmd.ExecuteNonQuery();
            }

            // Migration check for older databases that may lack new columns
            EnsureColumnExists("Entries", "SourceApp", "TEXT");
            EnsureColumnExists("Entries", "IsSensitive", "INTEGER DEFAULT 0");
            EnsureColumnExists("Entries", "ExpiresAt", "TEXT");
            EnsureColumnExists("Entries", "Tags", "TEXT");
            EnsureColumnExists("Entries", "ContentHash", "TEXT");
            EnsureColumnExists("Entries", "CopyCount", "INTEGER DEFAULT 1");
            EnsureColumnExists("Entries", "Seq", "INTEGER DEFAULT 0");
            EnsureColumnExists("Entries", "LastCopiedAt", "TEXT");

            // One-time backfill for databases created before stable ordering existed.
            // rowid preserves the original insertion order.
            try
            {
                using var backfill = _connection.CreateCommand();
                backfill.CommandText = "UPDATE Entries SET Seq = rowid WHERE Seq IS NULL OR Seq = 0;";
                backfill.ExecuteNonQuery();
            }
            catch { }

            try
            {
                using var idxCmd = _connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_entries_hash ON Entries(ContentHash);";
                idxCmd.ExecuteNonQuery();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_entries_seq ON Entries(Pinned DESC, Seq DESC);";
                idxCmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void EnsureColumnExists(string table, string column, string typeDef)
        {
            try
            {
                using var checkCmd = _connection.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
                var count = Convert.ToInt64(checkCmd.ExecuteScalar());
                if (count == 0)
                {
                    using var alterCmd = _connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeDef}";
                    alterCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // In-memory or restricted DB might already have it
            }
        }

        public void InsertEntry(ClipboardEntry entry)
        {
            entry.ContentHash = ClipboardContentIdentity.Compute(entry);

            // Global identity check. The repository is the single source of truth for
            // deduplication; the UI must never make its own decision here.
            if (DeduplicationMode != DeduplicationMode.AllowDuplicates)
            {
                try
                {
                    using var checkCmd = _connection.CreateCommand();
                    checkCmd.CommandText = "SELECT Id, CopyCount FROM Entries WHERE ContentHash = @hash LIMIT 1";
                    checkCmd.Parameters.AddWithValue("@hash", entry.ContentHash);
                    using var reader = checkCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string existingId = reader.GetString(0);
                        int currentCount = reader.IsDBNull(1) ? 1 : Convert.ToInt32(reader[1]);
                        reader.Close();

                        var now = DateTime.Now;
                        using var updateCmd = _connection.CreateCommand();
                        if (DeduplicationMode == DeduplicationMode.MoveToTop)
                        {
                            long newSeq = GetNextSeq();
                            updateCmd.CommandText = @"UPDATE Entries
                                                      SET CreatedAt = @created,
                                                          CopyCount = CopyCount + 1,
                                                          LastCopiedAt = @last,
                                                          Seq = @seq,
                                                          SourceApp = CASE WHEN (SourceApp IS NULL OR SourceApp = '') AND @sourceApp != '' THEN @sourceApp ELSE SourceApp END
                                                      WHERE Id = @id";
                            updateCmd.Parameters.AddWithValue("@seq", newSeq);
                            entry.Seq = newSeq;
                        }
                        else
                        {
                            updateCmd.CommandText = @"UPDATE Entries
                                                      SET CopyCount = CopyCount + 1,
                                                          LastCopiedAt = @last,
                                                          SourceApp = CASE WHEN (SourceApp IS NULL OR SourceApp = '') AND @sourceApp != '' THEN @sourceApp ELSE SourceApp END
                                                      WHERE Id = @id";
                        }

                        updateCmd.Parameters.AddWithValue("@id", existingId);
                        updateCmd.Parameters.AddWithValue("@created", now.ToString("O"));
                        updateCmd.Parameters.AddWithValue("@last", now.ToString("O"));
                        updateCmd.Parameters.AddWithValue("@sourceApp", entry.SourceApp ?? "");
                        updateCmd.ExecuteNonQuery();

                        entry.Id = existingId;
                        entry.CopyCount = currentCount + 1;
                        entry.LastCopiedAt = now;
                        return;
                    }
                }
                catch
                {
                    // Never lose a clip because the dedupe probe failed.
                }
            }

            long seq = GetNextSeq();
            var copiedAt = DateTime.Now;
            entry.Seq = seq;
            entry.LastCopiedAt = copiedAt;
            if (entry.CopyCount < 1) entry.CopyCount = 1;

            var encryptedContent = DataProtector.EncryptString(entry.Content ?? string.Empty);
            var encryptedImage = entry.ImageData != null ? DataProtector.EncryptBytes(entry.ImageData) : null;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO Entries (Id, Content, Alias, Pinned, ImageData, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, ContentHash, CopyCount, Seq, LastCopiedAt)
                                VALUES (@id, @content, @alias, @pinned, @image, @created, @format, @sourceApp, @isSensitive, @expiresAt, @tags, @hash, @copyCount, @seq, @lastCopied)";
            cmd.Parameters.AddWithValue("@id", entry.Id);
            cmd.Parameters.AddWithValue("@content", encryptedContent);
            cmd.Parameters.AddWithValue("@alias", entry.Alias);
            cmd.Parameters.AddWithValue("@pinned", entry.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@image", (object?)encryptedImage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", entry.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@format", entry.Format);
            cmd.Parameters.AddWithValue("@sourceApp", entry.SourceApp ?? "");
            cmd.Parameters.AddWithValue("@isSensitive", entry.IsSensitive ? 1 : 0);
            cmd.Parameters.AddWithValue("@expiresAt", entry.ExpiresAt.HasValue ? entry.ExpiresAt.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("@tags", entry.Tags != null && entry.Tags.Count > 0 ? string.Join(",", entry.Tags) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hash", entry.ContentHash);
            cmd.Parameters.AddWithValue("@copyCount", entry.CopyCount);
            cmd.Parameters.AddWithValue("@seq", entry.Seq);
            cmd.Parameters.AddWithValue("@lastCopied", entry.LastCopiedAt.Value.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        private long GetNextSeq()
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(MAX(Seq), 0) + 1 FROM Entries";
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
            catch
            {
                return DateTime.UtcNow.Ticks;
            }
        }

        public ClipboardEntry? GetEntryById(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Content, Alias, Pinned, ImageData, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapReader(reader, includeImageData: true) : null;
        }

        public byte[]? GetImageData(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT ImageData FROM Entries WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            var result = cmd.ExecuteScalar();
            if (result == null || result is DBNull) return null;
            var encryptedBytes = (byte[])result;
            return DataProtector.DecryptBytes(encryptedBytes);
        }

        public List<ClipboardEntry> GetAllEntries(bool includeImageData = true)
        {
            var results = new List<ClipboardEntry>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = includeImageData
                ? "SELECT Id, Content, Alias, Pinned, ImageData, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries ORDER BY Pinned DESC, Seq DESC"
                : "SELECT Id, Content, Alias, Pinned, NULL, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, (ImageData IS NOT NULL) AS HasImage, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries ORDER BY Pinned DESC, Seq DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapReader(reader, includeImageData));
            }
            return results;
        }

        public List<ClipboardEntry> GetAllEntries() => GetAllEntries(includeImageData: true);

        public ClipboardEntry? GetNewestEntry()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Content, Alias, Pinned, ImageData, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries ORDER BY Seq DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapReader(reader, includeImageData: true) : null;
        }

        public List<ClipboardEntry> GetEntriesByDate(DateTime date)
        {
            var results = new List<ClipboardEntry>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Content, Alias, Pinned, NULL, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, (ImageData IS NOT NULL) AS HasImage, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries WHERE date(CreatedAt) = date(@d) ORDER BY Pinned DESC, Seq DESC";
            cmd.Parameters.AddWithValue("@d", date.ToString("O"));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapReader(reader, includeImageData: false));
            }
            return results;
        }

        public List<ClipboardEntry> SearchByAlias(string alias)
        {
            var results = new List<ClipboardEntry>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Content, Alias, Pinned, NULL, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, (ImageData IS NOT NULL) AS HasImage, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries WHERE Alias LIKE @q ORDER BY Pinned DESC, Seq DESC";
            cmd.Parameters.AddWithValue("@q", $"%{alias}%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapReader(reader, includeImageData: false));
            }
            return results;
        }

        public List<ClipboardEntry> SearchByTag(string tag)
        {
            var results = new List<ClipboardEntry>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Content, Alias, Pinned, NULL, CreatedAt, Format, SourceApp, IsSensitive, ExpiresAt, Tags, (ImageData IS NOT NULL) AS HasImage, ContentHash, CopyCount, Seq, LastCopiedAt FROM Entries WHERE Tags LIKE @tag ORDER BY Pinned DESC, Seq DESC";
            cmd.Parameters.AddWithValue("@tag", $"%{tag}%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapReader(reader, includeImageData: false));
            }
            return results;
        }

        public void UpdateEntry(ClipboardEntry entry)
        {
            // Image entries are edited from the list without their blob loaded.
            // In that case keep the identity that was computed when the image was captured.
            if (entry.ImageData != null || (entry.Format != "image" && !entry.HasImage))
            {
                entry.ContentHash = ClipboardContentIdentity.Compute(entry);
            }

            var encryptedContent = DataProtector.EncryptString(entry.Content);
            byte[]? encryptedImage = null;
            if (entry.ImageData != null)
            {
                encryptedImage = DataProtector.EncryptBytes(entry.ImageData);
            }

            using var cmd = _connection.CreateCommand();
            if (entry.ImageData != null)
            {
                cmd.CommandText = @"UPDATE Entries SET Content=@c, Alias=@a, Pinned=@p, ImageData=@i, Format=@f, SourceApp=@s, IsSensitive=@sens, ExpiresAt=@exp, Tags=@tags, ContentHash=@hash WHERE Id=@id";
                cmd.Parameters.AddWithValue("@i", (object?)encryptedImage ?? DBNull.Value);
            }
            else
            {
                cmd.CommandText = @"UPDATE Entries SET Content=@c, Alias=@a, Pinned=@p, Format=@f, SourceApp=@s, IsSensitive=@sens, ExpiresAt=@exp, Tags=@tags, ContentHash=@hash WHERE Id=@id";
            }

            cmd.Parameters.AddWithValue("@id", entry.Id);
            cmd.Parameters.AddWithValue("@c", encryptedContent);
            cmd.Parameters.AddWithValue("@a", entry.Alias);
            cmd.Parameters.AddWithValue("@p", entry.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@f", entry.Format);
            cmd.Parameters.AddWithValue("@s", entry.SourceApp ?? "");
            cmd.Parameters.AddWithValue("@sens", entry.IsSensitive ? 1 : 0);
            cmd.Parameters.AddWithValue("@exp", entry.ExpiresAt.HasValue ? entry.ExpiresAt.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("@tags", entry.Tags != null && entry.Tags.Count > 0 ? string.Join(",", entry.Tags) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hash", entry.ContentHash ?? "");
            cmd.ExecuteNonQuery();
        }

        public void DeleteEntry(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Entries WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteEntries(List<string> ids)
        {
            if (ids.Count == 0) return;
            using var tx = _connection.BeginTransaction();
            foreach (var id in ids)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = tx; // Fix transaction bug
                cmd.CommandText = "DELETE FROM Entries WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public void ClearAll(bool includePinned = false)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = includePinned ? "DELETE FROM Entries" : "DELETE FROM Entries WHERE Pinned = 0";
            cmd.ExecuteNonQuery();
        }

        public void PinEntry(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE Entries SET Pinned=1 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void TogglePin(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE Entries SET Pinned = CASE WHEN Pinned = 1 THEN 0 ELSE 1 END WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int PruneEntries(int maxCount, int maxAgeDays)
        {
            int deleted = 0;
            // 1. Prune by age for unpinned entries
            if (maxAgeDays > 0)
            {
                using var cmdAge = _connection.CreateCommand();
                cmdAge.CommandText = "DELETE FROM Entries WHERE Pinned = 0 AND datetime(CreatedAt) < datetime('now', @days)";
                cmdAge.Parameters.AddWithValue("@days", $"-{maxAgeDays} days");
                deleted += cmdAge.ExecuteNonQuery();
            }

            // 2. Prune by max count for unpinned entries
            if (maxCount > 0)
            {
                using var cmdCount = _connection.CreateCommand();
                cmdCount.CommandText = @"
                    DELETE FROM Entries WHERE Id IN (
                        SELECT Id FROM Entries 
                        WHERE Pinned = 0 
                        ORDER BY Seq DESC
                        LIMIT -1 OFFSET @maxCount
                    )";
                cmdCount.Parameters.AddWithValue("@maxCount", maxCount);
                deleted += cmdCount.ExecuteNonQuery();
            }

            return deleted;
        }

        public int ClearExpired()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Entries WHERE Pinned = 0 AND ExpiresAt IS NOT NULL AND datetime(ExpiresAt) < datetime('now')";
            return cmd.ExecuteNonQuery();
        }

        public void AddGroup(ClipboardGroup group)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Groups (Id, Name) VALUES (@id, @name)";
            cmd.Parameters.AddWithValue("@id", group.Id);
            cmd.Parameters.AddWithValue("@name", group.Name);
            cmd.ExecuteNonQuery();
        }

        public List<ClipboardGroup> GetAllGroups()
        {
            var results = new List<ClipboardGroup>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Name FROM Groups";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new ClipboardGroup { Id = reader.GetString(0), Name = reader.GetString(1) });
            }
            return results;
        }

        public void AddTag(Tag tag)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Tags (Id, Name, Color) VALUES (@id, @name, @color)";
            cmd.Parameters.AddWithValue("@id", tag.Id);
            cmd.Parameters.AddWithValue("@name", tag.Name);
            cmd.Parameters.AddWithValue("@color", tag.Color);
            cmd.ExecuteNonQuery();
        }

        public List<Tag> GetAllTags()
        {
            var results = new List<Tag>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Color FROM Tags";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new Tag { Id = reader.GetString(0), Name = reader.GetString(1), Color = reader.GetString(2) });
            }
            return results;
        }

        public void AssignEntryToGroup(string entryId, string groupId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO EntryGroups (EntryId, GroupId) VALUES (@e, @g)";
            cmd.Parameters.AddWithValue("@e", entryId);
            cmd.Parameters.AddWithValue("@g", groupId);
            cmd.ExecuteNonQuery();
        }

        public void AssignEntryToTag(string entryId, string tagId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO EntryTags (EntryId, TagId) VALUES (@e, @t)";
            cmd.Parameters.AddWithValue("@e", entryId);
            cmd.Parameters.AddWithValue("@t", tagId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveEntryFromGroup(string entryId, string groupId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM EntryGroups WHERE EntryId=@e AND GroupId=@g";
            cmd.Parameters.AddWithValue("@e", entryId);
            cmd.Parameters.AddWithValue("@g", groupId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveEntryFromTag(string entryId, string tagId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM EntryTags WHERE EntryId=@e AND TagId=@t";
            cmd.Parameters.AddWithValue("@e", entryId);
            cmd.Parameters.AddWithValue("@t", tagId);
            cmd.ExecuteNonQuery();
        }

        private static ClipboardEntry MapReader(SqliteDataReader reader, bool includeImageData)
        {
            var rawDate = reader.IsDBNull(5) ? "" : reader.GetString(5);
            if (!DateTime.TryParse(rawDate, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var created) &&
                !DateTime.TryParse(rawDate, out created))
            {
                created = DateTime.Now;
            }

            var cipherContent = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var content = DataProtector.DecryptString(cipherContent);

            byte[]? imageData = null;
            bool hasImage = false;

            if (includeImageData)
            {
                if (!reader.IsDBNull(4))
                {
                    var encryptedBytes = (byte[])reader[4];
                    imageData = DataProtector.DecryptBytes(encryptedBytes);
                    hasImage = imageData != null && imageData.Length > 0;
                }
            }
            var tagsList = new List<string>();
            if (reader.FieldCount > 10 && !reader.IsDBNull(10))
            {
                var rawTags = reader.GetString(10);
                if (!string.IsNullOrWhiteSpace(rawTags))
                {
                    tagsList = rawTags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim())
                                      .Where(t => t.Length > 0)
                                      .ToList();
                }
            }

            if (!includeImageData)
            {
                // In non-image queries the HasImage flag is an expression column.
                int hasImageIndex = IndexOf(reader, "HasImage");
                if (hasImageIndex >= 0 && !reader.IsDBNull(hasImageIndex))
                {
                    hasImage = Convert.ToInt64(reader[hasImageIndex]) != 0;
                }
            }

            string sourceApp = "";
            if (reader.FieldCount > 7 && !reader.IsDBNull(7))
            {
                sourceApp = reader.GetString(7);
            }

            bool isSensitive = false;
            if (reader.FieldCount > 8 && !reader.IsDBNull(8))
            {
                isSensitive = Convert.ToInt32(reader[8]) == 1;
            }

            DateTime? expiresAt = null;
            if (reader.FieldCount > 9 && !reader.IsDBNull(9))
            {
                var expStr = reader.GetString(9);
                if (DateTime.TryParse(expStr, out var expVal)) expiresAt = expVal;
            }

            int copyCount = 1;
            string contentHash = "";
            long seq = 0;
            DateTime? lastCopiedAt = null;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (string.Equals(name, "CopyCount", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reader.IsDBNull(i)) copyCount = Convert.ToInt32(reader[i]);
                }
                else if (string.Equals(name, "ContentHash", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reader.IsDBNull(i)) contentHash = reader.GetString(i);
                }
                else if (string.Equals(name, "Seq", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reader.IsDBNull(i)) seq = Convert.ToInt64(reader[i]);
                }
                else if (string.Equals(name, "LastCopiedAt", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reader.IsDBNull(i) && DateTime.TryParse(reader.GetString(i), out var last)) lastCopiedAt = last;
                }
            }

            return new ClipboardEntry
            {
                Id = reader.IsDBNull(0) ? Guid.NewGuid().ToString() : reader.GetString(0),
                Content = content,
                Alias = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Pinned = !reader.IsDBNull(3) && Convert.ToInt32(reader[3]) == 1,
                ImageData = imageData,
                HasImage = hasImage,
                CreatedAt = created,
                Format = reader.IsDBNull(6) ? "text" : reader.GetString(6),
                SourceApp = sourceApp,
                IsSensitive = isSensitive,
                ExpiresAt = expiresAt,
                Tags = tagsList,
                ContentHash = contentHash,
                CopyCount = copyCount,
                Seq = seq,
                LastCopiedAt = lastCopiedAt
            };
        }

        private static int IndexOf(SqliteDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}

using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipboardApp.Core.Storage
{
    /// <summary>
    /// Separate encrypted store for API keys. Deliberately separate from the
    /// clipboard-history database so secrets never appear in history, search,
    /// previews, or batch paste.
    /// </summary>
    public sealed class ApiKeyRepository : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApiVaultService _vault;

        public ApiKeyRepository(string? databasePath = null, ApiVaultService? vault = null)
        {
            _vault = vault ?? new ApiVaultService();
            string cs;
            if (!string.IsNullOrWhiteSpace(databasePath))
            {
                cs = databasePath.Contains("=", StringComparison.Ordinal)
                    ? databasePath
                    : $"Data Source={databasePath}";
                if (!databasePath.Contains("=", StringComparison.Ordinal))
                {
                    string? dir = Path.GetDirectoryName(databasePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
            }
            else
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClipboardApp");
                Directory.CreateDirectory(dir);
                cs = $"Data Source={Path.Combine(dir, "api-vault.db")}";
            }

            _connection = new SqliteConnection(cs);
            _connection.Open();
            InitializeSchema();
        }

        public ApiVaultService Vault => _vault;

        private void InitializeSchema()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ApiKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Service TEXT,
                    Environment TEXT,
                    Notes TEXT,
                    Tags TEXT,
                    Nonce TEXT,
                    Cipher TEXT,
                    Tag TEXT,
                    CreatedAt TEXT,
                    UpdatedAt TEXT,
                    LastCopiedAt TEXT,
                    CopyCount INTEGER DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_api_keys_updated ON ApiKeys(UpdatedAt DESC);
            ";
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<ApiKey> GetAll()
        {
            var result = new List<ApiKey>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, Service, Environment, Notes, Tags, Nonce, Cipher, Tag,
                                       CreatedAt, UpdatedAt, LastCopiedAt, CopyCount
                                FROM ApiKeys ORDER BY UpdatedAt DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(ReadKey(reader));
            }
            return result;
        }

        public ApiKey? GetById(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, Service, Environment, Notes, Tags, Nonce, Cipher, Tag,
                                       CreatedAt, UpdatedAt, LastCopiedAt, CopyCount
                                FROM ApiKeys WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadKey(reader) : null;
        }

        public void Upsert(ApiKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(key.Name))
            {
                throw new ArgumentException("An API key needs a name.", nameof(key));
            }

            var encrypted = _vault.EncryptSecret(key.Secret ?? string.Empty);
            key.UpdatedAt = DateTime.Now;
            if (key.CreatedAt == default) key.CreatedAt = key.UpdatedAt;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ApiKeys (Id, Name, Service, Environment, Notes, Tags, Nonce, Cipher, Tag,
                                     CreatedAt, UpdatedAt, LastCopiedAt, CopyCount)
                VALUES (@id, @name, @service, @environment, @notes, @tags, @nonce, @cipher, @tag,
                        @created, @updated, @lastCopied, @copyCount)
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    Service = excluded.Service,
                    Environment = excluded.Environment,
                    Notes = excluded.Notes,
                    Tags = excluded.Tags,
                    Nonce = excluded.Nonce,
                    Cipher = excluded.Cipher,
                    Tag = excluded.Tag,
                    UpdatedAt = excluded.UpdatedAt";
            cmd.Parameters.AddWithValue("@id", key.Id);
            cmd.Parameters.AddWithValue("@name", key.Name.Trim());
            cmd.Parameters.AddWithValue("@service", key.Service ?? "");
            cmd.Parameters.AddWithValue("@environment", key.Environment ?? "");
            cmd.Parameters.AddWithValue("@notes", key.Notes ?? "");
            cmd.Parameters.AddWithValue("@tags", key.Tags != null && key.Tags.Count > 0 ? string.Join(",", key.Tags) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@nonce", encrypted.Nonce);
            cmd.Parameters.AddWithValue("@cipher", encrypted.Cipher);
            cmd.Parameters.AddWithValue("@tag", encrypted.Tag);
            cmd.Parameters.AddWithValue("@created", key.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@updated", key.UpdatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@lastCopied", key.LastCopiedAt.HasValue ? key.LastCopiedAt.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("@copyCount", key.CopyCount);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string id)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM ApiKeys WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void RecordCopy(IEnumerable<string> ids)
        {
            var now = DateTime.Now.ToString("O");
            foreach (var id in ids)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "UPDATE ApiKeys SET LastCopiedAt = @now, CopyCount = CopyCount + 1 WHERE Id = @id";
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private ApiKey ReadKey(SqliteDataReader reader)
        {
            var ciphertext = new VaultCiphertext
            {
                Nonce = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Cipher = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Tag = reader.IsDBNull(8) ? "" : reader.GetString(8)
            };

            return new ApiKey
            {
                Id = reader.GetString(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Service = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Environment = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Notes = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Tags = reader.IsDBNull(5)
                    ? new List<string>()
                    : reader.GetString(5).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                Secret = _vault.DecryptSecret(ciphertext),
                CreatedAt = ParseDate(reader, 9),
                UpdatedAt = ParseDate(reader, 10),
                LastCopiedAt = reader.IsDBNull(11) ? null : ParseDate(reader, 11),
                CopyCount = reader.IsDBNull(12) ? 0 : Convert.ToInt32(reader.GetValue(12))
            };
        }

        private static DateTime ParseDate(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return default;
            return DateTime.TryParse(reader.GetString(ordinal), out var value) ? value : default;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}

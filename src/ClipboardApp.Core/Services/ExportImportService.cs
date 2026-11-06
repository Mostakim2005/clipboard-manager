using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ClipboardApp.Core.Services
{
    public static class ExportImportService
    {
        public static string ExportToJson(IEnumerable<ClipboardEntry> entries, bool encrypt = false)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entries, options);
            if (encrypt)
            {
                return DataProtector.EncryptString(json);
            }
            return json;
        }

        public static List<ClipboardEntry> ImportFromJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return new List<ClipboardEntry>();

            string json = content.Trim();
            if (json.StartsWith("ENC:", StringComparison.Ordinal))
            {
                json = DataProtector.DecryptString(json);
            }

            var entries = JsonSerializer.Deserialize<List<ClipboardEntry>>(json);
            return entries ?? new List<ClipboardEntry>();
        }

        public static string ExportToCsv(IEnumerable<ClipboardEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,CreatedAt,Format,Pinned,Alias,SourceApp,Content");
            foreach (var e in entries)
            {
                var escapedContent = EscapeCsv(e.Content);
                var escapedAlias = EscapeCsv(e.Alias);
                var escapedApp = EscapeCsv(e.SourceApp);
                sb.AppendLine($"{e.Id},{e.CreatedAt:O},{e.Format},{e.Pinned},{escapedAlias},{escapedApp},{escapedContent}");
            }
            return sb.ToString();
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            var text = field;
            // Prevent spreadsheet formula / DDE command injection (CWE-1236)
            if (text.Length > 0 && (text[0] == '=' || text[0] == '+' || text[0] == '-' || text[0] == '@' || text[0] == '\t' || text[0] == '\r'))
            {
                text = "'" + text;
            }
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}

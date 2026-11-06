using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Builds one deterministic clipboard payload from several selected entries.
    /// A clipboard can hold one payload at a time, so "copy many" always means
    /// either a merged payload or a file list.
    /// </summary>
    public static class BatchCopyService
    {
        public static string Build(
            IEnumerable<ClipboardEntry> entries,
            string format,
            string separator)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var list = entries.ToList();
            var pairs = list
                .Select((entry, index) => new KeyValuePair<string, string>(
                    ResolveName(entry, index),
                    ResolveValue(entry)))
                .ToList();

            return BuildPairs(pairs, format, separator);
        }

        public static string BuildApiKeys(
            IEnumerable<(string Name, string Value)> keys,
            string format)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            var pairs = keys
                .Select((pair, index) => new KeyValuePair<string, string>(
                    string.IsNullOrWhiteSpace(pair.Name) ? $"KEY_{index + 1}" : pair.Name.Trim(),
                    pair.Value ?? string.Empty))
                .ToList();
            return BuildPairs(pairs, format, Environment.NewLine);
        }

        private static string BuildPairs(
            IReadOnlyList<KeyValuePair<string, string>> pairs,
            string format,
            string separator)
        {
            return (format ?? "Plain").ToUpperInvariant() switch
            {
                "ENV" => BuildEnv(pairs),
                "JSON" => BuildJson(pairs),
                "CSV" => BuildCsv(pairs),
                _ => string.Join(
                    string.IsNullOrEmpty(separator) ? Environment.NewLine + Environment.NewLine : separator,
                    pairs.Select(p => p.Value))
            };
        }

        public static string NormalizeFormat(string? format) =>
            (format ?? "Plain").ToUpperInvariant() switch
            {
                "ENV" => "Env",
                "JSON" => "Json",
                "CSV" => "Csv",
                "RICH" => "Rich",
                _ => "Plain"
            };

        private static string BuildEnv(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            foreach (var pair in pairs)
            {
                string name = SanitizeEnvName(pair.Key);
                string candidate = name;
                int suffix = 2;
                while (!used.Add(candidate))
                {
                    candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }

                string value = pair.Value ?? string.Empty;
                bool needsQuotes = value.Any(c => c is '\r' or '\n' or '"' or '\\' or '#' or ' ');
                if (needsQuotes)
                {
                    value = value
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n");
                    value = "\"" + value + "\"";
                }
                sb.Append(candidate).Append('=').Append(value).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string BuildJson(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in pairs)
            {
                string name = string.IsNullOrWhiteSpace(pair.Key) ? "value" : pair.Key.Trim();
                string candidate = name;
                int suffix = 2;
                while (dict.ContainsKey(candidate))
                {
                    candidate = name + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }
                dict[candidate] = pair.Value ?? string.Empty;
            }

            return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string BuildCsv(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var sb = new StringBuilder();
            sb.Append("name,value\n");
            foreach (var pair in pairs)
            {
                sb.Append(Csv(pair.Key)).Append(',').Append(Csv(pair.Value)).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string Csv(string? value)
        {
            string text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }

        private static string ResolveName(ClipboardEntry entry, int index)
        {
            if (!string.IsNullOrWhiteSpace(entry.Alias)) return entry.Alias.Trim();
            if (!string.IsNullOrWhiteSpace(entry.Content))
            {
                string firstLine = entry.Content
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim() ?? "";
                if (firstLine.Length > 0)
                {
                    return firstLine.Length <= 40 ? firstLine : firstLine[..40];
                }
            }

            string format = string.IsNullOrWhiteSpace(entry.Format) ? "clip" : entry.Format.Trim();
            return $"{format}_{index + 1}";
        }

        private static string ResolveValue(ClipboardEntry entry)
        {
            return entry.Format switch
            {
                "html" => PasteService.HtmlToPlainText(entry.Content ?? string.Empty),
                "rtf" => PasteService.RtfToPlainText(entry.Content ?? string.Empty),
                "image" or "file" when entry.HasImage && string.IsNullOrWhiteSpace(entry.Content) => "[image]",
                _ => entry.Content ?? string.Empty
            };
        }

        private static string SanitizeEnvName(string? name)
        {
            string text = (name ?? "VALUE").Trim().ToUpperInvariant();
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }
            string result = sb.ToString().Trim('_');
            if (result.Length == 0) result = "VALUE";
            if (char.IsDigit(result[0])) result = "_" + result;
            return result;
        }
    }
}

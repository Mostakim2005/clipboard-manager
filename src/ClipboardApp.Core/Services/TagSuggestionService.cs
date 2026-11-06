using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Learns tag and alias tokens from history and ranks suggestions for the
    /// editor and the inline tag prompt. Frequency wins first so the user's
    /// real vocabulary appears before alphabetical noise.
    /// </summary>
    public static class TagSuggestionService
    {
        private static readonly char[] s_tagSeparators = { ',', ';' };

        public static List<string> CollectTags(IEnumerable<ClipboardEntry>? entries)
        {
            var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries ?? Enumerable.Empty<ClipboardEntry>())
            {
                foreach (var tag in entry.Tags ?? new List<string>())
                {
                    foreach (var token in SplitTokens(tag, s_tagSeparators))
                    {
                        Add(frequency, token);
                    }
                }
            }

            return Rank(frequency);
        }

        public static List<string> CollectTagTokens(IEnumerable<string>? tagValues)
        {
            var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in tagValues ?? Enumerable.Empty<string>())
            {
                foreach (var token in SplitTokens(value, s_tagSeparators))
                {
                    Add(frequency, token);
                }
            }
            return Rank(frequency);
        }

        public static List<string> CollectAliases(IEnumerable<ClipboardEntry>? entries)
        {
            var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries ?? Enumerable.Empty<ClipboardEntry>())
            {
                var alias = (entry.Alias ?? "").Trim();
                if (alias.Length > 0) Add(frequency, alias);
            }
            return Rank(frequency);
        }

        public static List<string> Suggest(
            IEnumerable<string>? source,
            string? token,
            int max = 8)
        {
            var values = (source ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string query = (token ?? "").Trim().TrimStart('#');
            if (query.Length == 0) return values.Take(Math.Max(1, max)).ToList();

            var starts = values
                .Where(s => s.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var contains = values
                .Where(s => !s.StartsWith(query, StringComparison.OrdinalIgnoreCase) &&
                            s.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return starts.Concat(contains).Take(Math.Max(1, max)).ToList();
        }

        public static string GetToken(
            string? text,
            int caret,
            char[] separators,
            out int tokenStart,
            out int tokenLength)
        {
            text ??= string.Empty;
            caret = Math.Clamp(caret, 0, text.Length);
            separators ??= Array.Empty<char>();

            int start = caret;
            while (start > 0 && Array.IndexOf(separators, text[start - 1]) < 0)
            {
                start--;
            }

            string raw = text.Substring(start, caret - start);
            int leading = raw.Length - raw.TrimStart().Length;

            tokenStart = start + leading;
            tokenLength = Math.Max(0, caret - tokenStart);
            return raw.Trim();
        }

        public static string ApplySuggestion(
            string? text,
            int tokenStart,
            int tokenLength,
            string suggestion)
        {
            text ??= string.Empty;
            tokenStart = Math.Clamp(tokenStart, 0, text.Length);
            tokenLength = Math.Clamp(tokenLength, 0, text.Length - tokenStart);
            suggestion ??= string.Empty;

            return text.Remove(tokenStart, tokenLength).Insert(tokenStart, suggestion);
        }

        private static IEnumerable<string> SplitTokens(string? value, char[] separators)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;
            foreach (var part in value.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = part.Trim().TrimStart('#').Trim();
                if (token.Length > 0) yield return token;
            }
        }

        private static void Add(Dictionary<string, int> frequency, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            frequency[token] = frequency.TryGetValue(token, out int count) ? count + 1 : 1;
        }

        private static List<string> Rank(Dictionary<string, int> frequency) =>
            frequency
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key)
                .ToList();
    }
}

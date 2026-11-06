using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClipboardApp.Core.Services
{
    public class SearchService
    {
        private static readonly Regex s_urlRegex = new(@"^(https?://|www\.)[^\s/$.?#].[^\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_hexColorRegex = new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

        public List<ClipboardEntry> FilterEntries(
            IEnumerable<ClipboardEntry> entries,
            string query,
            bool useRegex = false,
            string? formatFilter = null)
        {
            if (entries == null) return new List<ClipboardEntry>();

            var filtered = entries;

            // 1. Content format filtering
            if (!string.IsNullOrEmpty(formatFilter) && !formatFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e => MatchesFormatFilter(e, formatFilter));
            }

            // 2. Query filtering
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                if (useRegex)
                {
                    try
                    {
                        var regex = new Regex(q, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(150));
                        filtered = filtered.Where(e =>
                        {
                            try
                            {
                                return (!string.IsNullOrEmpty(e.Alias) && regex.IsMatch(e.Alias)) ||
                                       (!string.IsNullOrEmpty(e.Content) && regex.IsMatch(e.Content)) ||
                                       (!string.IsNullOrEmpty(e.SourceApp) && regex.IsMatch(e.SourceApp)) ||
                                       (e.Tags != null && e.Tags.Any(t => regex.IsMatch(t)));
                            }
                            catch (RegexMatchTimeoutException)
                            {
                                return false;
                            }
                        });
                    }
                    catch
                    {
                        // Fall back to plain string search if regex is invalid
                        filtered = filtered.Where(e => MatchesQuery(e, q));
                    }
                }
                else
                {
                    filtered = filtered.Where(e => MatchesQuery(e, q));
                }
            }

            return GetPinnedFirst(filtered);
        }

        private static bool MatchesQuery(ClipboardEntry e, string q)
        {
            if (!string.IsNullOrEmpty(e.Alias) && e.Alias.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(e.Content) && e.Content.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(e.SourceApp) && e.SourceApp.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.Tags != null && e.Tags.Any(t => t.Contains(q.TrimStart('#'), StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        private static bool MatchesFormatFilter(ClipboardEntry e, string filter)
        {
            var f = filter.ToLowerInvariant();
            switch (f)
            {
                case "image":
                case "images":
                    return e.Format == "image" || e.HasImage;
                case "file":
                case "files":
                    return e.Format == "file";
                case "link":
                case "links":
                    return (e.Format == "link" || e.Format == "text") && !string.IsNullOrWhiteSpace(e.Content) && s_urlRegex.IsMatch(e.Content.Trim());
                case "color":
                case "colors":
                    return (e.Format == "color" || e.Format == "text") && !string.IsNullOrWhiteSpace(e.Content) && s_hexColorRegex.IsMatch(e.Content.Trim());
                case "html":
                    return e.Format == "html";
                case "rtf":
                    return e.Format == "rtf";
                case "text":
                    return e.Format == "text";
                default:
                    return true;
            }
        }

        public List<ClipboardEntry> SearchByAlias(IEnumerable<ClipboardEntry> entries, string alias)
        {
            if (string.IsNullOrEmpty(alias)) return entries.ToList();
            return entries.Where(e =>
                (!string.IsNullOrEmpty(e.Alias) && e.Alias.Contains(alias, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.Content) && e.Content.Contains(alias, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        public List<ClipboardEntry> FilterByDate(IEnumerable<ClipboardEntry> entries, DateTime date)
        {
            return entries.Where(e => e.CreatedAt.Date == date.Date).ToList();
        }

        public List<ClipboardEntry> GetPinnedFirst(IEnumerable<ClipboardEntry> entries)
        {
            // Stable ordering: pinned group first, then the permanent Seq order.
            // LINQ OrderBy is stable, so entries with Seq == 0 (tests/imports)
            // retain their incoming order.
            return entries
                .OrderByDescending(e => e.Pinned)
                .ThenByDescending(e => e.Seq)
                .ToList();
        }
    }
}

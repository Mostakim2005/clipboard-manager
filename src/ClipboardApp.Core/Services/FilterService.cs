using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Always-available Date / Type / App filters. Kept in Core so the
    /// selection and deletion rules can be unit tested without a UI.
    /// </summary>
    public static class FilterService
    {
        private static readonly Regex s_urlRegex = new(
            @"^(https?://|www\.)[^\s/$.?#].[^\s]*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex s_hexColorRegex = new(
            @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
            RegexOptions.Compiled);

        public static IEnumerable<ClipboardEntry> Apply(
            IEnumerable<ClipboardEntry> entries,
            string? dateFilter,
            string? typeFilter,
            string? appFilter)
        {
            if (entries == null) return Enumerable.Empty<ClipboardEntry>();

            var result = entries;
            string date = NormalizeDate(dateFilter);
            string type = NormalizeType(typeFilter);
            string app = string.IsNullOrWhiteSpace(appFilter) ? "Any" : appFilter.Trim();

            if (date != "Any")
            {
                result = result.Where(e => MatchesDate(e.CreatedAt, date));
            }

            if (type != "Any")
            {
                result = result.Where(e => MatchesType(e, type));
            }

            if (!app.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(e =>
                    string.Equals(e.SourceApp ?? "", app, StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }

        public static bool MatchesDate(DateTime created, string filter)
        {
            var today = DateTime.Today;
            var date = created.Date;
            return filter switch
            {
                "Today" => date == today,
                "Yesterday" => date == today.AddDays(-1),
                "Past7" => date >= today.AddDays(-6) && date <= today,
                "Past30" => date >= today.AddDays(-29) && date <= today,
                "Older" => date < today.AddDays(-29),
                _ => true
            };
        }

        public static bool MatchesType(ClipboardEntry entry, string filter)
        {
            if (entry == null) return false;

            return filter switch
            {
                "Text" => entry.Format == "text",
                "Image" => entry.Format == "image" || entry.HasImage,
                "File" => entry.Format == "file",
                "Link" => (entry.Format == "link" || entry.Format == "text") &&
                          !string.IsNullOrWhiteSpace(entry.Content) &&
                          s_urlRegex.IsMatch(entry.Content.Trim()),
                "Color" => (entry.Format == "color" || entry.Format == "text") &&
                           !string.IsNullOrWhiteSpace(entry.Content) &&
                           s_hexColorRegex.IsMatch(entry.Content.Trim()),
                _ => true
            };
        }

        public static string NormalizeDate(string? value) => value switch
        {
            "Today" => "Today",
            "Yesterday" => "Yesterday",
            "Past7" => "Past7",
            "Past30" => "Past30",
            "Older" => "Older",
            _ => "Any"
        };

        public static string NormalizeType(string? value) => value switch
        {
            "Text" => "Text",
            "Image" => "Image",
            "Link" => "Link",
            "File" => "File",
            "Color" => "Color",
            _ => "Any"
        };
    }
}

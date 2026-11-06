using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipboardApp.WinForms.UI
{
    public sealed class ContextMenuCommand
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        public string Description { get; init; } = "";
        public string Group { get; init; } = "";
        public bool CanHide { get; init; } = true;
    }

    /// <summary>
    /// Central registry for the card right-click menu. Persisted order and
    /// hidden keys are normalized against this list so adding a command in a
    /// later release cannot corrupt an existing profile.
    /// </summary>
    public static class ContextMenuCommands
    {
        public const string CustomizeKey = "customize";

        public static readonly IReadOnlyList<ContextMenuCommand> All = new List<ContextMenuCommand>
        {
            new()
            {
                Key = "copy",
                Label = "Copy to Clipboard",
                Description = "Copies the current clip, or the checked clips as one payload.",
                Group = "clipboard"
            },
            new()
            {
                Key = "copyAs",
                Label = "Copy Selected as...",
                Description = "Plain text, .env, JSON, CSV, or rich text + images.",
                Group = "clipboard"
            },
            new()
            {
                Key = "paste",
                Label = "Paste",
                Description = "Pastes into the resolved or chosen target window.",
                Group = "clipboard"
            },
            new()
            {
                Key = "pastePlain",
                Label = "Paste as Plain Text",
                Description = "Strips formatting before pasting.",
                Group = "clipboard"
            },
            new()
            {
                Key = "pin",
                Label = "Pin / Unpin",
                Description = "Pinned clips are kept above the regular history.",
                Group = "organize"
            },
            new()
            {
                Key = "tags",
                Label = "Assign Tags & Groups...",
                Description = "Applies comma-separated tags to the selected clips.",
                Group = "organize"
            },
            new()
            {
                Key = "edit",
                Label = "Edit Entry...",
                Description = "Opens the alias, tag and content editor.",
                Group = "organize"
            },
            new()
            {
                Key = "transform",
                Label = "Transform Text",
                Description = "Upper/lower/title case, trimming, and JSON formatting.",
                Group = "organize"
            },
            new()
            {
                Key = "delete",
                Label = "Delete",
                Description = "Deletes the selected clip or clips.",
                Group = "danger"
            },
            new()
            {
                Key = CustomizeKey,
                Label = "Customize this menu...",
                Description = "Always available. Hide, show and reorder these commands.",
                Group = "settings",
                CanHide = false
            }
        };

        public static ContextMenuCommand? Find(string key) =>
            All.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        public static List<string> NormalizeOrder(IEnumerable<string>? order)
        {
            var known = All.Select(c => c.Key).ToList();
            var result = new List<string>();

            if (order != null)
            {
                foreach (var raw in order)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var match = known.FirstOrDefault(k =>
                        string.Equals(k, raw.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null && !result.Contains(match, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(match);
                    }
                }
            }

            foreach (var key in known)
            {
                if (!result.Contains(key, StringComparer.OrdinalIgnoreCase)) result.Add(key);
            }

            // The escape hatch stays last and can never be hidden.
            result.RemoveAll(k => string.Equals(k, CustomizeKey, StringComparison.OrdinalIgnoreCase));
            result.Add(CustomizeKey);
            return result;
        }

        public static bool IsHidden(IEnumerable<string>? hiddenKeys, string key)
        {
            var command = Find(key);
            if (command == null || !command.CanHide) return false;

            return (hiddenKeys ?? Enumerable.Empty<string>()).Any(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> VisibleKeys(
            IEnumerable<string>? order,
            IEnumerable<string>? hiddenKeys)
        {
            return NormalizeOrder(order)
                .Where(k => !IsHidden(hiddenKeys, k))
                .ToList();
        }

        public static List<string> NormalizeHidden(IEnumerable<string>? hiddenKeys)
        {
            var knownHideable = All
                .Where(c => c.CanHide)
                .ToDictionary(c => c.Key, c => c.Key, StringComparer.OrdinalIgnoreCase);

            var result = new List<string>();
            foreach (var raw in hiddenKeys ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (knownHideable.TryGetValue(raw.Trim(), out var key) &&
                    !result.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(key);
                }
            }
            return result;
        }
    }
}

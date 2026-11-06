using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipboardApp.Core.Services
{
    public sealed class BatchPastePlan
    {
        public string MergedText { get; init; } = string.Empty;
        public List<string> FilePaths { get; init; } = new();
        public bool FileOnly { get; init; }
        public int EntryCount { get; init; }

        public bool HasContent => FilePaths.Count > 0 || !string.IsNullOrEmpty(MergedText);
    }

    /// <summary>
    /// Builds a deterministic batch payload from the visual order supplied by the UI.
    /// Kept free of WinForms/SQLite so the behavior can be unit tested.
    /// </summary>
    public static class BatchPasteService
    {
        public static BatchPastePlan Build(
            IEnumerable<ClipboardEntry> entries,
            string separator,
            Func<ClipboardEntry, string?>? imageFilePathResolver = null)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var list = entries.ToList();
            var textSegments = new List<string>();
            var filePaths = new List<string>();
            bool hasNonFileEntry = false;

            foreach (var entry in list)
            {
                if (entry.Format == "file" && !string.IsNullOrWhiteSpace(entry.Content))
                {
                    var files = entry.Content
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => f.Length > 0)
                        .ToList();

                    filePaths.AddRange(files);
                    textSegments.Add(entry.Content.Trim());
                    continue;
                }

                if (entry.Format == "image" || entry.HasImage)
                {
                    var resolvedPath = imageFilePathResolver?.Invoke(entry);
                    if (!string.IsNullOrEmpty(resolvedPath))
                    {
                        filePaths.Add(resolvedPath);
                        textSegments.Add(resolvedPath);
                    }
                    else
                    {
                        hasNonFileEntry = true;
                        textSegments.Add("[Image]");
                    }
                    continue;
                }

                hasNonFileEntry = true;
                var mergeText = entry.Format switch
                {
                    "html" => PasteService.HtmlToPlainText(entry.Content ?? string.Empty),
                    "rtf" => PasteService.RtfToPlainText(entry.Content ?? string.Empty),
                    _ => entry.Content ?? string.Empty
                };
                if (!string.IsNullOrEmpty(mergeText))
                {
                    textSegments.Add(mergeText);
                }
            }

            var effectiveSeparator = string.IsNullOrEmpty(separator) ? Environment.NewLine + Environment.NewLine : separator;
            return new BatchPastePlan
            {
                MergedText = string.Join(effectiveSeparator, textSegments),
                FilePaths = filePaths,
                FileOnly = !hasNonFileEntry && filePaths.Count > 0,
                EntryCount = list.Count
            };
        }
    }
}

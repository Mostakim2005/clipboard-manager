using System;
using System.Collections.Generic;

namespace ClipboardApp.Core.Models
{
    public class ClipboardEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public string Alias { get; set; } = "";
        public bool Pinned { get; set; }
        public byte[]? ImageData { get; set; }
        public bool HasImage { get; set; }
        public string Format { get; set; } = "text"; // text, rtf, html, image, file
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string SourceApp { get; set; } = "";
        public bool IsSensitive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<string> Tags { get; set; } = new();
        public string ContentHash { get; set; } = "";
        public int CopyCount { get; set; } = 1;

        /// <summary>
        /// Persistent stable ordering key. Assigned once when the entry is created
        /// and never changed by a normal paste/copy. Pins are handled separately.
        /// </summary>
        public long Seq { get; set; }

        /// <summary>Last time this exact content was copied into the clipboard.</summary>
        public DateTime? LastCopiedAt { get; set; }
    }
}

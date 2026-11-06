using System;
using System.Collections.Generic;

namespace ClipboardApp.Core.Models
{
    public sealed class ApiKey
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Service { get; set; } = "";
        public string Environment { get; set; } = "Production";
        public string Secret { get; set; } = "";
        public string Notes { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? LastCopiedAt { get; set; }
        public int CopyCount { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(unnamed key)" : Name;
    }
}

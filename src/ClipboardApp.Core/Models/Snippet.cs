using System;

namespace ClipboardApp.Core.Models
{
    public class Snippet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Category { get; set; } = "General";
        public string Shortcut { get; set; } = ""; // e.g. ;sig or ;addr for text expansion
    }
}

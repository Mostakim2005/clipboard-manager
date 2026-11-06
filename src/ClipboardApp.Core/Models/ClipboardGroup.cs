using System;

namespace ClipboardApp.Core.Models
{
    public class ClipboardGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
    }
}

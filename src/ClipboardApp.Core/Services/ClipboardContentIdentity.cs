using ClipboardApp.Core.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Single source of truth for clipboard content identity.
    /// Hash includes the content kind (text/image/file/html/rtf) so different
    /// representations of the same bytes never collapse into each other.
    /// </summary>
    public static class ClipboardContentIdentity
    {
        public static string Compute(ClipboardEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            if ((entry.Format == "image" || entry.HasImage) && entry.ImageData is { Length: > 0 })
            {
                return "img:" + HashBytes(entry.ImageData);
            }

            var normalized = Normalize(entry.Content ?? string.Empty);
            var prefix = entry.Format switch
            {
                "file" => "file:",
                "html" => "html:",
                "rtf" => "rtf:",
                _ => "txt:"
            };
            return prefix + HashText(normalized);
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string HashText(string text)
        {
            return HashBytes(Encoding.UTF8.GetBytes(text));
        }

        private static string HashBytes(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }
    }
}

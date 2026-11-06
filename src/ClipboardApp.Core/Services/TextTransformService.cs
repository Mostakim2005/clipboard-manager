using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClipboardApp.Core.Services
{
    public static class TextTransformService
    {
        public static string ToUpper(string input) => input.ToUpperInvariant();

        public static string ToLower(string input) => input.ToLowerInvariant();

        public static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            return textInfo.ToTitleCase(input.ToLower());
        }

        public static string Trim(string input) => input?.Trim() ?? string.Empty;

        public static string RemoveLineBreaks(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, @"\r?\n", " ");
        }

        public static string UrlEncode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return WebUtility.UrlEncode(input);
        }

        public static string UrlDecode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return WebUtility.UrlDecode(input);
        }

        public static string Base64Encode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        public static string Base64Decode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            try
            {
                var bytes = Convert.FromBase64String(input.Trim());
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return input; // Return original if not valid base64
            }
        }

        public static string PrettifyJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            try
            {
                using var doc = JsonDocument.Parse(input);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return input; // Return original if not valid json
            }
        }

        public static string MinifyJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            try
            {
                using var doc = JsonDocument.Parse(input);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return input;
            }
        }

        public static string MergeEntries(System.Collections.Generic.IEnumerable<string> items, string separator = "\n")
        {
            return string.Join(separator, items);
        }

        public static string ToSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var str = input.ToLowerInvariant();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            return Regex.Replace(str, @"[\s-]+", "-").Trim('-');
        }

        public static string ToCamelCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var words = Regex.Split(input, @"[\s_-]+");
            var sb = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (string.IsNullOrEmpty(w)) continue;
                if (sb.Length == 0)
                {
                    sb.Append(char.ToLowerInvariant(w[0])).Append(w.Length > 1 ? w.Substring(1) : "");
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(w[0])).Append(w.Length > 1 ? w.Substring(1).ToLowerInvariant() : "");
                }
            }
            return sb.ToString();
        }

        public static string ToSnakeCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var str = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2");
            str = Regex.Replace(str, @"[\s-]+", "_");
            return str.ToLowerInvariant();
        }
    }
}

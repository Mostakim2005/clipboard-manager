using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Builds one rich clipboard payload that can carry text and real embedded
    /// images. Destinations that understand CF_HTML or RTF receive both; plain
    /// text destinations fall back to the text-only representation.
    /// </summary>
    public sealed class RichMergeResult
    {
        public string PlainText { get; init; } = "";
        public string HtmlFragment { get; init; } = "";
        public string Rtf { get; init; } = "";
        public List<string> ImageEntryIds { get; init; } = new();
        public List<string> SkippedImageEntryIds { get; init; } = new();
        public int IncludedImageCount { get; init; }
        public int TextEntryCount { get; init; }

        public bool IncludesImages => IncludedImageCount > 0;
        public bool HasContent =>
            !string.IsNullOrEmpty(PlainText) ||
            !string.IsNullOrEmpty(HtmlFragment) ||
            !string.IsNullOrEmpty(Rtf);
    }

    public static class RichMergeService
    {
        private const int MaxEmbeddedDimension = 2000;
        private const int MaxOriginalBytes = 6 * 1024 * 1024;

        public static bool IsImageEntry(ClipboardEntry entry) =>
            entry != null && (entry.Format == "image" || entry.HasImage);

        public static RichMergeResult Build(
            IEnumerable<ClipboardEntry> entries,
            string separator,
            Func<ClipboardEntry, byte[]?>? imageBytesResolver)
        {
            var list = entries?.ToList() ?? new List<ClipboardEntry>();
            var plainSegments = new List<string>();
            var htmlSegments = new List<string>();
            var rtfParts = new List<string>();
            var imageIds = new List<string>();
            var skippedIds = new List<string>();
            int includedImages = 0;
            int textEntries = 0;

            foreach (var entry in list)
            {
                if (IsImageEntry(entry))
                {
                    imageIds.Add(entry.Id);
                    var bytes = imageBytesResolver?.Invoke(entry);
                    if (bytes == null || bytes.Length == 0)
                    {
                        skippedIds.Add(entry.Id);
                        continue;
                    }

                    var prepared = PreparePng(bytes);
                    var size = GetPngSize(prepared);
                    string base64 = Convert.ToBase64String(prepared);
                    htmlSegments.Add(
                        "<div style=\"margin:8px 0\"><img src=\"data:image/png;base64," + base64 +
                        "\" alt=\"embedded image\" style=\"max-width:100%;height:auto\" /></div>");
                    rtfParts.Add(BuildRtfPicture(prepared, size.Width, size.Height));
                    includedImages++;
                    continue;
                }

                string text = entry.Format switch
                {
                    "html" => PasteService.HtmlToPlainText(entry.Content ?? string.Empty),
                    "rtf" => PasteService.RtfToPlainText(entry.Content ?? string.Empty),
                    _ => entry.Content ?? string.Empty
                };

                if (string.IsNullOrEmpty(text)) continue;

                textEntries++;
                plainSegments.Add(text);
                htmlSegments.Add(
                    "<div style=\"white-space:pre-wrap;font-family:Segoe UI,Arial,sans-serif;font-size:10pt\">" +
                    HtmlEscape(text) + "</div>");
                rtfParts.Add(EscapeRtf(text) + "\\par\r\n");
            }

            string effectiveSeparator = string.IsNullOrEmpty(separator)
                ? Environment.NewLine + Environment.NewLine
                : separator;

            string plain = string.Join(effectiveSeparator, plainSegments);
            string html = string.Join(
                "<hr style=\"border:0;border-top:1px solid #d4d4d8;margin:8px 0\" />",
                htmlSegments);
            string rtf = "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\f0\\fs20 " +
                         string.Join("\r\n", rtfParts) + "}";

            return new RichMergeResult
            {
                PlainText = plain,
                HtmlFragment = html,
                Rtf = rtf,
                ImageEntryIds = imageIds,
                SkippedImageEntryIds = skippedIds,
                IncludedImageCount = includedImages,
                TextEntryCount = textEntries
            };
        }

        private static string HtmlEscape(string text)
        {
            string encoded = System.Net.WebUtility.HtmlEncode(text);
            return encoded
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br />");
        }

        private static string EscapeRtf(string text)
        {
            var sb = new StringBuilder(text.Length + 16);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\':
                    case '{':
                    case '}':
                        sb.Append('\\').Append(c);
                        break;
                    case '\r':
                        break;
                    case '\n':
                        sb.Append("\\par\r\n");
                        break;
                    case '\t':
                        sb.Append("\\tab ");
                        break;
                    default:
                        if (c < 32)
                        {
                            // Drop other control characters.
                        }
                        else if (c > 127)
                        {
                            sb.Append("\\u").Append((int)c).Append('?');
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string BuildRtfPicture(byte[] png, int width, int height)
        {
            int w = Math.Max(1, width);
            int h = Math.Max(1, height);
            int wGoal = w * 15;
            int hGoal = h * 15;
            return "{\\pict\\pngblip\\picw" + w + "\\pich" + h +
                   "\\picwgoal" + wGoal + "\\pichgoal" + hGoal + "\r\n" +
                   ToRtfHex(png) + "}";
        }

        private static string ToRtfHex(byte[] data)
        {
            string hex = Convert.ToHexString(data);
            var sb = new StringBuilder(hex.Length + (hex.Length / 128) + 2);
            for (int i = 0; i < hex.Length; i++)
            {
                sb.Append(hex[i]);
                if ((i + 1) % 128 == 0) sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private static byte[] PreparePng(byte[] bytes)
        {
            try
            {
                using var source = new MemoryStream(bytes);
                using var image = Image.FromStream(source);

                int width = image.Width;
                int height = image.Height;

                if (image.Width > MaxEmbeddedDimension ||
                    image.Height > MaxEmbeddedDimension ||
                    bytes.Length > MaxOriginalBytes)
                {
                    double scale = Math.Min(
                        1.0,
                        Math.Min(
                            (double)MaxEmbeddedDimension / Math.Max(1, image.Width),
                            (double)MaxEmbeddedDimension / Math.Max(1, image.Height)));
                    width = Math.Max(1, (int)Math.Round(image.Width * scale));
                    height = Math.Max(1, (int)Math.Round(image.Height * scale));
                }

                // Always re-encode to PNG so the HTML data URI and the RTF
                // \pngblip header agree even when the source was JPEG/GIF/BMP.
                using var bitmap = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, width, height);
                }

                using var output = new MemoryStream();
                bitmap.Save(output, ImageFormat.Png);
                return output.ToArray();
            }
            catch
            {
                return bytes;
            }
        }

        private static (int Width, int Height) GetPngSize(byte[] png)
        {
            try
            {
                using var stream = new MemoryStream(png);
                using var image = Image.FromStream(stream);
                return (image.Width, image.Height);
            }
            catch
            {
                return (1, 1);
            }
        }
    }
}

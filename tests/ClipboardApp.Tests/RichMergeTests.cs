using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;

namespace ClipboardApp.Tests
{
    public class RichMergeTests
    {
        private static byte[] CreatePng(int width = 4, int height = 4)
        {
            using var bitmap = new Bitmap(width, height);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        [Fact]
        public void BuildEmbedsTextAndImageInHtmlAndRtf()
        {
            byte[] png = CreatePng();
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "text", Content = "hello <world>" },
                new() { Format = "image", ImageData = png, HasImage = true }
            };

            var result = RichMergeService.Build(entries, Environment.NewLine, e => e.ImageData);

            Assert.Equal(1, result.TextEntryCount);
            Assert.Equal(1, result.IncludedImageCount);
            Assert.Single(result.ImageEntryIds);
            Assert.Empty(result.SkippedImageEntryIds);
            Assert.Contains("data:image/png;base64,", result.HtmlFragment);
            Assert.Contains("\\pngblip", result.Rtf);
            Assert.Contains("hello &lt;world&gt;", result.HtmlFragment);
            Assert.Contains("hello <world>", result.PlainText);
        }

        [Fact]
        public void MissingImageBytesAreReportedAsSkipped()
        {
            var image = new ClipboardEntry { Format = "image", HasImage = true };
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "text", Content = "only text" },
                image
            };

            var result = RichMergeService.Build(entries, "\n", _ => null);

            Assert.Equal(0, result.IncludedImageCount);
            Assert.Single(result.SkippedImageEntryIds);
            Assert.Contains(image.Id, result.SkippedImageEntryIds);
            Assert.Contains("only text", result.PlainText);
        }

        [Fact]
        public void RtfEscapesUnicodeAndControlCharacters()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "text", Content = "café\t{ok}\\done" }
            };

            var result = RichMergeService.Build(entries, "\n", null);

            Assert.Contains("caf\\u233?", result.Rtf);
            Assert.Contains("\\tab", result.Rtf);
            Assert.Contains("\\{ok\\}", result.Rtf);
            Assert.Contains("\\\\done", result.Rtf);
        }

        [Fact]
        public void HtmlEntriesUsePlainTextRepresentation()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "html", Content = "<p>Hello <b>rich</b></p>" }
            };

            var result = RichMergeService.Build(entries, "\n", null);

            Assert.Contains("Hello", result.PlainText);
            Assert.Contains("rich", result.PlainText);
            Assert.DoesNotContain("<b>", result.PlainText);
        }
    }
}

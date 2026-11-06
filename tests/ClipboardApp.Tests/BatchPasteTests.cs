using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace ClipboardApp.Tests
{
    public class BatchPasteTests
    {
        [Fact]
        public void TextEntriesAreMergedInVisualOrder()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "first", Format = "text" },
                new() { Content = "second", Format = "text" },
                new() { Content = "third", Format = "text" }
            };

            var plan = BatchPasteService.Build(entries, "\r\n");

            Assert.Equal("first\r\nsecond\r\nthird", plan.MergedText);
            Assert.False(plan.FileOnly);
            Assert.True(plan.HasContent);
        }

        [Fact]
        public void FileOnlySelectionBuildsFileList()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = @"C:\temp\a.txt", Format = "file" },
                new() { Content = @"C:\temp\b.txt", Format = "file" }
            };

            var plan = BatchPasteService.Build(entries, "\r\n\r\n");

            Assert.True(plan.FileOnly);
            Assert.Equal(2, plan.FilePaths.Count);
            Assert.Contains(@"C:\temp\a.txt", plan.FilePaths);
        }

        [Fact]
        public void MixedSelectionIsNotFileOnly()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = @"C:\temp\a.txt", Format = "file" },
                new() { Content = "notes", Format = "text" }
            };

            var plan = BatchPasteService.Build(entries, "\r\n\r\n");

            Assert.False(plan.FileOnly);
            Assert.Single(plan.FilePaths);
            Assert.Contains("notes", plan.MergedText);
        }

        [Fact]
        public void ImageResolverProducesAPathEntry()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "image", HasImage = true }
            };

            var plan = BatchPasteService.Build(entries, "\r\n", _ => @"C:\temp\drop.png");

            Assert.True(plan.FileOnly);
            Assert.Single(plan.FilePaths);
            Assert.Equal(@"C:\temp\drop.png", plan.FilePaths[0]);
        }

        [Fact]
        public void HtmlEntriesAreMergedAsPlainText()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "<p>Hello <b>world</b></p>", Format = "html" },
                new() { Content = "tail", Format = "text" }
            };

            var plan = BatchPasteService.Build(entries, "\r\n");

            Assert.Contains("Hello world", plan.MergedText);
            Assert.DoesNotContain("<p>", plan.MergedText);
            Assert.Contains("tail", plan.MergedText);
        }

        [Fact]
        public void EmptySelectionHasNoContent()
        {
            var plan = BatchPasteService.Build(new List<ClipboardEntry>(), "\r\n");
            Assert.False(plan.HasContent);
            Assert.Equal(0, plan.EntryCount);
        }

        [Fact]
        public void RichFormatIsAcceptedAsAFirstClassCopyFormat()
        {
            Assert.Equal("Rich", BatchCopyService.NormalizeFormat("rich"));
            Assert.Equal("Rich", BatchCopyService.NormalizeFormat("Rich"));
        }
    }
}

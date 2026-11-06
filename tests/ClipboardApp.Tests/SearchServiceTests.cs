using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace ClipboardApp.Tests
{
    public class SearchServiceTests
    {
        [Fact]
        public void TestSearchByAlias()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Alias = "work", Content = "x" },
                new() { Alias = "home", Content = "y" }
            };
            var svc = new SearchService();
            var r = svc.SearchByAlias(entries, "work");
            Assert.Single(r);
            Assert.Equal("work", r[0].Alias);
        }

        [Fact]
        public void TestPinnedFirst()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "normal", Pinned = false },
                new() { Content = "pinned", Pinned = true }
            };
            var svc = new SearchService();
            var r = svc.GetPinnedFirst(entries);
            Assert.Equal("pinned", r[0].Content);
        }

        [Fact]
        public void TestPinnedFirstPreservesSeqOrder()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "seq-1", Seq = 1 },
                new() { Content = "seq-3", Seq = 3 },
                new() { Content = "seq-2", Seq = 2 },
                new() { Content = "pinned-1", Pinned = true, Seq = 1 }
            };
            var svc = new SearchService();
            var r = svc.GetPinnedFirst(entries);
            Assert.Equal("pinned-1", r[0].Content);
            Assert.Equal("seq-3", r[1].Content);
            Assert.Equal("seq-2", r[2].Content);
            Assert.Equal("seq-1", r[3].Content);
        }

        [Fact]
        public void TestFilterByDate()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { CreatedAt = new System.DateTime(2026, 10, 18) },
                new() { CreatedAt = new System.DateTime(2026, 10, 17) }
            };
            var svc = new SearchService();
            var r = svc.FilterByDate(entries, new System.DateTime(2026, 10, 18));
            Assert.Single(r);
        }

        [Fact]
        public void TestFilterEntriesByTag()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "snippet 1", Tags = new List<string> { "csharp", "backend" } },
                new() { Content = "snippet 2", Tags = new List<string> { "frontend", "react" } }
            };
            var svc = new SearchService();
            var r1 = svc.FilterEntries(entries, "#csharp", useRegex: false, formatFilter: "All");
            Assert.Single(r1);
            Assert.Equal("snippet 1", r1[0].Content);

            var r2 = svc.FilterEntries(entries, "react", useRegex: false, formatFilter: "All");
            Assert.Single(r2);
            Assert.Equal("snippet 2", r2[0].Content);
        }

        [Fact]
        public void TestFormatFiltering()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Format = "color", Content = "#FF5733" },
                new() { Format = "text", Content = "https://github.com/project" },
                new() { Format = "image", HasImage = true },
                new() { Format = "file", Content = "C:\\docs\\report.pdf" },
                new() { Format = "text", Content = "plain words" }
            };
            var svc = new SearchService();

            var colors = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "Color");
            Assert.Single(colors);
            Assert.Equal("#FF5733", colors[0].Content);

            var links = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "Link");
            Assert.Single(links);
            Assert.Equal("https://github.com/project", links[0].Content);

            var images = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "Image");
            Assert.Single(images);
            Assert.Equal("image", images[0].Format);

            var files = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "File");
            Assert.Single(files);
            Assert.Equal("file", files[0].Format);
        }
    }
}

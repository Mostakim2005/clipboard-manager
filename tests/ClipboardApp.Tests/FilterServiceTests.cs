using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ClipboardApp.Tests
{
    public class FilterServiceTests
    {
        private static ClipboardEntry Entry(string content, string format, DateTime createdAt, string app = "App") =>
            new()
            {
                Content = content,
                Format = format,
                CreatedAt = createdAt,
                SourceApp = app
            };

        [Fact]
        public void TypeFilterRecognizesLinksColorsFilesAndImages()
        {
            var today = DateTime.Today.AddHours(9);
            var image = Entry("", "image", today);
            image.HasImage = true;
            image.ImageData = new byte[] { 1, 2, 3 };

            var entries = new List<ClipboardEntry>
            {
                Entry("https://example.com/path", "text", today),
                Entry("#ff8800", "text", today),
                Entry(@"C:\temp\a.txt", "file", today),
                image,
                Entry("plain words", "text", today)
            };

            Assert.Single(FilterService.Apply(entries, "Any", "Link", "Any"));
            Assert.Single(FilterService.Apply(entries, "Any", "Color", "Any"));
            Assert.Single(FilterService.Apply(entries, "Any", "File", "Any"));
            Assert.Single(FilterService.Apply(entries, "Any", "Image", "Any"));
            Assert.Equal(3, FilterService.Apply(entries, "Any", "Text", "Any").Count());
        }

        [Fact]
        public void DateFilterUsesInclusiveDayBoundaries()
        {
            var today = DateTime.Today;
            Assert.True(FilterService.MatchesDate(today.AddHours(1), "Today"));
            Assert.False(FilterService.MatchesDate(today.AddDays(-1), "Today"));
            Assert.True(FilterService.MatchesDate(today.AddDays(-1), "Yesterday"));
            Assert.True(FilterService.MatchesDate(today.AddDays(-6), "Past7"));
            Assert.False(FilterService.MatchesDate(today.AddDays(-7), "Past7"));
            Assert.True(FilterService.MatchesDate(today.AddDays(-29), "Past30"));
            Assert.False(FilterService.MatchesDate(today.AddDays(-30), "Past30"));
            Assert.True(FilterService.MatchesDate(today.AddDays(-30), "Older"));
        }

        [Fact]
        public void AppFilterIsCaseInsensitiveAndCombinesWithType()
        {
            var today = DateTime.Today.AddHours(10);
            var entries = new List<ClipboardEntry>
            {
                Entry("https://a.test", "text", today, "Chrome"),
                Entry("https://b.test", "text", today, "Edge"),
                Entry("not a link", "text", today, "Chrome")
            };

            var result = FilterService.Apply(entries, "Any", "Link", "chrome").ToList();

            Assert.Single(result);
            Assert.Equal("https://a.test", result[0].Content);
        }

        [Fact]
        public void UnknownFilterValuesMeanAny()
        {
            var entries = new List<ClipboardEntry>
            {
                Entry("one", "text", DateTime.Today),
                Entry("two", "file", DateTime.Today)
            };

            Assert.Equal(2, FilterService.Apply(entries, "nonsense", "nonsense", "Any").Count());
        }
    }
}

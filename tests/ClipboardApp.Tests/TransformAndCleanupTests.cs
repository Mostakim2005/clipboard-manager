using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using ClipboardApp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ClipboardApp.Tests
{
    public class TransformAndCleanupTests : IDisposable
    {
        private readonly SqliteClipboardRepository _repo;

        public TransformAndCleanupTests()
        {
            _repo = new SqliteClipboardRepository("Data Source=:memory:");
        }

        [Fact]
        public void TestTextTransforms()
        {
            var raw = "  hello world\nline 2  ";
            Assert.Equal("HELLO WORLD", TextTransformService.ToUpper("hello world"));
            Assert.Equal("hello world", TextTransformService.ToLower("HELLO WORLD"));
            Assert.Equal("Hello World", TextTransformService.ToTitleCase("hello world"));
            Assert.Equal("hello world\nline 2", TextTransformService.Trim(raw));
            Assert.Equal("  hello world line 2  ", TextTransformService.RemoveLineBreaks(raw));

            var url = "https://example.com/search?q=test string&id=1";
            var encoded = TextTransformService.UrlEncode(url);
            var decoded = TextTransformService.UrlDecode(encoded);
            Assert.Equal(url, decoded);

            var base64 = TextTransformService.Base64Encode("SecretData");
            var original = TextTransformService.Base64Decode(base64);
            Assert.Equal("SecretData", original);

            var json = "{\"name\":\"Alice\",\"age\":30}";
            var prettified = TextTransformService.PrettifyJson(json);
            Assert.Contains("\n", prettified);
            var minified = TextTransformService.MinifyJson(prettified);
            Assert.DoesNotContain("\n", minified);

            Assert.Equal("hello-world-2026", TextTransformService.ToSlug("Hello World! 2026"));
            Assert.Equal("helloWorldTest", TextTransformService.ToCamelCase("hello_world-test"));
            Assert.Equal("hello_world_test", TextTransformService.ToSnakeCase("helloWorldTest"));
        }

        [Fact]
        public void TestCsvExport_FormulaInjectionSanitization()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "=cmd|' /C calc'!A0", Alias = "+formula", SourceApp = "-app" },
                new() { Content = "@SUM(1+1)", Alias = "\ttab", SourceApp = "normal" }
            };

            var csv = ExportImportService.ExportToCsv(entries);
            Assert.Contains("\"'=cmd|' /C calc'!A0\"", csv);
            Assert.Contains("\"'+formula\"", csv);
            Assert.Contains("\"'-app\"", csv);
            Assert.Contains("\"'@SUM(1+1)\"", csv);
            Assert.Contains("\"'\ttab\"", csv);
        }

        [Fact]
        public void TestExportImportRoundtrip()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "entry 1", Alias = "a1", Pinned = true, Format = "text" },
                new() { Content = "entry 2", Alias = "a2", Pinned = false, Format = "html" }
            };

            // Plain JSON
            var json = ExportImportService.ExportToJson(entries, encrypt: false);
            var imported = ExportImportService.ImportFromJson(json);
            Assert.Equal(2, imported.Count);
            Assert.Equal("entry 1", imported[0].Content);

            // Encrypted JSON
            var encJson = ExportImportService.ExportToJson(entries, encrypt: true);
            Assert.StartsWith("ENC:", encJson);
            var importedEnc = ExportImportService.ImportFromJson(encJson);
            Assert.Equal(2, importedEnc.Count);
            Assert.Equal("entry 2", importedEnc[1].Content);

            // CSV
            var csv = ExportImportService.ExportToCsv(entries);
            Assert.Contains("\"entry 1\"", csv);
            Assert.Contains("\"entry 2\"", csv);
        }

        [Fact]
        public void TestPruning_CountAndPinnedPreservation()
        {
            // Insert 10 unpinned entries
            for (int i = 0; i < 10; i++)
            {
                _repo.InsertEntry(new ClipboardEntry
                {
                    Content = $"unpinned {i}",
                    Pinned = false,
                    CreatedAt = DateTime.Now.AddMinutes(-10 + i)
                });
            }

            // Insert 2 pinned entries
            var pinned1 = new ClipboardEntry { Content = "pinned 1", Pinned = true, CreatedAt = DateTime.Now.AddDays(-5) };
            var pinned2 = new ClipboardEntry { Content = "pinned 2", Pinned = true, CreatedAt = DateTime.Now.AddDays(-10) };
            _repo.InsertEntry(pinned1);
            _repo.InsertEntry(pinned2);

            // Total is 12. Prune maxCount = 5
            int pruned = _repo.PruneEntries(maxCount: 5, maxAgeDays: 0);

            var all = _repo.GetAllEntries();
            // Pinned items must never be pruned
            Assert.Contains(all, e => e.Content == "pinned 1");
            Assert.Contains(all, e => e.Content == "pinned 2");

            // Remaining unpinned items should be at most 5
            var remainingUnpinned = all.Where(e => !e.Pinned).ToList();
            Assert.True(remainingUnpinned.Count <= 5);
        }

        [Fact]
        public void TestClearExpiredSensitiveEntries()
        {
            var expired = new ClipboardEntry
            {
                Content = "expired secret",
                IsSensitive = true,
                ExpiresAt = DateTime.Now.AddMinutes(-5) // Expired 5 min ago
            };
            var active = new ClipboardEntry
            {
                Content = "active secret",
                IsSensitive = true,
                ExpiresAt = DateTime.Now.AddMinutes(15) // Active
            };

            _repo.InsertEntry(expired);
            _repo.InsertEntry(active);

            int cleared = _repo.ClearExpired();
            Assert.Equal(1, cleared);

            var all = _repo.GetAllEntries();
            Assert.DoesNotContain(all, e => e.Content == "expired secret");
            Assert.Contains(all, e => e.Content == "active secret");
        }

        [Fact]
        public void TestSearchService_RegexAndFormatFilter()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Content = "https://github.com/project", Format = "text" },
                new() { Content = "#FF5733", Format = "text" },
                new() { Content = "Plain notes", Format = "text" },
                new() { Content = "invoice.pdf", Format = "file" },
                new() { Content = "", Format = "image", HasImage = true }
            };

            var svc = new SearchService();

            // Link format filter
            var links = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "link");
            Assert.Single(links);
            Assert.Contains("github.com", links[0].Content);

            // Color format filter
            var colors = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "color");
            Assert.Single(colors);
            Assert.Equal("#FF5733", colors[0].Content);

            // Image format filter
            var images = svc.FilterEntries(entries, "", useRegex: false, formatFilter: "image");
            Assert.Single(images);
            Assert.Equal("image", images[0].Format);

            // Regex query
            var regexResults = svc.FilterEntries(entries, @"#[0-9A-Fa-f]{6}", useRegex: true, formatFilter: "all");
            Assert.Single(regexResults);
            Assert.Equal("#FF5733", regexResults[0].Content);
        }

        public void Dispose() => _repo.Dispose();
    }
}

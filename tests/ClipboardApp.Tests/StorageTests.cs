using ClipboardApp.Core.Models;
using ClipboardApp.Core.Storage;
using System;
using System.Linq;
using Xunit;

namespace ClipboardApp.Tests
{
    public class StorageTests : IDisposable
    {
        private readonly SqliteClipboardRepository _repo;

        public StorageTests()
        {
            _repo = new SqliteClipboardRepository("Data Source=:memory:");
        }

        [Fact]
        public void TestInsertAndRetrieve()
        {
            var entry = new ClipboardEntry { Content = "hello", Alias = "greeting", Format = "text" };
            _repo.InsertEntry(entry);
            var all = _repo.GetAllEntries();
            Assert.Contains(all, e => e.Id == entry.Id && e.Content == "hello");
        }

        [Fact]
        public void TestImageStorage()
        {
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var entry = new ClipboardEntry { Alias = "img", Format = "image", ImageData = png };
            _repo.InsertEntry(entry);
            var found = _repo.GetAllEntries().First(e => e.Alias == "img");
            Assert.Equal(png, found.ImageData);
        }

        [Fact]
        public void TestSearchByAlias()
        {
            _repo.InsertEntry(new ClipboardEntry { Alias = "work-email", Content = "a@b.com" });
            _repo.InsertEntry(new ClipboardEntry { Alias = "home-email", Content = "c@d.com" });
            var results = _repo.SearchByAlias("work");
            Assert.Single(results);
            Assert.Equal("work-email", results[0].Alias);
        }

        [Fact]
        public void TestPinSortsFirst()
        {
            _repo.InsertEntry(new ClipboardEntry { Content = "normal" });
            var pinned = new ClipboardEntry { Content = "pinned", Pinned = true };
            _repo.InsertEntry(pinned);
            var all = _repo.GetAllEntries();
            Assert.Equal("pinned", all[0].Content);
        }

        [Fact]
        public void TestDelete()
        {
            var entry = new ClipboardEntry { Content = "temp" };
            _repo.InsertEntry(entry);
            _repo.DeleteEntry(entry.Id);
            Assert.DoesNotContain(_repo.GetAllEntries(), e => e.Id == entry.Id);
        }

        [Fact]
        public void TestTagsPersistence()
        {
            var entry = new ClipboardEntry
            {
                Content = "tagged entry",
                Tags = new System.Collections.Generic.List<string> { "project", "urgent", "v2" }
            };
            _repo.InsertEntry(entry);
            var retrieved = _repo.GetEntryById(entry.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(3, retrieved!.Tags.Count);
            Assert.Contains("project", retrieved.Tags);
            Assert.Contains("urgent", retrieved.Tags);
            Assert.Contains("v2", retrieved.Tags);

            // Test search by tag
            var searchResults = _repo.SearchByTag("urgent");
            Assert.Single(searchResults);
            Assert.Equal(entry.Id, searchResults[0].Id);
        }

        [Fact]
        public void TestSha256Deduplication()
        {
            var entry1 = new ClipboardEntry { Content = "repeated secret payload", Format = "text" };
            _repo.InsertEntry(entry1);
            Assert.Equal(1, entry1.CopyCount);

            // Second insert of identical payload
            var entry2 = new ClipboardEntry { Content = "repeated secret payload", Format = "text" };
            _repo.InsertEntry(entry2);

            // Verify entry2 reuses entry1 ID and increments CopyCount
            Assert.Equal(entry1.Id, entry2.Id);
            Assert.Equal(2, entry2.CopyCount);

            // Verify total entries in database is still 1
            var all = _repo.GetAllEntries();
            Assert.Single(all);
            Assert.Equal(entry1.Id, all[0].Id);
            Assert.Equal(2, all[0].CopyCount);
        }

        [Fact]
        public void TestDeduplicationKeepPositionDoesNotReorder()
        {
            var first = new ClipboardEntry { Content = "first", Format = "text" };
            var second = new ClipboardEntry { Content = "second", Format = "text" };
            _repo.InsertEntry(first);
            _repo.InsertEntry(second);

            long originalSeq = first.Seq;
            var before = _repo.GetAllEntries();
            Assert.Equal("second", before[0].Content);
            Assert.Equal("first", before[1].Content);

            _repo.DeduplicationMode = DeduplicationMode.KeepPosition;
            var duplicate = new ClipboardEntry { Content = "first", Format = "text" };
            _repo.InsertEntry(duplicate);

            Assert.Equal(first.Id, duplicate.Id);
            Assert.Equal(2, duplicate.CopyCount);

            var after = _repo.GetAllEntries();
            Assert.Equal(2, after.Count);
            Assert.Equal("second", after[0].Content);
            Assert.Equal("first", after[1].Content);
            Assert.Equal(originalSeq, after[1].Seq);
            Assert.Equal(2, after[1].CopyCount);
        }

        [Fact]
        public void TestDeduplicationMoveToTopMovesExistingEntry()
        {
            var first = new ClipboardEntry { Content = "first", Format = "text" };
            var second = new ClipboardEntry { Content = "second", Format = "text" };
            _repo.InsertEntry(first);
            _repo.InsertEntry(second);

            _repo.DeduplicationMode = DeduplicationMode.MoveToTop;
            var duplicate = new ClipboardEntry { Content = "first", Format = "text" };
            _repo.InsertEntry(duplicate);

            var after = _repo.GetAllEntries();
            Assert.Equal(2, after.Count);
            Assert.Equal("first", after[0].Content);
            Assert.True(after[0].Seq > after[1].Seq);
            Assert.Equal(2, after[0].CopyCount);
        }

        [Fact]
        public void TestAllowDuplicatesCreatesNewRow()
        {
            _repo.DeduplicationMode = DeduplicationMode.AllowDuplicates;
            _repo.InsertEntry(new ClipboardEntry { Content = "same", Format = "text" });
            _repo.InsertEntry(new ClipboardEntry { Content = "same", Format = "text" });

            var all = _repo.GetAllEntries();
            Assert.Equal(2, all.Count);
            Assert.All(all, e => Assert.Equal("same", e.Content));
        }

        [Fact]
        public void TestPinnedEntryKeepsStableOrderWithinGroups()
        {
            var a = new ClipboardEntry { Content = "a" };
            var b = new ClipboardEntry { Content = "b" };
            var c = new ClipboardEntry { Content = "c", Pinned = true };
            _repo.InsertEntry(a);
            _repo.InsertEntry(b);
            _repo.InsertEntry(c);

            var all = _repo.GetAllEntries();
            Assert.Equal("c", all[0].Content); // pinned stays first
            Assert.Equal("b", all[1].Content); // unpinned stay newest-first
            Assert.Equal("a", all[2].Content);
        }

        public void Dispose() => _repo.Dispose();
    }
}

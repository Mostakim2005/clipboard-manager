using ClipboardApp.Core.Models;
using System;
using System.Collections.Generic;

namespace ClipboardApp.Core.Storage
{
    public interface IClipboardRepository : IDisposable
    {
        void InsertEntry(ClipboardEntry entry);
        ClipboardEntry? GetEntryById(string id);
        ClipboardEntry? GetNewestEntry();
        byte[]? GetImageData(string id);
        List<ClipboardEntry> GetAllEntries(bool includeImageData = true);
        List<ClipboardEntry> GetEntriesByDate(DateTime date);
        List<ClipboardEntry> SearchByAlias(string alias);
        List<ClipboardEntry> SearchByTag(string tag);
        void UpdateEntry(ClipboardEntry entry);
        void DeleteEntry(string id);
        void DeleteEntries(List<string> ids);
        void ClearAll(bool includePinned = false);
        void PinEntry(string id);
        void TogglePin(string id);
        int PruneEntries(int maxCount, int maxAgeDays);
        int ClearExpired();
        void AddGroup(ClipboardGroup group);
        List<ClipboardGroup> GetAllGroups();
        void AddTag(Tag entry);
        List<Tag> GetAllTags();
        void AssignEntryToGroup(string entryId, string groupId);
        void AssignEntryToTag(string entryId, string tagId);
        void RemoveEntryFromGroup(string entryId, string groupId);
        void RemoveEntryFromTag(string entryId, string tagId);
    }
}

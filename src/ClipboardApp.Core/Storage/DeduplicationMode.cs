namespace ClipboardApp.Core.Storage
{
    public enum DeduplicationMode
    {
        /// <summary>Keep the existing entry where it is; only update copy metadata.</summary>
        KeepPosition,
        /// <summary>Move the existing entry to the top of the unpinned history.</summary>
        MoveToTop,
        /// <summary>Always insert a new row, even when the content hash matches.</summary>
        AllowDuplicates
    }
}

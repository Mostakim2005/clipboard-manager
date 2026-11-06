using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClipboardApp.WinForms.Controls
{
    /// <summary>
    /// Decodes PNG bytes into a small, aspect-preserving thumbnail for the card
    /// list and grid tiles.
    ///
    /// Design notes:
    ///  - Thumbnails come from the DPAPI-decrypted bytes supplied by the caller;
    ///    this class never touches the database.
    ///  - Results are cached per entry id and bounded by a small LRU so scrolling
    ///    a long image history cannot grow memory without limit.
    ///  - Letterboxing (rather than stretching) is the whole point: an image must
    ///    never be distorted to fit a tile.
    ///  - All bitmaps are owned by the cache and disposed when evicted.
    /// </summary>
    public sealed class ThumbnailCache : IDisposable
    {
        private const int MaxCachedThumbnails = 160;

        private readonly Dictionary<(string Id, int Width, int Height), LinkedListNode<CacheItem>> _map = new();
        private readonly LinkedList<CacheItem> _order = new(); // most recently used first
        private readonly object _sync = new();
        private bool _disposed;

        private sealed class CacheItem
        {
            public string Id { get; init; } = "";
            public Bitmap Bitmap { get; init; } = null!;
        }

        /// <summary>
        /// Returns a bitmap that fits inside <paramref name="maxWidth"/> x
        /// <paramref name="maxHeight"/> with the source aspect ratio preserved.
        /// Returns null when the bytes are not a decodable image.
        /// </summary>
        public Bitmap? GetThumbnail(string entryId, byte[]? pngBytes, int maxWidth, int maxHeight)
        {
            if (_disposed || pngBytes == null || pngBytes.Length == 0) return null;
            if (maxWidth < 8 || maxHeight < 8) return null;

            var key = (entryId, maxWidth, maxHeight);
            lock (_sync)
            {
                if (_map.TryGetValue(key, out var cached))
                {
                    if (cached != _order.First)
                    {
                        _order.Remove(cached);
                        _order.AddFirst(cached);
                    }
                    return cached.Value.Bitmap;
                }
            }

            Bitmap created;
            try
            {
                using var ms = new System.IO.MemoryStream(pngBytes, writable: false);
                using var source = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                created = RenderFitted(source, maxWidth, maxHeight);
            }
            catch
            {
                return null;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    created.Dispose();
                    return null;
                }

                if (_map.TryGetValue(key, out var existing)) return existing.Value.Bitmap;

                var node = _order.AddFirst(new CacheItem { Id = entryId, Bitmap = created });
                _map[key] = node;

                while (_order.Count > MaxCachedThumbnails)
                {
                    var last = _order.Last!;
                    _order.RemoveLast();
                    foreach (var pair in _map)
                    {
                        if (ReferenceEquals(pair.Value, last))
                        {
                            _map.Remove(pair.Key);
                            break;
                        }
                    }
                    last.Value.Bitmap.Dispose();
                }
            }

            return created;
        }

        private static Bitmap RenderFitted(Image source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bitmap);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY); // avoids transparent edge artifacts
            g.DrawImage(source,
                new Rectangle(0, 0, width, height),
                0, 0, source.Width, source.Height,
                GraphicsUnit.Pixel, attributes);
            return bitmap;
        }

        public void Clear()
        {
            lock (_sync)
            {
                foreach (var item in _order) item.Bitmap.Dispose();
                _order.Clear();
                _map.Clear();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (var item in _order) item.Bitmap.Dispose();
                _order.Clear();
                _map.Clear();
            }
            GC.SuppressFinalize(this);
        }
    }
}

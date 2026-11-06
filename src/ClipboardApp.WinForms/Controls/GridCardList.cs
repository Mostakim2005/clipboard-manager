using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    /// <summary>
    /// Grid presentation of clipboard entries.
    ///
    /// Each entry is one tile. Text tiles are one text row tall; image tiles grow
    /// to roughly twice that height so a real thumbnail fits, and the thumbnail is
    /// always letterboxed (aspect ratio preserved, never stretched). Group headers
    /// still render as full-width separators, matching the list view.
    ///
    /// Selection semantics are deliberately identical to <see cref="ModernCardList"/>
    /// (checkbox, Ctrl/Shift, marquee, Space, Ctrl+A) so switching views never
    /// changes what a gesture means. Drag arming and double-click activation use
    /// the same contracts.
    /// </summary>
    public sealed class GridCardList : ScrollableControl
    {
        private const int TileGutter = 6;
        private const int TilePadding = 10;
        private const int CheckboxSize = 16;
        private const int HeaderHeight = 28;
        private const int DragThreshold = 8;

        private readonly ThumbnailCache _thumbnails;
        private readonly Func<ClipboardEntry, byte[]?> _imageProvider;

        private readonly List<object> _items = new();
        private readonly HashSet<string> _checkedIds = new();
        private readonly HashSet<string> _unmaskedEntryIds = new();
        private readonly List<TileLayout> _layout = new();

        private int _rows;
        private int _contentHeight;
        private Point _mouseDownPoint;
        private bool _leftDown;
        private bool _pendingDrag;
        private bool _pressOnCheckbox;
        private bool _isMarqueeActive;
        private Rectangle _marqueeRect;
        private readonly HashSet<string> _marqueeBaseline = new();
        private bool _suppressClick;
        private bool _movedDuringPress;
        private bool _isDragging;
        private string? _pressEntryId;
        private int _hoveredRank = -1;
        private ClipboardEntry? _pendingActivateEntry;
        private int _focusIndex = -1;
        private string? _selectionAnchorId;
        private long _lastClickTick;
        private Point _lastClickPoint;
        private string? _lastClickEntryId;

        private bool _isDark = true;
        private string _visualStyle = "Fluent";
        private bool _showCheckboxes = true;
        private bool _showSerials = true;
        private int _columns; // 0 = auto

        private readonly Font _headerFont = new("Segoe UI", 8.5f, FontStyle.Bold);
        private readonly Font _badgeFont = new("Segoe UI", 7f, FontStyle.Bold);
        private readonly Font _primaryFont = new("Segoe UI", 9f, FontStyle.Regular);
        private readonly Font _primaryBoldFont = new("Segoe UI", 9f, FontStyle.Bold);
        private readonly Font _metaFont = new("Segoe UI", 7.5f, FontStyle.Regular);
        private readonly Font _shortcutFont = new("Segoe UI", 8f, FontStyle.Bold);

        public event EventHandler? SelectionOrCheckedStateChanged;
        public event EventHandler<ClipboardEntry>? StartCardDragRequested;
        public event EventHandler<ClipboardEntry>? CardActivated;
        public event EventHandler<ClipboardEntry>? CardClicked;
        public event EventHandler? DragSessionFinished;

        public bool DragEnabled { get; set; } = true;
        public bool DragArmed { get; set; }
        public bool IsDragging => _isDragging;

        private sealed class TileLayout
        {
            public object Item = null!;
            public Rectangle Bounds;
            public bool IsHeader;
            public int EntryRank; // quick-paste rank among visible entries (#1 = first)
        }

        public GridCardList(ThumbnailCache thumbnails, Func<ClipboardEntry, byte[]?> imageProvider)
        {
            _thumbnails = thumbnails;
            _imageProvider = imageProvider;

            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            AutoScroll = true;
            TabStop = true;
            BackColor = ModernPalette.DarkWindowBg;
        }

        // ------------------------------------------------------------ presentation

        public bool IsDark
        {
            get => _isDark;
            set
            {
                if (_isDark == value) return;
                _isDark = value;
                BackColor = _isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
                Invalidate();
            }
        }

        public string VisualStyle
        {
            get => _visualStyle;
            set { _visualStyle = value; Invalidate(); }
        }

        public bool ShowCheckboxes
        {
            get => _showCheckboxes;
            set { _showCheckboxes = value; Invalidate(); }
        }

        public bool ShowSerials
        {
            get => _showSerials;
            set { _showSerials = value; Invalidate(); }
        }

        /// <summary>0 = auto-fit to the window width; 1-10 = fixed column count.</summary>
        public int Columns
        {
            get => _columns;
            set
            {
                int clamped = Math.Clamp(value, 0, 10);
                if (_columns == clamped) return;
                _columns = clamped;
                Relayout();
            }
        }

        public int TextTileHeight => _showCheckboxes ? 76 : 66;
        public int ImageTileHeight => TextTileHeight * 2 + 8;

        // ------------------------------------------------------------ item API

        public void BeginItems() => _items.Clear();

        public void AddItem(object item) => _items.Add(item);

        public void EndItems() => Relayout();

        public IReadOnlyList<object> Items => _items;

        public int ItemCount => _items.Count;

        /// <summary>Card density is shared with the list view; the grid scales with it.</summary>
        public string CardDensity { get; set; } = "Comfortable";

        /// <summary>Currently focused entry, mirroring ListBox.SelectedItem.</summary>
        public ClipboardEntry? SelectedItem =>
            _focusIndex >= 0 && _focusIndex < _items.Count ? _items[_focusIndex] as ClipboardEntry : null;

        public int SelectedIndex
        {
            get => _focusIndex;
            set
            {
                if (_focusIndex == value) return;
                _focusIndex = value;
                Invalidate();
            }
        }

        public int TopIndex => 0;

        public void BeginUpdate() { }

        public void EndUpdate() => Relayout();

        public int GetVisibleCheckedCount()
        {
            if (_checkedIds.Count == 0) return 0;
            int count = 0;
            foreach (var entry in _items.OfType<ClipboardEntry>())
            {
                if (_checkedIds.Contains(entry.Id)) count++;
            }
            return count;
        }

        public int GetHiddenCheckedCount() => Math.Max(0, _checkedIds.Count - GetVisibleCheckedCount());

        public int GetVisibleEntryCount() => _items.OfType<ClipboardEntry>().Count();

        public void UpdateThumbnails() => Invalidate();

        public HashSet<string> GetCheckedIds() => new(_checkedIds);

        public string? GetFocusedEntryId() => (_focusIndex >= 0 && _focusIndex < _items.Count ? _items[_focusIndex] as ClipboardEntry : null)?.Id;

        public string? GetSelectionAnchorId() => _selectionAnchorId;

        public List<string> GetCheckedOrSelectedIds()
        {
            if (_checkedIds.Count > 0) return _checkedIds.ToList();
            var focused = _focusIndex >= 0 && _focusIndex < _items.Count ? _items[_focusIndex] as ClipboardEntry : null;
            return focused != null ? new List<string> { focused.Id } : new List<string>();
        }

        public void RestoreCheckedIds(IEnumerable<string> ids)
        {
            _checkedIds.Clear();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (!string.IsNullOrEmpty(id)) _checkedIds.Add(id);
                }
            }
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RestoreSelectionState(string? focusedId, string? anchorId)
        {
            _focusIndex = string.IsNullOrEmpty(focusedId) ? -1 : IndexOfEntry(focusedId);
            _selectionAnchorId = string.IsNullOrEmpty(anchorId) ? null : anchorId;
            Invalidate();
        }

        public ClipboardEntry? GetEntryById(string id)
        {
            foreach (var item in _items)
            {
                if (item is ClipboardEntry entry && entry.Id == id) return entry;
            }
            return null;
        }

        public void PruneCheckedToExisting(IEnumerable<string> existingIds)
        {
            var existing = new HashSet<string>(existingIds ?? Array.Empty<string>());
            int removed = _checkedIds.RemoveWhere(id => !existing.Contains(id));
            if (!string.IsNullOrEmpty(_selectionAnchorId) && !existing.Contains(_selectionAnchorId)) _selectionAnchorId = null;
            if (removed > 0) SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleCardChecked(string id)
        {
            if (!_checkedIds.Remove(id)) _checkedIds.Add(id);
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Arms/relayouts the grid; kept for API parity with the list view.</summary>
        public void UpdateThumbnailsAndLayout() => Relayout();

        public void SelectAllCards()
        {
            _checkedIds.Clear();
            foreach (var entry in _items.OfType<ClipboardEntry>()) _checkedIds.Add(entry.Id);
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearCheckedCards()
        {
            if (_checkedIds.Count == 0) return;
            _checkedIds.Clear();
            _selectionAnchorId = null;
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InvertSelection()
        {
            foreach (var entry in _items.OfType<ClipboardEntry>())
            {
                if (!_checkedIds.Remove(entry.Id)) _checkedIds.Add(entry.Id);
            }
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UncheckCards(IEnumerable<string> ids)
        {
            bool changed = false;
            foreach (var id in ids ?? Array.Empty<string>()) changed |= _checkedIds.Remove(id);
            if (!changed) return;
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsEntryUnmasked(string id) => _unmaskedEntryIds.Contains(id);

        public void SetEntryUnmasked(string id, bool unmasked)
        {
            if (unmasked) _unmaskedEntryIds.Add(id);
            else _unmaskedEntryIds.Remove(id);
            Invalidate();
        }

        public void ResetInteractionState()
        {
            _pressEntryId = null;
            _pendingDrag = false;
            _leftDown = false;
            _hoveredRank = -1;
            _lastClickEntryId = null;
        }

        // ------------------------------------------------------------ layout

        private int ResolveColumns(int tileWidth)
        {
            int viewport = ClientSize.Width - (SystemInformation.VerticalScrollBarWidth + 2);
            if (viewport <= 0) return 1;
            if (_columns > 0) return Math.Clamp(_columns, 1, 10);
            return Math.Max(1, viewport / Math.Max(1, tileWidth));
        }

        private int AutoTileWidth()
        {
            int viewport = Math.Max(120, ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            return Math.Clamp(viewport / 3, 150, 260);
        }

        private void Relayout()
        {
            _layout.Clear();
            int viewport = Math.Max(120, ClientSize.Width - (VisibleScrollbar ? SystemInformation.VerticalScrollBarWidth : 0));

            int tileWidth = _columns > 0
                ? Math.Max(60, (viewport - (TileGutter * (_columns + 1))) / _columns)
                : AutoTileWidth();
            int cols = ResolveColumns(tileWidth);

            int x = TileGutter;
            int y = TileGutter;
            int col = 0;
            int rank = 0;
            int rowsInBand = 1;

            void FlushBand()
            {
                y += rowsInBand + TileGutter;
                rowsInBand = 1;
                col = 0;
                x = TileGutter;
            }

            foreach (var item in _items)
            {
                if (item is CardGroupHeader)
                {
                    if (col > 0) FlushBand();
                    _layout.Add(new TileLayout { Item = item, IsHeader = true, Bounds = new Rectangle(TileGutter, y, Math.Max(80, viewport - TileGutter * 2), HeaderHeight) });
                    y += HeaderHeight;
                    continue;
                }

                if (item is not ClipboardEntry entry) continue;

                bool isImage = entry.Format == "image" || entry.HasImage;
                int tileHeight = isImage ? ImageTileHeight : TextTileHeight;

                // Wide windows fit two rows; a tile that does not fit drops to
                // the next row so tiles never shear.
                int nextX = x;
                if (col >= cols)
                {
                    y += rowsInBand + TileGutter;
                    rowsInBand = 1;
                    col = 0;
                    nextX = TileGutter;
                }

                _layout.Add(new TileLayout
                {
                    Item = entry,
                    IsHeader = false,
                    EntryRank = rank++,
                    Bounds = new Rectangle(nextX, y, tileWidth, tileHeight)
                });

                rowsInBand = Math.Max(rowsInBand, tileHeight);
                col++;
                x = nextX + tileWidth + TileGutter;
            }

            _rows = _layout.Count;
            _contentHeight = y + rowsInBand + TileGutter;
            AutoScrollMinSize = new Size(0, _contentHeight);
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool VisibleScrollbar => _contentHeight > ClientSize.Height;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var windowBg = _visualStyle == "Native"
                ? SystemColors.Window
                : (_isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg);
            using (var bg = new SolidBrush(windowBg)) g.FillRectangle(bg, ClientRectangle);

            g.TranslateTransform(-AutoScrollPosition.X, -AutoScrollPosition.Y);
            try
            {
                foreach (var tile in _layout)
                {
                    if (tile.IsHeader)
                    {
                        if (tile.Item is CardGroupHeader header) DrawHeader(g, tile.Bounds, header, windowBg);
                        continue;
                    }
                    if (tile.Item is ClipboardEntry entry) DrawTile(g, tile, entry);
                }

                if (_isMarqueeActive && _marqueeRect.Width > 0 && _marqueeRect.Height > 0)
                {
                    using var mBrush = new SolidBrush(Color.FromArgb(40, 0, 120, 212));
                    using var mPen = new Pen(Color.FromArgb(180, 0, 120, 212), 1);
                    g.FillRectangle(mBrush, _marqueeRect);
                    g.DrawRectangle(mPen, _marqueeRect);
                }
            }
            finally
            {
                g.ResetTransform();
            }
        }

        private void DrawHeader(Graphics g, Rectangle bounds, CardGroupHeader header, Color windowBg)
        {
            bool native = string.Equals(_visualStyle, "Native", StringComparison.OrdinalIgnoreCase);
            int sepY = bounds.Y + bounds.Height / 2;
            using (var pen = new Pen(native ? SystemColors.ControlDark : (_isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240))))
            {
                g.DrawLine(pen, bounds.X + 6, sepY, bounds.Right - 6, sepY);
            }

            string text = $"{header.Title} ({header.Count})";
            var size = TextRenderer.MeasureText(text, _headerFont);
            var rect = new Rectangle(bounds.X + 10, bounds.Y + (bounds.Height - size.Height) / 2, size.Width + 16, size.Height);
            using (var bg = new SolidBrush(windowBg)) g.FillRectangle(bg, rect);
            TextRenderer.DrawText(g, text, _headerFont, rect,
                native ? SystemColors.GrayText : (_isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawTile(Graphics g, TileLayout tile, ClipboardEntry entry)
        {
            var rect = tile.Bounds;
            bool isChecked = _checkedIds.Contains(entry.Id);
            bool isFocused = _focusIndex >= 0 && _focusIndex < _items.Count && ReferenceEquals(_items[_focusIndex], entry);
            bool isHovered = !isChecked && tile.EntryRank == _hoveredRank;
            bool native = string.Equals(_visualStyle, "Native", StringComparison.OrdinalIgnoreCase);

            Color cardBg, cardBorder;
            if (isChecked || isFocused)
            {
                cardBg = native ? SystemColors.Highlight : (_isDark ? ModernPalette.DarkCardSelected : ModernPalette.LightCardSelected);
                cardBorder = native ? SystemColors.Highlight : (_isDark ? ModernPalette.DarkAccent : ModernPalette.LightAccent);
            }
            else if (isHovered)
            {
                cardBg = native ? SystemColors.Control : (_isDark ? ModernPalette.DarkCardHover : ModernPalette.LightCardHover);
                cardBorder = native ? SystemColors.ControlDark : (_isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder);
            }
            else if (!native && _visualStyle == "Acrylic")
            {
                cardBg = _isDark ? ModernPalette.AcrylicCardBgDark : ModernPalette.AcrylicCardBgLight;
                cardBorder = _isDark ? ModernPalette.AcrylicCardBorderDark : ModernPalette.AcrylicCardBorderLight;
            }
            else
            {
                cardBg = native ? SystemColors.Window : (_isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg);
                cardBorder = native ? SystemColors.ControlDark : (_isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder);
            }

            using (var path = ModernPalette.CreateRoundedPath(rect, 6))
            {
                using var brush = new SolidBrush(cardBg);
                g.FillPath(brush, path);
                using var pen = new Pen(cardBorder, (isChecked || isFocused) ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            int contentX = rect.X + TilePadding;
            int top = rect.Y + TilePadding - 2;

            if (_showCheckboxes)
            {
                DrawCheckbox(g, new Rectangle(contentX, top + 1, CheckboxSize, CheckboxSize), isChecked);
                contentX += CheckboxSize + 8;
            }

            string badge = ResolveBadge(entry);
            int badgeWidth = DrawBadge(g, badge, contentX, top);
            if (entry.IsSensitive)
            {
                badgeWidth += 4 + DrawBadge(g, "ðŸ”’", contentX + badgeWidth + 4, top, sensitive: true);
            }

            // Quick-paste badge (top-right)
            if (tile.EntryRank < 9)
            {
                var shortcutRect = new Rectangle(rect.Right - 26, top + 1, 18, 16);
                using (var sPath = ModernPalette.CreateRoundedPath(shortcutRect, 3))
                using (var sBrush = new SolidBrush(_isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240)))
                {
                    g.FillPath(sBrush, sPath);
                }
                TextRenderer.DrawText(g, (tile.EntryRank + 1).ToString(), _shortcutFont, shortcutRect,
                    _isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(51, 65, 85),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else if (entry.Pinned)
            {
                TextRenderer.DrawText(g, "ðŸ“Œ", _badgeFont, new Rectangle(rect.Right - 26, top, 18, 16), Color.DodgerBlue,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            bool isImage = entry.Format == "image" || entry.HasImage;
            int textLeft = rect.X + TilePadding;
            int textWidth = Math.Max(40, rect.Right - textLeft - TilePadding);

            if (isImage)
            {
                DrawImageThumbnail(g, rect, entry);
            }

            bool isMasked = entry.IsSensitive && !_unmaskedEntryIds.Contains(entry.Id);
            bool hasAlias = !string.IsNullOrWhiteSpace(entry.Alias);
            string primary;
            if (isMasked && !hasAlias) primary = "â€¢â€¢â€¢â€¢â€¢â€¢â€¢â€¢  (Sensitive)";
            else if (hasAlias) primary = entry.Alias!;
            else if (isImage) primary = "[Image]";
            else primary = (entry.Content ?? "").Replace("\r", " ").Replace("\n", " ");

            if (string.IsNullOrWhiteSpace(primary)) primary = "(empty)";

            var primaryColor = isChecked || isFocused
                ? (native ? SystemColors.HighlightText : (_isDark ? Color.White : Color.FromArgb(2, 132, 199)))
                : (native ? SystemColors.WindowText : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary));

            // Primary text sits at the bottom of the tile; the thumbnail area
            // above it is unused for text tiles.
            int metaHeight = 14;
            var primaryRect = new Rectangle(textLeft, rect.Bottom - TilePadding - metaHeight - 18, textWidth, 18);
            TextRenderer.DrawText(g, primary, hasAlias ? _primaryBoldFont : _primaryFont, primaryRect, primaryColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            var metaParts = new List<string>();
            if (_showSerials) metaParts.Add($"#{tile.EntryRank + 1}");
            if (!string.IsNullOrEmpty(entry.SourceApp)) metaParts.Add(entry.SourceApp);
            metaParts.Add(ModernPalette.GetRelativeTime(entry.CreatedAt));

            var metaRect = new Rectangle(textLeft, rect.Bottom - TilePadding - metaHeight, textWidth, metaHeight);
            TextRenderer.DrawText(g, string.Join(" â€¢ ", metaParts), _metaFont, metaRect,
                native ? SystemColors.GrayText : (_isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void DrawImageThumbnail(Graphics g, Rectangle tileRect, ClipboardEntry entry)
        {
            int imageTop = tileRect.Y + TilePadding + 20;
            int imageHeight = Math.Max(20, tileRect.Bottom - TilePadding - 34 - imageTop);
            int imageWidth = Math.Max(20, tileRect.Width - TilePadding * 2);
            var imageRect = new Rectangle(tileRect.X + TilePadding, imageTop, imageWidth, imageHeight);

            using (var back = new SolidBrush(_isDark ? Color.FromArgb(24, 24, 27) : Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(back, imageRect);
            }

            var bytes = SafeGetImageBytes(entry);
            var thumb = _thumbnails.GetThumbnail(entry.Id, bytes, imageWidth, imageHeight);
            if (thumb == null)
            {
                TextRenderer.DrawText(g, "ðŸ–¼ï¸", _primaryFont, imageRect,
                    _isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Letterbox: center the scaled bitmap, never stretch it.
            int x = imageRect.X + (imageRect.Width - thumb.Width) / 2;
            int y = imageRect.Y + (imageRect.Height - thumb.Height) / 2;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(thumb, new Rectangle(x, y, thumb.Width, thumb.Height));
        }

        private byte[]? SafeGetImageBytes(ClipboardEntry entry)
        {
            try { return _imageProvider(entry); }
            catch { return null; }
        }

        private static string ResolveBadge(ClipboardEntry entry)
        {
            if (entry.Format == "image" || entry.HasImage) return "IMAGE";
            if (entry.Format == "file") return "FILE";
            if (entry.Format == "color") return "COLOR";
            if (entry.Format == "html") return "HTML";
            if (entry.Format == "rtf") return "RTF";
            if (!string.IsNullOrEmpty(entry.Content))
            {
                var trimmed = entry.Content.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "LINK";
            }
            return "TEXT";
        }

        private int DrawBadge(Graphics g, string text, int x, int y, bool sensitive = false)
        {
            var size = TextRenderer.MeasureText(text, _badgeFont);
            var rect = new Rectangle(x, y, size.Width + 8, 16);
            var bg = sensitive
                ? (_isDark ? Color.FromArgb(127, 29, 29) : Color.FromArgb(254, 226, 226))
                : ModernPalette.GetBadgeBg(text, _isDark);
            var fg = sensitive
                ? (_isDark ? Color.FromArgb(252, 165, 165) : Color.FromArgb(185, 28, 28))
                : ModernPalette.GetBadgeText(text, _isDark);
            using (var path = ModernPalette.CreateRoundedPath(rect, 4))
            using (var b = new SolidBrush(bg))
            {
                g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, text, _badgeFont, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            return rect.Width;
        }

        private void DrawCheckbox(Graphics g, Rectangle rect, bool isChecked)
        {
            using var path = ModernPalette.CreateRoundedPath(rect, 3);
            Color border = isChecked ? Color.FromArgb(0, 120, 212) : (_isDark ? Color.FromArgb(82, 82, 91) : Color.FromArgb(212, 212, 216));
            Color fill = isChecked ? Color.FromArgb(0, 120, 212) : (_isDark ? Color.FromArgb(39, 39, 42) : Color.White);
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var p = new Pen(border, 1.2f)) g.DrawPath(p, path);
            if (!isChecked) return;
            using var checkPen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(checkPen, new[]
            {
                new Point(rect.X + 3, rect.Y + 8),
                new Point(rect.X + 6, rect.Y + 12),
                new Point(rect.X + 13, rect.Y + 4)
            });
        }

        // ------------------------------------------------------------ hit testing

        private int IndexFromPoint(Point location)
        {
            var pt = new Point(location.X - AutoScrollPosition.X, location.Y - AutoScrollPosition.Y);
            for (int i = 0; i < _layout.Count; i++)
            {
                if (_layout[i].Bounds.Contains(pt)) return i;
            }
            return -1;
        }

        private Rectangle CheckboxRect(int index)
        {
            var bounds = _layout[index].Bounds;
            return new Rectangle(bounds.X + TilePadding, bounds.Y + TilePadding + 1, CheckboxSize, CheckboxSize);
        }

        private int IndexOfEntry(string id)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] is ClipboardEntry e && e.Id == id) return i;
            }
            return -1;
        }

        /// <summary>Entry under a screen point (used by the context menu).</summary>
        public ClipboardEntry? EntryFromScreenPoint(Point screenPoint)
        {
            var client = PointToClient(screenPoint);
            int index = IndexFromPoint(client);
            if (index < 0) return null;
            return _layout[index].Item as ClipboardEntry;
        }

        /// <summary>Selects and focuses an entry by id.</summary>
        public void FocusEntry(string id)
        {
            int index = IndexOfEntry(id);
            if (index < 0) return;
            _focusIndex = index;
            _selectionAnchorId = id;
            Invalidate();
        }

        public int FirstEntryIndex() => NextEntryIndex(0, 1);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _mouseDownPoint = e.Location;
            _isMarqueeActive = false;
            _pressOnCheckbox = false;
            _pressEntryId = null;
            _pendingDrag = false;
            _isDragging = false;
            _suppressClick = false;
            _leftDown = false;
            _movedDuringPress = false;
            _pendingActivateEntry = null;

            if (e.Button != MouseButtons.Left)
            {
                _lastClickEntryId = null;
                base.OnMouseDown(e);
                return;
            }

            _leftDown = true;
            Focus();

            int index = IndexFromPoint(e.Location);
            if (index < 0)
            {
                _lastClickEntryId = null;
                _pendingDrag = true;
                base.OnMouseDown(e);
                return;
            }

            if (_layout[index].Item is not ClipboardEntry entry)
            {
                _lastClickEntryId = null;
                return;
            }

            int itemIndex = IndexOfEntry(entry.Id);

            if (_showCheckboxes && CheckboxRect(index).Contains(e.Location))
            {
                _lastClickEntryId = null;
                ToggleCardChecked(entry.Id);
                _pressOnCheckbox = true;
                _suppressClick = true;
                return;
            }

            long now = Environment.TickCount64;
            bool doubleClick =
                _lastClickEntryId == entry.Id &&
                now - _lastClickTick <= SystemInformation.DoubleClickTime &&
                Math.Abs(e.X - _lastClickPoint.X) <= SystemInformation.DoubleClickSize.Width &&
                Math.Abs(e.Y - _lastClickPoint.Y) <= SystemInformation.DoubleClickSize.Height;

            _lastClickTick = now;
            _lastClickPoint = e.Location;
            _lastClickEntryId = entry.Id;

            if (doubleClick)
            {
                _pendingDrag = false;
                _suppressClick = true;
                _focusIndex = itemIndex;
                _selectionAnchorId = entry.Id;
                _pendingActivateEntry = entry; // activates on mouse-up
                Invalidate();
                return;
            }

            if ((ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                _lastClickEntryId = null;
                int anchor = string.IsNullOrEmpty(_selectionAnchorId) ? itemIndex : IndexOfEntry(_selectionAnchorId);
                if (anchor < 0) anchor = itemIndex;
                SelectRange(anchor, itemIndex, (ModifierKeys & Keys.Control) == Keys.Control);
                _focusIndex = itemIndex;
                _suppressClick = true;
                Invalidate();
                return;
            }

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                _lastClickEntryId = null;
                ToggleCardChecked(entry.Id);
                _focusIndex = itemIndex;
                _selectionAnchorId = entry.Id;
                _suppressClick = true;
                return;
            }

            _focusIndex = itemIndex;
            _selectionAnchorId = entry.Id;
            _pressEntryId = entry.Id;
            _pendingDrag = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int hovered = IndexFromPoint(e.Location);
            int hoveredRank = hovered >= 0 && _layout[hovered].Item is ClipboardEntry ? _layout[hovered].EntryRank : -1;
            if (hoveredRank != _hoveredRank)
            {
                _hoveredRank = hoveredRank;
                Invalidate();
            }

            if (!_leftDown || e.Button != MouseButtons.Left) return;

            bool beyond =
                Math.Abs(e.X - _mouseDownPoint.X) >= DragThreshold ||
                Math.Abs(e.Y - _mouseDownPoint.Y) >= DragThreshold;
            if (!beyond) return;

            _movedDuringPress = true;

            if ((ModifierKeys & (Keys.Control | Keys.Shift)) != 0) return;

            if (_pendingDrag && !_pressOnCheckbox && _pressEntryId != null)
            {
                if (!DragEnabled || !DragArmed) return;

                _pendingDrag = false;
                _leftDown = false;
                _isDragging = true;
                _suppressClick = true;
                var entry = GetEntryById(_pressEntryId);
                if (entry != null)
                {
                    try { StartCardDragRequested?.Invoke(this, entry); }
                    catch { }
                    finally
                    {
                        _isDragging = false;
                        DragSessionFinished?.Invoke(this, EventArgs.Empty);
                    }
                }
                return;
            }

            if (_pendingDrag && !_pressOnCheckbox)
            {
                if (!_isMarqueeActive)
                {
                    _isMarqueeActive = true;
                    _marqueeBaseline.Clear();
                    if ((ModifierKeys & Keys.Control) == Keys.Control)
                    {
                        foreach (var id in _checkedIds) _marqueeBaseline.Add(id);
                    }
                    else
                    {
                        _checkedIds.Clear();
                    }
                }
                UpdateMarquee(e.Location);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _leftDown = false;
            _pendingDrag = false;

            if (_isMarqueeActive)
            {
                _isMarqueeActive = false;
                _marqueeBaseline.Clear();
                _marqueeRect = Rectangle.Empty;
                Invalidate();
                SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (e.Button == MouseButtons.Left && !_suppressClick && !_movedDuringPress && _pressEntryId != null)
            {
                var entry = GetEntryById(_pressEntryId);
                if (entry != null) CardClicked?.Invoke(this, entry);
            }

            if (_pendingActivateEntry != null)
            {
                var activated = _pendingActivateEntry;
                _pendingActivateEntry = null;
                CardActivated?.Invoke(this, activated);
            }

            _pressEntryId = null;
            _suppressClick = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredRank == -1) return;
            _hoveredRank = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Let ScrollableControl handle it; relayout is not needed.
            base.OnMouseWheel(e);
        }

        private void UpdateMarquee(Point current)
        {
            int scrollY = AutoScrollPosition.Y;
            var start = new Point(_mouseDownPoint.X - AutoScrollPosition.X, _mouseDownPoint.Y - AutoScrollPosition.Y);
            var end = new Point(current.X - AutoScrollPosition.X, current.Y - AutoScrollPosition.Y);

            _marqueeRect = Rectangle.FromLTRB(
                Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

            var refreshed = new HashSet<string>(_marqueeBaseline);
            foreach (var tile in _layout)
            {
                if (tile.Item is ClipboardEntry entry && tile.Bounds.IntersectsWith(_marqueeRect)) refreshed.Add(entry.Id);
            }

            bool changed = !refreshed.SetEquals(_checkedIds);
            if (changed)
            {
                _checkedIds.Clear();
                foreach (var id in refreshed) _checkedIds.Add(id);
            }

            Invalidate();
            if (changed) SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
            _ = scrollY;
        }

        private void SelectRange(int from, int to, bool additive)
        {
            int start = Math.Max(0, Math.Min(from, to));
            int end = Math.Min(_items.Count - 1, Math.Max(from, to));
            if (!additive) _checkedIds.Clear();
            for (int i = start; i <= end; i++)
            {
                if (_items[i] is ClipboardEntry entry) _checkedIds.Add(entry.Id);
            }
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------ keyboard

        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllCards();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Space && _focusIndex >= 0 && _focusIndex < _items.Count && _items[_focusIndex] is ClipboardEntry focused)
            {
                ToggleCardChecked(focused.Id);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End)
            {
                int cols = Math.Max(1, ResolveColumns(_columns > 0 ? 0 : AutoTileWidth()));
                int step = e.KeyCode switch
                {
                    Keys.Left => -1,
                    Keys.Right => 1,
                    Keys.Up => -cols,
                    Keys.Down => cols,
                    Keys.Home => int.MinValue,
                    _ => int.MaxValue
                };

                int start = _focusIndex < 0 ? NextEntryIndex(0, 1) : _focusIndex;
                int target = e.KeyCode == Keys.Home ? NextEntryIndex(0, 1) : e.KeyCode == Keys.End ? NextEntryIndex(_items.Count - 1, -1) : NextEntryIndex(start + step, Math.Sign(step) is 0 ? 1 : Math.Sign(step));
                if (target >= 0)
                {
                    _focusIndex = target;
                    if (e.Shift)
                    {
                        SelectRange(string.IsNullOrEmpty(_selectionAnchorId) ? start : IndexOfEntry(_selectionAnchorId!), target, e.Control);
                    }
                    else if (e.Control && _items[target] is ClipboardEntry targetEntry)
                    {
                        _selectionAnchorId = targetEntry.Id;
                    }
                    else
                    {
                        _selectionAnchorId = (_items[target] as ClipboardEntry)?.Id;
                    }
                    ScrollEntryIntoView(target);
                    Invalidate();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private int NextEntryIndex(int from, int direction)
        {
            int i = Math.Clamp(from, 0, Math.Max(0, _items.Count - 1));
            while (i >= 0 && i < _items.Count)
            {
                if (_items[i] is ClipboardEntry) return i;
                i += direction;
            }
            return -1;
        }

        private void ScrollEntryIntoView(int itemIndex)
        {
            int layoutIndex = -1;
            for (int i = 0; i < _layout.Count; i++)
            {
                if (ReferenceEquals(_layout[i].Item, _items[itemIndex])) { layoutIndex = i; break; }
            }
            if (layoutIndex < 0) return;

            var tile = _layout[layoutIndex].Bounds;
            int viewTop = -AutoScrollPosition.Y;
            int viewHeight = ClientSize.Height;

            int newTop = viewTop;
            if (tile.Top < viewTop) newTop = tile.Top - TileGutter;
            else if (tile.Bottom > viewTop + viewHeight) newTop = tile.Bottom - viewHeight + TileGutter;

            if (newTop == viewTop) return;
            AutoScrollPosition = new Point(0, Math.Max(0, newTop));
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headerFont.Dispose();
                _badgeFont.Dispose();
                _primaryFont.Dispose();
                _primaryBoldFont.Dispose();
                _metaFont.Dispose();
                _shortcutFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

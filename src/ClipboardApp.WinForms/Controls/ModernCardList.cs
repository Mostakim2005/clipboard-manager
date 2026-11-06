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
    public class ModernCardList : ListBox
    {
        private const int CheckboxSize = 16;
        private const int CheckboxLeft = 10;
        private const int CardGutter = 6;
        private const int DragThreshold = 8;

        private bool _isDark = true;
        private int _hoveredIndex = -1;
        private bool _showCheckboxes = true;
        private bool _showSerials = true;
        private string _cardDensity = "Comfortable";
        private string _visualStyle = "Fluent";

        private readonly HashSet<string> _checkedIds = new();
        private readonly HashSet<string> _unmaskedEntryIds = new();
        private Point _mouseDownPoint;
        private bool _leftDown;
        private bool _pendingDrag;
        private bool _pressOnCheckbox;
        private string? _pressEntryId;
        private int _pressIndex = -1;
        private bool _isMarqueeActive;
        private Rectangle _marqueeRect;
        private readonly HashSet<string> _marqueeBaseline = new();
        private int _lastAnchorIndex = -1;
        private bool _suppressClick;
        private bool _isDragging;
        private bool _movedDuringPress;
        private ClipboardEntry? _pendingActivateEntry;
        private long _lastClickTick;
        private string? _selectionAnchorId;
        private Point _lastClickPoint;
        private int _lastClickIndex = -1;

        private readonly Font _headerFont = new("Segoe UI", 8.5f, FontStyle.Bold);
        private readonly Font _badgeFont = new("Segoe UI", 7.5f, FontStyle.Bold);
        private readonly Font _tagFont = new("Segoe UI", 7.5f, FontStyle.Regular);
        private readonly Font _primaryFont = new("Segoe UI", 9.5f, FontStyle.Regular);
        private readonly Font _primaryBoldFont = new("Segoe UI", 9.5f, FontStyle.Bold);
        private readonly Font _metaFont = new("Segoe UI", 8f, FontStyle.Regular);
        private readonly Font _shortcutFont = new("Segoe UI", 8f, FontStyle.Bold);
        private readonly Font _pinFont = new("Segoe UI", 9f);

        public event EventHandler? SelectionOrCheckedStateChanged;
        public event EventHandler<ClipboardEntry>? StartCardDragRequested;
        public event EventHandler<ClipboardEntry>? CardActivated;
        public event EventHandler<ClipboardEntry>? CardClicked;
        public event EventHandler? DragSessionFinished;

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

        public bool ShowCheckboxes
        {
            get => _showCheckboxes;
            set
            {
                if (_showCheckboxes == value) return;
                _showCheckboxes = value;
                Invalidate();
            }
        }

        public bool ShowSerials
        {
            get => _showSerials;
            set
            {
                if (_showSerials == value) return;
                _showSerials = value;
                Invalidate();
            }
        }

        /// <summary>Master switch for OLE drag-out.</summary>
        public bool DragEnabled { get; set; } = true;

        /// <summary>
        /// When true, a drag can only start after the user explicitly arms it.
        /// MainForm clears this on Deactivate / minimize / hide.
        /// </summary>
        public bool DragArmed { get; set; }

        public bool IsDragging => _isDragging;

        /// <summary>
        /// Supplies the (already DPAPI-decrypted) PNG bytes for an image entry so
        /// the card can paint a real thumbnail instead of only its file name.
        /// May be null before the repository is wired up.
        /// </summary>
        public Func<ClipboardEntry, byte[]?>? ImageBytesProvider { get; set; }

        /// <summary>Shared, LRU-bounded thumbnail cache owned by MainForm.</summary>
        public ThumbnailCache? Thumbnails { get; set; }

        /// <summary>
        /// Called after the visible list is rebuilt so stale click anchors and
        /// hover indexes cannot target a different row.
        /// </summary>
        public void ResetInteractionState()
        {
            _lastAnchorIndex = -1;
            _lastClickIndex = -1;
            _pressEntryId = null;
            _pressIndex = -1;
            _pendingDrag = false;
            _leftDown = false;
            _hoveredIndex = -1;
        }

        public string CardDensity
        {
            get => _cardDensity;
            set
            {
                _cardDensity = value;
                ItemHeight = _cardDensity == "Compact" ? 54 : 72;
                Invalidate();
            }
        }

        public string VisualStyle
        {
            get => _visualStyle;
            set
            {
                _visualStyle = value;
                Invalidate();
            }
        }

        public ModernCardList()
        {
            DrawMode = DrawMode.OwnerDrawVariable;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw, true);
            BorderStyle = BorderStyle.None;
            ItemHeight = 72;
            IntegralHeight = false;
            SelectionMode = SelectionMode.One;
            TabStop = true;
            BackColor = ModernPalette.DarkWindowBg;
            Cursor = Cursors.Default;
        }

        // ------------------------------------------------------------- selection API

        public HashSet<string> GetCheckedIds() => new(_checkedIds);

        public string? GetFocusedEntryId() => (SelectedItem as ClipboardEntry)?.Id;

        public string? GetSelectionAnchorId() => _selectionAnchorId;

        public int GetHiddenCheckedCount()
        {
            if (_checkedIds.Count == 0) return 0;
            int visible = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry entry && _checkedIds.Contains(entry.Id)) visible++;
            }
            return Math.Max(0, _checkedIds.Count - visible);
        }

        public int GetVisibleCheckedCount()
        {
            if (_checkedIds.Count == 0) return 0;
            int visible = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry entry && _checkedIds.Contains(entry.Id)) visible++;
            }
            return visible;
        }

        public int GetVisibleEntryCount()
        {
            int count = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry) count++;
            }
            return count;
        }

        /// <summary>
        /// Removes several IDs from the checked set in one pass. Used when a
        /// mixed text+image operation can only carry text.
        /// </summary>
        public void UncheckCards(IEnumerable<string> ids)
        {
            if (ids == null) return;
            bool changed = false;
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id)) changed |= _checkedIds.Remove(id);
            }
            if (!changed) return;

            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Restores focus and the range anchor by ID after a snapshot rebuild.
        /// Selection itself lives in the repository-independent checked set and
        /// is intentionally not cleared by filtering or refresh.
        /// </summary>
        public void RestoreSelectionState(string? focusedId, string? anchorId)
        {
            int focusIndex = string.IsNullOrEmpty(focusedId) ? -1 : IndexOfEntry(focusedId);
            if (focusIndex >= 0) SelectedIndex = focusIndex;

            int anchorIndex = string.IsNullOrEmpty(anchorId) ? -1 : IndexOfEntry(anchorId);
            _lastAnchorIndex = anchorIndex;
            _selectionAnchorId = anchorIndex >= 0 ? anchorId : null;
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

        public List<string> GetCheckedOrSelectedIds()
        {
            if (_checkedIds.Count > 0) return _checkedIds.ToList();
            if (SelectedItem is ClipboardEntry selected) return new List<string> { selected.Id };
            return new List<string>();
        }

        public ClipboardEntry? GetEntryById(string id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry entry && entry.Id == id) return entry;
            }
            return null;
        }

        public void SelectAllCards()
        {
            _checkedIds.Clear();
            string? firstId = null;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry e)
                {
                    _checkedIds.Add(e.Id);
                    firstId ??= e.Id;
                }
            }
            _selectionAnchorId ??= firstId;
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearCheckedCards()
        {
            if (_checkedIds.Count == 0) return;
            _checkedIds.Clear();
            _selectionAnchorId = null;
            _lastAnchorIndex = -1;
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleCardChecked(string id)
        {
            if (_checkedIds.Contains(id)) _checkedIds.Remove(id);
            else _checkedIds.Add(id);

            int index = IndexOfEntry(id);
            if (index >= 0) InvalidateRow(index);
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InvertSelection()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is not ClipboardEntry e) continue;
                if (_checkedIds.Contains(e.Id)) _checkedIds.Remove(e.Id);
                else _checkedIds.Add(e.Id);
            }
            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsEntryUnmasked(string id) => _unmaskedEntryIds.Contains(id);

        public void SetEntryUnmasked(string id, bool unmasked)
        {
            if (unmasked) _unmaskedEntryIds.Add(id);
            else _unmaskedEntryIds.Remove(id);
            int index = IndexOfEntry(id);
            if (index >= 0) InvalidateRow(index);
        }

        public void ToggleEntryMask(string id)
        {
            SetEntryUnmasked(id, !_unmaskedEntryIds.Contains(id));
        }

        /// <summary>
        /// Drops checked IDs that no longer exist in the database (deleted), but
        /// keeps selections that are merely filtered out. This is what makes
        /// selection survive search/filter/group changes.
        /// </summary>
        public void PruneCheckedToExisting(IEnumerable<string> existingIds)
        {
            var existing = new HashSet<string>(existingIds ?? Array.Empty<string>());
            int removed = _checkedIds.RemoveWhere(id => !existing.Contains(id));
            if (removed > 0)
            {
                SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (!string.IsNullOrEmpty(_selectionAnchorId) && !existing.Contains(_selectionAnchorId))
            {
                _selectionAnchorId = null;
                _lastAnchorIndex = -1;
            }
        }

        private int IndexOfEntry(string id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry e && e.Id == id) return i;
            }
            return -1;
        }

        private int NearestEntryIndex(int index)
        {
            for (int i = Math.Max(0, index); i < Items.Count; i++)
            {
                if (Items[i] is ClipboardEntry) return i;
            }
            for (int i = Math.Min(index - 1, Items.Count - 1); i >= 0; i--)
            {
                if (Items[i] is ClipboardEntry) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------- input / gestures

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _mouseDownPoint = e.Location;
            _isMarqueeActive = false;
            _pressOnCheckbox = false;
            _pressEntryId = null;
            _pressIndex = -1;
            _pendingDrag = false;
            _isDragging = false;
            _suppressClick = false;
            _leftDown = false;
            _movedDuringPress = false;
            _pendingActivateEntry = null;

            if (e.Button != MouseButtons.Left)
            {
                _lastClickIndex = -1;
                base.OnMouseDown(e);
                return;
            }

            _leftDown = true;
            Focus();

            int idx = IndexFromPoint(e.Location);
            if (idx < 0 || idx >= Items.Count)
            {
                // Blank/gutter area: candidate for a marquee selection.
                _lastClickIndex = -1;
                _pendingDrag = true;
                base.OnMouseDown(e);
                return;
            }

            if (Items[idx] is CardGroupHeader header)
            {
                if (_showCheckboxes && header.Count > 0)
                {
                    var itemBounds = GetItemRectangle(idx);
                    string headerText = $"{header.Title} ({header.Count})";
                    var textSize = TextRenderer.MeasureText(headerText, _headerFont);
                    var textRect = new Rectangle(itemBounds.X + 16, itemBounds.Y + (itemBounds.Height - textSize.Height) / 2, textSize.Width + 8, textSize.Height);
                    var cbRect = GetGroupHeaderCheckboxRect(itemBounds, textRect);
                    var hitArea = Rectangle.Inflate(cbRect, 4, 4);

                    if (hitArea.Contains(e.Location))
                    {
                        _lastClickIndex = -1;
                        ToggleGroupChecked(header);
                        _pressOnCheckbox = true;
                        _suppressClick = true;
                        return;
                    }
                }

                // Headers are visual separators, not selectable rows. Move to the
                // nearest real entry so Enter/Delete keep working.
                _lastClickIndex = -1;
                int near = NearestEntryIndex(idx);
                if (near >= 0) SelectedIndex = near;
                return;
            }

            if (Items[idx] is not ClipboardEntry entry)
            {
                _lastClickIndex = -1;
                base.OnMouseDown(e);
                return;
            }

            if (_showCheckboxes && GetCheckboxRect(idx).Contains(e.Location))
            {
                _lastClickIndex = -1;
                ToggleCardChecked(entry.Id);
                _pressOnCheckbox = true;
                _suppressClick = true;
                return;
            }

            // Double-click is detected by the control itself rather than relying on
            // ListBox's DoubleClick event, which is unreliable for owner-drawn lists.
            long now = Environment.TickCount64;
            bool manualDoubleClick =
                _lastClickIndex == idx &&
                now - _lastClickTick <= SystemInformation.DoubleClickTime &&
                Math.Abs(e.X - _lastClickPoint.X) <= SystemInformation.DoubleClickSize.Width &&
                Math.Abs(e.Y - _lastClickPoint.Y) <= SystemInformation.DoubleClickSize.Height;

            _lastClickTick = now;
            _lastClickPoint = e.Location;
            _lastClickIndex = idx;

            if (manualDoubleClick)
            {
                _pendingDrag = false;
                _suppressClick = true;
                SelectedIndex = idx;
                _lastAnchorIndex = idx;
                _selectionAnchorId = entry.Id;
                // Activate on mouse-up so the window is not hidden in the middle
                // of the double-click gesture. This also prevents the second
                // click from reaching the target window after Hide().
                _pendingActivateEntry = entry;
                return;
            }

            if ((ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                _lastClickIndex = -1;
                if (_lastAnchorIndex < 0 || _lastAnchorIndex >= Items.Count)
                {
                    _lastAnchorIndex = idx;
                    _selectionAnchorId = entry.Id;
                }
                bool additive = (ModifierKeys & Keys.Control) == Keys.Control;
                SelectRange(_lastAnchorIndex, idx, additive);
                SelectedIndex = idx;
                _suppressClick = true;
                return;
            }

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                _lastClickIndex = -1;
                ToggleCardChecked(entry.Id);
                SelectedIndex = idx;
                _lastAnchorIndex = idx;
                _selectionAnchorId = entry.Id;
                _suppressClick = true;
                return;
            }

            SelectedIndex = idx;
            _lastAnchorIndex = idx;
            _selectionAnchorId = entry.Id;
            _pressEntryId = entry.Id;
            _pressIndex = idx;
            _pendingDrag = true;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            UpdateHover(e.Location);

            if (!_leftDown || e.Button != MouseButtons.Left) return;

            if (_pendingDrag && !_pressOnCheckbox)
            {
                bool beyondThreshold =
                    Math.Abs(e.X - _mouseDownPoint.X) >= DragThreshold ||
                    Math.Abs(e.Y - _mouseDownPoint.Y) >= DragThreshold;

                if (!beyondThreshold) return;

                _movedDuringPress = true;

                // Ctrl/Shift gestures are selection gestures, never drags.
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) != 0) return;

                if (_pressEntryId != null)
                {
                    // Dragging a card is only possible while explicitly armed.
                    if (!DragEnabled || !DragArmed) return;

                    _pendingDrag = false;
                    _leftDown = false;
                    _isDragging = true;
                    _suppressClick = true;

                    var entry = GetEntryById(_pressEntryId) ?? (_pressIndex >= 0 && _pressIndex < Items.Count ? Items[_pressIndex] as ClipboardEntry : null);
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

                // Blank-area drag: marquee selection.
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
            else if (_isMarqueeActive)
            {
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
                var invalid = Rectangle.Inflate(_marqueeRect, 2, 2);
                _marqueeRect = Rectangle.Empty;
                Invalidate(invalid);
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

            _suppressClick = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isDragging || _isMarqueeActive) return;
            if (_hoveredIndex != -1)
            {
                int old = _hoveredIndex;
                _hoveredIndex = -1;
                InvalidateRow(old);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllCards();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Space && SelectedItem is ClipboardEntry selected)
            {
                ToggleCardChecked(selected.Id);
                _selectionAnchorId = selected.Id;
                _lastAnchorIndex = SelectedIndex;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode is Keys.Down or Keys.Up or Keys.Home or Keys.End or Keys.PageDown or Keys.PageUp)
            {
                int target = ResolveKeyboardTarget(e.KeyCode);
                if (target >= 0)
                {
                    ApplyKeyboardSelection(target, e.Control, e.Shift);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private int ResolveKeyboardTarget(Keys key)
        {
            if (Items.Count == 0) return -1;

            if (key == Keys.Home) return NearestEntryIndex(0);
            if (key == Keys.End) return NearestEntryIndex(Items.Count - 1);

            int current = SelectedIndex;
            if (current < 0 || current >= Items.Count) return NearestEntryIndex(0);

            if (key is Keys.PageDown or Keys.PageUp)
            {
                int page = Math.Max(1, (ClientSize.Height / Math.Max(1, ItemHeight)) - 1);
                int direction = key == Keys.PageDown ? 1 : -1;
                int candidate = Math.Clamp(current + (page * direction), 0, Items.Count - 1);
                int found = direction > 0
                    ? NearestEntryIndex(candidate)
                    : NearestEntryIndexBackwards(candidate);
                return found;
            }

            int step = key == Keys.Down ? 1 : -1;
            int index = current + step;
            while (index >= 0 && index < Items.Count)
            {
                if (Items[index] is ClipboardEntry) return index;
                index += step;
            }
            return -1;
        }

        private int NearestEntryIndexBackwards(int index)
        {
            for (int i = Math.Min(index, Items.Count - 1); i >= 0; i--)
            {
                if (Items[i] is ClipboardEntry) return i;
            }
            return -1;
        }

        private void ApplyKeyboardSelection(int targetIndex, bool control, bool shift)
        {
            if (targetIndex < 0 || targetIndex >= Items.Count) return;
            if (Items[targetIndex] is not ClipboardEntry target) return;

            int focusBefore = SelectedIndex;
            bool selectionChanged = false;

            if (shift)
            {
                int anchorIndex = IndexOfEntry(_selectionAnchorId ?? "");
                if (anchorIndex < 0)
                {
                    anchorIndex = focusBefore >= 0 ? focusBefore : targetIndex;
                    _selectionAnchorId = (Items[anchorIndex] as ClipboardEntry)?.Id;
                }

                if (!control && _checkedIds.Count > 0)
                {
                    _checkedIds.Clear();
                    selectionChanged = true;
                }

                int start = Math.Min(anchorIndex, targetIndex);
                int end = Math.Max(anchorIndex, targetIndex);
                var before = new HashSet<string>(_checkedIds);
                for (int i = start; i <= end; i++)
                {
                    if (Items[i] is ClipboardEntry rangeEntry) _checkedIds.Add(rangeEntry.Id);
                }
                selectionChanged |= !before.SetEquals(_checkedIds);
            }
            else if (control)
            {
                if (_checkedIds.Count == 0 && focusBefore >= 0 && focusBefore < Items.Count &&
                    Items[focusBefore] is ClipboardEntry current)
                {
                    _checkedIds.Add(current.Id);
                    selectionChanged = true;
                }
                selectionChanged |= _checkedIds.Add(target.Id);
                _selectionAnchorId = target.Id;
                _lastAnchorIndex = targetIndex;
            }
            else
            {
                _selectionAnchorId = target.Id;
                _lastAnchorIndex = targetIndex;
            }

            SelectedIndex = targetIndex;
            EnsureEntryVisible(targetIndex);

            if (selectionChanged)
            {
                Invalidate();
                SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                InvalidateRow(targetIndex);
            }
        }

        private void EnsureEntryVisible(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            int top = TopIndex;
            if (index < top)
            {
                TopIndex = index;
            }
            else
            {
                int visible = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
                if (index >= top + visible)
                {
                    TopIndex = Math.Max(0, index - visible + 1);
                }
            }
        }

        private void SelectRange(int from, int to, bool additive)
        {
            int start = Math.Max(0, Math.Min(from, to));
            int end = Math.Min(Items.Count - 1, Math.Max(from, to));
            var before = new HashSet<string>(_checkedIds);

            if (!additive && _checkedIds.Count > 0)
            {
                _checkedIds.Clear();
            }

            for (int i = start; i <= end; i++)
            {
                if (Items[i] is ClipboardEntry rangeEntry) _checkedIds.Add(rangeEntry.Id);
            }

            if (!before.SetEquals(_checkedIds) || !additive)
            {
                Invalidate();
                SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateMarquee(Point current)
        {
            var old = _marqueeRect;
            _marqueeRect = Rectangle.FromLTRB(
                Math.Min(_mouseDownPoint.X, current.X),
                Math.Min(_mouseDownPoint.Y, current.Y),
                Math.Max(_mouseDownPoint.X, current.X),
                Math.Max(_mouseDownPoint.Y, current.Y));

            var refreshed = new HashSet<string>(_marqueeBaseline);
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is not ClipboardEntry entry) continue;
                if (GetItemRectangle(i).IntersectsWith(_marqueeRect)) refreshed.Add(entry.Id);
            }

            bool changed = !refreshed.SetEquals(_checkedIds);
            if (changed)
            {
                _checkedIds.Clear();
                foreach (var id in refreshed) _checkedIds.Add(id);
            }

            var invalid = Rectangle.Union(old, _marqueeRect);
            if (invalid.Width > 0 && invalid.Height > 0)
            {
                Invalidate(Rectangle.Inflate(invalid, 2, 2));
            }

            if (changed) SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateHover(Point location)
        {
            int idx = IndexFromPoint(location);
            if (idx == _hoveredIndex) return;

            int old = _hoveredIndex;
            _hoveredIndex = idx;
            InvalidateRow(old);
            InvalidateRow(idx);
        }

        private void InvalidateRow(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            Invalidate(Rectangle.Inflate(GetItemRectangle(index), 1, 1));
        }

        private Rectangle GetCheckboxRect(int index)
        {
            var bounds = GetItemRectangle(index);
            var card = new Rectangle(bounds.X + CardGutter, bounds.Y + 3, bounds.Width - (CardGutter * 2), bounds.Height - 6);
            return new Rectangle(card.X + CheckboxLeft, card.Y + (card.Height - CheckboxSize) / 2, CheckboxSize, CheckboxSize);
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            if (e.Index >= 0 && e.Index < Items.Count && Items[e.Index] is CardGroupHeader)
            {
                e.ItemHeight = 28;
                return;
            }
            e.ItemHeight = _cardDensity == "Compact" ? 54 : 72;
            base.OnMeasureItem(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var windowBg = _visualStyle == "Native"
                ? SystemColors.Window
                : (_isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg);
            using (var windowBrush = new SolidBrush(windowBg))
            {
                g.FillRectangle(windowBrush, e.Bounds);
            }

            if (Items[e.Index] is CardGroupHeader header)
            {
                DrawGroupHeader(g, e.Bounds, header, windowBg);
                return;
            }

            if (Items[e.Index] is not ClipboardEntry entry) return;

            bool isSelected = (e.State & DrawItemState.Selected) != 0;
            bool isChecked = _checkedIds.Contains(entry.Id);
            bool isHovered = e.Index == _hoveredIndex && !isSelected;

            var cardRect = new Rectangle(
                e.Bounds.X + CardGutter,
                e.Bounds.Y + 3,
                e.Bounds.Width - (CardGutter * 2),
                e.Bounds.Height - 6);

            Color cardBg;
            Color cardBorder;
            bool native = string.Equals(_visualStyle, "Native", StringComparison.OrdinalIgnoreCase);

            if (isSelected || isChecked)
            {
                cardBg = native
                    ? SystemColors.Highlight
                    : (_isDark ? ModernPalette.DarkCardSelected : ModernPalette.LightCardSelected);
                cardBorder = native
                    ? SystemColors.Highlight
                    : (_isDark ? ModernPalette.DarkAccent : ModernPalette.LightAccent);
            }
            else if (isHovered)
            {
                cardBg = native
                    ? SystemColors.Control
                    : (_isDark ? ModernPalette.DarkCardHover : ModernPalette.LightCardHover);
                cardBorder = native
                    ? SystemColors.ControlDark
                    : (_isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder);
            }
            else if (!native && _visualStyle == "Acrylic")
            {
                cardBg = _isDark ? ModernPalette.AcrylicCardBgDark : ModernPalette.AcrylicCardBgLight;
                cardBorder = _isDark ? ModernPalette.AcrylicCardBorderDark : ModernPalette.AcrylicCardBorderLight;
            }
            else
            {
                cardBg = native
                    ? SystemColors.Window
                    : (_isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg);
                cardBorder = native
                    ? SystemColors.ControlDark
                    : (_isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder);
            }

            using (var path = ModernPalette.CreateRoundedPath(cardRect, 6))
            {
                using var brush = new SolidBrush(cardBg);
                g.FillPath(brush, path);
                using var pen = new Pen(cardBorder, (isSelected || isChecked) ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            int contentX = cardRect.X + CheckboxLeft;
            if (_showCheckboxes)
            {
                DrawModernCheckbox(g, GetCheckboxRect(e.Index), isChecked);
                contentX += CheckboxSize + 10;
            }

            int topRowY = cardRect.Y + (_cardDensity == "Compact" ? 6 : 8);

            string badgeType = entry.Format.ToUpperInvariant();
            if (entry.Format == "text" && !string.IsNullOrEmpty(entry.Content))
            {
                var trimmed = entry.Content.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    badgeType = "LINK";
                else if (trimmed.StartsWith("#") && (trimmed.Length == 7 || trimmed.Length == 9))
                    badgeType = "COLOR";
            }

            int badgeWidth = DrawBadgePill(g, badgeType, contentX, topRowY);
            int currentBadgeX = contentX + badgeWidth + 8;

            if (entry.IsSensitive)
            {
                currentBadgeX += DrawBadgePill(g, "🔒 SENSITIVE", currentBadgeX, topRowY, isSensitive: true) + 8;
            }

            if (entry.Tags != null && entry.Tags.Count > 0)
            {
                foreach (var tag in entry.Tags.Take(2))
                {
                    currentBadgeX += DrawTagPill(g, $"🏷️ {tag}", currentBadgeX, topRowY) + 6;
                }
            }

            int entryRank = 0;
            for (int i = 0; i < e.Index; i++)
            {
                if (Items[i] is ClipboardEntry) entryRank++;
            }

            if (entryRank < 9)
            {
                string shortcutText = (entryRank + 1).ToString();
                var sRect = new Rectangle(cardRect.Right - 28, topRowY + 1, 20, 18);
                using (var sPath = ModernPalette.CreateRoundedPath(sRect, 3))
                {
                    var sBg = _isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
                    using var sBrush = new SolidBrush(sBg);
                    g.FillPath(sBrush, sPath);
                }
                var sFg = _isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(51, 65, 85);
                TextRenderer.DrawText(g, shortcutText, _shortcutFont, sRect, sFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (entry.Pinned)
            {
                var pinRect = new Rectangle(cardRect.Right - (entryRank < 9 ? 52 : 30), topRowY, 20, 20);
                TextRenderer.DrawText(g, "📌", _pinFont, pinRect, Color.DodgerBlue,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            int middleY = cardRect.Y + (_cardDensity == "Compact" ? 24 : 28);

            bool isImageCard = entry.Format == "image" || entry.HasImage;
            if (isImageCard) DrawImageThumbnail(g, cardRect, entry);

            // Leave room for the thumbnail strip on the right of image cards so
            // text never paints underneath or next to it.
            int textWidth = Math.Max(40, cardRect.Right - contentX - 55 - (isImageCard ? ThumbnailSize + 12 : 0));

            bool isMasked = entry.IsSensitive && !_unmaskedEntryIds.Contains(entry.Id);
            bool hasAlias = !string.IsNullOrWhiteSpace(entry.Alias);
            string primaryText;
            if (isMasked && !hasAlias)
            {
                primaryText = "••••••••••••••••  (Sensitive)";
            }
            else
            {
                primaryText = hasAlias
                    ? $"🏷️ {entry.Alias}"
                    : (entry.Format == "image" ? "[Image / Screenshot]" : (entry.Content ?? string.Empty).Replace("\r", " ").Replace("\n", " "));
            }
            if (string.IsNullOrWhiteSpace(primaryText)) primaryText = "(empty)";
            if (primaryText.Length > 90) primaryText = primaryText.Substring(0, 90) + "...";

            var primaryRect = new Rectangle(contentX, middleY, textWidth, 18);
            var primaryColor = isSelected
                ? (native ? SystemColors.HighlightText : (_isDark ? Color.White : Color.FromArgb(2, 132, 199)))
                : (native ? SystemColors.WindowText : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary));

            TextRenderer.DrawText(g, primaryText, hasAlias ? _primaryBoldFont : _primaryFont, primaryRect, primaryColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (_cardDensity != "Compact")
            {
                var metaParts = new List<string>();
                if (_showSerials) metaParts.Add($"#{entryRank + 1}");
                if (hasAlias && !string.IsNullOrWhiteSpace(entry.Content))
                {
                    string subSnippet = isMasked
                        ? "••••••••"
                        : entry.Content.Replace("\r", " ").Replace("\n", " ").Trim();
                    if (subSnippet.Length > 35) subSnippet = subSnippet.Substring(0, 35) + "...";
                    metaParts.Add(subSnippet);
                }
                if (!string.IsNullOrEmpty(entry.SourceApp)) metaParts.Add(entry.SourceApp);
                metaParts.Add(ModernPalette.GetRelativeTime(entry.CreatedAt));
                if (entry.CopyCount > 1) metaParts.Add($"copied {entry.CopyCount}×");
                if (entry.Format == "image") metaParts.Add("PNG Image");
                else if (entry.Format == "file") metaParts.Add("File Drop");
                else if (!string.IsNullOrEmpty(entry.Content)) metaParts.Add($"{entry.Content.Length} chars");

                var metaRect = new Rectangle(contentX, cardRect.Y + 48, Math.Max(40, textWidth + 30), 15);
                var metaColor = native
                    ? SystemColors.GrayText
                    : (_isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary);
                TextRenderer.DrawText(g, string.Join("  •  ", metaParts), _metaFont, metaRect, metaColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (_isMarqueeActive && _marqueeRect.Width > 0 && _marqueeRect.Height > 0)
            {
                using var marqueeBrush = new SolidBrush(Color.FromArgb(40, 0, 120, 212));
                using var marqueePen = new Pen(Color.FromArgb(180, 0, 120, 212), 1);
                g.FillRectangle(marqueeBrush, _marqueeRect);
                g.DrawRectangle(marqueePen, _marqueeRect);
            }
        }

        private void DrawGroupHeader(Graphics g, Rectangle bounds, CardGroupHeader header, Color windowBg)
        {
            int sepY = bounds.Y + bounds.Height / 2;
            bool native = string.Equals(_visualStyle, "Native", StringComparison.OrdinalIgnoreCase);
            var sepColor = native
                ? SystemColors.ControlDark
                : (_isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240));
            using (var sepPen = new Pen(sepColor, 1))
            {
                g.DrawLine(sepPen, bounds.X + 12, sepY, bounds.Right - 12, sepY);
            }

            string headerText = $"{header.Title} ({header.Count})";
            var textSize = TextRenderer.MeasureText(headerText, _headerFont);
            var textRect = new Rectangle(bounds.X + 16, bounds.Y + (bounds.Height - textSize.Height) / 2, textSize.Width + 8, textSize.Height);

            using (var badgeBg = new SolidBrush(windowBg))
            {
                g.FillRectangle(badgeBg, textRect);
            }

            var headerFg = native
                ? SystemColors.GrayText
                : (_isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary);
            TextRenderer.DrawText(g, headerText, _headerFont, textRect, headerFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (_showCheckboxes && header.Count > 0)
            {
                var cbRect = GetGroupHeaderCheckboxRect(bounds, textRect);
                using (var cbBg = new SolidBrush(windowBg))
                {
                    g.FillRectangle(cbBg, new Rectangle(cbRect.X - 3, bounds.Y + (bounds.Height - textSize.Height) / 2, cbRect.Width + 6, textSize.Height));
                }

                var ids = GetGroupEntryIds(header);
                int checkedCount = ids.Count(id => _checkedIds.Contains(id));
                bool isAllChecked = ids.Count > 0 && checkedCount == ids.Count;
                bool isIndeterminate = checkedCount > 0 && !isAllChecked;

                DrawGroupModernCheckbox(g, cbRect, isAllChecked, isIndeterminate);
            }
        }

        private static Rectangle GetGroupHeaderCheckboxRect(Rectangle bounds, Rectangle textRect)
        {
            int cbX = textRect.Right + 6;
            int cbY = bounds.Y + (bounds.Height - 16) / 2;
            return new Rectangle(cbX, cbY, 16, 16);
        }

        public List<string> GetGroupEntryIds(CardGroupHeader header)
        {
            if (header.EntryIds.Count > 0) return header.EntryIds;

            var list = new List<string>();
            int idx = Items.IndexOf(header);
            if (idx >= 0)
            {
                for (int i = idx + 1; i < Items.Count; i++)
                {
                    if (Items[i] is CardGroupHeader) break;
                    if (Items[i] is ClipboardEntry e) list.Add(e.Id);
                }
            }
            return list;
        }

        public void ToggleGroupChecked(CardGroupHeader header)
        {
            var ids = GetGroupEntryIds(header);
            if (ids.Count == 0) return;

            int checkedCount = ids.Count(id => _checkedIds.Contains(id));
            bool shouldCheck = checkedCount < ids.Count;

            foreach (var id in ids)
            {
                if (shouldCheck)
                {
                    _checkedIds.Add(id);
                }
                else
                {
                    _checkedIds.Remove(id);
                }
            }

            Invalidate();
            SelectionOrCheckedStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DrawGroupModernCheckbox(Graphics g, Rectangle rect, bool isChecked, bool isIndeterminate)
        {
            using var path = ModernPalette.CreateRoundedPath(rect, 3);
            Color border = (isChecked || isIndeterminate)
                ? Color.FromArgb(0, 120, 212)
                : (_isDark ? Color.FromArgb(82, 82, 91) : Color.FromArgb(212, 212, 216));
            Color fill = isChecked
                ? Color.FromArgb(0, 120, 212)
                : (isIndeterminate
                    ? (_isDark ? Color.FromArgb(30, 58, 138) : Color.FromArgb(219, 234, 254))
                    : (_isDark ? Color.FromArgb(39, 39, 42) : Color.White));

            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var p = new Pen(border, 1.2f)) g.DrawPath(p, path);

            if (isChecked)
            {
                using var checkPen = new Pen(Color.White, 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLines(checkPen, new[]
                {
                    new Point(rect.X + 3, rect.Y + 8),
                    new Point(rect.X + 6, rect.Y + 12),
                    new Point(rect.X + 13, rect.Y + 4)
                });
            }
            else if (isIndeterminate)
            {
                using var dashPen = new Pen(Color.FromArgb(0, 120, 212), 2.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                int centerY = rect.Y + rect.Height / 2;
                g.DrawLine(dashPen, rect.X + 3, centerY, rect.Right - 4, centerY);
            }
        }

        private void DrawModernCheckbox(Graphics g, Rectangle rect, bool isChecked)
        {
            using var path = ModernPalette.CreateRoundedPath(rect, 3);
            Color border = isChecked
                ? Color.FromArgb(0, 120, 212)
                : (_isDark ? Color.FromArgb(82, 82, 91) : Color.FromArgb(212, 212, 216));
            Color fill = isChecked
                ? Color.FromArgb(0, 120, 212)
                : (_isDark ? Color.FromArgb(39, 39, 42) : Color.White);

            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var p = new Pen(border, 1.2f)) g.DrawPath(p, path);

            if (!isChecked) return;

            using var checkPen = new Pen(Color.White, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLines(checkPen, new[]
            {
                new Point(rect.X + 3, rect.Y + 8),
                new Point(rect.X + 6, rect.Y + 12),
                new Point(rect.X + 13, rect.Y + 4)
            });
        }

        /// <summary>
        /// Paints the screenshot thumbnail for an image card.
        ///
        /// The thumbnail is letterboxed inside the reserved area so the source
        /// aspect ratio is always preserved; a portrait screenshot and a wide
        /// screenshot both fit without distortion or cropping. Decoding is
        /// cached by <see cref="ThumbnailCache"/>, never done inline per paint.
        /// </summary>
        private void DrawImageThumbnail(Graphics g, Rectangle cardRect, ClipboardEntry entry)
        {
            int width = ThumbnailSize;
            int height = ThumbnailSize;
            int x = cardRect.Right - width - 84;
            int y = cardRect.Y + Math.Max(2, (cardRect.Height - height) / 2);
            var imageRect = new Rectangle(x, y, width, height);

            using (var back = new SolidBrush(_isDark ? Color.FromArgb(24, 24, 27) : Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(back, imageRect);
            }

            byte[]? bytes = null;
            try { bytes = ImageBytesProvider?.Invoke(entry); }
            catch { bytes = null; }

            var thumb = Thumbnails?.GetThumbnail(entry.Id, bytes, width, height);
            if (thumb == null)
            {
                TextRenderer.DrawText(g, "🖼️", _primaryFont, imageRect,
                    _isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int thumbX = imageRect.X + (imageRect.Width - thumb.Width) / 2;
            int thumbY = imageRect.Y + (imageRect.Height - thumb.Height) / 2;
            var previous = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(thumb, new Rectangle(thumbX, thumbY, thumb.Width, thumb.Height));
            g.InterpolationMode = previous;
        }

        /// <summary>Square box the list thumbnail is fitted into.</summary>
        private int ThumbnailSize => _cardDensity == "Compact" ? 40 : 56;

        private int DrawBadgePill(Graphics g, string text, int x, int y, bool isSensitive = false)
        {
            var size = TextRenderer.MeasureText(text, _badgeFont);
            var pillRect = new Rectangle(x, y, size.Width + 8, 17);
            var bg = isSensitive
                ? (_isDark ? Color.FromArgb(127, 29, 29) : Color.FromArgb(254, 226, 226))
                : ModernPalette.GetBadgeBg(text, _isDark);
            var fg = isSensitive
                ? (_isDark ? Color.FromArgb(252, 165, 165) : Color.FromArgb(185, 28, 28))
                : ModernPalette.GetBadgeText(text, _isDark);

            using (var path = ModernPalette.CreateRoundedPath(pillRect, 4))
            {
                using var b = new SolidBrush(bg);
                g.FillPath(b, path);
            }

            TextRenderer.DrawText(g, text, _badgeFont, pillRect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            return pillRect.Width;
        }

        private int DrawTagPill(Graphics g, string text, int x, int y)
        {
            var size = TextRenderer.MeasureText(text, _tagFont);
            var pillRect = new Rectangle(x, y, size.Width + 8, 17);
            var bg = _isDark ? Color.FromArgb(46, 16, 101) : Color.FromArgb(243, 232, 255);
            var fg = _isDark ? Color.FromArgb(216, 180, 254) : Color.FromArgb(107, 33, 168);

            using (var path = ModernPalette.CreateRoundedPath(pillRect, 4))
            {
                using var b = new SolidBrush(bg);
                g.FillPath(b, path);
            }

            TextRenderer.DrawText(g, text, _tagFont, pillRect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            return pillRect.Width;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headerFont.Dispose();
                _badgeFont.Dispose();
                _tagFont.Dispose();
                _primaryFont.Dispose();
                _primaryBoldFont.Dispose();
                _metaFont.Dispose();
                _shortcutFont.Dispose();
                _pinFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class CardGroupHeader
    {
        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Key { get; set; } = string.Empty;
        public bool IsCollapsed { get; set; }
        public List<string> EntryIds { get; } = new();
    }
}

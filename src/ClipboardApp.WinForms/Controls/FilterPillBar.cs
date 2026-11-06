using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    public class FilterPillBar : Control
    {
        public class FilterItem
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public int Count { get; set; }
            public Rectangle Bounds { get; set; }
        }

        private readonly List<FilterItem> _filters = new()
        {
            new FilterItem { Key = "All", Label = "All" },
            new FilterItem { Key = "Text", Label = "📝 Text" },
            new FilterItem { Key = "Image", Label = "🖼️ Images" },
            new FilterItem { Key = "Link", Label = "🔗 Links" },
            new FilterItem { Key = "File", Label = "📁 Files" },
            new FilterItem { Key = "Color", Label = "🎨 Colors" },
            new FilterItem { Key = "Pinned", Label = "📌 Pinned" }
        };

        private string _selectedKey = "All";
        private int _hoveredIndex = -1;
        private bool _isDark = true;
        private bool _useNative;

        public event EventHandler<string>? FilterSelected;

        public string SelectedFilter => _selectedKey;

        public bool IsDark
        {
            get => _isDark;
            set
            {
                _isDark = value;
                Invalidate();
            }
        }

        public bool UseNative
        {
            get => _useNative;
            set
            {
                _useNative = value;
                Invalidate();
            }
        }

        public FilterPillBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            Height = 36;
            Dock = DockStyle.Top;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Cursor = Cursors.Hand;
        }

        public void UpdateCounts(Dictionary<string, int> counts)
        {
            foreach (var item in _filters)
            {
                if (counts.TryGetValue(item.Key, out int count))
                {
                    item.Count = count;
                }
                else
                {
                    item.Count = 0;
                }
            }
            Invalidate();
        }

        public void SetSelectedFilter(string key)
        {
            _selectedKey = key;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevHover = _hoveredIndex;
            _hoveredIndex = -1;

            for (int i = 0; i < _filters.Count; i++)
            {
                if (_filters[i].Bounds.Contains(e.Location))
                {
                    _hoveredIndex = i;
                    break;
                }
            }

            if (_hoveredIndex != prevHover) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _filters.Count; i++)
                {
                    if (_filters[i].Bounds.Contains(e.Location))
                    {
                        _selectedKey = _filters[i].Key;
                        FilterSelected?.Invoke(this, _selectedKey);
                        Invalidate();
                        break;
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _useNative
                ? SystemColors.Window
                : (_isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg);
            using var bgBrush = new SolidBrush(bg);
            g.FillRectangle(bgBrush, ClientRectangle);

            int x = 6;
            int y = 4;
            int pillHeight = 28;

            for (int i = 0; i < _filters.Count; i++)
            {
                var item = _filters[i];
                bool isSelected = item.Key.Equals(_selectedKey, StringComparison.OrdinalIgnoreCase);
                bool isHovered = (i == _hoveredIndex);

                string text = item.Count > 0 ? $"{item.Label} ({item.Count})" : item.Label;
                var textSize = TextRenderer.MeasureText(text, Font);
                int pillWidth = textSize.Width + 18;

                var rect = new Rectangle(x, y, pillWidth, pillHeight);
                item.Bounds = rect;

                // Pill Background
                Color pillBg;
                Color textColor;

                if (isSelected)
                {
                    pillBg = _useNative ? SystemColors.Highlight : (_isDark ? ModernPalette.DarkPillActive : ModernPalette.LightPillActive);
                    textColor = _useNative ? SystemColors.HighlightText : Color.White;
                }
                else if (isHovered)
                {
                    pillBg = _useNative ? SystemColors.Control : (_isDark ? ModernPalette.DarkCardHover : ModernPalette.LightCardHover);
                    textColor = _useNative
                        ? SystemColors.ControlText
                        : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary);
                }
                else
                {
                    pillBg = _useNative ? SystemColors.Control : (_isDark ? ModernPalette.DarkPillBg : ModernPalette.LightPillBg);
                    textColor = _useNative
                        ? SystemColors.ControlText
                        : (_isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary);
                }

                using (var path = CreateRoundedRectanglePath(rect, 14))
                {
                    using var brush = new SolidBrush(pillBg);
                    g.FillPath(brush, path);

                    if (!isSelected && !_useNative && !_isDark)
                    {
                        using var pen = new Pen(ModernPalette.LightCardBorder, 1);
                        g.DrawPath(pen, path);
                    }
                }

                // Pill Text
                TextRenderer.DrawText(g, text, Font, rect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                x += pillWidth + 6;
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

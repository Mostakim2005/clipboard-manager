using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.UI
{
    public class ModernMenuColorTable : ProfessionalColorTable
    {
        private readonly bool _isDark;

        public ModernMenuColorTable(bool isDark)
        {
            _isDark = isDark;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground =>
            _isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(250, 250, 252);

        public override Color MenuBorder =>
            _isDark ? Color.FromArgb(63, 63, 70) : Color.FromArgb(228, 228, 231);

        public override Color MenuItemBorder => Color.Transparent;

        public override Color MenuItemSelected =>
            _isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);

        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;

        public override Color SeparatorDark =>
            _isDark ? Color.FromArgb(50, 50, 56) : Color.FromArgb(228, 228, 231);

        public override Color SeparatorLight => Color.Transparent;
    }

    public class ModernMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool _isDark;

        public ModernMenuRenderer(bool isDark) : base(new ModernMenuColorTable(isDark))
        {
            _isDark = isDark;
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var borderPen = new Pen(_isDark ? Color.FromArgb(63, 63, 70) : Color.FromArgb(212, 212, 216), 1);
            using var path = ModernPalette.CreateRoundedPath(rect, 6);
            g.DrawPath(borderPen, path);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Do not paint the classic WinForms image margin bar
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
                bool isDelete = e.Item?.Text != null && e.Item.Text.Contains("Delete", StringComparison.OrdinalIgnoreCase);

                Color hoverColor;
                if (isDelete)
                {
                    hoverColor = Color.FromArgb(45, 239, 68, 68); // Soft red
                }
                else
                {
                    hoverColor = _isDark
                        ? Color.FromArgb(40, 255, 255, 255) // Translucent Fluent Dark
                        : Color.FromArgb(20, 0, 120, 212); // Translucent Fluent Light
                }

                using var brush = new SolidBrush(hoverColor);
                using var path = ModernPalette.CreateRoundedPath(rect, 4);
                g.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool isDelete = e.Item?.Text != null && e.Item.Text.Contains("Delete", StringComparison.OrdinalIgnoreCase);

            if (isDelete && e.Item?.Selected == true)
            {
                e.TextColor = Color.FromArgb(248, 113, 113); // Red highlight
            }
            else
            {
                e.TextColor = _isDark ? Color.FromArgb(244, 244, 245) : Color.FromArgb(24, 24, 27);
            }

            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (e.Item == null) return;
            var g = e.Graphics;
            int y = e.Item.Height / 2;
            using var pen = new Pen(_isDark ? Color.FromArgb(50, 50, 56) : Color.FromArgb(228, 228, 231), 1);
            g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }
    }
}

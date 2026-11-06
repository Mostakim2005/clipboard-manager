using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    public class GroupedListView : ListView
    {
        public Rectangle MarqueeRect { get; set; } = Rectangle.Empty;

        public GroupedListView()
        {
            // Enable double buffering to eliminate flicker and tearing
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (MarqueeRect.Width > 2 && MarqueeRect.Height > 2)
            {
                using var fill = new SolidBrush(Color.FromArgb(60, SystemColors.Highlight));
                using var pen = new Pen(SystemColors.Highlight);
                e.Graphics.FillRectangle(fill, MarqueeRect);
                e.Graphics.DrawRectangle(pen, MarqueeRect.X, MarqueeRect.Y,
                    Math.Max(0, MarqueeRect.Width - 1), Math.Max(0, MarqueeRect.Height - 1));
            }
        }

        public void AddGroup(string title)
        {
            Groups.Add(new ListViewGroup(title, HorizontalAlignment.Left));
        }

        public void ClearGroups()
        {
            Groups.Clear();
        }

        public void AddItemToGroup(ListViewItem item, int groupIndex)
        {
            if (groupIndex >= 0 && groupIndex < Groups.Count)
            {
                item.Group = Groups[groupIndex];
                Items.Add(item);
            }
        }

        public string GetDateGroupHeader(DateTime date)
        {
            var today = DateTime.Today;
            if (date.Date == today) return "Today";
            if (date.Date == today.AddDays(-1)) return "Yesterday";
            return date.ToString("dddd, MMMM d, yyyy");
        }
    }
}

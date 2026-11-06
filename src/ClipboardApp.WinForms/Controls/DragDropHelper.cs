using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    public static class DragDropHelper
    {
        public static void StartDrag(Control source, string text, string rtf, string html, Image? image, string[]? files = null)
        {
            var data = new DataObject();
            if (!string.IsNullOrEmpty(text)) data.SetText(text, TextDataFormat.UnicodeText);
            if (!string.IsNullOrEmpty(rtf)) data.SetText(rtf, TextDataFormat.Rtf);
            if (!string.IsNullOrEmpty(html)) data.SetText(html, TextDataFormat.Html);
            if (image != null) data.SetImage(image);
            if (files != null && files.Length > 0)
            {
                var col = new StringCollection();
                col.AddRange(files);
                data.SetFileDropList(col);
            }
            source.DoDragDrop(data, DragDropEffects.Copy);
        }
    }
}

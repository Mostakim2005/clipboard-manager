using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    /// <summary>
    /// Loads the embedded multi-resolution app icon. The same source is used by
    /// the window, the tray, and the executable, so branding stays consistent.
    /// </summary>
    public static class AppIcon
    {
        public static Icon Load(int size = 32)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("ClipboardApp.WinForms.Resources.app.ico");
                if (stream != null)
                {
                    return size > 0 ? new Icon(stream, size, size) : new Icon(stream);
                }
            }
            catch { }

            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exe);
                    if (extracted != null)
                    {
                        return size > 0 ? new Icon(extracted, size, size) : (Icon)extracted.Clone();
                    }
                }
            }
            catch { }

            return (Icon)SystemIcons.Application.Clone();
        }
    }
}

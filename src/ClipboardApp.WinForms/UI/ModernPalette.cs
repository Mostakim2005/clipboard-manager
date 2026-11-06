using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ClipboardApp.WinForms.UI
{
    public static class ModernPalette
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static void ApplyDwmStyling(IntPtr hwnd, bool isDark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int darkValue = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, sizeof(int));
                int cornerValue = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerValue, sizeof(int));
            }
            catch { }
        }

        // Dark Theme Colors (Modern Zinc / Slate)
        public static readonly Color DarkWindowBg = Color.FromArgb(24, 24, 27);          // Zinc-900
        public static readonly Color DarkCardBg = Color.FromArgb(39, 39, 42);             // Zinc-800
        public static readonly Color DarkCardHover = Color.FromArgb(63, 63, 70);          // Zinc-700
        public static readonly Color DarkCardSelected = Color.FromArgb(49, 46, 129);      // Indigo-900 tint
        public static readonly Color DarkCardBorder = Color.FromArgb(54, 54, 60);
        public static readonly Color DarkAccent = Color.FromArgb(56, 189, 248);           // Sky-400
        public static readonly Color DarkTextPrimary = Color.FromArgb(244, 244, 245);     // Zinc-100
        public static readonly Color DarkTextSecondary = Color.FromArgb(161, 161, 170);   // Zinc-400
        public static readonly Color DarkSearchBg = Color.FromArgb(32, 32, 36);
        public static readonly Color DarkPillBg = Color.FromArgb(45, 45, 52);
        public static readonly Color DarkPillActive = Color.FromArgb(2, 132, 199);        // Sky-600

        // Light Theme Colors (Clean Slate)
        public static readonly Color LightWindowBg = Color.FromArgb(248, 250, 252);       // Slate-50
        public static readonly Color LightCardBg = Color.FromArgb(255, 255, 255);         // White
        public static readonly Color LightCardHover = Color.FromArgb(241, 245, 249);      // Slate-100
        public static readonly Color LightCardSelected = Color.FromArgb(224, 242, 254);   // Sky-100
        public static readonly Color LightCardBorder = Color.FromArgb(226, 232, 240);     // Slate-200
        public static readonly Color LightAccent = Color.FromArgb(2, 132, 199);           // Sky-600
        public static readonly Color LightTextPrimary = Color.FromArgb(15, 23, 42);       // Slate-900
        public static readonly Color LightTextSecondary = Color.FromArgb(100, 116, 139);  // Slate-500
        public static readonly Color LightSearchBg = Color.FromArgb(255, 255, 255);
        public static readonly Color LightPillBg = Color.FromArgb(241, 245, 249);
        public static readonly Color LightPillActive = Color.FromArgb(2, 132, 199);

        // Badge Pill Colors
        public static Color GetBadgeBg(string format, bool isDark) => format.ToLowerInvariant() switch
        {
            "image" => isDark ? Color.FromArgb(49, 46, 129) : Color.FromArgb(224, 231, 255), // Indigo
            "file" => isDark ? Color.FromArgb(67, 56, 202) : Color.FromArgb(238, 242, 255),  // Violet
            "link" => isDark ? Color.FromArgb(6, 78, 59) : Color.FromArgb(209, 250, 229),    // Emerald
            "color" => isDark ? Color.FromArgb(112, 26, 117) : Color.FromArgb(250, 232, 255),// Fuchsia
            "html" or "rtf" => isDark ? Color.FromArgb(120, 53, 15) : Color.FromArgb(254, 243, 199), // Amber
            _ => isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(241, 245, 249)        // Slate (Text)
        };

        public static Color GetBadgeText(string format, bool isDark) => format.ToLowerInvariant() switch
        {
            "image" => isDark ? Color.FromArgb(165, 180, 252) : Color.FromArgb(67, 56, 202),
            "file" => isDark ? Color.FromArgb(196, 181, 253) : Color.FromArgb(109, 40, 217),
            "link" => isDark ? Color.FromArgb(110, 231, 183) : Color.FromArgb(4, 120, 87),
            "color" => isDark ? Color.FromArgb(240, 171, 252) : Color.FromArgb(162, 28, 175),
            "html" or "rtf" => isDark ? Color.FromArgb(252, 211, 77) : Color.FromArgb(180, 83, 9),
            _ => isDark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(51, 65, 85)
        };

        public static string GetRelativeTime(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24 && dateTime.Date == DateTime.Today) return dateTime.ToString("h:mm tt");
            if (dateTime.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
            return dateTime.ToString("MMM d");
        }

        // Acrylic / Glassmorphism Colors
        public static readonly Color AcrylicCardBgDark = Color.FromArgb(200, 32, 32, 36);
        public static readonly Color AcrylicCardBorderDark = Color.FromArgb(70, 255, 255, 255);
        public static readonly Color AcrylicCardBgLight = Color.FromArgb(220, 255, 255, 255);
        public static readonly Color AcrylicCardBorderLight = Color.FromArgb(140, 255, 255, 255);

        public static System.Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

using System;
using System.Text;

namespace ClipboardApp.Core.Interop
{
    public enum PasteTargetKind
    {
        Unknown,
        RichText,
        PlainText
    }

    /// <summary>
    /// Best-effort capability probe for the focused control of a paste target.
    /// Mixed text+image payloads are only attempted when the destination is
    /// likely to understand HTML or RTF; plain editors get the text fallback
    /// before the clipboard is written.
    /// </summary>
    public static class PasteTargetInspector
    {
        public static PasteTargetKind GetKind(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(targetHwnd))
            {
                return PasteTargetKind.Unknown;
            }

            var focus = GetFocusedWindow(targetHwnd);
            string className = GetClassName(focus != IntPtr.Zero ? focus : targetHwnd);
            var kind = Classify(className);

            // If the focus is a generic child host, inspect its parent once.
            if (kind == PasteTargetKind.Unknown && focus != IntPtr.Zero && focus != targetHwnd)
            {
                var parent = NativeMethods.GetAncestor(focus, NativeMethods.GA_PARENT);
                if (parent != IntPtr.Zero && parent != focus)
                {
                    kind = Classify(GetClassName(parent));
                }
            }

            return kind;
        }

        public static IntPtr GetFocusedWindow(IntPtr targetHwnd)
        {
            uint threadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
            if (threadId == 0) return IntPtr.Zero;

            var gui = new NativeMethods.GUITHREADINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>()
            };

            if (NativeMethods.GetGUIThreadInfo(threadId, ref gui))
            {
                if (gui.hwndFocus != IntPtr.Zero) return gui.hwndFocus;
                if (gui.hwndActive != IntPtr.Zero) return gui.hwndActive;
            }

            return IntPtr.Zero;
        }

        private static string GetClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static PasteTargetKind Classify(string className) => className switch
        {
            "Edit" => PasteTargetKind.PlainText,
            "Scintilla" => PasteTargetKind.PlainText,
            "ConsoleWindowClass" => PasteTargetKind.PlainText,
            "CASCADIA_HOSTING_WINDOW_CLASS" => PasteTargetKind.PlainText,
            "Windows.UI.Core.CoreWindow" => PasteTargetKind.PlainText,
            "RichEdit20W" => PasteTargetKind.RichText,
            "RichEdit20A" => PasteTargetKind.RichText,
            "RichEdit50W" => PasteTargetKind.RichText,
            "RICHEDIT60W" => PasteTargetKind.RichText,
            "RichEditD2DPT" => PasteTargetKind.RichText,
            "_WwG" => PasteTargetKind.RichText,
            "Chrome_RenderWidgetHostHWND" => PasteTargetKind.RichText,
            "MozillaWindowClass" => PasteTargetKind.RichText,
            "Internet Explorer_Server" => PasteTargetKind.RichText,
            "ApplicationFrameWindow" => PasteTargetKind.RichText,
            _ => PasteTargetKind.Unknown
        };
    }
}

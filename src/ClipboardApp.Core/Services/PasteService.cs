using ClipboardApp.Core.Interop;
using ClipboardApp.Core.Models;
using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardApp.Core.Services
{
    public static class PasteService
    {
        /// <summary>
        /// Sets the clipboard with retries. Returns false instead of throwing when
        /// another application keeps the clipboard locked.
        /// </summary>
        public static bool SetClipboardWithRetry(Action setAction, int maxRetries = 6, int delayMs = 35)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    setAction();
                    return true;
                }
                catch (Exception)
                {
                    if (attempt >= maxRetries - 1) return false;
                    Thread.Sleep(delayMs * (attempt + 1));
                }
            }
            return false;
        }

        /// <summary>
        /// Waits until physical shortcut keys are released. Prevents synthetic
        /// Ctrl+V from becoming Ctrl+Enter / Shift+Ctrl+V in the target app.
        /// </summary>
        public static void WaitForKeysReleased(params int[] virtualKeys)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 350)
            {
                bool anyPressed = false;
                foreach (var vk in virtualKeys)
                {
                    if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
                    {
                        anyPressed = true;
                        break;
                    }
                }
                if (!anyPressed) break;
                Thread.Sleep(10);
            }
        }

        public enum PasteFailureReason
        {
            None,
            TargetMissing,
            TargetElevated,
            FocusDenied,
            InputBlocked,
            ClipboardBusy,
            Unknown
        }

        public sealed class PasteAttemptResult
        {
            public bool Success { get; init; }
            public PasteFailureReason Reason { get; init; }
            public string Message { get; init; } = "";

            public static PasteAttemptResult Ok() => new() { Success = true, Reason = PasteFailureReason.None };
            public static PasteAttemptResult Fail(PasteFailureReason reason, string message) =>
                new() { Success = false, Reason = reason, Message = message };
        }

        /// <summary>
        /// Activates the target, verifies that it really owns the foreground,
        /// and only then injects Ctrl+V. Sending a blind keystroke is how
        /// clipboard managers paste into the wrong window.
        /// </summary>
        public static bool SendPasteToWindow(IntPtr targetHwnd) => TrySendPasteToWindow(targetHwnd).Success;

        public static PasteAttemptResult TrySendPasteToWindow(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !NativeMethods.IsWindow(targetHwnd))
            {
                return PasteAttemptResult.Fail(PasteFailureReason.TargetMissing, "The target window no longer exists.");
            }

            var root = NativeMethods.GetAncestor(targetHwnd, NativeMethods.GA_ROOT);
            if (root != IntPtr.Zero) targetHwnd = root;

            if (!NativeMethods.IsWindowVisible(targetHwnd) || !NativeMethods.IsWindowEnabled(targetHwnd))
            {
                return PasteAttemptResult.Fail(PasteFailureReason.TargetMissing, "The target window is hidden or disabled.");
            }

            uint targetPid = 0;
            NativeMethods.GetWindowThreadProcessId(targetHwnd, out targetPid);
            uint ownPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            if (targetPid == ownPid)
            {
                return PasteAttemptResult.Fail(
                    PasteFailureReason.TargetMissing,
                    "ClipboardApp cannot paste into itself. Choose another window.");
            }
            if (targetPid != 0 && NativeMethods.IsProcessElevated(targetPid) && !NativeMethods.IsProcessElevated(ownPid))
            {
                return PasteAttemptResult.Fail(
                    PasteFailureReason.TargetElevated,
                    "The target is running as administrator. Run ClipboardApp as administrator, or use drag-and-drop.");
            }

            WaitForKeysReleased(
                NativeMethods.VK_RETURN,
                NativeMethods.VK_SHIFT,
                NativeMethods.VK_CONTROL,
                NativeMethods.VK_MENU,
                NativeMethods.VK_LBUTTON);

            bool attachedToTarget = false;
            bool attachedToForeground = false;
            uint currentThreadId = 0;
            uint targetThreadId = 0;
            uint foregroundThreadId = 0;

            try
            {
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

                if (NativeMethods.IsIconic(targetHwnd))
                {
                    NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_RESTORE);
                    Thread.Sleep(90);
                }

                // A synthetic Alt tap is the standard workaround for Windows'
                // foreground lock. It is released before Ctrl+V is sent.
                NativeMethods.keybd_event((byte)NativeMethods.VK_MENU, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(15);

                currentThreadId = NativeMethods.GetCurrentThreadId();
                targetThreadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
                var foreground = NativeMethods.GetForegroundWindow();
                foregroundThreadId = foreground != IntPtr.Zero
                    ? NativeMethods.GetWindowThreadProcessId(foreground, out _)
                    : 0;

                if (currentThreadId != foregroundThreadId && foregroundThreadId != 0)
                {
                    attachedToForeground = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }
                if (currentThreadId != targetThreadId && targetThreadId != 0)
                {
                    attachedToTarget = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                bool focused = false;
                for (int attempt = 0; attempt < 7; attempt++)
                {
                    NativeMethods.SetForegroundWindow(targetHwnd);
                    NativeMethods.BringWindowToTop(targetHwnd);
                    NativeMethods.SetActiveWindow(targetHwnd);

                    // Electron and browser targets keep keyboard focus on a
                    // child renderer. Attach to the target thread first, then
                    // give that child focus instead of the top-level frame.
                    var gui = new NativeMethods.GUITHREADINFO
                    {
                        cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>()
                    };
                    if (targetThreadId != 0 && NativeMethods.GetGUIThreadInfo(targetThreadId, ref gui))
                    {
                        if (gui.hwndFocus != IntPtr.Zero)
                        {
                            NativeMethods.SetFocus(gui.hwndFocus);
                        }
                        else if (gui.hwndActive != IntPtr.Zero)
                        {
                            NativeMethods.SetFocus(gui.hwndActive);
                        }
                    }

                    if (attempt >= 2)
                    {
                        // Undocumented but widely supported fallback for
                        // stubborn foreground-lock cases.
                        NativeMethods.SwitchToThisWindow(targetHwnd, false);
                    }

                    Thread.Sleep(55);
                    if (IsForegroundTarget(targetHwnd))
                    {
                        focused = true;
                        break;
                    }
                }

                if (!focused)
                {
                    return PasteAttemptResult.Fail(
                        PasteFailureReason.FocusDenied,
                        "Windows refused to give the target focus. It may be elevated, on another desktop, or holding a modal dialog.");
                }

                Thread.Sleep(30);

                ushort ctrlScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_CONTROL, NativeMethods.MAPVK_VK_TO_VSC);
                ushort vScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_V, NativeMethods.MAPVK_VK_TO_VSC);

                var inputs = new NativeMethods.INPUT[4];

                inputs[0].type = NativeMethods.INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;
                inputs[0].u.ki.wScan = ctrlScan;
                inputs[0].u.ki.dwFlags = 0;

                inputs[1].type = NativeMethods.INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = NativeMethods.VK_V;
                inputs[1].u.ki.wScan = vScan;
                inputs[1].u.ki.dwFlags = 0;

                inputs[2].type = NativeMethods.INPUT_KEYBOARD;
                inputs[2].u.ki.wVk = NativeMethods.VK_V;
                inputs[2].u.ki.wScan = vScan;
                inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

                inputs[3].type = NativeMethods.INPUT_KEYBOARD;
                inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
                inputs[3].u.ki.wScan = ctrlScan;
                inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

                uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                if (sent != inputs.Length)
                {
                    return PasteAttemptResult.Fail(PasteFailureReason.InputBlocked, "Windows blocked synthetic keyboard input.");
                }

                return PasteAttemptResult.Ok();
            }
            catch (Exception ex)
            {
                return PasteAttemptResult.Fail(PasteFailureReason.Unknown, ex.Message);
            }
            finally
            {
                if (attachedToTarget && currentThreadId != 0 && targetThreadId != 0)
                {
                    NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
                }
                if (attachedToForeground && currentThreadId != 0 && foregroundThreadId != 0)
                {
                    NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
        }

        private static bool IsForegroundTarget(IntPtr target)
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            if (foreground == target) return true;

            var foregroundRoot = NativeMethods.GetAncestor(foreground, NativeMethods.GA_ROOT);
            var targetRoot = NativeMethods.GetAncestor(target, NativeMethods.GA_ROOT);
            return foregroundRoot == target || targetRoot == foreground;
        }

        // ---------------------------------------------------------------- clipboard writes

        public static bool CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return SetClipboardWithRetry(() => Clipboard.SetText(text, TextDataFormat.UnicodeText));
        }

        public static bool CopyHtml(string htmlFragment)
        {
            if (string.IsNullOrEmpty(htmlFragment)) return false;

            string cfHtml = BuildCfHtml(htmlFragment);
            string plainText = HtmlToPlainText(htmlFragment);

            return SetClipboardWithRetry(() =>
            {
                var data = new DataObject();
                data.SetData(DataFormats.Html, false, cfHtml);
                data.SetData(DataFormats.UnicodeText, false, plainText);
                Clipboard.SetDataObject(data, copy: true, retryTimes: 5, retryDelay: 35);
            });
        }

        public static bool CopyRtf(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return false;

            return SetClipboardWithRetry(() =>
            {
                var data = new DataObject();
                data.SetData(DataFormats.Rtf, false, rtf);
                data.SetData(DataFormats.UnicodeText, false, RtfToPlainText(rtf));
                Clipboard.SetDataObject(data, copy: true, retryTimes: 5, retryDelay: 35);
            });
        }

        /// <summary>
        /// Writes a rich payload that carries HTML, RTF and plain text at the
        /// same time. Apps that understand HTML/RTF paste formatted text and
        /// embedded images; plain-text apps paste the UnicodeText fallback.
        /// </summary>
        public static bool CopyRich(string htmlFragment, string rtf, string plainText)
        {
            if (string.IsNullOrEmpty(htmlFragment) &&
                string.IsNullOrEmpty(rtf) &&
                string.IsNullOrEmpty(plainText))
            {
                return false;
            }

            return SetClipboardWithRetry(() =>
            {
                var data = new DataObject();
                if (!string.IsNullOrEmpty(plainText))
                {
                    data.SetData(DataFormats.UnicodeText, false, plainText);
                }
                if (!string.IsNullOrEmpty(htmlFragment))
                {
                    data.SetData(DataFormats.Html, false, BuildCfHtml(htmlFragment));
                }
                if (!string.IsNullOrEmpty(rtf))
                {
                    data.SetData(DataFormats.Rtf, false, rtf);
                }
                Clipboard.SetDataObject(data, copy: true, retryTimes: 5, retryDelay: 35);
            });
        }

        public static bool CopyImageBytes(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return false;

            return SetClipboardWithRetry(() =>
            {
                using var ms = new MemoryStream(imageBytes);
                using var bmp = new Bitmap(ms);
                using var png = new MemoryStream(imageBytes);
                var data = new DataObject();
                data.SetData(DataFormats.Bitmap, true, new Bitmap(bmp));
                data.SetData("PNG", false, png);
                Clipboard.SetDataObject(data, copy: true, retryTimes: 5, retryDelay: 35);
            });
        }

        public static bool CopyFiles(StringCollection filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return false;

            return SetClipboardWithRetry(() =>
            {
                var data = new DataObject();
                data.SetFileDropList(filePaths);
                data.SetData(DataFormats.UnicodeText, false, string.Join(Environment.NewLine, filePaths.Cast<string>()));
                Clipboard.SetDataObject(data, copy: true, retryTimes: 5, retryDelay: 35);
            });
        }

        public static bool CopyEntry(ClipboardEntry entry, byte[]? imageData = null)
        {
            if (entry == null) return false;

            if (entry.Format == "image" || entry.HasImage)
            {
                var bytes = imageData ?? entry.ImageData;
                return bytes != null && CopyImageBytes(bytes);
            }

            if (entry.Format == "file")
            {
                var files = (entry.Content ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => f.Length > 0)
                    .ToList();
                if (files.Count == 0) return false;
                var collection = new StringCollection();
                collection.AddRange(files.ToArray());
                return CopyFiles(collection);
            }

            if (entry.Format == "html") return CopyHtml(entry.Content);
            if (entry.Format == "rtf") return CopyRtf(entry.Content);
            return CopyText(entry.Content);
        }

        // ---------------------------------------------------------------- paste + write

        public static bool PasteText(string text, IntPtr targetHwnd) => CopyText(text) && SendPasteToWindow(targetHwnd);

        public static bool PasteHtml(string html, IntPtr targetHwnd) => CopyHtml(html) && SendPasteToWindow(targetHwnd);

        public static bool PasteRtf(string rtf, IntPtr targetHwnd) => CopyRtf(rtf) && SendPasteToWindow(targetHwnd);

        public static bool PasteImageBytes(byte[] imageBytes, IntPtr targetHwnd) => CopyImageBytes(imageBytes) && SendPasteToWindow(targetHwnd);

        public static bool PasteFiles(StringCollection filePaths, IntPtr targetHwnd) => CopyFiles(filePaths) && SendPasteToWindow(targetHwnd);

        public static PasteAttemptResult PasteFilesDetailed(StringCollection filePaths, IntPtr targetHwnd)
        {
            if (!CopyFiles(filePaths))
            {
                return PasteAttemptResult.Fail(PasteFailureReason.ClipboardBusy, "The clipboard is busy. Try again.");
            }
            return TrySendPasteToWindow(targetHwnd);
        }

        public static PasteAttemptResult PasteRichDetailed(
            string htmlFragment,
            string rtf,
            string plainText,
            IntPtr targetHwnd)
        {
            if (!CopyRich(htmlFragment, rtf, plainText))
            {
                return PasteAttemptResult.Fail(PasteFailureReason.ClipboardBusy, "The clipboard is busy. Try again.");
            }
            return TrySendPasteToWindow(targetHwnd);
        }

        public static bool PasteEntry(ClipboardEntry entry, IntPtr targetHwnd, byte[]? imageData = null)
        {
            return PasteEntryDetailed(entry, targetHwnd, imageData).Success;
        }

        public static PasteAttemptResult PasteEntryDetailed(ClipboardEntry entry, IntPtr targetHwnd, byte[]? imageData = null)
        {
            if (!CopyEntry(entry, imageData))
            {
                return PasteAttemptResult.Fail(PasteFailureReason.ClipboardBusy, "The clipboard is busy. Try again.");
            }
            return TrySendPasteToWindow(targetHwnd);
        }

        // ---------------------------------------------------------------- text/format helpers

        internal static string BuildCfHtml(string fragment)
        {
            string html = "<html><body><!--StartFragment-->" + fragment + "<!--EndFragment--></body></html>";
            const string placeholders = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

            int headerBytes = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, placeholders, 0, 0, 0, 0));
            int startFragment = headerBytes + Encoding.UTF8.GetByteCount("<html><body><!--StartFragment-->");
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            int endHtml = headerBytes + Encoding.UTF8.GetByteCount(html);

            string header = string.Format(
                CultureInfo.InvariantCulture,
                placeholders,
                headerBytes,
                endHtml,
                startFragment,
                endFragment);

            return header + html;
        }

        public static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
            withoutTags = withoutTags.Replace("&nbsp;", " ");
            withoutTags = System.Net.WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(withoutTags, @"[ \t]{2,}", " ").Trim();
        }

        public static string RtfToPlainText(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return string.Empty;

            var text = Regex.Replace(rtf, @"\\u(-?\d+)\??", m =>
            {
                if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    if (code < 0) code += 65536;
                    return ((char)code).ToString();
                }
                return string.Empty;
            });
            text = Regex.Replace(text, @"\\par[d]?\b", "\n");
            text = text.Replace("\\tab", "\t");
            text = Regex.Replace(text, @"\\[a-zA-Z]+-?\d* ?", string.Empty);
            text = text.Replace("{", string.Empty).Replace("}", string.Empty);
            text = text.Replace("\r", string.Empty);
            return text.Trim();
        }
    }
}

using System;
using System.Windows.Forms;
using ClipboardApp.Core.Interop;

namespace ClipboardApp.Core
{
    public class ClipboardMonitor : NativeWindow, IDisposable
    {
        private bool _listening;
        private bool _disposed;
        public bool Paused { get; set; }
        public event EventHandler? ClipboardChanged;

        public ClipboardMonitor()
        {
            var cp = new CreateParams
            {
                // HWND_MESSAGE (-3): Creates a message-only window, not visible in Alt-Tab or desktop
                Parent = (IntPtr)(-3)
            };
            CreateHandle(cp);
            _listening = NativeMethods.AddClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                if (!Paused)
                {
                    try { ClipboardChanged?.Invoke(this, EventArgs.Empty); } catch { }
                }
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_listening && Handle != IntPtr.Zero)
            {
                NativeMethods.RemoveClipboardFormatListener(Handle);
                _listening = false;
            }
            DestroyHandle();
            GC.SuppressFinalize(this);
        }
    }
}

using System;
using System.Windows.Forms;
using ClipboardApp.Core.Interop;
using ClipboardApp.Core.Services;

namespace ClipboardApp.WinForms
{
    public class HotkeyManager : IDisposable
    {
        private readonly Form _form;
        private readonly int _id;
        private bool _isRegistered;

        public event EventHandler? HotkeyPressed;

        /// <summary>The canonical string of the combination currently registered.</summary>
        public string RegisteredHotkey { get; private set; } = "";

        public HotkeyManager(Form form, int id = 1)
        {
            _form = form;
            _id = id;
        }

        public bool Register(string hotkey, out string errorMessage)
            => Register(hotkey, _form, out errorMessage);

        /// <summary>
        /// Registers the combination on <paramref name="messageTarget"/>. Settings
        /// can therefore test a candidate hotkey on a hidden window without
        /// disturbing the running application instance.
        /// </summary>
        public bool Register(string hotkey, Form messageTarget, out string errorMessage)
        {
            errorMessage = string.Empty;
            Unregister();

            if (!HotkeyParser.TryParse(hotkey, out int mods, out int vk))
            {
                errorMessage = string.IsNullOrWhiteSpace(hotkey)
                    ? "Hotkey string is empty."
                    : $"Invalid key combination: '{hotkey}'";
                return false;
            }

            if (!messageTarget.IsHandleCreated)
            {
                _ = messageTarget.Handle; // Ensure handle is created
            }

            _isRegistered = NativeMethods.RegisterHotKey(messageTarget.Handle, _id, mods | HotkeyParser.ModNoRepeat, vk);
            if (!_isRegistered)
            {
                errorMessage = $"Failed to register '{hotkey}'. It may already be in use by another application or Windows.";
                return false;
            }

            // Keep the target so Unregister releases it from the same window.
            _targetHandle = messageTarget.Handle;
            RegisteredHotkey = HotkeyParser.Format(mods, vk);
            return true;
        }

        private IntPtr _targetHandle = IntPtr.Zero;

        /// <summary>
        /// Probes whether a combination can be registered right now without
        /// leaving anything registered. Used by the key-capture UI to warn
        /// before the user commits a conflicting shortcut.
        /// </summary>
        public static bool TryProbe(int modifiers, int virtualKey, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!HotkeyParser.HasModifier(modifiers))
            {
                errorMessage = "A global shortcut needs at least one of Ctrl, Alt, Shift or Win.";
                return false;
            }

            using var probe = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32000, -32000),
                Size = new System.Drawing.Size(1, 1)
            };
            _ = probe.Handle;

            const int probeId = 0x7C1;
            bool ok = NativeMethods.RegisterHotKey(probe.Handle, probeId, modifiers | HotkeyParser.ModNoRepeat, virtualKey);
            if (ok)
            {
                NativeMethods.UnregisterHotKey(probe.Handle, probeId);
                return true;
            }

            errorMessage = "That shortcut is already in use by Windows or another application.";
            return false;
        }

        public void Unregister()
        {
            if (_isRegistered)
            {
                try { NativeMethods.UnregisterHotKey(_targetHandle, _id); }
                catch { }
                _isRegistered = false;
            }
            _targetHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            GC.SuppressFinalize(this);
        }

        public void ProcessMessage(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == _id)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

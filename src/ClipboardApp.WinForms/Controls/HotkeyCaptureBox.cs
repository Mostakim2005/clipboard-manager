using ClipboardApp.Core.Services;
using ClipboardApp.WinForms.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    /// <summary>
    /// A read-only shortcut display that captures the next key combination the
    /// user presses instead of asking them to type "Ctrl+F1".
    ///
    /// Flow:
    ///   1. Click the captured row (or the Set button) to enter capture mode.
    ///   2. The row clears and shows "Press the new shortcut...".
    ///   3. Modifier keys update the live preview; the first non-modifier key
    ///      completes the combination, which is validated and applied through
    ///      <see cref="HotkeyApplied"/>.
    ///   4. Esc cancels and restores the previous shortcut.
    ///
    /// The control never writes to the clipboard and never depends on the
    /// global hotkey being registered; callers decide what to do with the value.
    /// </summary>
    public sealed class HotkeyCaptureBox : Panel
    {
        private readonly TextBox _display;
        private readonly Button _setButton;
        private readonly Button _clearButton;
        private readonly ToolTip _tip;
        private readonly PaintSurface _surface;

        private bool _capturing;
        private int _pendingModifiers;
        private string _committedValue = HotkeyParser.DefaultHotkey;
        private bool _isDark;

        /// <summary>Raised with the validated canonical shortcut string.</summary>
        public event EventHandler<string>? HotkeyApplied;

        /// <summary>Raised whenever capture starts or stops (used to pause global hotkeys).</summary>
        public event EventHandler<bool>? CaptureStateChanged;

        public bool IsCapturing => _capturing;

        /// <summary>The committed shortcut (validated before it lands here).</summary>
        public string Value
        {
            get => _committedValue;
            set
            {
                _committedValue = string.IsNullOrWhiteSpace(value) ? "" : (HotkeyParser.TryParse(value, out int m, out int v)
                    ? HotkeyParser.Format(m, v)
                    : value);
                RefreshDisplay();
            }
        }

        public HotkeyCaptureBox(bool isDark)
        {
            _isDark = isDark;
            Height = 32;
            Padding = Padding.Empty;

            _surface = new PaintSurface
            {
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            _surface.Click += (_, _) => BeginCapture();

            _display = new TextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                TabStop = false,
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                ShortcutsEnabled = false
            };
            _display.Click += (_, _) => BeginCapture();

            _setButton = new Button
            {
                Text = "Set",
                Dock = DockStyle.Right,
                Width = 56,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _setButton.FlatAppearance.BorderSize = 0;
            _setButton.Click += (_, _) => BeginCapture();

            _clearButton = new Button
            {
                Text = "Reset",
                Dock = DockStyle.Right,
                Width = 56,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.Click += (_, _) =>
            {
                CancelCapture();
                Value = HotkeyParser.DefaultHotkey;
                HotkeyApplied?.Invoke(this, _committedValue);
            };

            // _surface hosts the painted frame; the text box lives inside it.
            _surface.Controls.Add(_display);
            Controls.Add(_surface);
            Controls.Add(_clearButton);
            Controls.Add(_setButton);

            _tip = new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 350,
                ReshowDelay = 80,
                ShowAlways = true
            };
            _tip.SetToolTip(_surface, "Click, then press the shortcut you want. Esc cancels.");
            _tip.SetToolTip(_setButton, "Capture the next key combination you press.");

            ApplyTheme(isDark);
            RefreshDisplay();

            // Capture mode requires all key input, including Tab/Esc, to stay
            // inside this control.
            _surface.PreviewKeyDown += OnCapturePreviewKeyDown;
            _surface.KeyDown += OnCaptureKeyDown;
            _surface.KeyUp += OnCaptureKeyUp;
            _surface.LostFocus += (_, _) => EndCapture(silent: true);
        }

        public void ApplyTheme(bool isDark)
        {
            _isDark = isDark;
            _display.BackColor = isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg;
            _display.ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            foreach (var button in new[] { _setButton, _clearButton })
            {
                button.BackColor = isDark ? Color.FromArgb(45, 45, 52) : Color.FromArgb(241, 245, 249);
                button.ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            }
            _surface.Invalidate();
        }

        // ------------------------------------------------------------------ capture

        public void BeginCapture()
        {
            if (_capturing) return;
            _capturing = true;
            _pendingModifiers = 0;
            _setButton.Text = "Cancel";
            _surface.TabStop = true;
            _surface.Focus();
            CaptureStateChanged?.Invoke(this, true);
            RefreshDisplay();
        }

        private void CancelCapture()
        {
            if (!_capturing) return;
            EndCapture(silent: false);
        }

        private void EndCapture(bool silent)
        {
            if (!_capturing) return;
            _capturing = false;
            _pendingModifiers = 0;
            _setButton.Text = "Set";
            _surface.TabStop = false;
            if (!silent) RefreshDisplay();
            else RefreshDisplay();
            CaptureStateChanged?.Invoke(this, false);
        }

        private void OnCapturePreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (!_capturing) return;
            // Keep Tab / arrows / Enter inside the capture surface.
            e.IsInputKey = true;
        }

        private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
        {
            if (!_capturing) return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                EndCapture(silent: false);
                _setButton.Focus();
                return;
            }

            if (IsModifierKey(e.KeyCode))
            {
                _pendingModifiers = CurrentModifiers(e);
                RefreshDisplay();
                return;
            }

            int modifiers = CurrentModifiers(e);
            int vk = MapKey(e.KeyCode);
            if (vk == 0)
            {
                ShowHint("That key cannot be used as a global shortcut.");
                return;
            }

            if (!HotkeyParser.HasModifier(modifiers))
            {
                ShowHint("Hold Ctrl, Alt, Shift or Win with another key. A bare key would block typing everywhere.");
                _pendingModifiers = 0;
                RefreshDisplay();
                return;
            }

            if (!HotkeyManager.TryProbe(modifiers, vk, out string error))
            {
                ShowHint(error);
                _pendingModifiers = 0;
                RefreshDisplay();
                return;
            }

            string formatted = HotkeyParser.Format(modifiers, vk);
            _committedValue = formatted;
            EndCapture(silent: true);
            _tip.Show("Saved: " + formatted, _surface, 2400);
            HotkeyApplied?.Invoke(this, formatted);
        }

        private void OnCaptureKeyUp(object? sender, KeyEventArgs e)
        {
            if (!_capturing || !IsModifierKey(e.KeyCode)) return;
            // Modifiers are cleared when released without a completing key.
            _pendingModifiers = CurrentModifiers(e);
            RefreshDisplay();
        }

        private static bool IsModifierKey(Keys key)
            => key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
                or Keys.Menu or Keys.LMenu or Keys.RMenu
                or Keys.LWin or Keys.RWin;

        private static int CurrentModifiers(KeyEventArgs e)
        {
            int mods = 0;
            var keys = e.Modifiers;
            if ((keys & Keys.Control) != 0) mods |= HotkeyParser.ModControl;
            if ((keys & Keys.Shift) != 0) mods |= HotkeyParser.ModShift;
            if ((keys & Keys.Alt) != 0) mods |= HotkeyParser.ModAlt;

            // Win is not surfaced through KeyEventArgs.Modifiers.
            if ((NativeKeyState(0x5B) & 0x8000) != 0 || (NativeKeyState(0x5C) & 0x8000) != 0)
            {
                mods |= HotkeyParser.ModWin;
            }
            return mods;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static int NativeKeyState(int vKey) => GetAsyncKeyState(vKey);

        private static int MapKey(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z) return (int)key;
            if (key >= Keys.D0 && key <= Keys.D9) return (int)key;
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return 0x60 + ((int)key - (int)Keys.NumPad0);
            if (key >= Keys.F1 && key <= Keys.F24) return HotkeyParser.VkF1 + ((int)key - (int)Keys.F1);

            return key switch
            {
                Keys.Oemtilde => HotkeyParser.VkTilde,
                Keys.Space => 0x20,
                Keys.Tab => 0x09,
                Keys.Oemplus => 0xBB,
                Keys.OemMinus => 0xBD,
                Keys.Oemcomma => 0xBC,
                Keys.OemPeriod => 0xBE,
                Keys.OemQuestion => 0xBF,
                Keys.OemOpenBrackets => 0xDB,
                Keys.OemCloseBrackets => 0xDD,
                Keys.OemPipe => 0xDC,
                Keys.OemSemicolon => 0xBA,
                Keys.OemQuotes => 0xDE,
                Keys.Insert => 0x2D,
                Keys.Delete => 0x2E,
                Keys.Home => 0x24,
                Keys.End => 0x23,
                Keys.PageUp => 0x21,
                Keys.PageDown => 0x22,
                Keys.Up => 0x26,
                Keys.Down => 0x28,
                Keys.Left => 0x25,
                Keys.Right => 0x27,
                Keys.Back => 0x08,
                _ => 0
            };
        }

        private void ShowHint(string message)
        {
            _tip.Show(message, _surface, 3200);
        }

        // ------------------------------------------------------------------ painting

        private void RefreshDisplay()
        {
            // OnResize can fire from the base constructor before the child
            // controls exist; nothing to paint in that case.
            if (_display == null || _setButton == null || _surface == null) return;

            if (_capturing)
            {
                string preview = _pendingModifiers == 0
                    ? ""
                    : string.Join("+", HotkeyParser.DescribeModifiers(_pendingModifiers)) + "+";
                _display.Text = preview.Length == 0 ? "Press the new shortcut…" : preview + "▌";
                _display.ForeColor = _isDark ? Color.FromArgb(125, 211, 252) : Color.FromArgb(2, 132, 199);
                _setButton.Text = "Cancel";
            }
            else
            {
                _display.Text = string.IsNullOrWhiteSpace(_committedValue)
                    ? "(no shortcut)"
                    : HotkeyParser.ToDisplayString(_committedValue);
                _display.ForeColor = _isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
                _setButton.Text = "Set";
            }

            // Center the text box vertically inside the painted frame.
            _surface.BorderColor = _isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            _surface.FillColor = _isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg;
            _surface.Highlight = _capturing;
            _display.SetBounds(
                Padding.Left + 8,
                (Height - _display.PreferredHeight) / 2,
                Math.Max(10, _surface.Width - 16),
                _display.PreferredHeight);
            _surface.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RefreshDisplay();
        }

        /// <summary>
        /// Border frame behind the text box. It owns the focus rectangle so the
        /// capture state is visible even though the text box never takes focus.
        /// </summary>
        private sealed class PaintSurface : Panel
        {
            public Color BorderColor { get; set; } = Color.Gray;
            public Color FillColor { get; set; } = Color.Transparent;
            public bool Highlight { get; set; }

            public PaintSurface()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                Padding = Padding.Empty;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(Highlight ? Color.FromArgb(0, 120, 212) : BorderColor, Highlight ? 1.6f : 1f);
                g.FillRectangle(fill, rect);
                g.DrawRectangle(pen, rect);
                if (Highlight && Focused)
                {
                    var focusRect = Rectangle.Inflate(rect, -2, -2);
                    ControlPaint.DrawFocusRectangle(g, focusRect);
                }
            }

            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }
    }
}

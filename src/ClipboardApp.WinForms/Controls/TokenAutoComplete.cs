using ClipboardApp.Core.Services;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    /// <summary>
    /// Lightweight token autocomplete for tag/alias text boxes. Suggestions
    /// are learned from history and never modify the text until the user
    /// accepts one with Enter, Tab, or a click.
    /// </summary>
    public sealed class TokenAutoComplete : IDisposable
    {
        private readonly TextBox _textBox;
        private readonly Func<IEnumerable<string>> _source;
        private readonly char[] _separators;
        private readonly SuggestionPopup _popup;
        private bool _updating;
        private bool _disposed;
        private int _tokenStart;
        private int _tokenLength;

        public TokenAutoComplete(
            TextBox textBox,
            Func<IEnumerable<string>> source,
            params char[] separators)
        {
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _source = source ?? (() => Enumerable.Empty<string>());
            _separators = separators == null || separators.Length == 0
                ? new[] { ',' }
                : separators;

            _popup = new SuggestionPopup();
            _popup.List.MouseDown += OnListMouseDown;
            _popup.List.DoubleClick += (_, _) => Accept();

            _textBox.TextChanged += OnTextChanged;
            _textBox.KeyDown += OnKeyDown;
            _textBox.LostFocus += OnLostFocus;
            _textBox.Disposed += (_, _) => Dispose();
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            if (_updating || _disposed) return;
            UpdateSuggestions();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_disposed) return;

            if (e.KeyCode == Keys.Escape && _popup.Visible)
            {
                ClosePopup();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (!_popup.Visible) return;

            if (e.KeyCode == Keys.Down)
            {
                MoveSelection(1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                if (_popup.List.SelectedIndex >= 0)
                {
                    Accept();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void OnLostFocus(object? sender, EventArgs e)
        {
            if (_disposed) return;
            if (_popup.Visible && _popup.Bounds.Contains(Cursor.Position))
            {
                // Clicking the suggestion list is handled by MouseDown.
                return;
            }
            ClosePopup();
        }

        private void OnListMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int index = _popup.List.IndexFromPoint(e.Location);
            if (index < 0) return;

            _popup.List.SelectedIndex = index;
            Accept();
        }

        private void UpdateSuggestions()
        {
            string text = _textBox.Text ?? string.Empty;
            int caret = Math.Clamp(_textBox.SelectionStart, 0, text.Length);

            string token = TagSuggestionService.GetToken(
                text,
                caret,
                _separators,
                out _tokenStart,
                out _tokenLength);

            // Do not pop up on an empty token; that would show the entire tag
            // list every time the user types a comma.
            if (token.Length == 0)
            {
                ClosePopup();
                return;
            }

            var suggestions = TagSuggestionService.Suggest(_source(), token, 8);
            if (suggestions.Count == 0)
            {
                ClosePopup();
                return;
            }

            _popup.List.BeginUpdate();
            try
            {
                _popup.List.Items.Clear();
                foreach (var suggestion in suggestions)
                {
                    _popup.List.Items.Add(suggestion);
                }
                _popup.List.SelectedIndex = 0;
            }
            finally
            {
                _popup.List.EndUpdate();
            }

            ShowPopup();
        }

        private void ShowPopup()
        {
            ApplyTheme();

            int itemHeight = Math.Max(18, _popup.List.Font.Height + 6);
            int visibleRows = Math.Min(8, Math.Max(1, _popup.List.Items.Count));
            int width = Math.Max(_textBox.Width, 230);
            int height = (visibleRows * itemHeight) + 4;

            _popup.List.Width = width - 2;
            _popup.List.Height = height - 2;
            _popup.ClientSize = new Size(width, height);

            Point anchor = _textBox.GetPositionFromCharIndex(Math.Max(0, _tokenStart));
            Point screen = _textBox.PointToScreen(new Point(Math.Max(0, anchor.X), _textBox.Height + 2));

            var screenRect = Screen.FromControl(_textBox).WorkingArea;
            if (screen.X + width > screenRect.Right) screen.X = Math.Max(screenRect.Left, screenRect.Right - width);
            if (screen.Y + height > screenRect.Bottom) screen.Y = Math.Max(screenRect.Top, screen.Y - _textBox.Height - height - 4);

            _popup.Location = screen;
            if (!_popup.Visible) _popup.Show();
            _popup.BringToFront();
        }

        private void ApplyTheme()
        {
            var settings = UiStyler.CurrentSettings;
            var tokens = UiStyler.GetTokens(settings);
            _popup.BackColor = tokens.Border;
            _popup.List.BackColor = tokens.CardBg;
            _popup.List.ForeColor = tokens.TextPrimary;
        }

        private void MoveSelection(int delta)
        {
            int count = _popup.List.Items.Count;
            if (count == 0) return;

            int index = _popup.List.SelectedIndex + delta;
            if (index < 0) index = count - 1;
            if (index >= count) index = 0;
            _popup.List.SelectedIndex = index;
        }

        private void Accept()
        {
            if (!_popup.Visible) return;
            if (_popup.List.SelectedItem is not string suggestion || suggestion.Length == 0)
            {
                ClosePopup();
                return;
            }

            _updating = true;
            try
            {
                string updated = TagSuggestionService.ApplySuggestion(
                    _textBox.Text,
                    _tokenStart,
                    _tokenLength,
                    suggestion);

                _textBox.Text = updated;
                int caret = Math.Clamp(_tokenStart + suggestion.Length, 0, _textBox.Text.Length);
                _textBox.SelectionStart = caret;
                _textBox.SelectionLength = 0;
            }
            finally
            {
                _updating = false;
            }

            ClosePopup();
            _textBox.Focus();
        }

        private void ClosePopup()
        {
            if (_popup.Visible) _popup.Hide();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _textBox.TextChanged -= OnTextChanged;
            _textBox.KeyDown -= OnKeyDown;
            _textBox.LostFocus -= OnLostFocus;

            if (_popup.Visible) _popup.Hide();
            _popup.Dispose();
        }

        private sealed class SuggestionPopup : Form
        {
            public ListBox List { get; }

            public SuggestionPopup()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                TopMost = true;
                Padding = new Padding(1);
                BackColor = SystemColors.ControlDark;

                List = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None,
                    IntegralHeight = false,
                    TabStop = false,
                    Font = new Font("Segoe UI", 9.5f)
                };

                Controls.Add(List);
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    var parameters = base.CreateParams;
                    parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    return parameters;
                }
            }
        }
    }
}

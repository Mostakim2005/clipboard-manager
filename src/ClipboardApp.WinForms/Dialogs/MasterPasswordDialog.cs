using ClipboardApp.WinForms.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public sealed class MasterPasswordDialog : Form
    {
        private readonly TextBox _password;
        private readonly TextBox? _confirm;
        private readonly bool _requireConfirmation;
        private readonly bool _isDark;

        public string Password => _password.Text;

        public MasterPasswordDialog(string title, string message, bool requireConfirmation, bool isDark)
        {
            _requireConfirmation = requireConfirmation;
            _isDark = isDark;

            Text = title;
            Width = 430;
            Height = requireConfirmation ? 300 : 250;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(16, 12, 16, 4),
                Text = title,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ForeColor
            };

            var description = new Label
            {
                Dock = DockStyle.Top,
                Height = 55,
                Padding = new Padding(16, 0, 16, 6),
                Text = message,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };

            var fields = new Panel { Dock = DockStyle.Top, Height = requireConfirmation ? 105 : 62, Padding = new Padding(16, 0, 16, 0) };
            _password = MakePasswordBox("Master password");
            _password.Dock = DockStyle.Top;
            fields.Controls.Add(_password);

            if (requireConfirmation)
            {
                _confirm = MakePasswordBox("Confirm password");
                _confirm.Dock = DockStyle.Top;
                fields.Controls.Add(_confirm);
                _confirm.BringToFront();
            }

            var show = new CheckBox
            {
                Text = "Show password",
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(16, 0, 16, 0),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ForeColor,
                BackColor = Color.Transparent
            };
            show.CheckedChanged += (_, _) =>
            {
                _password.UseSystemPasswordChar = !show.Checked;
                if (_confirm != null) _confirm.UseSystemPasswordChar = !show.Checked;
            };

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(16, 8, 16, 8),
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249)
            };
            var ok = MakeButton(requireConfirmation ? "Set password" : "Unlock", true);
            ok.Dock = DockStyle.Right;
            ok.Width = 130;
            ok.Click += (_, _) => Accept();
            var cancel = MakeButton("Cancel", false);
            cancel.Dock = DockStyle.Right;
            cancel.Width = 90;
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);

            Controls.Add(show);
            Controls.Add(fields);
            Controls.Add(description);
            Controls.Add(bottom);
            Controls.Add(header);

            AcceptButton = ok;
            CancelButton = cancel;

            UiStyler.ApplyForm(this);

            Shown += (_, _) => _password.Focus();
        }

        private TextBox MakePasswordBox(string placeholder) => new()
        {
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg,
            ForeColor = _isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary,
            PlaceholderText = placeholder,
            Margin = new Padding(0, 0, 0, 8),
            Height = 30
        };

        private Button MakeButton(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Height = 34,
                BackColor = primary
                    ? (_isDark ? ModernPalette.DarkAccent : ModernPalette.LightAccent)
                    : (_isDark ? Color.FromArgb(45, 45, 52) : Color.White),
                ForeColor = primary ? Color.White : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary)
            };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = _isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            return button;
        }

        private void Accept()
        {
            if (_requireConfirmation)
            {
                if (_password.Text.Length < 8)
                {
                    ThemedMessageBox.Show(this, "Use at least 8 characters.", "Master password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_confirm == null || _password.Text != _confirm.Text)
                {
                    ThemedMessageBox.Show(this, "The passwords do not match.", "Master password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (string.IsNullOrEmpty(_password.Text))
            {
                ThemedMessageBox.Show(this, "Enter the master password.", "Unlock vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

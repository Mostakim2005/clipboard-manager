using ClipboardApp.WinForms.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    /// <summary>
    /// Styled replacement for MessageBox. Native MessageBox ignores the app's
    /// theme entirely; this dialog keeps the same button semantics while using
    /// the active Fluent/Acrylic/Classic style.
    /// </summary>
    public sealed class MessageDialog : Form
    {
        private readonly Label _body;

        public MessageDialog(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            AppSettings? settings = null)
        {
            settings ??= UiStyler.CurrentSettings;

            Text = string.IsNullOrWhiteSpace(caption) ? "Clipboard App" : caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            var font = new Font("Segoe UI", 9.5f);
            Font = font;

            int width = 470;
            var measured = TextRenderer.MeasureText(
                text ?? string.Empty,
                font,
                new Size(width - 92, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int bodyHeight = Math.Max(76, Math.Min(300, measured.Height + 42));

            ClientSize = new Size(width, bodyHeight + 58);

            var iconBox = new PictureBox
            {
                Dock = DockStyle.Left,
                Width = 54,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = GetIcon(icon),
                Padding = new Padding(12, 12, 4, 12)
            };

            _body = new Label
            {
                Dock = DockStyle.Fill,
                Text = text ?? string.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 14, 18, 14),
                AutoEllipsis = false,
                UseMnemonic = false
            };

            var bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };
            bodyPanel.Controls.Add(_body);
            bodyPanel.Controls.Add(iconBox);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 14, 10)
            };

            AddButtons(buttonPanel, buttons, settings);

            Controls.Add(bodyPanel);
            Controls.Add(buttonPanel);

            UiStyler.ApplyForm(this, settings);
        }

        private void AddButtons(FlowLayoutPanel panel, MessageBoxButtons buttons, AppSettings settings)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    panel.Controls.Add(CreateButton("OK", DialogResult.OK, true, settings));
                    AcceptButton = panel.Controls[0] as Button;
                    break;

                case MessageBoxButtons.OKCancel:
                    panel.Controls.Add(CreateButton("Cancel", DialogResult.Cancel, false, settings));
                    panel.Controls.Add(CreateButton("OK", DialogResult.OK, true, settings));
                    AcceptButton = panel.Controls[1] as Button;
                    CancelButton = panel.Controls[0] as Button;
                    break;

                case MessageBoxButtons.YesNo:
                    panel.Controls.Add(CreateButton("No", DialogResult.No, false, settings));
                    panel.Controls.Add(CreateButton("Yes", DialogResult.Yes, true, settings));
                    AcceptButton = panel.Controls[1] as Button;
                    CancelButton = panel.Controls[0] as Button;
                    break;

                case MessageBoxButtons.YesNoCancel:
                    panel.Controls.Add(CreateButton("Cancel", DialogResult.Cancel, false, settings));
                    panel.Controls.Add(CreateButton("No", DialogResult.No, false, settings));
                    panel.Controls.Add(CreateButton("Yes", DialogResult.Yes, true, settings));
                    AcceptButton = panel.Controls[2] as Button;
                    CancelButton = panel.Controls[0] as Button;
                    break;

                case MessageBoxButtons.RetryCancel:
                    panel.Controls.Add(CreateButton("Cancel", DialogResult.Cancel, false, settings));
                    panel.Controls.Add(CreateButton("Retry", DialogResult.Retry, true, settings));
                    AcceptButton = panel.Controls[1] as Button;
                    CancelButton = panel.Controls[0] as Button;
                    break;

                case MessageBoxButtons.AbortRetryIgnore:
                    panel.Controls.Add(CreateButton("Ignore", DialogResult.Ignore, false, settings));
                    panel.Controls.Add(CreateButton("Retry", DialogResult.Retry, false, settings));
                    panel.Controls.Add(CreateButton("Abort", DialogResult.Abort, false, settings));
                    AcceptButton = panel.Controls[1] as Button;
                    break;
            }
        }

        private static Button CreateButton(
            string text,
            DialogResult result,
            bool primary,
            AppSettings settings)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                Width = 92,
                Height = 30,
                Margin = new Padding(6, 0, 0, 0),
                UseVisualStyleBackColor = false
            };
            UiStyler.StyleButton(button, primary, settings);
            return button;
        }

        private static Image? GetIcon(MessageBoxIcon icon) => icon switch
        {
            MessageBoxIcon.Error or MessageBoxIcon.Hand or MessageBoxIcon.Stop =>
                SystemIcons.Error.ToBitmap(),
            MessageBoxIcon.Warning or MessageBoxIcon.Exclamation =>
                SystemIcons.Warning.ToBitmap(),
            MessageBoxIcon.Question =>
                SystemIcons.Question.ToBitmap(),
            MessageBoxIcon.Information or MessageBoxIcon.Asterisk =>
                SystemIcons.Information.ToBitmap(),
            _ => null
        };
    }

    /// <summary>
    /// Drop-in replacement for MessageBox.Show used by app-owned UI. The
    /// startup crash handler in Program.cs deliberately keeps the native
    /// MessageBox because the UI stack may not be healthy there.
    /// </summary>
    public static class ThemedMessageBox
    {
        public static DialogResult Show(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            using var dialog = new MessageDialog(text, caption, buttons, icon, UiStyler.CurrentSettings);
            return dialog.ShowDialog();
        }

        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            using var dialog = new MessageDialog(text, caption, buttons, icon, UiStyler.CurrentSettings);
            return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        }
    }
}

using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public sealed class ApiKeyEditDialog : Form
    {
        private readonly ApiKey _key;
        private readonly bool _isDark;
        private readonly TextBox _service;
        private readonly TextBox _name;
        private readonly TextBox _secret;
        private readonly TextBox _tags;
        private readonly TokenAutoComplete? _tagsAutoComplete;

        public ApiKey Key => _key;
        public event EventHandler<string>? CopyRequested;

        public ApiKeyEditDialog(ApiKey key, bool isDark, IEnumerable<string>? knownTags = null)
            : this(key, isDark, null, null, knownTags)
        {
        }

        public ApiKeyEditDialog(
            ApiKey key,
            bool isDark,
            IEnumerable<string>? knownProviders,
            IEnumerable<string>? knownNames,
            IEnumerable<string>? knownTags)
        {
            _key = key;
            _isDark = isDark;

            Text = string.IsNullOrWhiteSpace(key.Name) ? "Add API Key" : "Edit API Key";
            Width = 620;
            Height = 420;
            MinimumSize = new Size(560, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;

            // Two-column TableLayoutPanel ensures label is strictly beside the input
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(24, 20, 24, 12),
                AutoScroll = true
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

            // Row 0: Provider / Origin
            table.Controls.Add(CreateFieldLabel("Provider:"), 0, 0);
            _service = CreateInputTextBox(key.Service, "e.g. DeepSeek, OpenAI, Anthropic, Google, Stripe", knownProviders);
            table.Controls.Add(_service, 1, 0);

            // Row 1: Name / Use Case
            table.Controls.Add(CreateFieldLabel("Name / Use Case:"), 0, 1);
            _name = CreateInputTextBox(key.Name, "e.g. DeepTutor, Personal Blog, Prod Agent", knownNames);
            table.Controls.Add(_name, 1, 1);

            // Row 2: API Key Secret + Reveal + One-Tap Copy
            table.Controls.Add(CreateFieldLabel("API Key:"), 0, 2);
            var secretPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 8),
                Height = 32
            };

            _secret = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg,
                ForeColor = ForeColor,
                Text = key.Secret,
                PlaceholderText = "Paste secret API key value here..."
            };

            var btnCopy = new Button
            {
                Dock = DockStyle.Right,
                Width = 84,
                Height = 30,
                Text = "📋 Copy",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 0, 0),
                BackColor = isDark ? Color.FromArgb(45, 45, 52) : Color.White,
                ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            btnCopy.FlatAppearance.BorderSize = 1;
            btnCopy.FlatAppearance.BorderColor = isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            btnCopy.Click += (_, _) => CopySecretToClipboard(btnCopy);

            var btnReveal = new Button
            {
                Dock = DockStyle.Right,
                Width = 44,
                Height = 30,
                Text = "👁️",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 4, 0),
                BackColor = isDark ? Color.FromArgb(45, 45, 52) : Color.White,
                ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            btnReveal.FlatAppearance.BorderSize = 1;
            btnReveal.FlatAppearance.BorderColor = isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            btnReveal.Click += (_, _) =>
            {
                _secret.UseSystemPasswordChar = !_secret.UseSystemPasswordChar;
                btnReveal.Text = _secret.UseSystemPasswordChar ? "👁️" : "🙈";
            };

            secretPanel.Controls.Add(_secret);
            secretPanel.Controls.Add(btnReveal);
            secretPanel.Controls.Add(btnCopy);
            table.Controls.Add(secretPanel, 1, 2);

            // Row 3: Tag / Comment
            table.Controls.Add(CreateFieldLabel("Tag / Comment:"), 0, 3);
            string initialTags = key.Tags != null && key.Tags.Count > 0
                ? string.Join(", ", key.Tags)
                : key.Notes;
            _tags = CreateInputTextBox(initialTags, "e.g. llm, personal, payments, dev", knownTags);
            table.Controls.Add(_tags, 1, 3);

            // Security Hint Banner
            var hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                Padding = new Padding(24, 4, 24, 4),
                Text = "🔒 AES-256-GCM encrypted • DPAPI protected • Zero memory on exit • Never enters history",
                Font = new Font("Segoe UI", 8f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Bottom Actions Panel
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(24, 10, 24, 10),
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249)
            };
            var save = MakeActionButton("Save key", true);
            save.Dock = DockStyle.Right;
            save.Width = 105;
            save.Click += (_, _) => SaveAndClose();

            var cancel = MakeActionButton("Cancel", false);
            cancel.Dock = DockStyle.Right;
            cancel.Width = 90;
            cancel.Margin = new Padding(0, 0, 8, 0);
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            bottom.Controls.Add(save);
            bottom.Controls.Add(cancel);

            Controls.Add(table);
            Controls.Add(hint);
            Controls.Add(bottom);
            AcceptButton = save;
            CancelButton = cancel;

            var tagSuggestions = (knownTags ?? Enumerable.Empty<string>()).ToList();
            if (tagSuggestions.Count > 0)
            {
                _tagsAutoComplete = new TokenAutoComplete(_tags, () => tagSuggestions, ',', ';');
            }

            UiStyler.ApplyForm(this);
            Shown += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_service.Text)) _service.Focus();
                else if (string.IsNullOrWhiteSpace(_name.Text)) _name.Focus();
                else _secret.Focus();
            };
        }

        private Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = _isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                Padding = new Padding(0, 0, 12, 16)
            };
        }

        private TextBox CreateInputTextBox(string value, string placeholder, IEnumerable<string>? suggestions)
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg,
                ForeColor = ForeColor,
                Text = value,
                PlaceholderText = placeholder,
                Margin = new Padding(0, 4, 0, 16)
            };

            var list = suggestions?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (list != null && list.Count > 0)
            {
                box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                box.AutoCompleteSource = AutoCompleteSource.CustomSource;
                var ac = new AutoCompleteStringCollection();
                ac.AddRange(list.ToArray());
                box.AutoCompleteCustomSource = ac;
            }

            return box;
        }

        private void CopySecretToClipboard(Button btnCopy)
        {
            string secret = _secret.Text;
            if (string.IsNullOrEmpty(secret))
            {
                ThemedMessageBox.Show(this, "The secret field is empty.", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Clipboard.SetText(secret);
                CopyRequested?.Invoke(this, secret);

                btnCopy.Text = "✓ Copied!";
                btnCopy.BackColor = Color.FromArgb(16, 185, 129);
                btnCopy.ForeColor = Color.White;

                var timer = new System.Windows.Forms.Timer { Interval = 1500 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (!btnCopy.IsDisposed)
                    {
                        btnCopy.Text = "📋 Copy";
                        btnCopy.BackColor = _isDark ? Color.FromArgb(45, 45, 52) : Color.White;
                        btnCopy.ForeColor = _isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(this, "Could not copy to clipboard: " + ex.Message, "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Button MakeActionButton(string text, bool primary)
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

        private void SaveAndClose()
        {
            if (string.IsNullOrWhiteSpace(_name.Text) && string.IsNullOrWhiteSpace(_service.Text))
            {
                ThemedMessageBox.Show(this, "Give the key a provider or name/use case.", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _name.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_secret.Text))
            {
                ThemedMessageBox.Show(this, "The secret value cannot be empty.", "API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _secret.Focus();
                return;
            }

            _key.Service = _service.Text.Trim();
            _key.Name = string.IsNullOrWhiteSpace(_name.Text) ? _service.Text.Trim() : _name.Text.Trim();
            _key.Secret = _secret.Text;
            _key.Notes = _tags.Text.Trim();
            _key.Tags = _tags.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _key.UpdatedAt = DateTime.Now;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tagsAutoComplete?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

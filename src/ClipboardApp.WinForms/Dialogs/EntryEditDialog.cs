using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public class EntryEditDialog : Form
    {
        public ClipboardEntry Entry { get; }
        private readonly AppSettings _settings;
        private readonly TextBox _aliasBox;
        private readonly TextBox _tagsBox;
        private readonly TextBox _contentBox;
        private readonly TokenAutoComplete _aliasAutoComplete;
        private readonly TokenAutoComplete _tagsAutoComplete;

        public EntryEditDialog(
            ClipboardEntry entry,
            AppSettings settings,
            IEnumerable<string>? knownTags = null,
            IEnumerable<string>? knownAliases = null)
        {
            Entry = entry;
            _settings = settings;

            Text = "Edit Entry — ClipboardApp";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            // Apply size preset
            int initWidth = _settings.EditWindowWidth > 0 ? _settings.EditWindowWidth : 680;
            int initHeight = _settings.EditWindowHeight > 0 ? _settings.EditWindowHeight : 520;
            if (_settings.EditWindowPreset == "Large")
            {
                initWidth = Math.Max(initWidth, 840);
                initHeight = Math.Max(initHeight, 640);
            }
            Size = new Size(initWidth, initHeight);
            MinimumSize = new Size(480, 360);

            bool isDark = _settings.Theme == "Dark" || (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // DWM Rounded corners
            ModernPalette.ApplyDwmStyling(Handle, isDark);

            // Top Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249),
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblHeaderTitle = new Label
            {
                Text = $"Editing {entry.Format.ToUpperInvariant()} Entry",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = isDark ? Color.White : Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(20, 10)
            };

            var lblHeaderMeta = new Label
            {
                Text = $"Source: {entry.SourceApp}  •  Created: {entry.CreatedAt:g}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                AutoSize = true,
                Location = new Point(20, 33)
            };
            headerPanel.Controls.AddRange(new Control[] { lblHeaderTitle, lblHeaderMeta });

            // Main Editor Form Layout
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16)
            };

            // Intelligent name suggestion for image / file entries
            string initialAlias = entry.Alias;
            if (string.IsNullOrWhiteSpace(initialAlias) && entry.Format == "image")
            {
                initialAlias = $"Image_{entry.SourceApp}_{entry.CreatedAt:yyyyMMdd_HHmm}";
            }

            // Alias / Multiple Aliases
            var lblAlias = new Label
            {
                Text = "Aliases (comma-separated, e.g. snippet, email, query):",
                Location = new Point(20, 16),
                AutoSize = true,
                ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            _aliasBox = new TextBox
            {
                Location = new Point(20, 40),
                Width = ClientSize.Width - 40,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                ForeColor = isDark ? Color.White : Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Text = initialAlias
            };

            // Tags & Groups
            var lblTags = new Label
            {
                Text = "Tags & Groups (comma-separated, e.g. work, reference, code):",
                Location = new Point(20, 75),
                AutoSize = true,
                ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            _tagsBox = new TextBox
            {
                Location = new Point(20, 99),
                Width = ClientSize.Width - 40,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                ForeColor = isDark ? Color.White : Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Text = entry.Tags != null ? string.Join(", ", entry.Tags) : ""
            };

            // Content Box
            var lblContent = new Label
            {
                Text = "Content:",
                Location = new Point(20, 134),
                AutoSize = true,
                ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            _contentBox = new TextBox
            {
                Location = new Point(20, 158),
                Width = ClientSize.Width - 40,
                Height = ClientSize.Height - 270,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BackColor = isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                ForeColor = isDark ? Color.White : Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f),
                Text = entry.Content
            };

            mainContainer.Controls.AddRange(new Control[]
            {
                lblAlias, _aliasBox,
                lblTags, _tagsBox,
                lblContent, _contentBox
            });

            // Bottom Buttons Panel
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249),
                Padding = new Padding(20, 10, 20, 10)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = isDark ? ModernPalette.DarkPillBg : ModernPalette.LightPillBg,
                ForeColor = isDark ? Color.White : Color.Black
            };
            btnCancel.FlatAppearance.BorderColor = isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;

            var spacer = new Panel { Dock = DockStyle.Right, Width = 10 };

            var btnOk = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;

            bottomBar.Controls.AddRange(new Control[] { btnOk, spacer, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { mainContainer, bottomBar, headerPanel });

            var tagSuggestions = (knownTags ?? Enumerable.Empty<string>()).ToList();
            var aliasSuggestions = (knownAliases ?? Enumerable.Empty<string>()).ToList();
            _tagsAutoComplete = new TokenAutoComplete(_tagsBox, () => tagSuggestions, ',', ';');
            _aliasAutoComplete = new TokenAutoComplete(_aliasBox, () => aliasSuggestions, ',', ';');

            UiStyler.ApplyForm(this, _settings);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tagsAutoComplete?.Dispose();
                _aliasAutoComplete?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Entry.Alias = _aliasBox.Text.Trim();
                Entry.Content = _contentBox.Text;

                // Parse tags
                var tags = _tagsBox.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().TrimStart('#'))
                    .Where(t => t.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                Entry.Tags = tags;

                // Save window dimensions if preset is Remember
                if (_settings.EditWindowPreset == "Remember")
                {
                    _settings.EditWindowWidth = Width;
                    _settings.EditWindowHeight = Height;
                    _settings.Save();
                }
            }
            base.OnFormClosing(e);
        }
    }
}

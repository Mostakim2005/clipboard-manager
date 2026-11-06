using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using ClipboardApp.Core.Storage;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    /// <summary>
    /// Encrypted API-key manager. Values are masked and copy-only; the dialog
    /// never sends them to clipboard history or logs.
    /// </summary>
    public sealed class ApiVaultDialog : Form
    {
        private readonly ApiKeyRepository _repo;
        private readonly Core.Security.ApiVaultService _vault;
        private readonly AppSettings _settings;
        private readonly bool _isDark;
        private readonly ListView _list;
        private readonly TextBox _search;
        private readonly Label _status;
        private readonly Label _detail;
        private readonly ComboBox _copyFormat;
        private readonly System.Windows.Forms.Timer _lockTimer;
        private List<ApiKey> _keys = new();

        public event EventHandler<string>? CopyRequested;
        public event EventHandler? VaultLocked;

        public ApiVaultDialog(ApiKeyRepository repo, Core.Security.ApiVaultService vault, AppSettings settings, bool isDark)
        {
            _repo = repo;
            _vault = vault;
            _settings = settings;
            _isDark = isDark;

            Text = "API Vault — " + AppVersion.Display;
            Width = 860;
            Height = 600;
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = true;
            TopMost = false;
            // Modeless: the vault can stay open and movable while the main list
            // keeps working behind it. It is not a blocking dialog.
            ShowIcon = true;
            ShowInTaskbar = true;
            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(16, 12, 16, 4),
                Text = "🔐 API Vault",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = ForeColor
            };

            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(16, 4, 16, 4)
            };
            _search = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Search by name, service, environment or tag...",
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg,
                ForeColor = ForeColor
            };
            _search.TextChanged += (_, _) => LoadKeys();
            searchPanel.Controls.Add(_search);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                CheckBoxes = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                ForeColor = ForeColor
            };
            _list.Columns.Add("Name", 220, HorizontalAlignment.Left);
            _list.Columns.Add("Service", 150, HorizontalAlignment.Left);
            _list.Columns.Add("Environment", 100, HorizontalAlignment.Left);
            _list.Columns.Add("Updated", 140, HorizontalAlignment.Left);
            _list.SelectedIndexChanged += (_, _) => UpdateDetail();
            _list.ItemChecked += (_, _) => UpdateStatus();
            _list.KeyDown += OnListKeyDown;
            _list.MouseDoubleClick += (_, _) => EditSelected();

            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 2, 16, 6) };
            listHost.Controls.Add(_list);

            _detail = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(16, 4, 16, 4),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                Text = "Secrets are masked. Copying a key never adds it to clipboard history."
            };

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(16, 2, 16, 2),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };
            // Live unlock-window countdown without a full reload.
            var unlockTicker = new System.Windows.Forms.Timer { Interval = 10_000 };
            unlockTicker.Tick += (_, _) => UpdateStatus();
            unlockTicker.Start();
            Disposed += (_, _) => unlockTicker.Dispose();

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(16, 8, 16, 8),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249)
            };

            actions.Controls.Add(MakeButton("Add key", true, AddKey));
            actions.Controls.Add(MakeButton("Edit", false, EditSelected));
            actions.Controls.Add(MakeButton("Delete", false, DeleteSelected));
            actions.Controls.Add(MakeButton("Copy selected", true, () => CopySelected(_copyFormat!.Text)));
            _copyFormat = new ComboBox
            {
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5f)
            };
            _copyFormat.Items.AddRange(new object[] { "Plain", "Env", "Json", "Csv" });
            _copyFormat.SelectedItem = _settings.BatchCopyFormat;
            if (_copyFormat.SelectedIndex < 0) _copyFormat.SelectedIndex = 0;
            actions.Controls.Add(_copyFormat);
            actions.Controls.Add(MakeButton("Lock", false, LockVault));
            actions.Controls.Add(MakeButton(_vault.IsPasswordProtected ? "Password" : "Set password", false, ManagePassword));
            actions.Controls.Add(MakeButton("Close", false, Close));

            var menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Copy selected (merged)", null, (_, _) => CopySelected(_copyFormat!.Text));
            menu.Items.Add("Copy as .env", null, (_, _) => CopySelected("Env"));
            menu.Items.Add("Copy as JSON", null, (_, _) => CopySelected("Json"));
            menu.Items.Add("Copy as CSV", null, (_, _) => CopySelected("Csv"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Edit...", null, (_, _) => EditSelected());
            menu.Items.Add("Delete", null, (_, _) => DeleteSelected());
            _list.ContextMenuStrip = menu;

            Controls.Add(listHost);
            Controls.Add(_detail);
            Controls.Add(_status);
            Controls.Add(actions);
            Controls.Add(searchPanel);
            Controls.Add(header);

            _lockTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _lockTimer.Tick += (_, _) =>
            {
                if (_vault.LockIfIdle(_settings.ApiVaultAutoLockMinutes))
                {
                    VaultLocked?.Invoke(this, EventArgs.Empty);
                    Close();
                }
                else
                {
                    UpdateStatus();
                }
            };
            _lockTimer.Start();

            Shown += (_, _) =>
            {
                LoadKeys();
                // Opening the window counts as use: refresh the timed unlock
                // window so a key can be copied without re-entering the password.
                _vault.ExtendUnlockWindow(_settings.ApiVaultAutoLockMinutes);
                UpdateStatus();
            };

            FormClosed += (_, _) =>
            {
                _lockTimer.Stop();
                _lockTimer.Dispose();
                foreach (var key in _keys) key.Secret = "";
            };

            UiStyler.ApplyForm(this, _settings);
        }

        private Button MakeButton(string text, bool primary, Action action)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 32,
                Padding = new Padding(8, 2, 8, 2),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = primary
                    ? (_isDark ? ModernPalette.DarkAccent : ModernPalette.LightAccent)
                    : (_isDark ? Color.FromArgb(45, 45, 52) : Color.White),
                ForeColor = primary ? Color.White : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary)
            };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = _isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            button.Click += (_, _) => action();
            return button;
        }

        private void LoadKeys()
        {
            try
            {
                var checkedIds = new HashSet<string>(_keys.Where(k => IsChecked(k.Id)).Select(k => k.Id));
                string query = _search.Text.Trim();
                _keys = _repo.GetAll().ToList();
                var filtered = string.IsNullOrEmpty(query)
                    ? _keys
                    : _keys.Where(k =>
                        k.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        k.Service.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        k.Environment.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        k.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

                _list.BeginUpdate();
                try
                {
                    _list.Items.Clear();
                    foreach (var key in filtered)
                    {
                        var item = new ListViewItem(key.DisplayName) { Tag = key, Checked = checkedIds.Contains(key.Id) };
                        item.SubItems.Add(string.IsNullOrWhiteSpace(key.Service) ? "—" : key.Service);
                        item.SubItems.Add(string.IsNullOrWhiteSpace(key.Environment) ? "—" : key.Environment);
                        item.SubItems.Add(key.UpdatedAt.ToString("MMM d, HH:mm"));
                        if (key.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                        {
                            item.ForeColor = _isDark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(185, 28, 28);
                        }
                        _list.Items.Add(item);
                    }
                }
                finally
                {
                    _list.EndUpdate();
                }

                if (_list.Items.Count > 0 && _list.SelectedItems.Count == 0)
                {
                    _list.Items[0].Selected = true;
                }
                UpdateStatus();
                UpdateDetail();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(this, "The vault could not be read:\n\n" + ex.Message, "API Vault",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsChecked(string id)
        {
            foreach (ListViewItem item in _list.Items)
            {
                if (item.Tag is ApiKey key && key.Id == id) return item.Checked;
            }
            return false;
        }

        private List<ApiKey> SelectedKeys()
        {
            var checkedKeys = _list.CheckedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as ApiKey)
                .Where(k => k != null)
                .Cast<ApiKey>()
                .ToList();
            if (checkedKeys.Count > 0) return checkedKeys;

            return _list.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as ApiKey)
                .Where(k => k != null)
                .Cast<ApiKey>()
                .ToList();
        }

        private void UpdateStatus()
        {
            int checkedCount = _list.CheckedItems.Count;
            int selectedCount = _list.SelectedItems.Count;
            string selection = checkedCount > 0
                ? $"{checkedCount} key{(checkedCount == 1 ? "" : "s")} checked"
                : selectedCount > 0
                    ? $"{selectedCount} key{(selectedCount == 1 ? "" : "s")} selected"
                    : $"{_list.Items.Count} key{(_list.Items.Count == 1 ? "" : "s")} in vault";

            int seconds = _vault.UnlockSecondsRemaining;
            if (seconds > 0)
            {
                int minutes = Math.Max(1, (int)Math.Ceiling(seconds / 60.0));
                selection += $"   •   unlocked for {minutes} more minute{(minutes == 1 ? "" : "s")}";
            }
            else if (_vault.IsPasswordProtected)
            {
                selection += "   •   locked (re-open the vault to unlock)";
            }

            _status.Text = selection;
        }

        private void UpdateDetail()
        {
            if (_list.SelectedItems.Count == 0)
            {
                _detail.Text = "Secrets are masked. Copying a key never adds it to clipboard history.";
                return;
            }
            if (_list.SelectedItems[0].Tag is not ApiKey key) return;
            string tags = key.Tags.Count > 0 ? " • " + string.Join(", ", key.Tags) : "";
            _detail.Text = $"{key.Service} • {key.Environment} • copied {key.CopyCount}×{tags}\n" +
                           (string.IsNullOrWhiteSpace(key.Notes) ? "No notes." : key.Notes);
        }

        private void AddKey()
        {
            var key = new ApiKey();
            var knownProviders = _keys.Select(k => k.Service).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var knownNames = _keys.Select(k => k.Name).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var knownTags = _keys.SelectMany(k => k.Tags ?? new List<string>()).Concat(_keys.Select(k => k.Notes)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            using var dialog = new ApiKeyEditDialog(key, _isDark, knownProviders, knownNames, knownTags) { Owner = this };
            dialog.CopyRequested += (_, secret) => CopyRequested?.Invoke(this, secret);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _repo.Upsert(dialog.Key);
                LoadKeys();
                ShowStatus("Key encrypted and saved.");
            }
        }

        private void EditSelected()
        {
            if (_list.SelectedItems.Count == 0) return;
            if (_list.SelectedItems[0].Tag is not ApiKey key) return;
            var knownProviders = _keys.Select(k => k.Service).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var knownNames = _keys.Select(k => k.Name).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var knownTags = _keys.SelectMany(k => k.Tags ?? new List<string>()).Concat(_keys.Select(k => k.Notes)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            using var dialog = new ApiKeyEditDialog(key, _isDark, knownProviders, knownNames, knownTags) { Owner = this };
            dialog.CopyRequested += (_, secret) => CopyRequested?.Invoke(this, secret);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _repo.Upsert(dialog.Key);
                LoadKeys();
                ShowStatus("Key updated.");
            }
        }

        private void DeleteSelected()
        {
            var keys = SelectedKeys();
            if (keys.Count == 0) return;
            string prompt = keys.Count == 1
                ? $"Delete “{keys[0].DisplayName}”? This cannot be undone."
                : $"Delete {keys.Count} API keys? This cannot be undone.";
            if (ThemedMessageBox.Show(this, prompt, "Delete API keys", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            foreach (var key in keys) _repo.Delete(key.Id);
            LoadKeys();
            ShowStatus($"{keys.Count} key{(keys.Count == 1 ? "" : "s")} deleted.");
        }

        private void CopySelected(string format)
        {
            var keys = SelectedKeys();
            if (keys.Count == 0)
            {
                ShowStatus("Select or check at least one key.");
                return;
            }

            string normalized = BatchCopyService.NormalizeFormat(format);
            string payload = BatchCopyService.BuildApiKeys(keys.Select(k => (k.Name, k.Secret)), normalized);
            CopyRequested?.Invoke(this, payload);
            _repo.RecordCopy(keys.Select(k => k.Id));
            _vault.Touch();
            ShowStatus(keys.Count == 1
                ? "Key copied. Clipboard will be cleared automatically."
                : $"{keys.Count} keys copied as one {normalized.ToUpperInvariant()} payload.");
            LoadKeys();
        }

        private void OnListKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in _list.Items) item.Checked = true;
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelected(_copyFormat!.Text);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
        }

        private void LockVault()
        {
            _vault.Lock();
            VaultLocked?.Invoke(this, EventArgs.Empty);
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ManagePassword()
        {
            if (!_vault.IsPasswordProtected)
            {
                using var set = new MasterPasswordDialog(
                    "Set master password",
                    "This password wraps the vault key. Without it, Windows DPAPI protects the vault for your Windows account.",
                    requireConfirmation: true,
                    _isDark)
                { Owner = this };
                if (set.ShowDialog(this) != DialogResult.OK) return;
                if (!_vault.SetMasterPassword(set.Password))
                {
                    ThemedMessageBox.Show(this, "The master password could not be set.", "API Vault",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                ShowStatus("Master password enabled. The vault will lock when idle.");
                return;
            }

            var choice = ThemedMessageBox.Show(this,
                "Yes = change the master password\nNo = remove the master password\nCancel = keep it",
                "Master password", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return;
            if (choice == DialogResult.No)
            {
                if (_vault.RemoveMasterPassword()) ShowStatus("Master password removed. Windows DPAPI still protects the vault.");
                return;
            }

            using var change = new MasterPasswordDialog(
                "Change master password",
                "Enter a new master password for the vault key.",
                requireConfirmation: true,
                _isDark)
            { Owner = this };
            if (change.ShowDialog(this) == DialogResult.OK && _vault.SetMasterPassword(change.Password))
            {
                ShowStatus("Master password changed.");
            }
        }

        private void ShowStatus(string text) => _status.Text = text;
    }
}



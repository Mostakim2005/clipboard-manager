using ClipboardApp.Core.Interop;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    /// <summary>
    /// Manual target chooser used when Windows refuses to activate the
    /// automatically detected window. Only window metadata is shown; clipboard
    /// content is never displayed here.
    /// </summary>
    public sealed class TargetPickerDialog : Form
    {
        private readonly ListView _list;
        private readonly Label _detail;
        private readonly bool _isDark;
        private List<TargetWindowInfo> _targets;

        public IntPtr SelectedTarget { get; private set; } = IntPtr.Zero;

        public TargetPickerDialog(IReadOnlyList<TargetWindowInfo> targets, string? failureMessage, bool isDark)
        {
            _isDark = isDark;
            _targets = targets?.ToList() ?? new List<TargetWindowInfo>();

            Text = "Choose paste target";
            Width = 620;
            Height = 430;
            MinimumSize = new Size(520, 340);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;

            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(16, 12, 16, 4),
                Text = "Choose the window that should receive the paste",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ForeColor
            };

            var subtitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(16, 0, 16, 6),
                Text = string.IsNullOrWhiteSpace(failureMessage)
                    ? "The clip is on the clipboard. Pick a target and ClipboardApp will activate it before sending Ctrl+V."
                    : failureMessage,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                ForeColor = ForeColor
            };
            _list.Columns.Add("Window", 360, HorizontalAlignment.Left);
            _list.Columns.Add("App", 130, HorizontalAlignment.Left);
            _list.Columns.Add("State", 90, HorizontalAlignment.Left);
            _list.DoubleClick += (_, _) => AcceptSelection();

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 4, 16, 8)
            };
            listHost.Controls.Add(_list);

            _detail = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Padding = new Padding(16, 2, 16, 4),
                Font = new Font("Segoe UI", 8f),
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                Text = "Tip: use Alt+Tab to focus the exact editor window, then reopen ClipboardApp."
            };

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(16, 8, 16, 8),
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249)
            };

            var pasteButton = MakeButton("Paste into this window", true);
            pasteButton.Dock = DockStyle.Right;
            pasteButton.Width = 180;
            pasteButton.Click += (_, _) => AcceptSelection();

            var copyOnlyButton = MakeButton("Copy only", false);
            copyOnlyButton.Dock = DockStyle.Right;
            copyOnlyButton.Width = 105;
            copyOnlyButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var refreshButton = MakeButton("Refresh", false);
            refreshButton.Dock = DockStyle.Left;
            refreshButton.Width = 90;
            refreshButton.Click += (_, _) => ReloadTargets();

            bottom.Controls.Add(pasteButton);
            bottom.Controls.Add(copyOnlyButton);
            bottom.Controls.Add(refreshButton);

            Controls.Add(listHost);
            Controls.Add(_detail);
            Controls.Add(bottom);
            Controls.Add(subtitle);
            Controls.Add(header);

            AcceptButton = pasteButton;
            CancelButton = copyOnlyButton;

            UiStyler.ApplyForm(this);

            Load += (_, _) => ReloadTargets();
        }

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
                ForeColor = primary
                    ? Color.White
                    : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary)
            };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = _isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            return button;
        }

        private void ReloadTargets()
        {
            var selected = SelectedTarget;
            _targets = TargetWindowTracker.Instance.GetCandidates(IntPtr.Zero).ToList();
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (var target in _targets)
                {
                    var item = new ListViewItem(target.DisplayName) { Tag = target };
                    item.SubItems.Add(string.IsNullOrWhiteSpace(target.ProcessName) ? "—" : target.ProcessName);
                    item.SubItems.Add(target.IsElevated ? "admin" : target.IsMinimized ? "minimized" : "visible");
                    if (target.IsElevated) item.ForeColor = Color.FromArgb(245, 158, 11);
                    _list.Items.Add(item);
                    if (target.Handle == selected) item.Selected = true;
                }
            }
            finally
            {
                _list.EndUpdate();
            }

            if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
            }

            _detail.Text = _list.Items.Count == 0
                ? "No eligible top-level windows found. Focus the destination window, then press Refresh."
                : "Only the window is activated; the clipboard payload itself is not shown in this list.";
        }

        private void AcceptSelection()
        {
            if (_list.SelectedItems.Count == 0) return;
            if (_list.SelectedItems[0].Tag is TargetWindowInfo info)
            {
                SelectedTarget = info.Handle;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}

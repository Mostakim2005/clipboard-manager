using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public sealed class ContextMenuCustomizeDialog : Form
    {
        private sealed class Row
        {
            public string Key { get; init; } = "";
            public bool Visible { get; set; }
        }

        private readonly AppSettings _settings;
        private readonly CheckedListBox _list;
        private readonly Label _description;
        private readonly Button _up;
        private readonly Button _down;
        private readonly List<Row> _rows = new();
        private bool _suppress;

        public List<string> Order { get; private set; } = new();
        public List<string> Hidden { get; private set; } = new();

        public ContextMenuCustomizeDialog(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Text = "Customize Right-Click Menu";
            Width = 560;
            Height = 520;
            MinimumSize = new Size(460, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(16, 12, 16, 4),
                Text = "Check the commands you want to see. Use Up/Down to reorder. " +
                       "\"Customize this menu...\" always stays available.",
                Font = new Font("Segoe UI", 9f)
            };

            _list = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(6)
            };
            _list.ItemCheck += (_, e) =>
            {
                if (_suppress) return;
                if (e.Index >= 0 && e.Index < _rows.Count)
                {
                    _rows[e.Index].Visible = e.NewValue == CheckState.Checked;
                }
            };
            _list.SelectedIndexChanged += (_, _) =>
            {
                UpdateDescription();
                UpdateButtons();
            };

            var side = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 116,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 8, 8, 8)
            };

            _up = CreateSideButton("Move Up", MoveUp);
            _down = CreateSideButton("Move Down", MoveDown);
            var showAll = CreateSideButton("Show All", ShowAll);
            var reset = CreateSideButton("Reset Order", ResetOrder);
            side.Controls.Add(_up);
            side.Controls.Add(_down);
            side.Controls.Add(showAll);
            side.Controls.Add(reset);

            _description = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(16, 8, 16, 4),
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 14, 8)
            };

            var ok = new Button
            {
                Text = "Save",
                Width = 92,
                Height = 30,
                Margin = new Padding(6, 0, 0, 0)
            };
            ok.Click += (_, _) => SaveAndClose();

            var cancel = new Button
            {
                Text = "Cancel",
                Width = 92,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(6, 0, 0, 0)
            };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 0, 0)
            };
            listHost.Controls.Add(_list);
            listHost.Controls.Add(side);

            Controls.Add(listHost);
            Controls.Add(header);
            Controls.Add(_description);
            Controls.Add(buttons);

            LoadRows();
            UiStyler.ApplyForm(this, settings);
            UiStyler.StyleButton(ok, primary: true, settings);
        }

        private Button CreateSideButton(string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                Width = 98,
                Height = 28,
                Margin = new Padding(0, 0, 0, 6),
                UseVisualStyleBackColor = false
            };
            button.Click += (_, _) => action();
            return button;
        }

        private void LoadRows()
        {
            _rows.Clear();
            var hidden = new HashSet<string>(
                ContextMenuCommands.NormalizeHidden(_settings.ContextMenuHidden),
                StringComparer.OrdinalIgnoreCase);

            foreach (var key in ContextMenuCommands.NormalizeOrder(_settings.ContextMenuOrder))
            {
                if (string.Equals(key, ContextMenuCommands.CustomizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _rows.Add(new Row { Key = key, Visible = !hidden.Contains(key) });
            }

            RefreshList();
        }

        private void RefreshList()
        {
            _suppress = true;
            try
            {
                int selected = _list.SelectedIndex;
                _list.Items.Clear();
                foreach (var row in _rows)
                {
                    var command = ContextMenuCommands.Find(row.Key);
                    if (command == null) continue;
                    _list.Items.Add(command.Label, row.Visible);
                }

                if (_list.Items.Count > 0)
                {
                    _list.SelectedIndex = Math.Clamp(selected, 0, _list.Items.Count - 1);
                }
            }
            finally
            {
                _suppress = false;
            }

            UpdateButtons();
            UpdateDescription();
        }

        private void CaptureChecked()
        {
            for (int i = 0; i < _rows.Count && i < _list.Items.Count; i++)
            {
                _rows[i].Visible = _list.GetItemChecked(i);
            }
        }

        private void MoveUp()
        {
            int index = _list.SelectedIndex;
            if (index <= 0) return;
            CaptureChecked();
            (_rows[index - 1], _rows[index]) = (_rows[index], _rows[index - 1]);
            RefreshList();
            _list.SelectedIndex = index - 1;
        }

        private void MoveDown()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _rows.Count - 1) return;
            CaptureChecked();
            (_rows[index + 1], _rows[index]) = (_rows[index], _rows[index + 1]);
            RefreshList();
            _list.SelectedIndex = index + 1;
        }

        private void ShowAll()
        {
            CaptureChecked();
            foreach (var row in _rows) row.Visible = true;
            RefreshList();
        }

        private void ResetOrder()
        {
            _rows.Clear();
            foreach (var key in ContextMenuCommands.NormalizeOrder(null))
            {
                if (string.Equals(key, ContextMenuCommands.CustomizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                _rows.Add(new Row { Key = key, Visible = true });
            }
            RefreshList();
            _list.SelectedIndex = 0;
        }

        private void UpdateButtons()
        {
            int index = _list.SelectedIndex;
            _up.Enabled = index > 0;
            _down.Enabled = index >= 0 && index < _rows.Count - 1;
        }

        private void UpdateDescription()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _rows.Count)
            {
                _description.Text = "";
                return;
            }

            var command = ContextMenuCommands.Find(_rows[index].Key);
            _description.Text = command == null
                ? ""
                : command.Description + (_rows[index].Visible ? "" : "  (hidden)");
        }

        private void SaveAndClose()
        {
            CaptureChecked();
            Order = _rows.Select(r => r.Key).ToList();
            Order.Add(ContextMenuCommands.CustomizeKey);
            Hidden = _rows.Where(r => !r.Visible).Select(r => r.Key).ToList();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    public partial class MainForm
    {
        private void BuildFilterBar()
        {
            _filterBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Padding = new Padding(10, 2, 10, 2)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _filterDateCombo = CreateFilterCombo(112);
            _filterDateCombo.Items.AddRange(new object[]
            {
                "Any", "Today", "Yesterday", "Past 7 days", "Past 30 days", "Older"
            });
            _filterDateCombo.SelectedIndex = DateFilterIndex(_settings.FilterDate);
            _filterDateCombo.SelectedIndexChanged += (_, _) => OnFilterChanged();

            _filterTypeCombo = CreateFilterCombo(96);
            _filterTypeCombo.Items.AddRange(new object[]
            {
                "Any", "Text", "Image", "Link", "File", "Color"
            });
            int typeIndex = _filterTypeCombo.Items.IndexOf(_settings.FilterType);
            _filterTypeCombo.SelectedIndex = typeIndex >= 0 ? typeIndex : 0;
            _filterTypeCombo.SelectedIndexChanged += (_, _) => OnFilterChanged();

            _filterAppCombo = CreateFilterCombo(160);
            _filterAppCombo.Items.Add("Any");
            _filterAppCombo.SelectedIndex = 0;
            _filterAppCombo.SelectedIndexChanged += (_, _) => OnFilterChanged();

            _selectAllFilteredCheck = new CheckBox
            {
                Text = "Select all filtered",
                AutoSize = true,
                Visible = false,
                Margin = new Padding(12, 8, 4, 0),
                Font = new Font("Segoe UI", 8.5f)
            };
            _selectAllFilteredCheck.CheckedChanged += (_, _) => OnSelectAllFilteredChanged();

            _deleteFilteredButton = new Button
            {
                Text = "Delete filtered",
                Width = 108,
                Height = 26,
                Visible = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(6, 4, 4, 0),
                UseVisualStyleBackColor = false,
                ForeColor = Color.FromArgb(220, 38, 38)
            };
            _deleteFilteredButton.FlatAppearance.BorderSize = 0;
            _deleteFilteredButton.Click += (_, _) => DeleteFiltered();

            _clearFiltersButton = new Button
            {
                Text = "Clear filters",
                Width = 94,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(4, 4, 0, 0),
                UseVisualStyleBackColor = false
            };
            _clearFiltersButton.FlatAppearance.BorderSize = 0;
            _clearFiltersButton.Click += (_, _) => ClearFilters();

            flow.Controls.Add(CreateFilterLabel("Date"));
            flow.Controls.Add(_filterDateCombo);
            flow.Controls.Add(CreateFilterLabel("Type"));
            flow.Controls.Add(_filterTypeCombo);
            flow.Controls.Add(CreateFilterLabel("App"));
            flow.Controls.Add(_filterAppCombo);
            flow.Controls.Add(_selectAllFilteredCheck);
            flow.Controls.Add(_deleteFilteredButton);
            flow.Controls.Add(_clearFiltersButton);

            _filterBar.Controls.Add(flow);

            _toolTip?.SetToolTip(_filterDateCombo, "Always-available date filter. Combines with search and the type pills.");
            _toolTip?.SetToolTip(_filterTypeCombo, "Always-available type filter. Combines with search and the type pills.");
            _toolTip?.SetToolTip(_filterAppCombo, "Show only clips copied from one application. The list is learned from history.");
            _toolTip?.SetToolTip(_selectAllFilteredCheck, "Checks every item currently visible under the active filters.");
            _toolTip?.SetToolTip(_deleteFilteredButton, "Deletes every item currently visible under the active filters. Hidden checked items are not touched.");
            _toolTip?.SetToolTip(_clearFiltersButton, "Resets Date, Type, App, the type pill and the search box.");
        }

        private ComboBox CreateFilterCombo(int width) => new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = width,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 4, 10, 0)
        };

        private Label CreateFilterLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 8, 4, 0)
        };

        private void SetFilterTooltips()
        {
            if (_toolTip == null) return;

            _toolTip.SetToolTip(_filterDateCombo, "Always-available date filter. Combines with search and the type pills.");
            _toolTip.SetToolTip(_filterTypeCombo, "Always-available type filter. Combines with search and the type pills.");
            _toolTip.SetToolTip(_filterAppCombo, "Show only clips copied from one application. The list is learned from history.");
            _toolTip.SetToolTip(_selectAllFilteredCheck, "Checks every item currently visible under the active filters.");
            _toolTip.SetToolTip(_deleteFilteredButton, "Deletes every item currently visible under the active filters. Hidden checked items are not touched.");
            _toolTip.SetToolTip(_clearFiltersButton, "Resets Date, Type, App, the type pill and the search box.");
        }

        private void RefreshAppFilterOptions()
        {
            if (_filterAppCombo == null) return;

            string selected = _settings.FilterApp ?? "Any";
            var apps = _snapshot
                .Select(e => (e.SourceApp ?? "").Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressFilterEvents = true;
            try
            {
                _filterAppCombo.BeginUpdate();
                _filterAppCombo.Items.Clear();
                _filterAppCombo.Items.Add("Any");
                foreach (var app in apps) _filterAppCombo.Items.Add(app);

                int index = 0;
                for (int i = 0; i < _filterAppCombo.Items.Count; i++)
                {
                    if (string.Equals(
                        _filterAppCombo.Items[i]?.ToString(),
                        selected,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }

                _filterAppCombo.SelectedIndex = index;
                if (index == 0 && !string.Equals(selected, "Any", StringComparison.OrdinalIgnoreCase))
                {
                    _settings.FilterApp = "Any";
                }
                _filterAppCombo.EndUpdate();
            }
            finally
            {
                _suppressFilterEvents = false;
            }
        }

        private void OnFilterChanged()
        {
            if (_suppressFilterEvents) return;

            _settings.FilterDate = DateFilterValue(_filterDateCombo.SelectedIndex);
            _settings.FilterType = _filterTypeCombo.SelectedIndex switch
            {
                1 => "Text",
                2 => "Image",
                3 => "Link",
                4 => "File",
                5 => "Color",
                _ => "Any"
            };
            _settings.FilterApp = _filterAppCombo.SelectedItem?.ToString() ?? "Any";
            _settings.Save();
            RebuildList();
        }

        private void OnSelectAllFilteredChanged()
        {
            if (_suppressFilterEvents || _cardList == null) return;

            if (_selectAllFilteredCheck.Checked)
            {
                Accessor.SelectAllCards();
            }
            else
            {
                Accessor.ClearCheckedCards();
            }
        }

        private void ClearFilters()
        {
            _suppressFilterEvents = true;
            try
            {
                _settings.FilterDate = "Any";
                _settings.FilterType = "Any";
                _settings.FilterApp = "Any";

                _filterDateCombo.SelectedIndex = 0;
                _filterTypeCombo.SelectedIndex = 0;
                _filterAppCombo.SelectedIndex = 0;
                _pillBar?.SetSelectedFilter("All");
                if (_searchBox != null) _searchBox.Text = "";
            }
            finally
            {
                _suppressFilterEvents = false;
            }

            _settings.Save();
            RebuildList();
            ShowTransientStatus("Filters cleared.");
        }

        private void DeleteFiltered()
        {
            var ids = _cardList.Items
                .OfType<ClipboardEntry>()
                .Select(e => e.Id)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                ShowTransientStatus("No filtered items to delete.");
                return;
            }

            if (_settings.ConfirmBatchDelete)
            {
                bool wasTopMost = TopMost;
                try
                {
                    TopMost = false;
                    var result = ThemedMessageBox.Show(
                        this,
                        $"Delete all {ids.Count} item(s) currently visible under the active filters?",
                        "Delete filtered items",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes) return;
                }
                finally
                {
                    TopMost = _settings.AlwaysOnTop;
                }
            }

            foreach (var id in ids)
            {
                _repo.DeleteEntry(id);
                _snapshot.RemoveAll(e => e.Id == id);
            }

            Accessor.ClearCheckedCards();
            RefreshAppFilterOptions();
            RebuildList();
            ShowTransientStatus($"{ids.Count} filtered item{(ids.Count == 1 ? "" : "s")} deleted.");
        }

        private bool HasActiveFilter =>
            (_filterDateCombo != null && _filterDateCombo.SelectedIndex > 0) ||
            (_filterTypeCombo != null && _filterTypeCombo.SelectedIndex > 0) ||
            (_filterAppCombo != null && _filterAppCombo.SelectedIndex > 0) ||
            (_pillBar != null && !string.Equals(_pillBar.SelectedFilter, "All", StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrWhiteSpace(_searchBox?.Text);

        private void UpdateFilterBarState()
        {
            if (_selectAllFilteredCheck == null || _cardList == null) return;

            bool active = HasActiveFilter;
            _selectAllFilteredCheck.Visible = active;
            _deleteFilteredButton.Visible = active;

            int visible = Accessor.GetVisibleEntryCount();
            int checkedVisible = Accessor.GetVisibleCheckedCount();

            _suppressFilterEvents = true;
            try
            {
                _selectAllFilteredCheck.Checked = active && visible > 0 && checkedVisible >= visible;
            }
            finally
            {
                _suppressFilterEvents = false;
            }

            _selectAllFilteredCheck.Text = visible > 0
                ? $"Select all filtered ({visible})"
                : "Select all filtered";
        }

        private static int DateFilterIndex(string? value) => value switch
        {
            "Today" => 1,
            "Yesterday" => 2,
            "Past7" => 3,
            "Past30" => 4,
            "Older" => 5,
            _ => 0
        };

        private static string DateFilterValue(int index) => index switch
        {
            1 => "Today",
            2 => "Yesterday",
            3 => "Past7",
            4 => "Past30",
            5 => "Older",
            _ => "Any"
        };
    }
}

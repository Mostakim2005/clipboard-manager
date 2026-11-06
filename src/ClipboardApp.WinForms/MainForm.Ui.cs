using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    public partial class MainForm
    {
        private void BuildModernUi()
        {
            _topHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 126,
                Padding = Padding.Empty
            };

            _searchContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(12, 8, 12, 6)
            };

            var searchInnerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 8, 6)
            };

            var searchIcon = new Label
            {
                Text = "🔍",
                Dock = DockStyle.Left,
                Width = 28,
                Font = new Font("Segoe UI", 11f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _clearSearchBtn = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 28,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f),
                Visible = false
            };
            _clearSearchBtn.FlatAppearance.BorderSize = 0;
            _clearSearchBtn.Click += (_, _) => { _searchBox.Text = ""; _searchBox.Focus(); };

            _groupCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Right,
                Width = 105,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            _groupCombo.Items.AddRange(new object[] { "No Grouping", "Group: Date", "Group: App", "Group: Type" });
            _groupCombo.SelectedIndex = _settings.GroupingMode switch
            {
                "Date" => 1,
                "App" => 2,
                "Type" => 3,
                _ => 0
            };
            _groupCombo.SelectedIndexChanged += (_, _) =>
            {
                _settings.GroupingMode = _groupCombo.SelectedIndex switch
                {
                    1 => "Date",
                    2 => "App",
                    3 => "Type",
                    _ => "None"
                };
                _settings.Save();
                RebuildList();
            };

            _regexToggle = new CheckBox
            {
                Text = ".* Regex",
                Dock = DockStyle.Right,
                Width = 85,
                Font = new Font("Segoe UI", 8.5f),
                Checked = _settings.UseRegexSearch,
                Cursor = Cursors.Hand
            };
            _regexToggle.CheckedChanged += (_, _) =>
            {
                _settings.UseRegexSearch = _regexToggle.Checked;
                _settings.Save();
                RebuildList();
            };

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                PlaceholderText = "Type to search history (↑↓ navigate, ↵ paste, F5 refresh, Esc close)...",
                Font = new Font("Segoe UI", 10.5f)
            };
            _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 160 };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                RebuildList();
            };
            _searchBox.TextChanged += (_, _) =>
            {
                _clearSearchBtn.Visible = !string.IsNullOrEmpty(_searchBox.Text);
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };
            _searchBox.KeyDown += OnSearchBoxKeyDown;

            searchInnerPanel.Controls.Add(_searchBox);
            searchInnerPanel.Controls.Add(_clearSearchBtn);
            searchInnerPanel.Controls.Add(_groupCombo);
            searchInnerPanel.Controls.Add(_regexToggle);
            searchInnerPanel.Controls.Add(searchIcon);
            _searchContainer.Controls.Add(searchInnerPanel);

            _pillBar = new FilterPillBar
            {
                Dock = DockStyle.Bottom,
                Height = 38
            };
            _pillBar.FilterSelected += (_, _) => RebuildList();

            BuildFilterBar();

            _topHeader.Controls.Add(_pillBar);
            _topHeader.Controls.Add(_filterBar);
            _topHeader.Controls.Add(_searchContainer);

            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            _splitContainer.SplitterMoved += (_, _) =>
            {
                if (_applyingSplitter) return;
                int total = _splitContainer.ClientSize.Width - _splitContainer.SplitterWidth;
                if (total > 80)
                {
                    _settings.SplitterRatio = (float)_splitContainer.SplitterDistance / total;
                    _splitterSaveTimer.Stop();
                    _splitterSaveTimer.Start();
                }
            };

            var listWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 0)
            };
            _cardList = new ModernCardList
            {
                Dock = DockStyle.Fill,
                CardDensity = _settings.CardDensity,
                VisualStyle = _settings.VisualStyle,
                ShowCheckboxes = _settings.ShowCardCheckboxes,
                ShowSerials = _settings.ShowSerials
            };
            _cardList.SelectedIndexChanged += (_, _) =>
            {
                if (!_suppressListEvents) UpdatePreview();
            };
            _cardList.SelectionOrCheckedStateChanged += (_, _) =>
            {
                if (!_suppressListEvents) UpdateBatchBar();
            };
            _cardList.CardActivated += (_, entry) => OnCardActivated(entry);
            _cardList.CardClicked += (_, entry) => OnCardClicked(entry);
            _cardList.StartCardDragRequested += (_, entry) => StartOleDrag(entry);
            _cardList.DragSessionFinished += (_, _) => SetDragArmed(false);
            _cardList.KeyDown += OnCardListKeyDown;
            _cardList.MouseDown += OnCardListMouseDown;

            listWrapper.Controls.Add(_cardList);
            _splitContainer.Panel1.Controls.Add(listWrapper);

            _previewPanel = new RichPreviewPanel();
            _previewPanel.EntryUpdated += (_, updatedEntry) =>
            {
                _repo.UpdateEntry(updatedEntry);
                RebuildList();
            };
            _previewPanel.ClipboardWriteStarted += (_, _) =>
            {
                // Preview copy buttons are explicit user copies, not new history events.
                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
            };
            _splitContainer.Panel2.Controls.Add(_previewPanel);
            _previewPanel.SensitiveVisibilityToggled += (_, entryId) =>
            {
                Accessor.SetEntryUnmasked(entryId, _previewPanel.IsSensitiveRevealed(entryId));
            };

            // The same swap container decides which presentation fills the area
            // currently occupied by the split list + preview host.
            _viewHost = new Panel { Dock = DockStyle.Fill, Padding = Padding.Empty };
            _listPreviewHost = _splitContainer;
            _viewHost.Controls.Add(_splitContainer);
            ApplyViewMode(persist: false);

            _cardContextMenu = new ContextMenuStrip
            {
                ShowImageMargin = false
            };

            _batchBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Visible = false,
                Padding = new Padding(12, 4, 12, 4)
            };
            _batchCountLabel = new Label
            {
                Text = "0 items selected",
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            var btnSelectAll = CreateBatchButton("Select All", () => _cardList.SelectAllCards());
            var btnInvertSelect = CreateBatchButton("Invert", () => _cardList.InvertSelection());
            var btnClearSelect = CreateBatchButton("Clear", () => _cardList.ClearCheckedCards());
            var btnBatchDelete = CreateBatchButton("Delete", BatchDeleteSelected, isDanger: true);
            var btnBatchTag = CreateBatchButton("Tags", () => AssignTagsToSelected());
            var btnBatchCopy = CreateBatchButton("Copy Merged", () => CopySelectedMerged());
            var btnBatchMerge = CreateBatchButton("Merge & Paste", () => BatchMergeAndPaste());
            _batchPasteEachButton = CreateBatchButton("Paste Each", PasteCheckedEach);
            _batchPasteEachButton.Visible = false;

            _batchBar.Controls.Add(_batchPasteEachButton);
            _batchBar.Controls.Add(btnBatchMerge);
            _batchBar.Controls.Add(btnBatchCopy);
            _batchBar.Controls.Add(btnBatchTag);
            _batchBar.Controls.Add(btnBatchDelete);
            _batchBar.Controls.Add(btnClearSelect);
            _batchBar.Controls.Add(btnInvertSelect);
            _batchBar.Controls.Add(btnSelectAll);
            _batchBar.Controls.Add(_batchCountLabel);

            _footerBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Padding = new Padding(12, 4, 12, 4)
            };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f),
                Text = DefaultStatusText
            };

            var footerButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _settingsButton = CreateFooterButton("Settings", 88, ShowOptions);
            _targetButton = CreateFooterButton("Target: Auto", 124, ChoosePasteTarget);
            _vaultButton = CreateFooterButton("Vault", 72, ShowApiVault);
            _viewButton = CreateFooterButton("Grid", 74, ToggleViewMode);
            _targetButton.Visible = _settings.ShowPasteTargetBadge;
            _vaultButton.Visible = _settings.EnableApiVault;
            _dragToggle = CreateFooterButton("Drag: Off", 108, () => SetDragArmed(!_manualDragArmed));
            _refreshButton = CreateFooterButton("Refresh", 82, () =>
            {
                ReloadSnapshot(force: true);
                ShowTransientStatus("History refreshed.");
            });

            footerButtons.Controls.Add(_settingsButton);
            footerButtons.Controls.Add(_targetButton);
            footerButtons.Controls.Add(_vaultButton);
            footerButtons.Controls.Add(_viewButton);
            footerButtons.Controls.Add(_dragToggle);
            footerButtons.Controls.Add(_refreshButton);

            _footerBar.Controls.Add(_statusLabel);
            _footerBar.Controls.Add(footerButtons);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 450,
                ReshowDelay = 100,
                ShowAlways = true
            };
            _toolTip.SetToolTip(_dragToggle,
                "Arms drag-and-drop for this window session.\n" +
                "Drag turns off automatically when you leave, minimize, hide, or close the window.\n" +
                "Double-click paste and multi-select are never blocked.");
            _toolTip.SetToolTip(_refreshButton,
                "Reloads the list from the encrypted database.\n" +
                "While this is off, new copies are saved but the visible list stays stable.");
            _toolTip.SetToolTip(_settingsButton, "Open settings. Includes drag, refresh, dedupe and multi-paste options.");
            _toolTip.SetToolTip(_targetButton, "Current paste target. Click to choose a specific window when many apps are open.");
            _toolTip.SetToolTip(_vaultButton, "Open the encrypted API-key vault. Values are never added to clipboard history.");
            _toolTip.SetToolTip(_viewButton,
                "Switches between List + Preview (card list with inspector) and Grid (tiles across the window).\n" +
                "Image tiles grow to show a thumbnail; the choice is remembered until you change it.");
            _toolTip.SetToolTip(_batchPasteEachButton, "Pastes each checked item separately, in visual order, instead of merging them. Enable it in Settings > Interactions.");
            _toolTip.SetToolTip(_regexToggle, "Treats the search box as a regular expression.");
            _toolTip.SetToolTip(_groupCombo, "Groups the current snapshot without changing the stored order.");
            SetFilterTooltips();

            // Bottom area keeps the batch bar and footer stacked instead of
            // overlapping each other. Height grows only while a batch is active.
            _bottomArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34
            };
            _bottomArea.Controls.Add(_batchBar);
            _bottomArea.Controls.Add(_footerBar);

            // Add Fill first, then edges; this gives deterministic docking without BringToFront hacks.
            Controls.Add(_viewHost);
            Controls.Add(_bottomArea);
            Controls.Add(_topHeader);

            StyleViewButton();

            _maintenanceTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _maintenanceTimer.Tick += (_, _) =>
            {
                _repo.PruneEntries(_settings.MaxHistoryCount, _settings.MaxHistoryAgeDays);
                int purged = _repo.ClearExpired();
                if (purged > 0)
                {
                    _snapshotDirty = true;
                    if (Visible && WindowState != FormWindowState.Minimized) ReloadSnapshot(force: true);
                }
                _apiVault.LockIfIdle(_settings.ApiVaultAutoLockMinutes);
                _snapshotDirty = true;
            };
            _maintenanceTimer.Start();
        }

        // ---------------------------------------------------------------- view mode

        /// <summary>Active presentation: the classic list plus inspector, or the tile grid.</summary>
        private string ViewMode => _settings.ViewMode == "Grid" ? "Grid" : "ListPreview";

        /// <summary>
        /// Live switch between "List + Preview" and "Grid".
        ///
        /// Selection, filtering, grouping and the snapshot are shared, so toggling
        /// only re-parents the visible control and re-renders from the same data.
        /// The chosen mode is persisted immediately so it survives a restart.
        /// </summary>
        private void ToggleViewMode()
        {
            _settings.ViewMode = ViewMode == "Grid" ? "ListPreview" : "Grid";
            _settings.Save();
            ApplyViewMode(persist: false);
            ShowTransientStatus(_settings.ViewMode == "Grid"
                ? "Grid view — checkboxes and selection carry over from the list."
                : "List + Preview view.");
        }

        private void ApplyViewMode(bool persist)
        {
            if (_viewHost == null) return;

            bool grid = _settings.ViewMode == "Grid";
            Control desired = grid ? (_gridList ??= BuildGridList()) : _listPreviewHost;

            if (_viewHost.Controls.Count != 1 || !ReferenceEquals(_viewHost.Controls[0], desired))
            {
                _viewHost.SuspendLayout();
                _viewHost.Controls.Clear();
                _viewHost.Controls.Add(desired);
                _viewHost.ResumeLayout(true);
            }

            if (grid)
            {
                _gridList!.Columns = _settings.GridColumns;
                _gridList.VisualStyle = UiStyler.UseCustomStyling(_settings) ? _settings.VisualStyle : "Native";
                _gridList.ShowCheckboxes = _settings.ShowCardCheckboxes;
                _gridList.ShowSerials = _settings.ShowSerials;
                _gridList.DragEnabled = _settings.EnableDragDrop;
                _gridList.DragArmed = _settings.EnableDragDrop && !_settings.RequireManualDragArm;
                _gridList.IsDark = IsDarkTheme();
                _gridList.BackColor = IsDarkTheme() ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
                RebuildGridItems();
            }
            else
            {
                ApplySplitterLayout();
            }

            StyleViewButton();
            UpdateDragToggleVisual();
            if (persist) _settings.Save();
        }

        private GridCardList BuildGridList()
        {
            var grid = new GridCardList(_thumbnails, e => _repo.GetImageData(e.Id))
            {
                Dock = DockStyle.Fill,
                CardDensity = _settings.CardDensity
            };
            grid.SelectionOrCheckedStateChanged += (_, _) =>
            {
                if (!_suppressListEvents) UpdateBatchBar();
            };
            grid.CardActivated += (_, entry) => OnCardActivated(entry);
            grid.CardClicked += (_, entry) => OnCardClicked(entry);
            grid.StartCardDragRequested += (_, entry) => StartOleDrag(entry);
            grid.DragSessionFinished += (_, _) => SetDragArmed(false);
            grid.KeyDown += OnCardListKeyDown;
            return grid;
        }

        /// <summary>Mirrors the filtered/grouped list rows into the grid, preserving selection.</summary>
        private void RebuildGridItems()
        {
            if (_gridList == null) return;

            _suppressListEvents = true;
            try
            {
                _gridList.BeginItems();
                foreach (var item in _cardList.Items) _gridList.AddItem(item);
                _gridList.RestoreCheckedIds(_cardList.GetCheckedIds());
                _gridList.RestoreSelectionState(
                    (_cardList.SelectedItem as ClipboardEntry)?.Id,
                    _cardList.GetSelectionAnchorId());
                _gridList.EndItems();
            }
            finally
            {
                _suppressListEvents = false;
            }
        }

        private void StyleViewButton()
        {
            if (_viewButton == null) return;
            // Label the button with what it switches TO.
            _viewButton.Text = ViewMode == "Grid" ? "List + Preview" : "Grid";
            _viewButton.Width = ViewMode == "Grid" ? 100 : 74;
            StyleFooterButton(_viewButton, IsDarkTheme());
            if (ViewMode == "Grid")
            {
                _viewButton.ForeColor = IsDarkTheme() ? ModernPalette.DarkAccent : ModernPalette.LightAccent;
            }
        }

        private Button CreateFooterButton(string text, int width, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(3, 0, 0, 0),
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => onClick();
            return button;
        }

        private Button CreateBatchButton(string text, Action onClick, bool isDanger = false)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(3, 2, 3, 2),
                Padding = new Padding(6, 2, 6, 2)
            };
            btn.FlatAppearance.BorderSize = 0;
            if (isDanger) btn.ForeColor = Color.FromArgb(239, 68, 68);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private void PasteCheckedEach()
        {
            var targetIds = new System.Collections.Generic.HashSet<string>(_cardList.GetCheckedOrSelectedIds());
            var entries = new System.Collections.Generic.List<ClipboardEntry>();
            foreach (var item in _cardList.Items)
            {
                if (item is ClipboardEntry entry && targetIds.Contains(entry.Id)) entries.Add(entry);
            }
            SequentialBatchPaste(entries);
        }

        private void ApplyTheme()
        {
            UiStyler.ActiveSettings = _settings;
            if (!UiStyler.UseCustomStyling(_settings))
            {
                ApplyNativeChrome();
                return;
            }

            bool isDark = _settings.Theme == "Dark" || (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            ModernPalette.ApplyDwmStyling(Handle, isDark);

            _topHeader.BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            _pillBar.IsDark = isDark;
            _pillBar.UseNative = false;
            _cardList.IsDark = isDark;
            _previewPanel.IsDark = isDark;
            if (_gridList != null)
            {
                _gridList.IsDark = isDark;
                _gridList.VisualStyle = _settings.VisualStyle;
                _gridList.ShowCheckboxes = _settings.ShowCardCheckboxes;
                _gridList.ShowSerials = _settings.ShowSerials;
                _gridList.BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            }

            UiStyler.ApplyMenu(_cardContextMenu, _settings);
            _cardList.CardDensity = _settings.CardDensity;
            _cardList.VisualStyle = _settings.VisualStyle;
            _cardList.ShowCheckboxes = _settings.ShowCardCheckboxes;
            _cardList.ShowSerials = _settings.ShowSerials;
            StyleFilterBar(isDark);
            _tray.ApplyStyle(_settings);

            if (_batchBar != null)
            {
                _batchBar.BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249);
                _batchCountLabel.ForeColor = isDark ? Color.White : Color.FromArgb(15, 23, 42);
            }
            if (_bottomArea != null)
            {
                _bottomArea.BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            }

            if (isDark)
            {
                BackColor = ModernPalette.DarkWindowBg;
                ForeColor = ModernPalette.DarkTextPrimary;
                _searchContainer.BackColor = ModernPalette.DarkWindowBg;
                _searchBox.BackColor = ModernPalette.DarkSearchBg;
                _searchBox.ForeColor = ModernPalette.DarkTextPrimary;
                _clearSearchBtn.ForeColor = ModernPalette.DarkTextSecondary;
                _regexToggle.ForeColor = ModernPalette.DarkTextSecondary;
                if (_groupCombo != null)
                {
                    _groupCombo.BackColor = ModernPalette.DarkSearchBg;
                    _groupCombo.ForeColor = ModernPalette.DarkTextPrimary;
                }
                _footerBar.BackColor = ModernPalette.DarkWindowBg;
                _statusLabel.ForeColor = ModernPalette.DarkTextSecondary;
                if (_settingsButton != null)
                {
                    _settingsButton.BackColor = Color.FromArgb(32, 32, 36);
                    _settingsButton.ForeColor = ModernPalette.DarkTextPrimary;
                }
                if (_refreshButton != null)
                {
                    _refreshButton.BackColor = Color.FromArgb(32, 32, 36);
                    _refreshButton.ForeColor = ModernPalette.DarkTextPrimary;
                }
                StyleFooterButton(_targetButton, true);
                StyleFooterButton(_vaultButton, true);
                StyleFooterButton(_dragToggle, true);
            }
            else
            {
                BackColor = ModernPalette.LightWindowBg;
                ForeColor = ModernPalette.LightTextPrimary;
                _searchContainer.BackColor = ModernPalette.LightWindowBg;
                _searchBox.BackColor = ModernPalette.LightSearchBg;
                _searchBox.ForeColor = ModernPalette.LightTextPrimary;
                _clearSearchBtn.ForeColor = ModernPalette.LightTextSecondary;
                _regexToggle.ForeColor = ModernPalette.LightTextSecondary;
                if (_groupCombo != null)
                {
                    _groupCombo.BackColor = ModernPalette.LightSearchBg;
                    _groupCombo.ForeColor = ModernPalette.LightTextPrimary;
                }
                _footerBar.BackColor = ModernPalette.LightWindowBg;
                _statusLabel.ForeColor = ModernPalette.LightTextSecondary;
                if (_settingsButton != null)
                {
                    _settingsButton.BackColor = Color.FromArgb(241, 245, 249);
                    _settingsButton.ForeColor = ModernPalette.LightTextPrimary;
                }
                if (_refreshButton != null)
                {
                    _refreshButton.BackColor = Color.FromArgb(241, 245, 249);
                    _refreshButton.ForeColor = ModernPalette.LightTextPrimary;
                }
                StyleFooterButton(_targetButton, false);
                StyleFooterButton(_vaultButton, false);
                StyleFooterButton(_dragToggle, false);
            }

            UpdateDragToggleVisual();
            UpdateBatchBar();
        }

        private void StyleFooterButton(Button? button, bool isDark)
        {
            if (button == null) return;
            button.BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249);
            button.ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            button.FlatAppearance.BorderSize = 0;
        }

        private void StyleFilterBar(bool isDark)
        {
            if (_filterBar == null) return;

            _filterBar.BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ApplyFilterControlColors(_filterBar, isDark);

            if (_deleteFilteredButton != null)
            {
                _deleteFilteredButton.ForeColor = isDark
                    ? Color.FromArgb(248, 113, 113)
                    : Color.FromArgb(185, 28, 28);
            }
        }

        private void ApplyFilterControlColors(Control parent, bool isDark)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case ComboBox combo:
                        combo.BackColor = isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg;
                        combo.ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
                        break;
                    case Button button:
                        button.BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249);
                        button.ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
                        break;
                    case CheckBox checkBox:
                        checkBox.BackColor = Color.Transparent;
                        checkBox.ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary;
                        break;
                }

                if (control.HasChildren) ApplyFilterControlColors(control, isDark);
            }
        }

        private void ApplyNativeChrome()
        {
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;

            if (_topHeader != null) _topHeader.BackColor = SystemColors.Window;
            if (_searchContainer != null) _searchContainer.BackColor = SystemColors.Window;
            if (_searchBox != null)
            {
                _searchBox.BackColor = SystemColors.Window;
                _searchBox.ForeColor = SystemColors.WindowText;
            }
            if (_clearSearchBtn != null) _clearSearchBtn.ForeColor = SystemColors.ControlText;
            if (_regexToggle != null)
            {
                _regexToggle.BackColor = Color.Transparent;
                _regexToggle.ForeColor = SystemColors.ControlText;
            }
            if (_groupCombo != null)
            {
                _groupCombo.BackColor = SystemColors.Window;
                _groupCombo.ForeColor = SystemColors.WindowText;
            }
            if (_pillBar != null)
            {
                _pillBar.IsDark = false;
                _pillBar.UseNative = true;
            }
            if (_cardList != null)
            {
                _cardList.IsDark = false;
                _cardList.VisualStyle = "Native";
                _cardList.CardDensity = _settings.CardDensity;
                _cardList.ShowCheckboxes = _settings.ShowCardCheckboxes;
                _cardList.ShowSerials = _settings.ShowSerials;
            }
            if (_previewPanel != null) _previewPanel.IsDark = false;
            if (_gridList != null)
            {
                _gridList.IsDark = false;
                _gridList.VisualStyle = "Native";
                _gridList.BackColor = SystemColors.Window;
            }
            if (_filterBar != null) _filterBar.BackColor = SystemColors.Window;
            if (_filterBar != null) ApplyNativeFilterColors(_filterBar);
            if (_footerBar != null) _footerBar.BackColor = SystemColors.Control;
            if (_batchBar != null) _batchBar.BackColor = SystemColors.Control;
            if (_footerBar != null) ApplyNativeFilterColors(_footerBar);
            if (_batchBar != null) ApplyNativeFilterColors(_batchBar);
            if (_batchCountLabel != null) _batchCountLabel.ForeColor = SystemColors.ControlText;
            if (_statusLabel != null) _statusLabel.ForeColor = SystemColors.ControlText;

            foreach (var button in new[] { _settingsButton, _refreshButton, _targetButton, _vaultButton, _dragToggle })
            {
                if (button == null) continue;
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }

            if (_cardContextMenu != null)
            {
                UiStyler.ApplyMenu(_cardContextMenu, _settings);
            }
            _tray.ApplyStyle(_settings);
            UpdateDragToggleVisual();
            UpdateBatchBar();
        }

        private void ApplyNativeFilterColors(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case ComboBox combo:
                        combo.BackColor = SystemColors.Window;
                        combo.ForeColor = SystemColors.WindowText;
                        break;
                    case Button button:
                        button.BackColor = SystemColors.Control;
                        button.ForeColor = SystemColors.ControlText;
                        break;
                    case CheckBox checkBox:
                        checkBox.BackColor = Color.Transparent;
                        checkBox.ForeColor = SystemColors.ControlText;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = SystemColors.ControlText;
                        break;
                }

                if (control.HasChildren) ApplyNativeFilterColors(control);
            }
        }
    }
}

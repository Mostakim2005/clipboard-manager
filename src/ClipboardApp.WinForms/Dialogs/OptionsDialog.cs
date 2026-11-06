using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public class OptionsDialog : Form
    {
        public AppSettings Settings { get; }

        // General
        private readonly CheckBox _topCheck;
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _minimizedCheck;
        private readonly CheckBox _soundCheck;
        private readonly CheckBox _toastCheck;
        private readonly CheckBox _captureTextCheck;
        private readonly CheckBox _captureImagesCheck;
        private readonly CheckBox _captureFilesCheck;
        private readonly HotkeyCaptureBox _hkBox;

        // Appearance
        private readonly ComboBox _themeCombo;
        private readonly ComboBox _visualStyleCombo;
        private readonly ComboBox _densityCombo;
        private readonly ComboBox _groupingCombo;
        private readonly ComboBox _editPresetCombo;
        private readonly NumericUpDown _splitterRatioNum;
        private readonly ComboBox _viewModeCombo;
        private readonly NumericUpDown _gridColumnsNum;

        // Interactions
        private readonly ComboBox _pasteTriggerCombo;
        private readonly CheckBox _doubleClickPasteCheck;
        private readonly CheckBox _dragDropCheck;
        private readonly CheckBox _manualDragCheck;
        private readonly CheckBox _cardCheckboxesCheck;
        private readonly CheckBox _plainTextCheck;
        private readonly CheckBox _confirmBatchDeleteCheck;
        private readonly CheckBox _sequentialBatchCheck;
        private readonly ComboBox _batchSeparatorCombo;
        private readonly ComboBox _batchCopyFormatCombo;
        private readonly CheckBox _mixedMergeCheck;
        private readonly CheckBox _clearSelectionCheck;
        private readonly CheckBox _persistSelectionCheck;
        private readonly ComboBox _pasteTargetModeCombo;
        private readonly CheckBox _showTargetBadgeCheck;
        private readonly Label _interactionWarning;

        // UI styling
        private readonly CheckBox _customUiStylingCheck;
        private readonly List<string> _initialContextMenuOrder;
        private readonly List<string> _initialContextMenuHidden;

        // History refresh
        private readonly CheckBox _liveRefreshCheck;
        private readonly CheckBox _showSerialsCheck;

        // Privacy / API vault
        private readonly NumericUpDown _maxCountNum;
        private readonly NumericUpDown _autoClearNum;
        private readonly ComboBox _dedupCombo;
        private readonly CheckBox _respectPrivacyCheck;
        private readonly TextBox _ignoredAppsBox;
        private readonly CheckBox _enableVaultCheck;
        private readonly CheckBox _apiClearClipboardCheck;
        private readonly NumericUpDown _apiClearSecondsNum;
        private readonly NumericUpDown _apiAutoLockNum;

        private readonly ToolTip _toolTip;
        private readonly Panel _helpPanel;
        private readonly Label _helpTitle;
        private readonly Label _helpBody;

        // Navigation Sidebar
        private readonly Panel _sidebar;
        private readonly Panel _contentContainer;
        private readonly Panel _pnlGeneral;
        private readonly Panel _pnlAppearance;
        private readonly Panel _pnlInteractions;
        private readonly Panel _pnlPrivacy;
        private readonly Button[] _navButtons = new Button[4];

        public bool HistoryImported { get; private set; }
        private readonly ClipboardApp.Core.Storage.IClipboardRepository? _repo;

        public OptionsDialog(AppSettings currentSettings, ClipboardApp.Core.Storage.IClipboardRepository? repo = null)
        {
            Settings = currentSettings;
            _repo = repo;
            _initialContextMenuOrder = new List<string>(Settings.ContextMenuOrder);
            _initialContextMenuHidden = new List<string>(Settings.ContextMenuHidden);
            _toolTip = new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 450,
                ReshowDelay = 100,
                ShowAlways = true
            };

            bool isDarkTheme = currentSettings.Theme == "Dark" || (currentSettings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            _helpPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 112,
                Padding = new Padding(20, 8, 20, 8),
                BackColor = isDarkTheme ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249)
            };
            _helpTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = isDarkTheme ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
            };
            _helpBody = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = isDarkTheme ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };
            _helpPanel.Controls.Add(_helpBody);
            _helpPanel.Controls.Add(_helpTitle);

            Text = "ClipboardApp Settings — " + AppVersion.Display;
            Width = 700;
            Height = 660;
            MinimumSize = new Size(640, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            bool isDark = Settings.Theme == "Dark" || (Settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg;
            ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            Font = new Font("Segoe UI", 9.5f);

            ModernPalette.ApplyDwmStyling(Handle, isDark);

            // Left Navigation Sidebar
            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 175,
                BackColor = isDark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(241, 245, 249),
                Padding = new Padding(8, 16, 8, 16)
            };

            // Content Container
            _contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                AutoScroll = true
            };

            // Category Panels
            _pnlGeneral = CreateSectionPanel(isDark);
            _pnlAppearance = CreateSectionPanel(isDark);
            _pnlInteractions = CreateSectionPanel(isDark);
            _pnlPrivacy = CreateSectionPanel(isDark);

            // ================= 1. General Panel =================
            AddSectionHeader(_pnlGeneral, "General & Behavior", "System tray, startup and notifications");
            _topCheck = AddCheckBox(_pnlGeneral, "Always on top", Settings.AlwaysOnTop);
            _startupCheck = AddCheckBox(_pnlGeneral, "Start with Windows", Settings.StartWithWindows);
            _minimizedCheck = AddCheckBox(_pnlGeneral, "Start minimized to system tray", Settings.StartMinimizedToTray);
            _soundCheck = AddCheckBox(_pnlGeneral, "Play sound on capture", Settings.PlaySoundOnCapture);
            _toastCheck = AddCheckBox(_pnlGeneral, "Show balloon notification on capture", Settings.ShowToastOnCapture);
            _captureTextCheck = AddCheckBox(_pnlGeneral, "Capture copied text & links", Settings.CaptureText,
                "Stores text and URL clips. Turn off for a text-free history.");
            _captureImagesCheck = AddCheckBox(_pnlGeneral, "Capture copied images & screenshots", Settings.CaptureImages,
                "Stores images as PNG. Large images are skipped above the configured size limit.");
            _captureFilesCheck = AddCheckBox(_pnlGeneral, "Capture copied files (File Drop / FD)", Settings.CaptureFiles,
                "File Drop: copy one or more files in Explorer and they appear as a single file-list clip. Pasting in Explorer or Slack restores/uploads the files.");
            _liveRefreshCheck = AddCheckBox(_pnlGeneral, "Refresh list after a confirmed copy (FD: fresh data)", Settings.LiveRefresh,
                "FD = fresh data. ON: after the clipboard capture is written to the database, the list refreshes once. New clips appear at the top; existing cards keep their positions and your selection. OFF: new clips are saved silently; press F5 or reopen to see them.");

            var hotkeyPanel = AddLabeledField(_pnlGeneral, "Global Hotkey:", isDark,
                "Click Set, then press the combination you want. Esc cancels. A shortcut needs at least one of Ctrl, Alt, Shift or Win.");
            _hkBox = new HotkeyCaptureBox(isDark)
            {
                Dock = DockStyle.Fill,
                Value = Settings.Hotkey
            };
            hotkeyPanel.Controls.Add(_hkBox);

            // ================= 2. Appearance Panel =================
            AddSectionHeader(_pnlAppearance, "Appearance & Visual Style", "Theme, acrylic glassmorphism, card density and sizing");

            var themePanel = AddLabeledField(_pnlAppearance, "Theme Mode:", isDark);
            _themeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _themeCombo.Items.AddRange(new object[] { "System", "Light", "Dark" });
            _themeCombo.SelectedItem = Settings.Theme;
            themePanel.Controls.Add(_themeCombo);

            var stylePanel = AddLabeledField(_pnlAppearance, "Material / Visual Style:", isDark);
            _visualStyleCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _visualStyleCombo.Items.AddRange(new object[] { "Fluent", "Acrylic", "Classic", "Native" });
            _visualStyleCombo.SelectedItem = Settings.VisualStyle;
            if (_visualStyleCombo.SelectedIndex < 0) _visualStyleCombo.SelectedIndex = 0;
            stylePanel.Controls.Add(_visualStyleCombo);

            _customUiStylingCheck = AddCheckBox(_pnlAppearance,
                "Apply custom styling to dialogs, menus and inline forms",
                Settings.EnableCustomUiStyling,
                "ON: every app-owned window, menu and form follows the selected material. OFF: dialogs and menus use native Windows colors. Cards and the main window keep their own theme.");

            var densityPanel = AddLabeledField(_pnlAppearance, "Card Layout Density:", isDark,
                "Comfortable shows a metadata row; Compact fits more cards on screen.");
            _densityCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _densityCombo.Items.AddRange(new object[] { "Comfortable", "Compact" });
            _densityCombo.SelectedItem = Settings.CardDensity;
            densityPanel.Controls.Add(_densityCombo);

            _showSerialsCheck = AddCheckBox(_pnlAppearance, "Show stable serial numbers (#1, #2, ...) on cards", Settings.ShowSerials,
                "Serial numbers are derived from the current visual order. They do not change when you copy or paste an existing clip; only adding, deleting or pinning changes the order.");

            var groupPanel = AddLabeledField(_pnlAppearance, "Default Grouping Mode:", isDark);
            _groupingCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _groupingCombo.Items.AddRange(new object[] { "None", "Date", "App", "Type" });
            _groupingCombo.SelectedItem = Settings.GroupingMode;
            groupPanel.Controls.Add(_groupingCombo);

            var presetPanel = AddLabeledField(_pnlAppearance, "Edit Window Preset Size:", isDark);
            _editPresetCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _editPresetCombo.Items.AddRange(new object[] { "Standard", "Large", "Remember" });
            _editPresetCombo.SelectedItem = Settings.EditWindowPreset;
            presetPanel.Controls.Add(_editPresetCombo);

            var splitPanel = AddLabeledField(_pnlAppearance, "Default Splitter Width (% left card list):", isDark);
            _splitterRatioNum = new NumericUpDown
            {
                Minimum = 20,
                Maximum = 80,
                Value = (decimal)Math.Round(Settings.SplitterRatio * 100),
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            splitPanel.Controls.Add(_splitterRatioNum);

            var viewModePanel = AddLabeledField(_pnlAppearance, "Entry View Mode:", isDark,
                "List + Preview keeps the card list on the left and the inspector on the right. " +
                "Grid shows entries as tiles across the full window: text tiles are one row tall and " +
                "image tiles grow to show a thumbnail. Switch any time with the List / Grid button in the footer.");
            _viewModeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _viewModeCombo.Items.AddRange(new object[] { "List + Preview", "Grid" });
            _viewModeCombo.SelectedIndex = Settings.ViewMode == "Grid" ? 1 : 0;
            viewModePanel.Controls.Add(_viewModeCombo);

            var gridColsPanel = AddLabeledField(_pnlAppearance, "Grid Columns (Auto, or 1-10):", isDark,
                "Auto fits as many tiles as the window width allows. A fixed count keeps tiles the same size " +
                "when you resize. Only used in Grid view.");
            _gridColumnsNum = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10,
                Value = Math.Clamp(Settings.GridColumns, 0, 10),
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            gridColsPanel.Controls.Add(_gridColumnsNum);

            // ================= 3. Interactions Panel =================
            AddSectionHeader(_pnlInteractions, "Interactions & Pasting", "Configure mouse clicks, drag-and-drop, and multi-selection");

            var pasteTriggerPanel = AddLabeledField(_pnlInteractions, "Single-Click Paste Action:", isDark,
                "EnterOnly is the safest default and disables single-click paste. SingleClick pastes on one click and can fire " +
                "while you are trying to select text. Double-click paste is controlled by the separate toggle below; Enter always pastes.");
            _pasteTriggerCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _pasteTriggerCombo.Items.AddRange(new object[] { "EnterOnly", "SingleClick" });
            _pasteTriggerCombo.SelectedItem = Settings.PasteTrigger;
            pasteTriggerPanel.Controls.Add(_pasteTriggerCombo);

            _doubleClickPasteCheck = AddCheckBox(_pnlInteractions,
                "Enable double-click paste (always opens the target chooser first)",
                Settings.EnableDoubleClickPaste,
                "OFF by default. ON: double-clicking a card opens a chooser of active apps, then pastes into the window you pick. " +
                "This is separate from the Paste Action Trigger above and never hides the window without a target.");

            _dragDropCheck = AddCheckBox(_pnlInteractions, "Enable OLE drag-and-drop to external apps", Settings.EnableDragDrop,
                "Master switch for dragging cards into other applications. When off, double-click and selection are completely unaffected.");
            _manualDragCheck = AddCheckBox(_pnlInteractions, "Require manual drag arming (hand toggle in the main window)", Settings.RequireManualDragArm,
                "ON (recommended): drag is OFF until you click the hand toggle. It turns off automatically when you leave, minimize, hide, or close the window. OFF: dragging is always available and can occasionally interfere with rapid clicks.");
            _cardCheckboxesCheck = AddCheckBox(_pnlInteractions, "Show multi-selection checkboxes on cards", Settings.ShowCardCheckboxes,
                "When hidden, Ctrl+Click and Shift+Click still select multiple cards. The batch bar remains available.");
            _confirmBatchDeleteCheck = AddCheckBox(_pnlInteractions, "Ask confirmation before batch deleting items", Settings.ConfirmBatchDelete,
                "Shows a Yes/No prompt before deleting more than one checked entry.");
            _plainTextCheck = AddCheckBox(_pnlInteractions, "Paste as plain text by default", Settings.PasteAsPlainText,
                "Strips HTML/RTF formatting from every paste. You can still use Shift+Enter for a one-off plain-text paste.");
            _sequentialBatchCheck = AddCheckBox(_pnlInteractions, "Enable 'Paste Each' for multi-selection", Settings.SequentialBatchPaste,
                "Adds a Paste Each button that sends one paste per checked clip. Leave off for merged single-paste behavior, which is safer for most apps.");

            var separatorPanel = AddLabeledField(_pnlInteractions, "Separator used when merging selected clips:", isDark,
                "Only applies to merged multi-paste. File-only selections are pasted as one file list.");
            _batchSeparatorCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _batchSeparatorCombo.Items.AddRange(new object[] { "Double Newline", "Single Newline", "Comma Space" });
            _batchSeparatorCombo.SelectedIndex = Settings.BatchSeparator switch
            {
                "SingleNewline" => 1,
                "CommaSpace" => 2,
                _ => 0
            };
            separatorPanel.Controls.Add(_batchSeparatorCombo);

            var copyFormatPanel = AddLabeledField(_pnlInteractions, "Copy-selected format:", isDark,
                "Used by Ctrl+C, the context menu, and the batch bar's Copy Merged. Windows has one clipboard, so multiple items become one merged payload.");
            _batchCopyFormatCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _batchCopyFormatCombo.Items.AddRange(new object[] { "Plain", "Env", "Json", "Csv", "Rich" });
            _batchCopyFormatCombo.SelectedItem = Settings.BatchCopyFormat;
            if (_batchCopyFormatCombo.SelectedIndex < 0) _batchCopyFormatCombo.SelectedIndex = 0;
            copyFormatPanel.Controls.Add(_batchCopyFormatCombo);

            _mixedMergeCheck = AddCheckBox(_pnlInteractions,
                "Enable mixed text + image merging (rich HTML/RTF payload)",
                Settings.EnableMixedMerge,
                "OFF by default. ON: selecting text and images together writes real embedded images plus text in one rich payload. " +
                "Plain-text-only targets still receive text. If the payload cannot be built, images are deselected and a warning is shown.");

            _clearSelectionCheck = AddCheckBox(_pnlInteractions, "Clear multi-selection after a successful paste", Settings.ClearSelectionAfterPaste,
                "OFF (recommended) remembers checked cards across pastes, refreshes, filters and grouping changes. ON clears checks after a confirmed paste.");
            _persistSelectionCheck = AddCheckBox(_pnlInteractions, "Remember selection after restarting the app", Settings.PersistSelectionAcrossRestarts,
                "ON saves checked IDs and the range anchor to settings. Missing/deleted IDs are pruned automatically. OFF remembers within the session only.");

            var targetPanel = AddLabeledField(_pnlInteractions, "Paste target mode:", isDark,
                "Auto uses the last focused window. Ask shows the target chooser before every paste. A failed Auto paste always offers the chooser.");
            _pasteTargetModeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _pasteTargetModeCombo.Items.AddRange(new object[] { "Auto", "Ask" });
            _pasteTargetModeCombo.SelectedItem = Settings.PasteTargetMode;
            if (_pasteTargetModeCombo.SelectedIndex < 0) _pasteTargetModeCombo.SelectedIndex = 0;
            targetPanel.Controls.Add(_pasteTargetModeCombo);

            _showTargetBadgeCheck = AddCheckBox(_pnlInteractions, "Show the current target badge in the footer", Settings.ShowPasteTargetBadge,
                "Shows Target: app-name. Click it to choose a specific window when many apps are open.");

            var customizeMenuPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 4, 0, 6)
            };
            var customizeMenuButton = new Button
            {
                Text = "Customize right-click menu...",
                Dock = DockStyle.Left,
                Width = 230,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            customizeMenuButton.Click += (_, _) => CustomizeContextMenu();
            customizeMenuPanel.Controls.Add(customizeMenuButton);
            _pnlInteractions.Controls.Add(customizeMenuPanel);
            customizeMenuPanel.BringToFront();

            _interactionWarning = new Label
            {
                Dock = DockStyle.Top,
                Height = 196,
                Padding = new Padding(4, 8, 4, 4),
                ForeColor = isDark ? Color.FromArgb(253, 186, 116) : Color.FromArgb(154, 52, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            _pnlInteractions.Controls.Add(_interactionWarning);
            _interactionWarning.BringToFront();

            _pasteTriggerCombo.SelectedIndexChanged += (_, _) => UpdateInteractionWarning();
            _doubleClickPasteCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _dragDropCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _manualDragCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _cardCheckboxesCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _sequentialBatchCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _mixedMergeCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();
            _liveRefreshCheck.CheckedChanged += (_, _) => UpdateInteractionWarning();

            // ================= 4. Privacy Panel =================
            AddSectionHeader(_pnlPrivacy, "History & Privacy", "Database limits, encryption and password manager filtering");

            var maxPanel = AddLabeledField(_pnlPrivacy, "Maximum History Count:", isDark);
            _maxCountNum = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 10000,
                Value = Settings.MaxHistoryCount,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            maxPanel.Controls.Add(_maxCountNum);

            var autoClearPanel = AddLabeledField(_pnlPrivacy, "Auto-clear Sensitive items (minutes, 0=off):", isDark);
            _autoClearNum = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1440,
                Value = Settings.AutoClearSensitiveMinutes,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            autoClearPanel.Controls.Add(_autoClearNum);

            var dedupPanel = AddLabeledField(_pnlPrivacy, "Duplicate content behavior (SHA-256 identity):", isDark,
                "Keep position: copying the same clip again never moves or duplicates it. Move to top: duplicates bump the existing card to the top. Allow duplicates: every copy creates a new row.");
            _dedupCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            _dedupCombo.Items.AddRange(new object[] { "Keep position", "Move to top", "Allow duplicates" });
            _dedupCombo.SelectedIndex = Settings.DeduplicationMode switch
            {
                "MoveToTop" => 1,
                "AllowDuplicates" => 2,
                _ => 0
            };
            dedupPanel.Controls.Add(_dedupCombo);

            _respectPrivacyCheck = AddCheckBox(_pnlPrivacy, "Block items with Password Manager flags (ExcludeFromMonitor / CanIncludeInHistory)", Settings.RespectPrivacyFlags);

            AddSectionHeader(_pnlPrivacy, "Encrypted API Vault", "AES-256-GCM + Windows DPAPI; optional master password");
            _enableVaultCheck = AddCheckBox(_pnlPrivacy, "Enable the API vault", Settings.EnableApiVault,
                "Shows the Vault button and tray entry. API keys are stored separately from clipboard history.");
            _apiClearClipboardCheck = AddCheckBox(_pnlPrivacy, "Clear clipboard after copying an API key", Settings.ClearClipboardAfterApiCopy,
                "The clipboard is cleared only if it still contains the copied key. If you copy something else first, it is left alone.");

            var apiClearPanel = AddLabeledField(_pnlPrivacy, "API clipboard auto-clear (seconds):", isDark,
                "Minimum 5 seconds. The clipboard is cleared only when its sequence number has not changed.");
            _apiClearSecondsNum = new NumericUpDown
            {
                Minimum = 5,
                Maximum = 600,
                Value = Math.Clamp(Settings.ApiClipboardClearSeconds, 5, 600),
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            apiClearPanel.Controls.Add(_apiClearSecondsNum);

            var apiLockPanel = AddLabeledField(_pnlPrivacy, "Lock vault after idle (minutes):", isDark,
                "Only applies when a master password is set. The vault key is cleared from memory when it locks.");
            _apiAutoLockNum = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 240,
                Value = Math.Clamp(Settings.ApiVaultAutoLockMinutes, 1, 240),
                Dock = DockStyle.Fill,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black
            };
            apiLockPanel.Controls.Add(_apiAutoLockNum);

            var lblIgnore = new Label
            {
                Text = "Ignored Apps (one per line, e.g. KeePass, BankingApp):",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };
            _pnlPrivacy.Controls.Add(lblIgnore);

            _ignoredAppsBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.White,
                ForeColor = isDark ? Color.White : Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Text = string.Join(Environment.NewLine, Settings.IgnoredApps)
            };
            _pnlPrivacy.Controls.Add(_ignoredAppsBox);
            _ignoredAppsBox.BringToFront();

            AddSectionHeader(_pnlPrivacy, "Backup & Restore History", "Export encrypted JSON / CSV snapshots or restore previous backups");

            var backupBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(0, 6, 0, 16)
            };

            var btnExportJson = new Button
            {
                Text = "📦 Export JSON...",
                AutoSize = true,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.FromArgb(241, 245, 249),
                ForeColor = isDark ? Color.White : Color.Black
            };
            btnExportJson.Click += (_, _) => ExportJsonBackup();

            var btnExportCsv = new Button
            {
                Text = "📄 Export CSV...",
                AutoSize = true,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.FromArgb(241, 245, 249),
                ForeColor = isDark ? Color.White : Color.Black
            };
            btnExportCsv.Click += (_, _) => ExportCsvBackup();

            var btnImportJson = new Button
            {
                Text = "📥 Import Backup...",
                AutoSize = true,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = isDark ? ModernPalette.DarkCardBg : Color.FromArgb(241, 245, 249),
                ForeColor = isDark ? Color.White : Color.Black
            };
            btnImportJson.Click += (_, _) => ImportJsonBackup();

            backupBar.Controls.Add(btnExportJson);
            backupBar.Controls.Add(btnExportCsv);
            backupBar.Controls.Add(btnImportJson);
            _pnlPrivacy.Controls.Add(backupBar);
            backupBar.BringToFront();

            // Bottom Buttons Bar
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

            var btnSave = new Button
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
            btnSave.FlatAppearance.BorderSize = 0;

            bottomBar.Controls.AddRange(new Control[] { btnSave, spacer, btnCancel });
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            // Build Sidebar Nav Buttons
            string[] navTitles = { "⚙️ General", "🎨 Appearance", "🖱️ Interactions", "🔒 Privacy" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var btn = new Button
                {
                    Text = navTitles[i],
                    Dock = DockStyle.Top,
                    Height = 40,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    UseVisualStyleBackColor = false,
                    TabStop = false,
                    Tag = "no-style"
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (_, _) => SwitchSection(index, isDark);
                _navButtons[i] = btn;
            }

            for (int i = 3; i >= 0; i--)
            {
                _sidebar.Controls.Add(_navButtons[i]);
            }

            Controls.AddRange(new Control[] { _contentContainer, _sidebar, _helpPanel, bottomBar });

            SwitchSection(0, isDark);
            WireDetailedHelp();
            UpdateInteractionWarning();
            UiStyler.ApplyForm(this, Settings);
            UiStyler.StyleButton(btnSave, primary: true, Settings);
        }

        private void CustomizeContextMenu()
        {
            using var dialog = new ContextMenuCustomizeDialog(Settings)
            {
                Owner = this,
                StartPosition = FormStartPosition.CenterParent,
                TopMost = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Settings.ContextMenuOrder = dialog.Order;
                Settings.ContextMenuHidden = dialog.Hidden;
            }
        }

        private void UpdateHelp(string title, string body)
        {
            if (_helpTitle == null) return;
            _helpTitle.Text = title;
            _helpBody.Text = body;
        }

        private void WireHelp(Control root, string title, string body)
        {
            void Attach(Control control)
            {
                control.MouseEnter += (_, _) => UpdateHelp(title, body);
                control.Enter += (_, _) => UpdateHelp(title, body);
                foreach (Control child in control.Controls) Attach(child);
            }
            Attach(root);
        }

        private void WireDetailedHelp()
        {
            UpdateHelp(
                "Setting help",
                "Hover or focus any setting to see exactly what it changes and a concrete example. The warning text in Interactions explains when one option can block another.");

            WireHelp(_liveRefreshCheck, "Refresh after a confirmed copy (FD: fresh data)",
                "ON: when a copy is confirmed (the database insert succeeds), the visible list refreshes once. " +
                "Example: you have 8 clips, copy new text X, and the list refreshes. X appears at #1, but the other 8 clips keep their relative order and your selected card stays selected. " +
                "OFF: X is still saved, but the list stays frozen until F5/Refresh or you reopen the window.");

            WireHelp(_captureFilesCheck, "Capture copied files (File Drop / FD)",
                "ON: copying files in Explorer stores the file list as one clip. Example: select report.pdf and photo.png, press Ctrl+C: one card shows both paths. " +
                "Pasting into Explorer recreates the file selection; pasting into Slack/Chrome can upload both files. OFF: file copies are ignored; other formats are unaffected.");

            WireHelp(_captureImagesCheck, "Capture images and screenshots",
                "ON: bitmap/DIB clipboard data is stored as PNG. Example: Win+Shift+S then paste into ClipboardApp shows a PNG card with dimensions. " +
                "Images above MaxImageSizeMb are skipped. OFF: images are ignored.");

            WireHelp(_captureTextCheck, "Capture text and links",
                "ON: Unicode text, links, colors and HTML/RTF fallbacks are captured. Example: copy a URL from a browser; a LINK card is stored. OFF: text-like clips are ignored.");

            WireHelp(_dragDropCheck, "Enable drag-and-drop",
                "Master switch for dragging cards into other apps. OFF: the footer hand toggle is disabled and no OLE drag can start. " +
                "Double-click, Enter, copy, selection and marquee are unaffected.");

            WireHelp(_manualDragCheck, "Require manual drag arming",
                "ON (recommended): the footer toggle starts as 'Drag: Off'. Click it to arm one session; it turns off when you leave, minimize, hide, close, or finish a drag. " +
                "OFF: any card drag can start immediately, which may occasionally compete with rapid clicks.");

            WireHelp(_pasteTriggerCombo, "Paste on single-click / Enter only",
                "EnterOnly is the safest default and disables single-click paste. Single-click is faster but can fire while " +
                "you are trying to select text. Double-click paste is controlled by the separate toggle below.");

            WireHelp(_doubleClickPasteCheck, "Enable double-click paste",
                "OFF (default): double-click never pastes. ON: every double-click opens the active-app chooser first, then pastes into the window you select. " +
                "The chooser is forced even when an automatic target exists, so a stray double-click cannot paste into the wrong app.");

            WireHelp(_mixedMergeCheck, "Mixed text + image merging",
                "OFF (default): selecting text and images copies only the text, shows a warning, and unchecks the image cards. " +
                "ON: one rich payload carries text plus real embedded images through CF_HTML and RTF. Apps without rich support paste the text fallback. " +
                "If an image cannot be embedded, it is unchecked and the text is copied.");

            WireHelp(_customUiStylingCheck, "Custom dialog and menu styling",
                "ON: settings, editors, the vault, inline prompts, the tray menu and themed message boxes follow Fluent/Acrylic/Classic. " +
                "OFF: those surfaces use native Windows colors. Use the Material / Visual Style list to switch between styles.");

            WireHelp(_cardCheckboxesCheck, "Show multi-selection checkboxes",
                "ON: every card shows a checkbox; click it, use Ctrl+Click, Shift+Click, Space or blank-area marquee. OFF: checkboxes are hidden, but Ctrl/Shift selection still works.");

            WireHelp(_sequentialBatchCheck, "Enable Paste Each",
                "OFF (default): checked clips are merged into one paste using the separator. ON: a '⇉ Paste Each' button appears and sends one Ctrl+V per checked clip, in visual order. " +
                "Example: 3 checked lines are pasted as 3 separate pastes; some apps may ignore rapid repeats, so merged mode is safer.");

            WireHelp(_batchSeparatorCombo, "Merged-paste separator",
                "Only used when 2+ clips are merged. Example: [alpha, beta] with Double Newline becomes 'alpha\\r\\n\\r\\nbeta' in one paste. " +
                "File-only selections paste as one file list instead.");

            WireHelp(_batchCopyFormatCombo, "Copy-selected format",
                "Plain joins the selected text. Env writes KEY=value lines. Json writes an object. Csv writes name,value rows. " +
                "Example: clips named OPENAI_KEY=sk-1 and STRIPE_KEY=sk-2 copied as Env become two lines, pasted together in one action.");

            WireHelp(_clearSelectionCheck, "Clear selection after paste",
                "OFF remembers your checked cards across pastes, refreshes, filters, grouping and scrolling. " +
                "ON clears checks only after a confirmed successful paste; a failed paste keeps them.");

            WireHelp(_persistSelectionCheck, "Remember selection after restart",
                "ON stores checked IDs plus the range anchor in settings. Deleted IDs are pruned. OFF keeps them only for this process. " +
                "Selections hidden by a search are still remembered and are included in batch actions.");

            WireHelp(_pasteTargetModeCombo, "Paste target mode",
                "Auto: use the last focused window, and open the chooser if focus fails. Ask: choose the exact window before every paste. " +
                "The footer Target button always lets you override the automatic choice.");

            WireHelp(_showTargetBadgeCheck, "Show target badge",
                "Displays Target: Obsidian (or another process) in the footer. Click it to pick a different window. " +
                "This is the reliable fix when many windows are open and auto-detection guesses wrong.");

            WireHelp(_enableVaultCheck, "Enable the API vault",
                "Adds the Vault button and tray entry. Secrets are AES-256-GCM encrypted with a random vault key, and the vault key is wrapped by Windows DPAPI.");

            WireHelp(_apiClearClipboardCheck, "Clear clipboard after API copy",
                "Example: copy OPENAI_KEY, paste it within 30 seconds, then the clipboard clears automatically. If you copied something else first, that newer clipboard is not touched.");

            WireHelp(_apiClearSecondsNum, "API clipboard timeout",
                "Minimum 5 seconds. The app records the clipboard sequence number, so it only clears the exact API payload it placed there.");

            WireHelp(_apiAutoLockNum, "Vault idle lock",
                "Only used with a master password. After this many idle minutes the in-memory vault key is zeroed; reopening asks for the password again.");

            WireHelp(_showSerialsCheck, "Show stable serial numbers",
                "Cards show #1, #2, ... from the current visual order. Copying or pasting an existing clip does not change its serial. " +
                "Adding, deleting, or pinning can renumber the visible list; the stored sequence order is permanent.");

            WireHelp(_dedupCombo, "Duplicate behavior",
                "Identical content is identified by type-prefixed SHA-256. Keep position: recopying updates CopyCount but never moves or duplicates the card. " +
                "Move to top: recopying gives the existing card a new Seq and it moves to the top on the next refresh. Allow duplicates: every copy creates a new card. " +
                "Example: copy 'hello' then copy 'hello' again: Keep position = 1 card, still in place; Move to top = 1 card at top; Allow duplicates = 2 cards.");
        }

        private void UpdateInteractionWarning()
        {
            if (_interactionWarning == null || _pasteTriggerCombo == null) return;

            var warnings = new List<string>();

            if (!_dragDropCheck.Checked)
            {
                warnings.Add("Drag-and-drop is disabled. Cards can still be selected, copied, and double-clicked.");
            }
            else if (_manualDragCheck.Checked)
            {
                warnings.Add("Drag is OFF by default. Click “✋ Drag” in the footer to arm it; it turns off automatically when you leave the window.");
            }
            else
            {
                warnings.Add("Drag is always available. If rapid clicks accidentally start a drag, enable manual arming.");
            }

            if (_pasteTriggerCombo.SelectedItem?.ToString() == "SingleClick")
            {
                warnings.Add("Single-click paste can fire while you are trying to select text. EnterOnly is safest; enable the double-click toggle for a deliberate mouse gesture.");
            }

            if (_doubleClickPasteCheck.Checked)
            {
                warnings.Add("Double-click paste is ON. Every double-click opens the target chooser first; no paste happens until you pick a window.");
            }
            else
            {
                warnings.Add("Double-click paste is OFF. Double-click only selects a card; use Enter, the context menu, or the batch bar to paste.");
            }

            if (_mixedMergeCheck.Checked)
            {
                warnings.Add("Mixed text + image merge is ON. Rich editors receive embedded images; plain-text apps receive text only. Unsupported images are unchecked with a warning.");
            }
            else
            {
                warnings.Add("Mixed text + image merge is OFF. Selecting text and images copies text only and unchecks the images with a warning.");
            }

            if (!_cardCheckboxesCheck.Checked)
            {
                warnings.Add("Checkboxes are hidden. Use Ctrl+Click or Shift+Click to build a multi-selection.");
            }

            if (_sequentialBatchCheck.Checked)
            {
                warnings.Add("Paste Each sends one paste per clip. If the target app ignores repeated Ctrl+V, use merged paste instead.");
            }

            if (_liveRefreshCheck.Checked)
            {
                warnings.Add("Confirmed-copy refresh (FD): after a successful copy, the list refreshes once. New clips appear at the top; existing clips keep their positions and selection stays on the same card.");
            }
            else
            {
                warnings.Add("Confirmed-copy refresh is OFF. New copies are saved but the visible list will not change until you press F5/Refresh or reopen the window.");
            }

            _interactionWarning.Text = string.Join(Environment.NewLine + Environment.NewLine, warnings);
        }

        private void SwitchSection(int index, bool isDark)
        {
            _contentContainer.Controls.Clear();
            for (int i = 0; i < _navButtons.Length; i++)
            {
                bool active = i == index;
                _navButtons[i].BackColor = active
                    ? (isDark ? Color.FromArgb(49, 46, 129) : Color.FromArgb(224, 231, 255))
                    : Color.Transparent;
                _navButtons[i].ForeColor = active
                    ? (isDark ? Color.White : Color.FromArgb(67, 56, 202))
                    : (isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary);
                _navButtons[i].Font = new Font("Segoe UI", 9.5f, active ? FontStyle.Bold : FontStyle.Regular);
            }

            Panel target = index switch
            {
                0 => _pnlGeneral,
                1 => _pnlAppearance,
                2 => _pnlInteractions,
                _ => _pnlPrivacy
            };

            _contentContainer.Controls.Add(target);
            target.Dock = DockStyle.Fill;
        }

        private static Panel CreateSectionPanel(bool isDark) => new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(4)
        };

        private static void AddSectionHeader(Panel parent, string title, string subtitle)
        {
            var headerPnl = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(0, 0, 0, 8) };
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 26,
                AutoSize = false
            };
            var lblSub = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.Gray,
                Dock = DockStyle.Top,
                Height = 20,
                AutoSize = false
            };
            headerPnl.Controls.Add(lblSub);
            headerPnl.Controls.Add(lblTitle);
            parent.Controls.Add(headerPnl);
            headerPnl.BringToFront();
        }

        private CheckBox AddCheckBox(Panel parent, string text, bool isChecked, string? tooltip = null)
        {
            var chk = new CheckBox
            {
                Text = text,
                Checked = isChecked,
                Dock = DockStyle.Top,
                Height = 32,
                AutoSize = false,
                Padding = new Padding(2)
            };
            parent.Controls.Add(chk);
            chk.BringToFront();
            if (!string.IsNullOrEmpty(tooltip)) _toolTip.SetToolTip(chk, tooltip);
            return chk;
        }

        private Panel AddLabeledField(Panel parent, string labelText, bool isDark, string? tooltip = null)
        {
            var fieldPnl = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 4, 0, 8) };
            var lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary
            };
            var container = new Panel { Dock = DockStyle.Fill };
            fieldPnl.Controls.Add(container);
            fieldPnl.Controls.Add(lbl);
            parent.Controls.Add(fieldPnl);
            fieldPnl.BringToFront();
            if (!string.IsNullOrEmpty(tooltip))
            {
                _toolTip.SetToolTip(fieldPnl, tooltip);
                _toolTip.SetToolTip(lbl, tooltip);
                _toolTip.SetToolTip(container, tooltip);
            }
            return container;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Settings.AlwaysOnTop = _topCheck.Checked;
                Settings.StartWithWindows = _startupCheck.Checked;
                Settings.StartMinimizedToTray = _minimizedCheck.Checked;
                Settings.PlaySoundOnCapture = _soundCheck.Checked;
                Settings.ShowToastOnCapture = _toastCheck.Checked;
                Settings.CaptureText = _captureTextCheck.Checked;
                Settings.CaptureImages = _captureImagesCheck.Checked;
                Settings.CaptureFiles = _captureFilesCheck.Checked;
                Settings.LiveRefresh = _liveRefreshCheck.Checked;
                Settings.Hotkey = _hkBox.Value;

                Settings.Theme = _themeCombo.SelectedItem?.ToString() ?? "System";
                Settings.VisualStyle = _visualStyleCombo.SelectedItem?.ToString() ?? "Fluent";
                Settings.CardDensity = _densityCombo.SelectedItem?.ToString() ?? "Comfortable";
                Settings.GroupingMode = _groupingCombo.SelectedItem?.ToString() ?? "None";
                Settings.ShowSerials = _showSerialsCheck.Checked;
                Settings.EditWindowPreset = _editPresetCombo.SelectedItem?.ToString() ?? "Standard";
                Settings.SplitterRatio = (float)(_splitterRatioNum.Value / 100m);
                Settings.ViewMode = _viewModeCombo.SelectedIndex == 1 ? "Grid" : "ListPreview";
                Settings.GridColumns = (int)_gridColumnsNum.Value;

                Settings.PasteTrigger = _pasteTriggerCombo.SelectedItem?.ToString() ?? "EnterOnly";
                Settings.EnableDoubleClickPaste = _doubleClickPasteCheck.Checked;
                Settings.EnableDragDrop = _dragDropCheck.Checked;
                Settings.RequireManualDragArm = _manualDragCheck.Checked;
                Settings.ShowCardCheckboxes = _cardCheckboxesCheck.Checked;
                Settings.ConfirmBatchDelete = _confirmBatchDeleteCheck.Checked;
                Settings.PasteAsPlainText = _plainTextCheck.Checked;
                Settings.SequentialBatchPaste = _sequentialBatchCheck.Checked;
                Settings.EnableMixedMerge = _mixedMergeCheck.Checked;
                Settings.EnableCustomUiStyling = _customUiStylingCheck.Checked;
                Settings.BatchSeparator = _batchSeparatorCombo.SelectedIndex switch
                {
                    1 => "SingleNewline",
                    2 => "CommaSpace",
                    _ => "DoubleNewline"
                };
                Settings.BatchCopyFormat = _batchCopyFormatCombo.SelectedItem?.ToString() ?? "Plain";
                Settings.ClearSelectionAfterPaste = _clearSelectionCheck.Checked;
                Settings.PersistSelectionAcrossRestarts = _persistSelectionCheck.Checked;
                Settings.PasteTargetMode = _pasteTargetModeCombo.SelectedItem?.ToString() ?? "Auto";
                Settings.ShowPasteTargetBadge = _showTargetBadgeCheck.Checked;

                Settings.MaxHistoryCount = (int)_maxCountNum.Value;
                Settings.AutoClearSensitiveMinutes = (int)_autoClearNum.Value;
                Settings.DeduplicationMode = _dedupCombo.SelectedIndex switch
                {
                    1 => "MoveToTop",
                    2 => "AllowDuplicates",
                    _ => "KeepPosition"
                };
                Settings.RespectPrivacyFlags = _respectPrivacyCheck.Checked;
                Settings.EnableApiVault = _enableVaultCheck.Checked;
                Settings.ClearClipboardAfterApiCopy = _apiClearClipboardCheck.Checked;
                Settings.ApiClipboardClearSeconds = (int)_apiClearSecondsNum.Value;
                Settings.ApiVaultAutoLockMinutes = (int)_apiAutoLockNum.Value;

                var apps = _ignoredAppsBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                Settings.IgnoredApps = apps;
            }
            else
            {
                // The menu customizer writes into the shared settings object so
                // reopening it reflects unsaved changes. Restore on Cancel.
                Settings.ContextMenuOrder = _initialContextMenuOrder;
                Settings.ContextMenuHidden = _initialContextMenuHidden;
            }
            base.OnFormClosing(e);
        }

        private void ExportJsonBackup()
        {
            if (_repo == null)
            {
                MessageBox.Show(this, "Database repository is unavailable.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export History (Encrypted JSON)",
                Filter = "Encrypted JSON Backup (*.json)|*.json|Plain JSON (*.json)|*.json",
                DefaultExt = "json",
                FileName = $"ClipboardApp_Backup_{DateTime.Now:yyyyMMdd_HHmm}.json"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    bool encrypt = dlg.FilterIndex == 1;
                    var entries = _repo.GetAllEntries(includeImageData: true);
                    var json = ClipboardApp.Core.Services.ExportImportService.ExportToJson(entries, encrypt: encrypt);
                    System.IO.File.WriteAllText(dlg.FileName, json);
                    MessageBox.Show(this, $"Exported {entries.Count} clips successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to export: " + ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportCsvBackup()
        {
            if (_repo == null)
            {
                MessageBox.Show(this, "Database repository is unavailable.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export History (CSV)",
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"ClipboardApp_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var entries = _repo.GetAllEntries(includeImageData: false);
                    var csv = ClipboardApp.Core.Services.ExportImportService.ExportToCsv(entries);
                    System.IO.File.WriteAllText(dlg.FileName, csv);
                    MessageBox.Show(this, $"Exported {entries.Count} clips successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to export: " + ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportJsonBackup()
        {
            if (_repo == null)
            {
                MessageBox.Show(this, "Database repository is unavailable.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new OpenFileDialog
            {
                Title = "Import History Backup",
                Filter = "JSON Backup (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var content = System.IO.File.ReadAllText(dlg.FileName);
                    var entries = ClipboardApp.Core.Services.ExportImportService.ImportFromJson(content);
                    if (entries.Count == 0)
                    {
                        MessageBox.Show(this, "No valid clipboard entries found in the file.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int count = 0;
                    foreach (var entry in entries)
                    {
                        _repo.InsertEntry(entry);
                        count++;
                    }

                    HistoryImported = true;
                    MessageBox.Show(this, $"Successfully imported {count} clips into history.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to import: " + ex.Message, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

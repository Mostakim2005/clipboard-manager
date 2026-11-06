using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipboardApp.WinForms
{
    public class AppSettings
    {
        public bool AlwaysOnTop { get; set; } = true;
        public string Hotkey { get; set; } = "Ctrl+`";
        public bool PasteAsPlainText { get; set; } = false;
        public int MaxHistoryCount { get; set; } = 500;
        public int MaxHistoryAgeDays { get; set; } = 30;
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimizedToTray { get; set; } = true;
        public string Theme { get; set; } = "System"; // System, Dark, Light
        public bool PlaySoundOnCapture { get; set; } = false;
        public bool ShowToastOnCapture { get; set; } = false;
        public bool CheckSensitiveData { get; set; } = true;
        public bool RespectPrivacyFlags { get; set; } = false;
        public int AutoClearSensitiveMinutes { get; set; } = 15;
        public bool UseRegexSearch { get; set; } = false;
        public bool MaskSensitiveInList { get; set; } = true;
        public bool EnableFifoPasteQueue { get; set; } = true;
        public List<string> IgnoredApps { get; set; } = new()
        {
            "1Password", "Bitwarden", "KeePass", "KeePassXC", "LastPass", "Dashlane", "ProtonPass", "Enpass"
        };

        // UI, Spacing & Visual Style Settings
        public float SplitterRatio { get; set; } = 0.50f; // 50% / 50% default

        /// <summary>
        /// Entry presentation. "ListPreview" is the classic card list plus the
        /// inspector pane; "Grid" shows tiles across the full width.
        /// </summary>
        public string ViewMode { get; set; } = "ListPreview";

        /// <summary>Grid mode column count (1-10). 0 means auto-fit to the window width.</summary>
        public int GridColumns { get; set; } = 0;

        public string VisualStyle { get; set; } = "Fluent"; // Fluent, Acrylic, Classic, Native
        public string CardDensity { get; set; } = "Comfortable"; // Comfortable, Compact
        public bool ShowCardCheckboxes { get; set; } = true;
        public bool ShowSerials { get; set; } = true;
        public string PasteTrigger { get; set; } = "EnterOnly"; // EnterOnly, SingleClick
        public bool EnableDoubleClickPaste { get; set; } = false;
        public bool EnableDragDrop { get; set; } = true;
        public bool RequireManualDragArm { get; set; } = true;
        public string EditWindowPreset { get; set; } = "Standard"; // Standard, Large, Remember
        public int EditWindowWidth { get; set; } = 680;
        public int EditWindowHeight { get; set; } = 520;

        // Grouping, Deduplication & Advanced Capture
        public string GroupingMode { get; set; } = "None"; // None, Date, App, Type
        public string DeduplicationMode { get; set; } = "KeepPosition"; // KeepPosition, MoveToTop, AllowDuplicates
        public bool CaptureImages { get; set; } = true;
        public bool CaptureFiles { get; set; } = true;
        public bool CaptureText { get; set; } = true;
        public bool ConfirmBatchDelete { get; set; } = true;
        public int MaxImageSizeMb { get; set; } = 15;

        // History snapshot behavior
        public bool LiveRefresh { get; set; } = true;
        public string BatchSeparator { get; set; } = "DoubleNewline"; // DoubleNewline, SingleNewline, CommaSpace
        public bool SequentialBatchPaste { get; set; } = true;

        // Multi-select behavior
        public bool ClearSelectionAfterPaste { get; set; } = false;
        public bool PersistSelectionAcrossRestarts { get; set; } = false;
        public List<string> RememberedSelectedIds { get; set; } = new();
        public string RememberedFocusedId { get; set; } = "";
        public string RememberedAnchorId { get; set; } = "";

        // Batch copy / paste
        public string BatchCopyFormat { get; set; } = "Plain"; // Plain, Env, Json, Csv, Rich
        public bool ShowBatchMergePreview { get; set; } = false;
        public bool EnableMixedMerge { get; set; } = false;

        // Context menu layout (persisted so hidden commands stay hidden)
        public List<string> ContextMenuOrder { get; set; } = new();
        public List<string> ContextMenuHidden { get; set; } = new();

        // UI styling
        public bool EnableCustomUiStyling { get; set; } = true;

        // Always-available filters
        public string FilterDate { get; set; } = "Any"; // Any, Today, Yesterday, Past7, Past30, Older
        public string FilterType { get; set; } = "Any"; // Any, Text, Image, Link, File, Color
        public string FilterApp { get; set; } = "Any";

        // Paste target behavior
        public string PasteTargetMode { get; set; } = "Auto"; // Auto, Ask
        public bool ShowPasteTargetBadge { get; set; } = true;

        // API vault
        public bool EnableApiVault { get; set; } = true;
        public bool ClearClipboardAfterApiCopy { get; set; } = true;
        public int ApiClipboardClearSeconds { get; set; } = 30;
        public int ApiVaultAutoLockMinutes { get; set; } = 5;

        /// <summary>
        /// Bumped when defaults that need a one-time migration change.
        /// Version 2 turns on confirmed-copy refresh for existing profiles.
        /// </summary>
        public int SettingsVersion { get; set; } = 0;

        public ClipboardApp.Core.Storage.DeduplicationMode GetDeduplicationMode() => DeduplicationMode switch
        {
            "MoveToTop" => ClipboardApp.Core.Storage.DeduplicationMode.MoveToTop,
            "AllowDuplicates" => ClipboardApp.Core.Storage.DeduplicationMode.AllowDuplicates,
            _ => ClipboardApp.Core.Storage.DeduplicationMode.KeepPosition
        };

        public string GetBatchSeparator() => BatchSeparator switch
        {
            "SingleNewline" => "\r\n",
            "CommaSpace" => ", ",
            _ => "\r\n\r\n"
        };

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardApp", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        settings.Normalize();
                        return settings;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        private void Normalize()
        {
            // One-time migration: profiles written before SettingsVersion existed
            // get confirmed-copy refresh enabled so new clips are visible without
            // requiring an explicit F5.
            if (SettingsVersion < 3)
            {
                LiveRefresh = true;
                SettingsVersion = 3;
            }

            // Migrate legacy values from older releases.
            DeduplicationMode = DeduplicationMode switch
            {
                "MoveToTop" => "MoveToTop",
                "AllowDuplicates" => "AllowDuplicates",
                "UpdateTimestamp" => "MoveToTop",
                "Exact" => "KeepPosition",
                "None" => "AllowDuplicates",
                _ => "KeepPosition"
            };

            if (BatchSeparator is not ("DoubleNewline" or "SingleNewline" or "CommaSpace"))
            {
                BatchSeparator = "DoubleNewline";
            }

            if (PasteTrigger is not ("EnterOnly" or "SingleClick"))
            {
                // "DoubleClick" was the old default; mouse paste is now opt-in.
                PasteTrigger = "EnterOnly";
            }

            if (BatchCopyFormat is not ("Plain" or "Env" or "Json" or "Csv" or "Rich"))
            {
                BatchCopyFormat = "Plain";
            }

            if (VisualStyle is not ("Fluent" or "Acrylic" or "Classic" or "Native"))
            {
                VisualStyle = "Fluent";
            }

            if (ViewMode is not ("ListPreview" or "Grid"))
            {
                ViewMode = "ListPreview";
            }

            GridColumns = Math.Clamp(GridColumns, 0, 10);

            if (FilterDate is not ("Any" or "Today" or "Yesterday" or "Past7" or "Past30" or "Older"))
            {
                FilterDate = "Any";
            }

            if (FilterType is not ("Any" or "Text" or "Image" or "Link" or "File" or "Color"))
            {
                FilterType = "Any";
            }

            FilterApp ??= "Any";
            if (string.IsNullOrWhiteSpace(FilterApp)) FilterApp = "Any";

            ContextMenuOrder ??= new List<string>();
            ContextMenuHidden ??= new List<string>();
            ContextMenuOrder = ContextMenuOrder
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            ContextMenuHidden = ContextMenuHidden
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (PasteTargetMode is not ("Auto" or "Ask"))
            {
                PasteTargetMode = "Auto";
            }

            ApiClipboardClearSeconds = Math.Clamp(ApiClipboardClearSeconds, 5, 600);
            ApiVaultAutoLockMinutes = Math.Clamp(ApiVaultAutoLockMinutes, 1, 240);
            IgnoredApps ??= new List<string> { "1Password", "Bitwarden", "KeePass", "KeePassXC", "LastPass", "Dashlane", "ProtonPass", "Enpass" };
            RememberedSelectedIds ??= new List<string>();
            RememberedFocusedId ??= "";
            RememberedAnchorId ??= "";

            if (MaxImageSizeMb <= 0) MaxImageSizeMb = 15;
            if (MaxHistoryCount <= 0) MaxHistoryCount = 500;
            if (SplitterRatio < 0.15f || SplitterRatio > 0.85f) SplitterRatio = 0.50f;
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (dir != null) Directory.CreateDirectory(dir);

                // Atomic save using temporary file
                var tmpPath = SettingsPath + ".tmp";
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, SettingsPath, overwrite: true);

                ApplyStartupRegistry(StartWithWindows);
            }
            catch { }
        }

        public static void ApplyStartupRegistry(bool enable)
        {
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "ClipboardApp";

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
                if (key != null)
                {
                    var desired = enable ? $"\"{Application.ExecutablePath}\"" : null;
                    var current = key.GetValue(appName) as string;
                    if (string.Equals(current, desired, StringComparison.OrdinalIgnoreCase)) return;

                    if (enable)
                    {
                        key.SetValue(appName, desired!);
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
            }
            catch { }
        }

        public static bool IsWindowsInDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int lightTheme) return lightTheme == 0;
                }
            }
            catch { }
            return false;
        }
    }
}

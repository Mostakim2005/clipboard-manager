using ClipboardApp.Core;
using ClipboardApp.Core.Interop;
using ClipboardApp.Core.Models;
using ClipboardApp.Core.Security;
using ClipboardApp.Core.Services;
using ClipboardApp.Core.Storage;
using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.Dialogs;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    public partial class MainForm : Form
    {
        private const string DefaultStatusText =
            "Enter Paste  |  Shift+Enter Plain  |  1-9 Quick  |  F5 Refresh  |  Ctrl+C Copy  |  Del Delete  |  Ctrl+P Pin  |  Esc Close";

        private readonly ClipboardMonitor _monitor;
        private readonly SqliteClipboardRepository _repo;
        private readonly ApiKeyRepository _apiKeyRepo;
        private readonly ClipboardApp.Core.Security.ApiVaultService _apiVault;
        private readonly SearchService _search;
        private readonly TrayIconManager _tray;
        private HotkeyManager _hotkey = null!;
        private AppSettings _settings;

        private Panel _topHeader = null!;
        private FilterPillBar _pillBar = null!;
        private Panel _filterBar = null!;
        private ComboBox _filterDateCombo = null!;
        private ComboBox _filterTypeCombo = null!;
        private ComboBox _filterAppCombo = null!;
        private CheckBox _selectAllFilteredCheck = null!;
        private Button _deleteFilteredButton = null!;
        private Button _clearFiltersButton = null!;
        private ModernCardList _cardList = null!;
        private GridCardList? _gridList;
        private Panel _viewHost = null!;
        private Control _listPreviewHost = null!;
        private ThumbnailCache _thumbnails = null!;
        private RichPreviewPanel _previewPanel = null!;
        private TextBox _searchBox = null!;
        private Button _clearSearchBtn = null!;
        private CheckBox _regexToggle = null!;
        private ComboBox _groupCombo = null!;
        private SplitContainer _splitContainer = null!;
        private Label _statusLabel = null!;
        private Panel _searchContainer = null!;
        private Panel _footerBar = null!;
        private Panel _bottomArea = null!;
        private Panel _batchBar = null!;
        private Label _batchCountLabel = null!;
        private ContextMenuStrip _cardContextMenu = null!;
        private Button _dragToggle = null!;
        private Button _refreshButton = null!;
        private Button _settingsButton = null!;
        private Button _targetButton = null!;
        private Button _vaultButton = null!;
        private Button _viewButton = null!;
        private Button _batchPasteEachButton = null!;
        private ToolTip _toolTip = null!;
        private Icon _appIcon = null!;

        private System.Windows.Forms.Timer _searchDebounceTimer = null!;
        private System.Windows.Forms.Timer _maintenanceTimer = null!;
        private System.Windows.Forms.Timer _refreshTimer = null!;
        private System.Windows.Forms.Timer _splitterSaveTimer = null!;
        private System.Windows.Forms.Timer _statusResetTimer = null!;
        private System.Windows.Forms.Timer _singleClickTimer = null!;
        private System.Windows.Forms.Timer _captureRetryTimer = null!;
        private System.Windows.Forms.Timer _apiClipboardClearTimer = null!;

        private readonly List<ClipboardEntry> _snapshot = new();
        private bool _snapshotLoaded;
        private bool _snapshotDirty;
        private int _pendingCaptureCount;
        private bool _suppressListEvents;
        private bool _suppressFilterEvents;
        private bool _applyingSplitter;
        private int _captureRetryCount;
        private ClipboardEntry? _singleClickEntry;
        private bool _manualDragArmed;

        private IntPtr _lastActiveWindow = IntPtr.Zero;
        private IntPtr _preferredTarget = IntPtr.Zero;
        private uint _apiClipboardSequence;
        private DateTime _suppressCaptureUntilUtc = DateTime.MinValue;

        /// <summary>The modeless API vault window, if one is currently open.</summary>
        private ApiVaultDialog? _apiVaultDialog;

        /// <summary>
        /// The visible entry surface. List and Grid share selection state through
        /// this accessor so every command (paste, delete, pin, drag) works in
        /// both views without duplicating logic.
        /// </summary>
        private EntryListAccessor Accessor => new(this);

        private readonly struct EntryListAccessor
        {
            private readonly MainForm _form;
            public EntryListAccessor(MainForm form) => _form = form;

            private bool IsGrid => _form._settings.ViewMode == "Grid" && _form._gridList != null;

            public Control Control => IsGrid ? _form._gridList! : _form._cardList;
            public int Count => IsGrid ? _form._gridList!.ItemCount : _form._cardList.Items.Count;

            public HashSet<string> GetCheckedIds() => IsGrid ? _form._gridList!.GetCheckedIds() : _form._cardList.GetCheckedIds();
            public string? GetFocusedEntryId() => IsGrid ? _form._gridList!.GetFocusedEntryId() : _form._cardList.GetFocusedEntryId();
            public string? GetSelectionAnchorId() => IsGrid ? _form._gridList!.GetSelectionAnchorId() : _form._cardList.GetSelectionAnchorId();
            public List<string> GetCheckedOrSelectedIds() => IsGrid ? _form._gridList!.GetCheckedOrSelectedIds() : _form._cardList.GetCheckedOrSelectedIds();
            public ClipboardEntry? SelectedItem => IsGrid ? _form._gridList!.SelectedItem : _form._cardList.SelectedItem as ClipboardEntry;
            public ClipboardEntry? GetEntryById(string id) => IsGrid ? _form._gridList!.GetEntryById(id) : _form._cardList.GetEntryById(id);

            public int GetVisibleCheckedCount() => IsGrid ? _form._gridList!.GetVisibleCheckedCount() : _form._cardList.GetVisibleCheckedCount();
            public int GetHiddenCheckedCount() => IsGrid ? _form._gridList!.GetHiddenCheckedCount() : _form._cardList.GetHiddenCheckedCount();
            public int GetVisibleEntryCount() => IsGrid ? _form._gridList!.GetVisibleEntryCount() : _form._cardList.GetVisibleEntryCount();

            public void RestoreCheckedIds(IEnumerable<string> ids)
            {
                if (IsGrid) _form._gridList!.RestoreCheckedIds(ids);
                else _form._cardList.RestoreCheckedIds(ids);
            }

            public void RestoreSelectionState(string? focus, string? anchor)
            {
                if (IsGrid) _form._gridList!.RestoreSelectionState(focus, anchor);
                else _form._cardList.RestoreSelectionState(focus, anchor);
            }

            public void PruneCheckedToExisting(IEnumerable<string> ids)
            {
                if (IsGrid) _form._gridList!.PruneCheckedToExisting(ids);
                else _form._cardList.PruneCheckedToExisting(ids);
            }

            public void SelectAllCards()
            {
                if (IsGrid) _form._gridList!.SelectAllCards();
                else _form._cardList.SelectAllCards();
            }

            public void ClearCheckedCards()
            {
                if (IsGrid) _form._gridList!.ClearCheckedCards();
                else _form._cardList.ClearCheckedCards();
            }

            public void InvertSelection()
            {
                if (IsGrid) _form._gridList!.InvertSelection();
                else _form._cardList.InvertSelection();
            }

            public void UncheckCards(IEnumerable<string> ids)
            {
                if (IsGrid) _form._gridList!.UncheckCards(ids);
                else _form._cardList.UncheckCards(ids);
            }

            public void SetEntryUnmasked(string id, bool unmasked)
            {
                if (IsGrid) _form._gridList!.SetEntryUnmasked(id, unmasked);
                else _form._cardList.SetEntryUnmasked(id, unmasked);
            }

            public void ResetInteractionState()
            {
                if (IsGrid) _form._gridList!.ResetInteractionState();
                else _form._cardList.ResetInteractionState();
            }

            public bool IsDragging => IsGrid ? _form._gridList!.IsDragging : _form._cardList.IsDragging;

            public void SetDragArmed(bool armed)
            {
                if (IsGrid) _form._gridList!.DragArmed = armed;
                else _form._cardList.DragArmed = armed;
            }

            public bool DragArmed => IsGrid ? _form._gridList!.DragArmed : _form._cardList.DragArmed;
        }

        public MainForm(AppSettings? settings = null)
        {
            _settings = settings ?? AppSettings.Load();

            Text = "Clipboard App " + AppVersion.Display;
            Width = 920;
            Height = 680;
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = _settings.AlwaysOnTop;
            _appIcon = AppIcon.Load(32);
            Icon = _appIcon;

            CleanDropFiles();

            _repo = new SqliteClipboardRepository
            {
                DeduplicationMode = _settings.GetDeduplicationMode()
            };
            _search = new SearchService();
            _apiVault = new ClipboardApp.Core.Security.ApiVaultService();
            _apiKeyRepo = new ApiKeyRepository(vault: _apiVault);
            _monitor = new ClipboardMonitor();
            _monitor.ClipboardChanged += OnClipboardChanged;
            _thumbnails = new ThumbnailCache();

            _tray = new TrayIconManager(this);
            _tray.PauseToggled += (_, _) => _monitor.Paused = _tray.IsPaused;
            _tray.OpenSettingsRequested += (_, _) =>
            {
                ShowAppNearCursor();
                ShowOptions();
            };
            _tray.OpenVaultRequested += (_, _) =>
            {
                ShowAppNearCursor();
                ShowApiVault();
            };
            _tray.ClearHistoryRequested += (_, _) =>
            {
                if (ThemedMessageBox.Show("Are you sure you want to clear all unpinned entries?", "Clear History",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _repo.ClearAll(includePinned: false);
                    ReloadSnapshot(force: true);
                }
            };

            CreateTimers();
            BuildModernUi();
            // Thumbnails are decoded lazily from already-decrypted bytes, so the
            // list shows real screenshots without loading every image blob.
            _cardList.ImageBytesProvider = e => _repo.GetImageData(e.Id);
            _cardList.Thumbnails = _thumbnails;
            ApplySplitterLayout();
            ApplyTheme();
            ApplyDragSettings();
            ReloadSnapshot(force: true);

            if (_settings.PersistSelectionAcrossRestarts)
            {
                Accessor.RestoreCheckedIds(_settings.RememberedSelectedIds);
                Accessor.RestoreSelectionState(_settings.RememberedFocusedId, _settings.RememberedAnchorId);
                Accessor.PruneCheckedToExisting(_snapshot.Select(e => e.Id));
                UpdateBatchBar();
            }

            // Run initial prune
            _repo.PruneEntries(_settings.MaxHistoryCount, _settings.MaxHistoryAgeDays);
            _repo.ClearExpired();
        }

        private void CreateTimers()
        {
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                if (_cardList != null && _cardList.IsDragging)
                {
                    _refreshTimer.Start();
                    return;
                }
                ReloadSnapshot(force: true);
            };

            _splitterSaveTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _splitterSaveTimer.Tick += (_, _) =>
            {
                _splitterSaveTimer.Stop();
                _settings.Save();
            };

            _statusResetTimer = new System.Windows.Forms.Timer { Interval = 4500 };
            _statusResetTimer.Tick += (_, _) =>
            {
                _statusResetTimer.Stop();
                _statusLabel.Text = DefaultStatusText;
            };

            _singleClickTimer = new System.Windows.Forms.Timer { Interval = SystemInformation.DoubleClickTime + 30 };
            _singleClickTimer.Tick += (_, _) =>
            {
                _singleClickTimer.Stop();
                var entry = _singleClickEntry;
                _singleClickEntry = null;
                if (entry != null) PasteEntry(entry);
            };

            _captureRetryTimer = new System.Windows.Forms.Timer { Interval = 90 };
            _captureRetryTimer.Tick += (_, _) =>
            {
                _captureRetryTimer.Stop();
                HandleClipboardUpdate();
            };

            _apiClipboardClearTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _apiClipboardClearTimer.Tick += OnApiClipboardClearTick;
        }

        private void OnApiClipboardClearTick(object? sender, EventArgs e)
        {
            _apiClipboardClearTimer.Stop();
            if (!_settings.ClearClipboardAfterApiCopy) return;
            if (NativeMethods.GetClipboardSequenceNumber() != _apiClipboardSequence) return;

            // Suppress the resulting clipboard update so the cleared value does
            // not create a history event.
            _suppressCaptureUntilUtc = DateTime.UtcNow.AddSeconds(2);
            try { Clipboard.Clear(); } catch { }
            ShowTransientStatus("API key clipboard cleared.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            bool isDark = _settings.Theme == "Dark" || (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            ModernPalette.ApplyDwmStyling(Handle, isDark);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RegisterHotkey();

            // The app lives in the tray. Showing the window on launch is opt-out:
            // with the default StartMinimizedToTray the first visible surface is
            // the tray icon and the global hotkey.
            if (_settings.StartMinimizedToTray)
            {
                HideToTray();
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            SetDragArmed(false);
            _singleClickTimer.Stop();
            _singleClickEntry = null;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                SetDragArmed(false);
            }
            ApplySplitterLayout();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.WmShowApp && Program.WmShowApp != 0)
            {
                ShowAppNearCursor();
                return;
            }

            _hotkey?.ProcessMessage(ref m);
            base.WndProc(ref m);
        }

        private void RegisterHotkey()
        {
            _hotkey?.Dispose();
            _hotkey = new HotkeyManager(this);
            // The hotkey is a toggle: the first press brings the window forward
            // with focus, the next press sends it back to the tray.
            _hotkey.HotkeyPressed += (_, _) => ToggleAppWindow();

            if (!_hotkey.Register(_settings.Hotkey, out var err))
            {
                UpdateWindowTitle(hotkeyError: err);
            }
            else
            {
                UpdateWindowTitle();
            }
        }

        private void UpdateWindowTitle(string? hotkeyError = null)
        {
            string hotkey = HotkeyParser.ToDisplayString(_settings.Hotkey);
            Text = string.IsNullOrEmpty(hotkeyError)
                ? $"Clipboard App {AppVersion.Display} ({hotkey})"
                : $"Clipboard App {AppVersion.Display} (Hotkey unavailable — click for {hotkey})";
        }

        /// <summary>
        /// Global hotkey entry point. Showing focuses the search box; a second
        /// press hides the window back to the tray without quitting.
        /// </summary>
        public void ToggleAppWindow()
        {
            bool isShowing = Visible
                             && WindowState != FormWindowState.Minimized
                             && (NativeMethods.GetForegroundWindow() == Handle || ContainsFocus || ActiveForm == this);

            if (isShowing)
            {
                HideToTray();
                return;
            }

            ShowAppNearCursor();
        }

        /// <summary>Hides the window to the tray, leaving the app capturing in the background.</summary>
        public void HideToTray()
        {
            SetDragArmed(false);
            _singleClickTimer.Stop();
            _singleClickEntry = null;
            Hide();
            ShowInTaskbar = false;
        }

        public void ShowAppNearCursor()
        {
            var currentFg = NativeMethods.GetForegroundWindow();
            if (IsUsableTarget(currentFg))
            {
                _lastActiveWindow = currentFg;
                TargetWindowTracker.Instance.RecordManualWindow(currentFg);
            }

            var cursor = Cursor.Position;
            var screen = Screen.FromPoint(cursor);

            // While hidden to the tray, show at the last position the user chose,
            // but only if that position is still visible on a connected screen.
            bool hiddenToTray = !Visible && !ShowInTaskbar;
            bool rememberedPositionIsOnScreen =
                hiddenToTray &&
                WindowState == FormWindowState.Normal &&
                Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(Bounds));

            if (!rememberedPositionIsOnScreen)
            {
                int x = cursor.X - (Width / 2);
                int y = cursor.Y - 50;

                if (x + Width > screen.WorkingArea.Right) x = screen.WorkingArea.Right - Width;
                if (x < screen.WorkingArea.Left) x = screen.WorkingArea.Left;
                if (y + Height > screen.WorkingArea.Bottom) y = screen.WorkingArea.Bottom - Height;
                if (y < screen.WorkingArea.Top) y = screen.WorkingArea.Top;

                Location = new Point(x, y);
            }

            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            ForceForeground();
            SetDragArmed(false);

            // Opening the window is an explicit refresh point. The database is not
            // touched again while the list is simply being browsed.
            ReloadSnapshot(force: _snapshotDirty);

            _searchBox.Focus();
            _searchBox.SelectAll();
            UpdateTargetBadge();
        }

        private void ApplySplitterLayout()
        {
            if (_splitContainer == null || _applyingSplitter) return;

            int total = _splitContainer.ClientSize.Width - _splitContainer.SplitterWidth;
            if (total <= 80) return;

            float ratio = _settings.SplitterRatio > 0.15f && _settings.SplitterRatio < 0.85f
                ? _settings.SplitterRatio
                : 0.50f;
            int distance = (int)(total * ratio);
            int clamped = Math.Clamp(distance, 150, Math.Max(150, total - 150));
            if (Math.Abs(_splitContainer.SplitterDistance - clamped) <= 2) return;

            try
            {
                _applyingSplitter = true;
                _splitContainer.SplitterDistance = clamped;
            }
            finally
            {
                _applyingSplitter = false;
            }
        }

        // ------------------------------------------------------------------ capture

        private void OnClipboardChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (DateTime.UtcNow < _suppressCaptureUntilUtc) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnClipboardChanged(sender, e))); } catch { }
                return;
            }

            _captureRetryCount = 0;
            HandleClipboardUpdate();
        }

        private void ScheduleCaptureRetry()
        {
            if (_captureRetryCount >= 3) return;
            _captureRetryCount++;
            _captureRetryTimer.Interval = 90 * _captureRetryCount;
            _captureRetryTimer.Stop();
            _captureRetryTimer.Start();
        }

        private void HandleClipboardUpdate()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (DateTime.UtcNow < _suppressCaptureUntilUtc) return;

            IDataObject? data;
            try
            {
                data = Clipboard.GetDataObject();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                ScheduleCaptureRetry();
                return;
            }
            catch
            {
                return;
            }

            if (data == null) return;

            if (_settings.RespectPrivacyFlags && ClipboardPrivacyFilter.IsExcludedByPrivacyFlags(data))
            {
                return;
            }

            var ownerHwnd = NativeMethods.GetClipboardOwner();
            if (ownerHwnd == IntPtr.Zero) ownerHwnd = NativeMethods.GetForegroundWindow();
            var sourceApp = NativeMethods.GetProcessNameForWindow(ownerHwnd) ?? "";

            if (ClipboardPrivacyFilter.IsSourceAppExcluded(sourceApp, _settings.IgnoredApps)) return;

            try
            {
                var entry = new ClipboardEntry { SourceApp = sourceApp };

                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        entry.Format = "file";
                        entry.Content = string.Join(Environment.NewLine, files);
                        entry.Alias = Path.GetFileName(files[0]) + (files.Length > 1 ? $" (+{files.Length - 1} more)" : "");
                    }
                }
                else if (ImageHelper.TryGetImageFromClipboard(data, out var pngBytes, out var capturedImg))
                {
                    using (capturedImg)
                    {
                        if (pngBytes == null || pngBytes.Length == 0) return;
                        var maxBytes = Math.Max(1, _settings.MaxImageSizeMb) * 1024L * 1024L;
                        if (pngBytes.LongLength > maxBytes)
                        {
                            ShowTransientStatus($"Image skipped (larger than {_settings.MaxImageSizeMb} MB)");
                            return;
                        }

                        entry.ImageData = pngBytes;
                        entry.Format = "image";
                        entry.HasImage = true;
                        entry.Alias = $"Screenshot {DateTime.Now:HH:mm:ss}";
                    }
                }
                else if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        entry.Content = text;
                        var trimmed = text.Trim();
                        if ((trimmed.StartsWith("#") && (trimmed.Length == 4 || trimmed.Length == 7 || trimmed.Length == 9) &&
                             System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")) ||
                            trimmed.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.Format = "color";
                        }
                        else
                        {
                            entry.Format = "text";
                        }
                    }
                }
                else if (data.GetDataPresent(DataFormats.Html))
                {
                    var rawHtml = Clipboard.GetText(TextDataFormat.Html);
                    entry.Content = CleanHtmlFragment(rawHtml);
                    entry.Format = "html";
                }
                else if (data.GetDataPresent(DataFormats.Rtf))
                {
                    entry.Content = Clipboard.GetText(TextDataFormat.Rtf);
                    entry.Format = "rtf";
                }

                if (string.IsNullOrEmpty(entry.Content) && entry.ImageData == null) return;

                if (entry.Format == "image" && !_settings.CaptureImages) return;
                if (entry.Format == "file" && !_settings.CaptureFiles) return;
                if ((entry.Format == "text" || entry.Format == "color" || entry.Format == "html" || entry.Format == "rtf") && !_settings.CaptureText) return;

                if (_settings.CheckSensitiveData && !string.IsNullOrEmpty(entry.Content))
                {
                    if (ClipboardPrivacyFilter.ContainsSensitiveData(entry.Content, out _))
                    {
                        entry.IsSensitive = true;
                        if (_settings.AutoClearSensitiveMinutes > 0)
                        {
                            entry.ExpiresAt = DateTime.UtcNow.AddMinutes(_settings.AutoClearSensitiveMinutes);
                        }
                    }
                }

                // The repository performs the one and only dedupe decision. This
                // call does not reorder the current UI unless the user explicitly
                // enabled MoveToTop and refreshed.
                // The insert call has returned, so the capture is confirmed.
                _repo.InsertEntry(entry);

                _snapshotDirty = true;
                _pendingCaptureCount++;
                bool canRefreshNow = _settings.LiveRefresh && Visible && WindowState != FormWindowState.Minimized;
                if (canRefreshNow)
                {
                    // Confirmed-copy refresh: one coalesced reload, not a rebuild
                    // on every WM_CLIPBOARDUPDATE. Existing rows keep their Seq.
                    _refreshTimer.Stop();
                    _refreshTimer.Start();
                }
                else
                {
                    ShowTransientStatus($"{_pendingCaptureCount} clipboard capture{(_pendingCaptureCount == 1 ? "" : "s")} since refresh — press F5 to refresh");
                }

                if (_settings.PlaySoundOnCapture) SystemSounds.Beep.Play();
                if (_settings.ShowToastOnCapture)
                {
                    var preview = entry.Format == "image"
                        ? "[Image]"
                        : (entry.Content != null && entry.Content.Length > 40 ? entry.Content[..40] + "..." : entry.Content ?? "");
                    _tray.ShowNotification("Captured to Clipboard", preview);
                }
            }
            catch
            {
                // Never let a malformed clipboard object take down the app.
            }
        }

        private static string CleanHtmlFragment(string rawHtml)
        {
            if (string.IsNullOrEmpty(rawHtml)) return "";
            int startFrag = rawHtml.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
            int endFrag = rawHtml.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
            if (startFrag >= 0 && endFrag > startFrag)
            {
                startFrag += "<!--StartFragment-->".Length;
                return rawHtml.Substring(startFrag, endFrag - startFrag).Trim();
            }
            if (rawHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
            {
                int htmlTag = rawHtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
                if (htmlTag >= 0) return rawHtml.Substring(htmlTag).Trim();
            }
            return rawHtml;
        }

        // ------------------------------------------------------------------ snapshot

        private void ReloadSnapshot(bool force)
        {
            if (!force && _snapshotLoaded && !_snapshotDirty) return;

            _snapshot.Clear();
            _snapshot.AddRange(_repo.GetAllEntries(includeImageData: false));
            _snapshotLoaded = true;
            _snapshotDirty = false;
            _pendingCaptureCount = 0;
            if (_statusLabel != null) _statusLabel.Text = DefaultStatusText;
            RefreshAppFilterOptions();
            RebuildList();
        }

        private void RebuildList()
        {
            if (_cardList == null || _cardList.IsDragging) return;

            var query = _searchBox.Text;
            var filter = _pillBar.SelectedFilter;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["All"] = _snapshot.Count,
                ["Text"] = _snapshot.Count(e => e.Format == "text"),
                ["Image"] = _snapshot.Count(e => e.Format == "image" || e.HasImage),
                ["Link"] = _snapshot.Count(e => (e.Format == "text" || e.Format == "link") && e.Content != null &&
                    (e.Content.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.Content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))),
                ["File"] = _snapshot.Count(e => e.Format == "file"),
                ["Color"] = _snapshot.Count(e => e.Format == "color" || (e.Format == "text" && e.Content != null &&
                    e.Content.Trim().StartsWith("#") && (e.Content.Trim().Length == 4 || e.Content.Trim().Length == 7 || e.Content.Trim().Length == 9))),
                ["Pinned"] = _snapshot.Count(e => e.Pinned)
            };
            _pillBar.UpdateCounts(counts);

            IEnumerable<ClipboardEntry> targetEntries = _snapshot;
            if (filter.Equals("Pinned", StringComparison.OrdinalIgnoreCase))
            {
                targetEntries = _snapshot.Where(e => e.Pinned);
            }

            targetEntries = FilterService.Apply(
                targetEntries,
                _settings.FilterDate,
                _settings.FilterType,
                _settings.FilterApp);

            var filtered = _search.FilterEntries(
                targetEntries,
                query,
                _regexToggle.Checked,
                filter.Equals("Pinned", StringComparison.OrdinalIgnoreCase) ? "All" : filter);

            string? selectedId = (_cardList.SelectedItem as ClipboardEntry)?.Id;
            string? anchorId = _cardList.GetSelectionAnchorId();
            int topIndex = _cardList.TopIndex;

            _suppressListEvents = true;
            _cardList.BeginUpdate();
            try
            {
                _cardList.Items.Clear();
                AddGroupedItems(filtered);

                int restoreIndex = FindEntryIndex(selectedId);
                if (restoreIndex < 0) restoreIndex = FirstEntryIndex();
                if (restoreIndex >= 0) _cardList.SelectedIndex = restoreIndex;

                if (_cardList.Items.Count > 0)
                {
                    int maxTop = Math.Max(0, _cardList.Items.Count - 1);
                    _cardList.TopIndex = Math.Clamp(topIndex, 0, maxTop);
                }
            }
            finally
            {
                _cardList.EndUpdate();
                _suppressListEvents = false;
            }

            _cardList.ResetInteractionState();
            _cardList.RestoreSelectionState(selectedId, anchorId);
            _cardList.PruneCheckedToExisting(_snapshot.Select(e => e.Id));

            // Keep the grid presentation in lock-step with the filtered snapshot.
            if (_gridList != null)
            {
                RebuildGridItems();
            }

            UpdatePreview();
            UpdateBatchBar();
        }

        private void AddGroupedItems(IEnumerable<ClipboardEntry> filtered)
        {
            if (_settings.GroupingMode == "Date")
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var sevenDaysAgo = today.AddDays(-7);

                var groups = filtered.GroupBy(e =>
                {
                    var d = e.CreatedAt.Date;
                    if (d == today) return "Today";
                    if (d == yesterday) return "Yesterday";
                    if (d >= sevenDaysAgo) return "Past 7 Days";
                    return "Older";
                }).ToList();

                foreach (var key in new[] { "Today", "Yesterday", "Past 7 Days", "Older" })
                {
                    var grp = groups.FirstOrDefault(g => g.Key == key);
                    if (grp == null || !grp.Any()) continue;
                    var list = grp.ToList();
                    var header = new CardGroupHeader { Title = key, Count = list.Count, Key = key };
                    foreach (var item in list) header.EntryIds.Add(item.Id);
                    _cardList.Items.Add(header);
                    foreach (var item in list) _cardList.Items.Add(item);
                }
            }
            else if (_settings.GroupingMode == "App")
            {
                var groups = filtered
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.SourceApp) ? "Unknown Application" : e.SourceApp)
                    .OrderByDescending(g => g.Count());
                foreach (var grp in groups)
                {
                    var list = grp.ToList();
                    var header = new CardGroupHeader { Title = grp.Key, Count = list.Count, Key = grp.Key };
                    foreach (var item in list) header.EntryIds.Add(item.Id);
                    _cardList.Items.Add(header);
                    foreach (var item in list) _cardList.Items.Add(item);
                }
            }
            else if (_settings.GroupingMode == "Type")
            {
                var groups = filtered.GroupBy(e =>
                {
                    if (e.Format == "image" || e.HasImage) return "Images & Screenshots";
                    if (e.Format == "color" || (e.Content != null && e.Content.Trim().StartsWith("#") &&
                        (e.Content.Trim().Length == 4 || e.Content.Trim().Length == 7 || e.Content.Trim().Length == 9))) return "Colors";
                    if (e.Format == "file") return "Files";
                    if (e.Content != null && (e.Content.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        e.Content.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return "Links & URLs";
                    return "Text Snippets";
                }).OrderBy(g => g.Key);

                foreach (var grp in groups)
                {
                    var list = grp.ToList();
                    var header = new CardGroupHeader { Title = grp.Key, Count = list.Count, Key = grp.Key };
                    foreach (var item in list) header.EntryIds.Add(item.Id);
                    _cardList.Items.Add(header);
                    foreach (var item in list) _cardList.Items.Add(item);
                }
            }
            else
            {
                foreach (var item in filtered) _cardList.Items.Add(item);
            }
        }

        private int FindEntryIndex(string? id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _cardList.Items.Count; i++)
            {
                if (_cardList.Items[i] is ClipboardEntry e && e.Id == id) return i;
            }
            return -1;
        }

        private int FirstEntryIndex()
        {
            for (int i = 0; i < _cardList.Items.Count; i++)
            {
                if (_cardList.Items[i] is ClipboardEntry) return i;
            }
            return -1;
        }

        private void UpdatePreview()
        {
            if (_cardList.SelectedItem is ClipboardEntry entry)
            {
                byte[]? imgData = null;
                if (entry.Format == "image" || entry.HasImage)
                {
                    imgData = _repo.GetImageData(entry.Id);
                }
                _previewPanel.DisplayEntry(entry, imgData);
            }
            else
            {
                _previewPanel.DisplayEntry(null);
            }
        }
        private void ShowTransientStatus(string message)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = message;
            _statusResetTimer.Stop();
            _statusResetTimer.Start();
        }

        private void SaveSelectionMemory()
        {
            if (!_settings.PersistSelectionAcrossRestarts)
            {
                _settings.RememberedSelectedIds.Clear();
                _settings.RememberedFocusedId = "";
                _settings.RememberedAnchorId = "";
                return;
            }

            _settings.RememberedSelectedIds = Accessor.GetCheckedIds().ToList();
            _settings.RememberedFocusedId = Accessor.GetFocusedEntryId() ?? "";
            _settings.RememberedAnchorId = Accessor.GetSelectionAnchorId() ?? "";
            _settings.Save();
        }

        // ------------------------------------------------------------------ disposal

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSelectionMemory();

            // WM_CLOSE from the title-bar X can arrive with UserClosing; some
            // Windows shells report None. Both hide to tray; only an explicit
            // Application.Exit (ApplicationExitCall) should terminate.
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None)
            {
                SetDragArmed(false);
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TargetWindowTracker.Instance.Dispose();
                _cardContextMenu?.Dispose();
                _toolTip?.Dispose();
                _searchDebounceTimer?.Dispose();
                _maintenanceTimer?.Dispose();
                _refreshTimer?.Dispose();
                _splitterSaveTimer?.Dispose();
                _statusResetTimer?.Dispose();
                _singleClickTimer?.Dispose();
                _captureRetryTimer?.Dispose();
                _apiClipboardClearTimer?.Dispose();
                _monitor.Dispose();
                _apiKeyRepo?.Dispose();
                _apiVault?.Lock();
                _tray.Dispose();
                _appIcon?.Dispose();
                _hotkey?.Dispose();
                _repo.Dispose();
                _thumbnails?.Dispose();
                _apiVaultDialog?.Dispose();
                CleanDropFiles();
            }
            base.Dispose(disposing);
        }
    }
}

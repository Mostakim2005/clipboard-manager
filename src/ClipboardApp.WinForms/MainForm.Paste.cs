using ClipboardApp.Core.Interop;
using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using ClipboardApp.WinForms.Dialogs;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    public partial class MainForm
    {
        // ------------------------------------------------------------------ activation

        private void OnCardActivated(ClipboardEntry entry)
        {
            _singleClickTimer.Stop();
            _singleClickEntry = null;

            // The dedicated toggle is the only gate for double-click paste.
            // PasteTrigger still controls single-click; Enter always pastes.
            if (!_settings.EnableDoubleClickPaste) return;

            if (Accessor.GetCheckedIds().Count > 1)
            {
                BatchMergeAndPaste(plainTextOnly: false, forcePicker: true);
                return;
            }

            PasteEntry(entry, forcePicker: true);
        }

        private void OnCardClicked(ClipboardEntry entry)
        {
            if (_settings.PasteTrigger != "SingleClick") return;
            _singleClickEntry = entry;
            _singleClickTimer.Stop();
            _singleClickTimer.Start();
        }

        // ------------------------------------------------------------------ paste

        private void PasteSelectedEntry(bool plainTextOnly = false)
        {
            var checkedIds = Accessor.GetCheckedIds();

            if (checkedIds.Count > 1)
            {
                BatchMergeAndPaste(plainTextOnly);
                return;
            }

            if (checkedIds.Count == 1)
            {
                var checkedEntry = Accessor.GetEntryById(checkedIds.First());
                if (checkedEntry != null)
                {
                    PasteEntry(checkedEntry, plainTextOnly, CompletePasteSelection);
                    return;
                }
            }

            if (_cardList.SelectedItem is ClipboardEntry entry)
            {
                PasteEntry(entry, plainTextOnly);
            }
        }

        private void PasteEntry(
            ClipboardEntry entry,
            bool plainTextOnly = false,
            Action? onSuccess = null,
            bool forcePicker = false)
        {
            if (entry == null) return;

            bool forcePlainText = plainTextOnly || _settings.PasteAsPlainText;
            ClipboardEntry payload = entry;
            byte[]? imageBytes = null;

            if (entry.Format == "image" || entry.HasImage)
            {
                imageBytes = LoadImageBytes(entry);
                if (imageBytes == null)
                {
                    ShowTransientStatus("Image data is unavailable.");
                    return;
                }

                if (forcePlainText)
                {
                    ShowTransientStatus("An image cannot be pasted as plain text.");
                    return;
                }
            }
            else if (forcePlainText)
            {
                var plain = BuildPlainTextPayload(entry);
                if (plain == null)
                {
                    ShowTransientStatus("This clip has no plain-text representation.");
                    return;
                }
                payload = plain;
            }

            var target = ResolveTargetWindow();
            if (forcePicker || _settings.PasteTargetMode == "Ask" || target == IntPtr.Zero)
            {
                var picked = ShowTargetPicker(
                    target == IntPtr.Zero
                        ? "No automatic paste target was detected. Choose a window, or use Copy only."
                        : "Choose the window that should receive this paste.");
                if (picked == IntPtr.Zero)
                {
                    if (PasteService.CopyEntry(payload, imageBytes))
                    {
                        _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
                        ShowTransientStatus("Copied to clipboard Ã¢â‚¬â€ choose a target or press Ctrl+V.");
                    }
                    else
                    {
                        ShowTransientStatus("The clipboard is busy. Try again.");
                    }
                    return;
                }
                target = picked;
            }

            _preferredTarget = target;
            PasteEntryToTarget(payload, imageBytes, target, onSuccess);
        }

        private void PasteEntryToTarget(ClipboardEntry payload, byte[]? imageBytes, IntPtr target, Action? onSuccess)
        {
            _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1800);
            SetDragArmed(false);
            Hide();

            BeginInvoke(new Action(async () =>
            {
                try
                {
                    await Task.Delay(50);
                    var result = PasteService.PasteEntryDetailed(payload, target, imageBytes);
                    if (result.Success)
                    {
                        onSuccess?.Invoke();
                        return;
                    }

                    HandlePasteFailure(result, target, retryTarget =>
                    {
                        PasteEntryToTarget(payload, imageBytes, retryTarget, onSuccess);
                    });
                }
                catch
                {
                    // The window may have been disposed while the paste was in flight.
                }
            }));
        }

        private void HandlePasteFailure(PasteService.PasteAttemptResult result, IntPtr failedTarget, Action<IntPtr> retry)
        {
            try
            {
                Show();
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
                Activate();
                ForceForeground();
                UpdateTargetBadge();

                string message = string.IsNullOrWhiteSpace(result.Message)
                    ? "The target window did not take focus."
                    : result.Message;
                ShowTransientStatus("Paste not confirmed Ã¢â‚¬â€ " + message);
                _tray.ShowNotification("Paste not confirmed", message + " The clip is still on the clipboard.");

                var chosen = ShowTargetPicker(
                    message + Environment.NewLine +
                    "The clip is still on the clipboard. Choose another window to retry.");
                if (chosen != IntPtr.Zero)
                {
                    _preferredTarget = chosen;
                    retry(chosen);
                }
            }
            catch
            {
                // Never let target recovery take down the app.
            }
        }

        private IntPtr ShowTargetPicker(string message)
        {
            try
            {
                var candidates = TargetWindowTracker.Instance.GetCandidates(Handle);
                using var dialog = new TargetPickerDialog(candidates, message, IsDarkTheme())
                {
                    Owner = this,
                    TopMost = true
                };
                return dialog.ShowDialog(this) == DialogResult.OK
                    ? dialog.SelectedTarget
                    : IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private bool IsDarkTheme() =>
            _settings.Theme == "Dark" ||
            (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());

        private ClipboardEntry? BuildPlainTextPayload(ClipboardEntry entry)
        {
            string text = entry.Format switch
            {
                "html" => PasteService.HtmlToPlainText(entry.Content),
                "rtf" => PasteService.RtfToPlainText(entry.Content),
                _ => entry.Content ?? string.Empty
            };

            if (string.IsNullOrEmpty(text)) return null;

            return new ClipboardEntry
            {
                Id = entry.Id,
                Format = "text",
                Content = text,
                SourceApp = entry.SourceApp
            };
        }

        private byte[]? LoadImageBytes(ClipboardEntry entry)
        {
            return entry.ImageData ?? _repo.GetImageData(entry.Id);
        }

        private void CopyEntryToClipboard(ClipboardEntry entry)
        {
            var bytes = (entry.Format == "image" || entry.HasImage) ? LoadImageBytes(entry) : null;
            if (PasteService.CopyEntry(entry, bytes))
            {
                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
                ShowTransientStatus("Copied to clipboard.");
            }
            else
            {
                ShowTransientStatus("The clipboard is busy. Try again.");
            }
        }

        /// <summary>
        /// Copies the current multi-selection as one payload. Windows has one
        /// clipboard, so "copy many" is a merged text payload, a rich text +
        /// image payload, or one file list.
        /// </summary>
        private void CopySelectedMerged(string? formatOverride = null)
        {
            var targetIds = new HashSet<string>(Accessor.GetCheckedOrSelectedIds());
            if (targetIds.Count == 0)
            {
                ShowTransientStatus("Select one or more clips first.");
                return;
            }

            var entries = _cardList.Items
                .OfType<ClipboardEntry>()
                .Where(e => targetIds.Contains(e.Id))
                .ToList();
            if (entries.Count < targetIds.Count)
            {
                var visibleIds = new HashSet<string>(entries.Select(e => e.Id));
                entries.AddRange(_snapshot.Where(e => targetIds.Contains(e.Id) && !visibleIds.Contains(e.Id)));
            }
            if (entries.Count == 0) return;

            bool hasImages = entries.Any(RichMergeService.IsImageEntry);
            bool hasText = entries.Any(e => !RichMergeService.IsImageEntry(e));
            bool mixed = hasImages && hasText;

            string format = BatchCopyService.NormalizeFormat(formatOverride ?? _settings.BatchCopyFormat);
            bool richRequested = mixed &&
                                 _settings.EnableMixedMerge &&
                                 (format == "Rich" ||
                                  (format == "Plain" && formatOverride == null));

            if (richRequested)
            {
                var rich = BuildRichMerge(entries);
                if (rich.IncludedImageCount > 0)
                {
                    if (PasteService.CopyRich(rich.HtmlFragment, rich.Rtf, rich.PlainText))
                    {
                        _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1800);
                        if (rich.SkippedImageEntryIds.Count > 0)
                        {
                            Accessor.UncheckCards(rich.SkippedImageEntryIds);
                        }

                        ShowTransientStatus(
                            $"Copied {rich.TextEntryCount} text clip(s) + {rich.IncludedImageCount} image(s) as rich content." +
                            (rich.SkippedImageEntryIds.Count > 0
                                ? $" {rich.SkippedImageEntryIds.Count} image(s) could not be embedded and were deselected."
                                : ""));
                        return;
                    }

                    ShowTransientStatus("The clipboard is busy. Try again.");
                    return;
                }

                WarnMixedFallback(
                    rich.SkippedImageEntryIds.Count > 0 ? rich.SkippedImageEntryIds : entries.Where(RichMergeService.IsImageEntry).Select(e => e.Id),
                    "The selected images could not be embedded in a rich payload.");
                // Fall through to the text-only payload.
            }
            else if (mixed)
            {
                string reason = format != "Plain" && format != "Rich"
                    ? $"The {format.ToUpperInvariant()} format cannot carry images."
                    : (!_settings.EnableMixedMerge
                        ? "Mixed text + image merging is disabled in Settings."
                        : "Plain-text mode is on.");
                WarnMixedFallback(
                    entries.Where(RichMergeService.IsImageEntry).Select(e => e.Id),
                    reason);
                // Fall through to the text-only payload.
            }

            var copyEntries = mixed
                ? entries.Where(e => !RichMergeService.IsImageEntry(e)).ToList()
                : entries;

            if (copyEntries.Count == 0)
            {
                ShowTransientStatus("Only images are selected. Enable mixed merge or copy an image individually.");
                return;
            }

            string effectiveFormat = format == "Rich" ? "Plain" : format;
            var plan = BatchPasteService.Build(copyEntries, _settings.GetBatchSeparator(), EnsureDropFile);

            bool copied;
            if (effectiveFormat == "Plain" && plan.FileOnly && plan.FilePaths.Count > 0)
            {
                var files = new StringCollection();
                files.AddRange(plan.FilePaths.ToArray());
                copied = PasteService.CopyFiles(files);
            }
            else
            {
                string text = BatchCopyService.Build(copyEntries, effectiveFormat, _settings.GetBatchSeparator());
                if (string.IsNullOrEmpty(text))
                {
                    ShowTransientStatus("Nothing to copy in the selected clips.");
                    return;
                }
                copied = PasteService.CopyText(text);
            }

            if (copied)
            {
                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1800);
                string label = effectiveFormat == "Plain" ? "merged" : effectiveFormat.ToUpperInvariant();
                ShowTransientStatus(copyEntries.Count == 1
                    ? "Copied to clipboard."
                    : $"Copied {copyEntries.Count} clips as one {label} payload.");
            }
            else
            {
                ShowTransientStatus("The clipboard is busy. Try again.");
            }
        }

        private RichMergeResult BuildRichMerge(IEnumerable<ClipboardEntry> entries) =>
            RichMergeService.Build(entries, _settings.GetBatchSeparator(), LoadImageBytes);

        private void WarnMixedFallback(IEnumerable<string> imageIds, string reason)
        {
            var ids = imageIds?.Distinct().ToList() ?? new List<string>();
            if (ids.Count > 0) Accessor.UncheckCards(ids);

            string message = ids.Count > 0
                ? $"{reason} Copied text only and deselected {ids.Count} image(s)."
                : $"{reason} Copied text only.";
            ShowTransientStatus(message);
            if (!Visible) _tray.ShowNotification("Mixed text + image merge", message);
        }

        private void CompletePasteSelection()
        {
            if (_settings.ClearSelectionAfterPaste)
            {
                Accessor.ClearCheckedCards();
            }
        }

        private void ShowApiVault()
        {
            if (!_settings.EnableApiVault)
            {
                ShowTransientStatus("API vault is disabled in Settings.");
                return;
            }

            // One vault window at a time; re-invoking focuses the open one.
            if (_apiVaultDialog != null && !_apiVaultDialog.IsDisposed)
            {
                if (_apiVaultDialog.WindowState == FormWindowState.Minimized)
                {
                    _apiVaultDialog.WindowState = FormWindowState.Normal;
                }
                _apiVaultDialog.Activate();
                return;
            }

            if (!EnsureApiVaultUnlocked()) return;

            var dialog = new ApiVaultDialog(_apiKeyRepo, _apiVault, _settings, IsDarkTheme())
            {
                Owner = this
            };
            dialog.CopyRequested += (_, text) =>
            {
                if (PasteService.CopyText(text))
                {
                    _suppressCaptureUntilUtc = DateTime.UtcNow.AddSeconds(3);
                    _apiClipboardSequence = NativeMethods.GetClipboardSequenceNumber();
                    _apiClipboardClearTimer.Stop();
                    if (_settings.ClearClipboardAfterApiCopy)
                    {
                        _apiClipboardClearTimer.Interval = Math.Max(5, _settings.ApiClipboardClearSeconds) * 1000;
                        _apiClipboardClearTimer.Start();
                    }
                }
                else
                {
                    ShowTransientStatus("The clipboard is busy. Try again.");
                }
            };
            dialog.VaultLocked += (_, _) => ShowTransientStatus("API vault locked.");
            dialog.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_apiVaultDialog, dialog)) _apiVaultDialog = null;
                ShowTransientStatus("API vault closed.");
            };

            _apiVaultDialog = dialog;

            // Modeless: the main window stays usable so the vault can be kept
            // open and moved aside while you work.
            dialog.Show(this);
        }

        private bool EnsureApiVaultUnlocked()
        {
            if (!_apiVault.IsLocked) return true;

            if (!_apiVault.IsPasswordProtected)
            {
                if (_apiVault.Unlock(""))
                {
                    _apiVault.ExtendUnlockWindow(_settings.ApiVaultAutoLockMinutes);
                    return true;
                }
                ThemedMessageBox.Show(this, "The vault key could not be opened with Windows DPAPI.",
                    "API Vault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            using var dialog = new MasterPasswordDialog(
                "Unlock API vault",
                "Enter the master password. The vault stays unlocked for " +
                $"{_settings.ApiVaultAutoLockMinutes} minutes after this, so you can work without re-entering it.",
                requireConfirmation: false,
                IsDarkTheme())
            { Owner = this };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            if (_apiVault.Unlock(dialog.Password))
            {
                // Timed unlock: keeps the vault open for a few minutes even if
                // nobody touches it, so reopening the window is friction-free.
                _apiVault.ExtendUnlockWindow(_settings.ApiVaultAutoLockMinutes);
                return true;
            }

            ThemedMessageBox.Show(this, "That master password is incorrect.", "API Vault",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // ------------------------------------------------------------------ batch paste

        private void BatchMergeAndPaste(bool plainTextOnly = false, bool forcePicker = false)
        {
            var targetIds = new HashSet<string>(Accessor.GetCheckedOrSelectedIds());
            if (targetIds.Count == 0) return;

            var selectedEntries = _cardList.Items
                .OfType<ClipboardEntry>()
                .Where(e => targetIds.Contains(e.Id))
                .ToList();

            if (selectedEntries.Count == 0)
            {
                selectedEntries = _snapshot
                    .Where(e => targetIds.Contains(e.Id))
                    .ToList();
            }

            if (selectedEntries.Count == 0) return;

            if (_settings.SequentialBatchPaste && !plainTextOnly)
            {
                SequentialBatchPaste(selectedEntries, forcePicker);
                return;
            }

            bool hasImages = selectedEntries.Any(RichMergeService.IsImageEntry);
            bool hasText = selectedEntries.Any(e => !RichMergeService.IsImageEntry(e));
            bool mixed = hasImages && hasText;
            bool plainMode = plainTextOnly || _settings.PasteAsPlainText;

            if (plainMode && !hasText)
            {
                ShowTransientStatus("An image cannot be pasted as plain text.");
                return;
            }

            bool richRequested = mixed &&
                                 _settings.EnableMixedMerge &&
                                 !plainTextOnly &&
                                 !_settings.PasteAsPlainText;
            bool pickerAlreadyResolved = false;

            if (richRequested)
            {
                var rich = BuildRichMerge(selectedEntries);
                if (rich.IncludedImageCount > 0)
                {
                    var target = ResolveTargetWindow();
                    if (forcePicker || _settings.PasteTargetMode == "Ask" || target == IntPtr.Zero)
                    {
                        var picked = ShowTargetPicker(
                            target == IntPtr.Zero
                                ? "No automatic paste target was detected. Choose a window, or copy only."
                                : "Choose the window that should receive the rich text + image paste.");
                        if (picked == IntPtr.Zero)
                        {
                            if (PasteService.CopyRich(rich.HtmlFragment, rich.Rtf, rich.PlainText))
                            {
                                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
                                ShowTransientStatus("Copied rich text + images Ã¢â‚¬â€ choose a target or press Ctrl+V.");
                            }
                            else
                            {
                                ShowTransientStatus("The clipboard is busy. Try again.");
                            }
                            return;
                        }

                        target = picked;
                        _preferredTarget = picked;
                        pickerAlreadyResolved = true;
                    }

                    if (PasteTargetInspector.GetKind(target) == PasteTargetKind.PlainText)
                    {
                        WarnMixedFallback(
                            rich.ImageEntryIds,
                            "The selected target looks like a plain-text editor that cannot accept embedded images.");
                        // Fall through to the text-only payload.
                    }
                    else
                    {
                        PasteRichToTarget(rich, target);
                        return;
                    }
                }
                else
                {
                    WarnMixedFallback(
                        rich.SkippedImageEntryIds.Count > 0
                            ? rich.SkippedImageEntryIds
                            : selectedEntries.Where(RichMergeService.IsImageEntry).Select(e => e.Id),
                        "The selected images could not be embedded in a rich payload.");
                    // Fall through to the text-only payload.
                }
            }
            else if (mixed)
            {
                string reason = plainTextOnly || _settings.PasteAsPlainText
                    ? "Plain-text mode is on."
                    : "Mixed text + image merging is disabled in Settings.";
                WarnMixedFallback(
                    selectedEntries.Where(RichMergeService.IsImageEntry).Select(e => e.Id),
                    reason);
                // Fall through to the text-only payload.
            }

            var textEntries = mixed
                ? selectedEntries.Where(e => !RichMergeService.IsImageEntry(e)).ToList()
                : selectedEntries;

            if (textEntries.Count == 0)
            {
                ShowTransientStatus("Only images are selected. Enable mixed merge or paste an image individually.");
                return;
            }

            var plan = BatchPasteService.Build(
                textEntries,
                _settings.GetBatchSeparator(),
                EnsureDropFile);

            if (!plan.HasContent)
            {
                ShowTransientStatus("Nothing to paste in the selected clips.");
                return;
            }

            if (plan.FileOnly && !plainTextOnly && !_settings.PasteAsPlainText)
            {
                var files = new StringCollection();
                files.AddRange(plan.FilePaths.ToArray());

                var target = ResolveTargetWindow();
                if (forcePicker || _settings.PasteTargetMode == "Ask" || target == IntPtr.Zero)
                {
                    var picked = ShowTargetPicker(
                        target == IntPtr.Zero
                            ? "No automatic paste target was detected. Choose a window, or copy only."
                            : "Choose the window that should receive the file paste.");
                    if (picked == IntPtr.Zero)
                    {
                        if (PasteService.CopyFiles(files))
                        {
                            _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
                            ShowTransientStatus("Copied files to clipboard Ã¢â‚¬â€ no paste target detected.");
                        }
                        else
                        {
                            ShowTransientStatus("The clipboard is busy. Try again.");
                        }
                        return;
                    }

                    target = picked;
                    _preferredTarget = picked;
                    pickerAlreadyResolved = true;
                }

                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1800);
                SetDragArmed(false);
                Hide();

                BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await Task.Delay(50);
                        bool ok = PasteService.PasteFiles(files, target);
                        if (ok) CompletePasteSelection();
                        else _tray.ShowNotification("Paste not confirmed", "The target window did not take focus.");
                    }
                    catch { }
                }));
                return;
            }

            string mergedText = BatchCopyService.Build(
                textEntries,
                "Plain",
                _settings.GetBatchSeparator());

            if (string.IsNullOrEmpty(mergedText))
            {
                ShowTransientStatus("Nothing to paste in the selected clips.");
                return;
            }

            var mergedEntry = new ClipboardEntry
            {
                Format = "text",
                Content = mergedText,
                Alias = "Merged clip"
            };

            PasteEntry(
                mergedEntry,
                plainTextOnly,
                CompletePasteSelection,
                forcePicker && !pickerAlreadyResolved);
        }

        private void PasteRichToTarget(RichMergeResult rich, IntPtr target)
        {
            _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(2200);
            SetDragArmed(false);
            Hide();

            BeginInvoke(new Action(async () =>
            {
                try
                {
                    await Task.Delay(50);
                    var result = PasteService.PasteRichDetailed(
                        rich.HtmlFragment,
                        rich.Rtf,
                        rich.PlainText,
                        target);

                    if (result.Success)
                    {
                        CompletePasteSelection();
                        return;
                    }

                    HandlePasteFailure(result, target, retryTarget => PasteRichToTarget(rich, retryTarget));
                }
                catch
                {
                    // The window may have been disposed while the paste was in flight.
                }
            }));
        }

        private void SequentialBatchPaste(List<ClipboardEntry> entries, bool forcePicker = false)
        {
            if (entries.Count == 0) return;

            var target = ResolveTargetWindow();
            if (forcePicker || _settings.PasteTargetMode == "Ask" || target == IntPtr.Zero)
            {
                var picked = ShowTargetPicker(
                    target == IntPtr.Zero
                        ? "No automatic paste target was detected. Choose a window, or copy only."
                        : "Choose the window that should receive each paste.");
                if (picked == IntPtr.Zero)
                {
                    if (PasteService.CopyEntry(entries[0], LoadImageBytes(entries[0])))
                    {
                        _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
                        ShowTransientStatus("Copied the first clip Ã¢â‚¬â€ no paste target detected.");
                    }
                    return;
                }

                target = picked;
                _preferredTarget = picked;
            }

            _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(2000 + (entries.Count * 220));
            SetDragArmed(false);
            Hide();

            BeginInvoke(new Action(async () =>
            {
                bool allOk = true;
                try
                {
                    await Task.Delay(50);
                    foreach (var entry in entries)
                    {
                        byte[]? bytes = (entry.Format == "image" || entry.HasImage) ? LoadImageBytes(entry) : null;
                        if (!PasteService.PasteEntry(entry, target, bytes))
                        {
                            allOk = false;
                            break;
                        }
                        await Task.Delay(180);
                    }
                }
                catch
                {
                    allOk = false;
                }

                if (allOk)
                {
                    CompletePasteSelection();
                }
                else
                {
                    _tray.ShowNotification("Batch paste stopped", "One of the clips could not be pasted.");
                }
            }));
        }

        public static void CleanDropFiles()
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "ClipboardApp", "Drops");
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch { }
        }

        private string? EnsureDropFile(ClipboardEntry entry)
        {
            var bytes = LoadImageBytes(entry);
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "ClipboardApp", "Drops");
                Directory.CreateDirectory(dir);

                string safeId = string.Concat(entry.Id.Split(Path.GetInvalidFileNameChars()));
                if (safeId.Length > 40) safeId = safeId[..40];
                var path = Path.Combine(dir, $"clip_{safeId}.png");

                if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
                {
                    File.WriteAllBytes(path, bytes);
                }
                return path;
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ target + drag

        private IntPtr ResolveTargetWindow()
        {
            if (IsUsableTarget(_preferredTarget)) return _preferredTarget;

            var target = TargetWindowTracker.Instance.GetLastActiveClientWindow();
            if (!IsUsableTarget(target)) target = _lastActiveWindow;
            if (!IsUsableTarget(target)) target = TargetWindowTracker.Instance.FindTopmostClientWindow(Handle);
            return IsUsableTarget(target) ? target : IntPtr.Zero;
        }

        private void ChoosePasteTarget()
        {
            var picked = ShowTargetPicker("Choose the window that should receive the next paste.");
            if (picked == IntPtr.Zero) return;

            _preferredTarget = picked;
            var info = TargetWindowTracker.Instance.GetTargetInfo(picked);
            UpdateTargetBadge();
            ShowTransientStatus(info == null
                ? "Paste target set."
                : $"Paste target: {info.ProcessName} Ã¢â‚¬â€ {info.DisplayName}");
        }

        private void UpdateTargetBadge()
        {
            if (_targetButton == null) return;

            var target = ResolveTargetWindow();
            var info = target != IntPtr.Zero ? TargetWindowTracker.Instance.GetTargetInfo(target) : null;
            if (info == null)
            {
                _targetButton.Text = "Target: Auto";
                _toolTip?.SetToolTip(_targetButton, "No automatic paste target yet.\nClick to choose a window. ClipboardApp never blind-pastes.");
                return;
            }

            string name = string.IsNullOrWhiteSpace(info.ProcessName) ? info.DisplayName : info.ProcessName;
            if (name.Length > 16) name = name[..16];
            _targetButton.Text = "Target: " + name;
            string admin = info.IsElevated ? "\nWarning: this window is elevated (admin). Run ClipboardApp as administrator or use drag-and-drop." : "";
            _toolTip?.SetToolTip(_targetButton,
                $"Current paste target:\n{info.DisplayName}\n{info.ProcessName}{admin}\nClick to choose a different window.");
        }

        private void ForceForeground()
        {
            if (!IsHandleCreated) return;

            uint currentThreadId = NativeMethods.GetCurrentThreadId();
            var foreground = NativeMethods.GetForegroundWindow();
            uint foregroundThreadId = foreground != IntPtr.Zero
                ? NativeMethods.GetWindowThreadProcessId(foreground, out _)
                : 0;
            bool attached = false;

            try
            {
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                {
                    attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                NativeMethods.BringWindowToTop(Handle);
                NativeMethods.SetForegroundWindow(Handle);
                NativeMethods.SetActiveWindow(Handle);
            }
            catch
            {
                // Windows may refuse foreground activation; the window is still shown.
            }
            finally
            {
                if (attached)
                {
                    NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
        }

        private bool IsUsableTarget(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == Handle) return false;
            if (!NativeMethods.IsWindow(hwnd)) return false;
            return TargetWindowTracker.Instance.IsValidTargetWindow(hwnd);
        }

        private void ApplyDragSettings()
        {
            if (_cardList == null) return;

            bool alwaysArmed = _settings.EnableDragDrop && !_settings.RequireManualDragArm;
            _cardList.DragEnabled = _settings.EnableDragDrop;
            _cardList.DragArmed = alwaysArmed || _manualDragArmed;
            if (_gridList != null)
            {
                _gridList.DragEnabled = _settings.EnableDragDrop;
                _gridList.DragArmed = alwaysArmed || _manualDragArmed;
            }

            if (_dragToggle != null)
            {
                _dragToggle.Enabled = _settings.EnableDragDrop && _settings.RequireManualDragArm;
            }

            UpdateDragToggleVisual();
        }

        private void SetDragArmed(bool armed)
        {
            if (!_settings.EnableDragDrop || !_settings.RequireManualDragArm)
            {
                _manualDragArmed = false;
                bool autoArmed = _settings.EnableDragDrop && !_settings.RequireManualDragArm;
                if (_cardList != null) _cardList.DragArmed = autoArmed;
                if (_gridList != null) _gridList.DragArmed = autoArmed;
                UpdateDragToggleVisual();
                return;
            }

            _manualDragArmed = armed;
            if (_cardList != null) _cardList.DragArmed = armed;
            if (_gridList != null) _gridList.DragArmed = armed;
            UpdateDragToggleVisual();
        }

        private void UpdateDragToggleVisual()
        {
            if (_dragToggle == null) return;

            bool alwaysArmed = _settings.EnableDragDrop && !_settings.RequireManualDragArm;
            bool active = _settings.EnableDragDrop && (alwaysArmed || _manualDragArmed);

            _dragToggle.Text = !_settings.EnableDragDrop
                ? "Ã¢Å“â€¹ Drag: Off"
                : active ? "Ã¢Å“â€¹ Drag: Armed" : "Ã¢Å“â€¹ Drag: Off";

            if (!UiStyler.UseCustomStyling(_settings))
            {
                _dragToggle.BackColor = SystemColors.Control;
                _dragToggle.ForeColor = SystemColors.ControlText;
                _dragToggle.FlatAppearance.BorderSize = 1;
                _dragToggle.FlatAppearance.BorderColor = SystemColors.ControlDark;
                if (_cardList != null) _cardList.Cursor = active ? Cursors.Hand : Cursors.Default;
                if (_gridList != null) _gridList.Cursor = active ? Cursors.Hand : Cursors.Default;
                return;
            }

            bool isDark = _settings.Theme == "Dark" || (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
            _dragToggle.BackColor = active
                ? Color.FromArgb(0, 120, 212)
                : (isDark ? Color.FromArgb(45, 45, 50) : Color.FromArgb(241, 245, 249));
            _dragToggle.ForeColor = active
                ? Color.White
                : (isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary);

            if (_cardList != null) _cardList.Cursor = active ? Cursors.Hand : Cursors.Default;
            if (_gridList != null) _gridList.Cursor = active ? Cursors.Hand : Cursors.Default;
        }

        private void StartOleDrag(ClipboardEntry entry)
        {
            if (!_settings.EnableDragDrop || !Accessor.DragArmed) return;

            _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(2000);

            try
            {
                var dataObj = new DataObject();

                if (entry.Format == "image" || entry.HasImage)
                {
                    var bytes = LoadImageBytes(entry);
                    if (bytes == null) return;

                    var dropFile = EnsureDropFile(entry);
                    if (dropFile == null) return;

                    dataObj.SetFileDropList(new StringCollection { dropFile });

                    using var ms = new MemoryStream(bytes);
                    using var bmp = new Bitmap(ms);
                    dataObj.SetImage(bmp);

                    using var pngStream = new MemoryStream(bytes);
                    dataObj.SetData("PNG", false, pngStream);

                    DoDragDrop(dataObj, DragDropEffects.Copy);
                }
                else if (entry.Format == "file" && !string.IsNullOrEmpty(entry.Content))
                {
                    var files = entry.Content
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(f => f.Trim().Length > 0)
                        .ToList();
                    var collection = new StringCollection();
                    collection.AddRange(files.ToArray());
                    dataObj.SetFileDropList(collection);
                    if (files.Count > 0)
                    {
                        dataObj.SetData(DataFormats.UnicodeText, false, string.Join(Environment.NewLine, files));
                    }
                    DoDragDrop(dataObj, DragDropEffects.Copy);
                }
                else if (!string.IsNullOrEmpty(entry.Content))
                {
                    dataObj.SetData(DataFormats.UnicodeText, false, entry.Content);
                    DoDragDrop(dataObj, DragDropEffects.Copy);
                }
            }
            catch
            {
                // OLE drag is best effort; never surface a COM exception as a crash.
            }
            finally
            {
                _suppressCaptureUntilUtc = DateTime.UtcNow.AddMilliseconds(1200);
            }
        }

        // ------------------------------------------------------------------ keyboard

        private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                var listControl = Accessor.Control;
                if (Accessor.Count > 0)
                {
                    listControl.Focus();
                    if (_settings.ViewMode == "Grid" && _gridList != null)
                    {
                        if (_gridList.SelectedIndex < 0) _gridList.SelectedIndex = _gridList.FirstEntryIndex();
                    }
                    else if (_cardList.SelectedIndex < 0 || _cardList.Items[_cardList.SelectedIndex] is not ClipboardEntry)
                    {
                        int first = FirstEntryIndex();
                        if (first >= 0) _cardList.SelectedIndex = first;
                    }
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                PasteSelectedEntry(plainTextOnly: e.Shift);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (Accessor.GetCheckedIds().Count > 0)
                {
                    Accessor.ClearCheckedCards();
                }
                else
                {
                    Hide();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadSnapshot(force: true);
                ShowTransientStatus("History refreshed.");
                e.Handled = true;
            }
        }

        private void OnCardListKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                PasteSelectedEntry(plainTextOnly: e.Shift);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (Accessor.GetCheckedIds().Count > 0)
                {
                    Accessor.ClearCheckedCards();
                }
                else
                {
                    Hide();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (Accessor.GetCheckedIds().Count > 0) BatchDeleteSelected();
                else DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                if (_cardList.SelectedItem is ClipboardEntry entry) EditEntry(entry);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.P)
            {
                if (_cardList.SelectedItem is ClipboardEntry entry) TogglePinEntry(entry);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                if (Accessor.GetCheckedIds().Count > 0 || _cardList.SelectedItem is ClipboardEntry)
                {
                    CopySelectedMerged();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadSnapshot(force: true);
                ShowTransientStatus("History refreshed.");
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9 && !e.Control && !e.Alt)
            {
                int targetRank = e.KeyCode - Keys.D1;
                int currentRank = 0;
                for (int i = 0; i < _cardList.Items.Count; i++)
                {
                    if (_cardList.Items[i] is not ClipboardEntry entry) continue;
                    if (currentRank == targetRank)
                    {
                        PasteEntry(entry, plainTextOnly: e.Shift);
                        e.Handled = true;
                        break;
                    }
                    currentRank++;
                }
            }
        }

        // ------------------------------------------------------------------ history actions

        private void DeleteSelected()
        {
            if (_cardList.SelectedItem is not ClipboardEntry entry) return;

            _repo.DeleteEntry(entry.Id);
            _snapshot.RemoveAll(e => e.Id == entry.Id);
            RebuildList();
            ShowTransientStatus("Entry deleted.");
        }

        private void BatchDeleteSelected()
        {
            var targetIds = Accessor.GetCheckedOrSelectedIds();
            if (targetIds.Count == 0) return;

            if (_settings.ConfirmBatchDelete)
            {
                string confirmMsg = targetIds.Count == 1
                    ? "Are you sure you want to delete this entry?"
                    : $"Are you sure you want to delete {targetIds.Count} selected entries?";

                bool wasTopMost = TopMost;
                try
                {
                    TopMost = false;
                    if (ThemedMessageBox.Show(this, confirmMsg, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        return;
                    }
                }
                finally
                {
                    TopMost = _settings.AlwaysOnTop;
                }
            }

            foreach (var id in targetIds)
            {
                _repo.DeleteEntry(id);
                _snapshot.RemoveAll(e => e.Id == id);
            }

            Accessor.ClearCheckedCards();
            RebuildList();
            ShowTransientStatus($"{targetIds.Count} entr{(targetIds.Count == 1 ? "y" : "ies")} deleted.");
        }

        private void TogglePinEntry(ClipboardEntry entry)
        {
            _repo.TogglePin(entry.Id);
            entry.Pinned = !entry.Pinned;
            RebuildList();
            ShowTransientStatus(entry.Pinned ? "Pinned to the top." : "Unpinned.");
        }

        private void ApplyTransform(ClipboardEntry entry, Func<string, string> transform)
        {
            entry.Content = transform(entry.Content);
            _repo.UpdateEntry(entry);
            RebuildList();
        }

        // ------------------------------------------------------------------ UI helpers (implemented in MainForm.Ui.cs)

        private void UpdateBatchBar()
        {
            if (_batchBar == null) return;
            int count = Accessor.GetCheckedIds().Count;
            int hidden = Accessor.GetHiddenCheckedCount();
            _batchBar.Visible = count > 0;
            if (_bottomArea != null)
            {
                _bottomArea.Height = count > 0 ? 72 : 34;
            }
            if (count > 0)
            {
                _batchCountLabel.Text = hidden > 0
                    ? $"{count} selected ({hidden} hidden)"
                    : $"{count} item{(count == 1 ? "" : "s")} selected";
            }
            if (_batchPasteEachButton != null)
            {
                _batchPasteEachButton.Visible = count > 0 && _settings.SequentialBatchPaste;
            }

            UpdateFilterBarState();
        }
    }
}

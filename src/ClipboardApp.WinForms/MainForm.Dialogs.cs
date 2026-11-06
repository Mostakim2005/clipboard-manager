using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using ClipboardApp.WinForms.Controls;
using ClipboardApp.WinForms.Dialogs;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ClipboardApp.WinForms
{
    public partial class MainForm
    {
        private void OnCardListMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            if (_settings.ViewMode == "Grid")
            {
                // GridCardList has no ListBox.IndexFromPoint; hit-test the tile
                // under the cursor and select it before the menu opens.
                if (_gridList == null) return;
                var entryAtPoint = _gridList.EntryFromScreenPoint(Cursor.Position);
                if (entryAtPoint == null) return;
                _gridList.FocusEntry(entryAtPoint.Id);
                ShowContextMenu(e.Location);
                return;
            }

            int idx = _cardList.IndexFromPoint(e.Location);
            if (idx < 0 || idx >= _cardList.Items.Count) return;
            if (_cardList.Items[idx] is not ClipboardEntry) return;

            _cardList.SelectedIndex = idx;
            ShowContextMenu(e.Location);
        }

        private void ShowContextMenu(Point location)
        {
            if (Accessor.SelectedItem is not ClipboardEntry entry) return;

            _cardContextMenu.Items.Clear();

            int checkedCount = Accessor.GetCheckedIds().Count;
            bool entryIsChecked = Accessor.GetCheckedIds().Contains(entry.Id);
            string copyText = checkedCount > 1 ? $"ðŸ“‹ Copy {checkedCount} Selected (Merged)" : "ðŸ“‹ Copy to Clipboard";
            string pasteText = checkedCount > 1 ? $"ðŸ“‹ Paste {checkedCount} Selected (Merged)" : "ðŸ“‹ Paste";
            string pastePlainText = checkedCount > 1 ? $"ðŸ“„ Paste {checkedCount} Selected as Plain Text" : "ðŸ“„ Paste as Plain Text";

            var factories = new Dictionary<string, Func<ToolStripItem>>(StringComparer.OrdinalIgnoreCase)
            {
                ["copy"] = () => new ToolStripMenuItem(copyText, null, (_, _) => CopySelectedMerged())
                {
                    ShortcutKeyDisplayString = "Ctrl+C"
                },
                ["copyAs"] = () =>
                {
                    var copyAsMenu = new ToolStripMenuItem("ðŸ“‹ Copy Selected as");
                    copyAsMenu.DropDownItems.Add("Plain text", null, (_, _) => CopySelectedMerged("Plain"));
                    copyAsMenu.DropDownItems.Add(".env / KEY=value", null, (_, _) => CopySelectedMerged("Env"));
                    copyAsMenu.DropDownItems.Add("JSON object", null, (_, _) => CopySelectedMerged("Json"));
                    copyAsMenu.DropDownItems.Add("CSV", null, (_, _) => CopySelectedMerged("Csv"));
                    copyAsMenu.DropDownItems.Add(new ToolStripSeparator());
                    copyAsMenu.DropDownItems.Add("Rich text + images", null, (_, _) => CopySelectedMerged("Rich"));
                    return copyAsMenu;
                },
                ["paste"] = () => new ToolStripMenuItem(pasteText, null, (_, _) => PasteSelectedEntry(plainTextOnly: false))
                {
                    ShortcutKeyDisplayString = "Enter"
                },
                ["pastePlain"] = () => new ToolStripMenuItem(pastePlainText, null, (_, _) => PasteSelectedEntry(plainTextOnly: true))
                {
                    ShortcutKeyDisplayString = "Shift+Enter"
                },
                ["pin"] = () => new ToolStripMenuItem(entry.Pinned ? "ðŸ“Œ Unpin" : "ðŸ“Œ Pin (Favorite)", null, (_, _) => TogglePinEntry(entry))
                {
                    ShortcutKeyDisplayString = "Ctrl+P"
                },
                ["tags"] = () => new ToolStripMenuItem("ðŸ·ï¸ Assign Tags & Groups...", null, (_, _) => AssignTagsToSelected(entry)),
                ["edit"] = () => new ToolStripMenuItem("âœï¸ Edit Entry...", null, (_, _) => EditEntry(entry))
                {
                    ShortcutKeyDisplayString = "F2"
                },
                ["transform"] = () =>
                {
                    var transformMenu = new ToolStripMenuItem("âœ¨ Transform Text");
                    transformMenu.DropDownItems.Add("UPPERCASE", null, (_, _) => ApplyTransform(entry, TextTransformService.ToUpper));
                    transformMenu.DropDownItems.Add("lowercase", null, (_, _) => ApplyTransform(entry, TextTransformService.ToLower));
                    transformMenu.DropDownItems.Add("Title Case", null, (_, _) => ApplyTransform(entry, TextTransformService.ToTitleCase));
                    transformMenu.DropDownItems.Add("Trim Whitespace", null, (_, _) => ApplyTransform(entry, TextTransformService.Trim));
                    transformMenu.DropDownItems.Add("Remove Line Breaks", null, (_, _) => ApplyTransform(entry, TextTransformService.RemoveLineBreaks));
                    transformMenu.DropDownItems.Add(new ToolStripSeparator());
                    transformMenu.DropDownItems.Add("Prettify JSON", null, (_, _) => ApplyTransform(entry, TextTransformService.PrettifyJson));
                    transformMenu.DropDownItems.Add("Minify JSON", null, (_, _) => ApplyTransform(entry, TextTransformService.MinifyJson));
                    return transformMenu;
                },
                ["delete"] = () => new ToolStripMenuItem("ðŸ—‘ï¸ Delete", null, (_, _) =>
                {
                    if (checkedCount > 1 && entryIsChecked) BatchDeleteSelected();
                    else DeleteSelected();
                })
                {
                    ShortcutKeyDisplayString = "Del"
                }
            };

            string lastGroup = "";
            foreach (var key in ContextMenuCommands.NormalizeOrder(_settings.ContextMenuOrder))
            {
                if (string.Equals(key, ContextMenuCommands.CustomizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (ContextMenuCommands.IsHidden(_settings.ContextMenuHidden, key)) continue;

                if (string.Equals(key, "transform", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(entry.Content) || entry.Format == "image"))
                {
                    continue;
                }

                if (!factories.TryGetValue(key, out var factory)) continue;

                var command = ContextMenuCommands.Find(key);
                if (command != null &&
                    lastGroup.Length > 0 &&
                    !string.Equals(lastGroup, command.Group, StringComparison.OrdinalIgnoreCase))
                {
                    _cardContextMenu.Items.Add(new ToolStripSeparator());
                }

                _cardContextMenu.Items.Add(factory());
                lastGroup = command?.Group ?? lastGroup;
            }

            if (_cardContextMenu.Items.Count > 0)
            {
                _cardContextMenu.Items.Add(new ToolStripSeparator());
            }
            _cardContextMenu.Items.Add(new ToolStripMenuItem("âš™ï¸ Customize this menu...", null, (_, _) => CustomizeContextMenu()));

            UiStyler.ApplyMenu(_cardContextMenu, _settings);
            _cardContextMenu.Show(_cardList, location);
        }

        private void CustomizeContextMenu()
        {
            using var dlg = new ContextMenuCustomizeDialog(_settings)
            {
                Owner = this,
                StartPosition = FormStartPosition.CenterParent,
                TopMost = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _settings.ContextMenuOrder = dlg.Order;
                _settings.ContextMenuHidden = dlg.Hidden;
                _settings.Save();
                ShowTransientStatus("Right-click menu updated.");
            }
        }

        private void EditEntry(ClipboardEntry entry)
        {
            bool wasTopMost = TopMost;
            try
            {
                TopMost = false;
                var knownTags = TagSuggestionService.CollectTags(_snapshot);
                var knownAliases = TagSuggestionService.CollectAliases(_snapshot);
                using var dlg = new EntryEditDialog(entry, _settings, knownTags, knownAliases)
                {
                    TopMost = true,
                    Owner = this,
                    StartPosition = FormStartPosition.CenterParent
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _repo.UpdateEntry(entry);
                    RebuildList();
                }
            }
            finally
            {
                TopMost = _settings.AlwaysOnTop;
            }
        }

        private void ShowOptions()
        {
            bool wasTopMost = TopMost;
            try
            {
                TopMost = false;
                using var dlg = new OptionsDialog(_settings, _repo)
                {
                    TopMost = true,
                    Owner = this,
                    StartPosition = FormStartPosition.CenterParent
                };
                var res = dlg.ShowDialog(this);
                if (dlg.HistoryImported)
                {
                    ReloadSnapshot(force: true);
                }

                if (res == DialogResult.OK)
                {
                    _settings.Save();
                    _repo.DeduplicationMode = _settings.GetDeduplicationMode();

                    TopMost = _settings.AlwaysOnTop;
                    _cardList.CardDensity = _settings.CardDensity;
                    _cardList.VisualStyle = _settings.VisualStyle;
                    _cardList.ShowCheckboxes = _settings.ShowCardCheckboxes;
                    _cardList.ShowSerials = _settings.ShowSerials;
                    ApplySplitterLayout();
                    ApplyTheme();
                    ApplyDragSettings();
                    RegisterHotkey();
                    if (_targetButton != null) _targetButton.Visible = _settings.ShowPasteTargetBadge;
                    if (_vaultButton != null) _vaultButton.Visible = _settings.EnableApiVault;
                    UpdateTargetBadge();

                    if (_settings.LiveRefresh && _snapshotDirty) ReloadSnapshot(force: true);
                    else RebuildList();
                }
            }
            finally
            {
                TopMost = _settings.AlwaysOnTop;
            }
        }

        private void AssignTagsToSelected(ClipboardEntry? targetEntry = null)
        {
            System.Collections.Generic.List<string> targetIds;
            string initialTags = "";

            if (targetEntry != null)
            {
                targetIds = new System.Collections.Generic.List<string> { targetEntry.Id };
                initialTags = targetEntry.Tags != null ? string.Join(", ", targetEntry.Tags) : "";
            }
            else
            {
                targetIds = Accessor.GetCheckedOrSelectedIds();
                if (targetIds.Count == 0) return;
                var first = _snapshot.FirstOrDefault(e => e.Id == targetIds[0]);
                initialTags = first?.Tags != null ? string.Join(", ", first.Tags) : "";
            }

            try
            {
                TopMost = false;
                bool isDark = _settings.Theme == "Dark" || (_settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());
                using var inputDlg = new Form
                {
                    Text = "Assign Tags & Groups",
                    Width = 420,
                    Height = 180,
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    ShowInTaskbar = false,
                    Owner = this,
                    BackColor = isDark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg,
                    ForeColor = isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary
                };

                var lbl = new Label
                {
                    Text = $"Enter tags for {targetIds.Count} selected item(s) (comma-separated):",
                    Dock = DockStyle.Top,
                    Height = 35,
                    Padding = new Padding(16, 12, 16, 0)
                };

                var txtBox = new TextBox
                {
                    Text = initialTags,
                    Location = new Point(16, 45),
                    Width = 370
                };

                var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(0, 0, 16, 10) };
                var btnOk = new Button { Text = "Save", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 80 };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 80 };

                btnPanel.Controls.Add(btnOk);
                btnPanel.Controls.Add(btnCancel);
                inputDlg.Controls.Add(lbl);
                inputDlg.Controls.Add(txtBox);
                inputDlg.Controls.Add(btnPanel);
                inputDlg.AcceptButton = btnOk;
                inputDlg.CancelButton = btnCancel;

                using var tagAuto = new TokenAutoComplete(
                    txtBox,
                    () => TagSuggestionService.CollectTags(_snapshot),
                    ',',
                    ';');

                UiStyler.ApplyForm(inputDlg, _settings);

                if (inputDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var parsedTags = txtBox.Text
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var id in targetIds)
                    {
                        var entry = _repo.GetEntryById(id);
                        if (entry == null) continue;
                        entry.Tags = parsedTags;
                        _repo.UpdateEntry(entry);

                        var snapshotEntry = _snapshot.FirstOrDefault(e => e.Id == id);
                        if (snapshotEntry != null) snapshotEntry.Tags = parsedTags;
                    }

                    Accessor.ClearCheckedCards();
                    RebuildList();
                }
            }
            finally
            {
                TopMost = _settings.AlwaysOnTop;
            }
        }
    }
}

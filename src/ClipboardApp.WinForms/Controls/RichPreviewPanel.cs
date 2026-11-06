using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    public class RichPreviewPanel : Panel
    {
        private readonly Label _headerInfo;
        private readonly FlowLayoutPanel _actionBar;
        private readonly TextBox _textBox;
        private readonly PictureBox _pictureBox;
        private readonly Panel _colorPanel;
        private readonly Label _colorLabel;

        private ClipboardEntry? _currentEntry;
        private byte[]? _currentImageData;
        private bool _isDark = true;
        private readonly HashSet<string> _revealedSensitiveIds = new();

        // Transform preview state: transforms and JSON prettify no longer write
        // straight to the database. They stage a pending result that the user
        // reviews and then either Saves (commits) or Cancels (discards).
        private string? _pendingContent;
        private readonly List<string> _undoStack = new();
        private const int MaxUndoDepth = 20;

        public event EventHandler<ClipboardEntry>? EntryUpdated;
        public event EventHandler? ClipboardWriteStarted;
        public event EventHandler<string>? SensitiveVisibilityToggled;

        /// <summary>True while a transformed result is shown but not saved.</summary>
        public bool HasPendingChanges => _pendingContent != null;

        public bool IsSensitiveRevealed(string id) => _revealedSensitiveIds.Contains(id);
        public void RevealSensitive(string id) => _revealedSensitiveIds.Add(id);
        public void MaskSensitive(string id) => _revealedSensitiveIds.Remove(id);

        public bool IsDark
        {
            get => _isDark;
            set
            {
                _isDark = value;
                ApplyColors();
            }
        }

        public RichPreviewPanel()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);

            // 1. Header Information Banner
            _headerInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Text = "Select an item to view preview"
            };

            // 2. Quick Action Toolbar
            _actionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 2, 0, 4)
            };

            // 3. Text Preview Box
            _textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Visible = true
            };

            // 4. PictureBox Preview
            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 20, 20),
                Visible = false
            };

            // 5. Color Swatch Box
            _colorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Padding = new Padding(24)
            };
            _colorLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold)
            };
            _colorPanel.Controls.Add(_colorLabel);

            Controls.Add(_textBox);
            Controls.Add(_pictureBox);
            Controls.Add(_colorPanel);
            Controls.Add(_actionBar);
            Controls.Add(_headerInfo);

            ApplyColors();
        }

        private void ApplyColors()
        {
            BackColor = _isDark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg;
            _headerInfo.ForeColor = _isDark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary;
            _textBox.BackColor = _isDark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg;
            _textBox.ForeColor = _isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
        }

        public void DisplayEntry(ClipboardEntry? entry, byte[]? imageData = null)
        {
            // Switching to another entry discards any unsaved transform silently:
            // the database was never touched, so there is nothing to recover.
            if (_currentEntry?.Id != entry?.Id)
            {
                _pendingContent = null;
                _undoStack.Clear();
            }

            _currentEntry = entry;
            _currentImageData = imageData;
            _actionBar.Controls.Clear();

            if (entry == null)
            {
                _headerInfo.Text = "Select an item to view details & actions";
                _textBox.Visible = true;
                _textBox.Text = string.Empty;
                _pictureBox.Visible = false;
                _colorPanel.Visible = false;
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = null;
                return;
            }

            var relativeTime = ModernPalette.GetRelativeTime(entry.CreatedAt);
            _headerInfo.Text = $"{entry.Format.ToUpperInvariant()}  •  {entry.SourceApp}  •  {relativeTime}  •  {entry.CreatedAt:h:mm:ss tt}";

            bool isColor = entry.Format == "text" && !string.IsNullOrEmpty(entry.Content) &&
                           entry.Content.Trim().StartsWith("#") &&
                           (entry.Content.Trim().Length == 7 || entry.Content.Trim().Length == 9);

            bool isLink = entry.Format == "text" && !string.IsNullOrEmpty(entry.Content) &&
                          (entry.Content.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                           entry.Content.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            if (entry.Format == "image" || entry.HasImage)
            {
                DisplayImagePreview();
            }
            else if (isColor)
            {
                DisplayColorPreview(entry.Content.Trim());
            }
            else
            {
                DisplayTextPreview(isLink);
            }
        }

        private void DisplayTextPreview(bool isLink)
        {
            _pictureBox.Visible = false;
            _colorPanel.Visible = false;
            _textBox.Visible = true;

            bool isMasked = _currentEntry?.IsSensitive == true && !_revealedSensitiveIds.Contains(_currentEntry.Id);
            string currentText = _pendingContent ?? (_currentEntry?.Content ?? "");
            _textBox.Text = isMasked
                ? "••••••••••••••••  (Sensitive content masked — click 'Reveal' to view)"
                : currentText;

            if (_pendingContent != null)
            {
                _headerInfo.Text += "   •   PREVIEW — changes not saved";
            }

            if (_currentEntry?.IsSensitive == true)
            {
                if (isMasked)
                {
                    AddActionButton("👁️ Reveal", () =>
                    {
                        if (_currentEntry == null) return;
                        _revealedSensitiveIds.Add(_currentEntry.Id);
                        SensitiveVisibilityToggled?.Invoke(this, _currentEntry.Id);
                        DisplayEntry(_currentEntry, _currentImageData);
                    });
                }
                else
                {
                    AddActionButton("🔒 Mask", () =>
                    {
                        if (_currentEntry == null) return;
                        _revealedSensitiveIds.Remove(_currentEntry.Id);
                        SensitiveVisibilityToggled?.Invoke(this, _currentEntry.Id);
                        DisplayEntry(_currentEntry, _currentImageData);
                    });
                }
            }

            if (isLink)
            {
                AddActionButton("📋 Copy Link", () =>
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    var text = _pendingContent ?? _currentEntry?.Content;
                    if (!string.IsNullOrEmpty(text)) PasteService.CopyText(text);
                });
                AddActionButton("🌐 Open Link", () =>
                {
                    try
                    {
                        var url = (_pendingContent ?? _currentEntry?.Content)?.Trim();
                        if (!string.IsNullOrEmpty(url) &&
                            Uri.TryCreate(url, UriKind.Absolute, out var validatedUri) &&
                            (validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps))
                        {
                            Process.Start(new ProcessStartInfo(validatedUri.AbsoluteUri) { UseShellExecute = true });
                        }
                    }
                    catch { }
                });
            }
            else
            {
                AddActionButton("📋 Copy Text", () =>
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    var text = _pendingContent ?? _currentEntry?.Content;
                    if (!string.IsNullOrEmpty(text)) PasteService.CopyText(text);
                });
            }

            if (isMasked) return;

            // ---- transforms (preview only until saved) ----
            if (_currentEntry?.Content != null &&
                (_currentEntry.Content.TrimStart().StartsWith("{") || _currentEntry.Content.TrimStart().StartsWith("[")))
            {
                AddActionButton("⚡ Prettify JSON", () => StageTransform(TextTransformService.PrettifyJson));
            }

            AddActionButton("🔠 UPPER", () => StageTransform(TextTransformService.ToUpper));
            AddActionButton("🔡 lower", () => StageTransform(TextTransformService.ToLower));
            AddActionButton("🔗 slug", () => StageTransform(TextTransformService.ToSlug));
            AddActionButton("🐫 camel", () => StageTransform(TextTransformService.ToCamelCase));
            AddActionButton("🐍 snake", () => StageTransform(TextTransformService.ToSnakeCase));

            if (_pendingContent != null)
            {
                AddActionButton("↩ Undo", Undo, isAccent: false);
                AddActionButton("✖ Cancel", CancelPending, isAccent: false);
                AddActionButton("💾 Save Changes", SavePending, isAccent: true);
            }
        }

        /// <summary>
        /// Applies a transform to the current preview text without writing to the
        /// database. The previous text is stacked so Undo can walk back step by
        /// step even across several applied transforms.
        /// </summary>
        private void StageTransform(Func<string, string> transform)
        {
            if (_currentEntry == null) return;

            string baseline = _pendingContent ?? _currentEntry.Content ?? "";
            string result = transform(baseline);
            if (result == baseline) return; // no-op transform: nothing to save

            _undoStack.Add(baseline);
            if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);

            _pendingContent = result;
            DisplayEntry(_currentEntry, _currentImageData);
        }

        private void Undo()
        {
            if (_currentEntry == null || _undoStack.Count == 0) return;

            string previous = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _pendingContent = previous == _currentEntry.Content ? null : previous;
            DisplayEntry(_currentEntry, _currentImageData);
        }

        private void CancelPending()
        {
            _pendingContent = null;
            _undoStack.Clear();
            if (_currentEntry != null) DisplayEntry(_currentEntry, _currentImageData);
        }

        private void SavePending()
        {
            if (_currentEntry == null || _pendingContent == null) return;

            // Commit exactly once: the repository is the only writer.
            _currentEntry.Content = _pendingContent;
            _currentEntry.Format = _currentEntry.Format == "color" && !LooksLikeColor(_pendingContent)
                ? "text"
                : _currentEntry.Format;

            var saved = _currentEntry;
            _pendingContent = null;
            _undoStack.Clear();
            EntryUpdated?.Invoke(this, saved);
            DisplayEntry(saved, _currentImageData);
        }

        private static bool LooksLikeColor(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 7 || trimmed.Length == 9)
            {
                return trimmed.StartsWith("#");
            }
            return false;
        }

        private void DisplayColorPreview(string hex)
        {
            _textBox.Visible = false;
            _pictureBox.Visible = false;
            _colorPanel.Visible = true;

            try
            {
                var color = ColorTranslator.FromHtml(hex);
                _colorPanel.BackColor = color;

                // Pick contrasting text color
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                _colorLabel.ForeColor = luminance > 0.5 ? Color.Black : Color.White;
                _colorLabel.Text = $"{hex.ToUpperInvariant()}\nRGB({color.R}, {color.G}, {color.B})";

                AddActionButton("📋 Copy HEX", () =>
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    PasteService.CopyText(hex.ToUpperInvariant());
                });
                AddActionButton("📋 Copy RGB", () =>
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    PasteService.CopyText($"rgb({color.R}, {color.G}, {color.B})");
                });
            }
            catch
            {
                _colorPanel.BackColor = Color.Gray;
                _colorLabel.Text = hex;
                AddActionButton("📋 Copy HEX", () =>
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    PasteService.CopyText(hex);
                });
            }
        }

        private void DisplayImagePreview()
        {
            _textBox.Visible = false;
            _colorPanel.Visible = false;
            _pictureBox.Visible = true;

            if (_currentImageData != null && _currentImageData.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(_currentImageData);
                    var old = _pictureBox.Image;
                    var bmp = new Bitmap(ms);
                    _pictureBox.Image = bmp;
                    old?.Dispose();
                    _headerInfo.Text += $"  •  {bmp.Width} × {bmp.Height} px  •  {_currentImageData.Length / 1024} KB";
                }
                catch
                {
                    _pictureBox.Image = null;
                }
            }

            AddActionButton("📋 Copy Image", () =>
            {
                if (_currentImageData != null && _currentImageData.Length > 0)
                {
                    ClipboardWriteStarted?.Invoke(this, EventArgs.Empty);
                    PasteService.CopyImageBytes(_currentImageData);
                }
            });

            AddActionButton("💾 Save As...", () =>
            {
                if (_pictureBox.Image == null) return;

                string defaultName = !string.IsNullOrWhiteSpace(_currentEntry?.Alias)
                    ? _currentEntry.Alias
                    : $"Image_{_currentEntry?.SourceApp ?? "Clip"}_{_currentEntry?.CreatedAt:yyyyMMdd_HHmm}";

                using var dlg = new SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",
                    DefaultExt = "png",
                    FileName = defaultName
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                        var fmt = ext switch
                        {
                            ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                            _ => System.Drawing.Imaging.ImageFormat.Png
                        };
                        _pictureBox.Image.Save(dlg.FileName, fmt);
                    }
                    catch { }
                }
            });
        }

        private void AddActionButton(string text, Action action, bool isAccent = false)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderSize = isAccent ? 0 : 1;
            btn.FlatAppearance.BorderColor = _isDark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder;
            btn.BackColor = isAccent
                ? (_isDark ? ModernPalette.DarkAccent : ModernPalette.LightAccent)
                : (_isDark ? ModernPalette.DarkPillBg : ModernPalette.LightPillBg);
            btn.ForeColor = isAccent
                ? Color.White
                : (_isDark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary);
            btn.Click += (_, _) => action();
            _actionBar.Controls.Add(btn);
        }
    }
}

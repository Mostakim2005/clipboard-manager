using ClipboardApp.WinForms.Dialogs;
using ClipboardApp.WinForms.UI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Controls
{
    public class ImagePreview : Form
    {
        private readonly PictureBox _pb;
        private Image? _currentImage;
        private MemoryStream? _stream;

        public ImagePreview(byte[] imageData)
        {
            Text = "Image Preview";
            Width = 600;
            Height = 550;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            _pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(32, 32, 32)
            };

            try
            {
                _stream = new MemoryStream(imageData);
                _currentImage = Image.FromStream(_stream);
                _pb.Image = _currentImage;
                Text = $"Image Preview ({_currentImage.Width} x {_currentImage.Height} px)";
            }
            catch
            {
                Controls.Add(new Label
                {
                    Text = "[Unable to render image data]",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                UiStyler.ApplyForm(this);
                return;
            }

            Controls.Add(_pb);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 45,
                Padding = new Padding(6)
            };

            var closeBtn = new Button { Text = "Close", DialogResult = DialogResult.OK };
            var copyBtn = new Button { Text = "Copy Image", Width = 95 };
            copyBtn.Click += (_, _) => CopyImageToClipboard();

            var saveBtn = new Button { Text = "Save As...", Width = 90 };
            saveBtn.Click += (_, _) => SaveImage();

            buttonPanel.Controls.Add(closeBtn);
            buttonPanel.Controls.Add(saveBtn);
            buttonPanel.Controls.Add(copyBtn);
            Controls.Add(buttonPanel);
            UiStyler.ApplyForm(this);
        }

        private void CopyImageToClipboard()
        {
            if (_currentImage != null)
            {
                try
                {
                    Clipboard.SetImage(_currentImage);
                    ThemedMessageBox.Show("Image copied to clipboard.", "Clipboard App", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show("Failed to copy image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveImage()
        {
            if (_currentImage == null) return;

            using var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = "png"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var format = ImageFormat.Png;
                    var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") format = ImageFormat.Jpeg;
                    else if (ext == ".bmp") format = ImageFormat.Bmp;

                    _currentImage.Save(dlg.FileName, format);
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show("Failed to save image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pb.Image = null;
                _currentImage?.Dispose();
                _stream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

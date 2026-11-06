using System;
using System.Drawing;
using System.Windows.Forms;
using ClipboardApp.WinForms.UI;

namespace ClipboardApp.WinForms
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly Form _form;
        private readonly Icon _iconImage;
        private bool _isPaused;

        public event EventHandler? PauseToggled;
        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? OpenVaultRequested;
        public event EventHandler? ClearHistoryRequested;

        public ContextMenuStrip ContextMenuStrip => _icon.ContextMenuStrip!;

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                UpdateMenuText();
            }
        }

        public TrayIconManager(Form form)
        {
            _form = form;
            _iconImage = AppIcon.Load(SystemInformation.SmallIconSize.Width);
            _icon = new NotifyIcon
            {
                Icon = _iconImage,
                Text = "Clipboard App",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            BuildMenu();
            _icon.DoubleClick += (_, _) => ShowForm();
        }

        private void BuildMenu()
        {
            var menu = _icon.ContextMenuStrip!;
            menu.Items.Clear();

            menu.Items.Add("Open Clipboard App", null, (_, _) => ShowForm());
            var pauseItem = new ToolStripMenuItem("Pause Capture", null, (_, _) =>
            {
                IsPaused = !IsPaused;
                PauseToggled?.Invoke(this, EventArgs.Empty);
            });
            pauseItem.Name = "PauseItem";
            menu.Items.Add(pauseItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Clear Unpinned History", null, (_, _) => ClearHistoryRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("API Vault...", null, (_, _) => OpenVaultRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Settings...", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Application.Exit());
        }

        private void UpdateMenuText()
        {
            if (_icon.ContextMenuStrip?.Items["PauseItem"] is ToolStripMenuItem item)
            {
                item.Text = _isPaused ? "Resume Capture" : "Pause Capture";
                item.Checked = _isPaused;
            }
            _icon.Text = _isPaused ? "Clipboard App (Paused)" : "Clipboard App";
        }

        public void ShowNotification(string title, string text)
        {
            try
            {
                _icon.ShowBalloonTip(1500, title, text, ToolTipIcon.Info);
            }
            catch { }
        }

        public void ApplyStyle(AppSettings settings)
        {
            if (_icon.ContextMenuStrip == null) return;
            UiStyler.ApplyMenu(_icon.ContextMenuStrip, settings);
        }

        public void ShowForm()
        {
            if (_form is MainForm mainForm)
            {
                mainForm.ShowAppNearCursor();
            }
            else
            {
                _form.Show();
                if (_form.WindowState == FormWindowState.Minimized)
                {
                    _form.WindowState = FormWindowState.Normal;
                }
                _form.ShowInTaskbar = true;
                _form.Activate();
            }
        }

        public void Dispose()
        {
            // CRITICAL: Setting Visible = false before Dispose eliminates the Windows ghost tray icon
            _icon.Visible = false;
            _icon.Dispose();
            _iconImage.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

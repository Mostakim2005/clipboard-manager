using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClipboardApp.Installer;

public class InstallerForm : Form
{
    private readonly TextBox _txtDir;
    private readonly CheckBox _chkDesktop;
    private readonly CheckBox _chkStartMenu;
    private readonly CheckBox _chkStartup;
    private readonly CheckBox _chkLaunchNow;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblStatus;
    private readonly Button _btnInstall;
    private readonly Button _btnCancel;
    private bool _isCompleted = false;

    public InstallerForm()
    {
        Text = $"{SetupEngine.AppName} v{SetupEngine.AppVersion} Setup";
        ClientSize = new Size(540, 440);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(24, 24, 27);
        ForeColor = Color.FromArgb(240, 240, 245);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header Panel
        Panel headerPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(39, 39, 42),
            Padding = new Padding(24, 16, 24, 16)
        };

        Label lblTitle = new()
        {
            Text = $"{SetupEngine.AppName} v{SetupEngine.AppVersion}",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 14)
        };

        Label lblSubtitle = new()
        {
            Text = "Enterprise-Grade Windows Clipboard Manager - Setup Wizard",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            AutoSize = true,
            Location = new Point(25, 44)
        };
        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

        // Content Area
        Label lblDir = new()
        {
            Text = "Installation Directory:",
            Location = new Point(24, 100),
            AutoSize = true,
            ForeColor = Color.FromArgb(212, 212, 216)
        };

        _txtDir = new TextBox
        {
            Location = new Point(24, 126),
            Size = new Size(390, 25),
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = SetupEngine.GetDefaultInstallDir()
        };

        Button btnBrowse = new()
        {
            Text = "Browse...",
            Location = new Point(424, 125),
            Size = new Size(90, 27),
            BackColor = Color.FromArgb(45, 45, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        btnBrowse.Click += (s, e) =>
        {
            using FolderBrowserDialog fbd = new() { SelectedPath = _txtDir.Text };
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtDir.Text = fbd.SelectedPath;
            }
        };

        // Options
        _chkDesktop = new CheckBox
        {
            Text = "Create Desktop shortcut",
            Location = new Point(28, 175),
            AutoSize = true,
            Checked = true
        };

        _chkStartMenu = new CheckBox
        {
            Text = "Create Start Menu shortcut",
            Location = new Point(28, 205),
            AutoSize = true,
            Checked = true
        };

        _chkStartup = new CheckBox
        {
            Text = "Launch ClipboardApp on Windows startup",
            Location = new Point(28, 235),
            AutoSize = true,
            Checked = true
        };

        _chkLaunchNow = new CheckBox
        {
            Text = "Start ClipboardApp after installation finishes",
            Location = new Point(28, 265),
            AutoSize = true,
            Checked = true
        };

        // Progress bar & Status
        _progressBar = new ProgressBar
        {
            Location = new Point(24, 305),
            Size = new Size(490, 16),
            Visible = false
        };

        _lblStatus = new Label
        {
            Text = "Ready to install. Click Install to proceed.",
            Location = new Point(24, 330),
            Size = new Size(490, 20),
            ForeColor = Color.FromArgb(161, 161, 170)
        };

        // Footer buttons
        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(325, 375),
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(45, 45, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _btnCancel.Click += (s, e) => Close();

        _btnInstall = new Button
        {
            Text = "Install",
            Location = new Point(424, 375),
            Size = new Size(90, 32),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += async (s, e) =>
        {
            if (_isCompleted)
            {
                Close();
                return;
            }

            await StartInstallationAsync();
        };

        Controls.AddRange(new Control[]
        {
            headerPanel,
            lblDir, _txtDir, btnBrowse,
            _chkDesktop, _chkStartMenu, _chkStartup, _chkLaunchNow,
            _progressBar, _lblStatus,
            _btnCancel, _btnInstall
        });
    }

    private async Task StartInstallationAsync()
    {
        string targetDir = _txtDir.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            MessageBox.Show(this, "Please select an installation directory.", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnInstall.Enabled = false;
        _btnCancel.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Value = 0;

        try
        {
            _lblStatus.Text = "Closing existing ClipboardApp processes...";
            SetupEngine.KillRunningProcesses();

            _lblStatus.Text = "Extracting application binaries...";
            var progress = new Progress<int>(pct =>
            {
                _progressBar.Value = Math.Clamp(pct, 0, 100);
            });

            await SetupEngine.ExtractPayloadAsync(targetDir, progress);

            string targetExe = Path.Combine(targetDir, "ClipboardApp.exe");

            _lblStatus.Text = "Configuring shortcuts and registry...";
            if (_chkDesktop.Checked)
            {
                SetupEngine.CreateShortcut(targetExe, SetupEngine.GetDesktopShortcutPath(), "Enterprise-Grade Windows Clipboard Manager");
            }

            if (_chkStartMenu.Checked)
            {
                SetupEngine.CreateShortcut(targetExe, SetupEngine.GetStartMenuShortcutPath(), "Enterprise-Grade Windows Clipboard Manager");
            }

            if (_chkStartup.Checked)
            {
                SetupEngine.SetStartupRun(targetExe, true);
            }

            SetupEngine.RegisterUninstall(targetDir);

            _progressBar.Value = 100;
            _lblStatus.Text = "Installation completed successfully!";
            _lblStatus.ForeColor = Color.FromArgb(74, 222, 128); // Green

            if (_chkLaunchNow.Checked && File.Exists(targetExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
                }
                catch { }
            }

            _isCompleted = true;
            _btnInstall.Text = "Finish";
            _btnInstall.Enabled = true;
            _btnCancel.Visible = false;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Installation failed: {ex.Message}";
            _lblStatus.ForeColor = Color.FromArgb(248, 113, 113); // Red
            _btnInstall.Enabled = true;
            _btnCancel.Enabled = true;
            MessageBox.Show(this, $"An error occurred during installation:\n\n{ex.Message}", "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

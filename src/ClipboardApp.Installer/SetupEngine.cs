using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace ClipboardApp.Installer;

public static class SetupEngine
{
    public const string AppName = "ClipboardApp";
    public const string Publisher = "OpenCode";

    /// <summary>
    /// Version shown in the wizard title/header and written to the uninstall
    /// registry key. Resolved from the assembly so it can never drift from the
    /// released build.
    /// </summary>
    public static readonly string AppVersion = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            int plus = info!.IndexOf('+');
            return plus > 0 ? info.Substring(0, plus) : info;
        }

        var version = assembly.GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static string GetDefaultInstallDir()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", AppName);
    }

    public static void KillRunningProcesses()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("ClipboardApp"))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch { }
            }
        }
        catch { }
    }

    public static async Task ExtractPayloadAsync(string targetDir, IProgress<int>? progress = null)
    {
        Directory.CreateDirectory(targetDir);
        string targetExe = Path.Combine(targetDir, "ClipboardApp.exe");

        var assembly = Assembly.GetExecutingAssembly();
        using var resourceStream = assembly.GetManifestResourceStream("payload.bin")
            ?? throw new InvalidOperationException("Embedded payload resource 'payload.bin' not found.");

        using var gzipStream = new GZipStream(resourceStream, CompressionMode.Decompress);
        using var fileStream = new FileStream(targetExe, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        int bytesRead;
        long totalRead = 0;
        long estimatedTotal = 164000000;

        while ((bytesRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;
            if (progress != null && estimatedTotal > 0)
            {
                int pct = (int)Math.Min(98, (totalRead * 100) / estimatedTotal);
                progress.Report(pct);
            }
        }

        progress?.Report(100);

        string currentExe = Environment.ProcessPath ?? "";
        if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
        {
            string uninstallExe = Path.Combine(targetDir, "Uninstall.exe");
            if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(uninstallExe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, uninstallExe, true);
            }
        }
    }

    public static void CreateShortcut(string targetExePath, string shortcutPath, string description)
    {
        try
        {
            string? dir = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetExePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
                shortcut.Description = description;
                shortcut.Save();
            }
        }
        catch { }
    }

    public static void RemoveShortcut(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
        catch { }
    }

    public static string GetDesktopShortcutPath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, $"{AppName}.lnk");
    }

    public static string GetStartMenuShortcutPath()
    {
        string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        return Path.Combine(startMenu, $"{AppName}.lnk");
    }

    public static void SetStartupRun(string targetExePath, bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    key.SetValue(AppName, $"\"{targetExePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }
        catch { }
    }

    public static void RegisterUninstall(string installDir)
    {
        try
        {
            string keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (key != null)
            {
                string targetExe = Path.Combine(installDir, "ClipboardApp.exe");
                string uninstallExe = Path.Combine(installDir, "Uninstall.exe");
                string uninstallCmd = File.Exists(uninstallExe)
                    ? $"\"{uninstallExe}\" --uninstall"
                    : $"\"{targetExe}\" --uninstall";

                key.SetValue("DisplayName", $"{AppName} v{AppVersion}");
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", Publisher);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", uninstallCmd);
                key.SetValue("DisplayIcon", $"\"{targetExe}\",0");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 160000, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    public static void UnregisterUninstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}", false);
        }
        catch { }
    }

    public static string? GetRegisteredInstallLocation()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");
            var loc = key?.GetValue("InstallLocation") as string;
            return string.IsNullOrWhiteSpace(loc) ? null : loc.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsSafeUninstallDirectory(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;

        string fullDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? root = Path.GetPathRoot(fullDir)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullDir, root, StringComparison.OrdinalIgnoreCase)) return false;

        var forbidden = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        foreach (var f in forbidden)
        {
            if (string.IsNullOrWhiteSpace(f)) continue;
            string normF = Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullDir, normF, StringComparison.OrdinalIgnoreCase)) return false;
        }

        // Only delete a directory that actually contains ClipboardApp.exe
        return File.Exists(Path.Combine(fullDir, "ClipboardApp.exe"));
    }

    public static void PerformUninstall(bool silent)
    {
        // Resolve registered InstallLocation BEFORE deleting the Uninstall registry key.
        string installDir = GetRegisteredInstallLocation() ?? GetDefaultInstallDir();

        KillRunningProcesses();
        RemoveShortcut(GetDesktopShortcutPath());
        RemoveShortcut(GetStartMenuShortcutPath());
        SetStartupRun("", false);
        UnregisterUninstall();

        if (!silent)
        {
            MessageBox.Show(
                "ClipboardApp has been successfully uninstalled.",
                "ClipboardApp Uninstall",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        if (!IsSafeUninstallDirectory(installDir)) return;

        try
        {
            // Run self-delete AFTER modal confirmation closes, passing path safely via PowerShell -LiteralPath.
            string escaped = installDir.Replace("'", "''");
            ProcessStartInfo psi = new()
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-WindowStyle");
            psi.ArgumentList.Add("Hidden");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add($"Start-Sleep -Seconds 2; Remove-Item -LiteralPath '{escaped}' -Recurse -Force -ErrorAction SilentlyContinue");
            Process.Start(psi);
        }
        catch { }
    }
}

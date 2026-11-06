using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipboardApp.Installer;

internal static class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        bool isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));

        bool isSilent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                                      a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                      a.Equals("/verysilent", StringComparison.OrdinalIgnoreCase));

        // Self-check: report what this installer would actually extract, without
        // touching the machine. Used by tools/verify-release.ps1 to prove that a
        // shipped setup exe embeds the portable build it was released with.
        string? verifyArg = args.FirstOrDefault(a =>
            a.StartsWith("--verify-payload", StringComparison.OrdinalIgnoreCase));
        if (verifyArg != null)
        {
            // Exit code carries the result; Environment.Exit is used because Main
            // returns Task (no int return) and we must not fall through to the UI.
            Environment.ExitCode = VerifyPayload(verifyArg);
            return;
        }

        if (isUninstall)
        {
            if (!isSilent)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to completely remove ClipboardApp from your computer?",
                    "Confirm Uninstall",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            SetupEngine.PerformUninstall(isSilent);
            return;
        }

        if (isSilent)
        {
            try
            {
                string targetDir = SetupEngine.GetDefaultInstallDir();
                SetupEngine.KillRunningProcesses();
                await SetupEngine.ExtractPayloadAsync(targetDir);
                string targetExe = Path.Combine(targetDir, "ClipboardApp.exe");
                SetupEngine.CreateShortcut(targetExe, SetupEngine.GetDesktopShortcutPath(), "Enterprise-Grade Windows Clipboard Manager");
                SetupEngine.CreateShortcut(targetExe, SetupEngine.GetStartMenuShortcutPath(), "Enterprise-Grade Windows Clipboard Manager");
                SetupEngine.SetStartupRun(targetExe, true);
                SetupEngine.RegisterUninstall(targetDir);
            }
            catch { }
            return;
        }

        Application.Run(new InstallerForm());
    }

    /// <summary>
    /// Decompresses the embedded payload exactly the way installation does,
    /// hashes the result, and compares it with a supplied portable executable.
    /// Exit code 0 = identical, 1 = different or unreadable, 2 = bad arguments.
    ///
    /// Usage: ClipboardApp-Setup.exe --verify-payload "path\to\portable.exe"
    /// </summary>
    private static int VerifyPayload(string argument)
    {
        string[] parts = argument.Split('=', 2);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            Console.Error.WriteLine("usage: ClipboardApp-Setup.exe --verify-payload=<portable.exe>");
            return 2;
        }

        string portablePath = parts[1].Trim('"');
        if (!File.Exists(portablePath))
        {
            Console.Error.WriteLine($"FAIL: {portablePath} not found");
            return 2;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var resource = assembly.GetManifestResourceStream("payload.bin");
        if (resource == null)
        {
            Console.Error.WriteLine("FAIL: this setup exe does not embed a payload.bin resource.");
            return 1;
        }

        string temp = Path.Combine(Path.GetTempPath(), "clipboardapp-payload-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            using (var gzip = new GZipStream(resource, CompressionMode.Decompress))
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                gzip.CopyTo(file);
            }

            string embeddedHash = ComputeSha256(temp);
            string portableHash = ComputeSha256(portablePath);
            long embeddedSize = new FileInfo(temp).Length;
            long portableSize = new FileInfo(portablePath).Length;

            Console.WriteLine($"  embedded payload  : {embeddedSize,15:N0} bytes  {embeddedHash}");
            Console.WriteLine($"  release portable  : {portableSize,15:N0} bytes  {portableHash}");

            if (!string.Equals(embeddedHash, portableHash, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("FAIL: the installer payload is NOT the portable build it ships with.");
                return 1;
            }

            Console.WriteLine("MATCH: the setup installer embeds a byte-identical copy of the portable.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: the embedded payload could not be read: " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}

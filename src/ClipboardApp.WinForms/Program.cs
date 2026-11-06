using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ClipboardApp.Core.Interop;

namespace ClipboardApp.WinForms
{
    internal static class Program
    {
        private const string MutexName = @"Local\ClipboardApp_SingleInstance_Mutex_v1";
        public const string ShowAppMessageName = "WM_SHOW_CLIPBOARD_APP";
        public static readonly uint WmShowApp = NativeMethods.RegisterWindowMessage(ShowAppMessageName);

        private static Mutex? s_mutex;

        [STAThread]
        static void Main()
        {
            // Single instance enforcement via named system Mutex
            s_mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running: wake it up and exit cleanly
                if (WmShowApp != 0)
                {
                    NativeMethods.PostMessage((IntPtr)0xffff /* HWND_BROADCAST */, WmShowApp, IntPtr.Zero, IntPtr.Zero);
                }
                return;
            }

            ApplicationConfiguration.Initialize();

            // Global exception handling: never crash silently, protect log directory
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => HandleCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                HandleCrash(e.ExceptionObject as Exception);

            var settings = AppSettings.Load();
            var mainForm = new MainForm(settings);

            // StartMinimizedToTray is enforced inside MainForm.OnShown via
            // HideToTray(), so the window, tray icon, clipboard monitor and
            // global hotkey are all fully initialised before it disappears.
            // No pre-Run WindowState juggling is needed here.

            try
            {
                Application.Run(mainForm);
            }
            finally
            {
                s_mutex?.ReleaseMutex();
                s_mutex?.Dispose();
            }
        }

        private static void HandleCrash(Exception? ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardApp");
                Directory.CreateDirectory(dir);
                var log = Path.Combine(dir, "crash.log");
                // Log full exception graph with InnerException and stack traces — NEVER log clipboard text
                File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }

            try
            {
                string detail = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown error";
                MessageBox.Show(
                    "An unexpected error occurred:\n\n" + detail +
                    "\n\nCrash diagnostics were logged to:\n%LocalAppData%\\ClipboardApp\\crash.log",
                    "Clipboard App", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}

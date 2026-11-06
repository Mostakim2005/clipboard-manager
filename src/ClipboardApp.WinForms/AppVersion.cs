using System;
using System.Diagnostics;
using System.Reflection;

namespace ClipboardApp.WinForms
{
    /// <summary>
    /// The application version as stamped by /Directory.Build.props. Everything
    /// user-visible (window title, tray tooltip, setup wizard) reads this instead
    /// of hard-coding a version string.
    /// </summary>
    public static class AppVersion
    {
        /// <summary>e.g. "3.1.0".</summary>
        public static string SemVer { get; } = Resolve();

        public static string Display => "v" + SemVer;

        private static string Resolve()
        {
            try
            {
                var asm = typeof(AppVersion).Assembly;
                var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                {
                    // Strip any "+<commit-hash>" build metadata.
                    int plus = informational.IndexOf('+');
                    return (plus > 0 ? informational.Substring(0, plus) : informational).Trim();
                }

                var version = asm.GetName().Version;
                if (version != null)
                {
                    return version.Build > 0
                        ? $"{version.Major}.{version.Minor}.{version.Build}"
                        : $"{version.Major}.{version.Minor}.0";
                }
            }
            catch
            {
                // Never let version discovery break startup.
            }

            try
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
                if (!string.IsNullOrWhiteSpace(fileVersion)) return fileVersion;
            }
            catch { }

            return "0.0.0";
        }
    }
}

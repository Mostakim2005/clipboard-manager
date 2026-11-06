using ClipboardApp.WinForms;
using Xunit;

namespace ClipboardApp.Tests
{
    /// <summary>
    /// Version consistency guards. The version is defined once in
    /// /Directory.Build.props; these tests fail if a binary was built without it.
    /// </summary>
    public class VersionTests
    {
        [Fact]
        public void AppVersionIsSemantic()
        {
            var parts = AppVersion.SemVer.Split('.');
            Assert.Equal(3, parts.Length);
            foreach (var part in parts)
            {
                Assert.True(int.TryParse(part, out _), $"'{part}' is not numeric in {AppVersion.SemVer}");
            }
        }

        [Fact]
        public void AppVersionMatchesTheAssemblyVersion()
        {
            var asmVersion = typeof(AppVersion).Assembly.GetName().Version;
            Assert.NotNull(asmVersion);
            Assert.Equal($"{asmVersion!.Major}.{asmVersion.Minor}.{asmVersion.Build}", AppVersion.SemVer);
        }

        [Fact]
        public void DisplayHasVeePrefix()
        {
            Assert.Equal("v" + AppVersion.SemVer, AppVersion.Display);
        }

        [Fact]
        public void SettingsDefaultsMatchDocumentedBehaviour()
        {
            var settings = new AppSettings();
            Assert.True(settings.StartMinimizedToTray);
            Assert.Equal("ListPreview", settings.ViewMode);
            Assert.Equal(0, settings.GridColumns);
            Assert.False(settings.EnableDoubleClickPaste);
            Assert.False(settings.EnableMixedMerge);
            Assert.False(settings.RespectPrivacyFlags);
            Assert.Equal(15, settings.AutoClearSensitiveMinutes);
            Assert.Equal(5, settings.ApiVaultAutoLockMinutes);
        }
    }
}

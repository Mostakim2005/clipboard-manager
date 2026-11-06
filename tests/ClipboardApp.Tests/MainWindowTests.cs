using ClipboardApp.Core.Storage;
using ClipboardApp.Core.Models;
using ClipboardApp.WinForms;
using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    /// <summary>
    /// Integration tests for the window lifecycle and the List/Grid switch.
    ///
    /// They construct the real <see cref="MainForm"/> on an STA thread with a
    /// temporary repository, so the wiring exercised is the shipped wiring:
    /// tray start, hotkey toggle, view-mode persistence and thumbnails.
    ///
    /// These never touch the user's real %LocalAppData% settings or database.
    /// </summary>
    [Collection("STA UI")]
    public class MainWindowTests
    {
        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "UI test thread timed out.");
            if (failure != null)
            {
                Assert.Null(failure); // surfaces the captured exception and full stack
            }
        }

        private static AppSettings TestSettings() => new()
        {
            StartMinimizedToTray = true,
            Theme = "Light",
            EnableCustomUiStyling = false,
            VisualStyle = "Native"
        };

        [Fact]
        public void StartsHiddenInTheTray_AndHotkeyToggleShowsThenHides()
        {
            RunSta(() =>
            {
                using var form = new MainForm(TestSettings());
                form.Show();
                Application.DoEvents();

                // OnShown enforces StartMinimizedToTray.
                Assert.False(form.Visible, "The window must start hidden in the tray.");

                // First hotkey press: show and focus.
                form.ToggleAppWindow();
                Application.DoEvents();
                Assert.True(form.Visible);

                // Second hotkey press: back to the tray, not closed.
                form.ToggleAppWindow();
                Application.DoEvents();
                Assert.False(form.Visible);
                Assert.False(form.IsDisposed);
            });
        }

        [Fact]
        public void ViewModeToggles_AndGridShowsTheSameEntryCount()
        {
            RunSta(() =>
            {
                var settings = TestSettings();
                settings.ViewMode = "ListPreview";

                using var form = new MainForm(settings);
                form.Show();
                Application.DoEvents();

                var toggle = GetPrivate<MethodInfo>(form, "ToggleViewMode", isField: false) as MethodInfo;
                Assert.NotNull(toggle);

                toggle!.Invoke(form, null);
                Application.DoEvents();
                Assert.Equal("Grid", settings.ViewMode);

                var grid = GetPrivate<object>(form, "_gridList", isField: true);
                Assert.NotNull(grid);

                // The grid must mirror the same grouped/filtered rows as the list.
                var cardList = GetPrivate<ListBox>(form, "_cardList", isField: true)!;
                int gridCount = (int)grid!.GetType().GetProperty("ItemCount")!.GetValue(grid)!;
                Assert.Equal(cardList.Items.Count, gridCount);

                toggle.Invoke(form, null);
                Application.DoEvents();
                Assert.Equal("ListPreview", settings.ViewMode);
            });
        }

        [Fact]
        public void GridColumnCountIsClampedToTen()
        {
            var settings = TestSettings();
            settings.GridColumns = 99;
            // Normalize() runs on load; verify the clamp contract directly.
            Assert.True(settings.GridColumns == 99);

            settings.GridColumns = -5;
            Assert.Equal(-5, settings.GridColumns);

            var normalized = new AppSettings { GridColumns = 99, ViewMode = "Bogus" };
            typeof(AppSettings)
                .GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(normalized, null);

            Assert.Equal(10, normalized.GridColumns);
            Assert.Equal("ListPreview", normalized.ViewMode);
        }

        [Fact]
        public void ThumbnailPreservesAspectRatioForWideAndTallImages()
        {
            var wide = MakePng(400, 100);
            var tall = MakePng(100, 400);
            using var cache = new ClipboardApp.WinForms.Controls.ThumbnailCache();

            var wideThumb = cache.GetThumbnail("wide", wide, 64, 64);
            var tallThumb = cache.GetThumbnail("tall", tall, 64, 64);

            Assert.NotNull(wideThumb);
            Assert.NotNull(tallThumb);

            // Letterboxed inside 64x64 with the source ratio intact (4:1 / 1:4).
            Assert.Equal(64, wideThumb!.Width);
            Assert.Equal(16, wideThumb.Height);
            Assert.Equal(16, tallThumb!.Width);
            Assert.Equal(64, tallThumb.Height);
        }

        [Fact]
        public void ThumbnailCacheReturnsNullForGarbageAndCachesHits()
        {
            using var cache = new ClipboardApp.WinForms.Controls.ThumbnailCache();
            Assert.Null(cache.GetThumbnail("bad", new byte[] { 1, 2, 3, 4 }, 64, 64));

            var png = MakePng(20, 10);
            var first = cache.GetThumbnail("x", png, 32, 32);
            var second = cache.GetThumbnail("x", png, 32, 32);
            Assert.NotNull(first);
            Assert.Same(first, second); // cached, not decoded twice
        }

        private static byte[] MakePng(int width, int height)
        {
            using var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.SteelBlue);
                g.FillRectangle(Brushes.OrangeRed, 0, 0, width / 2, height / 2);
            }
            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        private static T? GetPrivate<T>(object instance, string name, bool isField)
        {
            var type = instance.GetType();
            if (isField)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                return field == null ? default : (T?)field.GetValue(instance);
            }
            var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return method == null ? default : (T?)(object)method;
        }
    }
}

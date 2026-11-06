using ClipboardApp.WinForms;
using System.Drawing;
using Xunit;

namespace ClipboardApp.Tests
{
    public class AppIconTests
    {
        // The tray icon must load from the embedded multi-resolution ICO at every
        // size Windows may ask for. A PNG-in-ICO file used to throw here.
        [Theory]
        [InlineData(16)]
        [InlineData(20)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(256)]
        public void EmbeddedAppIconLoadsAtRequestedSize(int size)
        {
            using var icon = AppIcon.Load(size);
            Assert.NotNull(icon);
            Assert.True(icon.Width > 0 && icon.Height > 0);
        }

        [Fact]
        public void EmbeddedAppIconHasVisiblePixels()
        {
            using var icon = AppIcon.Load(32);
            using var bitmap = icon.ToBitmap();
            Assert.Equal(32, bitmap.Width);
            Assert.Equal(32, bitmap.Height);

            // Center of the logo must be opaque; corners stay transparent.
            var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            var corner = bitmap.GetPixel(0, 0);
            Assert.True(center.A > 0);
            Assert.Equal(0, corner.A);
        }
    }
}

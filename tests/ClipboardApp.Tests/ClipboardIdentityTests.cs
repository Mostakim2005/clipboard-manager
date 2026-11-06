using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using Xunit;

namespace ClipboardApp.Tests
{
    public class ClipboardIdentityTests
    {
        [Fact]
        public void NormalizedLineEndingsProduceSameHash()
        {
            var a = new ClipboardEntry { Content = "line1\r\nline2", Format = "text" };
            var b = new ClipboardEntry { Content = "line1\nline2", Format = "text" };

            Assert.Equal(ClipboardContentIdentity.Compute(a), ClipboardContentIdentity.Compute(b));
        }

        [Fact]
        public void DifferentFormatsDoNotCollide()
        {
            var text = new ClipboardEntry { Content = "<b>x</b>", Format = "text" };
            var html = new ClipboardEntry { Content = "<b>x</b>", Format = "html" };

            Assert.NotEqual(ClipboardContentIdentity.Compute(text), ClipboardContentIdentity.Compute(html));
            Assert.StartsWith("txt:", ClipboardContentIdentity.Compute(text));
            Assert.StartsWith("html:", ClipboardContentIdentity.Compute(html));
        }

        [Fact]
        public void ImageIdentityUsesBytes()
        {
            var image = new ClipboardEntry
            {
                Format = "image",
                HasImage = true,
                ImageData = new byte[] { 1, 2, 3, 4 }
            };

            Assert.StartsWith("img:", ClipboardContentIdentity.Compute(image));
        }

        [Fact]
        public void FileIdentityIsDistinctFromText()
        {
            var text = new ClipboardEntry { Content = @"C:\temp\a.txt", Format = "text" };
            var file = new ClipboardEntry { Content = @"C:\temp\a.txt", Format = "file" };

            Assert.NotEqual(ClipboardContentIdentity.Compute(text), ClipboardContentIdentity.Compute(file));
            Assert.StartsWith("file:", ClipboardContentIdentity.Compute(file));
        }
    }
}

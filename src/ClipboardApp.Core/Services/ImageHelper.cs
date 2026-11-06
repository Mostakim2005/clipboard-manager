using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ClipboardApp.Core.Services
{
    public static class ImageHelper
    {
        public static bool TryGetImageFromClipboard(IDataObject? data, out byte[]? pngBytes, out Image? image)
        {
            pngBytes = null;
            image = null;
            if (data == null) return false;

            Image? img = null;

            // 1. Standard WinForms Clipboard.GetImage()
            try
            {
                if (data.GetDataPresent(DataFormats.Bitmap))
                {
                    img = Clipboard.GetImage();
                }
            }
            catch { }

            // 2. CF_DIB / DeviceIndependentBitmap fallback (Chrome, Edge, Electron apps)
            if (img == null)
            {
                try
                {
                    if (data.GetDataPresent("DeviceIndependentBitmap"))
                    {
                        var dibObj = data.GetData("DeviceIndependentBitmap");
                        if (dibObj is Stream dibStream)
                        {
                            img = GetBitmapFromDib(dibStream);
                        }
                    }
                    else if (data.GetDataPresent(DataFormats.Dib))
                    {
                        var dibObj = data.GetData(DataFormats.Dib);
                        if (dibObj is Stream dibStream)
                        {
                            img = GetBitmapFromDib(dibStream);
                        }
                    }
                }
                catch { }
            }

            if (img != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    img.Save(ms, ImageFormat.Png);
                    pngBytes = ms.ToArray();
                    image = img;
                    return true;
                }
                catch
                {
                    img.Dispose();
                }
            }

            return false;
        }

        public static Bitmap? GetBitmapFromDib(Stream dibStream)
        {
            using var reader = new BinaryReader(dibStream);
            var dibBytes = reader.ReadBytes((int)dibStream.Length);
            if (dibBytes.Length < 40) return null;

            int headerSize = BitConverter.ToInt32(dibBytes, 0);
            short bitCount = BitConverter.ToInt16(dibBytes, 14);
            int compression = dibBytes.Length >= 20 ? BitConverter.ToInt32(dibBytes, 16) : 0;
            int clrUsed = BitConverter.ToInt32(dibBytes, 32);

            int maskSize = 0;
            if (compression == 3 /* BI_BITFIELDS */ && headerSize == 40)
            {
                maskSize = 12;
            }

            int paletteSize = (clrUsed > 0 ? clrUsed : (bitCount <= 8 ? (1 << bitCount) : 0)) * 4;
            int offBits = 14 + headerSize + maskSize + paletteSize;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            // Write 14-byte BITMAPFILEHEADER
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(14 + dibBytes.Length); // Total file size
            writer.Write((short)0);             // Reserved 1
            writer.Write((short)0);             // Reserved 2
            writer.Write(offBits);              // Offset to pixel bits
            writer.Write(dibBytes);

            ms.Position = 0;
            return new Bitmap(ms);
        }
    }
}

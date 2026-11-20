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
            if (dibStream == null || dibStream.Length < 40 || dibStream.Length > 100 * 1024 * 1024) return null;

            using var reader = new BinaryReader(dibStream, System.Text.Encoding.Default, leaveOpen: true);
            var dibBytes = reader.ReadBytes((int)dibStream.Length);
            if (dibBytes.Length < 40) return null;

            int headerSize = BitConverter.ToInt32(dibBytes, 0);
            short bitCount = BitConverter.ToInt16(dibBytes, 14);
            int compression = BitConverter.ToInt32(dibBytes, 16);
            int clrUsed = BitConverter.ToInt32(dibBytes, 32);

            if (headerSize < 40 || headerSize > dibBytes.Length) return null;
            if (bitCount < 0 || bitCount > 64 || clrUsed < 0 || clrUsed > 65536) return null;

            int maskSize = (compression == 3 /* BI_BITFIELDS */ && headerSize == 40) ? 12 : 0;
            long paletteEntries = clrUsed > 0 ? clrUsed : (bitCount <= 8 ? (1L << bitCount) : 0L);
            long offBitsLong = 14L + headerSize + maskSize + paletteEntries * 4L;
            if (offBitsLong < 54 || offBitsLong > 14L + dibBytes.Length) return null;

            using var ms = new MemoryStream(14 + dibBytes.Length);
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
            {
                writer.Write((byte)'B');
                writer.Write((byte)'M');
                writer.Write(14 + dibBytes.Length);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((int)offBitsLong);
                writer.Write(dibBytes);
            }

            ms.Position = 0;
            // Clone into an independent Bitmap so disposing ms never triggers GDI+ ExternalException on Save().
            using var temp = new Bitmap(ms);
            return new Bitmap(temp);
        }
    }
}

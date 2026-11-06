using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Builds a multi-resolution Windows ICO using uncompressed 32-bit BMP/DIB
/// frames. PNG-in-ICO frames are rejected by some .NET Icon/NotifyIcon paths,
/// so this keeps the tray icon reliable on every Windows version.
/// </summary>
public static class IcoBuilder
{
    public static void Build(string outputPath, string[] pngPaths)
    {
        var frames = new byte[pngPaths.Length][];
        var sizes = new int[pngPaths.Length];

        for (int i = 0; i < pngPaths.Length; i++)
        {
            using (var source = new Bitmap(pngPaths[i]))
            using (var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source, 0, 0, bmp.Width, bmp.Height);
                }
                sizes[i] = bmp.Width;
                frames[i] = BuildFrame(bmp);
            }
        }

        using (var fs = File.Create(outputPath))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)frames.Length);

            int offset = 6 + (16 * frames.Length);
            for (int i = 0; i < frames.Length; i++)
            {
                byte dimension = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                bw.Write(dimension);
                bw.Write(dimension);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)frames[i].Length);
                bw.Write((uint)offset);
                offset += frames[i].Length;
            }

            foreach (var frame in frames)
            {
                bw.Write(frame);
            }
            bw.Flush();
        }
    }

    private static byte[] BuildFrame(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        int maskStride = ((width + 31) / 32) * 4;
        int imageSize = width * height * 4 + maskStride * height;

        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40);
            w.Write(width);
            w.Write(height * 2);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(0);
            w.Write(imageSize);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);

            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[width * 4];
                for (int y = height - 1; y >= 0; y--)
                {
                    IntPtr rowPtr = new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride);
                    Marshal.Copy(rowPtr, row, 0, row.Length);
                    w.Write(row);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            byte[] mask = new byte[maskStride * height];
            w.Write(mask);
            w.Flush();
            return ms.ToArray();
        }
    }
}

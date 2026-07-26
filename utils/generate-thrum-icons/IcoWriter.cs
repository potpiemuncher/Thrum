// Thrum icon generator — .ico container writer.
//
// Copyright (C) 2026  Thrum project
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using System.Drawing.Imaging;

namespace Thrum.IconGenerator;

/// <summary>
/// Assembles a multi-resolution <c>.ico</c> by hand.
/// </summary>
/// <remarks>
/// <para>
/// This exists because neither <see cref="Icon"/> nor WIC can *write* a
/// multi-frame icon from managed code, and the frame mix matters: the two
/// stacks that load these files disagree about what they accept.
/// </para>
/// <list type="bullet">
/// <item>
/// WPF (<c>BitmapFrame</c> / WIC, which is what an <c>ImageSource</c> pack URI
/// resolves through) reads PNG-compressed frames happily.
/// </item>
/// <item>
/// GDI (<see cref="Icon"/>, which is what H.NotifyIcon hands to
/// <c>Shell_NotifyIcon</c>) picks a frame by size before decoding. Classic
/// uncompressed BMP frames at the small sizes are therefore load bearing —
/// a PNG-only icon is a runtime tray failure that no build step reports.
/// </item>
/// </list>
/// <para>
/// So: BMP frames at the tray/shell sizes, PNG frames above them where the
/// uncompressed cost stops being worth paying (a 256x256 BMP frame is 256 KiB
/// on its own).
/// </para>
/// </remarks>
internal static class IcoWriter
{
    /// <summary>Sizes stored as classic uncompressed 32-bit BMP frames.</summary>
    internal static readonly int[] BmpFrameSizes = { 16, 24, 32, 48 };

    /// <summary>Sizes stored as PNG frames.</summary>
    internal static readonly int[] PngFrameSizes = { 64, 128, 256 };

    /// <summary>Every size a generated icon carries, ascending.</summary>
    internal static readonly int[] AllFrameSizes =
        BmpFrameSizes.Concat(PngFrameSizes).ToArray();

    /// <summary>
    /// Renders one frame per entry in <see cref="AllFrameSizes"/> using
    /// <paramref name="render"/> and writes the assembled icon to
    /// <paramref name="path"/>.
    /// </summary>
    internal static void Write(string path, Action<Graphics, int> render)
    {
        var frames = new List<(int Size, bool Png, byte[] Data)>();

        foreach (int size in AllFrameSizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                render(graphics, size);
            }

            bool png = PngFrameSizes.Contains(size);
            frames.Add((size, png, png ? EncodePng(bitmap) : EncodeBmpFrame(bitmap)));
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(output);

        // ICONDIR
        writer.Write((ushort)0);              // idReserved
        writer.Write((ushort)1);              // idType: 1 = icon
        writer.Write((ushort)frames.Count);   // idCount

        const int DirEntrySize = 16;
        int offset = 6 + (DirEntrySize * frames.Count);
        foreach ((int size, _, byte[] data) in frames)
        {
            // ICONDIRENTRY. 256 is encoded as 0 — the field is a single byte.
            writer.Write((byte)(size >= 256 ? 0 : size));   // bWidth
            writer.Write((byte)(size >= 256 ? 0 : size));   // bHeight
            writer.Write((byte)0);                          // bColorCount (0 = >=8bpp)
            writer.Write((byte)0);                          // bReserved, must be 0
            writer.Write((ushort)1);                        // wPlanes
            writer.Write((ushort)32);                       // wBitCount
            writer.Write((uint)data.Length);                // dwBytesInRes
            writer.Write((uint)offset);                     // dwImageOffset
            offset += data.Length;
        }

        foreach ((_, _, byte[] data) in frames)
        {
            writer.Write(data);
        }
    }

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, ImageFormat.Png);
        return buffer.ToArray();
    }

    /// <summary>
    /// Encodes a frame the way a pre-Vista icon does: a BITMAPINFOHEADER whose
    /// declared height is doubled, a bottom-up 32-bit BGRA colour bitmap, then
    /// a 1bpp AND mask.
    /// </summary>
    /// <remarks>
    /// The AND mask is redundant for a 32-bit frame — the alpha channel already
    /// says what is transparent — but it is not optional: the structure is
    /// sized from the doubled height, and a decoder that falls back to the mask
    /// (any path that ends up below 32bpp) renders an opaque black box without
    /// it. It is derived from alpha rather than left blank for that reason.
    /// </remarks>
    private static byte[] EncodeBmpFrame(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        BitmapData locked = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        byte[] pixels = new byte[width * height * 4];
        try
        {
            for (int row = 0; row < height; row++)
            {
                IntPtr source = locked.Scan0 + (row * locked.Stride);
                System.Runtime.InteropServices.Marshal.Copy(
                    source, pixels, row * width * 4, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(locked);
        }

        int maskStride = ((width + 31) / 32) * 4;
        int xorSize = width * height * 4;
        int andSize = maskStride * height;

        using var buffer = new MemoryStream(40 + xorSize + andSize);
        using var writer = new BinaryWriter(buffer);

        // BITMAPINFOHEADER
        writer.Write(40);                       // biSize
        writer.Write(width);                    // biWidth
        writer.Write(height * 2);               // biHeight: colour + mask
        writer.Write((ushort)1);                // biPlanes
        writer.Write((ushort)32);               // biBitCount
        writer.Write(0);                        // biCompression: BI_RGB
        writer.Write(xorSize + andSize);        // biSizeImage
        writer.Write(0);                        // biXPelsPerMeter
        writer.Write(0);                        // biYPelsPerMeter
        writer.Write(0);                        // biClrUsed
        writer.Write(0);                        // biClrImportant

        // Colour bitmap, bottom-up. Format32bppArgb is already BGRA in memory.
        for (int row = height - 1; row >= 0; row--)
        {
            writer.Write(pixels, row * width * 4, width * 4);
        }

        // AND mask, bottom-up, one bit per pixel, set where fully transparent.
        byte[] maskRow = new byte[maskStride];
        for (int row = height - 1; row >= 0; row--)
        {
            Array.Clear(maskRow);
            for (int column = 0; column < width; column++)
            {
                byte alpha = pixels[(row * width * 4) + (column * 4) + 3];
                if (alpha == 0)
                {
                    maskRow[column / 8] |= (byte)(0x80 >> (column % 8));
                }
            }

            writer.Write(maskRow);
        }

        writer.Flush();
        return buffer.ToArray();
    }
}

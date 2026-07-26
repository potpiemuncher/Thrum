// Thrum icon generator — the placeholder mark itself.
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
using System.Drawing.Drawing2D;

namespace Thrum.IconGenerator;

/// <summary>
/// Draws Thrum's placeholder brand mark: a rounded square carrying a bold
/// letter T.
/// </summary>
/// <remarks>
/// <para>
/// Two constraints shape every number in this file. The first is that the
/// smallest frame is 16x16, where the mark is roughly a dozen usable pixels
/// across; so the T is a hand-built polygon snapped to integer pixel
/// boundaries rather than rendered text, because a font at that size turns to
/// grey mush and depends on whatever is installed on the build machine.
/// </para>
/// <para>
/// The second is that the accent colour has to read against both a light and a
/// dark taskbar, and must not be mistaken for a first-party console brand.
/// It is a deep violet — deliberately nowhere near PlayStation blue, Xbox
/// green, or Nintendo red.
/// </para>
/// </remarks>
internal static class ThrumMark
{
    /// <summary>The brand accent. Deep violet; not PlayStation blue.</summary>
    internal static readonly Color Accent = Color.FromArgb(0xFF, 0x6D, 0x28, 0xD9);

    /// <summary>Glyph colour on the coloured variant.</summary>
    internal static readonly Color Glyph = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

    /// <summary>Unfilled portion of a battery bar.</summary>
    internal static readonly Color BatteryTrack = Color.FromArgb(0x66, 0x00, 0x00, 0x00);

    /// <summary>Battery bar at or below 10 percent.</summary>
    internal static readonly Color BatteryCritical = Color.FromArgb(0xFF, 0xEF, 0x44, 0x44);

    /// <summary>Battery bar below 40 percent.</summary>
    internal static readonly Color BatteryLow = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B);

    /// <summary>Battery bar at or above 40 percent.</summary>
    internal static readonly Color BatteryGood = Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E);

    /// <summary>
    /// The coloured mark: an accent rounded square with a white T.
    /// </summary>
    internal static void DrawColored(Graphics graphics, int size)
    {
        Rectangle plate = Plate(size);
        using var body = RoundedRectangle(plate, CornerRadius(plate));
        using var accent = new SolidBrush(Accent);
        graphics.FillPath(accent, body);

        using var letter = LetterT(plate);
        using var glyph = new SolidBrush(Glyph);
        graphics.FillPath(glyph, letter);
    }

    /// <summary>
    /// The monochrome mark: a solid rounded square in <paramref name="ink"/>
    /// with the T knocked out to transparency.
    /// </summary>
    /// <remarks>
    /// A knockout rather than a coloured glyph, so the one shape works on any
    /// taskbar tint — the T shows whatever is behind the icon. The White
    /// variant is for dark taskbars and the Black variant for light ones,
    /// matching what the inherited tray-icon setting already meant.
    /// </remarks>
    internal static void DrawMonochrome(Graphics graphics, int size, Color ink)
    {
        Rectangle plate = Plate(size);
        using var path = new GraphicsPath(FillMode.Alternate);
        using (var body = RoundedRectangle(plate, CornerRadius(plate)))
        {
            path.AddPath(body, false);
        }

        using (var letter = LetterT(plate))
        {
            path.AddPath(letter, false);
        }

        using var brush = new SolidBrush(ink);
        graphics.FillPath(brush, path);
    }

    /// <summary>
    /// A battery variant: the coloured mark with its glyph lifted into the
    /// upper portion of the plate, and a fill bar across the bottom whose
    /// width is proportional to <paramref name="percent"/>.
    /// </summary>
    /// <remarks>
    /// No digits. Two reasons: legible numerals need about 9 pixels of height
    /// and there are 14 to spend at 16x16 for the whole icon, and a bar keeps
    /// the brand mark recognisable at a glance where a number does not. The
    /// bar is also colour-coded, which costs nothing and is the part a user
    /// actually reads from the corner of their eye.
    /// </remarks>
    internal static void DrawBattery(Graphics graphics, int size, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);

        Rectangle plate = Plate(size);
        using var body = RoundedRectangle(plate, CornerRadius(plate));
        using (var accent = new SolidBrush(Accent))
        {
            graphics.FillPath(accent, body);
        }

        // Glyph occupies the top ~68% so the bar has honest room beneath it.
        var glyphBox = new Rectangle(
            plate.X, plate.Y, plate.Width, Snap(plate.Height * 0.68));
        using (var letter = LetterT(glyphBox))
        using (var glyph = new SolidBrush(Glyph))
        {
            graphics.FillPath(glyph, letter);
        }

        int pad = Math.Max(1, Snap(plate.Width * 0.10));
        int barTop = plate.Y + Snap(plate.Height * 0.72);
        int barBottom = plate.Bottom - pad;
        int barHeight = Math.Max(1, barBottom - barTop);
        int barLeft = plate.X + pad;
        int barWidth = Math.Max(1, plate.Width - (pad * 2));

        // The track is always drawn, so 0% is an empty bar rather than a
        // missing one — "no bar" and "no battery data" must not look alike.
        using (var track = new SolidBrush(BatteryTrack))
        {
            graphics.FillRectangle(track, barLeft, barTop, barWidth, barHeight);
        }

        int fillWidth = (int)Math.Round(barWidth * (percent / 100.0),
            MidpointRounding.AwayFromZero);
        if (percent > 0)
        {
            // Never round a non-zero charge down to nothing.
            fillWidth = Math.Max(1, fillWidth);
        }

        if (fillWidth > 0)
        {
            Color level = percent <= 10 ? BatteryCritical
                : percent < 40 ? BatteryLow
                : BatteryGood;
            using var fill = new SolidBrush(level);
            graphics.FillRectangle(fill, barLeft, barTop, fillWidth, barHeight);
        }
    }

    /// <summary>The rounded square's bounds inside a frame of the given size.</summary>
    private static Rectangle Plate(int size)
    {
        int inset = Math.Max(1, Snap(size * 0.0625));
        return new Rectangle(inset, inset, size - (inset * 2), size - (inset * 2));
    }

    private static float CornerRadius(Rectangle plate) => plate.Width * 0.22f;

    /// <summary>
    /// The T as a single eight-point polygon.
    /// </summary>
    /// <remarks>
    /// Deliberately one closed figure rather than two overlapping rectangles:
    /// <see cref="DrawMonochrome"/> relies on <see cref="FillMode.Alternate"/>
    /// to punch it out of the plate, and two overlapping figures would fill
    /// their intersection back in.
    /// </remarks>
    private static GraphicsPath LetterT(Rectangle box)
    {
        int left = box.X + Snap(box.Width * 0.18);
        int right = box.X + Snap(box.Width * 0.82);
        int top = box.Y + Snap(box.Height * 0.22);
        int bottom = box.Y + Snap(box.Height * 0.80);

        int bar = Math.Max(2, Snap(box.Height * 0.18));
        int stem = Math.Max(2, Snap(box.Width * 0.18));
        int barBottom = top + bar;
        int stemLeft = box.X + Snap((box.Width - stem) / 2.0);
        int stemRight = stemLeft + stem;

        var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new Point(left, top),
            new Point(right, top),
            new Point(right, barBottom),
            new Point(stemRight, barBottom),
            new Point(stemRight, bottom),
            new Point(stemLeft, bottom),
            new Point(stemLeft, barBottom),
            new Point(left, barBottom),
        });
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Rounds to a whole pixel. Every edge in the mark is snapped, because at
    /// 16x16 a half-pixel edge is a visibly blurred one.
    /// </summary>
    private static int Snap(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

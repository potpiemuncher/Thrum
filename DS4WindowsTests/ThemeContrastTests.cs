/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Measures the contrast of text brushes against the surface they sit on.
///
/// <para><c>ThemeResourceTests</c> proves every brush a style binds to exists in
/// both dictionaries. That is a different question from whether the result can
/// be read: the Trigger Lab's disabled controls passed the parity test while
/// rendering as blank grey blocks in dark mode, measured at 1:1 - the glyph was
/// not drawn in a different colour from its background at all (issue #50). Key
/// existence and legibility need separate guards.</para>
/// </summary>
[TestClass]
public class ThemeContrastTests
{
    /// <summary>WCAG AA for normal-size text.</summary>
    private const double MinimumContrast = 4.5;

    [DataTestMethod]
    [DataRow("DefaultTheme")]
    [DataRow("DarkTheme")]
    public void DisabledTextRemainsLegibleAgainstCardBackgrounds(string theme)
    {
        string path = Path.Combine(FindRepositoryRoot(), "DS4Windows",
            "DS4Forms", "Themes", theme + ".xaml");
        string xaml = File.ReadAllText(path);

        string disabled = ReadBrush(xaml, "DisabledForegroundColor", theme);
        string card = ReadBrush(xaml, "CardBackgroundColor", theme);

        double ratio = Contrast(disabled, card);
        Assert.IsTrue(ratio >= MinimumContrast,
            theme + ": disabled text " + disabled + " on card " + card +
            " measures " + ratio.ToString("F2", CultureInfo.InvariantCulture) +
            ":1, below the " + MinimumContrast + ":1 floor. Disabled does not " +
            "mean invisible - a user still has to read what is unavailable.");
    }

    [DataTestMethod]
    [DataRow("DefaultTheme")]
    [DataRow("DarkTheme")]
    public void MutedTextRemainsLegibleAgainstCardBackgrounds(string theme)
    {
        string path = Path.Combine(FindRepositoryRoot(), "DS4Windows",
            "DS4Forms", "Themes", theme + ".xaml");
        string xaml = File.ReadAllText(path);

        double ratio = Contrast(ReadBrush(xaml, "MutedForegroundColor", theme),
            ReadBrush(xaml, "CardBackgroundColor", theme));
        Assert.IsTrue(ratio >= MinimumContrast,
            theme + ": muted text measures " +
            ratio.ToString("F2", CultureInfo.InvariantCulture) + ":1.");
    }

    private static string ReadBrush(string xaml, string key, string theme)
    {
        Match match = Regex.Match(xaml,
            "x:Key=\"" + Regex.Escape(key) +
            "\"\\s+Color=\"(?<c>#[0-9A-Fa-f]{6,8})\"");
        Assert.IsTrue(match.Success,
            theme + " has no literal colour for " + key +
            "; if it moved to a system colour this guard needs rewriting " +
            "rather than deleting.");
        return match.Groups["c"].Value;
    }

    /// <summary>WCAG 2.x relative-luminance contrast ratio.</summary>
    private static double Contrast(string first, string second)
    {
        double a = Luminance(first);
        double b = Luminance(second);
        double high = Math.Max(a, b);
        double low = Math.Min(a, b);
        return (high + 0.05) / (low + 0.05);
    }

    private static double Luminance(string hex)
    {
        string rgb = hex.TrimStart('#');
        if (rgb.Length == 8)
        {
            // #AARRGGBB - drop the alpha; these are opaque surface brushes.
            rgb = rgb.Substring(2);
        }

        double Channel(int offset)
        {
            double c = Convert.ToInt32(rgb.Substring(offset, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) +
            0.0722 * Channel(4);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                "DS4WindowsWPF.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}

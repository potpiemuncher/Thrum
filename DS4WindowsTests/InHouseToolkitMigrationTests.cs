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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

[TestClass]
public class InHouseToolkitMigrationTests
{
    private static readonly string[] ViewFiles =
    {
        "AxialStickUserControl.xaml",
        "BindingWindow.xaml",
        "LightbarMacroCreator.xaml",
        "MainWindow.xaml",
        "ProfileEditor.xaml",
        "RecordBox.xaml",
        "SpecialActionEditor.xaml",
    };

    [TestMethod]
    public void EveryToolkitControlInstanceWasMigratedWithoutCountDrift()
    {
        string forms = Path.Combine(FindRepositoryRoot(), "DS4Windows",
            "DS4Forms");
        string xaml = string.Join("\n", ViewFiles.Select(file =>
            File.ReadAllText(Path.Combine(forms, file))));

        Assert.AreEqual(53, ElementCount(xaml, "IntegerUpDown"));
        Assert.AreEqual(92, ElementCount(xaml, "DoubleUpDown"));
        Assert.AreEqual(5, ElementCount(xaml, "DecimalUpDown"));
        Assert.AreEqual(4, ElementCount(xaml, "SByteUpDown"));
        Assert.AreEqual(1, ElementCount(xaml, "UIntegerUpDown"));
        Assert.AreEqual(1, ElementCount(xaml, "SplitButton"));
    }

    [TestMethod]
    public void ShippingSourceHasNoToolkitNamespacePackageOrCopiedGlyphs()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "DS4Windows",
            "DS4WinWPF.csproj"));
        string source = ReadShippingSource(Path.Combine(root, "DS4Windows"));
        string darkTheme = File.ReadAllText(Path.Combine(root, "DS4Windows",
            "DS4Forms", "Themes", "DarkTheme.xaml"));

        Assert.IsFalse(project.Contains(
            "DotNetProjects.Extended.Wpf.Toolkit",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains(
            "http://schemas.xceed.com/wpf/xaml/toolkit",
            StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Xceed.Wpf.Toolkit",
            StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(
            "DotNetProjects.Wpf.Extended.Toolkit",
            StringComparison.Ordinal));
        Assert.IsFalse(darkTheme.Contains("UpArrowGeometry",
            StringComparison.Ordinal));
        Assert.IsFalse(darkTheme.Contains("DownArrowGeometry",
            StringComparison.Ordinal));
        Assert.IsFalse(darkTheme.Contains(
            "github.com/dotnetprojects/WpfExtendedToolkit",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ColorPickerImplementationDoesNotLeakToCallers()
    {
        string forms = Path.Combine(FindRepositoryRoot(), "DS4Windows",
            "DS4Forms");
        string callers = string.Join("\n", Directory.GetFiles(forms, "*.cs",
            SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("ColorPickerWindow.InHouse.cs",
                StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

        Assert.IsFalse(callers.Contains("colorPicker.",
            StringComparison.Ordinal));
        StringAssert.Contains(File.ReadAllText(Path.Combine(forms,
            "ColorPickerWindow.InHouse.cs")), "public Color SelectedColor");
    }

    private static int ElementCount(string xaml, string type) =>
        Regex.Matches(xaml, "<controls:" + type + @"(?=[\s>])").Count;

    private static string ReadShippingSource(string appRoot)
    {
        HashSet<string> extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xaml", ".csproj",
        };
        return string.Join("\n", Directory.EnumerateFiles(appRoot, "*",
                SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Where(path => !path.Split(Path.DirectorySeparatorChar)
                .Any(part => string.Equals(part, "bin",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "obj",
                        StringComparison.OrdinalIgnoreCase)))
            .Select(File.ReadAllText));
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

        throw new InvalidOperationException(
            "Could not locate the repository root above " +
            AppContext.BaseDirectory + ".");
    }
}

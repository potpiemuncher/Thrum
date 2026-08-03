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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Guards a class of failure that compiles clean, passes every unit test, and
/// crashes at runtime: a {StaticResource} reference inside a DataTemplate
/// resolves only when the template is instantiated, so a missing declaration
/// is invisible until a user reaches that screen.
///
/// <para>This is not hypothetical. The first-run wizard shipped with three
/// such references to BooleanToVisibilityConverter and no declaration; the
/// first live run (VM pass 2026-08-02) crashed the wizard the moment the user
/// advanced past Welcome — before any configuration existed, on the app's very
/// first impression.</para>
///
/// <para>The scan is deliberately scoped to converters the views instantiate
/// locally (this product declares BooleanToVisibilityConverter per view, not
/// app-wide). Shell style and brush keys resolve through the theme
/// dictionaries merged at the application level and are covered by
/// ThemeResourceTests instead.</para>
/// </summary>
[TestClass]
public class XamlStaticResourceTests
{
    /// <summary>
    /// Converter keys that views must declare in the same file they reference
    /// them from, because no application-level dictionary provides them.
    /// </summary>
    private static readonly string[] LocallyDeclaredConverterKeys =
    {
        "BooleanToVisibilityConverter",
        "InverseBoolConverter",
    };

    [TestMethod]
    public void EveryLocallyDeclaredConverterReferenceHasADeclarationInItsFile()
    {
        string formsDirectory = Path.Combine(FindRepositoryRoot(),
            "DS4Windows", "DS4Forms");
        List<string> offenders = new List<string>();
        int referencingFiles = 0;

        foreach (string file in Directory.EnumerateFiles(formsDirectory,
            "*.xaml", SearchOption.AllDirectories))
        {
            string xaml = File.ReadAllText(file);
            foreach (string key in LocallyDeclaredConverterKeys)
            {
                bool referenced = Regex.IsMatch(xaml,
                    @"\{StaticResource\s+" + Regex.Escape(key) + @"\}");
                if (!referenced)
                {
                    continue;
                }

                referencingFiles++;
                bool declared = xaml.Contains(
                    "x:Key=\"" + key + "\"", StringComparison.Ordinal);
                if (!declared)
                {
                    offenders.Add(Path.GetFileName(file) + " references " +
                        key + " but never declares it");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A StaticResource reference without a same-file declaration " +
            "compiles clean and crashes when its template instantiates:\n" +
            string.Join("\n", offenders));

        // Negative control for the scan itself: the references genuinely
        // exist in the tree, so an empty offender list means "checked and
        // clean", not "found nothing to check".
        Assert.IsTrue(referencingFiles >= 5,
            "The scan found only " + referencingFiles + " referencing " +
            "file(s); it can no longer be trusted to detect an omission.");
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

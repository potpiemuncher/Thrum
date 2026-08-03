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
using System.Reflection;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Guards the second XAML failure mode the hardware pass found: a binding that
/// is TwoWay by default, pointed at a get-only property.
///
/// <para><c>RangeBase.Value</c> — which <c>ProgressBar</c> and <c>Slider</c>
/// both inherit — is registered <c>BindsTwoWayByDefault</c>. Writing
/// <c>Value="{Binding Something}"</c> therefore requests a TwoWay binding
/// silently. If the source property has no setter, WPF throws
/// <c>InvalidOperationException</c> — but only when the template containing it
/// is instantiated. It compiles, it passes every unit test, and it kills the
/// window the first time a user opens it.</para>
///
/// <para>That is exactly what happened: the input tester bound four
/// ProgressBars to read-only <c>RawValue</c>/<c>MappedValue</c> properties and
/// crashed the whole application on the first real click (hardware pass,
/// 2026-08-02). The sibling failure mode — a StaticResource that only resolves
/// at template instantiation — is guarded by
/// <see cref="XamlStaticResourceTests"/>.</para>
/// </summary>
[TestClass]
public class XamlBindingModeTests
{
    /// <summary>
    /// Controls whose Value property is BindsTwoWayByDefault.
    /// </summary>
    private static readonly string[] TwoWayByDefaultControls =
    {
        "ProgressBar", "Slider",
    };

    [TestMethod]
    public void RangeBaseValueBindingsAreExplicitlyOneWayOrTargetAWritableProperty()
    {
        string formsDirectory = Path.Combine(FindRepositoryRoot(),
            "DS4Windows", "DS4Forms");
        Assembly app = typeof(DS4Windows.Global).Assembly;

        List<string> offenders = new List<string>();
        int inspected = 0;

        foreach (string file in Directory.EnumerateFiles(formsDirectory,
            "*.xaml", SearchOption.AllDirectories))
        {
            foreach (string line in File.ReadAllLines(file))
            {
                if (!TwoWayByDefaultControls.Any(c =>
                    line.Contains("<" + c, StringComparison.Ordinal)))
                {
                    continue;
                }

                Match binding = Regex.Match(line,
                    @"Value\s*=\s*""\{Binding\s+(?<path>[^,}]+?)\s*(?<rest>[,}])");
                if (!binding.Success)
                {
                    continue;
                }

                inspected++;
                string path = binding.Groups["path"].Value.Trim();

                // An explicit one-way mode makes the writability question moot.
                if (Regex.IsMatch(line, @"Mode\s*=\s*(OneWay|OneTime)"))
                {
                    continue;
                }

                // Only a simple property name can be resolved here; anything
                // with a dotted path or an attached-property source is left to
                // the reviewer rather than guessed at.
                if (path.Contains('.') || path.StartsWith("(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsKnownReadOnlyViewModelProperty(app, path))
                {
                    offenders.Add(Path.GetFileName(file) + ": <" +
                        TwoWayByDefaultControls.First(c =>
                            line.Contains("<" + c, StringComparison.Ordinal)) +
                        " Value=\"{Binding " + path + "}\"> is TwoWay by " +
                        "default but '" + path + "' has no setter");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "RangeBase.Value is BindsTwoWayByDefault; binding it to a get-only " +
            "property throws when the template instantiates, not at compile " +
            "time. Add Mode=OneWay:\n" + string.Join("\n", offenders));

        // Negative control for the scan: if it stops finding bindings at all,
        // an empty offender list means nothing.
        Assert.IsTrue(inspected >= 4,
            "The scan inspected only " + inspected + " RangeBase Value " +
            "binding(s); it can no longer be trusted to catch a regression.");
    }

    /// <summary>
    /// True when every type in the application that declares this property
    /// name declares it without a setter. Conservative on purpose: a name that
    /// is writable anywhere is not reported.
    /// </summary>
    private static bool IsKnownReadOnlyViewModelProperty(Assembly app,
        string propertyName)
    {
        PropertyInfo[] matches = app.GetTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Instance))
            .Where(property => property.Name == propertyName)
            .ToArray();

        return matches.Length > 0 && matches.All(p => !p.CanWrite);
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

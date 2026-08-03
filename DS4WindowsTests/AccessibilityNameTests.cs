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
/// Guards the accessible name of list items that are rendered through
/// <c>DisplayMemberPath</c> or an item template.
///
/// <para>Those are purely visual bindings: UI Automation reports
/// <c>ToString()</c>. A plain data class therefore announces its own type name,
/// which is what the Audio Haptics capture-source list did — twenty entries all
/// reading <c>AudioSourceChoice</c>, indistinguishable to a screen reader and
/// undrivable by a UIA test (found on hardware, issue #57).</para>
/// </summary>
[TestClass]
public class AccessibilityNameTests
{
    /// <summary>
    /// Item types bound into a selector by a display path, with the property
    /// that display path names. Each must surface that text from ToString().
    /// </summary>
    private static readonly (string TypeName, string DisplayProperty)[]
        DisplayBoundItemTypes =
        {
            ("AudioSourceChoice", "DisplayName"),
        };

    [TestMethod]
    public void DisplayBoundListItemsAnnounceTheirDisplayNameNotTheirType()
    {
        Assembly app = typeof(DS4Windows.Global).Assembly;

        foreach ((string typeName, string displayProperty) in
            DisplayBoundItemTypes)
        {
            Type type = app.GetTypes()
                .FirstOrDefault(t => t.Name == typeName);
            Assert.IsNotNull(type,
                "Could not find " + typeName + "; if it was renamed, update " +
                "this guard rather than deleting it.");

            PropertyInfo display = type.GetProperty(displayProperty,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(display,
                typeName + " has no " + displayProperty + " property.");

            MethodInfo toString = type.GetMethod("ToString",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            Assert.AreEqual(type, toString.DeclaringType,
                typeName + " does not override ToString(), so UI Automation " +
                "reports its type name for every item in the list. A screen " +
                "reader user hears the same string repeated and cannot tell " +
                "the entries apart. Override ToString() to return " +
                displayProperty + ".");

            // And it must actually return the display text, not something else.
            object instance = Activator.CreateInstance(type, nonPublic: true);
            display.SetValue(instance, "Speakers (Test Device)");
            Assert.AreEqual("Speakers (Test Device)", instance.ToString(),
                typeName + ".ToString() does not return " + displayProperty +
                ".");
        }
    }

    /// <summary>
    /// The same defect one level up: a <c>ContentControl</c> or
    /// <c>ContentPresenter</c> whose <c>Content</c> is bound to a data object
    /// names itself from that object's <c>ToString()</c>, so the container for
    /// a whole page of content announces a view-model type name.
    ///
    /// <para>The first-run wizard's step host did exactly that - a screen
    /// reader met "FirstRunWelcomeStepViewModel" as the name of the first thing
    /// a new user sees (#62). Unlike #57 the fix belongs on the host, not on
    /// every view-model, so this checks the XAML rather than the types.</para>
    /// </summary>
    [TestMethod]
    public void ContentHostsBoundToDataObjectsCarryAnAccessibleName()
    {
        Regex host = new(
            @"<(?<tag>ContentControl|ContentPresenter)\b(?<attrs>[^>]*?)/?>",
            RegexOptions.Singleline);

        // A binding with an explicit property path - Content="{Binding Foo}".
        Regex boundContent = new(
            @"Content\s*=\s*""(?<binding>\{\s*Binding[^""]*)""");

        List<string> unnamed = new();
        int inspected = 0;

        foreach (string file in Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "DS4Windows", "DS4Forms"),
            "*.xaml", SearchOption.AllDirectories))
        {
            string xaml = File.ReadAllText(file);
            foreach (Match match in host.Matches(xaml))
            {
                string attrs = match.Groups["attrs"].Value;
                Match content = boundContent.Match(attrs);
                if (!content.Success)
                {
                    continue;
                }

                string binding = content.Groups["binding"].Value;

                // Template plumbing, not a defect: with {TemplateBinding},
                // {RelativeSource TemplatedParent} or a path-less {Binding},
                // the content comes from whatever uses the template or from
                // the item being presented. The name belongs to that consumer
                // or to the item peer (which #57 covers), not here.
                if (binding.Contains("RelativeSource") ||
                    !Regex.IsMatch(binding, @"\{\s*Binding\s+(Path\s*=\s*)?[A-Za-z_]"))
                {
                    continue;
                }

                inspected++;
                if (!attrs.Contains("AutomationProperties.Name") &&
                    !attrs.Contains("AutomationProperties.LabeledBy"))
                {
                    unnamed.Add(Path.GetFileName(file) + ": " +
                        match.Value.Trim());
                }
            }
        }

        Assert.IsTrue(inspected >= 1,
            "Found no ContentControl/ContentPresenter with a bound Content, " +
            "so this guard inspected nothing. Either the pattern moved or the " +
            "regex stopped matching - fix it rather than letting it pass on " +
            "an empty set.");

        Assert.AreEqual(0, unnamed.Count,
            "These content hosts are bound to a data object and have no " +
            "AutomationProperties.Name or LabeledBy, so UI Automation names " +
            "them from the bound object's ToString() - typically its type " +
            "name:\n  " + string.Join("\n  ", unnamed));
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

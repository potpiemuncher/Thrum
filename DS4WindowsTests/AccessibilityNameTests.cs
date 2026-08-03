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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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

            // Both found by sweeping a live UIA dump of all nine pages for
            // the app's own namespace while fixing #62 - the same defect #57
            // described, still present in two more lists.
            ("OverviewOutputControllerChoice", "Name"),
            ("FullPullModeChoice", "Name"),
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

            // And it must actually return the display text, not something
            // else. Built without running a constructor and written through
            // the backing field where needed, so this works for types with
            // required constructor arguments and get-only properties too.
            object instance = RuntimeHelpers.GetUninitializedObject(type);
            const string probe = "Speakers (Test Device)";
            if (display.CanWrite)
            {
                display.SetValue(instance, probe);
            }
            else
            {
                FieldInfo backing = type.GetField(
                    "<" + displayProperty + ">k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(backing,
                    typeName + "." + displayProperty + " is read-only and " +
                    "has no auto-property backing field, so this guard " +
                    "cannot set it - adjust the guard.");
                backing.SetValue(instance, probe);
            }

            Assert.AreEqual(probe, instance.ToString(),
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
    ///
    /// <para>It checks the bound host <em>and every content-host ancestor</em>,
    /// because naming only the bound control did not fix #62. The element UIA
    /// actually surfaced was the enclosing <c>ScrollViewer</c>, which derives
    /// from <c>ContentControl</c> and so resolved a name through its content
    /// the same way; the measured tree still read
    /// <c>Pane 'FirstRunWelcomeStepViewModel'</c>. A guard that stopped at the
    /// bound control would have passed on the unfixed app.</para>
    /// </summary>
    [TestMethod]
    public void ContentHostsBoundToDataObjectsCarryAnAccessibleName()
    {
        // ContentControl-derived types: each gets a peer that resolves its
        // name through its content, so each one in the chain can leak.
        HashSet<string> contentHosts = new(StringComparer.Ordinal)
        {
            "ContentControl", "ContentPresenter", "ScrollViewer",
            "GroupBox", "HeaderedContentControl",
        };

        List<string> unnamed = new();
        int inspected = 0;

        foreach (string file in Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "DS4Windows", "DS4Forms"),
            "*.xaml", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(file, LoadOptions.SetLineInfo);

            foreach (XElement element in document.Descendants())
            {
                if (element.Name.LocalName is not ("ContentControl" or
                    "ContentPresenter"))
                {
                    continue;
                }

                string binding = (string)element.Attribute("Content");

                // Template plumbing, not a defect: with {TemplateBinding},
                // {RelativeSource TemplatedParent} or a path-less {Binding},
                // the content comes from whatever uses the template or from
                // the item being presented. The name belongs to that consumer
                // or to the item peer (which #57 covers), not here.
                if (binding == null ||
                    binding.Contains("RelativeSource") ||
                    !Regex.IsMatch(binding,
                        @"^\{\s*Binding\s+(Path\s*=\s*)?[A-Za-z_]"))
                {
                    continue;
                }

                inspected++;

                // The bound host itself, then every content host outwards -
                // with no early exit once one is named. Naming the inner
                // control does not stop the wrapper leaking: with #62's
                // ContentControl named and its ScrollViewer not, UIA still
                // reported Pane 'FirstRunWelcomeStepViewModel'. Each peer
                // resolves its own name through the content independently, so
                // each one on the chain has to carry a name.
                for (XElement current = element; current != null;
                    current = current.Parent)
                {
                    if (!contentHosts.Contains(current.Name.LocalName) ||
                        current.Attribute("AutomationProperties.Name") !=
                            null ||
                        current.Attribute("AutomationProperties.LabeledBy") !=
                            null)
                    {
                        continue;
                    }

                    unnamed.Add(Path.GetFileName(file) + " line " +
                        ((IXmlLineInfo)current).LineNumber + ": <" +
                        current.Name.LocalName + "> hosting " + binding);
                }
            }
        }

        Assert.IsTrue(inspected >= 1,
            "Found no ContentControl/ContentPresenter with a bound Content, " +
            "so this guard inspected nothing. Either the pattern moved or the " +
            "match stopped working - fix it rather than letting it pass on " +
            "an empty set.");

        Assert.AreEqual(0, unnamed.Count,
            "These content hosts sit on the path to a Content bound to a data " +
            "object and have no AutomationProperties.Name or LabeledBy, so UI " +
            "Automation names them from that object's ToString() - typically " +
            "its type name:\n  " + string.Join("\n  ", unnamed));
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

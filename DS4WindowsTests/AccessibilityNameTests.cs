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
using System.Linq;
using System.Reflection;

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
}

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

using DS4Windows;
using DS4WinWPF.DS4Control.DTOXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DS4WindowsTests;

/// <summary>
/// Guards the wiring of the Native PS5 mode surfaces: the XAML contract
/// (which bindings and handlers exist, and that consent boxes stay OneWay
/// and unchecked), the theme brushes both dictionaries must define, and
/// the per-profile previous-output-type field (N8).
/// </summary>
[TestClass]
public class NativePs5ContractTests
{
    private static readonly string[] NewThemeBrushes =
    {
        "AccentTextColor", "SuccessBackgroundColor", "WarningBackgroundColor",
        "DangerBackgroundColor", "ExperimentalColor", "FocusRingColor",
        "ScrimColor",
    };

    private static readonly string[] NativePs5Views =
    {
        "ControllerOverviewControl.xaml",
        "NativePs5SetupSheet.xaml",
        "NativePs5TurnOffDialog.xaml",
    };

    [TestMethod]
    public void OverviewHostsTheCardAndHidesTheComboForADualSense()
    {
        string overview = File.ReadAllText(Forms("ControllerOverviewControl.xaml"));

        StringAssert.Contains(overview, "x:Name=\"NativePs5Card\"");
        StringAssert.Contains(overview,
            "Visibility=\"{Binding ShowNativePs5Mode, Converter={StaticResource BooleanToVisibilityConverter}}\"");
        StringAssert.Contains(overview, "DataContext=\"{Binding NativePs5}\"");
        StringAssert.Contains(overview, "Click=\"NativePs5SwitchButton_Click\"");
        StringAssert.Contains(overview, "Click=\"NativePs5SetupButton_Click\"");
        StringAssert.Contains(overview, "Click=\"HidHideClientLink_Click\"");
        StringAssert.Contains(overview,
            "AutomationProperties.Name=\"{Binding SwitchAccessibleName}\"");
        StringAssert.Contains(overview,
            "Visibility=\"{Binding ShowEmulatedDeviceChoice, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            "The Emulated device combo yields its slot to the card for a DualSense.");
    }

    [TestMethod]
    public void SheetConsentBoxesBindOneWayAndAreNeverPreChecked()
    {
        string sheet = File.ReadAllText(Forms("NativePs5SetupSheet.xaml"));

        StringAssert.Contains(sheet,
            "IsChecked=\"{Binding Acknowledged, Mode=OneWay}\"");
        StringAssert.Contains(sheet,
            "IsChecked=\"{Binding AudioEndpointsAllowed, Mode=OneWay}\"");
        Assert.IsFalse(Regex.IsMatch(sheet, @"IsChecked\s*=\s*""True""",
            RegexOptions.IgnoreCase),
            "No consent box may start ticked.");

        // Both writers are the Checked/Unchecked pair, as on Settings.
        StringAssert.Contains(sheet, "Checked=\"AcknowledgeCheckBox_Changed\"");
        StringAssert.Contains(sheet, "Unchecked=\"AcknowledgeCheckBox_Changed\"");
        StringAssert.Contains(sheet, "Checked=\"AudioConsentCheckBox_Changed\"");
        StringAssert.Contains(sheet, "Unchecked=\"AudioConsentCheckBox_Changed\"");

        // The disclosure is bound, not retyped into the view.
        StringAssert.Contains(sheet, "{Binding AcknowledgementBody}");
        StringAssert.Contains(sheet, "{Binding AudioClassSummary}");
    }

    [TestMethod]
    public void MainWindowHostsTheSheetBehindAScrim()
    {
        string main = File.ReadAllText(Forms("MainWindow.xaml"));

        StringAssert.Contains(main, "x:Name=\"nativePs5Scrim\"");
        StringAssert.Contains(main, "x:Name=\"nativePs5Sheet\"");
        StringAssert.Contains(main, "NativePs5ToggleRequested=\"ControllerOverview_NativePs5ToggleRequested\"");
        StringAssert.Contains(main, "TurnOnRequested=\"NativePs5Sheet_TurnOnRequested\"");

        int scrim = main.IndexOf("x:Name=\"nativePs5Scrim\"", StringComparison.Ordinal);
        int shell = main.IndexOf("x:Name=\"mainShellPanel\"", StringComparison.Ordinal);
        Assert.IsTrue(shell >= 0 && scrim > shell,
            "The scrim and sheet must be declared after the shell so they draw above it.");
    }

    [DataTestMethod]
    [DataRow("DefaultTheme")]
    [DataRow("DarkTheme")]
    public void BothThemesDefineTheSevenNewBrushes(string theme)
    {
        string[] defined = DefinedKeys(theme);
        foreach (string key in NewThemeBrushes)
        {
            CollectionAssert.Contains(defined, key,
                theme + " is missing \"" + key + "\".");
        }
    }

    [DataTestMethod]
    [DataRow("DefaultTheme")]
    [DataRow("DarkTheme")]
    public void EveryDynamicResourceTheNativePs5ViewsUseExistsInTheTheme(string theme)
    {
        string[] defined = DefinedKeys(theme);
        List<string> missing = new();
        int inspected = 0;

        foreach (string view in NativePs5Views)
        {
            string xaml = File.ReadAllText(Forms(view));
            foreach (Match match in Regex.Matches(xaml,
                @"\{DynamicResource\s+([^}\s,]+)"))
            {
                inspected++;
                string key = match.Groups[1].Value;
                if (!defined.Contains(key, StringComparer.Ordinal))
                {
                    missing.Add(view + ": " + key);
                }
            }
        }

        Assert.IsTrue(inspected > 20, "The scan found almost nothing; fix the scan.");
        Assert.AreEqual(0, missing.Count, theme + " lacks: " +
            string.Join(", ", missing.Distinct()));
    }

    [TestMethod]
    public void PreviousOutputTypeRoundTripsAndIsOmittedWhileUnrecorded()
    {
        var store = new BackingStore();
        store.outputDevType[0] = OutContType.ViiperDualSense;
        store.previousOutputDevType[0] = OutContType.ViiperDS4;

        var dto = new ProfileDTO { DeviceIndex = 0 };
        dto.MapFrom(store);
        Assert.AreEqual(OutContType.ViiperDS4, dto.PreviousOutputContDevice);
        Assert.AreEqual("ViiperDS4", dto.PreviousOutputContDeviceString);
        StringAssert.Contains(Serialize(dto), "<PreviousOutputContDevice>ViiperDS4</PreviousOutputContDevice>");

        var destination = new BackingStore();
        var loaded = new ProfileDTO
        {
            DeviceIndex = 0,
            PreviousOutputContDeviceString = dto.PreviousOutputContDeviceString,
        };
        loaded.MapTo(destination);
        Assert.AreEqual(OutContType.ViiperDS4, destination.previousOutputDevType[0]);

        var fresh = new ProfileDTO { DeviceIndex = 0 };
        fresh.MapFrom(new BackingStore());
        Assert.AreEqual(OutContType.None, fresh.PreviousOutputContDevice);
        Assert.IsNull(fresh.PreviousOutputContDeviceString);
        Assert.IsFalse(Serialize(fresh).Contains("PreviousOutputContDevice",
            StringComparison.Ordinal),
            "Profiles that never used the switch must stay byte-identical.");

        var legacy = new ProfileDTO { PreviousOutputContDeviceString = null };
        Assert.AreEqual(OutContType.None, legacy.PreviousOutputContDevice);
    }

    private static string Serialize(ProfileDTO dto)
    {
        var serializer = new XmlSerializer(typeof(ProfileDTO),
            ProfileDTO.GetAttributeOverrides());
        using var writer = new StringWriter();
        serializer.Serialize(writer, dto);
        return writer.ToString();
    }

    private static string[] DefinedKeys(string theme)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument dictionary = XDocument.Load(Path.Combine(FindRepositoryRoot(),
            "DS4Windows", "DS4Forms", "Themes", theme + ".xaml"));
        return dictionary.Descendants()
            .Select(element => (string)element.Attribute(xaml + "Key"))
            .Where(key => !string.IsNullOrEmpty(key))
            .ToArray();
    }

    private static string Forms(string file) => Path.Combine(
        FindRepositoryRoot(), "DS4Windows", "DS4Forms", file);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DS4WindowsWPF.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root above " + AppContext.BaseDirectory);
    }
}

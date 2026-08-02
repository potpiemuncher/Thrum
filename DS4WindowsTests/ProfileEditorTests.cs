using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests
{
    [TestClass]
    public class ProfileEditorSectionStateTests
    {
        [TestMethod]
        public void SectionDiffersFromProfileDefaultStartsExpanded()
        {
            ProfileEditorSectionSnapshot defaults = new(
                "axis-default", "gyro-default", "touch-default");

            ProfileEditorSectionStateViewModel unchanged = new(defaults,
                defaults);
            Assert.IsFalse(unchanged.IsAxisConfigExpanded);
            Assert.IsFalse(unchanged.IsGyroExpanded);
            Assert.IsFalse(unchanged.IsTouchpadExpanded);

            ProfileEditorSectionStateViewModel changed = new(
                new ProfileEditorSectionSnapshot("axis-custom", "gyro-custom",
                    "touch-custom"), defaults);
            Assert.IsTrue(changed.IsAxisConfigExpanded);
            Assert.IsTrue(changed.IsGyroExpanded);
            Assert.IsTrue(changed.IsTouchpadExpanded);
        }

        [TestMethod]
        public void DefaultProviderProducesStableSectionSignatures()
        {
            ProfileEditorSectionSnapshot first =
                ProfileEditorSectionSnapshot.Capture(
                    ProfileEditorDefaultProvider.CreateDefaultStore(),
                    ProfileEditorDefaultProvider.DefaultDeviceIndex);
            ProfileEditorSectionSnapshot second =
                ProfileEditorSectionSnapshot.Capture(
                    ProfileEditorDefaultProvider.CreateDefaultStore(),
                    ProfileEditorDefaultProvider.DefaultDeviceIndex);

            Assert.AreEqual(first.AxisSignature, second.AxisSignature);
            Assert.AreEqual(first.GyroSignature, second.GyroSignature);
            Assert.AreEqual(first.TouchpadSignature,
                second.TouchpadSignature);
        }

        [TestMethod]
        public void RuntimeSnapshotPartitionsDenseRailChanges()
        {
            BackingStore defaults =
                ProfileEditorDefaultProvider.CreateDefaultStore();
            ProfileEditorSectionSnapshot defaultSnapshot =
                ProfileEditorSectionSnapshot.Capture(defaults,
                    ProfileEditorDefaultProvider.DefaultDeviceIndex);

            BackingStore axis = ProfileEditorDefaultProvider.CreateDefaultStore();
            axis.lsModInfo[0].deadZone++;
            ProfileEditorSectionStateViewModel axisState = new(
                ProfileEditorSectionSnapshot.Capture(axis, 0), defaultSnapshot);
            Assert.IsTrue(axisState.IsAxisConfigExpanded);
            Assert.IsFalse(axisState.IsGyroExpanded);
            Assert.IsFalse(axisState.IsTouchpadExpanded);

            BackingStore gyro = ProfileEditorDefaultProvider.CreateDefaultStore();
            gyro.gyroOutMode[0] = GyroOutMode.Mouse;
            ProfileEditorSectionStateViewModel gyroState = new(
                ProfileEditorSectionSnapshot.Capture(gyro, 0), defaultSnapshot);
            Assert.IsFalse(gyroState.IsAxisConfigExpanded);
            Assert.IsTrue(gyroState.IsGyroExpanded);
            Assert.IsFalse(gyroState.IsTouchpadExpanded);

            BackingStore touchpad =
                ProfileEditorDefaultProvider.CreateDefaultStore();
            touchpad.touchSensitivity[0]--;
            ProfileEditorSectionStateViewModel touchpadState = new(
                ProfileEditorSectionSnapshot.Capture(touchpad, 0),
                defaultSnapshot);
            Assert.IsFalse(touchpadState.IsAxisConfigExpanded);
            Assert.IsFalse(touchpadState.IsGyroExpanded);
            Assert.IsTrue(touchpadState.IsTouchpadExpanded);
        }
    }

    [TestClass]
    public class ProfileEditorSearchIndexTests
    {
        [TestMethod]
        public void LabelSearchMapsToSectionAndMissIsEmpty()
        {
            object axisTarget = new();
            ProfileEditorSearchIndex index = new();
            index.Add("Dead Zone:", "Axis Config", axisTarget);
            index.Add("Gyro sensitivity", "Gyro", new object());

            IReadOnlyList<ProfileEditorSearchEntry> match =
                index.Search("dead zone");
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("Dead Zone", match[0].Label);
            Assert.AreEqual("Axis Config", match[0].SectionName);
            Assert.AreSame(axisTarget, match[0].Target);
            Assert.AreEqual(0, index.Search("not a setting").Count);
        }
    }

    [TestClass]
    public class ProfileEditorResetCatalogTests
    {
        private sealed class ResetTarget
        {
            public double LSDeadZone { get; set; }
            public int GyroSensitivity { get; set; }
            public int TouchSens { get; set; }
        }

        [TestMethod]
        public void ResetWritesValuesFromDefaultInitializationSource()
        {
            BackingStore defaults =
                ProfileEditorDefaultProvider.CreateDefaultStore();
            ResetTarget target = new()
            {
                LSDeadZone = -1.0,
                GyroSensitivity = -1,
                TouchSens = -1,
            };

            ProfileEditorResetCatalog.Get(nameof(target.LSDeadZone)).Reset(
                target, defaults,
                ProfileEditorDefaultProvider.DefaultDeviceIndex);
            ProfileEditorResetCatalog.Get(nameof(target.GyroSensitivity)).Reset(
                target, defaults,
                ProfileEditorDefaultProvider.DefaultDeviceIndex);
            ProfileEditorResetCatalog.Get(nameof(target.TouchSens)).Reset(
                target, defaults,
                ProfileEditorDefaultProvider.DefaultDeviceIndex);

            Assert.AreEqual(Math.Round(defaults.lsModInfo[0].deadZone / 127d,
                2), target.LSDeadZone);
            Assert.AreEqual(defaults.gyroSensitivity[0],
                target.GyroSensitivity);
            Assert.AreEqual(defaults.touchSensitivity[0], target.TouchSens);
        }

        [TestMethod]
        public void EveryResetEntryMatchesAWritableSettingsProperty()
        {
            BackingStore defaults =
                ProfileEditorDefaultProvider.CreateDefaultStore();

            foreach (string setting in ProfileEditorResetCatalog.Settings)
            {
                System.Reflection.PropertyInfo property =
                    typeof(ProfileSettingsViewModel).GetProperty(setting);
                Assert.IsNotNull(property,
                    $"Missing ProfileSettingsViewModel property: {setting}");
                Assert.IsTrue(property.CanWrite,
                    $"Reset property is not writable: {setting}");

                object value = ProfileEditorResetCatalog.Get(setting)
                    .GetDefaultValue(defaults,
                        ProfileEditorDefaultProvider.DefaultDeviceIndex);
                if (value != null &&
                    !property.PropertyType.IsInstanceOfType(value))
                {
                    Convert.ChangeType(value, property.PropertyType,
                        System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
    }

    [TestClass]
    public class ProfileEditorContractTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        [TestMethod]
        public void DenseRailsUseBoundCardExpandersWithVisibleHelp()
        {
            XDocument document = XDocument.Load(SourcePath("DS4Windows",
                "DS4Forms", "ProfileEditor.xaml"));
            Dictionary<string, string> expectedBindings = new()
            {
                ["axisConfigSectionExpander"] = "IsAxisConfigExpanded",
                ["gyroSectionExpander"] = "IsGyroExpanded",
                ["touchpadSectionExpander"] = "IsTouchpadExpanded",
            };

            foreach (KeyValuePair<string, string> expected in expectedBindings)
            {
                XElement expander = document.Descendants(
                        Presentation + "Expander")
                    .Single(element =>
                        (string)element.Attribute(Xaml + "Name") ==
                        expected.Key);
                StringAssert.Contains((string)expander.Attribute("IsExpanded"),
                    expected.Value);
                Assert.IsTrue(expander.Ancestors(Presentation + "Border")
                    .Any(border => ((string)border.Attribute("Style"))
                        ?.Contains("BridgeCardStyle",
                            StringComparison.Ordinal) == true));
                Assert.IsTrue(expander.Descendants(Presentation + "TextBlock")
                    .Any(text => ((string)text.Attribute("Style"))
                        ?.Contains("BridgeSectionDescriptionStyle",
                            StringComparison.Ordinal) == true));
            }
        }

        [TestMethod]
        public void SearchUsesRuntimeLabelsAndThemeResourceHighlighting()
        {
            XDocument document = XDocument.Load(SourcePath("DS4Windows",
                "DS4Forms", "ProfileEditor.xaml"));
            XElement searchBox = document.Descendants(
                    Presentation + "TextBox")
                .Single(element =>
                    (string)element.Attribute(Xaml + "Name") ==
                    "profileSettingsSearchBox");
            Assert.AreEqual("ProfileSettingsSearchBox_TextChanged",
                (string)searchBox.Attribute("TextChanged"));

            string searchController = File.ReadAllText(SourcePath(
                "DS4Windows", "DS4Forms",
                "ProfileEditorSearchController.cs"));
            StringAssert.Contains(searchController,
                "LogicalTreeHelper.GetChildren(current)");
            StringAssert.Contains(searchController, "TryGetSearchLabel");

            string highlighter = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ProfileEditorSearchHighlighter.cs"));
            StringAssert.Contains(highlighter,
                "SetResourceReference(Border.BorderBrushProperty");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
                highlighter,
                @"(?:Background|Foreground|BorderBrush)\s*=\s*""#[0-9A-Fa-f]"),
                "Search highlighting must not hardcode colours.");
        }

        [TestMethod]
        public void DenseRailNumericSettingsAllHaveResetEntries()
        {
            XDocument document = XDocument.Load(SourcePath("DS4Windows",
                "DS4Forms", "ProfileEditor.xaml"));
            string[] sectionNames =
            {
                "axisConfigSectionExpander",
                "gyroSectionExpander",
                "touchpadSectionExpander",
            };
            HashSet<string> boundSettings = new(StringComparer.Ordinal);

            foreach (string sectionName in sectionNames)
            {
                XElement section = document.Descendants()
                    .Single(element => element.Name.LocalName == "Expander" &&
                        (string)element.Attribute(Xaml + "Name") ==
                        sectionName);
                foreach (XElement input in section.Descendants().Where(
                    element => element.Name.LocalName == "Slider" ||
                        element.Name.LocalName.EndsWith("UpDown",
                            StringComparison.Ordinal)))
                {
                    string value = (string)input.Attribute("Value");
                    System.Text.RegularExpressions.Match match =
                        System.Text.RegularExpressions.Regex.Match(
                            value ?? string.Empty,
                            @"^\{Binding\s+([A-Za-z0-9]+)");
                    if (match.Success)
                    {
                        boundSettings.Add(match.Groups[1].Value);
                    }
                }
            }

            HashSet<string> resetSettings = new(
                ProfileEditorResetCatalog.Settings, StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(boundSettings.ToArray(),
                resetSettings.ToArray());
            Assert.AreEqual(108, boundSettings.Count,
                "Update this audited count when dense-rail numeric inputs " +
                "change.");

            string controller = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ProfileEditorResetController.cs"));
            StringAssert.Contains(controller,
                "LogicalTreeHelper.GetChildren(current)");
            StringAssert.Contains(controller, "SetResourceReference");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
                controller,
                @"(?:Background|Foreground|BorderBrush)\s*=\s*""#[0-9A-Fa-f]"""),
                "Reset affordances must not hardcode colours.");
        }

        private static string SourcePath(params string[] parts) =>
            Path.Combine(new[] { FindRepositoryRoot() }.Concat(parts)
                .ToArray());

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
                "Could not locate the repository root.");
        }
    }
}

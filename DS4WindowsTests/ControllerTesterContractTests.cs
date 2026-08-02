using System.Text.RegularExpressions;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerTesterContractTests
    {
        [TestMethod]
        public void TesterUsesOneVisibilityBoundedSixtyHertzTimerAndOneMarshal()
        {
            string source = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerTesterControl.xaml.cs"));
            string xaml = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerTesterControl.xaml"));

            Assert.AreEqual(1,
                Regex.Matches(source, @"new NonFormTimer").Count);
            StringAssert.Contains(source, "1000.0 / 60.0");
            Assert.AreEqual(1,
                Regex.Matches(source, @"Dispatcher\.Invoke\(").Count);
            StringAssert.Contains(xaml,
                "IsVisibleChanged=\"ControllerTesterControl_IsVisibleChanged\"");
            StringAssert.Contains(xaml,
                "Unloaded=\"ControllerTesterControl_Unloaded\"");
            StringAssert.Contains(source, "StopTimer();");
            string window = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerTesterWindow.xaml"));
            StringAssert.Contains(window,
                "StateChanged=\"ControllerTesterWindow_StateChanged\"");
        }

        [TestMethod]
        public void TesterUsesDynamicBrushesAndCapabilityGatesOutputActions()
        {
            string xaml = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerTesterControl.xaml"));

            Assert.IsFalse(Regex.IsMatch(xaml,
                "(?:Background|Foreground|Fill|Stroke|BorderBrush)=\\\"" +
                "(?:#[0-9A-Fa-f]+|Black|White|Red|Yellow|Transparent)\\\""),
                "Tester brushes must resolve through DynamicResource keys.");
            StringAssert.Contains(xaml,
                "Visibility=\"{Binding SupportsRumble");
            StringAssert.Contains(xaml,
                "Visibility=\"{Binding SupportsLightbar");
            StringAssert.Contains(xaml,
                "IsEnabled=\"{Binding CanTestRumble}\"");
            StringAssert.Contains(xaml,
                "IsEnabled=\"{Binding CanTestLightbar}\"");
            StringAssert.Contains(xaml, "Content=\"_Test rumble\"");
            StringAssert.Contains(xaml, "Content=\"_Flash lightbar\"");
        }

        [TestMethod]
        public void TesterReusesIdentifyPathAndHasBothCardEntryPoints()
        {
            string tester = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerTesterControl.xaml.cs"));
            string overview = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerOverviewControl.xaml"));
            string mainWindow = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "MainWindow.xaml"));

            StringAssert.Contains(tester,
                "await controller.IdentifyLightbarAsync()");
            StringAssert.Contains(overview, "Content=\"_Test inputs\"");
            StringAssert.Contains(mainWindow, "Content=\"Test inputs\"");
            Assert.IsFalse(tester.Contains("InputDeviceType",
                StringComparison.Ordinal),
                "Device-type visibility belongs only in the capability policy.");
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
                "Could not locate the repository root above " +
                AppContext.BaseDirectory + ".");
        }
    }
}

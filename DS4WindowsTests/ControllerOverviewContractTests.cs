using System.Xml.Linq;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerOverviewContractTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        [TestMethod]
        public void IdentifyControllerActionIsCapabilityGated()
        {
            XDocument overview = XDocument.Load(SourcePath("DS4Windows",
                "DS4Forms", "ControllerOverviewControl.xaml"));
            XElement button = overview
                .Descendants(Presentation + "Button")
                .SingleOrDefault(element =>
                    (string)element.Attribute(Xaml + "Name") ==
                    "IdentifyControllerButton");

            Assert.IsNotNull(button, "Overview is missing the identify action.");
            string visibility = (string)button.Attribute("Visibility") ??
                string.Empty;
            StringAssert.Contains(visibility,
                "SelectedController.SupportsLightbar",
                "Identify must stay hidden when the capability policy says " +
                "the physical controller has no writable lightbar.");
        }

        [TestMethod]
        public void OverviewSurfacesAccessChargingAndProfileSwitching()
        {
            string overview = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ControllerOverviewControl.xaml"));

            StringAssert.Contains(overview,
                "{Binding SelectedControllerAccessStatus");
            StringAssert.Contains(overview,
                "{Binding SelectedControllerChargingState}");
            StringAssert.Contains(overview,
                "ItemsSource=\"{Binding SelectedController.ProfileListCol}\"");
            StringAssert.Contains(overview,
                "SelectedIndex=\"{Binding SelectedController.SelectedIndex, Mode=TwoWay}\"");
        }

        [TestMethod]
        public void OverviewHapticStorageUsesCapabilityInsteadOfDeviceType()
        {
            string viewModel = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "ViewModels", "MainWindowsViewModel.cs"));

            StringAssert.Contains(viewModel,
                "selectedController.UsesDualSenseHapticPowerLevels");
            Assert.IsFalse(viewModel.Contains(
                "DeviceType == InputDeviceType.DualSense",
                StringComparison.Ordinal),
                "Overview must not reintroduce a device-type branch outside " +
                "ControllerUiCapabilities.");
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

using DS4Windows;
using DS4WinWPF.DS4Forms.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace DS4WindowsTests
{
    [TestClass]
    public class ThemeResourceTests
    {
        /// <summary>
        /// Relative pack URI authority for the app assembly. Composed from
        /// <see cref="ProductInfo.ExeBaseName"/> rather than spelled out, so an
        /// assembly rename cannot leave this test pointed at a name that no
        /// longer exists.
        /// </summary>
        private static readonly string ComponentPrefix =
            $"/{ProductInfo.ExeBaseName};component";

        /// <summary>
        /// Brushes the shell styles resolve with DynamicResource. Every one of
        /// them has to exist in both dictionaries or a theme switch leaves an
        /// unresolved reference at runtime, which WPF reports as nothing at all.
        /// </summary>
        private static readonly string[] ThemeBrushKeys =
            ReadShellDynamicResourceKeys();

        [DataTestMethod]
        [DataRow("DefaultTheme")]
        [DataRow("DarkTheme")]
        public void ThemeDefinesEveryBrushTheShellStylesBindTo(string theme)
        {
            CollectionAssert.Contains(ThemeBrushKeys,
                "ControllerSelectionBackgroundColor",
                "The selected controller card brush must be discovered from BridgeShellStyles.xaml, not maintained in a second manual list.");

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";
            XDocument dictionary = XDocument.Load(Path.Combine(
                FindRepositoryRoot(), "DS4Windows", "DS4Forms", "Themes",
                theme + ".xaml"));
            string[] definedKeys = dictionary.Descendants()
                .Select(element => (string)element.Attribute(xaml + "Key"))
                .Where(key => !string.IsNullOrEmpty(key))
                .ToArray();

            foreach (string key in ThemeBrushKeys)
            {
                CollectionAssert.Contains(definedKeys, key,
                    theme + " is missing the brush \"" + key +
                    "\". Light and dark must define the same keys.");
            }
        }

        private static string[] ReadShellDynamicResourceKeys()
        {
            string themeDirectory = Path.Combine(FindRepositoryRoot(),
                "DS4Windows", "DS4Forms", "Themes");
            string styles = string.Join("\n", new[]
            {
                "BridgeShellStyles.xaml",
                "InHouseControls.xaml",
            }.Select(file => File.ReadAllText(Path.Combine(themeDirectory,
                file))));
            return Regex.Matches(styles,
                    @"\{DynamicResource\s+([^}\s,]+)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
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

        private static void RunOnStaThread(Action body)
        {
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    body();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),
                "Theme resource loading did not finish.");
            if (failure != null)
            {
                Assert.Fail(failure.ToString());
            }
        }

        [TestMethod]
        public void DefaultThemeLoadsBridgeShellStylesOnFreshConfiguration()
        {
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    var application = new Application();
                    var defaultTheme = new ResourceDictionary();
                    application.Resources.MergedDictionaries.Add(defaultTheme);
                    defaultTheme.Source = new Uri(
                        ComponentPrefix + "/DS4Forms/Themes/DefaultTheme.xaml",
                        UriKind.Relative);

                    var bridgeStyles = new ResourceDictionary();
                    application.Resources.MergedDictionaries.Add(bridgeStyles);
                    bridgeStyles.Source = new Uri(
                        ComponentPrefix +
                        "/DS4Forms/Themes/BridgeShellStyles.xaml",
                        UriKind.Relative);

                    Assert.IsNotNull(application.TryFindResource(
                        "BridgePrimaryButtonStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeSecondaryButtonStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeProfileComboBoxStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeDescribedCheckBoxStyle"));

                    // Driver-status card (plan task 2.2). Asserted here rather
                    // than in its own test because WPF allows exactly one
                    // Application per AppDomain.
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeStatusBadgeStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeStatusBadgeTextStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeCardListItemStyle"));

                    AssertControlTemplate<IntegerUpDown>(application,
                        "PART_TextBox", "PART_IncreaseButton",
                        "PART_DecreaseButton");
                    AssertControlTemplate<DoubleUpDown>(application,
                        "PART_TextBox");
                    AssertControlTemplate<DecimalUpDown>(application,
                        "PART_TextBox");
                    AssertControlTemplate<SByteUpDown>(application,
                        "PART_TextBox");
                    AssertControlTemplate<UIntegerUpDown>(application,
                        "PART_TextBox");
                    AssertControlTemplate<SplitButton>(application,
                        "PART_MainButton", "PART_ToggleButton",
                        "PART_Popup");

                    defaultTheme.Source = new Uri(
                        ComponentPrefix + "/DS4Forms/Themes/DarkTheme.xaml",
                        UriKind.Relative);
                    Assert.IsNotNull(application.TryFindResource(
                        typeof(IntegerUpDown)));
                    Assert.IsNotNull(application.TryFindResource(
                        typeof(SplitButton)));
                    application.Shutdown();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),
                "Theme resource loading did not finish.");
            if (failure != null)
            {
                Assert.Fail(failure.ToString());
            }
        }

        private static void AssertControlTemplate<T>(Application application,
            params string[] partNames)
            where T : Control, new()
        {
            Style style = application.TryFindResource(typeof(T)) as Style;
            Assert.IsNotNull(style,
                "The shared theme has no implicit style for " +
                typeof(T).Name + ".");

            var control = new T
            {
                Style = style,
            };
            Assert.IsTrue(control.ApplyTemplate(),
                typeof(T).Name + " did not apply its template.");
            foreach (string partName in partNames)
            {
                Assert.IsNotNull(control.Template.FindName(partName, control),
                    typeof(T).Name + " is missing " + partName + ".");
            }

            if (control is NumericUpDownBase)
            {
                Assert.IsFalse(control.IsTabStop,
                    typeof(T).Name +
                    " must not add a duplicate parent tab stop.");
                var editor = (TextBox)control.Template.FindName(
                    NumericUpDownBase.TextBoxPartName, control);
                Assert.IsNotNull(editor);
                Assert.IsTrue(editor.IsTabStop,
                    typeof(T).Name + " must tab directly to its editor.");
            }
        }
    }
}

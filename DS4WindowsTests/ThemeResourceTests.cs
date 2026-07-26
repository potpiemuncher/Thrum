using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Windows;

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
        {
            "ForegroundColor",
            "BackgroundColor",
            "BorderColor",
            "AccentColor",
            "SecondaryColor",
            "SurfaceBackgroundColor",
            "SidebarBackgroundColor",
            "CardBackgroundColor",
            "RaisedBackgroundColor",
            "NavigationSelectionColor",
            "MutedForegroundColor",
            "SuccessColor",
            "WarningColor",
            "DangerColor",
        };

        [DataTestMethod]
        [DataRow("DefaultTheme")]
        [DataRow("DarkTheme")]
        public void ThemeDefinesEveryBrushTheShellStylesBindTo(string theme)
        {
            RunOnStaThread(() =>
            {
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri(
                        ComponentPrefix + "/DS4Forms/Themes/" + theme + ".xaml",
                        UriKind.Relative),
                };

                foreach (string key in ThemeBrushKeys)
                {
                    Assert.IsTrue(dictionary.Contains(key),
                        theme + " is missing the brush \"" + key +
                        "\". Light and dark must define the same keys.");
                }
            });
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
    }
}

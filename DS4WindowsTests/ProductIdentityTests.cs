using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Resources;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the couplings between <see cref="ProductInfo"/> and the things
    /// outside the C# source that have to agree with it: the csproj
    /// <c>AssemblyName</c>, the compiled WPF resources, and the
    /// <see cref="Global"/> aliases that call sites still use.
    ///
    /// These exist so a rebrand that misses one constant fails in CI instead of
    /// at runtime, where a broken pack URI only throws once a user opens the
    /// page that needs it.
    /// </summary>
    [TestClass]
    public class ProductIdentityTests
    {
        /// <summary>
        /// The single most important assertion in this file. Every pack URI in
        /// the tree names the assembly by this string; if the csproj
        /// <c>AssemblyName</c> is changed without changing
        /// <see cref="ProductInfo.ExeBaseName"/> (or the reverse), every
        /// resource lookup breaks at runtime.
        /// </summary>
        [TestMethod]
        public void ExeBaseNameMatchesTheApplicationAssemblyName()
        {
            Assembly appAssembly = typeof(Global).Assembly;

            Assert.AreEqual(ProductInfo.ExeBaseName,
                appAssembly.GetName().Name,
                "ProductInfo.ExeBaseName must equal the app project's " +
                "AssemblyName, or every pack URI built from it dies.");
        }

        [TestMethod]
        public void AssemblyResourcePrefixIsBuiltFromTheAssemblyName()
        {
            Assert.AreEqual(
                $"pack://application:,,,/{ProductInfo.ExeBaseName};",
                ProductInfo.AssemblyResourcePrefix);
        }

        [TestMethod]
        public void ResourcesPrefixIsBuiltFromTheAssemblyName()
        {
            Assert.AreEqual(
                $"/{ProductInfo.ExeBaseName};component/Resources",
                ProductInfo.ResourcesPrefix);
        }

        [TestMethod]
        public void LanguageAssemblyNameIsBuiltFromTheAssemblyName()
        {
            Assert.AreEqual(ProductInfo.ExeBaseName + ".resources.dll",
                ProductInfo.LanguageAssemblyName);
        }

        /// <summary>
        /// The <see cref="Global"/> constants are the historical names that
        /// hundreds of call sites use. They are aliases now; this catches a
        /// future edit that re-hardcodes one of them.
        /// </summary>
        [TestMethod]
        public void GlobalIdentityConstantsDelegateToProductInfo()
        {
            Assert.AreEqual(ProductInfo.AssemblyResourcePrefix,
                Global.ASSEMBLY_RESOURCE_PREFIX);
            Assert.AreEqual(ProductInfo.ResourcesPrefix,
                Global.RESOURCES_PREFIX);
            Assert.AreEqual(ProductInfo.LanguageAssemblyName,
                Global.LANGUAGE_ASSEMBLY_NAME);
            Assert.AreEqual(ProductInfo.InstalledReleaseFileName,
                ReleaseChannelPolicy.InstalledReleaseFileName);
            Assert.AreEqual(ProductInfo.ReleasesApiUri,
                Changelog.GITHUB_RELEASES_API_URI);
            Assert.AreEqual(ProductInfo.LatestReleaseApiUri,
                Changelog.GITHUB_LATEST_RELEASE_API_URI);
        }

        /// <summary>
        /// The IPC object names and the single-instance event are what let a
        /// rebranded build coexist with the product it forked from. They must
        /// not silently collapse to a shared name.
        /// </summary>
        [TestMethod]
        public void IpcObjectNamesAreDistinctAndNamespaced()
        {
            string[] names =
            {
                ProductInfo.IpcClassNameMmfName,
                ProductInfo.IpcResultDataMmfName,
                ProductInfo.IpcResultDataReadyEventName,
                ProductInfo.IpcResultDataSingleTaskMutexName,
            };

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                Assert.IsTrue(name.StartsWith(ProductInfo.ProductName,
                        StringComparison.Ordinal),
                    $"IPC object name '{name}' is not namespaced by the " +
                    "product name, so it can collide with another build.");
                Assert.IsTrue(seen.Add(name),
                    $"IPC object name '{name}' is used twice.");
            }

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(ProductInfo.SingleInstanceEventName));
        }

        /// <summary>
        /// Every tray icon the settings UI can select must actually exist as a
        /// compiled resource. Before this test a wrong pack URI only surfaced
        /// when a user picked that icon.
        /// </summary>
        [TestMethod]
        public void EveryTrayIconChoiceResolvesToACompiledResource()
        {
            var missing = new List<string>();
            foreach (KeyValuePair<TrayIconChoice, string> entry in
                Global.iconChoiceResources)
            {
                if (ResolveResource(entry.Value) == null)
                {
                    missing.Add($"{entry.Key} -> {entry.Value}");
                }
            }

            Assert.AreEqual(0, missing.Count,
                "Tray icon resources did not resolve: " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// The battery tray icons are addressed by the same prefix but are
        /// built ad hoc in the tray view model, so they get their own check.
        /// </summary>
        [TestMethod]
        public void EveryBatteryTrayIconResolvesToACompiledResource()
        {
            var missing = new List<string>();
            for (int battery = 0; battery <= 100; battery += 10)
            {
                string uri = $"{ProductInfo.ResourcesPrefix}/{battery}.ico";
                if (ResolveResource(uri) == null)
                {
                    missing.Add(uri);
                }
            }

            Assert.AreEqual(0, missing.Count,
                "Battery tray icons did not resolve: " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// The four controller artwork images shown on the profile editor's
        /// output-controller picker.
        /// </summary>
        [TestMethod]
        public void ControllerArtworkResolvesToCompiledResources()
        {
            string[] artwork =
            {
                "DualShock 4 Controller.png",
                "DualSense Controller.png",
                "DualSense Edge Controller.png",
                "Switch 2 Pro Controller.png",
            };

            var missing = new List<string>();
            foreach (string name in artwork)
            {
                string uri = $"{ProductInfo.ResourcesPrefix}/{name}";
                if (ResolveResource(uri) == null)
                {
                    missing.Add(uri);
                }
            }

            Assert.AreEqual(0, missing.Count,
                "Controller artwork did not resolve: " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// The absolute form of the prefix has to resolve as well, since the
        /// profile editor and binding window build their image URIs with it.
        /// </summary>
        [TestMethod]
        public void AbsoluteAssemblyResourcePrefixResolves()
        {
            string uri = ProductInfo.AssemblyResourcePrefix +
                "component/Resources/DS4-Config_Cross.png";

            Assert.IsNotNull(ResolveResource(uri),
                $"Absolute pack URI did not resolve: {uri}");
        }

        /// <summary>
        /// Resolves a pack URI the way WPF does at runtime, on an STA thread.
        /// Deliberately does not construct an <see cref="Application"/>: WPF
        /// permits only one per process for the lifetime of the process, and
        /// <c>ThemeResourceTests</c> already creates and shuts one down.
        /// </summary>
        private static StreamResourceInfo ResolveResource(string uri)
        {
            StreamResourceInfo info = null;
            Exception failure = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    // Force the app assembly to load so that the
                    // "/DS4Windows;component/..." authority can be resolved.
                    _ = typeof(Global).Assembly;

                    info = Application.GetResourceStream(
                        new Uri(uri,
                            uri.StartsWith("pack://", StringComparison.Ordinal)
                                ? UriKind.Absolute
                                : UriKind.Relative));
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),
                $"Resolving '{uri}' did not finish.");

            if (failure != null)
            {
                Assert.Fail($"Resolving '{uri}' threw: {failure}");
            }

            return info;
        }
    }
}

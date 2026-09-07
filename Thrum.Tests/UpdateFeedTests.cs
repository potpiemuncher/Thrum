using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the update-feed cutover.
    ///
    /// <para>Two separate properties are asserted here, and the second is the
    /// one that matters. The first is ordinary: the release URLs name this
    /// product's repository. The second is a safety property — this build
    /// cannot run the inherited external updater.</para>
    ///
    /// <para>That is not a branding concern. <c>DS4Updater.exe</c> installs
    /// DS4Windows. Left wired up after the rename, a user who clicked "yes" on
    /// an update prompt would have had this product downloaded over,
    /// overwritten by, and replaced with the product it was forked from — with
    /// an elevation prompt in the middle of it. The download, the elevated
    /// copy and the launch were deleted rather than repointed, and these tests
    /// exist so nobody restores any of it by accident.</para>
    /// </summary>
    [TestClass]
    public class UpdateFeedTests
    {
        /// <summary>
        /// Needles for the binary scan. Each is a string that can only be in
        /// the assembly if some form of the external-updater pipeline is back.
        /// Written out here and nowhere else in the product, which is what
        /// makes the scan meaningful.
        /// </summary>
        private static readonly string[] UpdaterArtefacts =
        {
            "DS4Updater.exe",
            "DS4Updater_x86.exe",
            "hbashton/DS4Updater",
            "updatercopy.bat",
        };

        [TestMethod]
        public void TheReleaseFeedNamesThisProductsRepository()
        {
            Assert.AreEqual("potpiemuncher/Thrum", ProductInfo.ReleaseOwnerRepo);
        }

        /// <summary>
        /// Every release URL is derived from one repository constant, so they
        /// cannot disagree about which project's builds to offer.
        /// </summary>
        [TestMethod]
        public void EveryReleaseUrlIsComposedFromTheReleaseRepository()
        {
            Assert.AreEqual(
                "https://github.com/" + ProductInfo.ReleaseOwnerRepo,
                ProductInfo.ProjectUri);
            Assert.AreEqual(ProductInfo.ProjectUri + "/releases",
                ProductInfo.ReleasesPageUri);
            Assert.AreEqual(
                "https://api.github.com/repos/" + ProductInfo.ReleaseOwnerRepo +
                    "/releases",
                ProductInfo.ReleasesApiUri);
            Assert.AreEqual(ProductInfo.ReleasesApiUri + "/latest",
                ProductInfo.LatestReleaseApiUri);
        }

        /// <summary>
        /// The update check reads the release <em>list</em> endpoint, not
        /// <c>/releases/latest</c>. The distinction is load bearing while this
        /// product has published nothing: the list endpoint answers 200 with an
        /// empty array, which the check reads as "up to date", whereas
        /// <c>/releases/latest</c> answers 404 and would have to be
        /// special-cased to avoid looking like a network failure.
        /// </summary>
        [TestMethod]
        public void TheUpdateCheckUsesTheReleaseListEndpoint()
        {
            Assert.AreEqual(ProductInfo.ReleasesApiUri,
                Changelog.GITHUB_RELEASES_API_URI);
            Assert.IsFalse(
                Changelog.GITHUB_RELEASES_API_URI.EndsWith("/latest",
                    StringComparison.Ordinal),
                "The update check must not depend on /releases/latest, which " +
                "404s until the first release is published.");
        }

        /// <summary>
        /// The external-updater constants are gone from the identity surface.
        /// Asserted by reflection rather than by not compiling, so that
        /// re-adding one is a test failure with an explanation attached.
        /// </summary>
        [TestMethod]
        public void ProductIdentityCarriesNoExternalUpdaterConstants()
        {
            string[] removed =
            {
                "UpdaterOwnerRepo",
                "UpdaterReleasesPageUri",
                "UpdaterLatestReleaseApiUri",
                "UpdaterExeName",
                "UpdaterExeNameX86",
            };

            var restored = removed
                .Where(name => typeof(ProductInfo).GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static) != null)
                .ToList();

            Assert.AreEqual(0, restored.Count,
                "ProductInfo has regained external-updater constants: " +
                string.Join(", ", restored) +
                ". Running DS4Updater would install DS4Windows over this " +
                "product; the pipeline was removed, not repointed.");
        }

        /// <summary>
        /// The methods that downloaded, elevated-copied and launched the
        /// external updater no longer exist on the types that hosted them.
        /// </summary>
        [TestMethod]
        public void TheUpdaterDownloadAndLaunchMethodsAreGone()
        {
            var missingCheck = new List<string>();

            void AssertAbsent(Type type, string member)
            {
                MemberInfo[] found = type.GetMember(member,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance);
                if (found.Length > 0)
                {
                    missingCheck.Add($"{type.Name}.{member}");
                }
            }

            AssertAbsent(typeof(Util), "ElevatedCopyUpdater");
            AssertAbsent(typeof(MainWindowsViewModel), "RunUpdaterCheck");
            AssertAbsent(typeof(MainWindowsViewModel), "LauchDS4Updater");
            AssertAbsent(typeof(MainWindowsViewModel), "DownloadUpstreamUpdaterVersion");
            AssertAbsent(typeof(MainWindowsViewModel), "updaterExe");

            Assert.AreEqual(0, missingCheck.Count,
                "The external-updater pipeline is reachable again: " +
                string.Join(", ", missingCheck));
        }

        /// <summary>
        /// The exhaustive one. Searches the whole compiled application for the
        /// updater artefacts, so it does not matter which class a restored
        /// literal ends up in, or whether it arrives as code or as a resource
        /// string a dialog could pass to <c>Process.Start</c>.
        /// </summary>
        [TestMethod]
        public void NothingInTheApplicationCanNameTheExternalUpdater()
        {
            string[] haystacks = ApplicationTextImages();

            var offenders = UpdaterArtefacts
                .Where(needle => ContainsText(haystacks, needle))
                .ToList();

            Assert.AreEqual(0, offenders.Count,
                "The application can still name the external updater: " +
                string.Join(", ", offenders) +
                ". DS4Updater installs DS4Windows, so no code path here may " +
                "download, copy or launch it.");

            // Negative control: the scan has to be able to find something that
            // genuinely is in there, or the assertion above proves nothing.
            Assert.IsTrue(ContainsText(haystacks, ProductInfo.ReleasesPageUri),
                "The scan could not find our own releases page URL, so it " +
                "cannot be trusted to find an updater artefact either.");
        }

        /// <summary>
        /// The application assembly decoded as UTF-16 text at both byte
        /// alignments. String literals live in the metadata <c>#US</c> heap as
        /// UTF-16 with a variable-length prefix, so a literal can start at an
        /// odd file offset.
        /// </summary>
        private static string[] ApplicationTextImages()
        {
            string location = typeof(Global).Assembly.Location;
            Assert.IsFalse(string.IsNullOrEmpty(location),
                "The application assembly has no file on disk to scan.");
            Assert.IsTrue(File.Exists(location), location);

            byte[] bytes = File.ReadAllBytes(location);
            Assert.IsTrue(bytes.Length > 0);

            return new[]
            {
                Encoding.Unicode.GetString(bytes, 0, bytes.Length & ~1),
                Encoding.Unicode.GetString(bytes, 1, (bytes.Length - 1) & ~1),
            };
        }

        private static bool ContainsText(string[] haystacks, string needle)
        {
            return haystacks.Any(haystack =>
                haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

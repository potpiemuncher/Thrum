using DS4Windows;
using DS4WinWPF.DS4Control.DTOXml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the version reset against the one thing that could actually hurt
    /// a user: their existing configuration.
    ///
    /// <para>This product's version went <em>backwards</em>, from 4.0.2.1 to
    /// 0.9.0. Every settings file and every profile a DS4Windows user brings
    /// across — including everything the one-time importer copies — carries
    /// <c>app_version="4.0.2.1"</c> or older in its root element. From the
    /// running application's point of view those files were written by a
    /// newer build, which in a great many programs is grounds for a warning, a
    /// refusal, or a "your settings were created by a newer version" dialog
    /// that a user cannot get past.</para>
    ///
    /// <para>The tests below pin the reason that cannot happen here:
    /// <c>app_version</c> has no reader. It is written on save and discarded on
    /// load, and every migration decision keys off the integer
    /// <c>config_version</c> instead.</para>
    /// </summary>
    [TestClass]
    public class VersionCompatibilityTests
    {
        /// <summary>
        /// A minimal but genuine app-settings document. The root element
        /// attributes are the whole point of the fixture, so they are
        /// parameterised rather than baked in.
        /// </summary>
        private static string SettingsXml(string rootAttributes)
        {
            return
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<Profile{rootAttributes}>
  <useExclusiveMode>False</useExclusiveMode>
  <startMinimized>True</startMinimized>
  <formWidth>782</formWidth>
  <formHeight>550</formHeight>
  <Controller1>Default</Controller1>
  <LastChecked>12/05/2023 00:24:15</LastChecked>
  <CheckWhen>24</CheckWhen>
  <Notifications>2</Notifications>
</Profile>";
        }

        private static BackingStore LoadSettings(string rootAttributes)
        {
            var serializer = new XmlSerializer(typeof(AppSettingsDTO));
            using var reader = new StringReader(SettingsXml(rootAttributes));
            var store = new BackingStore();
            var dto = serializer.Deserialize(reader) as AppSettingsDTO;
            Assert.IsNotNull(dto, "Settings document failed to deserialize.");
            dto.MapTo(store);
            return store;
        }

        /// <summary>
        /// The regression test the version reset needed: a header written by
        /// DS4Windows 4.0.2.1 loads into the 0.9.0 application.
        /// </summary>
        [TestMethod]
        public void SettingsWrittenByDS4Windows4021Load()
        {
            BackingStore store = LoadSettings(
                @" app_version=""4.0.2.1"" config_version=""2""");

            Assert.IsTrue(store.startMinimized,
                "Settings content did not survive the load.");
            Assert.AreEqual(24, store.CheckWhen);
            Assert.AreEqual(new DateTime(2023, 12, 5, 0, 24, 15),
                store.lastChecked);
        }

        /// <summary>
        /// The stronger form of the same claim. Three headers — absent, the
        /// real inherited one, and an implausibly future one — must produce
        /// byte-identical state. If any header attribute could influence the
        /// load, this is where it would show.
        /// </summary>
        [TestMethod]
        public void TheHeaderVersionCannotInfluenceWhatIsLoaded()
        {
            string[] headers =
            {
                string.Empty,
                @" app_version=""3.2.21"" config_version=""1""",
                @" app_version=""4.0.2.1"" config_version=""2""",
                @" app_version=""99.9.9.9"" config_version=""99""",
            };

            string baseline = null;
            foreach (string header in headers)
            {
                BackingStore store = LoadSettings(header);
                string state = Describe(store);

                baseline ??= state;
                Assert.AreEqual(baseline, state,
                    $"A settings header of '{header}' changed what was " +
                    "loaded. Nothing may branch on app_version or on the " +
                    "app-settings config_version.");
            }
        }

        /// <summary>
        /// The other half of the proof: <c>app_version</c> is not merely
        /// ignored on load, it is regenerated on save from the running build.
        /// So the first save after the version reset rewrites 4.0.2.1 to ours
        /// rather than preserving whatever was read.
        /// </summary>
        [TestMethod]
        public void SavingRewritesTheHeaderToTheRunningVersion()
        {
            var serializer = new XmlSerializer(typeof(AppSettingsDTO));
            using var reader = new StringReader(SettingsXml(
                @" app_version=""4.0.2.1"" config_version=""2"""));
            var dto = serializer.Deserialize(reader) as AppSettingsDTO;

            Assert.AreEqual(Global.exeversion, dto.AppVersion,
                "app_version must be regenerated from the running build, not " +
                "carried over from the file that was read.");
            Assert.AreEqual(Global.APP_CONFIG_VERSION.ToString(),
                dto.ConfigVersion);
        }

        /// <summary>
        /// <c>APP_CONFIG_VERSION</c> — the one stamped into the app settings
        /// file — is write-only by design: there is no migration engine for
        /// that file, and nothing compares it to anything. Asserted here so
        /// that adding a comparison later is a deliberate act with a test to
        /// update, not an accident.
        /// </summary>
        [TestMethod]
        public void TheAppSettingsConfigVersionIsUnchangedByTheVersionReset()
        {
            Assert.AreEqual(2, Global.APP_CONFIG_VERSION,
                "The product version and the config-format version are " +
                "independent. Renumbering the product must not renumber the " +
                "file format, or every existing settings file changes meaning.");
            Assert.AreEqual(5, Global.CONFIG_VERSION,
                "Same for the profile format version.");
        }

        /// <summary>
        /// Profiles are the one place a version <em>is</em> compared, and the
        /// comparison is one-directional. A profile claiming a config version
        /// above ours is passed through untouched — not migrated, not
        /// rejected, not warned about.
        /// </summary>
        [TestMethod]
        public void AProfileFromANewerConfigVersionIsPassedThroughUntouched()
        {
            string profile = ProfileXml(Global.CONFIG_VERSION + 1);
            var migration = new ProfileMigration(profile);
            try
            {
                Assert.IsFalse(migration.RequiresMigration(),
                    "A profile from a newer config version must not be " +
                    "migrated backwards.");
                Assert.IsFalse(migration.UsedMigration);
                Assert.AreEqual(profile, migration.CurrentMigrationText,
                    "The profile text was altered despite needing no migration.");
            }
            finally
            {
                migration.Close();
            }
        }

        /// <summary>
        /// Positive control for the test above: the migration gate is not
        /// simply switched off.
        /// </summary>
        [TestMethod]
        public void AProfileFromAnOlderConfigVersionStillMigrates()
        {
            var migration = new ProfileMigration(ProfileXml(1));
            try
            {
                Assert.IsTrue(migration.RequiresMigration(),
                    "The migration gate no longer fires for old profiles, so " +
                    "the newer-version test above proves nothing.");
            }
            finally
            {
                migration.Close();
            }
        }

        /// <summary>
        /// A profile carrying the inherited <c>app_version</c> but the current
        /// <c>config_version</c> needs no migration — the app version in the
        /// header is not consulted.
        /// </summary>
        [TestMethod]
        public void AProfileFromDS4Windows4021NeedsNoMigration()
        {
            string profile =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<DS4Windows app_version=""4.0.2.1"" config_version=""{Global.CONFIG_VERSION}"">
  <touchpadJitterCompensation>False</touchpadJitterCompensation>
  <LSDeadZone>10</LSDeadZone>
</DS4Windows>";

            var migration = new ProfileMigration(profile);
            try
            {
                Assert.IsFalse(migration.RequiresMigration());
                Assert.AreEqual(profile, migration.CurrentMigrationText);
            }
            finally
            {
                migration.Close();
            }
        }

        /// <summary>
        /// The release-channel classifier has to recognise the new version
        /// string as a prerelease. It does so by name, and the reset changed
        /// the name from "4.0.2.1 DualSense Beta" to "0.9.0-beta.1" — a
        /// different shape entirely, with the marker in a suffix rather than a
        /// trailing word.
        /// </summary>
        [TestMethod]
        public void TheResetVersionIsClassifiedAsAPrerelease()
        {
            Assert.IsTrue(
                ReleaseChannelPolicy.IsPrereleaseBuild("0.9.0-beta.1"));

            Assert.IsTrue(
                ReleaseChannelPolicy.TryParseReleaseVersion("0.9.0-beta.1",
                    out Version parsed),
                "The numeric core of the version must still be parseable, or " +
                "no update comparison can be made.");
            Assert.AreEqual(new Version(0, 9, 0), parsed);

            // A stable spelling of the same version must NOT be classified as
            // a prerelease, or the check is matching something incidental.
            Assert.IsFalse(ReleaseChannelPolicy.IsPrereleaseBuild("0.9.0"));
        }

        /// <summary>
        /// Ties the classifier assertion above to the version this build
        /// actually carries, which is what the running application feeds it.
        /// </summary>
        [TestMethod]
        public void TheBuiltAssemblyCarriesAPrereleaseInformationalVersion()
        {
            string informational = typeof(Global).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            Assert.IsFalse(string.IsNullOrWhiteSpace(informational),
                "The application assembly has no InformationalVersion.");
            Assert.IsTrue(ReleaseChannelPolicy.IsPrereleaseBuild(informational),
                $"'{informational}' is not classified as a prerelease, so " +
                "this build would follow the stable release channel.");
        }

        /// <summary>
        /// The version reset itself, asserted against the built assembly rather
        /// than against the props file, so it catches a project that stopped
        /// inheriting.
        /// </summary>
        [TestMethod]
        public void TheApplicationAssemblyCarriesTheResetVersion()
        {
            Assembly app = typeof(Global).Assembly;
            Version assemblyVersion = app.GetName().Version;

            Assert.AreNotEqual(new Version(4, 0, 2, 1), assemblyVersion,
                "The assembly still carries the upstream version, so it is " +
                "not inheriting Directory.Build.props.");
            Assert.IsTrue(assemblyVersion < new Version(1, 0, 0, 0),
                $"AssemblyVersion {assemblyVersion} is not a pre-1.0 version.");

            string informational = app
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            Assert.IsTrue(
                informational.StartsWith(
                    $"{assemblyVersion.Major}.{assemblyVersion.Minor}." +
                    $"{assemblyVersion.Build}", StringComparison.Ordinal),
                $"InformationalVersion '{informational}' disagrees with " +
                $"AssemblyVersion {assemblyVersion} about the numeric core.");
        }

        /// <summary>
        /// GPL correspondence and support triage both depend on a binary being
        /// traceable to the upstream tree it derives from. That is carried in
        /// the informational version and nowhere else in the shipped files.
        /// </summary>
        [TestMethod]
        public void TheInformationalVersionRecordsTheUpstreamBaseCommit()
        {
            string informational = typeof(Global).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            Assert.IsTrue(informational.Contains("5d2724a",
                    StringComparison.OrdinalIgnoreCase),
                $"InformationalVersion '{informational}' no longer names the " +
                "upstream base commit. A user handed this binary must be able " +
                "to identify the tree it derives from.");
            Assert.IsTrue(informational.Contains("4.0.2.1",
                    StringComparison.Ordinal),
                $"InformationalVersion '{informational}' no longer names the " +
                "upstream base version.");
        }

        private static string ProfileXml(int configVersion)
        {
            return
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<DS4Windows app_version=""4.0.2.1"" config_version=""{configVersion}"">
  <touchpadJitterCompensation>False</touchpadJitterCompensation>
  <LSDeadZone>10</LSDeadZone>
</DS4Windows>";
        }

        /// <summary>
        /// A comparable summary of the parts of the loaded state a header
        /// could plausibly reach.
        /// </summary>
        private static string Describe(BackingStore store)
        {
            return string.Join("|",
                store.useExclusiveMode,
                store.startMinimized,
                store.formWidth,
                store.formHeight,
                store.CheckWhen,
                store.lastChecked.ToString("O"),
                store.lastVersionChecked,
                store.lastVersionCheckedNum,
                store.notifications);
        }
    }
}

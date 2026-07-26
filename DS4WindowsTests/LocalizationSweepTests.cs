using DS4Windows;
using DS4WinWPF.Translations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards for the product-name sweep over the string resources
    /// (plan task 1.8).
    ///
    /// <para>The sweep changed <c>.resx</c> <em>values</em> only, in two
    /// families and 29 files. Nothing in a build fails when one of those values
    /// is wrong: a stale brand name renders perfectly. These tests are the only
    /// thing that notices.</para>
    ///
    /// <para>Every lookup pins <see cref="Strings.Culture"/> to the invariant
    /// culture. Without that, a machine running a UI culture that has a
    /// satellite assembly would read the translation instead of the neutral
    /// value and the assertions would be about the wrong string.</para>
    /// </summary>
    [TestClass]
    public class LocalizationSweepTests
    {
        private const string LegacyProductName = "DS4Windows";

        /// <summary>
        /// Cultures that ship as <c>&lt;culture&gt;/Thrum.resources.dll</c>.
        ///
        /// <para><c>Translations/Strings.idn.resx</c> is deliberately absent:
        /// <c>idn</c> is not a culture name (Indonesian is <c>id</c>), so
        /// MSBuild emits no satellite for it and that translation has never
        /// reached a user. Recorded in the identity map rather than renamed
        /// here, because renaming a resource file is a translation-shipping
        /// change and not part of a value-only sweep.</para>
        /// </summary>
        private static readonly string[] ShippedCultures =
        {
            "ar", "cs", "de", "el", "es", "fi", "fr", "he", "hu-HU", "it",
            "ja", "ms", "nl", "pl", "pt-BR", "pt", "ru", "se", "tr", "uk-UA",
            "vi", "zh-Hans", "zh-Hant",
        };

        /// <summary>
        /// The one key a translated file declares that the neutral file does
        /// not. Inherited from upstream and left alone: removing it would edit
        /// a translation for no user-visible gain, and adding it to the neutral
        /// file would invent an English string nobody wrote.
        /// </summary>
        private static readonly (string Culture, string Key)[] KnownOrphanKeys =
        {
            ("el", "ProfileEditor.VirtualTrigButtonOutput"),
        };

        private CultureInfo previousStringsCulture;
        private CultureInfo previousResourcesCulture;

        [TestInitialize]
        public void SetUp()
        {
            previousStringsCulture = Strings.Culture;
            previousResourcesCulture = DS4WinWPF.Properties.Resources.Culture;
            Strings.Culture = CultureInfo.InvariantCulture;
            DS4WinWPF.Properties.Resources.Culture = CultureInfo.InvariantCulture;
        }

        [TestCleanup]
        public void TearDown()
        {
            Strings.Culture = previousStringsCulture;
            DS4WinWPF.Properties.Resources.Culture = previousResourcesCulture;
        }

        [TestMethod]
        public void EveryFlippedNeutralStringNamesThisProduct()
        {
            var flipped = new Dictionary<string, string>
            {
                ["Strings.CheckUpdateStartup"] = Strings.CheckUpdateStartup,
                ["Strings.AntiDeadzoneTooltip"] = Strings.AntiDeadzoneTooltip,
                ["Strings.DualSRumbleForceGenericRescale_Tip"] =
                    Strings.DualSRumbleForceGenericRescale_Tip,
                ["Strings.TurnOffDS4WindowsTemporarily"] =
                    Strings.TurnOffDS4WindowsTemporarily,
                ["Strings.FirstLaunch_DeviceIntroText"] =
                    Strings.FirstLaunch_DeviceIntroText,
                ["Strings.SaveWhere_AppDataDescText"] =
                    Strings.SaveWhere_AppDataDescText,
                ["Strings.Welcome_Step5HelpText"] = Strings.Welcome_Step5HelpText,
                ["Strings.Welcome_WinTitle"] = Strings.Welcome_WinTitle,
                ["Resources.DS4Update"] = DS4WinWPF.Properties.Resources.DS4Update,
                ["Resources.LanguagePackApplyRestartRequired"] =
                    DS4WinWPF.Properties.Resources.LanguagePackApplyRestartRequired,
                ["Resources.StoppedDS4Windows"] =
                    DS4WinWPF.Properties.Resources.StoppedDS4Windows,
                ["Resources.UpToDate"] = DS4WinWPF.Properties.Resources.UpToDate,
            };

            var wrong = new List<string>();
            foreach (KeyValuePair<string, string> entry in flipped)
            {
                if (string.IsNullOrEmpty(entry.Value))
                {
                    wrong.Add($"{entry.Key}: resolved to nothing");
                    continue;
                }

                if (!entry.Value.Contains(ProductInfo.ProductName, StringComparison.Ordinal))
                {
                    wrong.Add($"{entry.Key}: does not name {ProductInfo.ProductName}");
                }

                if (entry.Value.Contains(LegacyProductName, StringComparison.OrdinalIgnoreCase))
                {
                    wrong.Add($"{entry.Key}: still names {LegacyProductName}");
                }
            }

            Assert.AreEqual(0, wrong.Count,
                "User-visible strings that name the wrong product:\n" +
                string.Join("\n", wrong));

            // The key of one of these deliberately still spells the old name.
            // Keys are contracts with 24 translated files and with XAML; only
            // the value was allowed to move.
            Assert.IsNotNull(
                Strings.ResourceManager.GetString(
                    "TurnOffDS4WindowsTemporarily", CultureInfo.InvariantCulture),
                "The sweep must not have renamed a key.");
        }

        [TestMethod]
        public void TheCustomExeNameHelpKeepsTheForeignNamesAndDropsTheDeadUpdater()
        {
            string help = Strings.CustomExeNameInfo;

            // These two name the executables a game looks for. They belong to
            // other products and must survive our rename, or the explanation
            // stops making sense.
            StringAssert.Contains(help, "DS4Windows.exe");
            StringAssert.Contains(help, "InputMapper.exe");

            // The external updater was deleted in plan task 1.7, so the
            // sentence that promised it would rename our executable is gone.
            Assert.IsFalse(help.Contains("DS4Updater", StringComparison.OrdinalIgnoreCase),
                "The help text still describes the deleted external updater.");
            StringAssert.Contains(help, ProductInfo.ProductName);
        }

        [TestMethod]
        public void TheUpstreamWikiLinkSurvivedTheSweep()
        {
            string text = DS4WinWPF.Properties.Resources.QuitOtherPrograms;

            // The only occurrence of the old name in this value is inside an
            // upstream documentation URL. Rewriting it would produce a 404,
            // which is why the sweep protected URLs. This is the positive
            // control for that protection.
            StringAssert.Contains(text,
                "https://github.com/Ryochan7/DS4Windows/wiki/");
            Assert.IsFalse(text.Contains(ProductInfo.ProductName, StringComparison.Ordinal),
                "The sweep rewrote a link it was supposed to leave alone.");
        }

        [TestMethod]
        public void EveryImportDialogKeyResolvesToNeutralText()
        {
            // The designer file is checked in and the command-line build never
            // regenerates it, so its properties were added by hand. A property
            // naming a key that does not exist compiles cleanly and returns
            // null at runtime; this is what notices.
            PropertyInfo[] properties = typeof(Strings)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.Name.StartsWith("Import_", StringComparison.Ordinal))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(17, properties.Length,
                "Expected the full set of import-dialog keys.");

            var unresolved = properties
                .Where(p => string.IsNullOrWhiteSpace((string)p.GetValue(null)))
                .Select(p => p.Name)
                .ToList();

            Assert.AreEqual(0, unresolved.Count,
                "Designer properties with no matching resx key:\n" +
                string.Join("\n", unresolved));
        }

        [TestMethod]
        public void TheImportDialogFormatStringsCarryTheirPlaceholders()
        {
            // Every one of these is passed to string.Format. A missing
            // placeholder loses information silently; an extra one throws.
            var expected = new Dictionary<string, int>
            {
                [Strings.Import_WinTitle] = 1,
                [Strings.Import_HeadingText] = 1,
                [Strings.Import_SourceText] = 3,
                [Strings.Import_FooterText] = 1,
                [Strings.Import_ProfileCountSingular] = 1,
                [Strings.Import_ProfileCountPlural] = 1,
                [Strings.Import_CollisionCountSingular] = 1,
                [Strings.Import_CollisionCountPlural] = 1,
                [Strings.Import_PartialFailureText] = 4,
            };

            foreach (KeyValuePair<string, int> entry in expected)
            {
                var indexes = Regex.Matches(entry.Key, @"\{(\d+)\}")
                    .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                    .Distinct()
                    .OrderBy(i => i)
                    .ToArray();

                CollectionAssert.AreEqual(Enumerable.Range(0, entry.Value).ToArray(), indexes,
                    $"Placeholder set is wrong in: {entry.Key}");
            }
        }

        [TestMethod]
        public void EveryExpectedTranslationShipsAsASatellite()
        {
            var missing = ShippedCultures
                .Where(culture => NeutralOrCultureKeys(culture) == null)
                .ToList();

            Assert.AreEqual(0, missing.Count,
                "Cultures whose satellite did not load. A resource file whose " +
                "name is not a culture name is dropped by MSBuild without an " +
                "error, which is how the idn translation became unreachable:\n" +
                string.Join(", ", missing));
        }

        [TestMethod]
        public void EverySatelliteOnlyDeclaresKeysTheNeutralFileDefines()
        {
            HashSet<string> neutral = NeutralOrCultureKeys(null);
            Assert.IsNotNull(neutral);

            var orphans = new List<string>();
            foreach (string culture in ShippedCultures)
            {
                HashSet<string> keys = NeutralOrCultureKeys(culture);
                if (keys == null)
                {
                    continue;
                }

                foreach (string key in keys.Except(neutral).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (KnownOrphanKeys.Contains((culture, key)))
                    {
                        continue;
                    }

                    orphans.Add($"{culture}: {key}");
                }
            }

            Assert.AreEqual(0, orphans.Count,
                "A translated resource file declares a key the neutral file " +
                "does not. Such a key can never be reached, because every " +
                "lookup goes through the neutral name:\n" +
                string.Join("\n", orphans));
        }

        /// <summary>
        /// Key names in one exact resource set: the neutral one when
        /// <paramref name="culture"/> is null, otherwise that culture's
        /// satellite with no parent fallback. Null when the set does not exist.
        /// </summary>
        private static HashSet<string> NeutralOrCultureKeys(string culture)
        {
            CultureInfo info = culture == null
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            ResourceSet set;
            try
            {
                set = Strings.ResourceManager.GetResourceSet(info,
                    createIfNotExists: true, tryParents: false);
            }
            catch (MissingManifestResourceException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            if (set == null)
            {
                return null;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in set)
            {
                keys.Add((string)entry.Key);
            }

            return keys;
        }
    }
}

using DS4WinWPF.DS4Forms.Localization;
using DS4WinWPF.Translations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DS4WindowsTests
{
    [TestClass]
    [DoNotParallelize]
    public class LocalizationMarkupExtensionTests
    {
        private const string MissingPrefix = "[[Missing localization:";

        [TestMethod]
        public void ConstructorsAndBothResourceFamiliesResolveNeutralText()
        {
            WithCurrentUiCulture(CultureInfo.InvariantCulture, () =>
            {
                Assert.AreEqual(
                    Strings.ResourceManager.GetString(
                        "Profile", CultureInfo.InvariantCulture),
                    new LocExtension("Profile").ProvideValue(null));

                var dotted = new LocExtension
                {
                    Key = "Welcome.Step2Text",
                };
                Assert.AreEqual(
                    Strings.ResourceManager.GetString(
                        "Welcome.Step2Text", CultureInfo.InvariantCulture),
                    dotted.ProvideValue(null));

                Assert.AreEqual(
                    DS4WinWPF.Properties.Resources.ResourceManager.GetString(
                        "RunAtStartup", CultureInfo.InvariantCulture),
                    new LocExtension("Resources:RunAtStartup")
                        .ProvideValue(null));
            });
        }

        [TestMethod]
        public void ResourcesPrefixIsExactAndMissingKeysStayVisible()
        {
            WithCurrentUiCulture(CultureInfo.InvariantCulture, () =>
            {
                AssertMissing(new LocExtension().ProvideValue(null));
                AssertMissing(new LocExtension("Resources:").ProvideValue(null));
                AssertMissing(new LocExtension("Does.Not.Exist")
                    .ProvideValue(null));

                // The compatibility prefix is deliberately ordinal and
                // case-sensitive. A near miss must not silently route to the
                // Properties/Resources family.
                AssertMissing(new LocExtension("resources:RunAtStartup")
                    .ProvideValue(null));
            });
        }

        [TestMethod]
        public void LookupUsesCurrentUiCultureAndDoesNotCacheAResult()
        {
            var extension = new LocExtension("Profile");

            WithCurrentUiCulture(CultureInfo.GetCultureInfo("en-US"), () =>
                Assert.AreEqual("Profile", extension.ProvideValue(null)));
            WithCurrentUiCulture(CultureInfo.GetCultureInfo("de"), () =>
                Assert.AreEqual("Profil", extension.ProvideValue(null)));
        }

        [TestMethod]
        public void ShorthandAndObjectSyntaxWorkInsideTemplatesAndStylesOnSta()
        {
            RunOnStaThread(() =>
            {
                WithCurrentUiCulture(CultureInfo.InvariantCulture, () =>
                {
                    const string xaml =
                        "<ResourceDictionary " +
                        "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                        "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
                        "xmlns:lex=\"clr-namespace:DS4WinWPF.DS4Forms.Localization;assembly=Thrum\">" +
                        "<DataTemplate x:Key=\"LocalizedTemplate\">" +
                        "<StackPanel>" +
                        "<TextBlock Text=\"{lex:Loc Profile}\" />" +
                        "<TextBlock Text=\"{Binding Source={lex:Loc Browse}}\" />" +
                        "</StackPanel>" +
                        "</DataTemplate>" +
                        "<Style x:Key=\"LocalizedStyle\" TargetType=\"{x:Type TextBlock}\">" +
                        "<Setter Property=\"Text\"><Setter.Value>" +
                        "<lex:Loc Key=\"Welcome.Step2Text\" />" +
                        "</Setter.Value></Setter>" +
                        "</Style>" +
                        "</ResourceDictionary>";

                    var resources = (ResourceDictionary)XamlReader.Parse(xaml);
                    var template = (DataTemplate)resources["LocalizedTemplate"];
                    var panel = (StackPanel)template.LoadContent();

                    Assert.AreEqual("Profile",
                        ((TextBlock)panel.Children[0]).Text);
                    Assert.AreEqual("Browse",
                        ((TextBlock)panel.Children[1]).Text);

                    var styled = new TextBlock
                    {
                        Style = (Style)resources["LocalizedStyle"],
                    };
                    Assert.AreEqual(
                        Strings.ResourceManager.GetString(
                            "Welcome.Step2Text", CultureInfo.InvariantCulture),
                        styled.Text);
                });
            });
        }

        [TestMethod]
        public void EveryMigratedXamlKeyResolves()
        {
            string formsRoot = Path.Combine(RepositoryRoot(),
                "DS4Windows", "DS4Forms");
            string[] files = Directory.GetFiles(formsRoot, "*.xaml",
                    SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "clr-namespace:DS4WinWPF.DS4Forms.Localization",
                    StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(26, files.Length,
                "The audited set of localized XAML files changed.");

            var uses = new List<(string File, string Key)>();
            var shorthand = new Regex(
                @"\{lex:Loc(?:Extension)?\s+(?<key>[^,}\s]+)",
                RegexOptions.CultureInvariant);
            var objectSyntax = new Regex(
                @"<lex:Loc(?:Extension)?\s+[^>]*Key=""(?<key>[^""]+)""",
                RegexOptions.CultureInvariant);

            foreach (string file in files)
            {
                // Commented XAML is not a runtime localization use. One
                // inherited, deliberately disabled trigger option contains a
                // Greek-only orphan key and must not distort this live-key
                // audit.
                string source = Regex.Replace(File.ReadAllText(file),
                    @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
                uses.AddRange(shorthand.Matches(source)
                    .Select(match => (file, match.Groups["key"].Value)));
                uses.AddRange(objectSyntax.Matches(source)
                    .Select(match => (file, match.Groups["key"].Value)));
            }

            Assert.AreEqual(602, uses.Count,
                "The audited localization-expression count changed.");

            var missing = new List<string>();
            WithCurrentUiCulture(CultureInfo.InvariantCulture, () =>
            {
                foreach ((string file, string key) in uses)
                {
                    object value = new LocExtension(key).ProvideValue(null);
                    if (value is not string text ||
                        text.StartsWith(MissingPrefix, StringComparison.Ordinal))
                    {
                        missing.Add($"{Path.GetFileName(file)}: {key}");
                    }
                }
            });

            Assert.AreEqual(0, missing.Count,
                "XAML localization keys that do not resolve:\n" +
                string.Join("\n", missing));
        }

        [TestMethod]
        public void ApplicationSourceHasNoLegacyLocalizationRuntimeReferences()
        {
            string appRoot = Path.Combine(RepositoryRoot(), "DS4Windows");
            string[] legacyTokens =
            {
                "WPFLocalizeExtension",
                "XAMLMarkupExtensions",
                "wpflocalizeextension.codeplex.com",
                "LocalizeDictionary",
                "ResxLocalizationProvider",
                "lex:BLoc",
            };

            var references = new List<string>();
            foreach (string file in Directory.GetFiles(appRoot, "*",
                         SearchOption.AllDirectories)
                .Where(IsAuditedApplicationSource))
            {
                string source = File.ReadAllText(file);
                foreach (string token in legacyTokens)
                {
                    if (source.Contains(token, StringComparison.Ordinal))
                    {
                        references.Add(
                            $"{Path.GetRelativePath(appRoot, file)}: {token}");
                    }
                }
            }

            Assert.AreEqual(0, references.Count,
                "Legacy localization runtime references remain:\n" +
                string.Join("\n", references));
        }

        private static bool IsAuditedApplicationSource(string path)
        {
            string relative = Path.GetRelativePath(
                Path.Combine(RepositoryRoot(), "DS4Windows"), path);
            if (relative.Split(Path.DirectorySeparatorChar)
                .Any(segment => segment.Equals("bin",
                                    StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals("obj",
                                    StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertMissing(object value)
        {
            Assert.IsInstanceOfType(value, typeof(string));
            StringAssert.StartsWith((string)value, MissingPrefix);
        }

        private static void WithCurrentUiCulture(
            CultureInfo culture, Action action)
        {
            CultureInfo previous = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = culture;
                action();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previous;
            }
        }

        private static void RunOnStaThread(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(
                    directory.FullName, "DS4WindowsWPF.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the repository root from the test output.");
        }
    }
}

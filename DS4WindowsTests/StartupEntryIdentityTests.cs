using DS4Windows;
using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the Windows startup entries — the logon scheduled task and the
    /// Startup-folder shortcut.
    ///
    /// <para>The hazard is specific and one-directional. A user who installs
    /// this product very likely still has a real DS4Windows install, with its
    /// own <c>RunDS4Windows</c> task and its own <c>DS4Windows.lnk</c>
    /// shortcut. Those entries belong to that install. Any code here that
    /// deletes or repairs a startup entry must be able to name only our own,
    /// and the settings page deletes startup entries on several paths
    /// (switching between task and shortcut, repairing a moved executable,
    /// turning the option off).</para>
    ///
    /// <para>So the assertion is not "we use the right name" but "the inherited
    /// names cannot be named at all": they are absent from the entire compiled
    /// application, which is a property no future edit can quietly break.</para>
    /// </summary>
    [TestClass]
    public class StartupEntryIdentityTests
    {
        /// <summary>
        /// The inherited names. Written out here, and nowhere else in the
        /// product, precisely so the scan below has something to look for.
        /// </summary>
        private const string LegacyStartupTaskName = "RunDS4Windows";
        private const string LegacyStartupShortcutName = "DS4Windows.lnk";

        [TestMethod]
        public void StartupEntryNamesComeFromTheProductIdentity()
        {
            Assert.AreEqual("Run" + ProductInfo.ProductName,
                ProductInfo.StartupTaskName);
            Assert.AreEqual(ProductInfo.ExeBaseName + ".lnk",
                ProductInfo.StartupShortcutName);
        }

        [TestMethod]
        public void StartupEntryNamesDifferFromTheInheritedOnes()
        {
            Assert.IsFalse(string.Equals(LegacyStartupTaskName,
                    ProductInfo.StartupTaskName,
                    StringComparison.OrdinalIgnoreCase),
                "Sharing the scheduled task name with a real DS4Windows " +
                "install would let either product delete the other's startup " +
                "entry.");
            Assert.IsFalse(string.Equals(LegacyStartupShortcutName,
                    ProductInfo.StartupShortcutName,
                    StringComparison.OrdinalIgnoreCase),
                "Sharing the Startup-folder shortcut name has the same " +
                "consequence as sharing the task name.");
        }

        /// <summary>
        /// The shortcut path the writer, the deleter, the "has an entry?" check
        /// and the settings view model all use has to be the same one, built
        /// from the product identity. A second, independently spelled copy of
        /// this path is exactly the bug the identity map recorded in
        /// <c>SettingsViewModel</c>.
        /// </summary>
        [TestMethod]
        public void TheStartupShortcutPathIsTheProductShortcutInTheStartupFolder()
        {
            string expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                ProductInfo.StartupShortcutName);

            Assert.AreEqual(expected, StartupMethods.lnkpath);
        }

        /// <summary>
        /// The load-bearing one. Searches the whole compiled application for
        /// the inherited startup entry names. A hit means some code path can
        /// name a real DS4Windows install's startup entry, which is the one
        /// thing our startup code must never be able to do — and it catches
        /// that whether the name arrives as a literal in
        /// <c>StartupMethods</c>, as a "legacy cleanup" helper somewhere else,
        /// or in a resource string a future dialog could pass along.
        /// </summary>
        [TestMethod]
        public void NoCodePathInTheApplicationCanNameTheInheritedStartupEntries()
        {
            string[] haystacks = ApplicationTextImages();

            var offenders = new List<string>();
            foreach (string legacy in
                new[] { LegacyStartupTaskName, LegacyStartupShortcutName })
            {
                if (ContainsText(haystacks, legacy))
                {
                    offenders.Add(legacy);
                }
            }

            Assert.AreEqual(0, offenders.Count,
                "The application can name a real DS4Windows install's startup " +
                "entries, which our startup code must never touch: " +
                string.Join(", ", offenders) +
                ". Scope the code to ProductInfo.StartupTaskName / " +
                "ProductInfo.StartupShortcutName, or delete the legacy " +
                "cleanup path.");

            // Negative control: the scan has to be able to find a name that is
            // genuinely in there, or the assertion above proves nothing.
            Assert.IsTrue(ContainsText(haystacks, ProductInfo.StartupTaskName),
                "The scan could not find our own scheduled task name, so it " +
                "cannot be trusted to find the inherited one either.");
            Assert.IsTrue(
                ContainsText(haystacks, ProductInfo.StartupShortcutName),
                "The scan could not find our own startup shortcut name.");
        }

        /// <summary>
        /// The application assembly decoded as UTF-16 text, at both byte
        /// alignments.
        ///
        /// <para>String literals live in the metadata <c>#US</c> heap as UTF-16
        /// with a variable-length prefix, so an individual literal can start at
        /// an odd file offset; decoding from offset 0 and from offset 1 covers
        /// both cases. Reading the file rather than reflecting over types is
        /// what makes this exhaustive — it does not matter which class the
        /// offending literal ends up in.</para>
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
            foreach (string haystack in haystacks)
            {
                if (haystack.IndexOf(needle,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

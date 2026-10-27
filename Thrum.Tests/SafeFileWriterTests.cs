using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class SafeFileWriterTests
    {
        [TestMethod]
        public void RewritingKeepsThePreviousVersionAndNoTemporaryFiles()
        {
            string directory = CreateDirectory();
            try
            {
                string path = Path.Combine(directory, "Profiles.xml");
                SafeFileWriter.WriteAllText(path, "<first/>");
                SafeFileWriter.WriteAllText(path, "<second/>");

                Assert.AreEqual("<second/>", File.ReadAllText(path));
                Assert.AreEqual("<first/>", File.ReadAllText(path + ".bak"));
                CollectionAssert.AreEquivalent(
                    new[] { "Profiles.xml", "Profiles.xml.bak" },
                    Directory.GetFiles(directory).Select(Path.GetFileName).ToArray());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ADamagedFileIsSetAsideAndTheBackupRestored()
        {
            string directory = CreateDirectory();
            try
            {
                string path = Path.Combine(directory, "Profiles.xml");
                File.WriteAllText(path + ".bak", "good");
                File.WriteAllText(path, "");

                bool restored = SettingsFileRecovery.Recover(path, "Test settings",
                    candidate => File.ReadAllText(candidate) == "good");

                Assert.IsTrue(restored);
                Assert.AreEqual("good", File.ReadAllText(path));
                Assert.AreEqual(1, Directory.GetFiles(directory, "Profiles.xml.damaged-*").Length,
                    "The damaged file must be kept, not overwritten by the next save.");
                StringAssert.Contains(SettingsFileRecovery.TakePendingNotice(),
                    "previous copy was restored");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void WithoutAUsableBackupDefaultsAreUsedAndTheUserIsTold()
        {
            string directory = CreateDirectory();
            try
            {
                string path = Path.Combine(directory, "Profiles.xml");
                File.WriteAllText(path, "not xml");

                bool restored = SettingsFileRecovery.Recover(path, "Test settings",
                    _ => false);

                Assert.IsFalse(restored);
                Assert.IsFalse(File.Exists(path));
                StringAssert.Contains(SettingsFileRecovery.TakePendingNotice(),
                    "default settings are being used");
                Assert.IsNull(SettingsFileRecovery.TakePendingNotice(),
                    "The notice is shown once.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ThrumSafeFileWriterTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}

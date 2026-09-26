using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DS4Windows;

namespace DS4WindowsTests
{
    /// <summary>
    /// installer/Thrum.iss repeats names the app defines; Inno Setup cannot
    /// read them from the code, so these tests keep the two in step.
    /// </summary>
    [TestClass]
    public class InstallerScriptTests
    {
        [TestMethod]
        public void SetupWaitsForTheMutexThrumHolds()
        {
            Assert.AreEqual(ProductInfo.InstallerAppMutexName, Define("AppMutexName"));
            StringAssert.Contains(Script(), "AppMutex={#AppMutexName}");
        }

        [TestMethod]
        public void UninstallRemovesTheRunAtStartupShortcut()
        {
            Assert.AreEqual(ProductInfo.StartupShortcutName, Define("StartupShortcutName"));
            StringAssert.Contains(Script(), @"Name: ""{userstartup}\{#StartupShortcutName}""");
        }

        [TestMethod]
        public void SetupNeverStartsThrumElevated()
        {
            // An all-users installation runs elevated; a program it starts
            // inherits that unless the entry says otherwise.
            string[] runEntries = Section("Run");
            Assert.IsTrue(runEntries.Length > 0);
            foreach (string entry in runEntries)
            {
                StringAssert.Contains(entry, "runasoriginaluser", entry);
            }
        }

        [TestMethod]
        public void TheReleaseWorkflowPublishesTheInstaller()
        {
            string workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "release.yml"));
            StringAssert.Contains(workflow, @"utils\build-installer.ps1");
            StringAssert.Contains(workflow, "${{ env.RELEASE_INSTALLER }}");
        }

        private static string Script() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "installer", "Thrum.iss"));

        private static string Define(string name)
        {
            Match match = Regex.Match(Script(),
                $@"^#define {name} ""(?<value>[^""]*)""\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, $"installer/Thrum.iss has no #define {name}.");
            return match.Groups["value"].Value;
        }

        private static string[] Section(string name)
        {
            string[] lines = Script().Replace("\r\n", "\n").Split('\n');
            return lines
                .SkipWhile(line => line.Trim() != $"[{name}]")
                .Skip(1)
                .TakeWhile(line => !line.TrimStart().StartsWith("["))
                .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith(";"))
                .ToArray();
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Thrum.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }
}

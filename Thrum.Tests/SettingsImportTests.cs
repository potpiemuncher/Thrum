using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// Covers the one-time import of an existing DS4Windows configuration.
    ///
    /// <para>These run against real temporary directories rather than a fake
    /// file system, because the properties worth protecting are file-system
    /// properties: that the source is never modified, that an existing
    /// destination is never overwritten, and that a failure part way through
    /// leaves a usable configuration behind. A failing copy is injected through
    /// <see cref="IImportFileSystem"/> — that is what the seam is for.</para>
    /// </summary>
    [TestClass]
    public class SettingsImportTests
    {
        public TestContext TestContext { get; set; }

        private string root;
        private string source;
        private string target;
        private CultureInfo previousStringsCulture;

        [TestInitialize]
        public void SetUp()
        {
            // The summary wording now comes from Strings.resx, so an assertion
            // on English text would otherwise depend on the machine's UI
            // culture. Pin the resource lookup to the neutral file.
            previousStringsCulture = DS4WinWPF.Translations.Strings.Culture;
            DS4WinWPF.Translations.Strings.Culture = CultureInfo.InvariantCulture;

            // TestRunDirectory when the adapter provides one, the process temp
            // path otherwise. Never a hard-coded location.
            string baseDirectory = TestContext?.TestRunDirectory;
            if (string.IsNullOrEmpty(baseDirectory) ||
                !Directory.Exists(baseDirectory))
            {
                baseDirectory = Path.GetTempPath();
            }

            root = Path.Combine(baseDirectory,
                "ThrumSettingsImport", Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "source");
            target = Path.Combine(root, "target");
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void TearDown()
        {
            DS4WinWPF.Translations.Strings.Culture = previousStringsCulture;

            try
            {
                if (root != null && Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory is not a test failure.
            }
        }

        // ---------------------------------------------------------------
        // Planning
        // ---------------------------------------------------------------

        [TestMethod]
        public void MissingSourceProducesAnEmptyPlan()
        {
            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.IsFalse(plan.SourceExists);
            Assert.IsTrue(plan.IsEmpty);
            Assert.AreEqual(0, plan.ProfileCount);
            Assert.AreEqual(0, ImportPlanSummary.Describe(plan).Count);
        }

        [TestMethod]
        public void PresentButEmptySourceProducesAnEmptyPlan()
        {
            Directory.CreateDirectory(source);

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.IsTrue(plan.SourceExists);
            Assert.IsTrue(plan.IsEmpty);
        }

        [TestMethod]
        public void FullSourceProducesACompletePlan()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing", "FPS" });

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.IsTrue(plan.SourceExists);
            Assert.AreEqual(9, plan.Items.Count,
                "Six single-file items plus three profiles were expected.");
            Assert.AreEqual(3, plan.ProfileCount);
            Assert.AreEqual(0, plan.CollisionCount);

            foreach (ImportItemKind kind in
                Enum.GetValues(typeof(ImportItemKind)).Cast<ImportItemKind>())
            {
                Assert.IsTrue(plan.Contains(kind),
                    $"A full source should plan an item of kind {kind}.");
            }

            // Targets must land under the target directory, with profiles in
            // the Profiles sub-folder.
            foreach (ImportItem item in plan.Items)
            {
                Assert.IsTrue(item.TargetPath.StartsWith(target,
                    StringComparison.OrdinalIgnoreCase), item.TargetPath);
                Assert.IsFalse(item.TargetExists);
            }

            Assert.AreEqual(
                Path.Combine(target, "Profiles", "Default.xml"),
                plan.Items.Single(item =>
                    item.RelativePath == Path.Combine("Profiles", "Default.xml"))
                    .TargetPath);
        }

        [TestMethod]
        public void PartialSourcePlansOnlyWhatExists()
        {
            Directory.CreateDirectory(source);
            WriteFile(Path.Combine(source, "Profiles.xml"), "settings");
            Directory.CreateDirectory(Path.Combine(source, "Profiles"));
            WriteFile(Path.Combine(source, "Profiles", "Default.xml"), "p");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.AreEqual(2, plan.Items.Count);
            Assert.AreEqual(1, plan.ProfileCount);
            Assert.IsTrue(plan.Contains(ImportItemKind.AppSettings));
            Assert.IsFalse(plan.Contains(ImportItemKind.AutoProfiles));
            Assert.IsFalse(plan.Contains(ImportItemKind.Actions));
            Assert.IsFalse(plan.Contains(ImportItemKind.OutputSlots));
        }

        [TestMethod]
        public void NonXmlFilesInTheProfilesFolderAreNotPlanned()
        {
            Directory.CreateDirectory(Path.Combine(source, "Profiles"));
            WriteFile(Path.Combine(source, "Profiles", "Default.xml"), "p");
            WriteFile(Path.Combine(source, "Profiles", "notes.txt"), "x");
            // The 8.3 short-name quirk: a Win32 "*.xml" search can also return
            // this one, so the planner re-checks the real extension.
            WriteFile(Path.Combine(source, "Profiles", "Old.xmlbackup"), "x");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.AreEqual(1, plan.ProfileCount);
            Assert.AreEqual(Path.Combine("Profiles", "Default.xml"),
                plan.Items.Single().RelativePath);
        }

        [TestMethod]
        public void ExistingTargetFilesAreReportedAsCollisions()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing" });
            Directory.CreateDirectory(Path.Combine(target, "Profiles"));
            WriteFile(Path.Combine(target, "Profiles.xml"), "mine");
            WriteFile(Path.Combine(target, "Profiles", "Racing.xml"), "mine");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.AreEqual(2, plan.CollisionCount);
            Assert.IsTrue(plan.Items
                .Single(item => item.RelativePath == "Profiles.xml")
                .TargetExists);
            Assert.IsFalse(plan.Items
                .Single(item => item.RelativePath == "Actions.xml")
                .TargetExists);
        }

        [TestMethod]
        public void ASourceThatIsAlsoTheTargetProducesAnEmptyPlan()
        {
            WriteFullSource(profileNames: new[] { "Default" });

            ImportPlan plan = new ImportPlanner().CreatePlan(source, source);

            Assert.IsTrue(plan.IsEmpty);
        }

        [TestMethod]
        public void TheDefaultSourceIsTheLegacyAppDataFolder()
        {
            Assert.AreEqual(
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "DS4Windows"),
                ImportPlanner.DefaultSourceDirectory());

            // The source folder name is a foreign product's and must never be
            // derived from this product's identity.
            Assert.AreNotEqual(ProductInfo.AppDataFolderName,
                ImportPlanner.LegacySourceFolderName);
        }

        // ---------------------------------------------------------------
        // Execution
        // ---------------------------------------------------------------

        [TestMethod]
        public void ExecutingAFullPlanCopiesEverythingAndLeavesTheSourceAlone()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing" });
            IReadOnlyDictionary<string, string> sourceBefore = SnapshotSource();

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);
            ImportResult result = new ImportExecutor().Execute(plan);

            Assert.AreEqual(plan.Items.Count, result.CopiedCount);
            Assert.AreEqual(0, result.SkippedCount);
            Assert.AreEqual(0, result.FailedCount);
            Assert.IsFalse(result.AnyFailed);
            Assert.IsTrue(result.Landed(ImportItemKind.AppSettings));

            foreach (ImportItem item in plan.Items)
            {
                Assert.IsTrue(File.Exists(item.TargetPath), item.RelativePath);
                Assert.AreEqual(File.ReadAllText(item.SourcePath),
                    File.ReadAllText(item.TargetPath), item.RelativePath);
            }

            AssertSourceUnchanged(sourceBefore);
        }

        [TestMethod]
        public void ExecutingSkipsCollisionsInsteadOfOverwriting()
        {
            WriteFullSource(profileNames: new[] { "Default" });
            Directory.CreateDirectory(Path.Combine(target, "Profiles"));
            WriteFile(Path.Combine(target, "Profiles.xml"), "keep me");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);
            ImportResult result = new ImportExecutor().Execute(plan);

            Assert.AreEqual(1, result.SkippedCount);
            Assert.AreEqual(plan.Items.Count - 1, result.CopiedCount);
            Assert.AreEqual(0, result.FailedCount);
            Assert.AreEqual("keep me",
                File.ReadAllText(Path.Combine(target, "Profiles.xml")));

            // The destination was already there, so the configuration counts as
            // present even though this run did not write it.
            Assert.IsTrue(result.Landed(ImportItemKind.AppSettings));
        }

        [TestMethod]
        public void RerunningAnImportOnlyCopiesWhatIsStillMissing()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing" });

            var planner = new ImportPlanner();
            var executor = new ImportExecutor();
            ImportResult first = executor.Execute(planner.CreatePlan(source, target));
            Assert.AreEqual(0, first.SkippedCount);

            // A new profile appears in the source between the two runs.
            WriteFile(Path.Combine(source, "Profiles", "Later.xml"), "later");

            ImportResult second =
                executor.Execute(planner.CreatePlan(source, target));

            Assert.AreEqual(1, second.CopiedCount);
            Assert.AreEqual(first.CopiedCount, second.SkippedCount);
            Assert.AreEqual(0, second.FailedCount);
            Assert.IsTrue(File.Exists(
                Path.Combine(target, "Profiles", "Later.xml")));
        }

        [TestMethod]
        public void AFailedItemDoesNotStopTheRestAndIsReported()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing", "FPS" });
            IReadOnlyDictionary<string, string> sourceBefore = SnapshotSource();

            string doomed = Path.Combine(target, "Profiles", "Racing.xml");
            var fileSystem = new FailingCopyFileSystem(doomed,
                new IOException("injected copy failure"));

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);
            ImportResult result = new ImportExecutor(fileSystem).Execute(plan);

            Assert.AreEqual(1, result.FailedCount);
            Assert.IsTrue(result.AnyFailed);
            Assert.AreEqual(plan.Items.Count - 1, result.CopiedCount);

            ImportItemResult failure = result.Failures.Single();
            Assert.AreEqual(Path.Combine("Profiles", "Racing.xml"),
                failure.Item.RelativePath);
            StringAssert.Contains(failure.FailureMessage,
                "injected copy failure");

            // Everything else landed, so the configuration is usable.
            Assert.IsFalse(File.Exists(doomed));
            Assert.IsTrue(File.Exists(Path.Combine(target, "Profiles.xml")));
            Assert.IsTrue(File.Exists(
                Path.Combine(target, "Profiles", "Default.xml")));
            Assert.IsTrue(File.Exists(
                Path.Combine(target, "Profiles", "FPS.xml")));
            Assert.IsTrue(result.Landed(ImportItemKind.AppSettings));

            AssertSourceUnchanged(sourceBefore);
        }

        [TestMethod]
        public void ARerunAfterAFailureFinishesTheImport()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing" });

            string doomed = Path.Combine(target, "Profiles", "Racing.xml");
            var planner = new ImportPlanner();
            new ImportExecutor(new FailingCopyFileSystem(doomed,
                    new IOException("injected copy failure")))
                .Execute(planner.CreatePlan(source, target));

            ImportResult retry =
                new ImportExecutor().Execute(planner.CreatePlan(source, target));

            Assert.AreEqual(0, retry.FailedCount);
            Assert.AreEqual(1, retry.CopiedCount,
                "Only the item that failed the first time should be copied.");
            Assert.IsTrue(File.Exists(doomed));
        }

        [TestMethod]
        public void AnEmptyPlanExecutesToAnEmptyResult()
        {
            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);
            ImportResult result = new ImportExecutor().Execute(plan);

            Assert.AreEqual(0, result.Items.Count);
            Assert.IsFalse(result.AnyFailed);
            Assert.IsFalse(result.AnyCopied);
            Assert.IsFalse(Directory.Exists(target),
                "An empty plan must not create the target directory.");
        }

        // ---------------------------------------------------------------
        // Offer state: pristine detection and the decline marker
        // ---------------------------------------------------------------

        [TestMethod]
        public void AnAbsentOrConfigLessTargetIsPristine()
        {
            var planner = new ImportPlanner();

            Assert.IsTrue(planner.IsTargetPristine(target));

            Directory.CreateDirectory(target);
            Assert.IsTrue(planner.IsTargetPristine(target));

            WriteFile(Path.Combine(target, "Actions.xml"), "actions");
            Assert.IsTrue(planner.IsTargetPristine(target),
                "Only app settings and auto-profile rules define an existing " +
                "configuration.");
        }

        [TestMethod]
        public void EitherConfigFileMakesTheTargetNotPristine()
        {
            var planner = new ImportPlanner();
            Directory.CreateDirectory(target);

            WriteFile(Path.Combine(target, "Profiles.xml"), "settings");
            Assert.IsFalse(planner.IsTargetPristine(target));

            File.Delete(Path.Combine(target, "Profiles.xml"));
            WriteFile(Path.Combine(target, "Auto Profiles.xml"), "auto");
            Assert.IsFalse(planner.IsTargetPristine(target));
        }

        [TestMethod]
        public void DecliningIsRememberedAcrossPlannerInstances()
        {
            Assert.IsFalse(new ImportPlanner().WasOfferDeclined(target));

            Assert.IsTrue(new ImportPlanner().RecordOfferDeclined(target));

            Assert.IsTrue(new ImportPlanner().WasOfferDeclined(target));
            Assert.IsTrue(File.Exists(Path.Combine(target,
                ImportPlanner.DeclineMarkerFileName)));
        }

        // ---------------------------------------------------------------
        // Summary text
        // ---------------------------------------------------------------

        [TestMethod]
        public void TheSummaryNamesWhatWasFound()
        {
            WriteFullSource(profileNames: new[] { "Default", "Racing" });
            Directory.CreateDirectory(target);
            WriteFile(Path.Combine(target, "Actions.xml"), "mine");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);
            IReadOnlyList<string> lines = ImportPlanSummary.Describe(plan);

            Assert.AreEqual("2 controller profiles", lines[0]);
            CollectionAssert.Contains(lines.ToList(),
                "Auto-profile rules");
            Assert.IsTrue(lines.Any(line => line.Contains("already present")),
                "A collision should be spelled out before the user commits.");
        }

        [TestMethod]
        public void TheSummaryUsesTheSingularForOneProfile()
        {
            Directory.CreateDirectory(Path.Combine(source, "Profiles"));
            WriteFile(Path.Combine(source, "Profiles", "Default.xml"), "p");

            ImportPlan plan = new ImportPlanner().CreatePlan(source, target);

            Assert.AreEqual("1 controller profile",
                ImportPlanSummary.Describe(plan)[0]);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private void WriteFullSource(string[] profileNames)
        {
            Directory.CreateDirectory(source);
            WriteFile(Path.Combine(source, "Profiles.xml"), "app settings");
            WriteFile(Path.Combine(source, "Auto Profiles.xml"), "auto");
            WriteFile(Path.Combine(source, "Actions.xml"), "actions");
            WriteFile(Path.Combine(source, "LinkedProfiles.xml"), "linked");
            WriteFile(Path.Combine(source, "ControllerConfigs.xml"), "configs");
            WriteFile(Path.Combine(source, "OutputSlots.xml"), "slots");

            Directory.CreateDirectory(Path.Combine(source, "Profiles"));
            foreach (string name in profileNames)
            {
                WriteFile(Path.Combine(source, "Profiles", name + ".xml"),
                    "profile " + name);
            }
        }

        private static void WriteFile(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        private IReadOnlyDictionary<string, string> SnapshotSource()
        {
            return Directory
                .GetFiles(source, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllText,
                    StringComparer.OrdinalIgnoreCase);
        }

        private void AssertSourceUnchanged(
            IReadOnlyDictionary<string, string> before)
        {
            string[] after =
                Directory.GetFiles(source, "*", SearchOption.AllDirectories);

            CollectionAssert.AreEquivalent(before.Keys.ToList(), after.ToList(),
                "The import added or removed files in the source.");
            foreach (string path in after)
            {
                Assert.AreEqual(before[path], File.ReadAllText(path),
                    $"The import modified the source file {Path.GetFileName(path)}.");
            }
        }

        /// <summary>
        /// The real file system, except that one destination always fails to
        /// copy. This is the seam's reason to exist: an unwritable target is
        /// otherwise awkward to arrange, and impossible to arrange
        /// deterministically for a specific file.
        /// </summary>
        private sealed class FailingCopyFileSystem : IImportFileSystem
        {
            private readonly IImportFileSystem inner =
                new PhysicalImportFileSystem();
            private readonly string failingDestination;
            private readonly Exception failure;

            public FailingCopyFileSystem(string failingDestination,
                Exception failure)
            {
                this.failingDestination = failingDestination;
                this.failure = failure;
            }

            public bool DirectoryExists(string path) =>
                inner.DirectoryExists(path);

            public bool FileExists(string path) => inner.FileExists(path);

            public IEnumerable<string> EnumerateFiles(string path,
                string searchPattern) =>
                inner.EnumerateFiles(path, searchPattern);

            public void CreateDirectory(string path) =>
                inner.CreateDirectory(path);

            public void CopyFile(string sourcePath, string destinationPath)
            {
                if (string.Equals(destinationPath, failingDestination,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw failure;
                }

                inner.CopyFile(sourcePath, destinationPath);
            }

            public void WriteAllText(string path, string contents) =>
                inner.WriteAllText(path, contents);

            public string ReadAllText(string path) => inner.ReadAllText(path);
        }
    }
}

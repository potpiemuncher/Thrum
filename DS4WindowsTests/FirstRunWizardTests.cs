using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using System.Xml.Linq;

namespace DS4WindowsTests
{
    [TestClass]
    public class FirstRunStartupCoordinatorTests
    {
        [TestMethod]
        public void PristineStateIsSampledBeforeContinuationCanWriteLocation()
        {
            var events = new List<string>();
            var source = new FakeSnapshotSource(events);

            FirstRunStartupSnapshot snapshot =
                FirstRunStartupCoordinator.CaptureAndContinue(source,
                    _ => events.Add("data-location-write"));

            CollectionAssert.AreEqual(new[]
            {
                "find-config-location",
                "sample-pristine:C:\\AppData\\Thrum",
                "data-location-write",
            }, events);
            Assert.IsTrue(snapshot.FirstRun);
            Assert.IsTrue(snapshot.AppDataConfigPristine);
        }

        private sealed class FakeSnapshotSource :
            IFirstRunStartupSnapshotSource
        {
            private readonly List<string> events;

            public FakeSnapshotSource(List<string> events) =>
                this.events = events;

            public bool FirstRun => true;
            public string AppDataPath => "C:\\AppData\\Thrum";

            public void FindConfigLocation() =>
                events.Add("find-config-location");

            public bool IsTargetPristine(string path)
            {
                events.Add("sample-pristine:" + path);
                return true;
            }
        }
    }

    [TestClass]
    public class FirstRunDataLocationRouterTests
    {
        [TestMethod]
        public void AppDataDefaultRoutesThroughSaveDefaultThenSaveWhere()
        {
            var operations = new FakeDataLocationOperations();
            var router = new FirstRunDataLocationRouter(operations);

            bool applied = router.Apply(FirstRunDataLocation.AppData,
                multipleSaveSpots: false, keepExistingSettings: false);

            Assert.IsTrue(applied);
            CollectionAssert.AreEqual(new[]
            {
                "save-default:" + Path.Combine(operations.AppDataPath,
                    "Profiles.xml"),
                "save-where:" + operations.AppDataPath,
            }, operations.Events);
        }

        [TestMethod]
        public void PortableRoutesThroughSaveWhereThenPortableDefault()
        {
            var operations = new FakeDataLocationOperations();
            var router = new FirstRunDataLocationRouter(operations);

            bool applied = router.Apply(FirstRunDataLocation.Portable,
                multipleSaveSpots: false, keepExistingSettings: false);

            Assert.IsTrue(applied);
            CollectionAssert.AreEqual(new[]
            {
                "admin-needed",
                "save-where:" + operations.ExeDirectoryPath,
                "save-default:" + Path.Combine(operations.ExeDirectoryPath,
                    "Profiles.xml"),
            }, operations.Events);
        }

        [TestMethod]
        public void AdminNeededRefusesPortableWithoutWritingAnything()
        {
            var operations = new FakeDataLocationOperations
            {
                NeedsAdministrator = true,
            };
            var router = new FirstRunDataLocationRouter(operations);

            bool applied = router.Apply(FirstRunDataLocation.Portable,
                multipleSaveSpots: false, keepExistingSettings: false);

            Assert.IsFalse(applied);
            CollectionAssert.AreEqual(new[] { "admin-needed" },
                operations.Events);
        }

        [TestMethod]
        public void MultiLocationAppDataUsesSaveWhereCleanupOrder()
        {
            var operations = new FakeDataLocationOperations();
            var router = new FirstRunDataLocationRouter(operations);

            router.Apply(FirstRunDataLocation.AppData,
                multipleSaveSpots: true, keepExistingSettings: false);

            CollectionAssert.AreEqual(new[]
            {
                "delete-directory:" + Path.Combine(
                    operations.ExeDirectoryPath, "Profiles") + ":True",
                "delete-file:" + Path.Combine(
                    operations.ExeDirectoryPath, "Profiles.xml"),
                "delete-file:" + Path.Combine(
                    operations.ExeDirectoryPath, "Auto Profiles.xml"),
                "save-where:" + operations.AppDataPath,
            }, operations.Events);
        }

        private sealed class FakeDataLocationOperations :
            IFirstRunDataLocationOperations
        {
            public string AppDataPath => "C:\\Users\\test\\AppData\\Thrum";
            public string ExeDirectoryPath => "C:\\Thrum";
            public bool NeedsAdministrator { get; set; }
            public List<string> Events { get; } = new();

            public bool AdminNeeded()
            {
                Events.Add("admin-needed");
                return NeedsAdministrator;
            }

            public void SaveWhere(string path) =>
                Events.Add("save-where:" + path);

            public void SaveDefault(string path) =>
                Events.Add("save-default:" + path);

            public bool DirectoryExists(string path)
            {
                Events.Add("directory-exists:" + path);
                return true;
            }

            public void DeleteDirectory(string path, bool recursive) =>
                Events.Add($"delete-directory:{path}:{recursive}");

            public void DeleteFile(string path) =>
                Events.Add("delete-file:" + path);

            public void ShowCannotDeleteOldSettings() =>
                Events.Add("cannot-delete");
        }
    }

    [TestClass]
    public class FirstRunWizardNavigationTests
    {
        [TestMethod]
        public void NoImportWalksEveryRequiredStepInOrder()
        {
            var effects = new FakeWizardEffects();
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            AssertStep(wizard, FirstRunStepKind.Welcome);
            Assert.IsTrue(wizard.CanAdvance);
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.DataLocation);
            Assert.IsTrue(((FirstRunDataLocationStepViewModel)
                wizard.CurrentStep).IsAppDataSelected,
                "Appdata must be the preselected default.");

            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.DeviceOptions);
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.BackendAndDriver);
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.Controllers);
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.Finish);
            Assert.AreEqual(FirstRunAdvanceResult.Finished,
                wizard.Advance());

            Assert.IsTrue(wizard.CanContinueStartup);
            Assert.IsTrue(wizard.IsComplete);
            CollectionAssert.AreEqual(new[]
            {
                FirstRunStepKind.Welcome,
                FirstRunStepKind.DataLocation,
                FirstRunStepKind.DeviceOptions,
                FirstRunStepKind.BackendAndDriver,
                FirstRunStepKind.Controllers,
                FirstRunStepKind.Finish,
            }, wizard.Steps.Select(step => step.Kind).ToArray());
            Assert.AreEqual(1, effects.SaveCalls);
            CollectionAssert.AreEqual(new[]
            {
                "apply-data:AppData",
                "prepare",
                "find-import:True",
                "load",
                "save",
                "read-viiper:False",
            }, effects.Events);
        }

        [TestMethod]
        public void ImportIsSkippedWhenTargetWasNotPristine()
        {
            var effects = new FakeWizardEffects
            {
                PlanToReturn = CreatePlan(),
            };
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: false);

            wizard.Advance();
            wizard.Advance();

            AssertStep(wizard, FirstRunStepKind.DeviceOptions);
            Assert.AreEqual(0, effects.FindImportCalls,
                "A non-pristine target must be rejected before plan lookup.");
            Assert.IsFalse(wizard.Steps.Any(step =>
                step.Kind == FirstRunStepKind.Import));
        }

        [TestMethod]
        public void ImportIsSkippedForPortableLocation()
        {
            var effects = new FakeWizardEffects
            {
                PlanToReturn = CreatePlan(),
            };
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            var data = (FirstRunDataLocationStepViewModel)wizard.CurrentStep;
            data.IsPortableSelected = true;
            wizard.Advance();

            AssertStep(wizard, FirstRunStepKind.DeviceOptions);
            Assert.AreEqual(0, effects.FindImportCalls);
            Assert.IsFalse(wizard.Steps.Any(step =>
                step.Kind == FirstRunStepKind.Import));
        }

        [TestMethod]
        public void ImportedConfigurationSkipsDeviceSaveBootstrap()
        {
            var effects = new FakeWizardEffects
            {
                PlanToReturn = CreatePlan(),
                ImportLanded = true,
            };
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.Import);
            wizard.Advance();

            AssertStep(wizard, FirstRunStepKind.BackendAndDriver);
            Assert.IsFalse(wizard.FirstRunAfterImport);
            Assert.IsFalse(wizard.Steps.Any(step =>
                step.Kind == FirstRunStepKind.DeviceOptions));
            wizard.Cancel();
            Assert.IsTrue(wizard.CanContinueStartup);
            Assert.AreEqual(0, effects.SaveCalls,
                "Global.Save/default bootstrap must not rewrite an import.");
            CollectionAssert.AreEqual(new[]
            {
                "apply-data:AppData",
                "prepare",
                "find-import:True",
                "offer-import",
                "load",
                "read-viiper:False",
            }, effects.Events,
                "The import must finish before Global.Load and status reads.");
        }

        [TestMethod]
        public void DeclinedImportContinuesThroughDeviceOptions()
        {
            var effects = new FakeWizardEffects
            {
                PlanToReturn = CreatePlan(),
                ImportLanded = false,
            };
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            wizard.Advance();
            wizard.Advance();

            AssertStep(wizard, FirstRunStepKind.DeviceOptions);
            Assert.IsTrue(wizard.FirstRunAfterImport);
        }

        [TestMethod]
        public void CancelBeforeDataChoiceMatchesSaveWhereNoChoice()
        {
            var effects = new FakeWizardEffects();
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Cancel();

            Assert.IsTrue(wizard.IsComplete);
            Assert.IsFalse(wizard.CanContinueStartup);
            Assert.AreEqual(0, effects.SaveCalls);
            Assert.AreEqual(0, effects.LoadCalls);
        }

        [TestMethod]
        public void CancelAfterDataChoiceCompletesOldRequiredSavePath()
        {
            var effects = new FakeWizardEffects();
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            wizard.Advance();
            AssertStep(wizard, FirstRunStepKind.DeviceOptions);
            wizard.Cancel();

            Assert.IsTrue(wizard.CanContinueStartup);
            Assert.AreEqual(1, effects.LoadCalls);
            Assert.AreEqual(1, effects.SaveCalls);
        }

        [TestMethod]
        public void EveryConstructedStepExposesCanAdvance()
        {
            var effects = new FakeWizardEffects();
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            wizard.Advance();

            Assert.IsTrue(wizard.Steps.All(step => step.CanAdvance));
        }

        [TestMethod]
        public void BackendInstallUsesExistingEffectThenRechecksGate()
        {
            var effects = new FakeWizardEffects();
            var wizard = new FirstRunWizardViewModel(effects,
                appDataConfigPristine: true);

            wizard.Advance();
            wizard.Advance();
            wizard.Advance();
            var backend = (FirstRunBackendStepViewModel)wizard.CurrentStep;
            backend.InstallOrRepair();

            CollectionAssert.AreEqual(new[]
            {
                "launch-viiper",
                "read-viiper:True",
            }, effects.Events.TakeLast(2).ToArray());
        }

        private static void AssertStep(FirstRunWizardViewModel wizard,
            FirstRunStepKind expected) =>
            Assert.AreEqual(expected, wizard.CurrentStepKind);

        private static ImportPlan CreatePlan()
        {
            var item = new ImportItem(ImportItemKind.AppSettings,
                "Profiles.xml", "C:\\source\\Profiles.xml",
                "C:\\target\\Profiles.xml", targetExists: false);
            return new ImportPlan("C:\\source", "C:\\target",
                sourceExists: true, new[] { item });
        }

        private sealed class FakeWizardEffects : IFirstRunWizardEffects
        {
            public bool MultipleSaveSpots { get; set; }
            public bool PortableAllowed { get; set; } = true;
            public ControlServiceDeviceOptions DeviceOptions { get; } = new();
            public ImportPlan PlanToReturn { get; set; }
            public bool ImportLanded { get; set; }
            public int FindImportCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public int SaveCalls { get; private set; }
            public List<string> Events { get; } = new();

            public bool ApplyDataLocation(FirstRunDataLocation location,
                bool keepExistingSettings)
            {
                Events.Add("apply-data:" + location);
                return location != FirstRunDataLocation.Portable ||
                    PortableAllowed;
            }

            public bool PrepareConfiguration()
            {
                Events.Add("prepare");
                return true;
            }

            public ImportPlan FindImportPlan(bool appDataConfigPristine)
            {
                FindImportCalls++;
                Events.Add("find-import:" + appDataConfigPristine);
                return PlanToReturn;
            }

            public bool OfferImport(ImportPlan plan)
            {
                Events.Add("offer-import");
                return ImportLanded;
            }

            public void DeclineImport(ImportPlan plan) =>
                Events.Add("decline-import");

            public bool LoadConfiguration()
            {
                LoadCalls++;
                Events.Add("load");
                return false;
            }

            public void SaveConfiguration()
            {
                SaveCalls++;
                Events.Add("save");
            }

            public ViiperPrerequisiteStatus ReadViiperStatus(
                bool refreshDriver)
            {
                Events.Add("read-viiper:" + refreshDriver);
                return new ViiperPrerequisiteStatus();
            }

            public bool LaunchViiperInstaller(
                ViiperPrerequisiteStatus status)
            {
                Events.Add("launch-viiper");
                return true;
            }
        }
    }

    [TestClass]
    public class FirstRunWizardContractTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        [TestMethod]
        public void WizardHasKeyboardDefaultCancelAndAdvancedPortableControl()
        {
            XDocument document = XDocument.Load(SourcePath("DS4Windows",
                "DS4Forms", "FirstRunWizard.xaml"));
            XElement[] buttons = document.Descendants(
                Presentation + "Button").ToArray();

            Assert.IsNotNull(buttons.SingleOrDefault(button =>
                (string)button.Attribute("IsDefault") == "True"),
                "Next/Finish must remain the Enter key default.");
            Assert.IsNotNull(buttons.SingleOrDefault(button =>
                (string)button.Attribute("IsCancel") == "True"),
                "Cancel/Finish later must remain reachable with Escape.");
            Assert.IsTrue(buttons.Where(button =>
                    button.Attribute("Click") != null)
                .All(button => button.Attribute("TabIndex") != null),
                "Every actionable wizard button needs an explicit tab order.");
            Assert.IsTrue(document.Descendants(Presentation + "Expander")
                .Any(expander => ((string)expander.Attribute("Header"))
                    ?.Contains("portable", StringComparison.OrdinalIgnoreCase)
                    == true));
        }

        [TestMethod]
        public void WizardBrushesAreDynamicAndDefinedByBothThemes()
        {
            string xamlPath = SourcePath("DS4Windows", "DS4Forms",
                "FirstRunWizard.xaml");
            string xaml = File.ReadAllText(xamlPath);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(xaml,
                @"(?:Background|Foreground|BorderBrush)\s*=\s*""#[0-9A-Fa-f]"),
                "Wizard brush properties must not hardcode colours.");

            string[] dynamicKeys =
                System.Text.RegularExpressions.Regex.Matches(xaml,
                        @"\{DynamicResource\s+([^}\s,]+)")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(match => match.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

            foreach (string theme in new[] { "DefaultTheme", "DarkTheme" })
            {
                XNamespace x =
                    "http://schemas.microsoft.com/winfx/2006/xaml";
                string[] keys = XDocument.Load(SourcePath("DS4Windows",
                        "DS4Forms", "Themes", theme + ".xaml"))
                    .Descendants()
                    .Select(element => (string)element.Attribute(x + "Key"))
                    .Where(key => !string.IsNullOrEmpty(key))
                    .ToArray();
                foreach (string dynamicKey in dynamicKeys)
                {
                    CollectionAssert.Contains(keys, dynamicKey,
                        $"{theme} does not define {dynamicKey}.");
                }
            }
        }

        [TestMethod]
        public void StartupUsesWizardAndReadOnlyBackendStatus()
        {
            string app = File.ReadAllText(SourcePath("DS4Windows",
                "App.xaml.cs"));
            string effects = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Forms", "FirstRunWizardEffects.cs"));

            StringAssert.Contains(app,
                "FirstRunStartupCoordinator.CaptureAndContinue");
            StringAssert.Contains(app, "new DS4Forms.FirstRunWizard(");
            Assert.IsFalse(app.Contains("new DS4Forms.SaveWhere",
                StringComparison.Ordinal));
            Assert.IsFalse(app.Contains("new DS4Forms.FirstLaunchUtilWindow",
                StringComparison.Ordinal));
            Assert.IsFalse(app.Contains(
                "ViiperSetupManager.EnsureReadyWithPrompt(null)",
                StringComparison.Ordinal));
            StringAssert.Contains(effects,
                "GetStatus(tryStartServer: false)");
        }

        private static string SourcePath(params string[] parts) =>
            Path.Combine(new[] { FindRepositoryRoot() }.Concat(parts)
                .ToArray());

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName,
                    "DS4WindowsWPF.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate the repository root.");
        }
    }
}

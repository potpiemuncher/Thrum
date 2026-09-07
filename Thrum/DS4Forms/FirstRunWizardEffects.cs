using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    internal sealed class FirstRunWizardEffects : IFirstRunWizardEffects
    {
        private readonly FirstRunDataLocationRouter dataLocationRouter;
        private readonly Func<bool> prepareConfiguration;
        private readonly Func<bool> loadConfiguration;
        private readonly Action<string> log;

        public FirstRunWizardEffects(bool multipleSaveSpots,
            Func<bool> prepareConfiguration, Func<bool> loadConfiguration,
            Action<string> log)
        {
            MultipleSaveSpots = multipleSaveSpots;
            dataLocationRouter = new FirstRunDataLocationRouter(
                new GlobalFirstRunDataLocationOperations());
            this.prepareConfiguration = prepareConfiguration ??
                throw new ArgumentNullException(nameof(prepareConfiguration));
            this.loadConfiguration = loadConfiguration ??
                throw new ArgumentNullException(nameof(loadConfiguration));
            this.log = log ?? (_ => { });
        }

        public Window Owner { get; set; }
        public bool MultipleSaveSpots { get; }
        public bool PortableAllowed => dataLocationRouter.PortableAllowed;
        public ControlServiceDeviceOptions DeviceOptions => Global.DeviceOptions;

        public bool ApplyDataLocation(FirstRunDataLocation location,
            bool keepExistingSettings) => dataLocationRouter.Apply(location,
                MultipleSaveSpots, keepExistingSettings);

        public bool PrepareConfiguration() => prepareConfiguration();

        public ImportPlan FindImportPlan(bool appDataConfigPristine)
        {
            string target = Global.appdatapath;
            if (!appDataConfigPristine || string.IsNullOrEmpty(target) ||
                !string.Equals(target, Global.appDataPpath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var planner = new ImportPlanner();
            if (planner.WasOfferDeclined(target))
            {
                return null;
            }

            ImportPlan plan = planner.CreatePlan(
                ImportPlanner.DefaultSourceDirectory(), target);
            return plan.IsEmpty ? null : plan;
        }

        public bool OfferImport(ImportPlan plan)
        {
            if (plan == null || plan.IsEmpty)
            {
                return false;
            }

            log($"Importable {ImportPlanner.LegacySourceFolderName} " +
                $"configuration found: {plan.Items.Count} files, " +
                $"{plan.ProfileCount} profiles, {plan.CollisionCount} " +
                "already present");

            var dialog = new ImportSettingsDialog(plan)
            {
                Owner = Owner,
            };
            dialog.ShowDialog();

            var planner = new ImportPlanner();
            if (!dialog.ImportRequested)
            {
                RecordDecline(planner, plan.TargetDirectory);
                return false;
            }

            ImportResult result = new ImportExecutor().Execute(plan);
            log($"Settings import finished: {result.CopiedCount} copied, " +
                $"{result.SkippedCount} already present, " +
                $"{result.FailedCount} failed");
            foreach (ImportItemResult failure in result.Failures)
            {
                log("Settings import could not copy " +
                    $"{failure.Item.RelativePath}: {failure.FailureMessage}");
            }

            if (result.AnyFailed)
            {
                MessageBox.Show(Owner,
                    string.Format(CultureInfo.CurrentCulture,
                        Translations.Strings.Import_PartialFailureText,
                        result.CopiedCount, plan.Items.Count,
                        result.FailedCount,
                        ImportPlanner.LegacySourceFolderName),
                    ProductInfo.ProductName, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return !planner.IsTargetPristine(plan.TargetDirectory);
        }

        public void DeclineImport(ImportPlan plan)
        {
            if (plan != null)
            {
                RecordDecline(new ImportPlanner(), plan.TargetDirectory);
            }
        }

        public bool LoadConfiguration() => loadConfiguration();

        public void SaveConfiguration() => Global.Save();

        public ViiperPrerequisiteStatus ReadViiperStatus(bool refreshDriver)
        {
            if (refreshDriver)
            {
                ViiperSetupManager.RefreshDriverReadiness();
            }

            // Read-only status: do not start the server just to populate the
            // first-run card or to imply controller detection is live.
            return ViiperSetupManager.GetStatus(tryStartServer: false);
        }

        public bool LaunchViiperInstaller(ViiperPrerequisiteStatus status) =>
            ViiperSetupManager.LaunchInstaller(status, Owner);

        private void RecordDecline(ImportPlanner planner, string target)
        {
            bool recorded = planner.RecordOfferDeclined(target);
            log("Settings import declined. Decline marker " +
                (recorded
                    ? "written."
                    : "could not be written; the offer will repeat."));
        }
    }

    internal static class SettingsImportWorkflow
    {
        public static void Run(Window owner)
        {
            string target = Global.appdatapath;
            ImportPlan plan = new ImportPlanner().CreatePlan(
                ImportPlanner.DefaultSourceDirectory(), target);
            if (plan.IsEmpty || plan.Items.All(item => item.TargetExists))
            {
                MessageBox.Show(owner,
                    $"No {ImportPlanner.LegacySourceFolderName} settings " +
                    "were found that are missing from this data folder.",
                    "Import settings", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new ImportSettingsDialog(plan)
            {
                Owner = owner,
            };
            dialog.ShowDialog();
            if (!dialog.ImportRequested)
            {
                return;
            }

            ImportResult result = new ImportExecutor().Execute(plan);
            string message = result.AnyFailed
                ? $"Imported {result.CopiedCount} item(s); " +
                  $"{result.FailedCount} failed. Restart Thrum to load what " +
                  "was imported. Existing files were not overwritten."
                : $"Imported {result.CopiedCount} item(s). Restart Thrum to " +
                  "load them. Existing files were not overwritten.";
            MessageBox.Show(owner, message, "Import settings",
                MessageBoxButton.OK,
                result.AnyFailed ? MessageBoxImage.Warning :
                    MessageBoxImage.Information);
        }
    }
}

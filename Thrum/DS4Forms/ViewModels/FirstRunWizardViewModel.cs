using DS4Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public enum FirstRunStepKind
    {
        Welcome,
        DataLocation,
        Import,
        DeviceOptions,
        BackendAndDriver,
        Controllers,
        Finish,
    }

    internal interface IFirstRunWizardEffects
    {
        bool MultipleSaveSpots { get; }
        bool PortableAllowed { get; }
        bool ApplyDataLocation(FirstRunDataLocation location,
            bool keepExistingSettings);
        bool PrepareConfiguration();
        ImportPlan FindImportPlan(bool appDataConfigPristine);
        bool OfferImport(ImportPlan plan);
        void DeclineImport(ImportPlan plan);
        bool LoadConfiguration();
        ControlServiceDeviceOptions DeviceOptions { get; }
        void SaveConfiguration();
        ViiperPrerequisiteStatus ReadViiperStatus(bool refreshDriver);
        bool LaunchViiperInstaller(ViiperPrerequisiteStatus status);
    }

    public abstract class FirstRunStepViewModel : INotifyPropertyChanged
    {
        protected FirstRunStepViewModel(FirstRunStepKind kind, string title,
            string progressText)
        {
            Kind = kind;
            Title = title;
            ProgressText = progressText;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public FirstRunStepKind Kind { get; }
        public string Title { get; }
        public string ProgressText { get; }
        public virtual bool CanAdvance => true;

        protected void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public sealed class FirstRunWelcomeStepViewModel : FirstRunStepViewModel
    {
        internal FirstRunWelcomeStepViewModel()
            : base(FirstRunStepKind.Welcome, "Welcome to Thrum",
                "Step 1 of 7")
        {
        }
    }

    public sealed class FirstRunDataLocationStepViewModel :
        FirstRunStepViewModel
    {
        private readonly IFirstRunWizardEffects effects;
        private FirstRunDataLocation selectedLocation =
            FirstRunDataLocation.AppData;
        private bool keepExistingSettings;

        internal FirstRunDataLocationStepViewModel(
            IFirstRunWizardEffects effects)
            : base(FirstRunStepKind.DataLocation,
                "Choose where Thrum stores its data", "Step 2 of 7")
        {
            this.effects = effects;
        }

        public FirstRunDataLocation SelectedLocation
        {
            get => selectedLocation;
            set
            {
                if (value == FirstRunDataLocation.Portable &&
                    !PortableAllowed)
                {
                    return;
                }

                if (selectedLocation == value)
                {
                    return;
                }

                selectedLocation = value;
                RaiseAllChanged();
            }
        }

        public bool IsAppDataSelected
        {
            get => selectedLocation == FirstRunDataLocation.AppData;
            set
            {
                if (value)
                {
                    SelectedLocation = FirstRunDataLocation.AppData;
                }
            }
        }

        public bool IsPortableSelected
        {
            get => selectedLocation == FirstRunDataLocation.Portable;
            set
            {
                if (value)
                {
                    SelectedLocation = FirstRunDataLocation.Portable;
                }
            }
        }

        public bool PortableAllowed => effects.PortableAllowed;
        public bool PortableUnavailable => !PortableAllowed;
        public bool ShowExistingSettingsChoice => effects.MultipleSaveSpots;

        public bool KeepExistingSettings
        {
            get => keepExistingSettings;
            set
            {
                if (keepExistingSettings == value)
                {
                    return;
                }

                keepExistingSettings = value;
                RaiseAllChanged();
            }
        }

        public override bool CanAdvance =>
            selectedLocation != FirstRunDataLocation.Portable ||
            PortableAllowed;

        internal bool Commit() => effects.ApplyDataLocation(selectedLocation,
            keepExistingSettings);
    }

    public sealed class FirstRunImportStepViewModel : FirstRunStepViewModel
    {
        private readonly IFirstRunWizardEffects effects;

        internal FirstRunImportStepViewModel(IFirstRunWizardEffects effects,
            ImportPlan plan)
            : base(FirstRunStepKind.Import, "Import existing settings",
                "Step 3 of 7")
        {
            this.effects = effects;
            Plan = plan;
            SummaryLines = ImportPlanSummary.Describe(plan);
        }

        public ImportPlan Plan { get; }
        public IReadOnlyList<string> SummaryLines { get; }

        /// <summary>
        /// Invokes the existing ImportSettingsDialog and ImportExecutor path.
        /// True means something landed and the old default bootstrap must not
        /// overwrite the imported configuration.
        /// </summary>
        internal bool Offer() => effects.OfferImport(Plan);

        internal void Decline() => effects.DeclineImport(Plan);
    }

    public sealed class FirstRunDeviceOptionsStepViewModel :
        FirstRunStepViewModel
    {
        private readonly IFirstRunWizardEffects effects;

        internal FirstRunDeviceOptionsStepViewModel(
            IFirstRunWizardEffects effects)
            : base(FirstRunStepKind.DeviceOptions,
                "Choose supported controller types", "Step 4 of 7")
        {
            this.effects = effects;
            Options = new FirstLauchUtilViewModel(effects.DeviceOptions);
        }

        public FirstLauchUtilViewModel Options { get; }

        internal void Commit() => effects.SaveConfiguration();
    }

    public sealed class FirstRunBackendStepViewModel : FirstRunStepViewModel
    {
        private readonly IFirstRunWizardEffects effects;
        private ViiperPrerequisiteStatus status;

        internal FirstRunBackendStepViewModel(IFirstRunWizardEffects effects)
            : base(FirstRunStepKind.BackendAndDriver,
                "Backend and driver status", "Step 5 of 7")
        {
            this.effects = effects;
            DriverStatus = new ViiperDriverStatusViewModel();
        }

        public ViiperDriverStatusViewModel DriverStatus { get; }
        public string StatusText => status?.DisplayText ?? "Status unavailable";

        public string ComponentText => status == null
            ? "Thrum could not read the VIIPER prerequisites."
            : status.ComponentSummary;

        public bool SetupAvailable => status?.SetupScriptFound == true;

        public string SetupButtonText => status?.Ready == true
            ? "VIIPER is ready"
            : "Install / Repair VIIPER";

        public void Refresh(bool refreshDriver = true)
        {
            status = effects.ReadViiperStatus(refreshDriver);
            if (status?.DriverReadiness != null)
            {
                DriverStatus.Apply(status.DriverReadiness);
            }

            RaiseAllChanged();
        }

        internal void EnsureLoaded()
        {
            if (status == null)
            {
                Refresh(refreshDriver: false);
            }
        }

        public void InstallOrRepair()
        {
            if (status?.Ready == true)
            {
                Refresh(refreshDriver: false);
                return;
            }

            effects.LaunchViiperInstaller(status);
            Refresh(refreshDriver: true);
        }
    }

    public sealed class FirstRunControllersStepViewModel :
        FirstRunStepViewModel
    {
        internal FirstRunControllersStepViewModel()
            : base(FirstRunStepKind.Controllers,
                "Connect a controller", "Step 6 of 7")
        {
        }
    }

    public sealed class FirstRunFinishStepViewModel : FirstRunStepViewModel
    {
        internal FirstRunFinishStepViewModel()
            : base(FirstRunStepKind.Finish, "You are ready to start",
                "Step 7 of 7")
        {
        }
    }

    public enum FirstRunAdvanceResult
    {
        Stayed,
        Advanced,
        Finished,
        Fatal,
    }

    /// <summary>
    /// Owns navigation and every startup-side effect. The Window only forwards
    /// keyboard/button actions and closes when this model says startup may
    /// continue or must stop.
    /// </summary>
    public sealed class FirstRunWizardViewModel : INotifyPropertyChanged
    {
        private readonly IFirstRunWizardEffects effects;
        private readonly bool appDataConfigPristine;
        private readonly List<FirstRunStepViewModel> steps = new();
        private int currentIndex;
        private bool dataLocationCommitted;
        private bool configurationPrepared;
        private bool configurationLoaded;
        private bool deviceOptionsSaved;
        private bool importHandled;
        private FirstRunImportStepViewModel importStep;

        internal FirstRunWizardViewModel(IFirstRunWizardEffects effects,
            bool appDataConfigPristine)
        {
            this.effects = effects ??
                throw new ArgumentNullException(nameof(effects));
            this.appDataConfigPristine = appDataConfigPristine;

            steps.Add(new FirstRunWelcomeStepViewModel());
            steps.Add(new FirstRunDataLocationStepViewModel(effects));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IReadOnlyList<FirstRunStepViewModel> Steps => steps;
        public FirstRunStepViewModel CurrentStep => steps[currentIndex];
        public FirstRunStepKind CurrentStepKind => CurrentStep.Kind;
        public bool CanAdvance => !IsComplete && CurrentStep.CanAdvance;

        public bool CanGoBack => !dataLocationCommitted
            ? currentIndex > 0
            : CurrentStepKind == FirstRunStepKind.Controllers ||
              CurrentStepKind == FirstRunStepKind.Finish;

        public string NextButtonText => CurrentStepKind == FirstRunStepKind.Finish
            ? "Finish"
            : "Next";

        public string CancelButtonText => dataLocationCommitted
            ? "Finish later"
            : "Cancel setup";

        public bool FirstRunAfterImport { get; private set; } = true;
        public bool ReadAppConfig { get; private set; }
        public bool CanContinueStartup { get; private set; }
        public bool IsComplete { get; private set; }
        public int ExitCode { get; private set; }

        public FirstRunAdvanceResult Advance()
        {
            if (!CanAdvance)
            {
                return FirstRunAdvanceResult.Stayed;
            }

            switch (CurrentStepKind)
            {
                case FirstRunStepKind.Welcome:
                    return MoveToIndex(1);

                case FirstRunStepKind.DataLocation:
                    return CommitDataLocation();

                case FirstRunStepKind.Import:
                    return CommitImport();

                case FirstRunStepKind.DeviceOptions:
                    ((FirstRunDeviceOptionsStepViewModel)CurrentStep).Commit();
                    deviceOptionsSaved = true;
                    return MoveToNext();

                case FirstRunStepKind.BackendAndDriver:
                case FirstRunStepKind.Controllers:
                    return MoveToNext();

                case FirstRunStepKind.Finish:
                    IsComplete = true;
                    CanContinueStartup = true;
                    RaiseAllChanged();
                    return FirstRunAdvanceResult.Finished;

                default:
                    return FirstRunAdvanceResult.Stayed;
            }
        }

        public void GoBack()
        {
            if (!CanGoBack)
            {
                return;
            }

            currentIndex--;
            ActivateCurrentStep();
            RaiseAllChanged();
        }

        /// <summary>
        /// Before the data choice, cancel is SaveWhere's no-choice shutdown.
        /// Afterwards it finishes only the ordinary import-decline/load/device
        /// save work the old chain would also have performed, then lets startup
        /// continue so no new half-configured terminal state is introduced.
        /// </summary>
        public void Cancel()
        {
            if (IsComplete)
            {
                return;
            }

            if (!dataLocationCommitted || !configurationPrepared)
            {
                IsComplete = true;
                CanContinueStartup = false;
                RaiseAllChanged();
                return;
            }

            if (importStep != null && !importHandled)
            {
                importStep.Decline();
                importHandled = true;
            }

            EnsureConfigurationLoaded();
            if (FirstRunAfterImport && !deviceOptionsSaved)
            {
                effects.SaveConfiguration();
                deviceOptionsSaved = true;
            }

            IsComplete = true;
            CanContinueStartup = true;
            RaiseAllChanged();
        }

        private FirstRunAdvanceResult CommitDataLocation()
        {
            var dataStep = (FirstRunDataLocationStepViewModel)CurrentStep;
            if (!dataStep.Commit())
            {
                return FirstRunAdvanceResult.Stayed;
            }

            dataLocationCommitted = true;
            if (!effects.PrepareConfiguration())
            {
                IsComplete = true;
                CanContinueStartup = false;
                ExitCode = 1;
                RaiseAllChanged();
                return FirstRunAdvanceResult.Fatal;
            }

            configurationPrepared = true;
            // Keep both non-negotiable skip gates in the navigation model as
            // well as the production planner: a non-pristine target or a
            // portable choice must never grow an Import step.
            ImportPlan plan = appDataConfigPristine &&
                dataStep.SelectedLocation == FirstRunDataLocation.AppData
                    ? effects.FindImportPlan(appDataConfigPristine)
                    : null;
            if (plan != null && !plan.IsEmpty)
            {
                importStep = new FirstRunImportStepViewModel(effects, plan);
                steps.Add(importStep);
                currentIndex = steps.Count - 1;
                RaiseAllChanged();
                return FirstRunAdvanceResult.Advanced;
            }

            EnsureConfigurationLoaded();
            AddPostLoadSteps(includeDeviceOptions: true);
            return FirstRunAdvanceResult.Advanced;
        }

        private FirstRunAdvanceResult CommitImport()
        {
            bool importedConfiguration = importStep.Offer();
            importHandled = true;
            FirstRunAfterImport = !importedConfiguration;
            EnsureConfigurationLoaded();

            // The old chain clears firstRun after an import specifically so a
            // later Global.Save/default bootstrap cannot rewrite the imported
            // Profiles.xml. Preserve that by skipping the device-save step.
            AddPostLoadSteps(includeDeviceOptions: FirstRunAfterImport);
            return FirstRunAdvanceResult.Advanced;
        }

        private void EnsureConfigurationLoaded()
        {
            if (configurationLoaded)
            {
                return;
            }

            ReadAppConfig = effects.LoadConfiguration();
            configurationLoaded = true;
        }

        private void AddPostLoadSteps(bool includeDeviceOptions)
        {
            int firstNewStep = steps.Count;
            if (includeDeviceOptions)
            {
                steps.Add(new FirstRunDeviceOptionsStepViewModel(effects));
            }

            steps.Add(new FirstRunBackendStepViewModel(effects));
            steps.Add(new FirstRunControllersStepViewModel());
            steps.Add(new FirstRunFinishStepViewModel());
            currentIndex = firstNewStep;
            ActivateCurrentStep();
            RaiseAllChanged();
        }

        private FirstRunAdvanceResult MoveToNext()
        {
            if (currentIndex >= steps.Count - 1)
            {
                return FirstRunAdvanceResult.Stayed;
            }

            currentIndex++;
            ActivateCurrentStep();
            RaiseAllChanged();
            return FirstRunAdvanceResult.Advanced;
        }

        private FirstRunAdvanceResult MoveToIndex(int index)
        {
            currentIndex = index;
            RaiseAllChanged();
            return FirstRunAdvanceResult.Advanced;
        }

        private void ActivateCurrentStep()
        {
            if (CurrentStep is FirstRunBackendStepViewModel backend)
            {
                backend.EnsureLoaded();
            }
        }

        private void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

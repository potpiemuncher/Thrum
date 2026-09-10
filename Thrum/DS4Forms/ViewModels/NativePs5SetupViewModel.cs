/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using DS4Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>What the sheet's primary button does at the current step.</summary>
    public enum NativePs5SetupAction
    {
        Continue,
        TurnOn,
        Done,
    }

    /// <summary>One row of the sheet's progress rail.</summary>
    public sealed class NativePs5SetupRailStep
    {
        public NativePs5SetupRailStep(int number, string title, string subtitle,
            bool isCurrent, bool isDone, bool isOptional)
        {
            Number = number;
            Title = title;
            Subtitle = subtitle;
            IsCurrent = isCurrent;
            IsDone = isDone;
            IsOptional = isOptional;
        }

        public int Number { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public bool IsCurrent { get; }
        public bool IsDone { get; }
        public bool IsOptional { get; }

        /// <summary>"✓" once the step is satisfied, else the step number.</summary>
        public string Glyph => IsDone ? "✓" : Number.ToString();

        /// <summary>Ring treatment token: Done, Current or Idle.</summary>
        public string RingKind => IsDone ? "Done" : IsCurrent ? "Current" : "Idle";

        /// <summary>
        /// UI Automation reads ToString() for templated items; a rail row
        /// must announce its step, not its type name.
        /// </summary>
        public override string ToString() =>
            "Step " + Number + ", " + Title +
            (IsDone ? ", done" : IsCurrent ? ", current" : string.Empty) +
            (IsOptional ? ", optional" : string.Empty);
    }

    /// <summary>The facts the sheet's steps are computed from.</summary>
    public sealed record NativePs5SetupInputs(
        bool DriverKnownPackage,
        bool BackendReady,
        bool Acknowledged,
        bool IsOn,
        bool AudioEndpointsAllowed,
        bool HasDualSense,
        bool IsWireless,
        string NativeDeviceName,
        string ProfileName,
        string TransportText)
    {
        public static readonly NativePs5SetupInputs None = new(false, false,
            false, false, false, false, false, "DualSense", string.Empty,
            "Not connected");
    }

    /// <summary>
    /// N2 in the design handoff: the four-step Native PS5 mode setup sheet.
    ///
    /// <para>Holds step position and derives every label, hint and enablement
    /// from a <see cref="NativePs5SetupInputs"/> the window supplies. It runs
    /// no command itself: install, re-check, consent and turn-on are the
    /// existing handlers, invoked by the view, and each one writes its own
    /// setting immediately - which is why closing the sheet loses nothing and
    /// reopening it lands on the first step that is still unsatisfied.</para>
    /// </summary>
    public sealed class NativePs5SetupViewModel : INotifyPropertyChanged
    {
        public const int InstallStep = 1;
        public const int ConsentStep = 2;
        public const int TurnOnStep = 3;
        public const int HapticsStep = 4;

        /// <summary>
        /// Shown verbatim when the user declines the elevation prompt; the
        /// same sentence <see cref="ViiperSetupManager"/> reports.
        /// </summary>
        public const string UacCancelledMessage =
            ViiperSetupManager.InstallerCancelledAtUacMessage;

        public const string ElevationPendingMessage =
            "Windows is asking for administrator permission…";

        /// <summary>N4: static copy, mapped to nothing in code.</summary>
        public const string AudioDefaultTakeoverWarningTitle =
            "Windows will change your default audio device";

        public static readonly string AudioDefaultTakeoverWarningText =
            "When a virtual DualSense attaches with audio endpoints, Windows " +
            "makes it the default for all six playback and recording roles. " +
            ProductInfo.ProductName + " cannot prevent this yet.";

        /// <summary>N5: rendered disabled with this reason.</summary>
        public const string RestoreDefaultDeviceUnavailableReason =
            "Not available in this build";

        private NativePs5SetupInputs inputs = NativePs5SetupInputs.None;
        private int currentStep = InstallStep;
        private bool installing;
        private string installMessage = string.Empty;

        public NativePs5SetupViewModel()
            : this(null)
        {
        }

        /// <param name="driverStatus">
        /// The Settings driver-status card model, shared so step 1 shows the
        /// same badge, headline and identity lists the card does.
        /// </param>
        public NativePs5SetupViewModel(ViiperDriverStatusViewModel driverStatus)
        {
            DriverStatus = driverStatus ?? new ViiperDriverStatusViewModel();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ViiperDriverStatusViewModel DriverStatus { get; }

        public NativePs5SetupInputs Inputs => inputs;

        public string Title => "Set up Native PS5 mode";

        public string Subtitle =>
            "Three steps. Each saves on its own; you can close and come back.";

        /// <summary>Step 1 is satisfied only by a known package with a running backend.</summary>
        public bool DriverReady => inputs.DriverKnownPackage && inputs.BackendReady;

        /// <summary>The first unsatisfied step, which is where the sheet opens.</summary>
        public int EntryStep => !DriverReady ? InstallStep :
            !inputs.Acknowledged ? ConsentStep : TurnOnStep;

        public int CurrentStep => currentStep;

        /// <summary>Opens at the first unsatisfied step and clears any install message.</summary>
        public void Open(NativePs5SetupInputs value)
        {
            inputs = value ?? NativePs5SetupInputs.None;
            currentStep = EntryStep;
            installMessage = string.Empty;
            RaiseAllChanged();
        }

        /// <summary>Republishes new facts without moving the step.</summary>
        public void Apply(NativePs5SetupInputs value)
        {
            inputs = value ?? NativePs5SetupInputs.None;
            RaiseAllChanged();
        }

        public bool IsStep1 => currentStep == InstallStep;
        public bool IsStep2 => currentStep == ConsentStep;
        public bool IsStep3 => currentStep == TurnOnStep;
        public bool IsStep4 => currentStep == HapticsStep;

        public string StepAccessibleName =>
            "Step " + currentStep + " of " + HapticsStep + ": " +
            StepTitle(currentStep);

        private static string StepTitle(int step) => step switch
        {
            InstallStep => "Install backend",
            ConsentStep => "Read and accept",
            TurnOnStep => "Turn on",
            _ => "Haptics over the virtual pad",
        };

        public IReadOnlyList<NativePs5SetupRailStep> RailSteps => new[]
        {
            new NativePs5SetupRailStep(InstallStep, StepTitle(InstallStep),
                "VIIPER + usbip-win2", currentStep == InstallStep,
                DriverReady, false),
            new NativePs5SetupRailStep(ConsentStep, StepTitle(ConsentStep),
                "Experimental driver", currentStep == ConsentStep,
                inputs.Acknowledged, false),
            new NativePs5SetupRailStep(TurnOnStep, StepTitle(TurnOnStep),
                "Games see a " + inputs.NativeDeviceName,
                currentStep == TurnOnStep, inputs.IsOn, false),
            new NativePs5SetupRailStep(HapticsStep, StepTitle(HapticsStep),
                "Optional", currentStep == HapticsStep,
                inputs.AudioEndpointsAllowed, true),
        };

        // ---- step 1 ----

        public string Step1Title =>
            "Install the VIIPER backend and the usbip-win2 driver";

        public bool IsInstalling => installing;

        public bool CanInstall => !installing;

        public string InstallLabel => installing
            ? "Installing…"
            : inputs.DriverKnownPackage ? "Repair" : "Install / Repair VIIPER";

        public string InstallMessage => installMessage;

        public bool HasInstallMessage => !string.IsNullOrEmpty(installMessage);

        /// <summary>
        /// Shown when the package is fine but the backend is not answering:
        /// the only case where step 1 is unsatisfied for a non-driver reason.
        /// </summary>
        public bool ShowBackendHint => inputs.DriverKnownPackage && !inputs.BackendReady;

        public string BackendHint =>
            "The driver is a known package, but the VIIPER backend is not " +
            "running. Use Repair, then Recheck.";

        /// <summary>Never softened: a match is evidence, not approval.</summary>
        public string NotApprovedNote =>
            ViiperDriverStatusViewModel.NotProductionApprovedNote;

        public string InstallLogText =>
            "Log: " + ViiperSetupManager.InstallLogPath;

        public void SetInstalling(bool value)
        {
            installing = value;
            RaiseAllChanged();
        }

        public void SetInstallMessage(string message)
        {
            installMessage = message ?? string.Empty;
            RaiseAllChanged();
        }

        // ---- step 2 ----

        public string Step2Title => "This uses an experimental kernel driver";

        public string Step2Intro =>
            "Virtual controllers are created by usbip-win2, a kernel-mode " +
            "driver that is not production-approved. Read the notes below " +
            "before continuing.";

        /// <summary>The full disclosure, as shipped in code. Not paraphrased.</summary>
        public string AcknowledgementBody =>
            ViiperExperimentalDisclosure.AcknowledgementBody;

        public bool Acknowledged => inputs.Acknowledged;

        public string AcknowledgementLabel =>
            "I have read this and accept that virtual controllers run on an " +
            "experimental kernel driver";

        public string AcknowledgementNote => inputs.Acknowledged
            ? "Saved. Continue is now available."
            : "Saved immediately to settings as \"Use virtual controllers " +
              "(experimental kernel driver)\". Audio and microphone endpoints " +
              "are a separate, later choice.";

        public string AcknowledgementNoteKind =>
            inputs.Acknowledged ? "Success" : "Muted";

        // ---- step 3 ----

        public bool NotOnYet => !inputs.IsOn;

        /// <summary>
        /// Steps 1 and 2 need no controller; step 3 does. Said out loud
        /// rather than leaving a disabled button unexplained.
        /// </summary>
        public bool WaitingForDualSense => !inputs.HasDualSense;

        public string WaitingForDualSenseText =>
            "Connect a DualSense or DualSense Edge to turn Native PS5 mode " +
            "on. Steps 1 and 2 are already saved.";

        public string Step3Title => "Turn on Native PS5 mode";

        public string Step3OnTitle => "Native PS5 mode is on";

        public string PadSummary => inputs.NativeDeviceName + " · " +
            inputs.TransportText + " · profile";

        public string ProfileName => inputs.ProfileName;

        public string Step3AppliesTo => "Applies to this profile.";

        public IReadOnlyList<string> Step3Bullets => new[]
        {
            "Emulated device → " + inputs.NativeDeviceName,
            "Hide DS4 Controller → on",
            "Games see " + inputs.NativeDeviceName +
                " (virtual) on the next connection",
        };

        public string GamesSeeOn => inputs.NativeDeviceName + " (virtual)";

        public string Step3HapticsNote => inputs.IsWireless
            ? "Haptics already work over Bluetooth with Audio Haptics on. " +
              "Step 4 is only for USB, and it is experimental."
            : "On USB, Audio Haptics needs the optional step 4, which is " +
              "experimental.";

        // ---- step 4 ----

        public string Step4Title => "Haptics over the virtual pad";

        public string Step4Badge => "Experimental, unverified";

        public string Step4Intro =>
            "Lets games drive the pad's own haptics and speaker through the " +
            "virtual pad, over Bluetooth or USB, the way a pad plugged " +
            "straight into a PS5 or PC is driven. Without it, games get " +
            "adaptive triggers and rumble-style haptics only, and Audio " +
            "Haptics can still add haptics from system audio over Bluetooth. " +
            "This needs the virtual audio endpoints consent below.";

        public bool AudioEndpointsAllowed => inputs.AudioEndpointsAllowed;

        public string AudioConsentLabel =>
            "Allow virtual audio and microphone endpoints";

        public string AudioConsentNote =>
            "Off by default. Ticking it opens the risk disclosure every " +
            "time; it is saved only after you accept there.";

        public string AudioClassSummary =>
            ViiperExperimentalDisclosure.AudioClassSummary;

        // ---- footer ----

        public bool CanBack => currentStep > InstallStep;

        public bool ShowStep4Link => currentStep == TurnOnStep && inputs.IsOn;

        public string Step4LinkLabel => "Haptics over the virtual pad…";

        public NativePs5SetupAction PrimaryAction => currentStep switch
        {
            TurnOnStep => inputs.IsOn ? NativePs5SetupAction.Done
                : NativePs5SetupAction.TurnOn,
            HapticsStep => NativePs5SetupAction.Done,
            _ => NativePs5SetupAction.Continue,
        };

        public string PrimaryLabel => PrimaryAction switch
        {
            NativePs5SetupAction.TurnOn => "Turn on",
            NativePs5SetupAction.Done => "Done",
            _ => "Continue",
        };

        /// <summary>Whether the step's own requirement is satisfied.</summary>
        public bool CanAdvance(int step) => step switch
        {
            InstallStep => DriverReady,
            ConsentStep => inputs.Acknowledged,
            TurnOnStep => inputs.IsOn || inputs.HasDualSense,
            _ => true,
        };

        public bool PrimaryEnabled => !installing && CanAdvance(currentStep);

        /// <summary>Every disabled control carries its reason.</summary>
        public string PrimaryToolTip
        {
            get
            {
                if (PrimaryEnabled)
                {
                    return string.Empty;
                }

                switch (currentStep)
                {
                    case InstallStep:
                        if (installing)
                        {
                            return "Waiting for VIIPER setup to finish";
                        }

                        if (!inputs.DriverKnownPackage)
                        {
                            return inputs.BackendReady
                                ? ProductInfo.ProductName +
                                  " does not proceed on an unverified package"
                                : "The driver is not installed yet";
                        }

                        return "The VIIPER backend is not running";
                    case ConsentStep:
                        return "Tick the box to continue";
                    case TurnOnStep:
                        return "Connect a DualSense first";
                    default:
                        return string.Empty;
                }
            }
        }

        public bool HasPrimaryToolTip => !string.IsNullOrEmpty(PrimaryToolTip);

        public string FooterHint => currentStep switch
        {
            InstallStep => "Continue is enabled once the driver reads back " +
                "as a known package and the backend is running.",
            ConsentStep => inputs.Acknowledged
                ? "Saved."
                : "Nothing is pre-checked. Tick the box to continue.",
            TurnOnStep => inputs.IsOn ? string.Empty
                : "Applies to profile " + inputs.ProfileName + ".",
            _ => "Optional. Leave the box off to keep Bluetooth haptics only.",
        };

        /// <summary>
        /// Continue: moves forward one step when the current one is
        /// satisfied. Returns false when the primary is not a Continue or the
        /// step is not satisfied, so the view never skips a gate.
        /// </summary>
        public bool Advance()
        {
            if (PrimaryAction != NativePs5SetupAction.Continue ||
                !CanAdvance(currentStep) || currentStep >= HapticsStep)
            {
                return false;
            }

            currentStep++;
            RaiseAllChanged();
            return true;
        }

        public void Back()
        {
            if (currentStep <= InstallStep)
            {
                return;
            }

            currentStep--;
            RaiseAllChanged();
        }

        public void GoToStep(int step)
        {
            if (step < InstallStep || step > HapticsStep || step == currentStep)
            {
                return;
            }

            currentStep = step;
            RaiseAllChanged();
        }

        private void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

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
using System.ComponentModel;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// The six states of the Overview "Native PS5 mode" card. Numbered to
    /// match the design handoff's N1 state table.
    /// </summary>
    public enum NativePs5ModeState
    {
        /// <summary>Prerequisites met, output is not a virtual DualSense.</summary>
        Off = 1,

        /// <summary>
        /// Driver missing, unverified or not yet checked, or the backend is not
        /// running. Nothing can be created; setup opens at the install step.
        /// </summary>
        NeedsSetup = 2,

        /// <summary>Known package, experimental acknowledgement not given.</summary>
        NeedsConsent = 3,

        /// <summary>Output is a virtual DualSense; haptics are game rumble.</summary>
        On = 4,

        /// <summary>
        /// On, physical link is Bluetooth, Audio Haptics is enabled with a
        /// source the Bluetooth streamer serves, and no virtual audio
        /// endpoint is involved. The safe default configuration: green.
        /// </summary>
        OnHapticsBluetooth = 5,

        /// <summary>
        /// On with virtual audio endpoints allowed, over Bluetooth or USB:
        /// games drive the pad's own haptics and speaker through the virtual
        /// pad. The only state that reaches the audio-class driver path;
        /// exercised on hardware over Bluetooth on usbip-win2 0.9.8.0
        /// (2026-09-09) but not yet at length, so still labelled unverified.
        /// </summary>
        OnHapticsVirtualPad = 6,
    }

    /// <summary>Whether the physical pad is hidden from games right now.</summary>
    public enum NativePs5HidHideStatus
    {
        /// <summary>HidHide (or exclusive access) is hiding the physical pad.</summary>
        Hiding,

        /// <summary>No HidHide control device on this machine.</summary>
        NotInstalled,

        /// <summary>HidHide is installed but this pad reads back as shared.</summary>
        NotHidingThisPad,
    }

    /// <summary>
    /// Everything the card's state is a function of. A record so the view
    /// model can skip republishing when nothing moved.
    /// </summary>
    public sealed record NativePs5ModeInputs(
        bool HasDualSense,
        bool PhysicalIsEdge,
        OutContType OutputType,
        ViiperDriverReadinessState? DriverState,
        bool BackendReady,
        bool ExperimentalAcknowledged,
        bool AudioEndpointsAllowed,
        bool IsWireless,
        bool AudioHapticsEnabled,
        AudioHapticsSourceKind AudioHapticsSource,
        NativePs5HidHideStatus HidHide)
    {
        public static readonly NativePs5ModeInputs None = new(false, false,
            OutContType.None, null, false, false, false, false, false,
            AudioHapticsSourceKind.SystemAudio, NativePs5HidHideStatus.NotInstalled);
    }

    /// <summary>
    /// N1 in the design handoff: the read-only projection of six settings and
    /// runtime facts into one card. Pure: <see cref="Evaluate"/> reads its
    /// argument and nothing else, so every state is unit-testable without a
    /// controller, a driver or a settings store.
    ///
    /// <para>The rule that shaped the table, from the brief: <b>Bluetooth +
    /// Audio Haptics + no virtual audio endpoints = state 5 = green.</b> That
    /// configuration is a fully working setup and must never read as broken.
    /// The project already shipped and reverted a card that said "Needs
    /// attention" for exactly it and trained users to enable the risky switch
    /// to clear the warning.</para>
    /// </summary>
    public sealed class NativePs5ModeViewModel : INotifyPropertyChanged
    {
        private NativePs5ModeInputs inputs = NativePs5ModeInputs.None;

        public event PropertyChangedEventHandler PropertyChanged;

        public NativePs5ModeInputs Inputs => inputs;

        /// <summary>
        /// Publishes a new set of inputs. Returns false, and raises nothing,
        /// when they equal the current ones - the Overview timer calls this
        /// four times a second.
        /// </summary>
        public bool Apply(NativePs5ModeInputs value)
        {
            value ??= NativePs5ModeInputs.None;
            if (value.Equals(inputs))
            {
                return false;
            }

            inputs = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            return true;
        }

        public static bool IsNativeOutput(OutContType type)
        {
            OutContType normalized = type.Normalize();
            return normalized == OutContType.ViiperDualSense ||
                normalized == OutContType.ViiperDualSenseEdge;
        }

        private static bool DriverIsKnownPackage(ViiperDriverReadinessState? state) =>
            state == ViiperDriverReadinessState.ValidatedExperimental ||
            state == ViiperDriverReadinessState.Approved;

        /// <summary>The N1 state table.</summary>
        public static NativePs5ModeState Evaluate(NativePs5ModeInputs i)
        {
            i ??= NativePs5ModeInputs.None;
            if (!DriverIsKnownPackage(i.DriverState) || !i.BackendReady)
            {
                return NativePs5ModeState.NeedsSetup;
            }

            if (!i.ExperimentalAcknowledged)
            {
                return NativePs5ModeState.NeedsConsent;
            }

            if (!IsNativeOutput(i.OutputType))
            {
                return NativePs5ModeState.Off;
            }

            if (i.AudioEndpointsAllowed)
            {
                return NativePs5ModeState.OnHapticsVirtualPad;
            }

            if (i.IsWireless && i.AudioHapticsEnabled &&
                !i.AudioEndpointsAllowed &&
                BluetoothStreamerServes(i.AudioHapticsSource))
            {
                return NativePs5ModeState.OnHapticsBluetooth;
            }

            return NativePs5ModeState.On;
        }

        /// <summary>
        /// The Bluetooth haptics streamer captures a render endpoint, so it
        /// serves the system mix and an explicit endpoint; app-session and
        /// controller-audio sources need a virtual output path.
        /// </summary>
        private static bool BluetoothStreamerServes(AudioHapticsSourceKind source) =>
            source == AudioHapticsSourceKind.SystemAudio ||
            source == AudioHapticsSourceKind.Endpoint;

        public NativePs5ModeState State => Evaluate(inputs);

        /// <summary>Only a DualSense or DualSense Edge shows the card.</summary>
        public bool IsVisible => inputs.HasDualSense;

        /// <summary>
        /// The switch position: what the profile says games should see. Set
        /// independently of the prerequisites on purpose - a profile can name
        /// a DualSense while the driver is missing, and the card then shows
        /// the switch on with the setup badge beside it rather than lying in
        /// either direction.
        /// </summary>
        public bool IsOn => IsNativeOutput(inputs.OutputType);

        public string SwitchLabel => IsOn ? "On" : "Off";

        public string SwitchAccessibleName => "Native PS5 mode, " + SwitchLabel;

        public string SwitchToolTip => IsOn
            ? "Turn off Native PS5 mode. Asks first; applies on the next connection."
            : State == NativePs5ModeState.Off
                ? "Turn on Native PS5 mode for this profile."
                : "Opens setup at the step that is still missing.";

        public string NativeDeviceName =>
            inputs.PhysicalIsEdge ? "DualSense Edge" : "DualSense";

        public OutContType NativeOutputType => inputs.PhysicalIsEdge
            ? OutContType.ViiperDualSenseEdge
            : OutContType.ViiperDualSense;

        public string BadgeText
        {
            get
            {
                switch (State)
                {
                    case NativePs5ModeState.Off:
                        return "Off";
                    case NativePs5ModeState.NeedsSetup:
                        if (inputs.DriverState == null)
                        {
                            return "Checking";
                        }

                        if (inputs.DriverState == ViiperDriverReadinessState.Missing)
                        {
                            return "Not installed";
                        }

                        return DriverIsKnownPackage(inputs.DriverState)
                            ? "Backend not running"
                            : "Unverified";
                    case NativePs5ModeState.NeedsConsent:
                        return "Experimental - known package";
                    case NativePs5ModeState.On:
                        return "On";
                    case NativePs5ModeState.OnHapticsBluetooth:
                        return "On · haptics via Bluetooth";
                    default:
                        return "Experimental, unverified";
                }
            }
        }

        /// <summary>
        /// Badge treatment token for the theme triggers: Muted, Danger, Warning
        /// or Success.
        /// </summary>
        public string BadgeKind
        {
            get
            {
                switch (State)
                {
                    case NativePs5ModeState.NeedsSetup:
                        return inputs.DriverState ==
                            ViiperDriverReadinessState.DetectedUnvalidated
                            ? "Danger"
                            : "Muted";
                    case NativePs5ModeState.NeedsConsent:
                    case NativePs5ModeState.OnHapticsVirtualPad:
                        return "Warning";
                    case NativePs5ModeState.On:
                    case NativePs5ModeState.OnHapticsBluetooth:
                        return "Success";
                    default:
                        return "Muted";
                }
            }
        }

        /// <summary>Card border token: Neutral, Success or Warning.</summary>
        public string CardBorderKind
        {
            get
            {
                switch (State)
                {
                    case NativePs5ModeState.On:
                    case NativePs5ModeState.OnHapticsBluetooth:
                        return "Success";
                    case NativePs5ModeState.OnHapticsVirtualPad:
                        return "Warning";
                    default:
                        return "Neutral";
                }
            }
        }

        /// <summary>The bold part of the "Games see" line.</summary>
        public string GamesSeeText
        {
            get
            {
                if (IsOn)
                {
                    return inputs.OutputType.Normalize().ToDisplayName() +
                        " (virtual)";
                }

                OutContType current = inputs.OutputType.Normalize();
                return current == OutContType.None
                    ? "the physical pad only"
                    : current.ToDisplayName() + " (virtual)";
            }
        }

        public string GamesSeeSuffix
        {
            get
            {
                if (State == NativePs5ModeState.OnHapticsVirtualPad)
                {
                    return " · virtual audio and microphone endpoints on";
                }

                return IsOn && inputs.HidHide == NativePs5HidHideStatus.Hiding
                    ? " · physical pad hidden by HidHide"
                    : string.Empty;
            }
        }

        /// <summary>One explanatory line, in the state's own words.</summary>
        public string PrimaryLine
        {
            get
            {
                string pad = NativeDeviceName;
                switch (State)
                {
                    case NativePs5ModeState.Off:
                        return "Turn on to present this " + pad +
                            " to games as a real " + pad + ". Needs the VIIPER " +
                            "backend, the usbip-win2 driver and a one-time " +
                            "acknowledgement; the switch walks you through " +
                            "whichever is missing.";
                    case NativePs5ModeState.NeedsSetup:
                        if (inputs.DriverState == null)
                        {
                            return "Checking the usbip-win2 driver and the " +
                                "VIIPER backend.";
                        }

                        if (inputs.DriverState ==
                            ViiperDriverReadinessState.DetectedUnvalidated)
                        {
                            return "A usbip-win2 driver is installed, but " +
                                ProductInfo.ProductName + " could not confirm " +
                                "which package it is. No new virtual " +
                                "controller is created while it is in this " +
                                "state.";
                        }

                        if (inputs.DriverState == ViiperDriverReadinessState.Missing)
                        {
                            return "Needs setup. No usbip-win2 driver is " +
                                "installed and the VIIPER backend is not " +
                                "running. Turning this on opens setup at the " +
                                "install step.";
                        }

                        return "The driver is a known package, but the VIIPER " +
                            "backend is not running. Turning this on opens " +
                            "setup at the install step, where Repair " +
                            "restarts it.";
                    case NativePs5ModeState.NeedsConsent:
                        return "The driver package is recognised but not " +
                            "production-approved. Read and accept the " +
                            "disclosure before virtual controllers can be " +
                            "created.";
                    case NativePs5ModeState.On:
                        return "Haptics: game rumble through the virtual pad. " +
                            "Game-authored haptics and speaker need step 4 of " +
                            "setup. " +
                            (inputs.AudioHapticsEnabled
                                ? "Audio Haptics is on."
                                : "Audio Haptics is off.");
                    case NativePs5ModeState.OnHapticsBluetooth:
                        return "Haptics stream straight to the pad over " +
                            "Bluetooth. This route needs no driver and no " +
                            "audio-endpoint consent.";
                    default:
                        return "Games drive the pad's own haptics and speaker " +
                            "through the virtual pad's audio endpoints, " +
                            (inputs.IsWireless
                                ? "relayed over Bluetooth."
                                : "over USB.");
                }
            }
        }

        /// <summary>The muted second line; empty below state 4.</summary>
        public string SecondaryLine => State >= NativePs5ModeState.On
            ? "Turning off applies on the next connection; a virtual pad " +
              "already running is not torn down."
            : string.Empty;

        public bool HasSecondaryLine => !string.IsNullOrEmpty(SecondaryLine);

        public bool ShowHidHideWarning =>
            IsOn && inputs.HidHide != NativePs5HidHideStatus.Hiding;

        public string HidHideWarningText =>
            inputs.HidHide == NativePs5HidHideStatus.NotInstalled
                ? "HidHide is not installed, so games may also see the " +
                  "physical pad."
                : "HidHide is installed but is not hiding this pad, so games " +
                  "may also see it. Make sure Hide DS4 Controller is on and " +
                  ProductInfo.ProductName + " is on the HidHide allow list.";

        public const string HidHideWorksWithoutNote =
            "Native PS5 mode works without it.";

        /// <summary>
        /// State 6 only: says the endpoints are on, that the path is
        /// unverified, and carries the risk sentence verbatim.
        /// </summary>
        public bool ShowAudioEndpointsLine =>
            State == NativePs5ModeState.OnHapticsVirtualPad;

        public const string AudioEndpointsUnverifiedLine =
            "Virtual audio endpoints are on. This path was first exercised " +
            "on hardware over Bluetooth on usbip-win2 0.9.8.0 (2026-09-09) " +
            "and is not yet verified at length (issue #65).";

        public string AudioEndpointsRiskText =>
            ViiperExperimentalDisclosure.AudioClassSummary;

        public bool ShowSetupButton =>
            State == NativePs5ModeState.NeedsSetup ||
            State == NativePs5ModeState.NeedsConsent;

        public string SetupButtonText =>
            State == NativePs5ModeState.NeedsConsent
                ? "Read and accept"
                : "Set up Native PS5 mode";
    }
}

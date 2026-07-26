/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

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

using System;

namespace DS4Windows
{
    /// <summary>
    /// Which risk class a virtual device belongs to.
    ///
    /// <para>The split is not cosmetic and it is not about features: it is the
    /// reachability boundary of the confirmed usbip-win2 kernel defect. The
    /// request-lifetime race is reached only by paths that open and close the
    /// virtual USB <i>audio</i> endpoints. A plain emulated pad — xbox360,
    /// dualshock4, dualsense, dualsenseedge or ns2pro with no audio interface —
    /// never exercises it.</para>
    /// </summary>
    public enum ViiperFeatureClass
    {
        /// <summary>
        /// HID only. No virtual USB audio or microphone interface is created or
        /// opened, so the known race is not reachable.
        /// </summary>
        ControllerOnly,

        /// <summary>
        /// Creates or opens a virtual USB audio/microphone endpoint: the
        /// <c>…audioduplex*</c> and <c>…audioonly*</c> VIIPER device types, the
        /// audio-only sidecar, and the microphone personas. This is the class
        /// the kernel defect lives on.
        /// </summary>
        Audio,
    }

    /// <summary>
    /// Why a virtual device was refused. Callers switch on this to choose the
    /// remedy they offer; the text in <see cref="ViiperVirtualDeviceDecision.Reason"/>
    /// is what a user reads.
    /// </summary>
    public enum ViiperVirtualDeviceBlock
    {
        /// <summary>Not blocked.</summary>
        None,

        /// <summary>No usbip-win2 driver package is installed.</summary>
        DriverMissing,

        /// <summary>
        /// A driver is installed but its identity or trust could not be
        /// established. Fail-closed: unproven is refused.
        /// </summary>
        DriverUnvalidated,

        /// <summary>
        /// The user has not yet acknowledged that virtual controllers depend on
        /// an experimental third-party kernel driver. Remedy: show the
        /// acknowledgement once and retry.
        /// </summary>
        ExperimentalNotAcknowledged,

        /// <summary>
        /// An audio-class device was requested while the audio-class setting is
        /// off. Remedy: the Settings opt-in, which carries the risk disclosure.
        /// Never resolved silently.
        /// </summary>
        AudioClassNotEnabled,
    }

    /// <summary>The answer to "may we create this virtual device now?".</summary>
    public sealed class ViiperVirtualDeviceDecision
    {
        private ViiperVirtualDeviceDecision(bool allowed,
            ViiperVirtualDeviceBlock block, string reason)
        {
            Allowed = allowed;
            Block = block;
            Reason = reason;
        }

        public bool Allowed { get; }

        /// <summary><see cref="ViiperVirtualDeviceBlock.None"/> when allowed.</summary>
        public ViiperVirtualDeviceBlock Block { get; }

        /// <summary>
        /// Plain language, and actionable: it names what is wrong and what the
        /// user can do about it. Logged and shown verbatim, so it never carries
        /// a path, a device instance id or a package the manifest does not list.
        /// </summary>
        public string Reason { get; }

        internal static ViiperVirtualDeviceDecision Allow(string reason) =>
            new ViiperVirtualDeviceDecision(true,
                ViiperVirtualDeviceBlock.None, reason);

        internal static ViiperVirtualDeviceDecision Refuse(
            ViiperVirtualDeviceBlock block, string reason) =>
            new ViiperVirtualDeviceDecision(false, block, reason);
    }

    /// <summary>
    /// The single decision point for "may a new virtual USB device be created".
    ///
    /// <para><b>Pure and total.</b> <see cref="Decide"/> is a function of five
    /// inputs and reads nothing else — no settings store, no clock, no machine.
    /// Every one of the 64 combinations is covered by
    /// <c>ViiperVirtualDeviceGateTests</c>, which is the only way a policy whose
    /// failure mode is "a kernel bugcheck the user cannot attribute to us" can
    /// be reviewed at all.</para>
    ///
    /// <para><b>Two rules shape the table.</b></para>
    /// <list type="number">
    /// <item><b>New allocations only.</b> <c>alreadyAttached</c> short-circuits
    /// to allow, unconditionally and before every other check. An in-flight
    /// session is never taken away by a policy change: pulling a pad out from
    /// under a running game is hostile, and for the audio class the teardown
    /// <i>is</i> the race trigger, so a gate that tore down live audio endpoints
    /// would cause the exact crash it exists to prevent.</item>
    /// <item><b>Fail-closed below a manifest match.</b> Nothing new is created
    /// at <see cref="ViiperDriverReadinessState.Missing"/> or
    /// <see cref="ViiperDriverReadinessState.DetectedUnvalidated"/>, for either
    /// class. Above that, the acknowledgement covers the whole backend and is
    /// therefore checked before the audio opt-in, so a user who has consented to
    /// nothing is told about the driver first rather than about audio.</item>
    /// </list>
    ///
    /// <para>The audio opt-in is required at every state below
    /// <see cref="ViiperDriverReadinessState.Approved"/> — which is every state
    /// reachable today, because the manifest deliberately has no Production
    /// entry. That is what makes <c>Approved</c> mean something: it is the only
    /// state in which audio endpoints stop being an explicit user risk
    /// decision.</para>
    /// </summary>
    public static class ViiperVirtualDeviceGate
    {
        /// <param name="state">Session driver readiness, from the validator.</param>
        /// <param name="featureClass">What the caller intends to create.</param>
        /// <param name="experimentalAcknowledged">
        /// Persisted: the user has been told the backend depends on an
        /// experimental third-party kernel driver.
        /// </param>
        /// <param name="audioClassEnabled">
        /// Persisted, default off: the user has explicitly enabled virtual audio
        /// endpoints after reading the risk disclosure.
        /// </param>
        /// <param name="alreadyAttached">
        /// True when the request concerns a device that is already attached and
        /// running. Such a request is never refused.
        /// </param>
        public static ViiperVirtualDeviceDecision Decide(
            ViiperDriverReadinessState state,
            ViiperFeatureClass featureClass,
            bool experimentalAcknowledged,
            bool audioClassEnabled,
            bool alreadyAttached)
        {
            if (alreadyAttached)
            {
                // Before everything else, deliberately. See the type remarks.
                return ViiperVirtualDeviceDecision.Allow(
                    "this virtual device is already attached, and a running " +
                    "session is never interrupted by a policy change");
            }

            switch (state)
            {
                case ViiperDriverReadinessState.Missing:
                    return ViiperVirtualDeviceDecision.Refuse(
                        ViiperVirtualDeviceBlock.DriverMissing,
                        DriverMissingReason);

                case ViiperDriverReadinessState.DetectedUnvalidated:
                    return ViiperVirtualDeviceDecision.Refuse(
                        ViiperVirtualDeviceBlock.DriverUnvalidated,
                        DriverUnvalidatedReason);

                case ViiperDriverReadinessState.Approved:
                    // A maintainer-accepted release. The experimental
                    // acknowledgement is about an experimental driver, and the
                    // audio opt-in is about a known-risk one; neither statement
                    // is true here, so neither is required.
                    return ViiperVirtualDeviceDecision.Allow(
                        "the installed usbip-win2 package is a release " +
                        ProductInfo.ProductName +
                        " has accepted for production use");

                case ViiperDriverReadinessState.ValidatedExperimental:
                    break;

                default:
                    // Unreachable unless the enum grows. Refuse rather than
                    // fall through to allow: a new state is unproven by
                    // definition.
                    return ViiperVirtualDeviceDecision.Refuse(
                        ViiperVirtualDeviceBlock.DriverUnvalidated,
                        DriverUnvalidatedReason);
            }

            if (!experimentalAcknowledged)
            {
                return ViiperVirtualDeviceDecision.Refuse(
                    ViiperVirtualDeviceBlock.ExperimentalNotAcknowledged,
                    ExperimentalNotAcknowledgedReason);
            }

            if (featureClass == ViiperFeatureClass.Audio && !audioClassEnabled)
            {
                return ViiperVirtualDeviceDecision.Refuse(
                    ViiperVirtualDeviceBlock.AudioClassNotEnabled,
                    AudioClassNotEnabledReason);
            }

            return ViiperVirtualDeviceDecision.Allow(
                featureClass == ViiperFeatureClass.Audio
                    ? "virtual audio endpoints are enabled for this experimental package"
                    : "the installed usbip-win2 package is a known experimental package and no audio endpoint is involved");
        }

        internal const string DriverMissingReason =
            "No usbip-win2 driver is installed, so no virtual controller can " +
            "be created. Open Settings and use Install / Repair VIIPER, which " +
            "installs the exact package version " + ProductInfo.ProductName +
            " lists. The driver status card there shows what was found.";

        internal const string DriverUnvalidatedReason =
            "A usbip-win2 driver is installed, but " + ProductInfo.ProductName +
            " could not confirm which package it is, so no new virtual " +
            "controller will be created. The driver status card in Settings " +
            "lists exactly what could not be confirmed.";

        internal const string ExperimentalNotAcknowledgedReason =
            "Virtual controllers run on a third-party kernel driver that is " +
            "still experimental, and that has not been acknowledged yet. Open " +
            "Settings and turn on virtual controller output to read what that " +
            "means and continue.";

        internal const string AudioClassNotEnabledReason =
            "Virtual audio and microphone endpoints are turned off. They are " +
            "off by default because the installed usbip-win2 driver has a " +
            "confirmed kernel defect that can crash Windows when such an " +
            "endpoint is torn down. Controller input, rumble and triggers work " +
            "without them; the switch is in Settings, next to the driver " +
            "status card.";
    }

    /// <summary>
    /// The gate wired to the running application: the session driver readiness
    /// and the two persisted consent flags.
    ///
    /// <para>Split from <see cref="ViiperVirtualDeviceGate"/> so the policy stays
    /// testable without a settings store, and so there is exactly one place that
    /// knows where consent is read from.</para>
    /// </summary>
    public static class ViiperVirtualDeviceGuard
    {
        /// <summary>
        /// Test seam. Null in the product, where readiness comes from the
        /// session cache.
        /// </summary>
        internal static Func<ViiperDriverReadinessState> ReadinessOverride;

        /// <summary>Test seam for the persisted flags.</summary>
        internal static Func<bool> AcknowledgedOverride;

        /// <summary>Test seam for the persisted flags.</summary>
        internal static Func<bool> AudioEnabledOverride;

        public static ViiperDriverReadinessState ReadinessState =>
            ReadinessOverride != null
                ? ReadinessOverride()
                : ViiperSetupManager.DriverReadiness.State;

        public static bool ExperimentalAcknowledged =>
            AcknowledgedOverride != null
                ? AcknowledgedOverride()
                : Global.ViiperExperimentalAcknowledged;

        public static bool AudioClassEnabled =>
            AudioEnabledOverride != null
                ? AudioEnabledOverride()
                : Global.AllowExperimentalAudioEndpoints;

        /// <param name="alreadyAttached">
        /// True only when the caller already holds a live, attached device of
        /// this exact kind. Passing true for a device that does not exist yet
        /// defeats the gate.
        /// </param>
        public static ViiperVirtualDeviceDecision Decide(
            ViiperFeatureClass featureClass, bool alreadyAttached = false) =>
            ViiperVirtualDeviceGate.Decide(ReadinessState, featureClass,
                ExperimentalAcknowledged, AudioClassEnabled, alreadyAttached);

        internal static void ResetOverridesForTests()
        {
            ReadinessOverride = null;
            AcknowledgedOverride = null;
            AudioEnabledOverride = null;
        }
    }
}

using System;

namespace DS4Windows
{
    public enum ControllerRuntimeLaneState : byte
    {
        NotRequired,
        Starting,
        Ready,
        Unavailable,
    }

    public enum ControllerStartupStage : byte
    {
        Disconnected,
        Connecting,
        Connected,
        CreatingVirtualController,
        ArmingAdvancedHaptics,
        StartingSpeaker,
        StartingMicrophone,
        StartingAudioHaptics,
        Ready,
        Attention,
    }

    public readonly struct ControllerRuntimeSignals
    {
        public ControllerRuntimeSignals(bool physicalPresent,
            bool physicalSynced, bool physicalAlive, bool virtualRequired,
            bool virtualConnected, bool virtualTypeMatches,
            ControllerRuntimeLaneState advancedHaptics,
            ControllerRuntimeLaneState speaker,
            ControllerRuntimeLaneState microphone,
            ControllerRuntimeLaneState audioHaptics,
            string virtualControllerName,
            string audioHapticsDetail = null)
        {
            PhysicalPresent = physicalPresent;
            PhysicalSynced = physicalSynced;
            PhysicalAlive = physicalAlive;
            VirtualRequired = virtualRequired;
            VirtualConnected = virtualConnected;
            VirtualTypeMatches = virtualTypeMatches;
            AdvancedHaptics = advancedHaptics;
            Speaker = speaker;
            Microphone = microphone;
            AudioHaptics = audioHaptics;
            VirtualControllerName = virtualControllerName ?? "virtual controller";
            AudioHapticsDetail = audioHapticsDetail ?? string.Empty;
        }

        public bool PhysicalPresent { get; }
        public bool PhysicalSynced { get; }
        public bool PhysicalAlive { get; }
        public bool VirtualRequired { get; }
        public bool VirtualConnected { get; }
        public bool VirtualTypeMatches { get; }
        public ControllerRuntimeLaneState AdvancedHaptics { get; }
        public ControllerRuntimeLaneState Speaker { get; }
        public ControllerRuntimeLaneState Microphone { get; }
        public ControllerRuntimeLaneState AudioHaptics { get; }
        public string VirtualControllerName { get; }

        /// <summary>
        /// The Audio Haptics service's own status message. Shown on the
        /// status card when the lane is <see cref="ControllerRuntimeLaneState.Unavailable"/>
        /// so the card names the real reason ("Waiting for a detected game",
        /// a capture error) instead of a generic "could not be armed".
        /// </summary>
        public string AudioHapticsDetail { get; }
    }

    public readonly struct ControllerStartupStatus : IEquatable<ControllerStartupStatus>
    {
        public ControllerStartupStatus(ControllerStartupStage stage,
            string title, string detail)
        {
            Stage = stage;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public ControllerStartupStage Stage { get; }
        public string Title { get; }
        public string Detail { get; }
        public bool IsReady => Stage == ControllerStartupStage.Ready;
        public bool NeedsAttention => Stage == ControllerStartupStage.Attention;

        public bool Equals(ControllerStartupStatus other) =>
            Stage == other.Stage && Title == other.Title && Detail == other.Detail;

        public override bool Equals(object obj) =>
            obj is ControllerStartupStatus other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Stage, Title, Detail);

        public static bool operator ==(ControllerStartupStatus left,
            ControllerStartupStatus right) => left.Equals(right);

        public static bool operator !=(ControllerStartupStatus left,
            ControllerStartupStatus right) => !left.Equals(right);
    }

    public static class ControllerRuntimeStatusPolicy
    {
        /// <summary>
        /// Lane state for the DualSense atomic audio+haptics carrier.
        ///
        /// <para>The lane rides the V4 frame type, which only exists on the
        /// audio-capable VIIPER persona. When audio-class consent is absent the
        /// persona ladder deliberately selects a HID-only variant, so the lane
        /// is <em>switched off</em>, not broken, and must not be reported as a
        /// fault. Deriving the requirement from the output persona alone —
        /// which is what this method replaces — produced a permanent amber
        /// "Needs attention" for the default, safe configuration, training
        /// users to ignore the status card and inviting them to "fix" it by
        /// enabling the one feature defaulted off for kernel-safety reasons.
        /// </para>
        ///
        /// <para><paramref name="laneLive"/> wins over the policy check: if the
        /// carrier really is up (consent was granted earlier in the session, or
        /// turned off while a device stayed attached) the lane is genuinely
        /// required, and a later failure must still surface as a fault.</para>
        /// </summary>
        public static ControllerRuntimeLaneState EvaluateAdvancedHapticsLane(
            bool virtualRequired, bool personaCarriesAdvancedHaptics,
            bool audioClassPermitted, bool laneLive, bool virtualConnected)
        {
            if (laneLive)
            {
                return ControllerRuntimeLaneState.Ready;
            }

            if (!virtualRequired || !personaCarriesAdvancedHaptics ||
                !audioClassPermitted)
            {
                return ControllerRuntimeLaneState.NotRequired;
            }

            return virtualConnected
                ? ControllerRuntimeLaneState.Unavailable
                : ControllerRuntimeLaneState.Starting;
        }

        /// <summary>
        /// True when the profile's output persona makes the <em>game</em> the
        /// haptics source: a virtual DualSense or DualSense Edge whose audio
        /// interfaces are permitted. On that path the title drives the pad's
        /// haptics through the virtual controller's channels 3/4 and Thrum
        /// relays them to the physical pad, so Audio Haptics — the substitute
        /// for games that cannot do this — would stack a second source onto
        /// the same Bluetooth stream. Issue #87.
        /// </summary>
        public static bool NativeHapticsPathOwnsAudioHaptics(
            OutContType desiredType, bool audioClassPermitted)
        {
            if (!audioClassPermitted)
            {
                return false;
            }

            OutContType normalized = desiredType.Normalize();
            return normalized == OutContType.ViiperDualSense ||
                normalized == OutContType.ViiperDualSenseEdge;
        }

        /// <summary>
        /// Audio Haptics lane state. Not required when the profile has it off
        /// or when the native haptics path owns the stream (switched off on
        /// purpose, not broken — same rule as the advanced-haptics lane).
        /// Otherwise it follows the service's own status.
        /// </summary>
        public static ControllerRuntimeLaneState EvaluateAudioHapticsLane(
            bool enabled, bool nativeHapticsPath, bool active,
            string statusMessage)
        {
            if (!enabled || nativeHapticsPath)
            {
                return ControllerRuntimeLaneState.NotRequired;
            }

            if (active)
            {
                return ControllerRuntimeLaneState.Ready;
            }

            return (statusMessage ?? string.Empty).IndexOf("starting",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? ControllerRuntimeLaneState.Starting
                : ControllerRuntimeLaneState.Unavailable;
        }

        public static ControllerStartupStatus Evaluate(
            ControllerRuntimeSignals signals)
        {
            if (!signals.PhysicalPresent)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.Disconnected, "Disconnected",
                    "No physical controller is assigned to this slot.");
            }

            if (!signals.PhysicalSynced || !signals.PhysicalAlive)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.Connecting, "Connecting",
                    "Waiting for stable input from the physical controller.");
            }

            if (signals.VirtualRequired && !signals.VirtualConnected)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.CreatingVirtualController,
                    "Connected",
                    $"Creating the virtual {signals.VirtualControllerName} pad.");
            }

            if (signals.VirtualRequired && !signals.VirtualTypeMatches)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.CreatingVirtualController,
                    "Connected",
                    $"Switching to the virtual {signals.VirtualControllerName} pad.");
            }

            ControllerStartupStatus laneStatus = EvaluateLane(
                signals.AdvancedHaptics,
                ControllerStartupStage.ArmingAdvancedHaptics,
                "Arming haptics", "advanced haptics lane");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            laneStatus = EvaluateLane(signals.Speaker,
                ControllerStartupStage.StartingSpeaker,
                "Starting speaker", "controller speaker and headset audio");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            laneStatus = EvaluateLane(signals.Microphone,
                ControllerStartupStage.StartingMicrophone,
                "Starting microphone", "controller microphone");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            laneStatus = EvaluateLane(signals.AudioHaptics,
                ControllerStartupStage.StartingAudioHaptics,
                "Starting Audio Haptics", "Audio Haptics capture",
                signals.AudioHapticsDetail);
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            string detail = signals.VirtualRequired
                ? "Physical input, virtual pad, and enabled media lanes are stable."
                : "Physical input and every enabled media lane are stable.";
            return new ControllerStartupStatus(ControllerStartupStage.Ready,
                "Ready", detail);
        }

        private static ControllerStartupStatus EvaluateLane(
            ControllerRuntimeLaneState state, ControllerStartupStage stage,
            string startingTitle, string laneName,
            string unavailableDetail = null)
        {
            return state switch
            {
                ControllerRuntimeLaneState.Starting =>
                    new ControllerStartupStatus(stage, startingTitle,
                        $"Waiting for the {laneName} to become stable."),
                ControllerRuntimeLaneState.Unavailable =>
                    new ControllerStartupStatus(ControllerStartupStage.Attention,
                        "Needs attention",
                        string.IsNullOrWhiteSpace(unavailableDetail)
                            ? $"The enabled {laneName} could not be armed."
                            : $"The enabled {laneName} could not be armed: {unavailableDetail.TrimEnd('.')}."),
                _ => new ControllerStartupStatus(
                    ControllerStartupStage.Ready, "Ready", string.Empty),
            };
        }
    }
}

using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerRuntimeStatusTests
    {
        private static ControllerRuntimeSignals Signals(
            bool present = true, bool synced = true, bool alive = true,
            bool virtualRequired = true, bool virtualConnected = true,
            bool virtualTypeMatches = true,
            ControllerRuntimeLaneState haptics = ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState speaker = ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState microphone = ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState audioHaptics = ControllerRuntimeLaneState.NotRequired) =>
            new ControllerRuntimeSignals(present, synced, alive,
                virtualRequired, virtualConnected, virtualTypeMatches,
                haptics, speaker, microphone, audioHaptics, "DualSense");

        [TestMethod]
        public void ReportsPhysicalConnectionStagesBeforeVirtualReadiness()
        {
            ControllerStartupStatus disconnected =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(present: false,
                    synced: false, alive: false, virtualRequired: false,
                    virtualConnected: false, virtualTypeMatches: false));
            Assert.AreEqual("Disconnected", disconnected.Title);

            ControllerStartupStatus connecting =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(synced: false,
                    alive: false, virtualConnected: false));
            Assert.AreEqual("Connecting", connecting.Title);

            ControllerStartupStatus creating =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(
                    virtualConnected: false));
            Assert.AreEqual("Connected", creating.Title);
            StringAssert.Contains(creating.Detail, "Creating");
        }

        [DataTestMethod]
        [DataRow(ControllerRuntimeLaneState.Starting, ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState.NotRequired, "Arming haptics")]
        [DataRow(ControllerRuntimeLaneState.Ready, ControllerRuntimeLaneState.Starting,
            ControllerRuntimeLaneState.NotRequired, "Starting speaker")]
        [DataRow(ControllerRuntimeLaneState.Ready, ControllerRuntimeLaneState.Ready,
            ControllerRuntimeLaneState.Starting, "Starting microphone")]
        public void ReportsEachRequiredLaneBeforeReady(
            ControllerRuntimeLaneState haptics,
            ControllerRuntimeLaneState speaker,
            ControllerRuntimeLaneState microphone, string detail)
        {
            ControllerStartupStatus status =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(
                    haptics: haptics, speaker: speaker,
                    microphone: microphone));
            Assert.IsFalse(status.IsReady);
            Assert.AreEqual(detail, status.Title);
        }

        [TestMethod]
        public void ReadyRequiresEveryRequestedLane()
        {
            ControllerStartupStatus status =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(
                    haptics: ControllerRuntimeLaneState.Ready,
                    speaker: ControllerRuntimeLaneState.Ready,
                    microphone: ControllerRuntimeLaneState.Ready,
                    audioHaptics: ControllerRuntimeLaneState.Ready));
            Assert.IsTrue(status.IsReady);
            Assert.AreEqual("Ready", status.Title);
        }

        [TestMethod]
        public void FailedLaneRequiresAttention()
        {
            ControllerStartupStatus status =
                ControllerRuntimeStatusPolicy.Evaluate(Signals(
                    speaker: ControllerRuntimeLaneState.Unavailable));
            Assert.IsTrue(status.NeedsAttention);
            Assert.IsFalse(status.IsReady);
        }

        /// <summary>
        /// The regression behind issue #17: without audio-class consent the
        /// persona ladder picks a HID-only variant, so the atomic
        /// audio+haptics carrier is absent by policy. Reporting that as a
        /// failed lane put the default, safe configuration into a permanent
        /// "Needs attention".
        /// </summary>
        [TestMethod]
        public void AdvancedHapticsLaneIsNotRequiredWhenAudioClassIsOff()
        {
            ControllerRuntimeLaneState lane =
                ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane(
                    virtualRequired: true, personaCarriesAdvancedHaptics: true,
                    audioClassPermitted: false, laneLive: false,
                    virtualConnected: true);

            Assert.AreEqual(ControllerRuntimeLaneState.NotRequired, lane);
            Assert.IsFalse(ControllerRuntimeStatusPolicy
                .Evaluate(Signals(haptics: lane)).NeedsAttention);
        }

        /// <summary>
        /// The other half: consent given and the carrier still absent is a
        /// genuine failure, and must keep surfacing. A fix that silenced this
        /// case would have hidden real breakage instead of a false alarm.
        /// </summary>
        [TestMethod]
        public void AdvancedHapticsLaneStillFailsWhenPermittedButAbsent()
        {
            ControllerRuntimeLaneState lane =
                ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane(
                    virtualRequired: true, personaCarriesAdvancedHaptics: true,
                    audioClassPermitted: true, laneLive: false,
                    virtualConnected: true);

            Assert.AreEqual(ControllerRuntimeLaneState.Unavailable, lane);
            Assert.IsTrue(ControllerRuntimeStatusPolicy
                .Evaluate(Signals(haptics: lane)).NeedsAttention);
        }

        [TestMethod]
        public void AdvancedHapticsLaneIsStartingWhileTheVirtualPadIsComingUp()
        {
            Assert.AreEqual(ControllerRuntimeLaneState.Starting,
                ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane(
                    virtualRequired: true, personaCarriesAdvancedHaptics: true,
                    audioClassPermitted: true, laneLive: false,
                    virtualConnected: false));
        }

        /// <summary>
        /// Consent turned off while a carrier stayed attached. The lane is up,
        /// so it is required and healthy; the setting governs the next
        /// connection, never this one.
        /// </summary>
        [TestMethod]
        public void ALiveLaneOutranksTheConsentFlag()
        {
            Assert.AreEqual(ControllerRuntimeLaneState.Ready,
                ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane(
                    virtualRequired: true, personaCarriesAdvancedHaptics: true,
                    audioClassPermitted: false, laneLive: true,
                    virtualConnected: true));
        }

        [DataTestMethod]
        [DataRow(false, true, DisplayName = "no virtual output at all")]
        [DataRow(true, false, DisplayName = "persona has no haptics carrier")]
        public void AdvancedHapticsLaneIsNotRequiredOffTheDualSensePersonas(
            bool virtualRequired, bool personaCarriesAdvancedHaptics)
        {
            Assert.AreEqual(ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane(
                    virtualRequired, personaCarriesAdvancedHaptics,
                    audioClassPermitted: true, laneLive: false,
                    virtualConnected: true));
        }
    }
}

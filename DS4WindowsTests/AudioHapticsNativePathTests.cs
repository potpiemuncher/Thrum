using DS4Windows;

namespace DS4WindowsTests
{
    /// <summary>
    /// Issue #87: when the profile's virtual DualSense carries audio
    /// interfaces, the game is the haptics source and Audio Haptics must be
    /// switched off for that profile — reported as "not required", never as
    /// a fault. Off that path the lane reports the service's real status.
    /// </summary>
    [TestClass]
    public class AudioHapticsNativePathTests
    {
        [DataTestMethod]
        [DataRow(OutContType.ViiperDualSense, true, true)]
        [DataRow(OutContType.ViiperDualSenseEdge, true, true)]
        [DataRow(OutContType.ViiperDualSense, false, false)]
        [DataRow(OutContType.ViiperDualSenseEdge, false, false)]
        [DataRow(OutContType.ViiperX360, true, false)]
        [DataRow(OutContType.ViiperDS4, true, false)]
        [DataRow(OutContType.ViiperSwitch2Pro, true, false)]
        [DataRow(OutContType.None, true, false)]
        public void NativePathIsOnlyTheAudioCapableDualSensePersonas(
            OutContType desiredType, bool audioClassPermitted, bool expected)
        {
            Assert.AreEqual(expected,
                ControllerRuntimeStatusPolicy.NativeHapticsPathOwnsAudioHaptics(
                    desiredType, audioClassPermitted));
        }

        [TestMethod]
        public void LaneIsNotRequiredWhenTheNativePathOwnsTheStream()
        {
            // Enabled in the profile, service reports inactive with the
            // suspended message: switched off on purpose, not broken.
            Assert.AreEqual(ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeStatusPolicy.EvaluateAudioHapticsLane(
                    enabled: true, nativeHapticsPath: true, active: false,
                    AudioHapticsService.NativeHapticsSuspendedMessage));
        }

        [TestMethod]
        public void LaneFollowsTheServiceOffTheNativePath()
        {
            Assert.AreEqual(ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeStatusPolicy.EvaluateAudioHapticsLane(
                    enabled: false, nativeHapticsPath: false, active: false,
                    "Audio Haptics is disabled."));
            Assert.AreEqual(ControllerRuntimeLaneState.Ready,
                ControllerRuntimeStatusPolicy.EvaluateAudioHapticsLane(
                    enabled: true, nativeHapticsPath: false, active: true,
                    "Audio Haptics is active."));
            Assert.AreEqual(ControllerRuntimeLaneState.Starting,
                ControllerRuntimeStatusPolicy.EvaluateAudioHapticsLane(
                    enabled: true, nativeHapticsPath: false, active: false,
                    "Starting Bluetooth haptics transport"));
            Assert.AreEqual(ControllerRuntimeLaneState.Unavailable,
                ControllerRuntimeStatusPolicy.EvaluateAudioHapticsLane(
                    enabled: true, nativeHapticsPath: false, active: false,
                    "Waiting for a detected game"));
        }

        [TestMethod]
        public void TheStatusCardNamesTheRealReasonInsteadOfCouldNotBeArmed()
        {
            ControllerRuntimeSignals signals = new ControllerRuntimeSignals(
                physicalPresent: true, physicalSynced: true, physicalAlive: true,
                virtualRequired: true, virtualConnected: true,
                virtualTypeMatches: true,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.Unavailable,
                "DualSense", "Waiting for a detected game");

            ControllerStartupStatus status =
                ControllerRuntimeStatusPolicy.Evaluate(signals);

            Assert.IsTrue(status.NeedsAttention);
            StringAssert.Contains(status.Detail, "Waiting for a detected game");
        }

        [TestMethod]
        public void TheStatusCardKeepsTheGenericTextWithoutADetail()
        {
            ControllerRuntimeSignals signals = new ControllerRuntimeSignals(
                true, true, true, true, true, true,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.NotRequired,
                ControllerRuntimeLaneState.Unavailable, "DualSense");

            ControllerStartupStatus status =
                ControllerRuntimeStatusPolicy.Evaluate(signals);

            Assert.AreEqual("The enabled Audio Haptics capture could not be armed.",
                status.Detail);
        }

        [TestMethod]
        public void SuspendingRecordsTheReasonAndLogsOnlyOnce()
        {
            using AudioHapticsService service = new AudioHapticsService();

            Assert.IsTrue(service.SuspendForNativeHaptics(0),
                "first suspension is the one to log");
            Assert.IsFalse(service.SuspendForNativeHaptics(0),
                "a profile reload on the same path must not log again");
            Assert.IsTrue(service.IsSuspendedForNativeHaptics(0));

            AudioHapticsRuntimeStatus status = service.GetStatus(0);
            Assert.IsFalse(status.Active);
            Assert.AreEqual(AudioHapticsService.NativeHapticsSuspendedMessage,
                status.Message);

            // Leaving the native path (or losing the pad) clears the state so
            // the next entry logs again.
            service.Stop(0);
            Assert.IsFalse(service.IsSuspendedForNativeHaptics(0));
            Assert.AreEqual("Audio Haptics is disabled.",
                service.GetStatus(0).Message);
            Assert.IsTrue(service.SuspendForNativeHaptics(0));
        }

        [TestMethod]
        public void SuspendingAnOutOfRangeSlotIsANoOp()
        {
            using AudioHapticsService service = new AudioHapticsService();
            Assert.IsFalse(service.SuspendForNativeHaptics(-1));
            Assert.IsFalse(service.SuspendForNativeHaptics(99));
            Assert.IsFalse(service.IsSuspendedForNativeHaptics(99));
        }
    }
}

using DS4Windows;
using DS4Windows.InputDevices;

namespace DS4WindowsTests;

[TestClass]
public class DualSenseControllerOptionsTests
{
    [TestMethod]
    public void ApplyBTHapticsOptionsPublishesCompleteSnapshotOnce()
    {
        DualSenseControllerOptions options = CreateOptions();
        int notificationCount = 0;
        DualSenseControllerOptions.HapticsMode observedMode = default;
        double observedGain = default;
        int observedLowPassHz = default;
        bool observedHFTexture = default;
        string observedAudioDeviceId = null;

        options.BTHapticsOptionChanged += (_, _) =>
        {
            notificationCount++;
            observedMode = options.BTHapticsMode;
            observedGain = options.BTHapticsGain;
            observedLowPassHz = options.BTHapticsLowPassHz;
            observedHFTexture = options.BTHapticsHFTexture;
            observedAudioDeviceId = options.BTHapticsAudioDeviceId;
        };

        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.SystemAudio,
            4.5, 250, true, "endpoint-1");

        Assert.AreEqual(1, notificationCount);
        Assert.AreEqual(DualSenseControllerOptions.HapticsMode.SystemAudio,
            observedMode);
        Assert.AreEqual(4.5, observedGain);
        Assert.AreEqual(250, observedLowPassHz);
        Assert.IsTrue(observedHFTexture);
        Assert.AreEqual("endpoint-1", observedAudioDeviceId);
    }

    [TestMethod]
    public void ApplyBTHapticsOptionsDoesNotRepublishIdenticalSnapshot()
    {
        DualSenseControllerOptions options = CreateOptions();
        int notificationCount = 0;
        options.BTHapticsOptionChanged += (_, _) => notificationCount++;

        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.RumbleToHaptics,
            6.0, 400, true, "endpoint-2");
        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.RumbleToHaptics,
            6.0, 400, true, "endpoint-2");

        Assert.AreEqual(1, notificationCount);
    }

    [TestMethod]
    public void ApplyBTHapticsOptionsNormalizesBeforeComparingSnapshots()
    {
        DualSenseControllerOptions options = CreateOptions();
        int notificationCount = 0;
        options.BTHapticsOptionChanged += (_, _) => notificationCount++;

        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.Off,
            0.0, 0, false, null);

        Assert.AreEqual(0.1, options.BTHapticsGain);
        Assert.AreEqual(40, options.BTHapticsLowPassHz);
        Assert.AreEqual(string.Empty, options.BTHapticsAudioDeviceId);
        Assert.AreEqual(1, notificationCount);

        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.Off,
            -100.0, -100, false, null);

        Assert.AreEqual(1, notificationCount);

        options.ApplyBTHapticsOptions(
            DualSenseControllerOptions.HapticsMode.Off,
            100.0, 5000, false, null);

        Assert.AreEqual(10.0, options.BTHapticsGain);
        Assert.AreEqual(1000, options.BTHapticsLowPassHz);
        Assert.AreEqual(2, notificationCount);
    }

    [TestMethod]
    public void IndividualBTHapticsSettersStillPublishEachChange()
    {
        DualSenseControllerOptions options = CreateOptions();
        int notificationCount = 0;
        options.BTHapticsOptionChanged += (_, _) => notificationCount++;

        options.BTHapticsMode =
            DualSenseControllerOptions.HapticsMode.SystemAudio;
        options.BTHapticsGain = 4.0;
        options.BTHapticsLowPassHz = 250;
        options.BTHapticsHFTexture = true;
        options.BTHapticsAudioDeviceId = "endpoint-3";

        Assert.AreEqual(5, notificationCount);
    }

    private static DualSenseControllerOptions CreateOptions() =>
        new DualSenseControllerOptions(InputDeviceType.DualSense);
}

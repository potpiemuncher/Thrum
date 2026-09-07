using DS4Windows;

namespace DS4WindowsTests;

[TestClass]
public class AudioHapticsStreamerMappingTests
{
    // --- Gain conversion: 0 / 100 / 200 percent ---

    [TestMethod]
    public void GainPercentZeroMapsToStreamerGain0_1Clamped()
    {
        AudioHapticsProfileSettings settings = new AudioHapticsProfileSettings
        {
            Enabled = true,
            GainPercent = 0,
            Source = AudioHapticsSourceKind.SystemAudio,
        };

        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(settings);

        Assert.AreEqual(0.1, opts.Gain);
    }

    [TestMethod]
    public void GainPercentHundredMapsToStreamerGain3_0()
    {
        AudioHapticsProfileSettings settings = new AudioHapticsProfileSettings
        {
            Enabled = true,
            GainPercent = 100,
            Source = AudioHapticsSourceKind.SystemAudio,
        };

        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(settings);

        Assert.AreEqual(3.0, opts.Gain);
    }

    [TestMethod]
    public void GainPercentTwoHundredMapsToStreamerGain6_0()
    {
        AudioHapticsProfileSettings settings = new AudioHapticsProfileSettings
        {
            Enabled = true,
            GainPercent = 200,
            Source = AudioHapticsSourceKind.SystemAudio,
        };

        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(settings);

        Assert.AreEqual(6.0, opts.Gain);
    }

    // --- BassFocus → low-pass Hz ---

    [TestMethod]
    public void BassFocusDeepMapsTo150Hz()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                BassFocus = AudioHapticsBassFocus.Deep,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(150, opts.LowPassHz);
    }

    [TestMethod]
    public void BassFocusBalancedMapsTo350Hz()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                BassFocus = AudioHapticsBassFocus.Balanced,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(350, opts.LowPassHz);
    }

    [TestMethod]
    public void BassFocusPunchyMapsTo250Hz()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                BassFocus = AudioHapticsBassFocus.Punchy,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(250, opts.LowPassHz);
    }

    [TestMethod]
    public void BassFocusWideMapsTo600Hz()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                BassFocus = AudioHapticsBassFocus.Wide,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(600, opts.LowPassHz);
    }

    // --- SourceKind → HapticsMode ---

    [TestMethod]
    public void SystemAudioEnabledMapsToSystemAudioMode()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.SystemAudio, opts.Mode);
    }

    [TestMethod]
    public void EndpointEnabledMapsToSystemAudioMode()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.Endpoint,
                EndpointId = "my-device-id",
            });

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.SystemAudio, opts.Mode);
    }

    [TestMethod]
    public void AppSessionEnabledMapsToOffMode()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.AppSession,
            });

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.Off, opts.Mode);
    }

    [TestMethod]
    public void ControllerAudioEnabledMapsToOffMode()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.ControllerAudio,
            });

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.Off, opts.Mode);
    }

    // --- Enabled = false forces Off ---

    [TestMethod]
    public void EnabledFalseForcesOffMode()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = false,
                Source = AudioHapticsSourceKind.SystemAudio,
                GainPercent = 150,
                BassFocus = AudioHapticsBassFocus.Wide,
            });

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.Off, opts.Mode);
        // Disabled settings return the streamer's proven defaults.
        Assert.AreEqual(3.0, opts.Gain);
        Assert.AreEqual(350, opts.LowPassHz);
    }

    // --- EndpointId passthrough ---

    [TestMethod]
    public void EndpointIdPassedThroughVerbatim()
    {
        string endpointId = "device-123-abc";
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.Endpoint,
                EndpointId = endpointId,
            });

        Assert.AreEqual(endpointId, opts.EndpointId);
    }

    [TestMethod]
    public void EmptyEndpointIdMeansDefaultRenderEndpoint()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.AreEqual(string.Empty, opts.EndpointId);
    }

    // --- HFTexture always false (streamer default) ---

    [TestMethod]
    public void HFTextureIsAlwaysFalse()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(new AudioHapticsProfileSettings
            {
                Enabled = true,
                Source = AudioHapticsSourceKind.SystemAudio,
            });

        Assert.IsFalse(opts.HFTexture);
    }

    // --- Null settings ---

    [TestMethod]
    public void NullSettingsReturnsOffModeWithDefaults()
    {
        AudioHapticsStreamerMapping.BTHapticsOptions opts =
            AudioHapticsStreamerMapping.Map(null!);

        Assert.AreEqual(
            DualSenseControllerOptions.HapticsMode.Off, opts.Mode);
        Assert.AreEqual(3.0, opts.Gain);
        Assert.AreEqual(350, opts.LowPassHz);
        Assert.AreEqual(string.Empty, opts.EndpointId);
    }
}

using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class AudioHapticsSourceValidationTests
    {
        [TestMethod]
        public void PresentVirtualRenderEndpointIsAcceptedWithoutDeviceFiltering()
        {
            AudioHapticsProfileSettings settings = EndpointSettings(
                "steelseries-sonar-game");

            AudioHapticsSourceValidationResult result =
                AudioHapticsSourceValidator.Validate(settings,
                    new[] { "speakers", "steelseries-sonar-game" },
                    defaultRenderEndpointAvailable: true,
                    controllerAudioEndpointAvailable: false,
                    _ => false);

            Assert.IsTrue(result.Valid, result.Message);
        }

        [TestMethod]
        public void VanishedRenderEndpointIsRejectedBeforeSettingsChange()
        {
            AudioHapticsProfileSettings settings = EndpointSettings(
                "unplugged-router");

            AudioHapticsSourceValidationResult result =
                AudioHapticsSourceValidator.Validate(settings,
                    new[] { "steelseries-sonar-game" },
                    defaultRenderEndpointAvailable: true,
                    controllerAudioEndpointAvailable: false,
                    _ => false);

            Assert.IsFalse(result.Valid);
            StringAssert.Contains(result.Message, "no longer available");
        }

        [TestMethod]
        public void DeadAppProcessIsRejectedBeforeSettingsChange()
        {
            AudioHapticsProfileSettings settings =
                new AudioHapticsProfileSettings
                {
                    Source = AudioHapticsSourceKind.AppSession,
                    ProcessId = 4242,
                    DisplayName = "Closed Game",
                };

            AudioHapticsSourceValidationResult result =
                AudioHapticsSourceValidator.Validate(settings,
                    Array.Empty<string>(),
                    defaultRenderEndpointAvailable: true,
                    controllerAudioEndpointAvailable: false,
                    _ => false,
                    processLoopbackSupported: true);

            Assert.IsFalse(result.Valid);
            StringAssert.Contains(result.Message, "not running");
        }

        [TestMethod]
        public void AnAppKnownByNameIsWaitedForNotRejected()
        {
            AudioHapticsProfileSettings settings =
                new AudioHapticsProfileSettings
                {
                    Source = AudioHapticsSourceKind.AppSession,
                    ProcessId = 4242,
                    ExecutableName = "SomeGame",
                    DisplayName = "Some Game",
                };

            AudioHapticsSourceValidationResult result =
                AudioHapticsSourceValidator.Validate(settings,
                    Array.Empty<string>(),
                    defaultRenderEndpointAvailable: true,
                    controllerAudioEndpointAvailable: false,
                    _ => false,
                    processLoopbackSupported: true);

            Assert.IsTrue(result.Valid, result.Message);
            StringAssert.Contains(result.Message, "start");
        }

        [TestMethod]
        public void AppSourcesAreRefusedWhereWindowsCannotCaptureOneApp()
        {
            foreach (bool automatic in new[] { false, true })
            {
                AudioHapticsProfileSettings settings =
                    new AudioHapticsProfileSettings
                    {
                        Source = AudioHapticsSourceKind.AppSession,
                        AutomaticGameDetection = automatic,
                        ExecutableName = "SomeGame",
                    };

                AudioHapticsSourceValidationResult result =
                    AudioHapticsSourceValidator.Validate(settings,
                        Array.Empty<string>(),
                        defaultRenderEndpointAvailable: true,
                        controllerAudioEndpointAvailable: false,
                        _ => true,
                        processLoopbackSupported: false);

                Assert.IsFalse(result.Valid, "automatic=" + automatic);
                StringAssert.Contains(result.Message, "Windows 11");
            }
        }

        private static AudioHapticsProfileSettings EndpointSettings(
            string endpointId) => new AudioHapticsProfileSettings
            {
                Source = AudioHapticsSourceKind.Endpoint,
                EndpointId = endpointId,
                EndpointName = "SteelSeries Sonar - Gaming",
            };
    }
}

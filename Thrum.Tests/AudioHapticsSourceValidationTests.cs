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
                    _ => false);

            Assert.IsFalse(result.Valid);
            StringAssert.Contains(result.Message, "not running");
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

using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests
{
    [TestClass]
    [DoNotParallelize]
    public class ControllerLightbarIdentifyTests
    {
        [TestMethod]
        public void IdentifyRestoresThePreviousForcedLightbarState()
        {
            const int device = 0;
            LightbarState original = Capture(device);
            try
            {
                var previous = new LightbarState(true,
                    new DS4Color(12, 34, 56), 7);
                Apply(device, previous);

                ControllerLightbarIdentify lease =
                    ControllerLightbarIdentify.Begin(device);

                Assert.IsTrue(DS4LightBar.forcelight[device]);
                Assert.AreEqual(new DS4Color(255, 255, 255),
                    DS4LightBar.forcedColor[device]);
                Assert.AreEqual((byte)20,
                    DS4LightBar.forcedFlash[device]);

                lease.Restore();

                Assert.AreEqual(previous, Capture(device));
            }
            finally
            {
                Apply(device, original);
            }
        }

        [TestMethod]
        public void IdentifyDoesNotOverwriteANewerForcedLightbarEffect()
        {
            const int device = 0;
            LightbarState original = Capture(device);
            try
            {
                Apply(device, new LightbarState(false,
                    new DS4Color(4, 5, 6), 0));
                ControllerLightbarIdentify lease =
                    ControllerLightbarIdentify.Begin(device);

                var newerEffect = new LightbarState(true,
                    new DS4Color(210, 40, 90), 3);
                Apply(device, newerEffect);
                lease.Restore();

                Assert.AreEqual(newerEffect, Capture(device));
            }
            finally
            {
                Apply(device, original);
            }
        }

        [TestMethod]
        public void IdentifyNeverMutatesConfiguredProfileLightbarValues()
        {
            const int device = 0;
            LightbarState original = Capture(device);
            LightbarDS4WinInfo configured =
                Global.LightbarSettingsInfo[device].ds4winSettings;
            bool useCustomLed = configured.useCustomLed;
            DS4Color mainColor = configured.m_Led;
            DS4Color customColor = configured.m_CustomLed;
            DS4Color flashColor = configured.m_FlashLed;
            try
            {
                ControllerLightbarIdentify lease =
                    ControllerLightbarIdentify.Begin(device);

                Assert.AreEqual(useCustomLed, configured.useCustomLed);
                Assert.AreEqual(mainColor, configured.m_Led);
                Assert.AreEqual(customColor, configured.m_CustomLed);
                Assert.AreEqual(flashColor, configured.m_FlashLed);

                lease.Restore();

                Assert.AreEqual(original, Capture(device));
                Assert.AreEqual(useCustomLed, configured.useCustomLed);
                Assert.AreEqual(mainColor, configured.m_Led);
                Assert.AreEqual(customColor, configured.m_CustomLed);
                Assert.AreEqual(flashColor, configured.m_FlashLed);
            }
            finally
            {
                Apply(device, original);
            }
        }

        private static LightbarState Capture(int device) =>
            new(DS4LightBar.forcelight[device],
                DS4LightBar.forcedColor[device],
                DS4LightBar.forcedFlash[device]);

        private static void Apply(int device, LightbarState state)
        {
            DS4LightBar.forcedColor[device] = state.Color;
            DS4LightBar.forcedFlash[device] = state.Flash;
            DS4LightBar.forcelight[device] = state.ForceLight;
        }

        private readonly struct LightbarState : IEquatable<LightbarState>
        {
            internal LightbarState(bool forceLight, DS4Color color,
                byte flash)
            {
                ForceLight = forceLight;
                Color = color;
                Flash = flash;
            }

            internal bool ForceLight { get; }
            internal DS4Color Color { get; }
            internal byte Flash { get; }

            public bool Equals(LightbarState other) =>
                ForceLight == other.ForceLight &&
                Color.Equals(other.Color) && Flash == other.Flash;

            public override bool Equals(object obj) =>
                obj is LightbarState other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(ForceLight, Color, Flash);
        }
    }
}

using DS4Windows;
using DS4Windows.InputDevices;

namespace DS4WindowsTests
{
    [TestClass]
    public class PlayStationFeatureOutputPolicyTests
    {
        [DataTestMethod]
        [DataRow((int)InputDeviceType.DS4, 0x05C4,
            (int)OutContType.ViiperX360, (int)OutContType.ViiperDS4)]
        [DataRow((int)InputDeviceType.DS4, 0x09CC,
            (int)OutContType.ViiperSwitch2Pro, (int)OutContType.ViiperDS4)]
        [DataRow((int)InputDeviceType.DualSense, 0x0CE6,
            (int)OutContType.ViiperX360,
            (int)OutContType.ViiperDualSense)]
        [DataRow((int)InputDeviceType.DualSense, 0x0DF2,
            (int)OutContType.ViiperSwitch2Pro,
            (int)OutContType.ViiperDualSense)]
        public void GenuineBluetoothPlayStationPadsGetAudioOnlySidecar(
            int deviceType, int productId, int primaryType, int expectedType)
        {
            OutContType actual = PlayStationFeatureOutputPolicy
                .GetAudioOnlySidecarType((InputDeviceType)deviceType,
                    ConnectionType.BT, DS4Devices.SONY_VID, productId,
                    (OutContType)primaryType, dInputOnly: false,
                    audioClassAllowed: true);

            Assert.AreEqual((OutContType)expectedType, actual);
        }

        [DataTestMethod]
        [DataRow((int)ConnectionType.USB, 0x054C, 0x0CE6,
            (int)OutContType.ViiperX360, false)]
        [DataRow((int)ConnectionType.BT, 0x054C, 0x0CE6,
            (int)OutContType.ViiperDualSense, false)]
        [DataRow((int)ConnectionType.BT, 0x1234, 0x0CE6,
            (int)OutContType.ViiperX360, false)]
        [DataRow((int)ConnectionType.BT, 0x054C, 0x0CE6,
            (int)OutContType.ViiperX360, true)]
        public void SidecarIsNotCreatedOutsideSupportedMatrix(
            int connectionType, int vendorId, int productId,
            int primaryType, bool dInputOnly)
        {
            OutContType actual = PlayStationFeatureOutputPolicy
                .GetAudioOnlySidecarType(InputDeviceType.DualSense,
                    (ConnectionType)connectionType, vendorId, productId,
                    (OutContType)primaryType, dInputOnly,
                    audioClassAllowed: true);

            Assert.AreEqual(OutContType.None, actual);
        }

        /// <summary>
        /// Plan task 2.3: the sidecar is no longer created implicitly.
        ///
        /// <para>This is the exact configuration observed on the maintainer's
        /// machine before this change - a genuine Sony pad on Bluetooth with an
        /// Xbox or Switch profile output - where a second virtual device
        /// carrying USB audio interfaces appeared without anyone asking for it,
        /// putting ordinary use on the teardown path the confirmed usbip-win2
        /// defect lives on. Every row here would have produced a sidecar before;
        /// none may now.</para>
        /// </summary>
        [DataTestMethod]
        [DataRow((int)InputDeviceType.DS4, 0x05C4, (int)OutContType.ViiperX360)]
        [DataRow((int)InputDeviceType.DS4, 0x09CC,
            (int)OutContType.ViiperSwitch2Pro)]
        [DataRow((int)InputDeviceType.DualSense, 0x0CE6,
            (int)OutContType.ViiperX360)]
        [DataRow((int)InputDeviceType.DualSense, 0x0DF2,
            (int)OutContType.ViiperSwitch2Pro)]
        public void NoSidecarIsCreatedWithoutAudioClassConsent(
            int deviceType, int productId, int primaryType)
        {
            OutContType actual = PlayStationFeatureOutputPolicy
                .GetAudioOnlySidecarType((InputDeviceType)deviceType,
                    ConnectionType.BT, DS4Devices.SONY_VID, productId,
                    (OutContType)primaryType, dInputOnly: false,
                    audioClassAllowed: false);

            Assert.AreEqual(OutContType.None, actual,
                "An audio-only sidecar was created without audio-class consent.");
        }

        /// <summary>
        /// Consent is a veto, not a trigger: granting it must not widen the
        /// hardware matrix. A USB pad, a third-party pad, a PlayStation primary
        /// output and a DInput-only profile still get no sidecar.
        /// </summary>
        [TestMethod]
        public void ConsentDoesNotWidenTheHardwareMatrix()
        {
            foreach (bool allowed in new[] { false, true })
            {
                Assert.AreEqual(OutContType.None, PlayStationFeatureOutputPolicy
                    .GetAudioOnlySidecarType(InputDeviceType.DualSense,
                        ConnectionType.USB, DS4Devices.SONY_VID, 0x0CE6,
                        OutContType.ViiperX360, dInputOnly: false,
                        audioClassAllowed: allowed));

                Assert.AreEqual(OutContType.None, PlayStationFeatureOutputPolicy
                    .GetAudioOnlySidecarType(InputDeviceType.SwitchPro,
                        ConnectionType.BT, DS4Devices.SONY_VID, 0x2009,
                        OutContType.ViiperX360, dInputOnly: false,
                        audioClassAllowed: allowed));
            }
        }

        /// <summary>
        /// The gate governs which devices are created, never which primary
        /// output owns the audio endpoints. A PlayStation profile still uses its
        /// own pad, which is why the sidecar exists only for the Xbox and Switch
        /// personas.
        /// </summary>
        [TestMethod]
        public void OnlyXboxAndSwitchOutputsEverWantASidecar()
        {
            Assert.IsTrue(PlayStationFeatureOutputPolicy.NeedsAudioOnlySidecar(
                OutContType.ViiperX360));
            Assert.IsTrue(PlayStationFeatureOutputPolicy.NeedsAudioOnlySidecar(
                OutContType.ViiperSwitch2Pro));

            foreach (OutContType playStation in new[]
            {
                OutContType.ViiperDS4, OutContType.ViiperDualSense,
                OutContType.ViiperDualSenseEdge,
            })
            {
                Assert.IsFalse(PlayStationFeatureOutputPolicy
                    .NeedsAudioOnlySidecar(playStation));
                Assert.IsTrue(PlayStationFeatureOutputPolicy
                    .IsPlayStationAudioOutput(playStation));
            }
        }
    }
}

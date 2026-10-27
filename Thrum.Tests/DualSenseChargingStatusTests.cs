using DS4Windows.InputDevices;

namespace DS4WindowsTests
{
    /// <summary>
    /// The DualSense battery byte carries the charge state in its high nibble
    /// (0 discharging, 1 charging, 2 full, 0xA/0xB/0xF errors); the byte after
    /// it has bit 3 set only with a USB data connection to the PC.
    /// </summary>
    [TestClass]
    public class DualSenseChargingStatusTests
    {
        [TestMethod]
        public void APadOnAWallChargerIsCharging()
        {
            // Charging, level 5, no USB data link (Bluetooth to the PC).
            Assert.IsTrue(DualSenseDevice.IsOnExternalPower(0x15, 0x00));
        }

        [TestMethod]
        public void AFullPadOnAChargerIsStillOnExternalPower()
        {
            Assert.IsTrue(DualSenseDevice.IsOnExternalPower(0x2A, 0x00));
            Assert.IsTrue(DualSenseDevice.IsFullyCharged(0x2A));
        }

        [TestMethod]
        public void APadOnBatteryIsNotCharging()
        {
            Assert.IsFalse(DualSenseDevice.IsOnExternalPower(0x05, 0x00));
            // A headset in the jack (bit 0) is not power.
            Assert.IsFalse(DualSenseDevice.IsOnExternalPower(0x05, 0x01));
            Assert.IsFalse(DualSenseDevice.IsFullyCharged(0x05));
        }

        [TestMethod]
        public void AUsbDataLinkStillCountsAsBefore()
        {
            // A charge error reported while plugged into the PC.
            Assert.IsTrue(DualSenseDevice.IsOnExternalPower(0xB0, 0x08));
        }

        [TestMethod]
        public void ErrorStatesAreNotFull()
        {
            foreach (byte state in new byte[] { 0xA0, 0xB0, 0xF0, 0x10 })
            {
                Assert.IsFalse(DualSenseDevice.IsFullyCharged(state),
                    $"0x{state:X2} read as full.");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    [DoNotParallelize]
    public class OwnVirtualSonyPendingTests
    {
        // A Bluetooth DualSense and a VIIPER DualSense report the same VID/PID.
        private const string PhysicalPad =
            @"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#b&6b79029&3&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        private const string ArrivingOutput =
            @"\\?\hid#vid_054c&pid_0ce6&mi_03#9&2a1b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

        private static HashSet<string> Snapshot(params string[] paths) =>
            new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

        [TestMethod]
        public void APadConnectedBeforeTheOutputWasPluggedIsNotHeldBack()
        {
            DS4Devices.BeginOwnVirtualSonyConnect(Snapshot(PhysicalPad));
            try
            {
                Assert.IsFalse(DS4Devices.IsOwnVirtualSonyConnectPending(PhysicalPad.ToUpperInvariant()));
                Assert.IsTrue(DS4Devices.IsOwnVirtualSonyConnectPending(ArrivingOutput));
            }
            finally
            {
                DS4Devices.EndOwnVirtualSonyConnect();
            }

            Assert.IsFalse(DS4Devices.IsOwnVirtualSonyConnectPending(ArrivingOutput));
        }

        [TestMethod]
        public void OverlappingConnectsTrustOnlyPathsInEverySnapshot()
        {
            // The first output arrived between the two snapshots, so it is in
            // the second one and must still be held back.
            DS4Devices.BeginOwnVirtualSonyConnect(Snapshot(PhysicalPad));
            DS4Devices.BeginOwnVirtualSonyConnect(Snapshot(PhysicalPad, ArrivingOutput));
            try
            {
                Assert.IsTrue(DS4Devices.IsOwnVirtualSonyConnectPending(ArrivingOutput));
                Assert.IsFalse(DS4Devices.IsOwnVirtualSonyConnectPending(PhysicalPad));
            }
            finally
            {
                DS4Devices.EndOwnVirtualSonyConnect();
                DS4Devices.EndOwnVirtualSonyConnect();
            }
        }

        [TestMethod]
        public void AFailedSnapshotHoldsBackEveryPadAsBefore()
        {
            DS4Devices.BeginOwnVirtualSonyConnect(null);
            try
            {
                Assert.IsTrue(DS4Devices.IsOwnVirtualSonyConnectPending(PhysicalPad));
            }
            finally
            {
                DS4Devices.EndOwnVirtualSonyConnect();
            }
        }
    }
}

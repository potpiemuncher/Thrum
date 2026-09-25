using System;
using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class ViiperServerWaitTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void WaitsOnlyForABackendLaunchedMomentsAgo()
        {
            Assert.IsTrue(ViiperSetupManager.ShouldWaitForServer(Now.AddSeconds(-1.75), Now));
        }

        [TestMethod]
        public void ARestartLaterInTheSessionDoesNotWait()
        {
            Assert.IsFalse(ViiperSetupManager.ShouldWaitForServer(Now.AddMinutes(-5), Now));
        }

        [TestMethod]
        public void NoLaunchThisSessionDoesNotWait()
        {
            Assert.IsFalse(ViiperSetupManager.ShouldWaitForServer(DateTime.MinValue, Now));
        }
    }
}

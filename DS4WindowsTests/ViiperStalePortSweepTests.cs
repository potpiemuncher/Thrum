/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The stale-import sweep's verdict (plan task 3.3, lifecycle invariant (f):
/// "unproven removal blocks reuse").
///
/// <para>The rule under test is the one the invariant actually turns on: a machine
/// nobody could look at is not a machine known to be clean. Before this, every
/// <c>usbip port</c> query could fail, the loop would see no ports, conclude nothing
/// needed detaching, and report the same clean window it reports when it genuinely
/// looked and found nothing — so "could not look" silently became evidence of
/// absence, and device creation proceeded on it.</para>
/// </summary>
[TestClass]
public class ViiperStalePortSweepTests
{
    [TestMethod]
    public void AMachineNobodyCouldLookAtIsNotProvenClean()
    {
        // Zero observed snapshots: every query failed. cleanSnapshots reaching the
        // requirement here is exactly the false negative - the loop counts a snapshot
        // as clean when it detached nothing, and a failed query detaches nothing.
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 0, cleanSnapshots: 10, requiredCleanSnapshots: 10,
            staleRemaining: 0, lastQueryError: "usbip.exe not found");

        Assert.IsFalse(sweep.Cleared,
            "no successful query means absence was never established.");
        StringAssert.Contains(sweep.Reason, "could not be read");
        StringAssert.Contains(sweep.Reason, "usbip.exe not found",
            "the refusal has to carry why, or the user cannot act on it.");
    }

    [TestMethod]
    public void AnUnreadablePortListStillRefusesWhenThereIsNoErrorText()
    {
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 0, cleanSnapshots: 10, requiredCleanSnapshots: 10,
            staleRemaining: 0, lastQueryError: "   ");

        Assert.IsFalse(sweep.Cleared);
        Assert.IsFalse(sweep.Reason.Contains("("),
            "an empty error must not produce an empty parenthetical.");
    }

    [TestMethod]
    public void StaleImportsThatSurviveEveryAttemptRefuse()
    {
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 32, cleanSnapshots: 0, requiredCleanSnapshots: 10,
            staleRemaining: 2, lastQueryError: null);

        Assert.IsFalse(sweep.Cleared);
        StringAssert.Contains(sweep.Reason, "still present");
        StringAssert.Contains(sweep.Reason, "2",
            "how many were left is the difference between a hiccup and a stuck port.");
    }

    [TestMethod]
    public void AnObservedAndSustainedCleanWindowIsProof()
    {
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 10, cleanSnapshots: 10, requiredCleanSnapshots: 10,
            staleRemaining: 0, lastQueryError: null);

        Assert.IsTrue(sweep.Cleared, "looked, repeatedly, and saw nothing: that is proof.");
        Assert.IsNull(sweep.Reason);
    }

    [TestMethod]
    public void OneObservedSnapshotIsEnoughWhenThatIsAllTheLoopRequired()
    {
        // requiredCleanSnapshots drops to 1 when this process already owns a port,
        // because PnP is established and the native device is protected.
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 1, cleanSnapshots: 1, requiredCleanSnapshots: 1,
            staleRemaining: 0, lastQueryError: null);

        Assert.IsTrue(sweep.Cleared);
    }

    [TestMethod]
    public void APartiallyObservedSweepIsJudgedOnWhatItSaw()
    {
        // Some queries failed and some succeeded. The successful ones are evidence,
        // so a sustained clean window among them still counts - a transient failure
        // must not permanently block device creation.
        ViiperStalePortSweep sweep = ViiperUsbipPortManager.DecideStaleSweep(
            observedSnapshots: 7, cleanSnapshots: 10, requiredCleanSnapshots: 10,
            staleRemaining: 0, lastQueryError: "transient");

        Assert.IsTrue(sweep.Cleared);
    }

    [TestMethod]
    public void TheResultTypeCarriesItsOwnContract()
    {
        Assert.IsTrue(ViiperStalePortSweep.Clear().Cleared);
        Assert.IsNull(ViiperStalePortSweep.Clear().Reason);
        Assert.IsFalse(ViiperStalePortSweep.Unproven("why").Cleared);
        Assert.AreEqual("why", ViiperStalePortSweep.Unproven("why").Reason);
    }
}

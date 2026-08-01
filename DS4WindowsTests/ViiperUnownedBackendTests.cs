using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The unowned-backend diagnostics behind the Settings backend-process card
/// (lifecycle invariant (d)'s follow-up).
///
/// <para>The safety property under test: the stop affordance is offered
/// exactly when the backend is provably not this session's and none of what
/// it hosts is this session's live controller — and refused everywhere else,
/// including every state that cannot be read. The classification itself must
/// stay honest about what it cannot know: leftovers of a dead session and
/// another consumer's live devices are indistinguishable from here.</para>
/// </summary>
[TestClass]
public class ViiperUnownedBackendTests
{
    private static ViiperBackendCensus Idle() =>
        ViiperBackendCensus.Success(Array.Empty<uint>(),
            Array.Empty<ViiperCensusDevice>());

    private static ViiperOwnedBackend SomeBackend() =>
        new ViiperOwnedBackend(4321,
            new DateTime(2026, 7, 30, 20, 0, 0, DateTimeKind.Local));

    private static ViiperUnownedBackendReport Assess(
        bool responding = true,
        ViiperOwnedBackend owned = null,
        bool ownedAlive = false,
        ViiperBackendCensus census = null,
        IReadOnlyCollection<ViiperCensusDevice> ours = null) =>
        ViiperUnownedBackendPolicy.Assess(responding, owned, ownedAlive,
            census ?? Idle(), ours ?? Array.Empty<ViiperCensusDevice>());

    // ---- Classification --------------------------------------------------

    [TestMethod]
    public void NothingRespondingIsNoBackendEvenWithAStaleOwnershipRecord()
    {
        ViiperUnownedBackendReport report = Assess(responding: false,
            owned: SomeBackend(), ownedAlive: false, census: null);

        Assert.AreEqual(ViiperUnownedBackendState.NoBackend, report.State);
        Assert.IsFalse(report.OffersStop);
    }

    [TestMethod]
    public void AnOwnedAliveBackendIsManagedAndNeedsNoCensus()
    {
        ViiperUnownedBackendReport report = Assess(owned: SomeBackend(),
            ownedAlive: true, census: null);

        Assert.AreEqual(ViiperUnownedBackendState.ManagedByThisApp,
            report.State);
        Assert.IsFalse(report.OffersStop,
            "The exit path owns the managed backend; this card must never " +
            "offer a second way to stop it.");
    }

    /// <summary>
    /// A record whose process is gone confers nothing: whatever answers the
    /// port now is somebody else's, and Windows reuses process ids.
    /// </summary>
    [TestMethod]
    public void ADeadOwnershipRecordConfersNothing()
    {
        ViiperUnownedBackendReport report = Assess(owned: SomeBackend(),
            ownedAlive: false, census: Idle());

        Assert.AreEqual(ViiperUnownedBackendState.UnownedIdle, report.State);
    }

    [TestMethod]
    public void AFailedCensusIsUnreadableAndOffersNothing()
    {
        ViiperUnownedBackendReport report = Assess(
            census: ViiperBackendCensus.Failed("connection reset"));

        Assert.AreEqual(ViiperUnownedBackendState.UnownedUnreadable,
            report.State);
        StringAssert.Contains(report.Detail, "connection reset");
        Assert.IsFalse(report.OffersStop,
            "Consent to stop a backend is consent to what it is holding; " +
            "an unreadable census means the user cannot be shown that.");

        Assert.AreEqual(ViiperUnownedBackendState.UnownedUnreadable,
            ViiperUnownedBackendPolicy.Assess(true, null, false, null,
                Array.Empty<ViiperCensusDevice>()).State);
    }

    [TestMethod]
    public void AnIdleUnownedBackendOffersTheStop()
    {
        ViiperUnownedBackendReport report = Assess();

        Assert.AreEqual(ViiperUnownedBackendState.UnownedIdle, report.State);
        Assert.IsTrue(report.OffersStop);
        Assert.AreEqual("nothing registered", report.DescribeHoldings());
    }

    [TestMethod]
    public void ForeignDevicesMakeItInUseAndTheStopIsOffered()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0 },
            new[] { new ViiperCensusDevice(0, "7", "dualsense") });

        ViiperUnownedBackendReport report = Assess(census: census);

        Assert.AreEqual(ViiperUnownedBackendState.UnownedInUse, report.State);
        Assert.AreEqual(1, report.ForeignDevices.Count);
        Assert.IsFalse(report.ServesThisApp);
        Assert.IsTrue(report.OffersStop);
        StringAssert.Contains(report.DescribeHoldings(), "dualsense");
        StringAssert.Contains(report.DescribeHoldings(),
            "not created by this session");
    }

    /// <summary>
    /// The pre-existing-backend arrangement: the user runs VIIPER, this app
    /// attaches to it. Its devices are ours; the backend is still not.
    /// </summary>
    [TestMethod]
    public void ABackendServingOnlyThisSessionsDevicesIsNotCalledInUse()
    {
        ViiperCensusDevice ourPad = new ViiperCensusDevice(0, "3", "dualsense");
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0 }, new[] { ourPad });

        ViiperUnownedBackendReport report = Assess(census: census,
            ours: new[] { new ViiperCensusDevice(0, "3", null) });

        Assert.AreEqual(ViiperUnownedBackendState.UnownedServingThisApp,
            report.State);
        Assert.IsTrue(report.ServesThisApp);
        Assert.IsFalse(report.OffersStop,
            "Stopping this backend would take this session's own " +
            "controller down with it.");
    }

    [TestMethod]
    public void AMixOfOursAndForeignDisablesTheStop()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0, 1 },
            new[]
            {
                new ViiperCensusDevice(0, "3", "dualsense"),
                new ViiperCensusDevice(1, "9", "xbox360"),
            });

        ViiperUnownedBackendReport report = Assess(census: census,
            ours: new[] { new ViiperCensusDevice(0, "3", null) });

        Assert.AreEqual(ViiperUnownedBackendState.UnownedInUse, report.State);
        Assert.IsTrue(report.ServesThisApp);
        Assert.IsFalse(report.OffersStop);
        Assert.AreEqual(1, report.ForeignDevices.Count);
        Assert.AreEqual(1, report.OurDevices.Count);
    }

    /// <summary>
    /// An empty bus is registered state somebody asked the backend to hold —
    /// the same reading the stop-on-exit policy gives it.
    /// </summary>
    [TestMethod]
    public void AnEmptyBusCountsAsInUse()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 3 }, Array.Empty<ViiperCensusDevice>());

        ViiperUnownedBackendReport report = Assess(census: census);

        Assert.AreEqual(ViiperUnownedBackendState.UnownedInUse, report.State);
        CollectionAssert.AreEqual(new List<uint> { 3 },
            report.EmptyBuses.ToList());
        Assert.IsTrue(report.OffersStop);
        StringAssert.Contains(report.DescribeHoldings(), "empty bus");
    }

    [TestMethod]
    public void ABusHostingADeviceIsNotAlsoListedAsEmpty()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0 },
            new[] { new ViiperCensusDevice(0, "7", "dualsense") });

        ViiperUnownedBackendReport report = Assess(census: census);

        Assert.AreEqual(0, report.EmptyBuses.Count,
            "The bus is already described by the device on it; listing it " +
            "twice would inflate the holdings.");
    }

    // ---- The listener locator's pure half --------------------------------

    private static ViiperTcpListenerRow Row(uint addr, int port,
        uint state = 2, int pid = 100) =>
        new ViiperTcpListenerRow(addr, port, state, pid);

    private const uint Loopback = 0x0100007F;
    private const uint Wildcard = 0;
    private const uint SomeLanAddress = 0x0A00000A;

    [TestMethod]
    public void TheLoopbackListenerIsPreferred()
    {
        int? pid = ViiperBackendProcessLocator.FindListenerProcessId(3242,
            new[]
            {
                Row(Wildcard, 3242, pid: 11),
                Row(Loopback, 3242, pid: 22),
                Row(SomeLanAddress, 3242, pid: 33),
            });

        Assert.AreEqual(22, pid,
            "The API host is 127.0.0.1; the loopback binding is the " +
            "strongest identification of the process that answered it.");
    }

    [TestMethod]
    public void TheWildcardListenerIsSecondChoice()
    {
        int? pid = ViiperBackendProcessLocator.FindListenerProcessId(3242,
            new[]
            {
                Row(SomeLanAddress, 3242, pid: 33),
                Row(Wildcard, 3242, pid: 11),
            });

        Assert.AreEqual(11, pid);
    }

    [TestMethod]
    public void OtherPortsAndNonListenStatesDoNotMatch()
    {
        Assert.IsNull(ViiperBackendProcessLocator.FindListenerProcessId(3242,
            new[]
            {
                Row(Loopback, 3243, pid: 11),
                Row(Loopback, 3242, state: 5, pid: 22), // ESTABLISHED
            }));
        Assert.IsNull(ViiperBackendProcessLocator.FindListenerProcessId(3242,
            Array.Empty<ViiperTcpListenerRow>()));
        Assert.IsNull(ViiperBackendProcessLocator.FindListenerProcessId(3242,
            null));
    }

    [TestMethod]
    public void ThePortDecodeSwapsNetworkByteOrder()
    {
        // 3242 is 0x0CAA; the table carries it byte-swapped in the low word.
        Assert.AreEqual(3242,
            ViiperBackendProcessLocator.DecodePort(0xAA0C));
        Assert.AreEqual(80, ViiperBackendProcessLocator.DecodePort(0x5000));
    }

    // ---- The stop request's commit-time gate ------------------------------

    private sealed class FixedCensus : IViiperBackendCensusSource
    {
        private readonly ViiperBackendCensus census;

        public FixedCensus(ViiperBackendCensus census)
        {
            this.census = census;
        }

        public ViiperBackendCensus TakeCensus() => census;
    }

    [TestMethod]
    public void TheStopRefusesWhenNoBackendIsRunning()
    {
        string logged = null;
        ViiperUnownedBackendStopOutcome outcome =
            ViiperSetupManager.StopUnownedBackend(
                message => logged = message,
                serverResponding: false);

        Assert.IsFalse(outcome.Attempted);
        StringAssert.Contains(outcome.Reason, "no backend is running");
        StringAssert.Contains(logged, "not stopped");
    }

    [TestMethod]
    public void TheStopRefusesWhenTheCensusCannotBeRead()
    {
        ViiperUnownedBackendStopOutcome outcome =
            ViiperSetupManager.StopUnownedBackend(
                censusSource: new FixedCensus(
                    ViiperBackendCensus.Failed("timed out")),
                serverResponding: true);

        Assert.IsFalse(outcome.Attempted);
        StringAssert.Contains(outcome.Reason, "could not be read");
        StringAssert.Contains(outcome.Reason, "timed out");
    }

    [TestMethod]
    public void TheStopRefusesWhenTheBackendServesThisSession()
    {
        ViiperOwnedDeviceRegistry.ResetForTests();
        try
        {
            ViiperOwnedDeviceRegistry.Register(0, "3");
            ViiperBackendCensus census = ViiperBackendCensus.Success(
                new uint[] { 0 },
                new[] { new ViiperCensusDevice(0, "3", "dualsense") });

            ViiperUnownedBackendStopOutcome outcome =
                ViiperSetupManager.StopUnownedBackend(
                    censusSource: new FixedCensus(census),
                    serverResponding: true);

            Assert.IsFalse(outcome.Attempted);
            StringAssert.Contains(outcome.Reason, "disconnect them first");
        }
        finally
        {
            ViiperOwnedDeviceRegistry.ResetForTests();
        }
    }

    /// <summary>
    /// The state can qualify while the process cannot be pinned down; the
    /// answer is a refusal that says so, never a guess at a process.
    /// </summary>
    [TestMethod]
    public void TheStopRefusesWhenTheListenerCannotBeIdentified()
    {
        ViiperUnownedBackendStopOutcome fromNull =
            ViiperSetupManager.StopUnownedBackend(
                censusSource: new FixedCensus(Idle()),
                listenerPidSource: () => null,
                serverResponding: true);
        Assert.IsFalse(fromNull.Attempted);
        StringAssert.Contains(fromNull.Reason, "3242");

        ViiperUnownedBackendStopOutcome fromThrow =
            ViiperSetupManager.StopUnownedBackend(
                censusSource: new FixedCensus(Idle()),
                listenerPidSource: () => throw new InvalidOperationException(),
                serverResponding: true);
        Assert.IsFalse(fromThrow.Attempted);
    }

    // ---- The outcome type -------------------------------------------------

    [TestMethod]
    public void OnlyAGracefulExitOrAKillCountsAsSuccess()
    {
        Assert.IsTrue(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Graceful,
                "exited"), "viiper (pid 5)").Succeeded);
        Assert.IsTrue(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Killed,
                "killed"), "viiper (pid 5)").Succeeded);
        Assert.IsFalse(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Failed,
                "kill did not end the process"), "viiper (pid 5)").Succeeded);
        Assert.IsFalse(ViiperUnownedBackendStopOutcome
            .Refused("no backend is running").Succeeded);
        Assert.IsFalse(ViiperUnownedBackendStopOutcome
            .Refused("anything").Attempted);
    }
}

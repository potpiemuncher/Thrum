using System;
using System.Collections.Generic;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The ordering fix for
/// <a href="https://github.com/potpiemuncher/Thrum/issues/12">issue #12</a>.
///
/// <para>The bug was not that the restart failed loudly — it was that it looked
/// like it worked. The replacement process started while the original still
/// held the named single-instance event, saw an instance already running,
/// signalled it and exited; the original then finished shutting down, taking
/// the VIIPER backend it owned with it. The user was left with nothing running
/// after a successful install.</para>
///
/// <para>So the tests below are mostly about what must <em>not</em> happen.
/// The launch is a precondition check, not a best effort: no release, no
/// launch, and the test proves the starter was never invoked rather than that
/// it returned something.</para>
/// </summary>
[TestClass]
public class PendingApplicationRestartTests
{
    private const string Executable = @"X:\install\Thrum.exe";

    [TestMethod]
    public void ARestartIsNotQueuedForAnExecutableThatIsNotThere()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();

        Assert.IsFalse(restart.Request(Executable, _ => false));
        Assert.IsFalse(restart.IsRequested);
        Assert.IsNull(restart.RequestedExecutable);
    }

    [TestMethod]
    public void ARestartIsNotQueuedWithoutAPath()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();

        Assert.IsFalse(restart.Request(null, _ => true));
        Assert.IsFalse(restart.Request("   ", _ => true));
        Assert.IsFalse(restart.IsRequested);
    }

    [TestMethod]
    public void NothingLaunchesWhenNothingAskedForARestart()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();
        restart.MarkSingleInstanceReleased();

        List<string> started = new List<string>();
        Assert.AreEqual(ViiperRestartLaunchOutcome.NotRequested,
            restart.Launch(started.Add));
        Assert.AreEqual(0, started.Count);
    }

    [TestMethod]
    public void AQueuedRestartRefusesToLaunchWhileTheSingleInstanceHandleIsHeld()
    {
        // This is issue #12 itself. Before the fix, this call started the
        // replacement; the replacement then exited on the guard, and the
        // shutdown that followed left the machine with no application and no
        // backend.
        PendingApplicationRestart restart = new PendingApplicationRestart();
        Assert.IsTrue(restart.Request(Executable, _ => true));

        List<string> started = new List<string>();
        List<string> log = new List<string>();

        Assert.AreEqual(ViiperRestartLaunchOutcome.SingleInstanceStillHeld,
            restart.Launch(started.Add, log.Add));
        Assert.AreEqual(0, started.Count,
            "the replacement must not be started while the handle is held");
        Assert.AreEqual(1, log.Count);
        StringAssert.Contains(log[0], "single-instance handle is still held");
    }

    [TestMethod]
    public void TheReplacementStartsOnceTheHandleHasBeenReleased()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();
        Assert.IsTrue(restart.Request(Executable, _ => true));
        restart.MarkSingleInstanceReleased();

        List<string> started = new List<string>();
        Assert.AreEqual(ViiperRestartLaunchOutcome.Launched,
            restart.Launch(started.Add));
        CollectionAssert.AreEqual(new[] { Executable }, started);
    }

    [TestMethod]
    public void AFailedLaunchAttemptStillCountsAndDoesNotRetryInALoop()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();
        restart.Request(Executable, _ => true);
        restart.MarkSingleInstanceReleased();

        List<string> log = new List<string>();
        Assert.AreEqual(ViiperRestartLaunchOutcome.LaunchFailed,
            restart.Launch(_ => throw new InvalidOperationException("denied"),
                log.Add));
        StringAssert.Contains(log[0], "denied");

        Assert.AreEqual(ViiperRestartLaunchOutcome.AlreadyLaunched,
            restart.Launch(_ => Assert.Fail("must not start a second time")));
    }

    [TestMethod]
    public void ASecondLaunchDoesNotStartASecondInstance()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();
        restart.Request(Executable, _ => true);
        restart.MarkSingleInstanceReleased();

        List<string> started = new List<string>();
        Assert.AreEqual(ViiperRestartLaunchOutcome.Launched,
            restart.Launch(started.Add));
        Assert.AreEqual(ViiperRestartLaunchOutcome.AlreadyLaunched,
            restart.Launch(started.Add));
        Assert.AreEqual(1, started.Count);
    }

    [TestMethod]
    public void ReleasingBeforeTheRequestIsStillAValidOrdering()
    {
        // The invariant is "released by the time we launch", not a fixed
        // sequence of calls; the shutdown path happens to do it the other way
        // round and either has to be safe.
        PendingApplicationRestart restart = new PendingApplicationRestart();
        restart.MarkSingleInstanceReleased();
        restart.Request(Executable, _ => true);

        List<string> started = new List<string>();
        Assert.AreEqual(ViiperRestartLaunchOutcome.Launched,
            restart.Launch(started.Add));
        Assert.AreEqual(1, started.Count);
    }

    [TestMethod]
    public void AQueuedRestartRemainsQueuedUntilItLaunches()
    {
        PendingApplicationRestart restart = new PendingApplicationRestart();
        restart.Request(Executable, _ => true);
        Assert.IsTrue(restart.IsRequested);
        Assert.AreEqual(Executable, restart.RequestedExecutable);

        restart.MarkSingleInstanceReleased();
        restart.Launch(_ => { });
        Assert.IsFalse(restart.IsRequested);
    }

    [TestMethod]
    public void TheApplicationWideInstanceStartsIdle()
    {
        // A shutdown that nobody asked to restart must not start anything, and
        // this is the instance the real shutdown path drains.
        Assert.IsFalse(PendingApplicationRestart.Current.IsRequested);
        Assert.AreEqual(ViiperRestartLaunchOutcome.NotRequested,
            PendingApplicationRestart.Current.Launch(
                _ => Assert.Fail("nothing was queued")));
    }
}

using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// Guards two logging truths from the 2026-07-30 Phase 2 VM validation pass:
/// the service-start line may claim a VIIPER backend only when the probe
/// actually found one (incidental defect 1), and a retry loop reports its
/// identical port-query failures once, not once per attempt (defect 2).
/// </summary>
[TestClass]
public class ViiperStatusLoggingTests
{
    // ---- The service-start line: defect 1 ------------------------------

    [TestMethod]
    public void AReadyBackendLogsTheReadyLine()
    {
        ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus
        {
            ViiperInstalled = true,
            UsbipInstalled = true,
            ServerRunning = true,
        };

        Assert.AreEqual("VIIPER virtual-controller backend ready",
            status.StartupLogLine);
    }

    /// <summary>
    /// The Phase A machine: no helper, no driver, no server. The log said
    /// "ready" one line before ten warnings that usbip.exe does not exist,
    /// while the Settings card correctly said everything was missing.
    /// </summary>
    [TestMethod]
    public void ABareMachineLogsWhatIsMissingInsteadOfReady()
    {
        ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus();

        Assert.AreEqual(
            "VIIPER virtual-controller backend not ready " +
            "(VIIPER and usbip-win2 need setup). " +
            "VIIPER helper: missing; usbip-win2: missing; server: not running.",
            status.StartupLogLine);
    }

    [TestMethod]
    public void AStoppedServerIsNamedAsTheMissingLeg()
    {
        ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus
        {
            ViiperInstalled = true,
            UsbipInstalled = true,
            ServerRunning = false,
        };

        Assert.AreEqual(
            "VIIPER virtual-controller backend not ready " +
            "(VIIPER server not running). " +
            "VIIPER helper: installed; usbip-win2: installed; server: not running.",
            status.StartupLogLine);
    }

    /// <summary>
    /// <see cref="ViiperPrerequisiteStatus.Ready"/> is the transport question:
    /// a server answering and a driver installed. A missing helper *file* with
    /// a live server still counts as ready — the server is the proof — and the
    /// log line must follow Ready rather than invent a stricter rule that
    /// disagrees with the prompts and attach paths built on it.
    /// </summary>
    [TestMethod]
    public void TheLogLineFollowsReadyNotTheHelperFile()
    {
        ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus
        {
            ViiperInstalled = false,
            UsbipInstalled = true,
            ServerRunning = true,
        };

        Assert.AreEqual("VIIPER virtual-controller backend ready",
            status.StartupLogLine);
    }

    /// <summary>
    /// The Settings card and the log line are composed from this one string,
    /// so a future wording change cannot make the two disagree.
    /// </summary>
    [TestMethod]
    public void TheComponentReadoutNamesEachLegsState()
    {
        ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus
        {
            UsbipInstalled = true,
        };

        Assert.AreEqual(
            "VIIPER helper: missing; usbip-win2: installed; server: not running",
            status.ComponentSummary);
    }

    // ---- The port-query warning: defect 2 ------------------------------

    [TestMethod]
    public void NoFailedQueriesMeansNoWarning()
    {
        Assert.IsNull(ViiperUsbipPortManager.DescribePortQueryFailures(
            0, "usbip.exe was not found."));
    }

    /// <summary>
    /// A single failure keeps the exact line shipped before this change, so
    /// triage notes and greps written against old logs still match.
    /// </summary>
    [TestMethod]
    public void ASingleFailureKeepsTheOriginalLine()
    {
        Assert.AreEqual(
            "VIIPER could not query usbip ports: usbip.exe was not found.",
            ViiperUsbipPortManager.DescribePortQueryFailures(
                1, "usbip.exe was not found."));
    }

    /// <summary>
    /// The Phase A log: the stale-port sweep queries ten clean snapshots at
    /// ~100 ms and each one failed the same way, ten identical warnings in one
    /// second. One line carries the same information.
    /// </summary>
    [TestMethod]
    public void RepeatedFailuresCollapseToOneLineNamingTheCount()
    {
        Assert.AreEqual(
            "VIIPER could not query usbip ports (10 attempts): usbip.exe was not found.",
            ViiperUsbipPortManager.DescribePortQueryFailures(
                10, "usbip.exe was not found."));
    }

    /// <summary>
    /// Mirrors the pre-change guard: a failure that produced no error text was
    /// never logged, and folding failures must not change that.
    /// </summary>
    [TestMethod]
    public void ABlankErrorIsStillNotWorthALine()
    {
        Assert.IsNull(ViiperUsbipPortManager.DescribePortQueryFailures(3, "  "));
    }
}

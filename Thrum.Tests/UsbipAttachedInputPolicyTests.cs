using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// Pads attached through usbip-win2's controller are never input. This closes
/// the residual PR #30 left open: the in-memory own-output registry dies with
/// its session, so a hard-killed session's leftover pad used to come back as
/// an input on the next start — the recursion the old startup port sweep
/// existed to prevent, and could no longer prevent once it stopped detaching
/// what it could not attribute. Ancestry does not depend on who remembers
/// creating the device, so the rule covers our leftovers and other
/// applications' virtual pads alike.
/// </summary>
[TestClass]
public class UsbipAttachedInputPolicyTests
{
    [TestInitialize]
    public void ResetWarnState()
    {
        UsbipAttachedInputPolicy.ResetForTests();
    }

    [TestMethod]
    public void APhysicalPadIsAccepted()
    {
        Assert.AreEqual(UsbipInputVerdict.Accept,
            UsbipAttachedInputPolicy.Decide(isOwnLiveOutput: false,
                isUsbipAttached: false));
    }

    [TestMethod]
    public void OurOwnLiveOutputIsRejectedQuietly()
    {
        Assert.AreEqual(UsbipInputVerdict.RejectOwnLiveOutput,
            UsbipAttachedInputPolicy.Decide(isOwnLiveOutput: true,
                isUsbipAttached: true));
        Assert.AreEqual(UsbipInputVerdict.RejectOwnLiveOutput,
            UsbipAttachedInputPolicy.Decide(isOwnLiveOutput: true,
                isUsbipAttached: false),
            "Ownership wins over whatever the ancestry probe said; a device " +
            "the registry claims is ours is ours to reject as ours.");
    }

    /// <summary>
    /// The case that used to recurse: usbip-attached, but nothing in this
    /// session remembers creating it — a dead session's leftover or another
    /// application's live pad, indistinguishable and equally not input.
    /// </summary>
    [TestMethod]
    public void AnUnmanagedUsbipPadIsRejected()
    {
        Assert.AreEqual(UsbipInputVerdict.RejectUnmanagedImport,
            UsbipAttachedInputPolicy.Decide(isOwnLiveOutput: false,
                isUsbipAttached: true));
    }

    [TestMethod]
    public void EachIgnoredPadIsAnnouncedExactlyOnce()
    {
        const string pad = @"\\?\hid#vid_054c&pid_0ce6#7&2ab44e7&0&0000#{guid}";
        const string otherPad = @"\\?\hid#vid_054c&pid_0df2#8&11112222&0&0000#{guid}";

        Assert.IsTrue(UsbipAttachedInputPolicy.ShouldWarnOnce(pad));
        Assert.IsFalse(UsbipAttachedInputPolicy.ShouldWarnOnce(pad),
            "Discovery re-runs on every hotplug; a deliberately ignored pad " +
            "must not be re-announced each time anything else arrives.");
        Assert.IsTrue(UsbipAttachedInputPolicy.ShouldWarnOnce(otherPad));
        Assert.IsFalse(UsbipAttachedInputPolicy.ShouldWarnOnce(null));
        Assert.IsFalse(UsbipAttachedInputPolicy.ShouldWarnOnce(string.Empty));
    }

    /// <summary>
    /// The line is all the user gets for a pad that visibly exists but is
    /// ignored: it must say what was seen, admit both readings of what it
    /// might be, and point at the affordance that can clear a leftover.
    /// </summary>
    [TestMethod]
    public void TheAnnouncementCarriesTheEvidenceAndTheRemedy()
    {
        const string pad = @"\\?\hid#vid_054c&pid_0ce6#7&2ab44e7&0&0000#{guid}";
        string line = UsbipAttachedInputPolicy.DescribeRejectedImport(pad);

        StringAssert.Contains(line, pad);
        StringAssert.Contains(line, "never used as input");
        StringAssert.Contains(line, "session that ended abruptly");
        StringAssert.Contains(line, "another program's live controller",
            "Naming only the leftover reading would invite the user to " +
            "clear a pad that another application is actively serving.");
        StringAssert.Contains(line, "Backend process");
    }
}

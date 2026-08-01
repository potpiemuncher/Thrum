using System;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The PnP absence proof itself: the shape policy code consumes, and the one
/// promise the real probe makes that can be tested on any machine — it
/// answers, and it never throws.
///
/// <para>The interesting verdicts (a phantom under the controller, a walk
/// that fails half-way) need a machine wearing the usbip-win2 driver in a
/// broken state, which is [VM] territory; what the suite pins down instead is
/// how <see cref="ViiperBackendStopPolicy"/> treats each verdict, over in
/// <see cref="ViiperBackendLifecycleTests"/>.</para>
/// </summary>
[TestClass]
public class ViiperPnpAbsenceProbeTests
{
    /// <summary>
    /// Runs the real SetupAPI/cfgmgr32 walk. On a machine without the driver
    /// it proves absence by the controller's absence; with the driver it
    /// walks the live tree. Either way the contract is the same: a non-null
    /// proof whose verdict carries its evidence.
    /// </summary>
    [TestMethod]
    public void TheRealProbeAlwaysAnswersAndNeverThrows()
    {
        ViiperPnpAbsenceProof proof = new CmTreePnpAbsenceProbe().Probe();

        Assert.IsNotNull(proof);
        switch (proof.Verdict)
        {
            case ViiperPnpAbsenceVerdict.ProvenAbsent:
            case ViiperPnpAbsenceVerdict.Unproven:
                Assert.IsFalse(string.IsNullOrWhiteSpace(proof.Detail),
                    "A verdict without its reasoning cannot be audited from " +
                    "the log line it ends up in.");
                break;
            case ViiperPnpAbsenceVerdict.DevicesPresent:
                Assert.IsTrue(proof.Devices.Count > 0,
                    "Claiming presence without naming a device is the " +
                    "unfalsifiable kind of claim this type exists to prevent.");
                break;
        }
    }

    [TestMethod]
    public void APresentProofNamesItsDevices()
    {
        ViiperPnpAbsenceProof proof = ViiperPnpAbsenceProof.Present(new[]
        {
            @"USB\VID_054C&PID_0CE6\1&0&1",
            @"USB\VID_054C&PID_0DF2\1&0&2 (problem 24)",
        });

        Assert.AreEqual(ViiperPnpAbsenceVerdict.DevicesPresent, proof.Verdict);
        Assert.AreEqual(2, proof.Devices.Count);
        StringAssert.Contains(proof.ToString(), "2 device(s)");
        StringAssert.Contains(proof.ToString(), "problem 24");
    }

    [TestMethod]
    public void AnUnprovenProofNeverCarriesAnEmptyReason()
    {
        Assert.AreEqual("unknown error",
            ViiperPnpAbsenceProof.Unproven(null).Detail);
        Assert.AreEqual("unknown error",
            ViiperPnpAbsenceProof.Unproven(string.Empty).Detail);
        StringAssert.Contains(
            ViiperPnpAbsenceProof.Unproven("CM_Get_Child returned CONFIGRET 3")
                .ToString(),
            "CONFIGRET 3");
    }

    [TestMethod]
    public void AnAbsentProofSaysWhatProvedIt()
    {
        ViiperPnpAbsenceProof proof = ViiperPnpAbsenceProof.Absent(
            "the usbip-win2 host controller is not present, so nothing can be attached through it");

        Assert.AreEqual(ViiperPnpAbsenceVerdict.ProvenAbsent, proof.Verdict);
        Assert.AreEqual(0, proof.Devices.Count);
        StringAssert.Contains(proof.ToString(), "not present");
    }
}

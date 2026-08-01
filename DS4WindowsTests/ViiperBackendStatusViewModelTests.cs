using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests;

/// <summary>
/// The Settings backend-process card. As with the driver card, the wording
/// assertions are policy checks: the card must never pretend to know whether
/// foreign devices are leftovers or another program's live controllers, the
/// stop button must track <see cref="ViiperUnownedBackendReport.OffersStop"/>
/// and nothing else, and the consent dialog must name the consequence of
/// being wrong.
/// </summary>
[TestClass]
public class ViiperBackendStatusViewModelTests
{
    private static ViiperUnownedBackendReport Report(
        ViiperUnownedBackendState state,
        IReadOnlyList<ViiperCensusDevice> foreign = null,
        IReadOnlyList<ViiperCensusDevice> ours = null,
        IReadOnlyList<uint> emptyBuses = null,
        string detail = null) =>
        new ViiperUnownedBackendReport(state, foreign, ours, emptyBuses,
            detail);

    private static ViiperBackendStatusViewModel Loaded(
        ViiperUnownedBackendReport report)
    {
        var viewModel = new ViiperBackendStatusViewModel();
        viewModel.Apply(report);
        return viewModel;
    }

    [TestMethod]
    public void BeforeApply_ClaimsNothing()
    {
        var viewModel = new ViiperBackendStatusViewModel();

        StringAssert.Contains(viewModel.Headline, "not been checked");
        Assert.IsFalse(viewModel.ShowStopButton);
        Assert.IsFalse(viewModel.HasHoldings);
        Assert.IsFalse(viewModel.HasActionResult);
    }

    [TestMethod]
    public void Busy_SaysCheckingAndWithdrawsTheButton()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(
            Report(ViiperUnownedBackendState.UnownedIdle));
        Assert.IsTrue(viewModel.ShowStopButton);

        viewModel.IsBusy = true;

        StringAssert.Contains(viewModel.Headline, "Checking");
        Assert.IsFalse(viewModel.ShowStopButton);
        Assert.IsFalse(viewModel.HasHoldings);
    }

    [TestMethod]
    public void NoBackend_IsOneQuietLine()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(
            Report(ViiperUnownedBackendState.NoBackend));

        StringAssert.Contains(viewModel.Headline, "No VIIPER backend");
        Assert.IsFalse(viewModel.ShowStopButton);
        Assert.IsFalse(viewModel.HasHoldings);
    }

    [TestMethod]
    public void Managed_SaysTheExitPathOwnsIt()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(Report(
            ViiperUnownedBackendState.ManagedByThisApp,
            detail: "pid 4321 started 2026-07-30T20:00:00.0000000"));

        StringAssert.Contains(viewModel.Headline, "started by");
        StringAssert.Contains(viewModel.Headline, "pid 4321");
        Assert.IsFalse(viewModel.ShowStopButton,
            "The managed backend already has an owner and an exit path; a " +
            "second stop control would race it.");
    }

    [TestMethod]
    public void UnownedIdle_OffersTheStopAndSaysWhyItWasNeverAutomatic()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(
            Report(ViiperUnownedBackendState.UnownedIdle));

        StringAssert.Contains(viewModel.Headline, "did not start");
        StringAssert.Contains(viewModel.Headline,
            "never stopped automatically");
        Assert.IsTrue(viewModel.ShowStopButton);
        Assert.IsFalse(viewModel.HasHoldings);
    }

    /// <summary>
    /// The (d) case. The card cannot know whether these devices are a dead
    /// session's leftovers or a live program's controllers, so it must say
    /// both — naming only the leftover reading would invite stopping a
    /// backend that is in real use.
    /// </summary>
    [TestMethod]
    public void UnownedInUse_NamesBothReadingsAndListsTheDevices()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(Report(
            ViiperUnownedBackendState.UnownedInUse,
            foreign: new[] { new ViiperCensusDevice(0, "7", "dualsense") },
            emptyBuses: new uint[] { 4 }));

        StringAssert.Contains(viewModel.Headline, "leftovers");
        StringAssert.Contains(viewModel.Headline, "another program");
        Assert.IsTrue(viewModel.ShowStopButton);
        Assert.IsTrue(viewModel.HasHoldings);

        List<string> lines = viewModel.HoldingLines.ToList();
        Assert.AreEqual(2, lines.Count);
        StringAssert.Contains(lines[0], "dualsense");
        StringAssert.Contains(lines[0], "not created by this session");
        StringAssert.Contains(lines[1], "bus 4");
        StringAssert.Contains(lines[1], "empty");
    }

    [TestMethod]
    public void UnownedInUseWithOurOwnDevices_DisablesTheStopAndSaysWhy()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(Report(
            ViiperUnownedBackendState.UnownedInUse,
            foreign: new[] { new ViiperCensusDevice(1, "9", "xbox360") },
            ours: new[] { new ViiperCensusDevice(0, "3", "dualsense") }));

        Assert.IsFalse(viewModel.ShowStopButton);
        StringAssert.Contains(viewModel.Headline, "disconnect them first");
        List<string> lines = viewModel.HoldingLines.ToList();
        Assert.IsTrue(lines.Any(line =>
            line.Contains("this session's controller")));
    }

    [TestMethod]
    public void ServingThisApp_AsksToLeaveItAlone()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(Report(
            ViiperUnownedBackendState.UnownedServingThisApp,
            ours: new[] { new ViiperCensusDevice(0, "3", "dualsense") }));

        StringAssert.Contains(viewModel.Headline, "Leave it running");
        Assert.IsFalse(viewModel.ShowStopButton);
    }

    [TestMethod]
    public void Unreadable_CarriesTheFailureAndOffersNothing()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(Report(
            ViiperUnownedBackendState.UnownedUnreadable,
            detail: "the backend did not answer bus/list"));

        StringAssert.Contains(viewModel.Headline,
            "could not be read");
        StringAssert.Contains(viewModel.Headline,
            "did not answer bus/list");
        Assert.IsFalse(viewModel.ShowStopButton);
    }

    // ---- The consent dialog's words ---------------------------------------

    [TestMethod]
    public void TheConfirmationAlwaysNamesTheConsequenceOfBeingWrong()
    {
        ViiperBackendStatusViewModel inUse = Loaded(Report(
            ViiperUnownedBackendState.UnownedInUse,
            foreign: new[] { new ViiperCensusDevice(0, "7", "dualsense") }));

        string body = inUse.BuildStopConfirmationBody();
        StringAssert.Contains(body, "dualsense");
        StringAssert.Contains(body, "will lose its virtual controller");
        StringAssert.Contains(body, "cannot tell the two apart");
    }

    [TestMethod]
    public void TheConfirmationForAnIdleBackendSaysItHostsNothing()
    {
        ViiperBackendStatusViewModel idle = Loaded(
            Report(ViiperUnownedBackendState.UnownedIdle));

        StringAssert.Contains(idle.BuildStopConfirmationBody(),
            "hosting nothing");
    }

    // ---- The outcome line --------------------------------------------------

    [TestMethod]
    public void EachOutcomeShapeGetsItsOwnPlainSentence()
    {
        ViiperBackendStatusViewModel viewModel = Loaded(
            Report(ViiperUnownedBackendState.UnownedIdle));

        viewModel.ApplyStopOutcome(
            ViiperUnownedBackendStopOutcome.Refused("no backend is running"));
        StringAssert.Contains(viewModel.ActionResultText, "Not stopped");
        StringAssert.Contains(viewModel.ActionResultText,
            "no backend is running");

        viewModel.ApplyStopOutcome(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Graceful,
                "console break accepted"), "viiper (pid 5)"));
        StringAssert.Contains(viewModel.ActionResultText, "exited on its own");
        StringAssert.Contains(viewModel.ActionResultText, "viiper (pid 5)");

        viewModel.ApplyStopOutcome(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Killed,
                "killed instead"), "viiper (pid 5)"));
        StringAssert.Contains(viewModel.ActionResultText, "had to be killed");

        viewModel.ApplyStopOutcome(ViiperUnownedBackendStopOutcome.From(
            new ViiperBackendStopResult(ViiperBackendStopMethod.Failed,
                "kill did not end the process"), "viiper (pid 5)"));
        StringAssert.Contains(viewModel.ActionResultText, "did not complete");
        StringAssert.Contains(viewModel.ActionResultText,
            "kill did not end the process");
    }
}

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
using DS4WinWPF.DS4Forms.ViewModels;
using System.Linq;

namespace DS4WindowsTests;

/// <summary>
/// The Native PS5 mode setup sheet (design handoff N2). What is checked here
/// is the gating: the sheet opens at the first unsatisfied step, never
/// advances past an unsatisfied one, never pre-checks a consent box, and
/// says why a disabled button is disabled.
/// </summary>
[TestClass]
public class NativePs5SetupViewModelTests
{
    private static NativePs5SetupInputs Inputs(
        bool driverKnown = false,
        bool backendReady = false,
        bool acknowledged = false,
        bool isOn = false,
        bool audioAllowed = false,
        bool hasDualSense = true,
        bool wireless = true,
        string deviceName = "DualSense",
        string profile = "Default",
        string transport = "Bluetooth") =>
        new(driverKnown, backendReady, acknowledged, isOn, audioAllowed,
            hasDualSense, wireless, deviceName, profile, transport);

    private static NativePs5SetupViewModel Opened(NativePs5SetupInputs inputs)
    {
        var sheet = new NativePs5SetupViewModel(
            new ViiperDriverStatusViewModel(() => null, null));
        sheet.Open(inputs);
        return sheet;
    }

    [TestMethod]
    public void OpensAtTheFirstUnsatisfiedStep()
    {
        Assert.AreEqual(1, Opened(Inputs()).CurrentStep);
        Assert.AreEqual(1, Opened(Inputs(driverKnown: true)).CurrentStep,
            "A known package with no backend answering is still step 1.");
        Assert.AreEqual(2, Opened(Inputs(driverKnown: true, backendReady: true))
            .CurrentStep);
        Assert.AreEqual(3, Opened(Inputs(driverKnown: true, backendReady: true,
            acknowledged: true)).CurrentStep);
    }

    [TestMethod]
    public void Step1_ContinueIsDisabledWithAReasonUntilTheDriverReadsBack()
    {
        NativePs5SetupViewModel missing = Opened(Inputs());
        Assert.IsFalse(missing.PrimaryEnabled);
        Assert.AreEqual("Continue", missing.PrimaryLabel);
        StringAssert.Contains(missing.PrimaryToolTip, "not installed yet");
        Assert.IsFalse(missing.Advance(), "Continue must not skip the gate.");
        Assert.AreEqual(1, missing.CurrentStep);

        NativePs5SetupViewModel unverified = Opened(Inputs(backendReady: true));
        StringAssert.Contains(unverified.PrimaryToolTip, "unverified package");

        NativePs5SetupViewModel backendDown = Opened(Inputs(driverKnown: true));
        StringAssert.Contains(backendDown.PrimaryToolTip, "backend is not running");
        Assert.IsTrue(backendDown.ShowBackendHint);
        Assert.AreEqual("Repair", backendDown.InstallLabel);

        Assert.AreEqual("Install / Repair VIIPER", missing.InstallLabel);
        StringAssert.Contains(missing.FooterHint, "known package");
    }

    [TestMethod]
    public void Step1_CopySaysAMatchIsNotApproval()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs());
        Assert.AreEqual(ViiperDriverStatusViewModel.NotProductionApprovedNote,
            sheet.NotApprovedNote);
        StringAssert.Contains(sheet.NotApprovedNote, "not");
        StringAssert.Contains(sheet.NotApprovedNote, "production approval");
    }

    [TestMethod]
    public void Step1_InstallingDisablesTheButtonsAndShowsTheMessageInline()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true));
        sheet.GoToStep(NativePs5SetupViewModel.InstallStep);

        sheet.SetInstalling(true);
        sheet.SetInstallMessage(NativePs5SetupViewModel.ElevationPendingMessage);
        Assert.IsFalse(sheet.CanInstall);
        Assert.IsFalse(sheet.PrimaryEnabled);
        Assert.AreEqual("Installing…", sheet.InstallLabel);
        Assert.IsTrue(sheet.HasInstallMessage);
        StringAssert.Contains(sheet.PrimaryToolTip, "finish");

        sheet.SetInstalling(false);
        Assert.IsTrue(sheet.CanInstall);
        Assert.IsTrue(sheet.PrimaryEnabled);
    }

    [TestMethod]
    public void UacCancellationTextIsTheSetupManagersOwnSentence()
    {
        Assert.AreEqual(ViiperSetupManager.InstallerCancelledAtUacMessage,
            NativePs5SetupViewModel.UacCancelledMessage);
        Assert.AreEqual(
            "VIIPER setup was canceled at the Windows administrator prompt. No changes were made.",
            NativePs5SetupViewModel.UacCancelledMessage);
    }

    [TestMethod]
    public void Step2_NothingIsPreCheckedAndContinueWaitsForTheBox()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true));

        Assert.AreEqual(2, sheet.CurrentStep);
        Assert.IsFalse(sheet.Acknowledged);
        Assert.IsFalse(sheet.PrimaryEnabled);
        Assert.AreEqual("Tick the box to continue", sheet.PrimaryToolTip);
        StringAssert.Contains(sheet.FooterHint, "Nothing is pre-checked");
        StringAssert.Contains(sheet.AcknowledgementNote, "Saved immediately");
        Assert.AreEqual("Muted", sheet.AcknowledgementNoteKind);
        Assert.IsFalse(sheet.Advance());

        sheet.Apply(Inputs(driverKnown: true, backendReady: true,
            acknowledged: true));
        Assert.AreEqual(2, sheet.CurrentStep, "Apply republishes facts; it does not move.");
        Assert.IsTrue(sheet.PrimaryEnabled);
        Assert.AreEqual("Saved.", sheet.FooterHint);
        Assert.AreEqual("Success", sheet.AcknowledgementNoteKind);
        Assert.IsTrue(sheet.Advance());
        Assert.AreEqual(3, sheet.CurrentStep);
    }

    [TestMethod]
    public void Step2_ShowsTheDisclosureAsShippedNotAParaphrase()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs());
        Assert.AreEqual(ViiperExperimentalDisclosure.AcknowledgementBody,
            sheet.AcknowledgementBody);
        StringAssert.Contains(sheet.AcknowledgementLabel, "experimental kernel driver");
    }

    [TestMethod]
    public void Step3_TurnOnWaitsForADualSenseAndSaysSo()
    {
        NativePs5SetupViewModel ready = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true));
        Assert.AreEqual(3, ready.CurrentStep);
        Assert.AreEqual(NativePs5SetupAction.TurnOn, ready.PrimaryAction);
        Assert.AreEqual("Turn on", ready.PrimaryLabel);
        Assert.IsTrue(ready.PrimaryEnabled);
        Assert.IsTrue(ready.NotOnYet);
        Assert.IsFalse(ready.WaitingForDualSense);
        Assert.IsFalse(ready.ShowStep4Link);
        StringAssert.Contains(ready.FooterHint, "profile Default");
        Assert.AreEqual(3, ready.Step3Bullets.Count);
        StringAssert.Contains(ready.Step3Bullets[1], "Hide DS4 Controller");

        NativePs5SetupViewModel noPad = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true, hasDualSense: false));
        Assert.AreEqual(3, noPad.CurrentStep,
            "Steps 1 and 2 need no controller; the sheet still opens at 3.");
        Assert.IsTrue(noPad.WaitingForDualSense);
        Assert.IsFalse(noPad.PrimaryEnabled);
        StringAssert.Contains(noPad.PrimaryToolTip, "Connect a DualSense");
    }

    [TestMethod]
    public void Step3_OnceOnItOffersDoneAndTheOptionalHapticsStep()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true, isOn: true));

        Assert.AreEqual(3, sheet.CurrentStep);
        Assert.AreEqual(NativePs5SetupAction.Done, sheet.PrimaryAction);
        Assert.AreEqual("Done", sheet.PrimaryLabel);
        Assert.IsTrue(sheet.ShowStep4Link);
        Assert.IsFalse(sheet.NotOnYet);
        Assert.AreEqual("DualSense (virtual)", sheet.GamesSeeOn);
        StringAssert.Contains(sheet.Step3HapticsNote, "Bluetooth");

        sheet.Apply(Inputs(driverKnown: true, backendReady: true,
            acknowledged: true, isOn: true, wireless: false));
        StringAssert.Contains(sheet.Step3HapticsNote, "USB");
    }

    [TestMethod]
    public void Step4_IsOptionalAndCarriesTheRiskSentenceAndTheTakeoverWarning()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true, isOn: true));
        sheet.GoToStep(NativePs5SetupViewModel.HapticsStep);

        Assert.IsTrue(sheet.IsStep4);
        Assert.AreEqual(NativePs5SetupAction.Done, sheet.PrimaryAction);
        Assert.IsTrue(sheet.PrimaryEnabled);
        Assert.IsFalse(sheet.AudioEndpointsAllowed);
        StringAssert.Contains(sheet.FooterHint, "Optional");
        StringAssert.Contains(sheet.AudioConsentNote, "every time");
        Assert.AreEqual(ViiperExperimentalDisclosure.AudioClassSummary,
            sheet.AudioClassSummary);
        Assert.AreEqual("Experimental, unverified", sheet.Step4Badge);
        StringAssert.Contains(NativePs5SetupViewModel.AudioDefaultTakeoverWarningText,
            ProductInfo.ProductName);
        StringAssert.Contains(NativePs5SetupViewModel.AudioDefaultTakeoverWarningText,
            "cannot prevent");
        Assert.AreEqual("Not available in this build",
            NativePs5SetupViewModel.RestoreDefaultDeviceUnavailableReason);
    }

    [TestMethod]
    public void RailMarksDoneCurrentAndOptionalStepsAndAnnouncesThem()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true));
        var rail = sheet.RailSteps.ToArray();

        Assert.AreEqual(4, rail.Length);
        Assert.AreEqual("✓", rail[0].Glyph);
        Assert.AreEqual("Done", rail[0].RingKind);
        Assert.AreEqual("✓", rail[1].Glyph);
        Assert.AreEqual("3", rail[2].Glyph);
        Assert.AreEqual("Current", rail[2].RingKind);
        Assert.IsTrue(rail[2].IsCurrent);
        Assert.AreEqual("Idle", rail[3].RingKind);
        Assert.IsTrue(rail[3].IsOptional);
        Assert.AreEqual("Step 1, Install backend, done", rail[0].ToString());
        Assert.AreEqual("Step 3, Turn on, current", rail[2].ToString());
        Assert.AreEqual("Step 4, Haptics over the virtual pad, optional",
            rail[3].ToString());
        Assert.AreEqual("Step 3 of 4: Turn on", sheet.StepAccessibleName);
    }

    [TestMethod]
    public void BackAndGoToStepStayInsideTheRail()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs(driverKnown: true,
            backendReady: true, acknowledged: true));

        Assert.IsTrue(sheet.CanBack);
        sheet.Back();
        Assert.AreEqual(2, sheet.CurrentStep);
        sheet.Back();
        Assert.AreEqual(1, sheet.CurrentStep);
        Assert.IsFalse(sheet.CanBack);
        sheet.Back();
        Assert.AreEqual(1, sheet.CurrentStep);

        sheet.GoToStep(9);
        Assert.AreEqual(1, sheet.CurrentStep);
        sheet.GoToStep(0);
        Assert.AreEqual(1, sheet.CurrentStep);
        sheet.GoToStep(4);
        Assert.AreEqual(4, sheet.CurrentStep);
        Assert.IsFalse(sheet.Advance(), "Nothing past the last step.");
    }

    [TestMethod]
    public void OpenResetsStepAndMessage_ApplyDoesNot()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs());
        sheet.SetInstallMessage("something");
        sheet.Apply(Inputs(driverKnown: true, backendReady: true));
        Assert.AreEqual(1, sheet.CurrentStep);
        Assert.IsTrue(sheet.HasInstallMessage);

        sheet.Open(Inputs(driverKnown: true, backendReady: true));
        Assert.AreEqual(2, sheet.CurrentStep);
        Assert.IsFalse(sheet.HasInstallMessage);
    }

    [TestMethod]
    public void EveryPublishRaisesOneNotificationForTheWholeSheet()
    {
        NativePs5SetupViewModel sheet = Opened(Inputs());
        int raised = 0;
        sheet.PropertyChanged += (_, e) =>
        {
            raised++;
            Assert.IsNull(e.PropertyName);
        };

        sheet.Apply(Inputs(driverKnown: true, backendReady: true));
        sheet.GoToStep(2);
        sheet.SetInstalling(true);
        Assert.AreEqual(3, raised);
    }
}

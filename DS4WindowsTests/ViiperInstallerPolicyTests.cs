using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The fail-closed decisions behind <c>extras/install-viiper-backend.ps1</c>.
///
/// <para>These live in C# rather than in the script for one reason worth
/// restating here: the admission rule is "the manifest decides", the manifest is
/// <see cref="ViiperDriverManifest"/>, and a PowerShell copy of its version
/// table would be the copy that decides whether a kernel driver is installed.
/// Keeping the decisions here means they are covered by the suite that already
/// gates every merge, and the script keeps only the mechanical half — fetching
/// bytes, running installers, swapping files.</para>
///
/// <para>Every test below is a pure call. Nothing here downloads, installs,
/// elevates, or reads the machine.</para>
/// </summary>
[TestClass]
public class ViiperInstallerPolicyTests
{
    private const string PinnedUsbipDigest =
        "51620FA5F9F8BE5932BC9D786DEEE557CE06D5407A99CAB490DCFAC71F185FEA";

    private const string PinnedViiperDigest =
        "3AD872D006DF2FC282E381A68B5A5B3C51E4DA3614D250AB3FDA1C272EF745D0";

    private const string PinnedSigner = "Cloudyne Systems (Scheibling Consulting AB)";

    // ---------------------------------------------------------------- pins --

    [TestMethod]
    public void ThePinnedUsbipReleaseIsOneTheManifestKnows()
    {
        // A pin the manifest does not recognise would install a package the
        // driver gate then refuses, which is the worst of both designs.
        Assert.IsTrue(ViiperDriverManifest.ObservedBaselines.Releases.Any(
            release => release.ReleaseLabel ==
                ViiperInstallerPins.UsbipWin2.ReleaseLabel));
    }

    [TestMethod]
    public void TheUsbipPinCarriesTheInspectedIdentity()
    {
        ViiperPinnedDownload pin = ViiperInstallerPins.UsbipWin2;
        Assert.AreEqual("0.9.7.7", pin.ReleaseLabel);
        Assert.AreEqual("USBip-0.9.7.7-x64.exe", pin.FileName);
        Assert.AreEqual(PinnedUsbipDigest, pin.Sha256);
        Assert.IsTrue(pin.RequireAuthenticode);
        Assert.AreEqual(PinnedSigner, pin.ExpectedSignerCommonName);
        StringAssert.StartsWith(pin.Url,
            "https://github.com/vadimgrn/usbip-win2/releases/download/");
    }

    [TestMethod]
    public void TheViiperPinIsAnExactAssetAndNotAReleaseQuery()
    {
        ViiperPinnedDownload pin = ViiperInstallerPins.ViiperBackend;
        Assert.AreEqual("v0.0.5", pin.ReleaseLabel);
        Assert.AreEqual(PinnedViiperDigest, pin.Sha256);
        Assert.AreEqual(
            "https://github.com/hbashton/VIIPER/releases/download/v0.0.5/viiper.exe",
            pin.Url);

        // Upstream publishes this asset unsigned, so requiring Authenticode
        // would fail on every honest download. The digest is the whole
        // identity, and the pin has to say so rather than quietly skip a check.
        Assert.IsFalse(pin.RequireAuthenticode);
        Assert.IsNull(pin.ExpectedSignerCommonName);
    }

    [TestMethod]
    public void NoPinResolvesAReleaseAtRunTime()
    {
        foreach (ViiperPinnedDownload pin in ViiperInstallerPins.All)
        {
            StringAssert.Contains(pin.Url, "/releases/download/",
                pin.FileName + " must name one asset, not a release query.");
            Assert.IsFalse(pin.Url.Contains("api.github.com"),
                pin.FileName + " must not be resolved through the releases API.");
            Assert.IsFalse(pin.Url.Contains("/latest"),
                pin.FileName + " must never resolve to 'latest'.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(pin.DigestProvenance),
                pin.FileName + " must record where its digest came from.");
        }
    }

    [TestMethod]
    public void ADigestComparesRegardlessOfHowItWasWrittenDown()
    {
        ViiperPinnedDownload pin = ViiperInstallerPins.UsbipWin2;
        Assert.IsTrue(pin.MatchesDigest(PinnedUsbipDigest.ToLowerInvariant()));
        Assert.IsTrue(pin.MatchesDigest("sha256:" + PinnedUsbipDigest));
        Assert.IsTrue(pin.MatchesDigest("  " + PinnedUsbipDigest + "  "));
        Assert.IsFalse(pin.MatchesDigest(null));
        Assert.IsFalse(pin.MatchesDigest(string.Empty));
    }

    [TestMethod]
    public void AComponentTokenIsParsedOrRejectedButNeverGuessed()
    {
        Assert.IsTrue(ViiperInstallerPins.TryParseComponent("usbip",
            out ViiperInstallerComponent usbip));
        Assert.AreEqual(ViiperInstallerComponent.UsbipWin2, usbip);

        Assert.IsTrue(ViiperInstallerPins.TryParseComponent("VIIPER",
            out ViiperInstallerComponent viiper));
        Assert.AreEqual(ViiperInstallerComponent.ViiperBackend, viiper);

        Assert.IsFalse(ViiperInstallerPins.TryParseComponent("usbipp", out _));
        Assert.IsFalse(ViiperInstallerPins.TryParseComponent(null, out _));
        Assert.IsFalse(ViiperInstallerPins.TryParseComponent(" ", out _));
    }

    // ------------------------------------------------- download verification --

    [TestMethod]
    public void ACorrectDigestAndTheExpectedSignerIsApproved()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2, GoodUsbipObservation());

        Assert.AreEqual(ViiperDownloadVerdict.Approved, decision.Action);
        AssertLogged(decision, "File name: expected USBip-0.9.7.7-x64.exe, " +
            "actual USBip-0.9.7.7-x64.exe.");
        AssertLogged(decision, "SHA-256: expected " + PinnedUsbipDigest +
            ", actual " + PinnedUsbipDigest + ".");
        AssertLogged(decision, "Authenticode signer: expected \"" +
            PinnedSigner + "\", actual \"" + PinnedSigner + "\".");
    }

    [TestMethod]
    public void ARefusalNamesTheFileItInspectedNotThePinnedArtefact()
    {
        // The Phase 2 VM pass verified a corrupted staged copy named
        // USBip-0.9.7.7-x64.CORRUPT.exe and the refusal read "Verification
        // failed: USBip-0.9.7.7-x64.exe does not have the pinned SHA-256" —
        // an accusation against the official artefact it never looked at.
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), sha256: new string('A', 64),
                fileName: "USBip-0.9.7.7-x64.CORRUPT.exe"));

        Assert.AreEqual(ViiperDownloadVerdict.DigestMismatch, decision.Action);
        StringAssert.Contains(decision.Summary,
            "USBip-0.9.7.7-x64.CORRUPT.exe");
        Assert.IsFalse(decision.Summary.Contains("USBip-0.9.7.7-x64.exe"),
            "the summary must not name the pinned artefact as the file that " +
            "failed: " + decision.Summary);
        AssertLogged(decision, "File name: expected USBip-0.9.7.7-x64.exe, " +
            "actual USBip-0.9.7.7-x64.CORRUPT.exe.");
    }

    [TestMethod]
    public void AFullPathRecordedInTheObservationNeverReachesTheReport()
    {
        // Belt and braces for the no-PII rule: even if a caller records the
        // whole path instead of the base name, only the base name may appear
        // in the decision text.
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), sha256: new string('A', 64),
                fileName: @"C:\p2\stage\USBip-0.9.7.7-x64.CORRUPT.exe"));

        StringAssert.Contains(decision.Summary,
            "USBip-0.9.7.7-x64.CORRUPT.exe");
        Assert.IsFalse(decision.Summary.Contains(@"C:\"), decision.Summary);
        Assert.IsFalse(decision.Lines.Any(line => line.Contains(@"C:\")),
            string.Join(Environment.NewLine, decision.Lines));
    }

    [TestMethod]
    public void AnObservationWithoutAFileNameNeverBlamesThePinnedArtefact()
    {
        foreach (ViiperDownloadObservation observation in
            new[] { null, new ViiperDownloadObservation { Exists = false } })
        {
            var decision = ViiperInstallerPolicy.DecideDownloadVerification(
                ViiperInstallerPins.UsbipWin2, observation);

            Assert.AreEqual(ViiperDownloadVerdict.Unavailable, decision.Action);
            StringAssert.Contains(decision.Summary, "the downloaded file");
            Assert.IsFalse(decision.Summary.Contains("USBip-0.9.7.7-x64.exe"),
                decision.Summary);
            AssertLogged(decision, "File name: expected " +
                "USBip-0.9.7.7-x64.exe, actual (not recorded).");
        }
    }

    [TestMethod]
    public void AWrongDigestIsRefusedEvenWithAPerfectSignature()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), sha256: new string('A', 64)));

        Assert.AreEqual(ViiperDownloadVerdict.DigestMismatch, decision.Action);
        AssertLogged(decision, "SHA-256: expected " + PinnedUsbipDigest +
            ", actual " + new string('A', 64) + ".");
    }

    [TestMethod]
    public void AMissingFileIsUnavailableRatherThanAMismatch()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            new ViiperDownloadObservation { Exists = false });

        Assert.AreEqual(ViiperDownloadVerdict.Unavailable, decision.Action);
        AssertLogged(decision, "File present: expected yes, actual no.");
    }

    [TestMethod]
    public void ANullObservationIsUnavailableAndNeverApproved()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2, null);

        Assert.AreEqual(ViiperDownloadVerdict.Unavailable, decision.Action);
    }

    [TestMethod]
    public void AnUncomputableDigestIsUnavailable()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), sha256: null,
                observationError: "the file is in use"));

        Assert.AreEqual(ViiperDownloadVerdict.Unavailable, decision.Action);
        AssertLogged(decision, "Digest could not be computed: the file is in use.");
    }

    [TestMethod]
    public void AValidSignatureFromTheWrongPublisherIsRefused()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), signerCommonName: "Some Other Publisher"));

        Assert.AreEqual(ViiperDownloadVerdict.UnexpectedSigner, decision.Action);
        AssertLogged(decision, "Authenticode signer: expected \"" +
            PinnedSigner + "\", actual \"Some Other Publisher\".");
    }

    [TestMethod]
    public void AnUntrustedSignatureIsRefusedBeforeTheSignerIsEvenConsidered()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), signatureTrusted: false,
                signatureDiagnostic: "untrusted root (developer/test signature)"));

        Assert.AreEqual(ViiperDownloadVerdict.SignatureNotTrusted, decision.Action);
        AssertLogged(decision, "Authenticode chain: expected trusted under " +
            "normal Windows policy, actual not trusted (untrusted root " +
            "(developer/test signature)).");
        Assert.IsFalse(decision.Lines.Any(line =>
            line.StartsWith("Authenticode signer:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnAbsentSignatureIsRefused()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), signatureTrusted: false,
                signerCommonName: null,
                signatureDiagnostic: "no valid signature"));

        Assert.AreEqual(ViiperDownloadVerdict.SignatureNotTrusted, decision.Action);
    }

    [TestMethod]
    public void ASignatureThatWasNeverEvaluatedIsNotAPass()
    {
        // The single most dangerous failure shape: a verifier that quietly did
        // not run reads exactly like one that ran and found nothing wrong.
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.UsbipWin2,
            With(GoodUsbipObservation(), signatureEvaluated: false));

        Assert.AreEqual(ViiperDownloadVerdict.Unavailable, decision.Action);
        AssertLogged(decision,
            "Authenticode: expected a verified chain, actual (not evaluated).");
    }

    [TestMethod]
    public void AnUnsignedComponentPassesOnItsDigestAloneAndSaysSo()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.ViiperBackend,
            new ViiperDownloadObservation
            {
                FileName = "viiper.exe",
                Exists = true,
                SizeInBytes = ViiperInstallerPins.ViiperBackend.SizeInBytes,
                Sha256 = PinnedViiperDigest,
                SignatureEvaluated = false,
            });

        Assert.AreEqual(ViiperDownloadVerdict.Approved, decision.Action);
        StringAssert.StartsWith(decision.Summary, "viiper.exe matches");
        Assert.IsTrue(decision.Lines.Any(line => line.Contains(
            "not required for this component")));
    }

    [TestMethod]
    public void AnUnsignedComponentStillFailsOnAWrongDigest()
    {
        var decision = ViiperInstallerPolicy.DecideDownloadVerification(
            ViiperInstallerPins.ViiperBackend,
            new ViiperDownloadObservation
            {
                Exists = true,
                SizeInBytes = 10,
                Sha256 = new string('B', 64),
            });

        Assert.AreEqual(ViiperDownloadVerdict.DigestMismatch, decision.Action);
    }

    [TestMethod]
    public void EveryVerificationRecordsExpectedBesideActual()
    {
        foreach (ViiperDownloadObservation observation in new[]
        {
            GoodUsbipObservation(),
            With(GoodUsbipObservation(), sha256: new string('C', 64)),
            With(GoodUsbipObservation(), signatureTrusted: false),
        })
        {
            var decision = ViiperInstallerPolicy.DecideDownloadVerification(
                ViiperInstallerPins.UsbipWin2, observation);
            Assert.IsTrue(decision.Lines.Any(line =>
                line.StartsWith("File name: expected ",
                    StringComparison.Ordinal)),
                "every verification has to record which file it examined");
            Assert.IsTrue(decision.Lines.Any(line =>
                line.StartsWith("SHA-256: expected ", StringComparison.Ordinal)),
                "every verification has to record the digest it compared");
            Assert.IsTrue(decision.Lines.Any(line =>
                line.StartsWith("Decision: ", StringComparison.Ordinal)),
                "every verification has to record its outcome");
        }
    }

    // --------------------------------------------------- usbip-win2 decision --

    [TestMethod]
    public void ThePinnedReleaseAlreadyInstalledIsLeftAloneAsAlreadyPinned()
    {
        var decision = Decide(ViiperDriverReadinessState.ValidatedExperimental,
            "0.9.7.7", ViiperDriverTier.ExperimentalBaseline);

        Assert.AreEqual(ViiperUsbipInstallAction.AlreadyPinned, decision.Action);
    }

    [TestMethod]
    public void ARecognisedNewerReleaseIsReportedAndNeverDowngraded()
    {
        // The maintainer's own machine: 0.9.7.8, which the manifest knows and
        // which is newer than the release setup would install. Reinstalling
        // 0.9.7.7 over it would be a kernel-driver downgrade decided by a
        // script rather than by the person who installed it.
        var decision = Decide(ViiperDriverReadinessState.ValidatedExperimental,
            "0.9.7.8", ViiperDriverTier.ExperimentalBaseline);

        Assert.AreEqual(ViiperUsbipInstallAction.LeaveRecognisedReleaseAlone,
            decision.Action);
        StringAssert.Contains(decision.Summary, "0.9.7.8");
        StringAssert.Contains(decision.Summary, "experimental baseline");
    }

    [TestMethod]
    public void AnUnvalidatedInstallIsRefusedRatherThanRepaired()
    {
        var decision = Decide(ViiperDriverReadinessState.DetectedUnvalidated,
            null, null);

        Assert.AreEqual(ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
            decision.Action);
        StringAssert.Contains(decision.Summary, "will not touch");
    }

    [TestMethod]
    public void AnEmptyMachineGetsThePinnedRelease()
    {
        var decision = Decide(ViiperDriverReadinessState.Missing, null, null);

        Assert.AreEqual(ViiperUsbipInstallAction.InstallPinned, decision.Action);
        StringAssert.Contains(decision.Summary, "0.9.7.7");
    }

    [TestMethod]
    public void ThePinnedReleaseRegisteredButNotBoundIsReinstalled()
    {
        // Reboot pending, or a half-finished install. Re-running the same
        // pinned release is the repair, and it is not a downgrade.
        var decision = Decide(ViiperDriverReadinessState.Missing, null, null,
            registered: "0.9.7.7");

        Assert.AreEqual(ViiperUsbipInstallAction.InstallPinned, decision.Action);
    }

    [TestMethod]
    public void ADifferentRecognisedReleaseRegisteredButNotBoundIsLeftAlone()
    {
        var decision = Decide(ViiperDriverReadinessState.Missing, null, null,
            registered: "0.9.7.8");

        Assert.AreEqual(ViiperUsbipInstallAction.LeaveRecognisedReleaseAlone,
            decision.Action);
    }

    [TestMethod]
    public void AVersionTheManifestDoesNotKnowIsRefusedNotAdmitted()
    {
        foreach (string unlisted in new[] { "0.9.7.9", "1.0.0", "0.9.7.6" })
        {
            var decision = Decide(ViiperDriverReadinessState.Missing, null, null,
                registered: unlisted);

            Assert.AreEqual(ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
                decision.Action, unlisted + " is not in the manifest");
            StringAssert.Contains(decision.Summary, unlisted);
        }
    }

    [TestMethod]
    public void ANewerReleaseIsNeverAdmittedForBeingNewer()
    {
        // The rule that replaces upstream's "-ge 0.9.7.7" floor. A floor admits
        // everything above it; the manifest admits exactly what it lists.
        var decision = Decide(ViiperDriverReadinessState.Missing, null, null,
            registered: "99.0.0.0");

        Assert.AreEqual(ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
            decision.Action);
    }

    [TestMethod]
    public void AReleaseLabelMatchesRegardlessOfALeadingV()
    {
        foreach (string spelling in new[] { "v0.9.7.7", "v.0.9.7.7", " 0.9.7.7 " })
        {
            var decision = Decide(ViiperDriverReadinessState.Missing, null, null,
                registered: spelling);
            Assert.AreEqual(ViiperUsbipInstallAction.InstallPinned,
                decision.Action, spelling);
        }
    }

    [TestMethod]
    public void AnApprovedTierMatchIsHandledLikeAnyOtherManifestMatch()
    {
        var decision = Decide(ViiperDriverReadinessState.Approved, "0.9.7.7",
            ViiperDriverTier.Production);

        Assert.AreEqual(ViiperUsbipInstallAction.AlreadyPinned, decision.Action);
    }

    [TestMethod]
    public void AnUnknownReadinessValueIsTreatedAsUnverifiable()
    {
        var decision = Decide((ViiperDriverReadinessState)99, "0.9.7.7",
            ViiperDriverTier.ExperimentalBaseline);

        Assert.AreEqual(ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
            decision.Action);
    }

    [TestMethod]
    public void EveryUsbipDecisionRecordsThePinAndTheObservedState()
    {
        foreach (ViiperDriverReadinessState state in
            Enum.GetValues(typeof(ViiperDriverReadinessState))
                .Cast<ViiperDriverReadinessState>())
        {
            var decision = Decide(state, "0.9.7.8",
                ViiperDriverTier.ExperimentalBaseline);
            Assert.IsTrue(decision.Lines.Any(line =>
                line.StartsWith("usbip-win2 pinned release: 0.9.7.7",
                    StringComparison.Ordinal)), state.ToString());
            Assert.IsTrue(decision.Lines.Any(line =>
                line.StartsWith("usbip-win2 installed state: ",
                    StringComparison.Ordinal)), state.ToString());
        }
    }

    // ------------------------------------------------ post-install validation --

    [TestMethod]
    public void APassingDiagnosticValidatesTheInstalledPair()
    {
        var decision = ViiperInstallerPolicy.DecidePostInstallValidation(true,
            ViiperDriverValidationCommand.ExitCodePassed);

        Assert.AreEqual(ViiperPostInstallVerdict.Validated, decision.Action);
    }

    [TestMethod]
    public void AFailingDiagnosticBlocks()
    {
        var decision = ViiperInstallerPolicy.DecidePostInstallValidation(true,
            ViiperDriverValidationCommand.ExitCodeFailed);

        Assert.AreEqual(ViiperPostInstallVerdict.Refused, decision.Action);
    }

    [TestMethod]
    public void ADiagnosticThatCouldNotRunIsAFailureAndNotANeutralOutcome()
    {
        var decision = ViiperInstallerPolicy.DecidePostInstallValidation(true,
            ViiperDriverValidationCommand.ExitCodeError);

        Assert.AreEqual(ViiperPostInstallVerdict.CouldNotRun, decision.Action);
        StringAssert.Contains(decision.Summary, "treated as a failure");
    }

    [TestMethod]
    public void ADiagnosticThatNeverStartedIsAlsoAFailure()
    {
        // The "the application executable is not next to the script" case.
        var decision = ViiperInstallerPolicy.DecidePostInstallValidation(false, 0);

        Assert.AreEqual(ViiperPostInstallVerdict.CouldNotRun, decision.Action);
    }

    [TestMethod]
    public void AnUndocumentedExitCodeIsNotTrusted()
    {
        foreach (int exitCode in new[] { -1, 3, 42 })
        {
            var decision = ViiperInstallerPolicy.DecidePostInstallValidation(
                true, exitCode);
            Assert.AreEqual(ViiperPostInstallVerdict.CouldNotRun,
                decision.Action, exitCode.ToString());
        }
    }

    // ------------------------------------------------------------ exit codes --

    [TestMethod]
    public void OnlyAValidatedPairProducesSuccess()
    {
        Assert.AreEqual(ViiperInstallerPolicy.ScriptExitSuccess,
            ViiperInstallerPolicy.ResolveScriptExitCode(
                ViiperPostInstallVerdict.Validated, restartPending: false));
        Assert.AreEqual(ViiperInstallerPolicy.ScriptExitSuccess,
            ViiperInstallerPolicy.ResolveScriptExitCode(
                ViiperPostInstallVerdict.Validated, restartPending: true));
    }

    [TestMethod]
    public void APendingRestartIsItsOwnOutcomeAndNotSuccess()
    {
        Assert.AreEqual(ViiperInstallerPolicy.ScriptExitRestartRequired,
            ViiperInstallerPolicy.ResolveScriptExitCode(
                ViiperPostInstallVerdict.CouldNotRun, restartPending: true));
        Assert.AreEqual(ViiperInstallerPolicy.ScriptExitFailed,
            ViiperInstallerPolicy.ResolveScriptExitCode(
                ViiperPostInstallVerdict.CouldNotRun, restartPending: false));
        Assert.AreEqual(ViiperInstallerPolicy.ScriptExitFailed,
            ViiperInstallerPolicy.ResolveScriptExitCode(
                ViiperPostInstallVerdict.Refused, restartPending: false));
    }

    [TestMethod]
    public void OnlyASuccessfulRunWithAReadyBackendRestartsTheApplication()
    {
        var report = ViiperInstallerPolicy.DescribeInstallerExit(
            ViiperInstallerPolicy.ScriptExitSuccess, ready: true, logPath: null);

        Assert.IsTrue(report.Succeeded);
        Assert.IsTrue(report.RestartApplication);
        Assert.IsFalse(report.IsError);
    }

    [TestMethod]
    public void ASuccessfulRunWithoutAReadyBackendDoesNotRestart()
    {
        var report = ViiperInstallerPolicy.DescribeInstallerExit(
            ViiperInstallerPolicy.ScriptExitSuccess, ready: false, logPath: null);

        Assert.IsFalse(report.Succeeded);
        Assert.IsFalse(report.RestartApplication);
        Assert.IsFalse(report.IsError);
    }

    [TestMethod]
    public void ARestartRequiredExitAsksForAWindowsRestartAndIsNotAnError()
    {
        var report = ViiperInstallerPolicy.DescribeInstallerExit(
            ViiperInstallerPolicy.ScriptExitRestartRequired, ready: true,
            logPath: null);

        Assert.IsFalse(report.Succeeded);
        Assert.IsFalse(report.RestartApplication);
        Assert.IsFalse(report.IsError);
        StringAssert.Contains(report.Message, "Restart Windows");
    }

    [TestMethod]
    public void AFailedRunIsAnErrorAndNeverRestartsTheApplication()
    {
        foreach (int exitCode in new[] { 1, 2, 7 })
        {
            var report = ViiperInstallerPolicy.DescribeInstallerExit(exitCode,
                ready: true, logPath: @"%LOCALAPPDATA%\VIIPER\install.log");

            Assert.IsFalse(report.Succeeded, exitCode.ToString());
            Assert.IsFalse(report.RestartApplication, exitCode.ToString());
            Assert.IsTrue(report.IsError, exitCode.ToString());
            StringAssert.Contains(report.Message, "install.log");
        }
    }

    [TestMethod]
    public void AReadyBackendNeverTurnsAFailedRunIntoASuccess()
    {
        // Setup can fail after the backend is already answering — a refused
        // driver package, for instance. The backend being up is not the
        // question the exit code answers.
        var report = ViiperInstallerPolicy.DescribeInstallerExit(
            ViiperInstallerPolicy.ScriptExitFailed, ready: true, logPath: null);

        Assert.IsFalse(report.Succeeded);
    }

    // -------------------------------------------------------------- autostart --

    [TestMethod]
    public void NoAutostartEntryMeansNothingToDoAndSetupCreatesNone()
    {
        var decision = ViiperInstallerPolicy.PlanAutostartRemoval(
            new ViiperAutostartStatus(Array.Empty<ViiperAutostartEntry>()),
            removalRequested: false);

        Assert.AreEqual(ViiperAutostartPlanAction.NothingToDo, decision.Action);
        Assert.IsTrue(decision.Lines.Any(line => line.Contains(
            "Setup does not create any")));
    }

    [TestMethod]
    public void AnExistingEntryIsOfferedForRemovalAndNotSilentlyAdopted()
    {
        var decision = ViiperInstallerPolicy.PlanAutostartRemoval(
            StatusWithBothEntries(), removalRequested: false);

        Assert.AreEqual(ViiperAutostartPlanAction.OfferRemoval, decision.Action);
        Assert.IsTrue(decision.Lines.Any(line => line.Contains(
            "Startup registry entry \"VIIPER\"")));
        Assert.IsTrue(decision.Lines.Any(line => line.Contains(
            "Logon scheduled task \"RunVIIPER\"")));
        Assert.IsTrue(decision.Lines.Any(line => line.Contains(
            "--update-notify none")),
            "the reason these entries matter is the live self-updater");
    }

    [TestMethod]
    public void RemovalHappensOnlyWhenItWasAskedFor()
    {
        var decision = ViiperInstallerPolicy.PlanAutostartRemoval(
            StatusWithBothEntries(), removalRequested: true);

        Assert.AreEqual(ViiperAutostartPlanAction.Remove, decision.Action);
        StringAssert.Contains(decision.Summary, "2 existing");
    }

    [TestMethod]
    public void AnUnreadableAutostartStateIsNeverReportedAsAbsent()
    {
        foreach (ViiperAutostartStatus status in new[]
        {
            null,
            new ViiperAutostartStatus(Array.Empty<ViiperAutostartEntry>(),
                "registry: access denied"),
        })
        {
            var decision = ViiperInstallerPolicy.PlanAutostartRemoval(status,
                removalRequested: true);
            Assert.AreEqual(ViiperAutostartPlanAction.CouldNotInspect,
                decision.Action);
        }
    }

    // ----------------------------------------------------------------- helpers --

    private static ViiperInstallerDecision<ViiperUsbipInstallAction> Decide(
        ViiperDriverReadinessState state, string matchedRelease,
        ViiperDriverTier? tier, string registered = null) =>
        ViiperInstallerPolicy.DecideUsbipInstall(state, matchedRelease, tier,
            registered, ViiperInstallerPins.UsbipWin2,
            ViiperDriverManifest.ObservedBaselines);

    private static ViiperDownloadObservation GoodUsbipObservation() =>
        new ViiperDownloadObservation
        {
            FileName = "USBip-0.9.7.7-x64.exe",
            Exists = true,
            SizeInBytes = ViiperInstallerPins.UsbipWin2.SizeInBytes,
            Sha256 = PinnedUsbipDigest,
            SignatureEvaluated = true,
            SignatureTrusted = true,
            SignerCommonName = PinnedSigner,
            SignatureDiagnostic = "trusted",
        };

    private static ViiperDownloadObservation With(
        ViiperDownloadObservation source, string sha256 = "\0",
        bool? signatureEvaluated = null, bool? signatureTrusted = null,
        string signerCommonName = "\0", string signatureDiagnostic = "\0",
        string observationError = "\0", string fileName = "\0") =>
        new ViiperDownloadObservation
        {
            FileName = fileName == "\0" ? source.FileName : fileName,
            Exists = source.Exists,
            SizeInBytes = source.SizeInBytes,
            Sha256 = sha256 == "\0" ? source.Sha256 : sha256,
            SignatureEvaluated = signatureEvaluated ?? source.SignatureEvaluated,
            SignatureTrusted = signatureTrusted ?? source.SignatureTrusted,
            SignerCommonName = signerCommonName == "\0"
                ? source.SignerCommonName : signerCommonName,
            SignatureDiagnostic = signatureDiagnostic == "\0"
                ? source.SignatureDiagnostic : signatureDiagnostic,
            ObservationError = observationError == "\0"
                ? source.ObservationError : observationError,
        };

    private static ViiperAutostartStatus StatusWithBothEntries() =>
        new ViiperAutostartStatus(new List<ViiperAutostartEntry>
        {
            new ViiperAutostartEntry(ViiperAutostartKind.RegistryRunValue,
                "VIIPER", @"C:\viiper\viiper.exe server"),
            new ViiperAutostartEntry(ViiperAutostartKind.ScheduledTask,
                "RunVIIPER", @"C:\viiper\viiper.exe server"),
        });

    private static void AssertLogged<T>(ViiperInstallerDecision<T> decision,
        string expected)
    {
        Assert.IsTrue(decision.Lines.Any(line =>
            string.Equals(line, expected, StringComparison.Ordinal)),
            "expected the decision to record: " + expected +
            Environment.NewLine + "actual lines:" + Environment.NewLine +
            string.Join(Environment.NewLine, decision.Lines));
    }
}

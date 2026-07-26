using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests;

/// <summary>
/// The Settings driver-status card (plan task 2.2). The wording assertions here
/// are policy checks, not copy checks: they come from the VM validation pass and
/// exist so a later edit cannot quietly turn "we recognise this package" into
/// "this package is approved", or drop the statement that a state restricts
/// features.
/// </summary>
[TestClass]
public class ViiperDriverStatusViewModelTests
{
    [TestMethod]
    public void BeforeLoad_ClaimsNoState()
    {
        var viewModel = new ViiperDriverStatusViewModel(
            () => Readiness(ViiperDriverReadinessState.Missing), null);

        Assert.IsNull(viewModel.State);
        Assert.AreEqual("Unknown", viewModel.BadgeKind);
        Assert.AreEqual("Not checked", viewModel.BadgeText);
        StringAssert.Contains(viewModel.Headline, "have not been checked");
        Assert.IsFalse(viewModel.HasRestriction);
        Assert.IsFalse(viewModel.HasReasons);
        Assert.IsFalse(viewModel.HasIdentities);
        Assert.AreEqual(string.Empty, viewModel.CheckedAtText);
    }

    [TestMethod]
    public void Missing_SaysWhatIsAbsentAndThatOutputIsUnavailable()
    {
        ViiperDriverStatusViewModel viewModel =
            Loaded(Readiness(ViiperDriverReadinessState.Missing));

        Assert.AreEqual("Missing", viewModel.BadgeKind);
        Assert.AreEqual("Not installed", viewModel.BadgeText);
        StringAssert.Contains(viewModel.Headline,
            "No usbip-win2 driver package is installed");
        Assert.IsTrue(viewModel.HasRestriction);
        StringAssert.Contains(viewModel.RestrictionText, "unavailable");
        Assert.IsFalse(viewModel.HasTierNote);
    }

    [TestMethod]
    public void DetectedUnvalidated_SaysFeaturesWillBeRestrictedAndListsReasons()
    {
        ViiperDriverStatusViewModel viewModel = Loaded(Readiness(
            ViiperDriverReadinessState.DetectedUnvalidated,
            reasons: new[]
            {
                "The usbip-win2 UDE host controller and filter extension do " +
                "not match a single observed baseline.",
                "Filter extension: the package is test-signed.",
            }));

        Assert.AreEqual("Unverified", viewModel.BadgeKind);
        Assert.AreEqual("Unverified", viewModel.BadgeText);
        Assert.IsTrue(viewModel.HasReasons);
        Assert.AreEqual(2, viewModel.Reasons.Count);
        CollectionAssert.Contains(viewModel.Reasons.ToArray(),
            "Filter extension: the package is test-signed.");

        Assert.IsTrue(viewModel.HasRestriction);
        // Present tense since the runtime guardrail landed (plan task 2.5), and
        // paired with the promise the guardrail actually keeps: live sessions
        // are not taken away.
        StringAssert.Contains(viewModel.RestrictionText,
            "no new virtual controller is created");
        StringAssert.Contains(viewModel.RestrictionText, "keep running");
        Assert.IsFalse(viewModel.HasTierNote,
            "Nothing matched, so there is no tier to describe.");
    }

    [TestMethod]
    public void ValidatedExperimental_NeverReadsAsProductionApproval()
    {
        ViiperDriverStatusViewModel viewModel = Loaded(Readiness(
            ViiperDriverReadinessState.ValidatedExperimental,
            releaseLabel: "0.9.7.7",
            tier: ViiperDriverTier.ExperimentalBaseline));

        Assert.AreEqual("Experimental", viewModel.BadgeKind);
        StringAssert.Contains(viewModel.BadgeText, "Experimental");
        Assert.IsFalse(
            viewModel.BadgeText.IndexOf("approved",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "An experimental match must never be badged as approved.");

        StringAssert.Contains(viewModel.Headline, "0.9.7.7");
        StringAssert.Contains(viewModel.Headline, "experimental baseline");

        Assert.IsTrue(viewModel.HasTierNote);
        StringAssert.Contains(viewModel.TierNote, "not production approval");
        StringAssert.Contains(viewModel.TierNote, "known kernel");
        Assert.AreEqual(ViiperDriverStatusViewModel.NotProductionApprovedNote,
            viewModel.TierNote);

        Assert.IsFalse(viewModel.HasRestriction,
            "A recognised package is not itself a restriction message.");
    }

    [TestMethod]
    public void Approved_IsTheOnlyStateAllowedToSayProduction()
    {
        ViiperDriverStatusViewModel approved = Loaded(Readiness(
            ViiperDriverReadinessState.Approved, releaseLabel: "9.9.9.9",
            tier: ViiperDriverTier.Production));

        Assert.AreEqual("Approved", approved.BadgeKind);
        Assert.AreEqual("Production approved", approved.BadgeText);
        StringAssert.Contains(approved.Headline, "9.9.9.9");
        Assert.IsFalse(approved.HasRestriction);

        foreach (ViiperDriverReadinessState state in new[]
        {
            ViiperDriverReadinessState.Missing,
            ViiperDriverReadinessState.DetectedUnvalidated,
            ViiperDriverReadinessState.ValidatedExperimental,
        })
        {
            ViiperDriverStatusViewModel other = Loaded(Readiness(state));
            Assert.IsFalse(
                other.BadgeText.IndexOf("Production approved",
                    StringComparison.OrdinalIgnoreCase) >= 0,
                state + " must not be badged as production approved.");
        }
    }

    [TestMethod]
    public void NoStateRecommendsInstallingAnUnlistedPackage()
    {
        // Rule 1 from the VM pass: the card never tells a user to go and get a
        // driver Thrum does not list. The only install path it may point at is
        // the bundled setup, which targets a listed release.
        string[] forbidden =
        {
            "download", "latest version", "newest", "upgrade to",
            "usbip-win2 releases", "github.com",
        };

        foreach (ViiperDriverReadinessState state in
            Enum.GetValues(typeof(ViiperDriverReadinessState))
                .Cast<ViiperDriverReadinessState>())
        {
            ViiperDriverStatusViewModel viewModel = Loaded(Readiness(state));
            string all = string.Join(" ", viewModel.Headline,
                viewModel.RestrictionText, viewModel.TierNote,
                viewModel.BadgeText);
            foreach (string token in forbidden)
            {
                Assert.IsFalse(
                    all.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0,
                    state + " card text contains \"" + token + "\": " + all);
            }
        }
    }

    [TestMethod]
    public void Identities_AreRenderedAsTheyWereObserved()
    {
        var identity = new ViiperDriverComponentIdentity(
            "UDE host controller", true, new[]
            {
                new ViiperDriverIdentityField("INF provider", "USBIP-WIN2"),
                new ViiperDriverIdentityField("DriverVer", "21.14.27.907"),
            });
        ViiperDriverStatusViewModel viewModel = Loaded(Readiness(
            ViiperDriverReadinessState.ValidatedExperimental,
            releaseLabel: "0.9.7.7",
            tier: ViiperDriverTier.ExperimentalBaseline,
            identities: new[] { identity }));

        Assert.IsTrue(viewModel.HasIdentities);
        Assert.AreEqual(1, viewModel.Identities.Count);
        Assert.AreEqual("UDE host controller",
            viewModel.Identities[0].Component);
        Assert.AreEqual("21.14.27.907",
            viewModel.Identities[0].Fields[1].Value);
    }

    [TestMethod]
    public void Recheck_UsesTheRefreshSourceAndRepublishes()
    {
        int reads = 0;
        int refreshes = 0;
        var viewModel = new ViiperDriverStatusViewModel(
            () =>
            {
                reads++;
                return Readiness(ViiperDriverReadinessState.Missing);
            },
            () =>
            {
                refreshes++;
                return Readiness(
                    ViiperDriverReadinessState.ValidatedExperimental,
                    releaseLabel: "0.9.7.8",
                    tier: ViiperDriverTier.ExperimentalBaseline);
            });

        int notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Load();
        Assert.AreEqual(1, reads);
        Assert.AreEqual(0, refreshes);
        Assert.AreEqual(ViiperDriverReadinessState.Missing, viewModel.State);

        viewModel.Recheck();
        Assert.AreEqual(1, reads, "Re-check must not fall back to the cache.");
        Assert.AreEqual(1, refreshes);
        Assert.AreEqual(ViiperDriverReadinessState.ValidatedExperimental,
            viewModel.State);
        StringAssert.Contains(viewModel.Headline, "0.9.7.8");
        Assert.IsTrue(notifications >= 2,
            "Each publish must notify the bindings.");
    }

    [TestMethod]
    public void Busy_ShowsProgressWithoutClaimingAState()
    {
        ViiperDriverStatusViewModel viewModel = Loaded(Readiness(
            ViiperDriverReadinessState.DetectedUnvalidated,
            reasons: new[] { "something was wrong" }));

        viewModel.IsBusy = true;

        Assert.IsFalse(viewModel.CanInteract);
        Assert.AreEqual("Checking", viewModel.BadgeKind);
        Assert.AreEqual("Checking", viewModel.BadgeText);
        Assert.AreEqual(0, viewModel.Reasons.Count,
            "Stale reasons must not sit under a running check.");
        Assert.IsFalse(viewModel.HasIdentities);

        viewModel.IsBusy = false;

        Assert.IsTrue(viewModel.CanInteract);
        Assert.AreEqual(1, viewModel.Reasons.Count);
    }

    [TestMethod]
    public void ApplyDiagnostic_ExposesTheSavedReportForCopyAndOpen()
    {
        ViiperDriverStatusViewModel viewModel =
            Loaded(Readiness(ViiperDriverReadinessState.Missing));

        Assert.IsFalse(viewModel.HasReport);
        Assert.IsFalse(viewModel.CanOpenReport);
        Assert.IsFalse(viewModel.HasReportStatus);

        viewModel.ApplyDiagnostic(new ViiperDriverDiagnosticRun
        {
            Text = "report body",
            FilePath = @"C:\Temp\Thrum\viiper-driver-validation-1.txt",
            DisplayPath = @"%TEMP%\Thrum\viiper-driver-validation-1.txt",
        });

        Assert.IsTrue(viewModel.HasReport);
        Assert.IsTrue(viewModel.CanOpenReport);
        Assert.AreEqual("report body", viewModel.ReportText);
        StringAssert.Contains(viewModel.ReportStatusText, "%TEMP%");
        Assert.IsFalse(
            viewModel.ReportStatusText.IndexOf(@"C:\Temp",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "The status line shows the redacted display path, not the real one.");
    }

    [TestMethod]
    public void ApplyDiagnostic_UnsavedReportIsStillCopyableAndSaysWhy()
    {
        ViiperDriverStatusViewModel viewModel =
            Loaded(Readiness(ViiperDriverReadinessState.Missing));

        viewModel.ApplyDiagnostic(new ViiperDriverDiagnosticRun
        {
            Text = "report body",
            FilePath = null,
            DisplayPath = @"%TEMP%\Thrum\viiper-driver-validation-1.txt",
            WriteError = "Access to the path is denied.",
        });

        Assert.IsTrue(viewModel.HasReport);
        Assert.IsFalse(viewModel.CanOpenReport);
        StringAssert.Contains(viewModel.ReportStatusText,
            "could not be saved");
    }

    // ---- Helpers ---------------------------------------------------------

    private static ViiperDriverStatusViewModel Loaded(
        ViiperDriverReadiness readiness)
    {
        var viewModel = new ViiperDriverStatusViewModel(() => readiness, null);
        viewModel.Load();
        return viewModel;
    }

    private static ViiperDriverReadiness Readiness(
        ViiperDriverReadinessState state,
        IReadOnlyList<string> reasons = null,
        IReadOnlyList<ViiperDriverComponentIdentity> identities = null,
        string releaseLabel = null,
        ViiperDriverTier? tier = null) =>
        new ViiperDriverReadiness(state, reasons, identities, releaseLabel,
            tier, DateTimeOffset.UtcNow);
}

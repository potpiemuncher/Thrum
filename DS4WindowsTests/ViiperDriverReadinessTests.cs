using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The four-state readiness mapping (plan task 2.1), exercised against fake
/// inspectors and trust verifiers exactly as
/// <see cref="ViiperDriverValidatorTests"/> does. Nothing here installs a
/// driver, elevates, attaches a device, or requires one to be installed.
///
/// <para>The property under test throughout is fail-closed: only a completed
/// inspection with a passing authoritative result may produce
/// ValidatedExperimental or Approved, and only a completed inspection that
/// found neither package may produce Missing.</para>
/// </summary>
[TestClass]
public class ViiperDriverReadinessTests
{
    private const string CanonicalUsbipPath = @"C:\Program Files\USBip\usbip.exe";

    // ---- Missing --------------------------------------------------------

    [TestMethod]
    public void Resolve_NoPackagesAtAll_IsMissing()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = NotFound(),
            Filter = NotFound(),
            ClientResolver = _ => new ViiperUsbipClientInfo { Found = false },
        });

        Assert.AreEqual(ViiperDriverReadinessState.Missing, readiness.State);
        Assert.AreEqual(0, readiness.Reasons.Count,
            "Proven absence is the whole explanation; the validator's report " +
            "wording is not a status-card reason.");
        Assert.AreEqual(0, readiness.Identities.Count);
        Assert.IsNull(readiness.ReleaseLabel);
        Assert.IsNull(readiness.Tier);
        Assert.IsFalse(readiness.IsManifestMatch);
        Assert.IsFalse(readiness.IsProductionApproved);
    }

    [TestMethod]
    public void Resolve_NoDriverPackagesButClientPresent_IsStillMissing()
    {
        // A stray usbip.exe is not a kernel driver. Absence of both packages is
        // proven, so the honest answer is the more restrictive state.
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = NotFound(),
            Filter = NotFound(),
        });

        Assert.AreEqual(ViiperDriverReadinessState.Missing, readiness.State);
    }

    // ---- DetectedUnvalidated: incomplete or mismatched pairs -------------

    [TestMethod]
    public void Resolve_FilterMissingFromPair_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = ValidHost(),
            Filter = NotFound(),
        });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "filter extension package was not found");
        CollectionAssert.AreEqual(
            new[] { "UDE host controller", "usbip.exe client" },
            readiness.Identities.Select(identity => identity.Component).ToArray(),
            "What was found must still be reported; what was not must not be " +
            "invented.");
    }

    [TestMethod]
    public void Resolve_HostMissingFromPair_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = NotFound(),
            Filter = ValidFilter(),
        });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "UDE host controller was not found");
    }

    [TestMethod]
    public void Resolve_MismatchedPair_IsDetectedUnvalidated()
    {
        // Host from 0.9.7.8, filter from 0.9.7.7: each is a listed identity,
        // the combination is not.
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = ValidHost(),
            Filter = With(ValidFilter(),
                driverVersion: new Version(21, 14, 27, 661)),
        });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness,
            "do not match a single observed baseline");
    }

    [TestMethod]
    public void Resolve_UnknownVersionPair_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = With(ValidHost(), driverVersion: new Version(2, 0, 0, 1)),
            Filter = With(ValidFilter(), driverVersion: new Version(2, 0, 0, 2)),
        });

        AssertUnvalidated(readiness);
        Assert.IsNull(readiness.ReleaseLabel,
            "An unlisted version must never be attributed to a release.");
    }

    [TestMethod]
    public void Resolve_HostPresentButNotStarted_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = With(ValidHost(), started: false),
            Filter = ValidFilter(),
        });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "not started");
    }

    // ---- DetectedUnvalidated: trust ------------------------------------

    [TestMethod]
    public void Resolve_TestSignedPackage_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector(),
            new FakeVerifier
            {
                DriverTrust = _ => new ViiperSignatureTrust
                {
                    Trusted = true,
                    TestSigned = true,
                    IsMicrosoftHardwareCompatibilityPublisher = true,
                },
            });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "test-signed");
    }

    [TestMethod]
    public void Resolve_UntrustedPackage_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector(),
            new FakeVerifier
            {
                DriverTrust = _ => ViiperSignatureTrust.Untrusted(
                    "chain could not be built"),
            });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "chain could not be built");
    }

    [TestMethod]
    public void Resolve_TrustVerificationThrows_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector(),
            new FakeVerifier
            {
                DriverTrust = _ =>
                    throw new InvalidOperationException("wintrust exploded"),
            });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "wintrust exploded");
    }

    [TestMethod]
    public void Resolve_WrongPublisher_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector(),
            new FakeVerifier
            {
                DriverTrust = _ => new ViiperSignatureTrust
                {
                    Trusted = true,
                    IsMicrosoftHardwareCompatibilityPublisher = false,
                    ObservedSignerCommonName = "Some Other Publisher",
                },
            });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness,
            "not signed by the Microsoft Hardware Compatibility Publisher");
    }

    // ---- DetectedUnvalidated: the inspection itself failed ---------------

    [TestMethod]
    public void Resolve_PackageInspectionThrows_IsDetectedUnvalidatedNotMissing()
    {
        // The load-bearing case. An unreadable machine is not an empty one, and
        // an inspection that throws must never surface as "fine".
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            HostResolver = _ => throw new InvalidOperationException(
                "SetupAPI refused"),
        });

        AssertUnvalidated(readiness);
        Assert.AreNotEqual(ViiperDriverReadinessState.Missing, readiness.State);
        AssertReasonContains(readiness, "could not be read");
        AssertReasonContains(readiness, "SetupAPI refused");
    }

    [TestMethod]
    public void Resolve_ClientInspectionThrows_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = ValidHost(),
            Filter = ValidFilter(),
            ClientResolver = _ =>
                throw new UnauthorizedAccessException("no read access"),
        });

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "no read access");
    }

    [TestMethod]
    public void Resolve_NullReport_IsDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness =
            ViiperDriverReadinessResolver.Resolve(null);

        AssertUnvalidated(readiness);
        Assert.AreEqual(1, readiness.Reasons.Count);
    }

    [TestMethod]
    public void Unavailable_IsNeverBetterThanDetectedUnvalidated()
    {
        ViiperDriverReadiness readiness =
            ViiperDriverReadinessResolver.Unavailable("the check did not run");

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "the check did not run");
    }

    // ---- ValidatedExperimental ------------------------------------------

    [TestMethod]
    public void Resolve_Exact0977Match_IsValidatedExperimental()
    {
        ViiperDriverReadiness readiness = Resolve(new FakeInspector
        {
            Host = With(ValidHost(),
                driverVersion: new Version(21, 14, 27, 907)),
            Filter = With(ValidFilter(),
                driverVersion: new Version(21, 14, 27, 661)),
            ClientResolver = _ => Client(new Version(0, 9, 7, 7)),
        });

        Assert.AreEqual(ViiperDriverReadinessState.ValidatedExperimental,
            readiness.State);
        Assert.AreEqual("0.9.7.7", readiness.ReleaseLabel);
        Assert.AreEqual(ViiperDriverTier.ExperimentalBaseline, readiness.Tier);
        Assert.IsTrue(readiness.IsManifestMatch);
        Assert.IsFalse(readiness.IsProductionApproved,
            "An experimental identity match is never production approval.");
        Assert.AreEqual(0, readiness.Reasons.Count);
    }

    [TestMethod]
    public void Resolve_Exact0978Match_IsValidatedExperimental()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector());

        Assert.AreEqual(ViiperDriverReadinessState.ValidatedExperimental,
            readiness.State);
        Assert.AreEqual("0.9.7.8", readiness.ReleaseLabel);
        Assert.AreEqual(ViiperDriverTier.ExperimentalBaseline, readiness.Tier);
        Assert.IsFalse(readiness.IsProductionApproved);
    }

    [TestMethod]
    public void Resolve_Match_ReportsEveryIdentityFieldTheCardShows()
    {
        ViiperDriverReadiness readiness = Resolve(ValidInspector());

        Assert.AreEqual(3, readiness.Identities.Count);
        ViiperDriverComponentIdentity host = readiness.Identities[0];
        Assert.AreEqual("UDE host controller", host.Component);
        Assert.AreEqual("USBIP-WIN2", Field(host, "INF provider"));
        Assert.AreEqual("usbip2_ude.inf", Field(host, "INF name"));
        Assert.AreEqual("1.45.29.368", Field(host, "DriverVer"));
        Assert.AreEqual("usbip2_ude", Field(host, "Service"));
        StringAssert.Contains(Field(host, "Catalog trust"), "usbip2_ude.cat");
        StringAssert.Contains(Field(host, "Catalog trust"), "trusted");

        Assert.AreEqual("Filter extension", readiness.Identities[1].Component);
        Assert.AreEqual("usbip.exe client", readiness.Identities[2].Component);
        Assert.AreEqual("0.9.7.8",
            Field(readiness.Identities[2], "ProductVersion"));

        // The rendered line, so adjacent XAML Runs cannot reintroduce
        // "DriverVer : 1.45.29.368".
        Assert.AreEqual("DriverVer: 1.45.29.368", host.Fields
            .Single(field => field.Label == "DriverVer").Display);
    }

    [TestMethod]
    public void Resolve_NeverSurfacesTheDriverStoreTrustPath()
    {
        // The gate's TrustEvaluationPath is a driver-store path. It exists so
        // WinVerifyTrust has something to evaluate; it must not reach the UI,
        // a log, or a report.
        ViiperDriverReadiness readiness = Resolve(ValidInspector());

        IEnumerable<string> everything = readiness.Reasons.Concat(
            readiness.Identities.SelectMany(identity =>
                identity.Fields.Select(field => field.Label + " " + field.Value)));
        foreach (string text in everything)
        {
            Assert.IsFalse(
                text.IndexOf(@"C:\store", StringComparison.OrdinalIgnoreCase) >= 0,
                "A driver-store path reached user-visible text: " + text);
        }
    }

    // ---- Approved (no real manifest entry reaches it, by design) ---------

    [TestMethod]
    public void Resolve_HypotheticalProductionEntry_IsApproved()
    {
        // The real manifest deliberately has no Production entry, so the tier
        // is exercised against a fabricated manifest rather than by weakening
        // the shipped one.
        ViiperDriverManifest manifest = ViiperDriverManifest.FromReleases(
            new[] { ProductionRelease() });
        var validator = new ViiperDriverValidator(manifest, ValidInspector(),
            new FakeVerifier());

        ViiperDriverReadiness readiness = ViiperDriverReadinessResolver.Resolve(
            validator.Inspect(CanonicalUsbipPath));

        Assert.AreEqual(ViiperDriverReadinessState.Approved, readiness.State);
        Assert.AreEqual("9.9.9.9", readiness.ReleaseLabel);
        Assert.AreEqual(ViiperDriverTier.Production, readiness.Tier);
        Assert.IsTrue(readiness.IsProductionApproved);
        Assert.IsTrue(readiness.IsManifestMatch);
        Assert.AreEqual(0, readiness.Reasons.Count);
    }

    [TestMethod]
    public void RealManifest_HasNoProductionEntry()
    {
        // Guards the Approved state against arriving by accident: it may only
        // ever appear after a deliberate manifest edit.
        Assert.IsFalse(ViiperDriverManifest.ObservedBaselines.Releases.Any(
            release => release.Tier == ViiperDriverTier.Production));
    }

    // ---- Provider caching ------------------------------------------------

    [TestMethod]
    public void Provider_EvaluatesOncePerSessionUntilRefreshed()
    {
        int inspections = 0;
        var provider = new ViiperDriverReadinessProvider(() =>
        {
            inspections++;
            return CreateValidator(ValidInspector(), new FakeVerifier())
                .Inspect(CanonicalUsbipPath);
        });

        ViiperDriverReadiness first = provider.Get();
        ViiperDriverReadiness second = provider.Get();

        Assert.AreSame(first, second, "The session cache must be reused.");
        Assert.AreEqual(1, inspections);
        Assert.AreEqual(1, provider.EvaluationCount);

        ViiperDriverReadiness refreshed = provider.Refresh();

        Assert.AreNotSame(first, refreshed);
        Assert.AreEqual(2, inspections);
        Assert.AreEqual(2, provider.EvaluationCount);
        Assert.AreSame(refreshed, provider.Get(),
            "A refresh replaces the cache rather than bypassing it.");
    }

    [TestMethod]
    public void Provider_InspectionThrowing_FailsClosedInsteadOfPropagating()
    {
        var provider = new ViiperDriverReadinessProvider(
            () => throw new InvalidOperationException("gate exploded"));

        ViiperDriverReadiness readiness = provider.Get();

        AssertUnvalidated(readiness);
        AssertReasonContains(readiness, "gate exploded");
    }

    [TestMethod]
    public void Provider_Adopt_ReusesAnAlreadyPaidForInspection()
    {
        var provider = new ViiperDriverReadinessProvider(
            () => throw new InvalidOperationException(
                "Adopt must not trigger a fresh inspection"));

        ViiperDriverValidationReport report =
            CreateValidator(ValidInspector(), new FakeVerifier())
                .Inspect(CanonicalUsbipPath);

        ViiperDriverReadiness adopted = provider.Adopt(report);

        Assert.AreEqual(ViiperDriverReadinessState.ValidatedExperimental,
            adopted.State);
        Assert.AreSame(adopted, provider.Get());
    }

    // ---- Helpers ---------------------------------------------------------

    private static ViiperDriverReadiness Resolve(FakeInspector inspector,
        FakeVerifier verifier = null) =>
        ViiperDriverReadinessResolver.Resolve(
            CreateValidator(inspector, verifier ?? new FakeVerifier())
                .Inspect(CanonicalUsbipPath));

    private static ViiperDriverValidator CreateValidator(
        IDriverPackageInspector inspector, IAuthenticodeVerifier verifier) =>
        new ViiperDriverValidator(ViiperDriverManifest.ObservedBaselines,
            inspector, verifier);

    private static void AssertUnvalidated(ViiperDriverReadiness readiness)
    {
        Assert.AreEqual(ViiperDriverReadinessState.DetectedUnvalidated,
            readiness.State,
            "Reasons: " + string.Join(" | ", readiness.Reasons));
        Assert.IsTrue(readiness.Reasons.Count > 0,
            "An unvalidated state must carry its reasons.");
        Assert.IsFalse(readiness.IsManifestMatch);
        Assert.IsFalse(readiness.IsProductionApproved);
    }

    private static void AssertReasonContains(ViiperDriverReadiness readiness,
        string fragment)
    {
        Assert.IsTrue(readiness.Reasons.Any(reason =>
            reason.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0),
            "No reason mentioned \"" + fragment + "\". Reasons: " +
            string.Join(" | ", readiness.Reasons));
    }

    private static string Field(ViiperDriverComponentIdentity identity,
        string label) =>
        identity.Fields.Single(field =>
            string.Equals(field.Label, label, StringComparison.Ordinal)).Value;

    private static ViiperDriverRelease ProductionRelease() =>
        new ViiperDriverRelease(
            releaseLabel: "9.9.9.9",
            tier: ViiperDriverTier.Production,
            driverSignerPolicy:
                ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher,
            udeHostController: new ViiperDriverPackageSpec("usbip2_ude.inf",
                "USBIP-WIN2", new Version(1, 45, 29, 368)),
            filterExtension: new ViiperDriverPackageSpec("usbip2_filter.inf",
                "USBIP-WIN2", new Version(1, 45, 28, 868)),
            userspaceClient: new ViiperUsbipClientSpec("usbip.exe",
                new Version(0, 9, 7, 8), requireAuthenticode: true),
            architectures: new[] { ViiperDriverArchitecture.X64 });

    private static FakeInspector ValidInspector() => new FakeInspector
    {
        Host = ValidHost(),
        Filter = ValidFilter(),
    };

    private static ViiperDriverPackageInfo ValidHost() =>
        new ViiperDriverPackageInfo
        {
            Found = true,
            HardwareId = @"ROOT\USBIP_WIN2\UDE",
            InfName = "usbip2_ude.inf",
            Provider = "USBIP-WIN2",
            DriverVersion = new Version(1, 45, 29, 368),
            Service = "usbip2_ude",
            CatalogFile = "usbip2_ude.cat",
            Architecture = ViiperDriverArchitecture.X64,
            DeviceNodePresent = true,
            Started = true,
            TrustEvaluationPath = @"C:\store\usbip2_ude.cat",
        };

    private static ViiperDriverPackageInfo ValidFilter() =>
        new ViiperDriverPackageInfo
        {
            Found = true,
            InfName = "usbip2_filter.inf",
            Provider = "USBIP-WIN2",
            DriverVersion = new Version(1, 45, 28, 868),
            Service = "usbip2_filter",
            CatalogFile = "usbip2_filter.cat",
            Architecture = ViiperDriverArchitecture.X64,
            DeviceNodePresent = true,
            Started = true,
            TrustEvaluationPath = @"C:\store\usbip2_filter.cat",
        };

    private static ViiperUsbipClientInfo Client(Version productVersion) =>
        new ViiperUsbipClientInfo
        {
            Found = true,
            FileName = "usbip.exe",
            ProductVersion = productVersion,
        };

    private static ViiperDriverPackageInfo NotFound() =>
        new ViiperDriverPackageInfo { Found = false };

    private static ViiperDriverPackageInfo With(
        ViiperDriverPackageInfo source, Version driverVersion = null,
        bool? started = null) =>
        new ViiperDriverPackageInfo
        {
            Found = source.Found,
            HardwareId = source.HardwareId,
            InfName = source.InfName,
            Provider = source.Provider,
            DriverVersion = driverVersion ?? source.DriverVersion,
            Service = source.Service,
            CatalogFile = source.CatalogFile,
            Architecture = source.Architecture,
            DeviceNodePresent = source.DeviceNodePresent,
            Started = started ?? source.Started,
            TrustEvaluationPath = source.TrustEvaluationPath,
        };

    private sealed class FakeInspector : IDriverPackageInspector
    {
        public ViiperDriverPackageInfo Host { get; set; }
        public ViiperDriverPackageInfo Filter { get; set; }
        public Func<string, ViiperDriverPackageInfo> HostResolver { get; set; }
        public Func<string, ViiperUsbipClientInfo> ClientResolver { get; set; }

        public ViiperDriverPackageInfo InspectHostController(string hardwareId) =>
            HostResolver != null
                ? HostResolver(hardwareId)
                : Host ?? NotFound();

        public ViiperDriverPackageInfo InspectFilterExtension(string infName) =>
            Filter ?? NotFound();

        public ViiperUsbipClientInfo InspectUsbipClient(string executablePath) =>
            ClientResolver != null
                ? ClientResolver(executablePath)
                : Client(new Version(0, 9, 7, 8));
    }

    private sealed class FakeVerifier : IAuthenticodeVerifier
    {
        public Func<ViiperDriverPackageInfo, ViiperSignatureTrust> DriverTrust
        { get; set; }

        public Func<string, ViiperSignatureTrust> FileTrust { get; set; }

        public ViiperSignatureTrust VerifyDriverPackage(
            ViiperDriverPackageInfo package) =>
            DriverTrust != null
                ? DriverTrust(package)
                : new ViiperSignatureTrust
                {
                    Trusted = true,
                    IsMicrosoftHardwareCompatibilityPublisher = true,
                    ObservedSignerCommonName = ViiperDriverManifest
                        .MicrosoftHardwareCompatibilityPublisherCommonName,
                };

        public ViiperSignatureTrust VerifyFile(string filePath) =>
            FileTrust != null
                ? FileTrust(filePath)
                : new ViiperSignatureTrust { Trusted = true };
    }
}

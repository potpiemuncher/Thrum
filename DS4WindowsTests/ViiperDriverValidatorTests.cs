using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

[TestClass]
public class ViiperDriverValidatorTests
{
    private const string CanonicalUsbipPath = @"C:\Program Files\USBip\usbip.exe";
    private const string StableHardwareId = @"ROOT\USBIP_WIN2\UDE";

    // ---- Manifest structure --------------------------------------------

    [TestMethod]
    public void Manifest_ExposesOnlyUnapprovedExperimentalBaselines()
    {
        ViiperDriverManifest manifest = ViiperDriverManifest.ObservedBaselines;

        Assert.AreEqual(2, manifest.Releases.Count);
        CollectionAssert.AreEqual(
            new[] { "0.9.7.7", "0.9.7.8" },
            manifest.Releases.Select(release => release.ReleaseLabel).ToArray());
        foreach (ViiperDriverRelease release in manifest.Releases)
        {
            Assert.AreEqual(ViiperDriverTier.ExperimentalBaseline, release.Tier);
            Assert.IsFalse(release.IsRunAllowed,
                "An observed experimental baseline is not production approval.");
            Assert.AreEqual(
                ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher,
                release.DriverSignerPolicy);
        }
    }

    [TestMethod]
    public void Manifest_HbashtonInstallerBaseline_UsesExactX64PackageVersions()
    {
        ViiperDriverRelease release = Release("0.9.7.7");

        Assert.AreEqual(new Version(21, 14, 27, 907),
            release.UdeHostController.DriverVersion);
        Assert.AreEqual(new Version(21, 14, 27, 661),
            release.FilterExtension.DriverVersion);
        Assert.AreEqual(new Version(0, 9, 7, 7),
            release.UserspaceClient.ProductVersion);
        Assert.AreEqual("usbip2_ude.inf", release.UdeHostController.InfName);
        Assert.AreEqual("usbip2_filter.inf", release.FilterExtension.InfName);
        Assert.AreEqual("USBIP-WIN2", release.UdeHostController.Provider);
        Assert.AreEqual("USBIP-WIN2", release.FilterExtension.Provider);
    }

    [TestMethod]
    public void Manifest_KnownRiskReleaseLabelDiffersFromDriverVersions()
    {
        ViiperDriverRelease release = Release("0.9.7.8");

        // §3: the upstream release label is not the value Windows reports per
        // package. Keep the three identities distinct.
        Assert.AreEqual("0.9.7.8", release.ReleaseLabel);
        Assert.AreEqual(new Version(1, 45, 29, 368),
            release.UdeHostController.DriverVersion);
        Assert.AreEqual(new Version(1, 45, 28, 868),
            release.FilterExtension.DriverVersion);
        Assert.AreEqual(new Version(0, 9, 7, 8),
            release.UserspaceClient.ProductVersion);
        Assert.AreEqual("usbip2_ude.inf", release.UdeHostController.InfName);
        Assert.AreEqual("usbip2_filter.inf", release.FilterExtension.InfName);
        Assert.AreEqual("USBIP-WIN2", release.UdeHostController.Provider);
        Assert.AreEqual("USBIP-WIN2", release.FilterExtension.Provider);
    }

    [TestMethod]
    public void Manifest_ArchitecturesReflectPackagesActuallyInspected()
    {
        ViiperDriverRelease hbashtonInstaller = Release("0.9.7.7");
        ViiperDriverRelease knownRisk = Release("0.9.7.8");

        Assert.IsTrue(hbashtonInstaller.SupportsArchitecture(
            ViiperDriverArchitecture.X64));
        Assert.IsFalse(hbashtonInstaller.SupportsArchitecture(
            ViiperDriverArchitecture.X86));
        Assert.IsTrue(knownRisk.SupportsArchitecture(
            ViiperDriverArchitecture.X64));
        Assert.IsTrue(knownRisk.SupportsArchitecture(
            ViiperDriverArchitecture.X86));
    }

    // ---- Valid matches --------------------------------------------------

    [DataTestMethod]
    [DataRow(ViiperDriverArchitecture.X64)]
    [DataRow(ViiperDriverArchitecture.X86)]
    public void Validate_ValidBaseline_PassesForArchitecture(
        ViiperDriverArchitecture architecture)
    {
        var inspector = new FakeInspector
        {
            Host = ValidHost(architecture),
            Filter = ValidFilter(architecture),
        };
        ViiperDriverValidator validator = CreateValidator(inspector,
            new FakeVerifier());

        ViiperDriverValidationResult result = validator.Validate(CanonicalUsbipPath);

        Assert.IsTrue(result.Passed, result.Diagnostic);
        Assert.AreEqual("0.9.7.8", result.ReleaseLabel);
        Assert.AreEqual(ViiperDriverTier.ExperimentalBaseline, result.Tier);
        Assert.IsTrue(result.RequiresExperimentalConfirmation,
            "0.9.7.8 must remain gated behind the experimental confirmation.");
    }

    [TestMethod]
    public void Validate_HbashtonInstallerBaseline_MatchesButDoesNotApproveRunning()
    {
        var inspector = new FakeInspector
        {
            Host = With(ValidHost(),
                driverVersion: new Version(21, 14, 27, 907)),
            Filter = With(ValidFilter(),
                driverVersion: new Version(21, 14, 27, 661)),
            ClientResolver = _ => new ViiperUsbipClientInfo
            {
                Found = true,
                FileName = "usbip.exe",
                ProductVersion = new Version(0, 9, 7, 7),
            },
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(
                CanonicalUsbipPath);

        Assert.IsTrue(result.Passed, result.Diagnostic);
        Assert.AreEqual("0.9.7.7", result.ReleaseLabel);
        Assert.AreEqual(ViiperDriverTier.ExperimentalBaseline, result.Tier);
        Assert.IsTrue(result.RequiresExperimentalConfirmation);
        Assert.IsFalse(Release(result.ReleaseLabel).IsRunAllowed,
            "Identity and trust matching must not approve driver execution.");
    }

    [TestMethod]
    public void Validate_QueriesStableHardwareId_NotInstancePath()
    {
        var inspector = new FakeInspector
        {
            Host = ValidHost(),
            Filter = ValidFilter(),
        };
        CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        Assert.AreEqual(StableHardwareId, inspector.RequestedHostHardwareId);
        Assert.AreEqual("usbip2_filter.inf", inspector.RequestedFilterInf);
    }

    // ---- Hardware ID / presence ----------------------------------------

    [TestMethod]
    public void Validate_HostFoundOnlyUnderChangedRootInstance_FailsNotFound()
    {
        // The device exists on the machine, but only under a machine-specific
        // instance such as ROOT\USB\0002; the stable hardware ID query misses.
        var inspector = new FakeInspector
        {
            HostResolver = hardwareId =>
                string.Equals(hardwareId, @"ROOT\USB\0002",
                    StringComparison.OrdinalIgnoreCase)
                    ? ValidHost()
                    : NotFoundPackage(),
            Filter = ValidFilter(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.NotFound);
    }

    [TestMethod]
    public void Validate_MissingHost_FailsNotFound()
    {
        var inspector = new FakeInspector
        {
            Host = NotFoundPackage(),
            Filter = ValidFilter(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.NotFound);
    }

    [TestMethod]
    public void Validate_MissingFilter_FailsNotFound()
    {
        var inspector = new FakeInspector
        {
            Host = ValidHost(),
            Filter = NotFoundPackage(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.FilterExtension,
            ViiperDriverFailureReason.NotFound);
    }

    [TestMethod]
    public void Validate_MissingBothPackages_FailsNotFound()
    {
        var inspector = new FakeInspector
        {
            Host = NotFoundPackage(),
            Filter = NotFoundPackage(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.NotFound);
    }

    // ---- Provider / version / mixed pairs ------------------------------

    [TestMethod]
    public void Validate_WrongHostProvider_FailsWrongProvider()
    {
        ViiperDriverPackageInfo host = ValidHost();
        var inspector = new FakeInspector
        {
            Host = With(host, provider: "Contoso Drivers"),
            Filter = ValidFilter(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.WrongProvider);
    }

    [TestMethod]
    public void Validate_WrongFilterProvider_FailsWrongProvider()
    {
        var inspector = new FakeInspector
        {
            Host = ValidHost(),
            Filter = With(ValidFilter(), provider: "Contoso Drivers"),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.FilterExtension,
            ViiperDriverFailureReason.WrongProvider);
    }

    [TestMethod]
    public void Validate_MixedVersionPair_FilterFromDifferentRelease_FailsMixedPair()
    {
        var inspector = new FakeInspector
        {
            Host = ValidHost(),
            Filter = With(ValidFilter(), driverVersion: new Version(1, 46, 0, 0)),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.FilterExtension,
            ViiperDriverFailureReason.MixedPair);
    }

    [TestMethod]
    public void Validate_MixedVersionPair_HostFromDifferentRelease_FailsMixedPair()
    {
        var inspector = new FakeInspector
        {
            Host = With(ValidHost(), driverVersion: new Version(1, 46, 0, 0)),
            Filter = ValidFilter(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.MixedPair);
    }

    [TestMethod]
    public void Validate_BothPackagesUnknownVersion_FailsWrongVersion()
    {
        var inspector = new FakeInspector
        {
            Host = With(ValidHost(), driverVersion: new Version(1, 40, 0, 0)),
            Filter = With(ValidFilter(), driverVersion: new Version(1, 40, 0, 0)),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.WrongVersion);
    }

    // ---- Health ---------------------------------------------------------

    [TestMethod]
    public void Validate_HostPresentButNotStarted_FailsUnhealthy()
    {
        var inspector = new FakeInspector
        {
            Host = With(ValidHost(), started: false),
            Filter = ValidFilter(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.Unhealthy);
    }

    // ---- Signatures (fake verifier) ------------------------------------

    [TestMethod]
    public void Validate_HostUntrustedSignature_Fails()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => ViiperSignatureTrust.Untrusted("not trusted"),
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
    }

    [TestMethod]
    public void Validate_HostExpiredSignature_Fails()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust { Expired = true },
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
        StringAssert.Contains(result.Diagnostic, "expired");
    }

    [TestMethod]
    public void Validate_HostRevokedSignature_Fails()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust { Revoked = true },
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
        StringAssert.Contains(result.Diagnostic, "revoked");
    }

    [TestMethod]
    public void Validate_HostDeveloperSigned_Fails()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust { DeveloperSigned = true },
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
    }

    [TestMethod]
    public void Validate_HostTestSigned_Fails()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust { TestSigned = true },
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
        StringAssert.Contains(result.Diagnostic, "test-signed");
    }

    [TestMethod]
    public void Validate_HostTrustedButNotMicrosoftHwcp_Fails()
    {
        // Trusted under normal chain policy, but NOT the Microsoft Hardware
        // Compatibility Publisher. Must be refused even though the signer chain
        // is trusted (guards against a "contains Microsoft" style acceptance).
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust
            {
                Trusted = true,
                IsMicrosoftHardwareCompatibilityPublisher = false,
            },
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.UntrustedSignature);
        StringAssert.Contains(result.Diagnostic, "Microsoft Hardware Compatibility Publisher");
    }

    [TestMethod]
    public void Validate_FilterUntrustedSignature_Fails()
    {
        // Host is trusted HWCP; only the filter is untrusted.
        var verifier = new FakeVerifier
        {
            DriverTrust = package =>
                package.InfName == "usbip2_filter.inf"
                    ? ViiperSignatureTrust.Untrusted("bad")
                    : TrustedHwcp(),
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.FilterExtension,
            ViiperDriverFailureReason.UntrustedSignature);
    }

    // ---- usbip.exe client ----------------------------------------------

    [TestMethod]
    public void Validate_UsbipWrongProductVersion_Fails()
    {
        var inspector = ValidInspector();
        inspector.ClientResolver = _ => new ViiperUsbipClientInfo
        {
            Found = true,
            FileName = "usbip.exe",
            ProductVersion = new Version(0, 9, 7, 7),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UsbipClient,
            ViiperDriverFailureReason.WrongVersion);
    }

    [TestMethod]
    public void Validate_UsbipMissing_Fails()
    {
        var inspector = ValidInspector();
        inspector.ClientResolver = _ => new ViiperUsbipClientInfo { Found = false };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UsbipClient,
            ViiperDriverFailureReason.NotFound);
    }

    [TestMethod]
    public void Validate_UsbipUntrustedAuthenticode_Fails()
    {
        var verifier = new FakeVerifier
        {
            FileTrust = _ => ViiperSignatureTrust.Untrusted("no signature"),
        };

        ViiperDriverValidationResult result =
            CreateValidator(ValidInspector(), verifier).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UsbipClient,
            ViiperDriverFailureReason.UntrustedSignature);
    }

    [TestMethod]
    public void Validate_UsbipCanonicalPath_Passes_NonCanonicalPath_FailsClosed()
    {
        var inspector = ValidInspector();
        // The real inspector resolves/reads the file; a non-canonical path
        // reads as not-found and fails closed.
        inspector.ClientResolver = path =>
            string.Equals(path, CanonicalUsbipPath, StringComparison.OrdinalIgnoreCase)
                ? ValidClient()
                : new ViiperUsbipClientInfo { Found = false };
        ViiperDriverValidator validator =
            CreateValidator(inspector, new FakeVerifier());

        Assert.IsTrue(validator.Validate(CanonicalUsbipPath).Passed);

        ViiperDriverValidationResult nonCanonical = validator.Validate(
            @"C:\Program Files\USBip\..\USBip\usbip.exe");
        AssertFail(nonCanonical, ViiperDriverComponent.UsbipClient,
            ViiperDriverFailureReason.NotFound);
    }

    [TestMethod]
    public void Validate_PassesUsbipPathToInspectorAndVerifier()
    {
        var inspector = ValidInspector();
        var verifier = new FakeVerifier();

        CreateValidator(inspector, verifier).Validate(CanonicalUsbipPath);

        Assert.AreEqual(CanonicalUsbipPath, inspector.ClientPathSeen);
        Assert.AreEqual(CanonicalUsbipPath, verifier.FilePathSeen);
    }

    // ---- Inspection failure fails closed -------------------------------

    [TestMethod]
    public void Validate_InspectorThrows_FailsClosed()
    {
        var inspector = new FakeInspector
        {
            HostResolver = _ => throw new InvalidOperationException("setupapi failure"),
        };

        ViiperDriverValidationResult result =
            CreateValidator(inspector, new FakeVerifier()).Validate(CanonicalUsbipPath);

        AssertFail(result, ViiperDriverComponent.UdeHostController,
            ViiperDriverFailureReason.InspectionFailed);
    }

    // ---- Helpers --------------------------------------------------------

    private static ViiperDriverValidator CreateValidator(
        IDriverPackageInspector inspector, IAuthenticodeVerifier verifier) =>
        new ViiperDriverValidator(ViiperDriverManifest.ObservedBaselines,
            inspector, verifier);

    private static ViiperDriverRelease Release(string label) =>
        ViiperDriverManifest.ObservedBaselines.Releases.Single(release =>
            string.Equals(release.ReleaseLabel, label,
                StringComparison.Ordinal));

    private static FakeInspector ValidInspector() => new FakeInspector
    {
        Host = ValidHost(),
        Filter = ValidFilter(),
    };

    private static ViiperDriverPackageInfo ValidHost(
        ViiperDriverArchitecture architecture = ViiperDriverArchitecture.X64) =>
        new ViiperDriverPackageInfo
        {
            Found = true,
            HardwareId = StableHardwareId,
            InfName = "usbip2_ude.inf",
            Provider = "USBIP-WIN2",
            DriverVersion = new Version(1, 45, 29, 368),
            Service = "usbip2_ude",
            CatalogFile = "usbip2_ude.cat",
            Architecture = architecture,
            DeviceNodePresent = true,
            Started = true,
            TrustEvaluationPath = @"C:\store\usbip2_ude.cat",
        };

    private static ViiperDriverPackageInfo ValidFilter(
        ViiperDriverArchitecture architecture = ViiperDriverArchitecture.X64) =>
        new ViiperDriverPackageInfo
        {
            Found = true,
            InfName = "usbip2_filter.inf",
            Provider = "USBIP-WIN2",
            DriverVersion = new Version(1, 45, 28, 868),
            Service = "usbip2_filter",
            CatalogFile = "usbip2_filter.cat",
            Architecture = architecture,
            DeviceNodePresent = true,
            Started = true,
            TrustEvaluationPath = @"C:\store\usbip2_filter.cat",
        };

    private static ViiperUsbipClientInfo ValidClient() =>
        new ViiperUsbipClientInfo
        {
            Found = true,
            FileName = "usbip.exe",
            ProductVersion = new Version(0, 9, 7, 8),
        };

    private static ViiperDriverPackageInfo NotFoundPackage() =>
        new ViiperDriverPackageInfo { Found = false };

    private static ViiperSignatureTrust TrustedHwcp() =>
        new ViiperSignatureTrust
        {
            Trusted = true,
            IsMicrosoftHardwareCompatibilityPublisher = true,
        };

    private static ViiperDriverPackageInfo With(
        ViiperDriverPackageInfo source, string provider = null,
        Version driverVersion = null, bool? started = null) =>
        new ViiperDriverPackageInfo
        {
            Found = source.Found,
            HardwareId = source.HardwareId,
            InfName = source.InfName,
            Provider = provider ?? source.Provider,
            DriverVersion = driverVersion ?? source.DriverVersion,
            Service = source.Service,
            CatalogFile = source.CatalogFile,
            Architecture = source.Architecture,
            DeviceNodePresent = source.DeviceNodePresent,
            Started = started ?? source.Started,
            TrustEvaluationPath = source.TrustEvaluationPath,
        };

    private static void AssertFail(ViiperDriverValidationResult result,
        ViiperDriverComponent component, ViiperDriverFailureReason reason)
    {
        Assert.IsFalse(result.Passed, "Expected validation to fail: " +
            result.Diagnostic);
        Assert.AreEqual(component, result.FailedComponent, result.Diagnostic);
        Assert.AreEqual(reason, result.Reason, result.Diagnostic);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Diagnostic),
            "A specific diagnostic is required.");
        Assert.IsNull(result.Tier);
        Assert.IsNull(result.ReleaseLabel);
    }

    private sealed class FakeInspector : IDriverPackageInspector
    {
        public ViiperDriverPackageInfo Host { get; set; }
        public ViiperDriverPackageInfo Filter { get; set; }
        public Func<string, ViiperDriverPackageInfo> HostResolver { get; set; }
        public Func<string, ViiperUsbipClientInfo> ClientResolver { get; set; }
        public string RequestedHostHardwareId { get; private set; }
        public string RequestedFilterInf { get; private set; }
        public string ClientPathSeen { get; private set; }

        public ViiperDriverPackageInfo InspectHostController(string hardwareId)
        {
            RequestedHostHardwareId = hardwareId;
            if (HostResolver != null)
                return HostResolver(hardwareId);
            return Host ?? new ViiperDriverPackageInfo { Found = false };
        }

        public ViiperDriverPackageInfo InspectFilterExtension(string infName)
        {
            RequestedFilterInf = infName;
            return Filter ?? new ViiperDriverPackageInfo { Found = false };
        }

        public ViiperUsbipClientInfo InspectUsbipClient(string executablePath)
        {
            ClientPathSeen = executablePath;
            if (ClientResolver != null)
                return ClientResolver(executablePath);
            return ValidClient();
        }
    }

    private sealed class FakeVerifier : IAuthenticodeVerifier
    {
        public Func<ViiperDriverPackageInfo, ViiperSignatureTrust> DriverTrust
        { get; set; }
        public Func<string, ViiperSignatureTrust> FileTrust { get; set; }
        public string FilePathSeen { get; private set; }

        public ViiperSignatureTrust VerifyDriverPackage(
            ViiperDriverPackageInfo package)
        {
            return DriverTrust != null ? DriverTrust(package) : TrustedHwcp();
        }

        public ViiperSignatureTrust VerifyFile(string filePath)
        {
            FilePathSeen = filePath;
            return FileTrust != null
                ? FileTrust(filePath)
                : new ViiperSignatureTrust { Trusted = true };
        }
    }
}

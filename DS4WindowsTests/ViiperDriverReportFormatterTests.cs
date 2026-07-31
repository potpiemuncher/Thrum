using System;
using System.Text.RegularExpressions;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// Covers the composition of the read-only
/// <c>-viiperdriverdiagnostic</c> report. The
/// report must show observed versus expected values for all three components
/// even on success, because the gate's OS probes have to be confirmed against a
/// real install. All observations here are fabricated; the real SetupAPI and
/// WinVerifyTrust paths are deliberately not exercised.
/// </summary>
[TestClass]
public class ViiperDriverReportFormatterTests
{
    private const string CanonicalUsbipPath = @"C:\Program Files\USBip\usbip.exe";
    private const string ExpectedPublisher =
        ViiperDriverManifest.MicrosoftHardwareCompatibilityPublisherCommonName;
    private const string HostTrustPath = @"C:\store\usbip2_ude.cat";
    private const string FilterTrustPath = @"C:\store\usbip2_filter.cat";

    // ---- All pass -------------------------------------------------------

    [TestMethod]
    public void Report_AllPass_ShowsObservedEqualsExpectedForEveryComparison()
    {
        string text = Format(ValidInspector(), ValidVerifier());

        StringAssert.Contains(text, "RESULT: PASS");
        StringAssert.Contains(text, "not production approval");
        StringAssert.Matches(text, new Regex(@"observed mismatches\s+: 0"));
        foreach (string line in ComparisonLines(text))
        {
            StringAssert.EndsWith(line, "[OK]",
                "Every comparison must match on an all-pass report: " + line);
        }
    }

    [TestMethod]
    public void Report_AllPass_ShowsObservedAndExpectedValuesForAllThreeComponents()
    {
        string text = Format(ValidInspector(), ValidVerifier());

        string host = HostSection(text);
        AssertComparison(host, "INF name", "usbip2_ude.inf", "usbip2_ude.inf");
        AssertComparison(host, "provider", "USBIP-WIN2", "USBIP-WIN2");
        AssertComparison(host, "DriverVer", "1.45.29.368", "1.45.29.368");
        AssertComparison(host, "architecture", "X64", "X64 or X86");
        AssertComparison(host, "device node present", "yes", "yes");
        AssertComparison(host, "started, no problem", "yes", "yes");
        AssertComparison(host, "driver store target", "resolved", "resolved");
        AssertComparison(host, "signature trusted", "yes", "yes");
        AssertComparison(host, "publisher accepted", "yes", "yes");
        AssertComparison(host, "signer common name", ExpectedPublisher,
            ExpectedPublisher);

        string filter = FilterSection(text);
        AssertComparison(filter, "INF name", "usbip2_filter.inf",
            "usbip2_filter.inf");
        AssertComparison(filter, "provider", "USBIP-WIN2", "USBIP-WIN2");
        AssertComparison(filter, "DriverVer", "1.45.28.868", "1.45.28.868");
        AssertComparison(filter, "driver store target", "resolved", "resolved");
        AssertComparison(filter, "publisher accepted", "yes", "yes");

        string client = ClientSection(text);
        StringAssert.Contains(client, CanonicalUsbipPath);
        AssertComparison(client, "file name", "usbip.exe", "usbip.exe");
        AssertComparison(client, "ProductVersion", "0.9.7.8", "0.9.7.8");
        AssertComparison(client, "signature trusted", "yes", "yes");
    }

    [TestMethod]
    public void Report_AllPass_StatesTheObservedBaselineIsNotProductionApproval()
    {
        string text = Format(ValidInspector(), ValidVerifier());

        StringAssert.Contains(text, "usbip-win2 0.9.7.8 (ExperimentalBaseline)");
        StringAssert.Contains(text, "does not change VIIPER readiness");
    }

    [TestMethod]
    public void Report_HbashtonInstallerBaseline_UsesItsMatchedExpectedValues()
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

        string text = Format(inspector, ValidVerifier());

        StringAssert.Contains(text, "RESULT: PASS");
        StringAssert.Contains(text, "usbip-win2 0.9.7.7 (ExperimentalBaseline)");
        AssertComparison(HostSection(text), "DriverVer",
            "21.14.27.907", "21.14.27.907");
        AssertComparison(FilterSection(text), "DriverVer",
            "21.14.27.661", "21.14.27.661");
        AssertComparison(ClientSection(text), "ProductVersion",
            "0.9.7.7", "0.9.7.7");
        StringAssert.Matches(text, new Regex(@"observed mismatches\s+: 0"));
        StringAssert.Contains(text, "not production approval");
    }

    // ---- Version mismatch -----------------------------------------------

    [TestMethod]
    public void Report_VersionMismatch_ShowsBothObservedAndExpectedVersions()
    {
        var inspector = ValidInspector();
        inspector.Host = With(ValidHost(), driverVersion: new Version(1, 45, 29, 999));

        string text = Format(inspector, ValidVerifier());

        StringAssert.Contains(text, "RESULT: FAIL");
        string line = AssertComparison(HostSection(text), "DriverVer",
            "1.45.29.999", "1.45.29.368");
        StringAssert.EndsWith(line, "[MISMATCH]");
        StringAssert.Matches(text, new Regex(@"observed mismatches\s+: 1"));
        // The filter's own version still reads back so a mixed pair is visible.
        AssertComparison(FilterSection(text), "DriverVer", "1.45.28.868",
            "1.45.28.868");
        StringAssert.Contains(text, "MixedPair");
    }

    [TestMethod]
    public void Report_WrongProviderAndInf_ShowsBothObservedAndExpectedValues()
    {
        var inspector = ValidInspector();
        inspector.Host = With(ValidHost(), provider: "Contoso Drivers",
            infName: "oem42.inf");

        string text = Format(inspector, ValidVerifier());

        StringAssert.Contains(text, "RESULT: FAIL");
        StringAssert.EndsWith(AssertComparison(HostSection(text), "INF name",
            "oem42.inf", "usbip2_ude.inf"), "[MISMATCH]");
        StringAssert.EndsWith(AssertComparison(HostSection(text), "provider",
            "Contoso Drivers", "USBIP-WIN2"), "[MISMATCH]");
    }

    [TestMethod]
    public void Report_DriverStoreTargetUnresolved_IsCalledOut()
    {
        // Mirrors SetupGetInfDriverStoreLocationW returning nothing: the INF
        // name and architecture then come from unreliable fallbacks, so the
        // report has to say the store target never resolved.
        var inspector = ValidInspector();
        inspector.Host = With(ValidHost(), trustEvaluationPath: string.Empty);

        string text = Format(inspector, ValidVerifier());

        StringAssert.EndsWith(AssertComparison(HostSection(text),
            "driver store target", "NOT RESOLVED", "resolved"), "[MISMATCH]");
    }

    // ---- Publisher mismatch ---------------------------------------------

    [TestMethod]
    public void Report_PublisherMismatch_IncludesTheObservedCommonName()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust
            {
                Trusted = true,
                IsMicrosoftHardwareCompatibilityPublisher = false,
                ObservedSignerCommonName = "Contoso Attestation Publisher",
                Diagnostic = "trusted",
            },
        };

        string text = Format(ValidInspector(), verifier);

        StringAssert.Contains(text, "RESULT: FAIL");
        StringAssert.Contains(text, "UntrustedSignature");
        StringAssert.Contains(text, "Contoso Attestation Publisher");
        string line = AssertComparison(HostSection(text), "signer common name",
            "Contoso Attestation Publisher", ExpectedPublisher);
        StringAssert.EndsWith(line, "[MISMATCH]");
        StringAssert.EndsWith(AssertComparison(HostSection(text),
            "publisher accepted", "no", "yes"), "[MISMATCH]");
    }

    [TestMethod]
    public void Report_UnreadableCommonName_IsReportedWithoutClaimingAMismatch()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust
            {
                Trusted = true,
                IsMicrosoftHardwareCompatibilityPublisher = true,
                Diagnostic = "trusted",
            },
        };

        string text = Format(ValidInspector(), verifier);

        StringAssert.Contains(text, "RESULT: PASS");
        StringAssert.Contains(text, "not production approval");
        StringAssert.Contains(HostSection(text),
            "signer common name     observed: (not reported)");
        StringAssert.Matches(text, new Regex(@"observed mismatches\s+: 0"),
            "An unread common name is unknown, not a mismatch.");
    }

    [TestMethod]
    public void Report_UntrustedSignature_ListsTheTrustFlags()
    {
        var verifier = new FakeVerifier
        {
            DriverTrust = _ => new ViiperSignatureTrust
            {
                Trusted = false,
                Expired = true,
                Diagnostic = "certificate expired",
                ObservedSignerCommonName = ExpectedPublisher,
            },
        };

        string text = Format(ValidInspector(), verifier);

        StringAssert.Contains(text, "RESULT: FAIL");
        StringAssert.Contains(HostSection(text), "signature flags");
        StringAssert.Contains(HostSection(text), "expired");
        StringAssert.EndsWith(AssertComparison(HostSection(text),
            "signature trusted", "no", "yes"), "[MISMATCH]");
    }

    // ---- Nothing installed ----------------------------------------------

    [TestMethod]
    public void Report_NothingInstalled_IsExplicitForEveryComponent()
    {
        var inspector = new FakeInspector
        {
            Host = new ViiperDriverPackageInfo { Found = false },
            Filter = new ViiperDriverPackageInfo { Found = false },
            ClientResolver = _ => new ViiperUsbipClientInfo { Found = false },
        };

        string text = Format(inspector, ValidVerifier());

        StringAssert.Contains(text, "RESULT: FAIL");
        StringAssert.Contains(text, "NotFound");
        StringAssert.Contains(text, "No usbip-win2 driver packages were found.");
        StringAssert.Contains(text,
            "does not approve or recommend installing any driver.");
        Assert.IsFalse(text.Contains("Install the supported",
            StringComparison.OrdinalIgnoreCase));
        StringAssert.EndsWith(AssertComparison(HostSection(text), "package found",
            "no", "yes"), "[MISMATCH]");
        StringAssert.EndsWith(AssertComparison(FilterSection(text),
            "package found", "no", "yes"), "[MISMATCH]");
        StringAssert.EndsWith(AssertComparison(ClientSection(text), "file found",
            "no", "yes"), "[MISMATCH]");
        StringAssert.Contains(FilterSection(text),
            @"DriverStore\FileRepository\usbip2_filter.inf_*");
        StringAssert.Matches(text, new Regex(@"observed mismatches\s+: 3"));
        // Nothing is claimed about fields that could not be read at all.
        Assert.AreEqual(3, Occurrences(text, "nothing further could be read"),
            "Each unreadable component must say so explicitly.");
        Assert.IsFalse(HostSection(text).Contains("signature trusted",
            StringComparison.Ordinal),
            "A package that was not found has no signature to report on.");
    }

    [TestMethod]
    public void Report_PackageEnumerationThrew_ReportsTheReadError()
    {
        var inspector = new FakeInspector
        {
            HostResolver = _ => throw new InvalidOperationException("setupapi failure"),
        };

        string text = Format(inspector, ValidVerifier());

        StringAssert.Contains(text, "RESULT: FAIL");
        StringAssert.Contains(text, "InspectionFailed");
        StringAssert.Contains(text, "package read error");
        StringAssert.Contains(text, "setupapi failure");
    }

    // ---- Privacy ---------------------------------------------------------

    [TestMethod]
    public void Report_OmitsPathsAndInstanceIdentity()
    {
        string text = Format(ValidInspector(), ValidVerifier());

        Assert.IsFalse(text.Contains(HostTrustPath, StringComparison.OrdinalIgnoreCase),
            "Driver store / catalog paths must not be surfaced.");
        Assert.IsFalse(text.Contains(FilterTrustPath, StringComparison.OrdinalIgnoreCase),
            "Driver store / catalog paths must not be surfaced.");
        Assert.IsFalse(text.Contains(@"ROOT\USB\0002", StringComparison.OrdinalIgnoreCase),
            "No machine-specific instance path may appear.");
        Assert.IsFalse(text.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase),
            "No user profile path may appear.");
    }

    [DataTestMethod]
    [DataRow(@"C:\Users\somebody\Desktop\usbip\usbip.exe",
        @"C:\Users\<user>\Desktop\usbip\usbip.exe")]
    [DataRow(@"C:\Users\somebody", @"C:\Users\<user>")]
    [DataRow(@"C:\Program Files\USBip\usbip.exe",
        @"C:\Program Files\USBip\usbip.exe")]
    public void RedactUserPath_RemovesTheAccountName(string path, string expected)
    {
        Assert.AreEqual(expected,
            ViiperDriverReportFormatter.RedactUserPath(path));
    }

    [TestMethod]
    public void RedactUserPath_HandlesMissingPath()
    {
        Assert.AreEqual("(not set)",
            ViiperDriverReportFormatter.RedactUserPath(null));
    }

    [DataTestMethod]
    [DataRow(
        @"Access to the path 'C:\Users\somebody\AppData\Local\Temp\x.exe' is denied.",
        @"Access to the path 'C:\Users\<user>\AppData\Local\Temp\x.exe' is denied.")]
    [DataRow(
        @"Could not find a part of the path 'C:\Users\somebody'.",
        @"Could not find a part of the path 'C:\Users\<user>'.")]
    [DataRow(
        @"moved C:\Users\one\a.exe to C:\Users\two\b.exe",
        @"moved C:\Users\<user>\a.exe to C:\Users\<user>\b.exe")]
    [DataRow(
        @"cannot open C:\Users\somebody because it is a directory",
        @"cannot open C:\Users\<user> because it is a directory")]
    public void RedactUserPathsInText_RedactsEveryEmbeddedAccountName(
        string text, string expected)
    {
        Assert.AreEqual(expected,
            ViiperDriverReportFormatter.RedactUserPathsInText(text));
    }

    [DataTestMethod]
    [DataRow("There is not enough space on the disk.")]
    [DataRow(@"could not read 'C:\Program Files\USBip\usbip.exe'")]
    public void RedactUserPathsInText_LeavesPathFreeTextAlone(string text)
    {
        Assert.AreEqual(text,
            ViiperDriverReportFormatter.RedactUserPathsInText(text));
    }

    [TestMethod]
    public void RedactUserPathsInText_HandlesMissingText()
    {
        Assert.AreEqual(string.Empty,
            ViiperDriverReportFormatter.RedactUserPathsInText(null));
        Assert.AreEqual(string.Empty,
            ViiperDriverReportFormatter.RedactUserPathsInText(string.Empty));
    }

    // ---- Header ----------------------------------------------------------

    [TestMethod]
    public void Report_HeaderCarriesEnvironmentAndReportLocation()
    {
        string text = Format(ValidInspector(), ValidVerifier());

        StringAssert.Contains(text,
            ProductInfo.ProductName + " VIIPER driver validation");
        StringAssert.Contains(text, "1999-12-31 23:59:59Z");
        StringAssert.Contains(text, "3.5.1");
        StringAssert.Contains(text, "X64");
        StringAssert.Contains(text, ExampleReportFilePath);
        StringAssert.Contains(text, "elevated");
        StringAssert.Contains(text, "read-only");
    }

    [TestMethod]
    public void Format_RequiresReportAndContext()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            ViiperDriverReportFormatter.Format(null, Context()));
        Assert.ThrowsException<ArgumentNullException>(() =>
            ViiperDriverReportFormatter.Format(
                new ViiperDriverValidationReport(), null));
    }

    // ---- Helpers ---------------------------------------------------------

    private static string Format(FakeInspector inspector, FakeVerifier verifier)
    {
        var validator = new ViiperDriverValidator(
            ViiperDriverManifest.ObservedBaselines, inspector, verifier);
        return ViiperDriverReportFormatter.Format(
            validator.Inspect(CanonicalUsbipPath), Context());
    }

    private static ViiperDriverReportContext Context() =>
        new ViiperDriverReportContext
        {
            TimestampUtc = new DateTimeOffset(1999, 12, 31, 23, 59, 59,
                TimeSpan.Zero),
            AppVersion = "3.5.1",
            OsVersion = "Microsoft Windows NT 10.0.99999.0",
            ProcessArchitecture = "X64",
            Elevated = false,
            UsbipExecutablePath = CanonicalUsbipPath,
            ReportFilePath = ExampleReportFilePath,
        };

    /// <summary>
    /// Stands in for the real report location, which the command builds under
    /// <c>%TEMP%\{ProductInfo.TempFolderName}</c>. Composed from the same
    /// constant so a rebrand cannot leave the fixture describing the old
    /// product's folder.
    /// </summary>
    private static readonly string ExampleReportFilePath =
        $@"%TEMP%\{ProductInfo.TempFolderName}\report.txt";

    /// <summary>
    /// Asserts one comparison line exists in a section and returns it, so a
    /// caller can also assert its verdict marker.
    /// </summary>
    private static string AssertComparison(string section, string label,
        string observed, string expected)
    {
        foreach (string line in section.Split('\n'))
        {
            string candidate = line.TrimEnd('\r');
            if (!candidate.TrimStart().StartsWith(label + " ",
                StringComparison.Ordinal))
            {
                continue;
            }

            StringAssert.Contains(candidate, "observed: " + observed);
            StringAssert.Contains(candidate, "expected: " + expected);
            return candidate;
        }

        Assert.Fail($"No '{label}' comparison line was found in:{Environment.NewLine}" +
            section);
        return null;
    }

    private static int Occurrences(string text, string needle)
    {
        int count = 0;
        int index = text.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(needle, index + needle.Length,
                StringComparison.Ordinal);
        }

        return count;
    }

    private static string[] ComparisonLines(string text)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (string line in text.Split('\n'))
        {
            string candidate = line.TrimEnd('\r');
            if (candidate.Contains(" expected: ", StringComparison.Ordinal))
                lines.Add(candidate);
        }

        Assert.IsTrue(lines.Count > 0, "The report produced no comparison lines.");
        return lines.ToArray();
    }

    private static string HostSection(string text) =>
        Between(text, "-- UDE host controller", "-- filter extension");

    private static string FilterSection(string text) =>
        Between(text, "-- filter extension", "-- usbip.exe");

    private static string ClientSection(string text) =>
        Between(text, "-- usbip.exe", "-- notes");

    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.IsTrue(from >= 0, $"Section '{start}' is missing from the report.");
        int to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.IsTrue(to > from, $"Section '{end}' is missing from the report.");
        return text.Substring(from, to - from);
    }

    private static FakeInspector ValidInspector() => new FakeInspector
    {
        Host = ValidHost(),
        Filter = ValidFilter(),
    };

    private static FakeVerifier ValidVerifier() => new FakeVerifier();

    private static ViiperDriverPackageInfo ValidHost() =>
        new ViiperDriverPackageInfo
        {
            Found = true,
            HardwareId = ViiperDriverManifest.UdeHostControllerHardwareId,
            InfName = "usbip2_ude.inf",
            Provider = "USBIP-WIN2",
            DriverVersion = new Version(1, 45, 29, 368),
            Service = "usbip2_ude",
            CatalogFile = "usbip2_ude.cat",
            Architecture = ViiperDriverArchitecture.X64,
            DeviceNodePresent = true,
            Started = true,
            TrustEvaluationPath = HostTrustPath,
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
            TrustEvaluationPath = FilterTrustPath,
        };

    private static ViiperUsbipClientInfo ValidClient() =>
        new ViiperUsbipClientInfo
        {
            Found = true,
            FileName = "usbip.exe",
            ProductVersion = new Version(0, 9, 7, 8),
        };

    private static ViiperDriverPackageInfo With(
        ViiperDriverPackageInfo source, string provider = null,
        string infName = null, Version driverVersion = null,
        string trustEvaluationPath = null) =>
        new ViiperDriverPackageInfo
        {
            Found = source.Found,
            HardwareId = source.HardwareId,
            InfName = infName ?? source.InfName,
            Provider = provider ?? source.Provider,
            DriverVersion = driverVersion ?? source.DriverVersion,
            Service = source.Service,
            CatalogFile = source.CatalogFile,
            Architecture = source.Architecture,
            DeviceNodePresent = source.DeviceNodePresent,
            Started = source.Started,
            TrustEvaluationPath = trustEvaluationPath ?? source.TrustEvaluationPath,
        };

    private sealed class FakeInspector : IDriverPackageInspector
    {
        public ViiperDriverPackageInfo Host { get; set; }
        public ViiperDriverPackageInfo Filter { get; set; }
        public Func<string, ViiperDriverPackageInfo> HostResolver { get; set; }
        public Func<string, ViiperUsbipClientInfo> ClientResolver { get; set; }

        public ViiperDriverPackageInfo InspectHostController(string hardwareId)
        {
            if (HostResolver != null)
                return HostResolver(hardwareId);
            return Host ?? new ViiperDriverPackageInfo { Found = false };
        }

        public ViiperDriverPackageInfo InspectFilterExtension(string infName) =>
            Filter ?? new ViiperDriverPackageInfo { Found = false };

        public ViiperUsbipClientInfo InspectUsbipClient(string executablePath)
        {
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

        public ViiperSignatureTrust VerifyDriverPackage(
            ViiperDriverPackageInfo package) =>
            DriverTrust != null
                ? DriverTrust(package)
                : new ViiperSignatureTrust
                {
                    Trusted = true,
                    IsMicrosoftHardwareCompatibilityPublisher = true,
                    Diagnostic = "trusted",
                    ObservedSignerCommonName = ExpectedPublisher,
                };

        public ViiperSignatureTrust VerifyFile(string filePath) =>
            FileTrust != null
                ? FileTrust(filePath)
                : new ViiperSignatureTrust
                {
                    Trusted = true,
                    Diagnostic = "trusted",
                    ObservedSignerCommonName = "usbip-win2 project",
                };
    }
}

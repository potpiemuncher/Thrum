/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

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

using System;

namespace DS4Windows
{
    /// <summary>
    /// The component that failed validation. Non-sensitive; safe for logs.
    /// </summary>
    public enum ViiperDriverComponent
    {
        None,
        UdeHostController,
        FilterExtension,
        UsbipClient,
    }

    /// <summary>
    /// Why a component failed. Non-sensitive; safe for logs.
    /// </summary>
    public enum ViiperDriverFailureReason
    {
        None,
        NotFound,
        WrongProvider,
        WrongInf,
        WrongVersion,
        WrongArchitecture,
        MixedPair,
        Unhealthy,
        UntrustedSignature,
        InspectionFailed,
    }

    /// <summary>
    /// Read-only view of one installed driver package, produced by an
    /// <see cref="IDriverPackageInspector"/>. Kept free of device instance
    /// paths, serials, or user paths so results can be logged.
    /// </summary>
    public sealed class ViiperDriverPackageInfo
    {
        public bool Found { get; init; }

        /// <summary>The hardware ID the package was located by (host controller).</summary>
        public string HardwareId { get; init; }

        /// <summary>Original INF name resolved from the driver store, e.g. usbip2_ude.inf.</summary>
        public string InfName { get; init; }

        public string Provider { get; init; }

        public Version DriverVersion { get; init; }

        public string Service { get; init; }

        /// <summary>Catalog identity (file name) for diagnostics; not identity.</summary>
        public string CatalogFile { get; init; }

        public ViiperDriverArchitecture Architecture { get; init; }

        /// <summary>True when a matching device node is present.</summary>
        public bool DeviceNodePresent { get; init; }

        /// <summary>True when the device node is started with no problem code.</summary>
        public bool Started { get; init; }

        /// <summary>
        /// Path the trust verifier should evaluate (the driver-store INF or its
        /// catalog). Never surfaced in diagnostics.
        /// </summary>
        public string TrustEvaluationPath { get; init; }
    }

    /// <summary>
    /// Read-only view of the userspace usbip.exe client.
    /// </summary>
    public sealed class ViiperUsbipClientInfo
    {
        public bool Found { get; init; }
        public string FileName { get; init; }
        public Version ProductVersion { get; init; }
    }

    /// <summary>
    /// Outcome of a trust evaluation performed with the Windows trust APIs and
    /// normal chain policy. All flags are derived from the trust chain, not
    /// from a substring match on any signer string.
    /// </summary>
    public sealed class ViiperSignatureTrust
    {
        /// <summary>WinVerifyTrust succeeded under normal chain policy.</summary>
        public bool Trusted { get; init; }
        public bool Revoked { get; init; }
        public bool Expired { get; init; }
        public bool TestSigned { get; init; }

        /// <summary>Self-signed or not chained to a trusted root.</summary>
        public bool DeveloperSigned { get; init; }

        /// <summary>
        /// The signing certificate obtained from the verified chain is the
        /// Microsoft Windows Hardware Compatibility Publisher.
        /// </summary>
        public bool IsMicrosoftHardwareCompatibilityPublisher { get; init; }

        /// <summary>Short, non-sensitive diagnostic (e.g. an error mnemonic).</summary>
        public string Diagnostic { get; init; }

        /// <summary>
        /// Common name of the signing certificate found on the chain, when one
        /// could be read. Diagnostic only: it is never part of the pass/fail
        /// decision, which uses
        /// <see cref="IsMicrosoftHardwareCompatibilityPublisher"/>. Reported so
        /// a wrong expected common name is visible instead of silently failing
        /// closed against a good install.
        /// </summary>
        public string ObservedSignerCommonName { get; init; }

        public static ViiperSignatureTrust Untrusted(string diagnostic,
            string observedSignerCommonName = null) =>
            new ViiperSignatureTrust
            {
                Trusted = false,
                Diagnostic = diagnostic,
                ObservedSignerCommonName = observedSignerCommonName,
            };
    }

    /// <summary>
    /// Enumerates and reads driver-package and userspace-client identity via
    /// SetupAPI / Configuration Manager. The interface exists so the
    /// manifest-matching and fail-closed decision logic is unit-testable with
    /// no driver installed (policy Section 4.3, Section 7).
    /// </summary>
    public interface IDriverPackageInspector
    {
        /// <summary>
        /// Locate the present emulated UDE host controller by hardware ID and
        /// read its bound INF, provider, DriverVer, service, catalog, arch, and
        /// health. Never located by a machine-specific instance path.
        /// </summary>
        ViiperDriverPackageInfo InspectHostController(string hardwareId);

        /// <summary>
        /// Locate the companion filter extension package by original INF name
        /// as a separate component.
        /// </summary>
        ViiperDriverPackageInfo InspectFilterExtension(string infName);

        /// <summary>
        /// Read the userspace usbip.exe file identity (file name and product
        /// version) at an already path-validated location.
        /// </summary>
        ViiperUsbipClientInfo InspectUsbipClient(string executablePath);
    }

    /// <summary>
    /// Verifies Authenticode / catalog trust via the Windows trust APIs. Behind
    /// an interface so the decision logic can be exercised with fabricated
    /// trust results (valid, expired, revoked, developer, test).
    /// </summary>
    public interface IAuthenticodeVerifier
    {
        /// <summary>Verify a driver package (catalog-backed) under normal chain policy.</summary>
        ViiperSignatureTrust VerifyDriverPackage(ViiperDriverPackageInfo package);

        /// <summary>Verify a stand-alone signed file such as usbip.exe.</summary>
        ViiperSignatureTrust VerifyFile(string filePath);
    }

    /// <summary>
    /// Fail-closed result of a VIIPER driver validation pass.
    /// Diagnostics are non-sensitive by construction.
    /// </summary>
    public sealed class ViiperDriverValidationResult
    {
        private ViiperDriverValidationResult(bool passed,
            ViiperDriverComponent failedComponent,
            ViiperDriverFailureReason reason, string diagnostic,
            string releaseLabel, ViiperDriverTier? tier)
        {
            Passed = passed;
            FailedComponent = failedComponent;
            Reason = reason;
            Diagnostic = diagnostic;
            ReleaseLabel = releaseLabel;
            Tier = tier;
        }

        public bool Passed { get; }
        public ViiperDriverComponent FailedComponent { get; }
        public ViiperDriverFailureReason Reason { get; }
        public string Diagnostic { get; }

        /// <summary>Matched release label on success; otherwise null.</summary>
        public string ReleaseLabel { get; }

        /// <summary>Matched tier on success; otherwise null.</summary>
        public ViiperDriverTier? Tier { get; }

        /// <summary>
        /// True when the matched release is only allowed as an experimental
        /// baseline, so the caller must keep the existing warning and per-Start
        /// confirmation.
        /// </summary>
        public bool RequiresExperimentalConfirmation =>
            Passed && Tier == ViiperDriverTier.ExperimentalBaseline;

        public static ViiperDriverValidationResult Pass(string releaseLabel,
            ViiperDriverTier tier) =>
            new ViiperDriverValidationResult(true,
                ViiperDriverComponent.None,
                ViiperDriverFailureReason.None,
                $"Validated usbip-win2 release {releaseLabel} ({tier}).",
                releaseLabel, tier);

        public static ViiperDriverValidationResult Fail(
            ViiperDriverComponent component,
            ViiperDriverFailureReason reason, string diagnostic) =>
            new ViiperDriverValidationResult(false, component, reason,
                diagnostic, null, null);
    }

    /// <summary>
    /// Every observation a validation pass consumed, paired with the
    /// authoritative <see cref="ViiperDriverValidationResult"/>. Purely
    /// diagnostic: nothing here participates in the fail-closed decision, which
    /// is still produced by <see cref="ViiperDriverValidator.Validate"/>.
    /// Exists so a tester can see observed-vs-expected values on real hardware
    /// instead of only a pass/fail verdict. Non-sensitive by construction: no
    /// instance paths, serials, addresses, or user paths.
    /// </summary>
    public sealed class ViiperDriverValidationReport
    {
        /// <summary>The authoritative fail-closed outcome.</summary>
        public ViiperDriverValidationResult Result { get; init; }

        /// <summary>Release the observations are described against.</summary>
        public ViiperDriverRelease ExpectedRelease { get; init; }

        public ViiperDriverPackageInfo HostController { get; init; }

        public ViiperDriverPackageInfo FilterExtension { get; init; }

        public ViiperUsbipClientInfo UsbipClient { get; init; }

        public ViiperSignatureTrust HostControllerTrust { get; init; }

        public ViiperSignatureTrust FilterExtensionTrust { get; init; }

        public ViiperSignatureTrust UsbipClientTrust { get; init; }

        /// <summary>
        /// Message from an inspection that threw while enumerating driver
        /// packages; null when enumeration completed.
        /// </summary>
        public string PackageInspectionError { get; init; }

        /// <summary>
        /// Message from an inspection that threw while reading the userspace
        /// client; null when the read completed.
        /// </summary>
        public string UsbipClientInspectionError { get; init; }

        /// <summary>
        /// True when a driver-store target could be resolved for the host
        /// controller. False means SetupGetInfDriverStoreLocation (or the INF
        /// read) produced nothing, which makes the reported INF name and
        /// architecture unreliable. The path itself is never surfaced.
        /// </summary>
        public bool HostControllerStoreTargetResolved { get; init; }

        /// <summary>
        /// True when a driver-store target could be resolved for the filter
        /// extension package.
        /// </summary>
        public bool FilterExtensionStoreTargetResolved { get; init; }
    }

    /// <summary>
    /// Pure manifest-matching and fail-closed decision logic. All OS access is
    /// delegated to <see cref="IDriverPackageInspector"/> and
    /// <see cref="IAuthenticodeVerifier"/> so this class is fully unit-testable.
    /// </summary>
    public sealed class ViiperDriverValidator
    {
        private readonly ViiperDriverManifest manifest;
        private readonly IDriverPackageInspector inspector;
        private readonly IAuthenticodeVerifier verifier;

        public ViiperDriverValidator(ViiperDriverManifest manifest,
            IDriverPackageInspector inspector, IAuthenticodeVerifier verifier)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            this.inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        }

        public ViiperDriverValidationResult Validate(string usbipExecutablePath)
        {
            ViiperDriverPackageInfo host;
            ViiperDriverPackageInfo filter;
            try
            {
                host = inspector.InspectHostController(
                    ViiperDriverManifest.UdeHostControllerHardwareId);
                filter = inspector.InspectFilterExtension(
                    manifest.ReferenceRelease.FilterExtension.InfName);
            }
            catch (Exception ex)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UdeHostController,
                    ViiperDriverFailureReason.InspectionFailed,
                    "VIIPER could not enumerate usbip-win2 driver " +
                    $"packages: {ex.Message}");
            }

            // Both packages must match one observed-baseline entry.
            ViiperDriverRelease matched = FindMatchingRelease(host, filter);
            if (matched == null)
            {
                return DescribeIdentityFailure(host, filter);
            }

            // Section 4.1: the host controller must be present, started, and healthy.
            if (!host.DeviceNodePresent || !host.Started)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UdeHostController,
                    ViiperDriverFailureReason.Unhealthy,
                    "The usbip-win2 UDE host controller is present but not " +
                    "started/healthy. The diagnostic fails closed until the driver " +
                    "reports a started, problem-free state.");
            }

            ViiperDriverValidationResult hostTrust = ValidateDriverTrust(host,
                ViiperDriverComponent.UdeHostController,
                matched.DriverSignerPolicy);
            if (hostTrust != null)
                return hostTrust;

            ViiperDriverValidationResult filterTrust = ValidateDriverTrust(filter,
                ViiperDriverComponent.FilterExtension,
                matched.DriverSignerPolicy);
            if (filterTrust != null)
                return filterTrust;

            ViiperDriverValidationResult clientResult =
                ValidateUsbipClient(usbipExecutablePath, matched.UserspaceClient);
            if (clientResult != null)
                return clientResult;

            return ViiperDriverValidationResult.Pass(matched.ReleaseLabel,
                matched.Tier);
        }

        /// <summary>
        /// Read-only diagnostic pass. Gathers the same observations
        /// <see cref="Validate"/> consumes and pairs them with the authoritative
        /// <see cref="Validate"/> outcome, so a report can show observed versus
        /// expected values for every component even when validation passes.
        /// Additive only: the fail-closed decision is unchanged, and every
        /// observation is gathered defensively so a throwing inspector still
        /// yields a report.
        /// </summary>
        public ViiperDriverValidationReport Inspect(string usbipExecutablePath)
        {
            ViiperDriverPackageInfo host = null;
            ViiperDriverPackageInfo filter = null;
            string packageError = null;
            try
            {
                host = inspector.InspectHostController(
                    ViiperDriverManifest.UdeHostControllerHardwareId);
                filter = inspector.InspectFilterExtension(
                    manifest.ReferenceRelease.FilterExtension.InfName);
            }
            catch (Exception ex)
            {
                packageError = ex.Message;
            }

            ViiperUsbipClientInfo client = null;
            string clientError = null;
            try
            {
                client = inspector.InspectUsbipClient(usbipExecutablePath);
            }
            catch (Exception ex)
            {
                clientError = ex.Message;
            }

            ViiperDriverValidationResult result = Validate(usbipExecutablePath);
            ViiperDriverRelease expectedRelease = result.Passed
                ? FindRelease(result.ReleaseLabel)
                : SelectComparisonRelease(host, filter);

            return new ViiperDriverValidationReport
            {
                Result = result,
                ExpectedRelease = expectedRelease,
                HostController = host,
                FilterExtension = filter,
                UsbipClient = client,
                HostControllerTrust = InspectPackageTrust(host),
                FilterExtensionTrust = InspectPackageTrust(filter),
                UsbipClientTrust = InspectFileTrust(usbipExecutablePath, client),
                PackageInspectionError = packageError,
                UsbipClientInspectionError = clientError,
                HostControllerStoreTargetResolved =
                    !string.IsNullOrWhiteSpace(host?.TrustEvaluationPath),
                FilterExtensionStoreTargetResolved =
                    !string.IsNullOrWhiteSpace(filter?.TrustEvaluationPath),
            };
        }

        private ViiperSignatureTrust InspectPackageTrust(
            ViiperDriverPackageInfo package)
        {
            if (package == null || !package.Found)
                return null;

            try
            {
                return verifier.VerifyDriverPackage(package);
            }
            catch (Exception ex)
            {
                return ViiperSignatureTrust.Untrusted(
                    "trust verification threw: " + ex.Message);
            }
        }

        private ViiperSignatureTrust InspectFileTrust(string filePath,
            ViiperUsbipClientInfo client)
        {
            if (client == null || !client.Found)
                return null;

            try
            {
                return verifier.VerifyFile(filePath);
            }
            catch (Exception ex)
            {
                return ViiperSignatureTrust.Untrusted(
                    "trust verification threw: " + ex.Message);
            }
        }

        private ViiperDriverRelease FindMatchingRelease(
            ViiperDriverPackageInfo host, ViiperDriverPackageInfo filter)
        {
            foreach (ViiperDriverRelease release in manifest.Releases)
            {
                if (IdentityMatches(host, release.UdeHostController, release) &&
                    IdentityMatches(filter, release.FilterExtension, release))
                {
                    return release;
                }
            }

            return null;
        }

        private ViiperDriverRelease FindRelease(string releaseLabel)
        {
            if (string.IsNullOrWhiteSpace(releaseLabel))
                return null;

            foreach (ViiperDriverRelease release in manifest.Releases)
            {
                if (string.Equals(release.ReleaseLabel, releaseLabel,
                    StringComparison.Ordinal))
                {
                    return release;
                }
            }

            return null;
        }

        /// <summary>
        /// Selects the observed baseline closest to the installed package pair.
        /// Exact component versions dominate provider/INF/architecture matches,
        /// so adding a new release cannot make diagnostics compare a recognized
        /// older pair against whichever entry happens to be first. A fully
        /// ambiguous or empty observation intentionally falls back to the
        /// manifest reference release.
        /// </summary>
        private ViiperDriverRelease SelectComparisonRelease(
            ViiperDriverPackageInfo host, ViiperDriverPackageInfo filter)
        {
            ViiperDriverRelease best = manifest.ReferenceRelease;
            int bestScore = -1;
            foreach (ViiperDriverRelease release in manifest.Releases)
            {
                int score = IdentitySimilarity(host,
                    release.UdeHostController, release) +
                    IdentitySimilarity(filter,
                        release.FilterExtension, release);
                if (score > bestScore)
                {
                    best = release;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int IdentitySimilarity(ViiperDriverPackageInfo package,
            ViiperDriverPackageSpec spec, ViiperDriverRelease release)
        {
            if (package == null || !package.Found)
                return 0;

            int score = 0;
            if (spec.MatchesProvider(package.Provider))
                score += 2;
            if (spec.MatchesInf(package.InfName))
                score += 2;
            if (spec.MatchesVersion(package.DriverVersion))
                score += 8;
            if (release.SupportsArchitecture(package.Architecture))
                score += 1;
            return score;
        }

        private static bool IdentityMatches(ViiperDriverPackageInfo package,
            ViiperDriverPackageSpec spec, ViiperDriverRelease release)
        {
            return package != null && package.Found &&
                spec.MatchesProvider(package.Provider) &&
                spec.MatchesInf(package.InfName) &&
                spec.MatchesVersion(package.DriverVersion) &&
                release.SupportsArchitecture(package.Architecture);
        }

        /// <summary>
        /// Produces the most specific non-matching diagnostic against a
        /// reference release: missing, wrong provider, wrong INF, mixed pair,
        /// wrong version, or wrong architecture.
        /// </summary>
        private ViiperDriverValidationResult DescribeIdentityFailure(
            ViiperDriverPackageInfo host, ViiperDriverPackageInfo filter)
        {
            ViiperDriverRelease reference =
                SelectComparisonRelease(host, filter);
            ViiperDriverFailureReason hostCheck =
                CheckIdentity(host, reference.UdeHostController, reference);
            ViiperDriverFailureReason filterCheck =
                CheckIdentity(filter, reference.FilterExtension, reference);

            if (hostCheck == ViiperDriverFailureReason.NotFound &&
                filterCheck == ViiperDriverFailureReason.NotFound)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UdeHostController,
                    ViiperDriverFailureReason.NotFound,
                    "No usbip-win2 driver packages were found. This diagnostic " +
                    $"compares only with observed baseline {reference.ReleaseLabel}; it " +
                    "does not approve or recommend installing any driver.");
            }

            if (hostCheck == ViiperDriverFailureReason.NotFound)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UdeHostController,
                    ViiperDriverFailureReason.NotFound,
                    "The usbip-win2 UDE host controller was not found. " +
                    $"Observed baseline {reference.ReleaseLabel} is not an install recommendation.");
            }

            if (filterCheck == ViiperDriverFailureReason.NotFound)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.FilterExtension,
                    ViiperDriverFailureReason.NotFound,
                    "The usbip-win2 filter extension package was not found. " +
                    "The two package identities must be evaluated together; both must be " +
                    "present to compare them with an observed baseline.");
            }

            if (hostCheck == ViiperDriverFailureReason.WrongProvider)
                return ProviderFailure(ViiperDriverComponent.UdeHostController);
            if (filterCheck == ViiperDriverFailureReason.WrongProvider)
                return ProviderFailure(ViiperDriverComponent.FilterExtension);

            if (hostCheck == ViiperDriverFailureReason.WrongInf)
                return InfFailure(ViiperDriverComponent.UdeHostController);
            if (filterCheck == ViiperDriverFailureReason.WrongInf)
                return InfFailure(ViiperDriverComponent.FilterExtension);

            bool hostVersionWrong = hostCheck == ViiperDriverFailureReason.WrongVersion;
            bool filterVersionWrong = filterCheck == ViiperDriverFailureReason.WrongVersion;
            if (hostVersionWrong ^ filterVersionWrong)
            {
                ViiperDriverComponent mixedComponent = hostVersionWrong
                    ? ViiperDriverComponent.UdeHostController
                    : ViiperDriverComponent.FilterExtension;
                return ViiperDriverValidationResult.Fail(mixedComponent,
                    ViiperDriverFailureReason.MixedPair,
                    "The usbip-win2 UDE host controller and filter extension " +
                    "do not match a single observed baseline; no replacement is " +
                    "recommended by this diagnostic.");
            }

            if (hostVersionWrong && filterVersionWrong)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UdeHostController,
                    ViiperDriverFailureReason.WrongVersion,
                    "The installed usbip-win2 driver packages do not match the " +
                    $"observed baseline {reference.ReleaseLabel}. This is identity evidence only; " +
                    "the diagnostic does not recommend changing the installed driver.");
            }

            if (hostCheck == ViiperDriverFailureReason.WrongArchitecture)
                return ArchitectureFailure(ViiperDriverComponent.UdeHostController);
            if (filterCheck == ViiperDriverFailureReason.WrongArchitecture)
                return ArchitectureFailure(ViiperDriverComponent.FilterExtension);

            // Should not happen: no release matched yet no component-level
            // difference was identified. Fail closed.
            return ViiperDriverValidationResult.Fail(
                ViiperDriverComponent.UdeHostController,
                ViiperDriverFailureReason.WrongVersion,
                "The installed usbip-win2 driver packages do not match an " +
                "observed baseline.");
        }

        private static ViiperDriverFailureReason CheckIdentity(
            ViiperDriverPackageInfo package, ViiperDriverPackageSpec spec,
            ViiperDriverRelease release)
        {
            if (package == null || !package.Found)
                return ViiperDriverFailureReason.NotFound;
            if (!spec.MatchesProvider(package.Provider))
                return ViiperDriverFailureReason.WrongProvider;
            if (!spec.MatchesInf(package.InfName))
                return ViiperDriverFailureReason.WrongInf;
            if (!spec.MatchesVersion(package.DriverVersion))
                return ViiperDriverFailureReason.WrongVersion;
            if (!release.SupportsArchitecture(package.Architecture))
                return ViiperDriverFailureReason.WrongArchitecture;
            return ViiperDriverFailureReason.None;
        }

        private ViiperDriverValidationResult ValidateDriverTrust(
            ViiperDriverPackageInfo package,
            ViiperDriverComponent component,
            ViiperDriverSignerPolicy policy)
        {
            ViiperSignatureTrust trust;
            try
            {
                trust = verifier.VerifyDriverPackage(package);
            }
            catch (Exception ex)
            {
                return ViiperDriverValidationResult.Fail(component,
                    ViiperDriverFailureReason.InspectionFailed,
                    $"Could not verify the {Describe(component)} signature: " +
                    ex.Message);
            }

            string reject = RejectTrust(trust);
            if (reject != null)
            {
                return ViiperDriverValidationResult.Fail(component,
                    ViiperDriverFailureReason.UntrustedSignature,
                    $"The {Describe(component)} signature is not acceptable: " +
                    reject);
            }

            if (policy ==
                ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher &&
                !trust.IsMicrosoftHardwareCompatibilityPublisher)
            {
                return ViiperDriverValidationResult.Fail(component,
                    ViiperDriverFailureReason.UntrustedSignature,
                    $"The {Describe(component)} is trusted but is not signed by " +
                    "the Microsoft Hardware Compatibility Publisher required for " +
                    "this release.");
            }

            if (policy == ViiperDriverSignerPolicy.MicrosoftWhqlCertified &&
                !trust.IsMicrosoftHardwareCompatibilityPublisher)
            {
                return ViiperDriverValidationResult.Fail(component,
                    ViiperDriverFailureReason.UntrustedSignature,
                    $"The {Describe(component)} does not satisfy the required " +
                    "WHQL certification policy.");
            }

            return null;
        }

        private ViiperDriverValidationResult ValidateUsbipClient(
            string usbipExecutablePath, ViiperUsbipClientSpec spec)
        {
            ViiperUsbipClientInfo client;
            try
            {
                client = inspector.InspectUsbipClient(usbipExecutablePath);
            }
            catch (Exception ex)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UsbipClient,
                    ViiperDriverFailureReason.InspectionFailed,
                    $"Could not read the usbip.exe client: {ex.Message}");
            }

            if (client == null || !client.Found)
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UsbipClient,
                    ViiperDriverFailureReason.NotFound,
                    "The usbip.exe userspace client was not found at the " +
                    "configured location.");
            }

            if (!spec.MatchesFileName(client.FileName))
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UsbipClient,
                    ViiperDriverFailureReason.WrongInf,
                    "The configured client is not named usbip.exe.");
            }

            if (!spec.MatchesProductVersion(client.ProductVersion))
            {
                return ViiperDriverValidationResult.Fail(
                    ViiperDriverComponent.UsbipClient,
                    ViiperDriverFailureReason.WrongVersion,
                    "The usbip.exe client product version does not match the " +
                    $"observed baseline {spec.ProductVersion}.");
            }

            if (spec.RequireAuthenticode)
            {
                ViiperSignatureTrust trust;
                try
                {
                    trust = verifier.VerifyFile(usbipExecutablePath);
                }
                catch (Exception ex)
                {
                    return ViiperDriverValidationResult.Fail(
                        ViiperDriverComponent.UsbipClient,
                        ViiperDriverFailureReason.InspectionFailed,
                        $"Could not verify the usbip.exe signature: {ex.Message}");
                }

                string reject = RejectTrust(trust);
                if (reject != null)
                {
                    return ViiperDriverValidationResult.Fail(
                        ViiperDriverComponent.UsbipClient,
                        ViiperDriverFailureReason.UntrustedSignature,
                        "The usbip.exe Authenticode signature is not " +
                        $"acceptable: {reject}");
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a non-null rejection reason when the trust result is not
        /// clean under normal chain policy; null when acceptable.
        /// </summary>
        private static string RejectTrust(ViiperSignatureTrust trust)
        {
            if (trust == null)
                return "no trust result was produced";
            if (trust.Revoked)
                return "the signing certificate is revoked";
            if (trust.Expired)
                return "the signing certificate is expired";
            if (trust.TestSigned)
                return "the package is test-signed";
            if (trust.DeveloperSigned)
                return "the package is developer-signed / not chained to a trusted root";
            if (!trust.Trusted)
            {
                return string.IsNullOrWhiteSpace(trust.Diagnostic)
                    ? "the signature is not trusted"
                    : trust.Diagnostic;
            }

            return null;
        }

        private static string Describe(ViiperDriverComponent component) =>
            component switch
            {
                ViiperDriverComponent.UdeHostController => "UDE host controller",
                ViiperDriverComponent.FilterExtension => "filter extension",
                ViiperDriverComponent.UsbipClient => "usbip.exe client",
                _ => "component",
            };

        private static ViiperDriverValidationResult ProviderFailure(
            ViiperDriverComponent component) =>
            ViiperDriverValidationResult.Fail(component,
                ViiperDriverFailureReason.WrongProvider,
                $"The {Describe(component)} reports an unexpected provider. " +
                "It does not match the observed package identity.");

        private static ViiperDriverValidationResult InfFailure(
            ViiperDriverComponent component) =>
            ViiperDriverValidationResult.Fail(component,
                ViiperDriverFailureReason.WrongInf,
                $"The {Describe(component)} is bound to an unexpected INF.");

        private static ViiperDriverValidationResult ArchitectureFailure(
            ViiperDriverComponent component) =>
            ViiperDriverValidationResult.Fail(component,
                ViiperDriverFailureReason.WrongArchitecture,
                $"The {Describe(component)} architecture does not match " +
                "this observed baseline.");
    }

    /// <summary>
    /// Composition point for read-only driver identity/trust inspection. A PASS
    /// identifies an observed baseline only; it does not authorize DS4Windows to
    /// release a controller, elevate, or attach. Holds OS-touching implementations
    /// behind the two interfaces so it is trivially replaced in tests.
    /// </summary>
    public sealed class ViiperDriverGate
    {
        private readonly ViiperDriverValidator validator;

        public ViiperDriverGate(ViiperDriverValidator validator)
        {
            this.validator = validator ??
                throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>The shared diagnostic wired to the real OS inspectors.</summary>
        public static ViiperDriverGate Default { get; } = CreateDefault();

        public static ViiperDriverGate CreateDefault()
        {
            var validator = new ViiperDriverValidator(
                ViiperDriverManifest.ObservedBaselines,
                new SetupApiDriverPackageInspector(),
                new WinTrustAuthenticodeVerifier());
            return new ViiperDriverGate(validator);
        }

        /// <summary>
        /// Full identity/trust validation with a rich result. Callers must never
        /// treat <see cref="ViiperDriverValidationResult.Passed"/> as run approval;
        /// they must independently require a Production-tier manifest entry.
        /// </summary>
        public ViiperDriverValidationResult Validate(string usbipExecutablePath) =>
            validator.Validate(usbipExecutablePath);

        /// <summary>
        /// Read-only diagnostic pass used by the
        /// <c>-viiperdriverdiagnostic</c> command.
        /// Nothing is released, elevated, attached, or modified; the gate only
        /// reads device, driver, and file state.
        /// </summary>
        public ViiperDriverValidationReport Inspect(string usbipExecutablePath) =>
            validator.Inspect(usbipExecutablePath);

    }
}

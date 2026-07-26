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
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// Risk tier for a usbip-win2 release entry, per
    /// doc/dev/native_mode_driver_policy.md Section 4.3.
    /// </summary>
    public enum ViiperDriverTier
    {
        /// <summary>
        /// Observed in controlled testing for diagnostics only; not approved to run.
        /// Versions 0.9.7.7 and 0.9.7.8 are observed baselines. Package identity,
        /// signing, or an upstream release label is not evidence of runtime safety.
        /// </summary>
        ExperimentalBaseline,

        /// <summary>
        /// Reserved for a future maintainer-accepted signed release that fixes
        /// the request-lifetime defect. No entry uses this tier yet; adding one
        /// later must not require touching the validator, broker, or UI.
        /// </summary>
        Production,
    }

    /// <summary>
    /// Signer policy required for a driver package. Evaluated through the
    /// Windows trust API chain, never by a substring match on a signer string.
    /// </summary>
    public enum ViiperDriverSignerPolicy
    {
        /// <summary>
        /// Require a valid Microsoft Hardware Compatibility Publisher chain
        /// (attestation-signed). WHCP version reports unknown on 0.9.7.8, so
        /// WHQL is deliberately not required here.
        /// </summary>
        MicrosoftHardwareCompatibilityPublisher,

        /// <summary>
        /// Reserved for a future WHQL-certified production release.
        /// </summary>
        MicrosoftWhqlCertified,
    }

    public enum ViiperDriverArchitecture
    {
        X64,
        X86,
    }

    /// <summary>
    /// Expected identity of one installed driver package (the UDE host
    /// controller or the companion filter extension). Version comparisons live
    /// here so they are not scattered across UI, broker, or installer code.
    /// </summary>
    public sealed class ViiperDriverPackageSpec
    {
        public ViiperDriverPackageSpec(string infName, string provider,
            Version driverVersion)
        {
            if (string.IsNullOrWhiteSpace(infName))
                throw new ArgumentException("INF name is required.", nameof(infName));
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentException("Provider is required.", nameof(provider));
            InfName = infName;
            Provider = provider;
            DriverVersion = driverVersion ??
                throw new ArgumentNullException(nameof(driverVersion));
        }

        /// <summary>Original INF file name, e.g. usbip2_ude.inf.</summary>
        public string InfName { get; }

        /// <summary>Windows-reported INF provider, e.g. USBIP-WIN2.</summary>
        public string Provider { get; }

        /// <summary>
        /// Windows DriverVer numeric component. This is the value Windows
        /// reports per package, not the upstream release label.
        /// </summary>
        public Version DriverVersion { get; }

        public bool MatchesInf(string candidate) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), InfName,
                StringComparison.OrdinalIgnoreCase);

        public bool MatchesProvider(string candidate) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), Provider,
                StringComparison.OrdinalIgnoreCase);

        public bool MatchesVersion(Version candidate) =>
            candidate != null && candidate == DriverVersion;
    }

    /// <summary>
    /// Expected identity of the userspace usbip.exe client. The upstream
    /// release label is carried on the release entry; this records only the
    /// per-file product version and whether Authenticode is required.
    /// </summary>
    public sealed class ViiperUsbipClientSpec
    {
        public ViiperUsbipClientSpec(string fileName, Version productVersion,
            bool requireAuthenticode)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));
            FileName = fileName;
            ProductVersion = productVersion ??
                throw new ArgumentNullException(nameof(productVersion));
            RequireAuthenticode = requireAuthenticode;
        }

        public string FileName { get; }

        public Version ProductVersion { get; }

        public bool RequireAuthenticode { get; }

        public bool MatchesFileName(string candidate) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), FileName,
                StringComparison.OrdinalIgnoreCase);

        public bool MatchesProductVersion(Version candidate) =>
            candidate != null && candidate == ProductVersion;
    }

    /// <summary>
    /// One observed release: maps a single upstream release label to both
    /// driver packages, the userspace client, accepted architectures, tier,
    /// and signer policy. Adding a future fixed release means adding another
    /// entry to <see cref="ViiperDriverManifest"/>, not editing comparison
    /// logic elsewhere.
    /// </summary>
    public sealed class ViiperDriverRelease
    {
        public ViiperDriverRelease(string releaseLabel,
            ViiperDriverTier tier,
            ViiperDriverSignerPolicy driverSignerPolicy,
            ViiperDriverPackageSpec udeHostController,
            ViiperDriverPackageSpec filterExtension,
            ViiperUsbipClientSpec userspaceClient,
            IEnumerable<ViiperDriverArchitecture> architectures)
        {
            if (string.IsNullOrWhiteSpace(releaseLabel))
                throw new ArgumentException("Release label is required.",
                    nameof(releaseLabel));
            ReleaseLabel = releaseLabel;
            Tier = tier;
            DriverSignerPolicy = driverSignerPolicy;
            UdeHostController = udeHostController ??
                throw new ArgumentNullException(nameof(udeHostController));
            FilterExtension = filterExtension ??
                throw new ArgumentNullException(nameof(filterExtension));
            UserspaceClient = userspaceClient ??
                throw new ArgumentNullException(nameof(userspaceClient));
            Architectures = (architectures ??
                    throw new ArgumentNullException(nameof(architectures)))
                .Distinct().ToArray();
            if (Architectures.Count == 0)
                throw new ArgumentException(
                    "At least one architecture is required.",
                    nameof(architectures));
        }

        /// <summary>Upstream release label, e.g. 0.9.7.8.</summary>
        public string ReleaseLabel { get; }

        public ViiperDriverTier Tier { get; }

        public ViiperDriverSignerPolicy DriverSignerPolicy { get; }

        public ViiperDriverPackageSpec UdeHostController { get; }

        public ViiperDriverPackageSpec FilterExtension { get; }

        public ViiperUsbipClientSpec UserspaceClient { get; }

        public IReadOnlyList<ViiperDriverArchitecture> Architectures { get; }

        public bool SupportsArchitecture(ViiperDriverArchitecture architecture) =>
            Architectures.Contains(architecture);

        /// <summary>
        /// True only for a maintainer-approved production release. No current entry
        /// is approved; an experimental identity match is diagnostic evidence only.
        ///
        /// </summary>
        public bool IsRunAllowed => Tier == ViiperDriverTier.Production;
    }

    /// <summary>
    /// The single versioned data structure that owns all tier and version
    /// policy for VIIPER driver validation. Section 4.3 requires that this not be
    /// duplicated across the UI, broker, or installer.
    /// </summary>
    public sealed class ViiperDriverManifest
    {
        /// <summary>
        /// Stable hardware ID of the emulated UDE host controller. Identity is
        /// this hardware ID plus the expected provider, never a machine-specific
        /// instance path such as ROOT\USB\0002.
        /// </summary>
        public const string UdeHostControllerHardwareId = @"ROOT\USBIP_WIN2\UDE";

        /// <summary>
        /// Common name the attestation-signing certificate carries when
        /// <see cref="ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher"/>
        /// is satisfied. Single source of truth for the trust verifier and for
        /// diagnostics; the decision itself is still made from the verified
        /// chain, never from a substring match.
        /// </summary>
        public const string MicrosoftHardwareCompatibilityPublisherCommonName =
            "Microsoft Windows Hardware Compatibility Publisher";

        private ViiperDriverManifest(IEnumerable<ViiperDriverRelease> releases)
        {
            Releases = (releases ?? throw new ArgumentNullException(nameof(releases)))
                .ToArray();
            if (Releases.Count == 0)
                throw new ArgumentException("At least one release is required.",
                    nameof(releases));
        }

        public IReadOnlyList<ViiperDriverRelease> Releases { get; }

        /// <summary>
        /// The release used to produce diagnostics when nothing matches. The
        /// first observed baseline is used only as the report comparison reference.
        /// </summary>
        public ViiperDriverRelease ReferenceRelease => Releases[0];

        /// <summary>
        /// Observed package identities for diagnostics. A future fixed release is added
        /// as a separate Production-tier entry; no other file changes.
        /// </summary>
        public static ViiperDriverManifest ObservedBaselines { get; } =
            BuildObservedBaselines();

        private static ViiperDriverManifest BuildObservedBaselines()
        {
            // Exact identity extracted offline from the signed x64 installer
            // currently downloaded by hbashton's VIIPER installer script. This
            // records what the package contains; it is not permission to install,
            // load, attach, or run the driver.
            var hbashtonInstallerBaseline = new ViiperDriverRelease(
                releaseLabel: "0.9.7.7",
                tier: ViiperDriverTier.ExperimentalBaseline,
                driverSignerPolicy:
                    ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher,
                udeHostController: new ViiperDriverPackageSpec(
                    infName: "usbip2_ude.inf",
                    provider: "USBIP-WIN2",
                    driverVersion: new Version(21, 14, 27, 907)),
                filterExtension: new ViiperDriverPackageSpec(
                    infName: "usbip2_filter.inf",
                    provider: "USBIP-WIN2",
                    driverVersion: new Version(21, 14, 27, 661)),
                userspaceClient: new ViiperUsbipClientSpec(
                    fileName: "usbip.exe",
                    productVersion: new Version(0, 9, 7, 7),
                    requireAuthenticode: true),
                architectures: new[]
                {
                    // Only the x64 installer has been inspected byte-for-byte.
                    ViiperDriverArchitecture.X64,
                });

            // Observed 0.9.7.8 package identity from the earlier local
            // controlled-test dossier. It remains known-risk and unapproved.
            var knownRiskBaseline = new ViiperDriverRelease(
                releaseLabel: "0.9.7.8",
                tier: ViiperDriverTier.ExperimentalBaseline,
                driverSignerPolicy:
                    ViiperDriverSignerPolicy.MicrosoftHardwareCompatibilityPublisher,
                udeHostController: new ViiperDriverPackageSpec(
                    infName: "usbip2_ude.inf",
                    provider: "USBIP-WIN2",
                    driverVersion: new Version(1, 45, 29, 368)),
                filterExtension: new ViiperDriverPackageSpec(
                    infName: "usbip2_filter.inf",
                    provider: "USBIP-WIN2",
                    driverVersion: new Version(1, 45, 28, 868)),
                userspaceClient: new ViiperUsbipClientSpec(
                    fileName: "usbip.exe",
                    productVersion: new Version(0, 9, 7, 8),
                    requireAuthenticode: true),
                architectures: new[]
                {
                    ViiperDriverArchitecture.X64,
                    ViiperDriverArchitecture.X86,
                });

            return new ViiperDriverManifest(new[]
            {
                hbashtonInstallerBaseline,
                knownRiskBaseline,
            });
        }
    }
}

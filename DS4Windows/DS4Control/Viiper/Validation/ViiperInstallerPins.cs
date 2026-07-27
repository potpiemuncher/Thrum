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

namespace DS4Windows
{
    /// <summary>
    /// Which bundled component a pin describes.
    /// </summary>
    public enum ViiperInstallerComponent
    {
        /// <summary>The usbip-win2 kernel driver package installer.</summary>
        UsbipWin2,

        /// <summary>The VIIPER userspace backend executable.</summary>
        ViiperBackend,
    }

    /// <summary>
    /// One exact file the setup script is allowed to fetch and act on.
    ///
    /// <para>A pin is an identity, not a version floor. Everything the script
    /// needs in order to decide whether the bytes in front of it are the bytes
    /// this project examined lives here: the URL they come from, their SHA-256,
    /// their size, and — where the publisher signs — the Authenticode signer
    /// the certificate chain must resolve to. Part 3 rule 2 of the phased plan
    /// is the reason there is no "or newer" anywhere in this type.</para>
    ///
    /// <para>Nothing in a pin is permission to run the file. It says only "this
    /// is the artefact whose behaviour was observed"; the tier decision stays
    /// with <see cref="ViiperDriverManifest"/>, and for the driver package the
    /// installed pair is re-validated afterwards through the same gate the
    /// <c>-viiperdriverdiagnostic</c> command uses.</para>
    /// </summary>
    public sealed class ViiperPinnedDownload
    {
        public ViiperPinnedDownload(ViiperInstallerComponent component,
            string releaseLabel, string fileName, string url, string sha256,
            long sizeInBytes, bool requireAuthenticode,
            string expectedSignerCommonName, string digestProvenance,
            string notes = null)
        {
            if (string.IsNullOrWhiteSpace(releaseLabel))
                throw new ArgumentException("A release label is required.",
                    nameof(releaseLabel));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A file name is required.",
                    nameof(fileName));
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("A URL is required.", nameof(url));
            if (string.IsNullOrWhiteSpace(sha256))
                throw new ArgumentException("A SHA-256 digest is required.",
                    nameof(sha256));
            if (sizeInBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
            if (requireAuthenticode &&
                string.IsNullOrWhiteSpace(expectedSignerCommonName))
                throw new ArgumentException(
                    "An expected signer is required when Authenticode is required.",
                    nameof(expectedSignerCommonName));
            if (string.IsNullOrWhiteSpace(digestProvenance))
                throw new ArgumentException(
                    "How the digest was obtained has to be recorded.",
                    nameof(digestProvenance));

            Component = component;
            ReleaseLabel = releaseLabel;
            FileName = fileName;
            Url = url;
            Sha256 = NormalizeDigest(sha256);
            SizeInBytes = sizeInBytes;
            RequireAuthenticode = requireAuthenticode;
            ExpectedSignerCommonName = requireAuthenticode
                ? expectedSignerCommonName
                : null;
            DigestProvenance = digestProvenance;
            Notes = notes;
        }

        public ViiperInstallerComponent Component { get; }

        /// <summary>
        /// Upstream release label, e.g. <c>0.9.7.7</c> or <c>v0.0.5</c>. For the
        /// driver this is the label a <see cref="ViiperDriverRelease"/> carries,
        /// which is what ties a download to a tier.
        /// </summary>
        public string ReleaseLabel { get; }

        public string FileName { get; }

        public string Url { get; }

        /// <summary>Upper-case hexadecimal, no separators.</summary>
        public string Sha256 { get; }

        /// <summary>
        /// Exact published size. Not a security control on its own — a digest
        /// match implies it — but it lets a truncated download be named as such
        /// instead of being reported as a digest mismatch.
        /// </summary>
        public long SizeInBytes { get; }

        /// <summary>
        /// Whether a valid Authenticode chain is a precondition for execution.
        /// False only where the publisher does not sign at all, and then the
        /// reason is spelled out in <see cref="Notes"/>.
        /// </summary>
        public bool RequireAuthenticode { get; }

        /// <summary>
        /// Common name the signing certificate must carry, or null when
        /// <see cref="RequireAuthenticode"/> is false. Compared only after
        /// Windows has already accepted the chain — it narrows a valid
        /// signature to the expected publisher, it never substitutes for chain
        /// validation.
        /// </summary>
        public string ExpectedSignerCommonName { get; }

        /// <summary>
        /// How this project obtained <see cref="Sha256"/>. Recorded in source
        /// because a pinned digest whose provenance nobody can restate is a
        /// number, not evidence.
        /// </summary>
        public string DigestProvenance { get; }

        /// <summary>Anything a reader needs in order not to misread the pin.</summary>
        public string Notes { get; }

        public bool MatchesDigest(string candidate) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(NormalizeDigest(candidate), Sha256,
                StringComparison.Ordinal);

        /// <summary>
        /// Case-insensitive, separator-free upper hex. Callers hand us digests
        /// from <c>Get-FileHash</c>, from GitHub's release metadata and from
        /// <see cref="System.Security.Cryptography.SHA256"/>, and those three
        /// disagree about case and about colons.
        /// </summary>
        public static string NormalizeDigest(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = value.Trim();
            int separator = text.IndexOf(':');
            if (separator >= 0)
            {
                text = text.Substring(separator + 1);
            }

            return text.Replace("-", string.Empty).Replace(" ", string.Empty)
                .Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// The exact artefacts <c>extras/install-viiper-backend.ps1</c> may fetch.
    ///
    /// <para>Single source of truth on purpose: the script holds no URL and no
    /// digest of its own, it asks for these. That is what makes "never fall
    /// back to latest" enforceable rather than aspirational — there is no
    /// second place a fallback could be written.</para>
    /// </summary>
    public static class ViiperInstallerPins
    {
        /// <summary>
        /// usbip-win2 0.9.7.7 x64, the release this project inspected
        /// byte-for-byte and validated on a clean Windows 11 checkpoint.
        ///
        /// <para>Not the newest release, deliberately. 0.9.7.8 exists and is
        /// the baseline the maintainer's own machine carries, but it is the
        /// release the request-lifetime race was reproduced on, so it is
        /// recognised by <see cref="ViiperDriverManifest"/> and never
        /// installed by us.</para>
        /// </summary>
        public static ViiperPinnedDownload UsbipWin2 { get; } =
            new ViiperPinnedDownload(
                component: ViiperInstallerComponent.UsbipWin2,
                releaseLabel: "0.9.7.7",
                fileName: "USBip-0.9.7.7-x64.exe",
                url: "https://github.com/vadimgrn/usbip-win2/releases/download/" +
                    "v.0.9.7.7/USBip-0.9.7.7-x64.exe",
                sha256:
                    "51620FA5F9F8BE5932BC9D786DEEE557CE06D5407A99CAB490DCFAC71F185FEA",
                sizeInBytes: 33226344L,
                requireAuthenticode: true,
                expectedSignerCommonName:
                    "Cloudyne Systems (Scheibling Consulting AB)",
                digestProvenance:
                    "SHA-256 of the release asset downloaded for the controlled " +
                    "Windows 11 validation pass, recomputed from the retained " +
                    "local copy before pinning.",
                notes:
                    "Inno Setup 6.7.0 payload. Installs UDE DriverVer 21.14.27.907 " +
                    "and filter DriverVer 21.14.27.661; the installed pair is " +
                    "re-validated after setup rather than trusted from this pin.");

        /// <summary>
        /// VIIPER v0.0.5, the backend release whose framed audio/haptics
        /// protocol this application negotiates against.
        ///
        /// <para>Two things a reader has to know. First, upstream does not sign
        /// this asset at all — it is an unsigned Go binary published by a
        /// release workflow — so the digest is the whole identity and
        /// <see cref="ViiperPinnedDownload.RequireAuthenticode"/> is false
        /// rather than a check that would fail on every honest download.
        /// Second, the published binary is <em>mis-stamped</em>: it reports
        /// <c>v0.0.3-18-g02fffe6</c> as its own version because the release
        /// workflow built it without fetching tags. hbashton/VIIPER#3 (ours)
        /// fixes that workflow. Until it lands, nothing may validate this file
        /// by the version it claims — only by
        /// <see cref="ViiperPinnedDownload.Sha256"/>.</para>
        /// </summary>
        public static ViiperPinnedDownload ViiperBackend { get; } =
            new ViiperPinnedDownload(
                component: ViiperInstallerComponent.ViiperBackend,
                releaseLabel: "v0.0.5",
                fileName: "viiper.exe",
                url: "https://github.com/hbashton/VIIPER/releases/download/" +
                    "v0.0.5/viiper.exe",
                sha256:
                    "3AD872D006DF2FC282E381A68B5A5B3C51E4DA3614D250AB3FDA1C272EF745D0",
                sizeInBytes: 11255296L,
                requireAuthenticode: false,
                expectedSignerCommonName: null,
                digestProvenance:
                    "Computed locally from the downloaded hbashton/VIIPER v0.0.5 " +
                    "asset and cross-checked against the digest GitHub reports " +
                    "for that same release asset.",
                notes:
                    "Unsigned upstream. The asset mis-reports its own version as " +
                    "v0.0.3-18-g02fffe6 (hbashton/VIIPER#3 fixes the release " +
                    "workflow), so the embedded version string is never a " +
                    "validation input.");

        /// <summary>
        /// The version string the pinned VIIPER asset reports about itself.
        /// Recorded so a diagnostic can say "this is the known mis-stamp"
        /// instead of a reader concluding the wrong file was downloaded.
        /// </summary>
        public const string ViiperBackendEmbeddedVersionMisstamp =
            "v0.0.3-18-g02fffe6";

        public static IReadOnlyList<ViiperPinnedDownload> All { get; } =
            new[] { UsbipWin2, ViiperBackend };

        public static ViiperPinnedDownload For(ViiperInstallerComponent component)
        {
            switch (component)
            {
                case ViiperInstallerComponent.UsbipWin2:
                    return UsbipWin2;
                case ViiperInstallerComponent.ViiperBackend:
                    return ViiperBackend;
                default:
                    throw new ArgumentOutOfRangeException(nameof(component));
            }
        }

        /// <summary>
        /// Parses the component token the setup script passes on the command
        /// line. Returns false for anything unrecognised rather than guessing —
        /// a typo must not silently verify the wrong file against the wrong pin.
        /// </summary>
        public static bool TryParseComponent(string token,
            out ViiperInstallerComponent component)
        {
            component = default;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "usbip":
                case "usbip-win2":
                    component = ViiperInstallerComponent.UsbipWin2;
                    return true;
                case "viiper":
                case "viiper-backend":
                    component = ViiperInstallerComponent.ViiperBackend;
                    return true;
                default:
                    return false;
            }
        }
    }
}

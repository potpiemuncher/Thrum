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
    /// One file inside a pinned archive whose identity must be checked after
    /// extraction and before it can be installed.
    /// </summary>
    public sealed class ViiperPinnedPayload
    {
        public ViiperPinnedPayload(string fileName, string sha256,
            long sizeInBytes)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A payload file name is required.",
                    nameof(fileName));
            if (string.IsNullOrWhiteSpace(sha256))
                throw new ArgumentException("A payload SHA-256 is required.",
                    nameof(sha256));
            if (sizeInBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

            FileName = fileName;
            Sha256 = ViiperPinnedDownload.NormalizeDigest(sha256);
            SizeInBytes = sizeInBytes;
        }

        /// <summary>The exact path-independent file name inside the archive.</summary>
        public string FileName { get; }

        /// <summary>Upper-case hexadecimal, no separators.</summary>
        public string Sha256 { get; }

        /// <summary>The exact extracted file size.</summary>
        public long SizeInBytes { get; }

        public bool MatchesDigest(string candidate) =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(ViiperPinnedDownload.NormalizeDigest(candidate),
                Sha256, StringComparison.Ordinal);
    }

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
            string notes = null, ViiperPinnedPayload extractedPayload = null,
            string expectedEmbeddedVersionStamp = null)
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
            ExtractedPayload = extractedPayload;
            ExpectedEmbeddedVersionStamp = expectedEmbeddedVersionStamp;
        }

        public ViiperInstallerComponent Component { get; }

        /// <summary>
        /// Upstream release label, e.g. <c>0.9.7.7</c> or <c>v0.0.6</c>. For the
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

        /// <summary>
        /// An executable payload whose bytes must be verified after this
        /// archive has itself passed verification, or null for a bare file.
        /// Both checks are required when present; neither substitutes for the
        /// other.
        /// </summary>
        public ViiperPinnedPayload ExtractedPayload { get; }

        /// <summary>
        /// The human-readable version stamp expected inside the pinned
        /// payload, or null when none is recorded. This is diagnostic context
        /// only and is never a validation input; the digest is the identity.
        /// </summary>
        public string ExpectedEmbeddedVersionStamp { get; }

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
        /// usbip-win2 0.9.8.0 x64 (released 2026-09-07), the first release that
        /// carries the filter memory-corruption fix <c>4139f44</c> and the UDE
        /// request-lifetime hardening this project contributed upstream
        /// (usbip-win2 PR #182).
        ///
        /// <para>The pin moved here from 0.9.7.7 on 2026-09-19. 0.9.7.8 is the
        /// release the corruption was reproduced on and is never installed by
        /// us; 0.9.7.7 predates that regression but lacks the hardening. The
        /// development PC recorded no bugchecks in ten days of daily use on
        /// 0.9.8.0 with virtual audio endpoints on, where the 0.9.7.x months
        /// had several (not all of them triaged, so that is an observation,
        /// not an attribution). Older recognised releases are upgraded
        /// to this one by setup
        /// (<see cref="ViiperUsbipInstallAction.UpgradeRecognisedToPinned"/>),
        /// because the pinned backend speaks only this release's attach
        /// ABI.</para>
        /// </summary>
        public static ViiperPinnedDownload UsbipWin2 { get; } =
            new ViiperPinnedDownload(
                component: ViiperInstallerComponent.UsbipWin2,
                releaseLabel: "0.9.8.0",
                fileName: "USBip-0.9.8.0-x64.exe",
                url: "https://github.com/vadimgrn/usbip-win2/releases/download/" +
                    "v.0.9.8.0/USBip-0.9.8.0-x64.exe",
                sha256:
                    "81F426741F7EE2ED991FEBE24A22DACA8400B6AE2F171054E3FB404897E15D39",
                sizeInBytes: 26390744L,
                requireAuthenticode: true,
                expectedSignerCommonName:
                    "Cloudyne Systems (Scheibling Consulting AB)",
                digestProvenance:
                    "SHA-256 of the v.0.9.8.0 release asset downloaded on " +
                    "2026-09-09, whose Authenticode signature was verified " +
                    "before it was installed on the maintainer's machine; " +
                    "recomputed from the retained local copy before pinning.",
                notes:
                    "Inno Setup 7.0 payload. Installs UDE DriverVer 23.56.48.757 " +
                    "and filter DriverVer 23.56.30.686, both attestation-signed " +
                    "by Microsoft Windows Hardware Compatibility Publisher; the " +
                    "installed pair is re-validated after setup rather than " +
                    "trusted from this pin.");

        /// <summary>
        /// VIIPER v0.1.2 plus this project's usbip-win2 0.9.8.0 delta, built
        /// and published by GitHub Actions on the project's own fork
        /// (<c>potpiemuncher/VIIPER</c>, tag <c>thrum-v0.1.2-usbip0980.1</c>).
        /// The framed audio/haptics protocol is upstream v0.1.2's, unchanged.
        /// Its published asset is a zip, so the archive and its executable
        /// payload carry separate, mandatory pins.
        ///
        /// <para>Why a fork build: upstream v0.1.2 (and every later upstream
        /// tag as of 2026-09-19) refuses to start unless <c>usbip --version</c>
        /// prints exactly <c>0.9.7.7</c>, and its native attach sends that
        /// release's 1100-byte <c>plugin_hardware</c>. The fork changes two
        /// things and nothing else: the prerequisite accepts 0.9.8.0, and the
        /// attach IOCTL uses 0.9.8.0's 1120-byte layout. The workflow mirrors
        /// upstream's windows/amd64 build leg step for step. VIIPER is
        /// GPL-3.0; the exact source is the tag and the source archive
        /// attached to the same release.</para>
        ///
        /// <para>Neither upstream nor the fork signs this asset or the
        /// executable inside it. The two digests are the whole identity and
        /// <see cref="ViiperPinnedDownload.RequireAuthenticode"/> is false
        /// rather than a check that would fail on every honest download. The
        /// embedded stamp remains a human-readable diagnostic cross-check,
        /// never a validation input.</para>
        /// </summary>
        public static ViiperPinnedDownload ViiperBackend { get; } =
            new ViiperPinnedDownload(
                component: ViiperInstallerComponent.ViiperBackend,
                releaseLabel: "v0.1.2-usbip0980.1",
                fileName: "viiper-windows-amd64.zip",
                url: "https://github.com/potpiemuncher/VIIPER/releases/download/" +
                    "thrum-v0.1.2-usbip0980.1/viiper-windows-amd64.zip",
                sha256:
                    "C2EFAF1E5AE5EE93EFB5838C1B49272049D3615E0C7F12E96670A5CA05EB97A8",
                sizeInBytes: 4809446L,
                requireAuthenticode: false,
                expectedSignerCommonName: null,
                digestProvenance:
                    "Reported by the fork's build workflow in SHA256SUMS.txt " +
                    "beside the asset, then recomputed locally from the " +
                    "downloaded release asset; the extracted executable was " +
                    "hashed independently from that archive and both values " +
                    "matched the workflow's.",
                notes:
                    "Unsigned. hbashton/VIIPER v0.1.2 (f5d097b) plus three " +
                    "commits: accept usbip-win2 0.9.8.0, match its 1120-byte " +
                    "plugin_hardware layout, and the build workflow. It " +
                    "requires usbip-win2 0.9.8.0 exactly and refuses to start " +
                    "on 0.9.7.x, which is why these two pins form a coherent " +
                    "pair. Not an upstream release and not supported by the " +
                    "VIIPER maintainers.",
                extractedPayload: new ViiperPinnedPayload(
                    fileName: "viiper.exe",
                    sha256:
                        "877050102C2D415561893ED9393955E4D6FEA50647AFE53FA69387EFFD4EB145",
                    sizeInBytes: 11407872L),
                expectedEmbeddedVersionStamp:
                    ViiperBackendExpectedEmbeddedVersionStamp);

        /// <summary>
        /// The human-readable version string in the pinned VIIPER executable.
        /// Diagnostics expose it as an expected-stamp cross-check only;
        /// validation continues to use the two digests.
        /// </summary>
        public const string ViiperBackendExpectedEmbeddedVersionStamp =
            "v0.1.2-usbip0980.1 (f28cab3)";

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

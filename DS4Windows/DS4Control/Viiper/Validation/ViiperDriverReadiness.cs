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
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// How far the installed usbip-win2 package pair can be trusted, as one
    /// ordered state. Ordered by how much is proven, not by severity: every
    /// state above <see cref="Missing"/> means a package is present, and only
    /// <see cref="Approved"/> means a maintainer has accepted a release for
    /// production use.
    /// </summary>
    public enum ViiperDriverReadinessState
    {
        /// <summary>
        /// No usbip-win2 driver package is installed. Proven absence: the
        /// enumeration completed and found neither package.
        /// </summary>
        Missing,

        /// <summary>
        /// A package is present but its identity or trust could not be matched
        /// to a manifest entry — a mixed pair, an unlisted version, a
        /// test-signed or untrusted catalog, or an inspection that could not be
        /// completed. Everything unproven lands here; nothing lands here by
        /// default that could have been proven better.
        /// </summary>
        DetectedUnvalidated,

        /// <summary>
        /// Both packages and the userspace client exactly match one manifest
        /// entry at <see cref="ViiperDriverTier.ExperimentalBaseline"/>. This is
        /// identity evidence, never production approval: the matched release is
        /// a known, observed package that is still known-risk.
        /// </summary>
        ValidatedExperimental,

        /// <summary>
        /// The matched manifest entry is at <see cref="ViiperDriverTier.Production"/>.
        /// The manifest deliberately has no Production entry today, so this
        /// state is unreachable in the shipped product; it exists so that adding
        /// a future accepted release is a manifest edit and nothing else.
        /// </summary>
        Approved,
    }

    /// <summary>One observed identity field of one component. Non-sensitive.</summary>
    public sealed class ViiperDriverIdentityField
    {
        public ViiperDriverIdentityField(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }

        /// <summary>
        /// The rendered line. Composed here rather than from adjacent XAML
        /// <c>Run</c> elements, which insert their own whitespace and produce
        /// "Label : Value".
        /// </summary>
        public string Display => Label + ": " + Value;
    }

    /// <summary>
    /// What was observed about one component of the installed package set.
    /// Deliberately a projection rather than the raw
    /// <see cref="ViiperDriverPackageInfo"/>: the raw record carries
    /// <see cref="ViiperDriverPackageInfo.TrustEvaluationPath"/>, a driver-store
    /// path that must never reach a log, a report, or the UI.
    /// </summary>
    public sealed class ViiperDriverComponentIdentity
    {
        public ViiperDriverComponentIdentity(string component, bool found,
            IReadOnlyList<ViiperDriverIdentityField> fields)
        {
            Component = component ?? string.Empty;
            Found = found;
            Fields = fields ?? Array.Empty<ViiperDriverIdentityField>();
        }

        /// <summary>Display name, e.g. "UDE host controller".</summary>
        public string Component { get; }

        public bool Found { get; }

        public IReadOnlyList<ViiperDriverIdentityField> Fields { get; }
    }

    /// <summary>
    /// The four-state answer to "may this machine's usbip-win2 install be
    /// trusted", plus the evidence a user needs to understand the answer.
    ///
    /// <para>Produced by <see cref="ViiperDriverReadinessResolver"/> from a
    /// read-only <see cref="ViiperDriverValidationReport"/>. Carries no
    /// driver-store paths, device instance paths, serials, or user paths, so the
    /// whole object is safe to render and to log.</para>
    /// </summary>
    public sealed class ViiperDriverReadiness
    {
        public ViiperDriverReadiness(ViiperDriverReadinessState state,
            IReadOnlyList<string> reasons,
            IReadOnlyList<ViiperDriverComponentIdentity> identities,
            string releaseLabel, ViiperDriverTier? tier,
            DateTimeOffset evaluatedAtUtc)
        {
            State = state;
            Reasons = reasons ?? Array.Empty<string>();
            Identities = identities ?? Array.Empty<ViiperDriverComponentIdentity>();
            ReleaseLabel = releaseLabel;
            Tier = tier;
            EvaluatedAtUtc = evaluatedAtUtc;
        }

        public ViiperDriverReadinessState State { get; }

        /// <summary>
        /// Why the state is not <see cref="ViiperDriverReadinessState.ValidatedExperimental"/>
        /// or better, in the order they were observed. Empty when the state
        /// needs no explanation.
        /// </summary>
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>
        /// Observed identity of each component, for the components that could
        /// be read at all.
        /// </summary>
        public IReadOnlyList<ViiperDriverComponentIdentity> Identities { get; }

        /// <summary>Matched manifest release label, or null when nothing matched.</summary>
        public string ReleaseLabel { get; }

        /// <summary>Matched manifest tier, or null when nothing matched.</summary>
        public ViiperDriverTier? Tier { get; }

        public DateTimeOffset EvaluatedAtUtc { get; }

        /// <summary>
        /// True when the package pair matched a manifest entry, at any tier.
        /// A match is identity evidence; <see cref="IsProductionApproved"/> is
        /// the separate question of whether it may be relied on.
        /// </summary>
        public bool IsManifestMatch =>
            State == ViiperDriverReadinessState.ValidatedExperimental ||
            State == ViiperDriverReadinessState.Approved;

        /// <summary>
        /// True only for a maintainer-accepted production release. No manifest
        /// entry satisfies this today, by design.
        /// </summary>
        public bool IsProductionApproved =>
            State == ViiperDriverReadinessState.Approved;
    }

    /// <summary>
    /// Maps a read-only <see cref="ViiperDriverValidationReport"/> onto the
    /// four readiness states. Pure and total: every input, including a null
    /// report or one produced by an inspection that threw, yields a state.
    ///
    /// <para><b>Fail-closed rule.</b> Only a completed inspection whose
    /// authoritative <see cref="ViiperDriverValidationResult"/> passed may
    /// produce <see cref="ViiperDriverReadinessState.ValidatedExperimental"/> or
    /// <see cref="ViiperDriverReadinessState.Approved"/>, and only a completed
    /// inspection that found neither package may produce
    /// <see cref="ViiperDriverReadinessState.Missing"/>. Anything unproven —
    /// including an inspection that threw — is
    /// <see cref="ViiperDriverReadinessState.DetectedUnvalidated"/>, because an
    /// unreadable machine is not an empty one.</para>
    /// </summary>
    public static class ViiperDriverReadinessResolver
    {
        private const string HostComponentName = "UDE host controller";
        private const string FilterComponentName = "Filter extension";
        private const string ClientComponentName = "usbip.exe client";
        private const string NotReported = "(not reported)";

        public static ViiperDriverReadiness Resolve(
            ViiperDriverValidationReport report) =>
            Resolve(report, DateTimeOffset.UtcNow);

        public static ViiperDriverReadiness Resolve(
            ViiperDriverValidationReport report, DateTimeOffset evaluatedAtUtc)
        {
            if (report == null)
            {
                return Unavailable(
                    "The driver check produced no result, so nothing about the " +
                    "installed package could be established.", evaluatedAtUtc);
            }

            List<string> reasons = new List<string>();
            ViiperDriverValidationResult result = report.Result;

            bool packageReadFailed =
                !string.IsNullOrWhiteSpace(report.PackageInspectionError);
            bool clientReadFailed =
                !string.IsNullOrWhiteSpace(report.UsbipClientInspectionError);

            if (packageReadFailed)
            {
                reasons.Add("The installed usbip-win2 driver packages could not " +
                    "be read, so neither their absence nor their identity is " +
                    "established: " + report.PackageInspectionError);
            }

            if (clientReadFailed)
            {
                reasons.Add("The usbip.exe client could not be read: " +
                    report.UsbipClientInspectionError);
            }

            if (result == null)
            {
                reasons.Add("No validation result was produced for the installed " +
                    "packages.");
            }
            else if (!result.Passed)
            {
                reasons.Add(result.Diagnostic);
            }

            AddTrustConcern(reasons, HostComponentName,
                ViiperDriverComponent.UdeHostController,
                report.HostControllerTrust, result);
            AddTrustConcern(reasons, FilterComponentName,
                ViiperDriverComponent.FilterExtension,
                report.FilterExtensionTrust, result);
            AddTrustConcern(reasons, ClientComponentName,
                ViiperDriverComponent.UsbipClient,
                report.UsbipClientTrust, result);

            bool hostFound = report.HostController != null &&
                report.HostController.Found;
            bool filterFound = report.FilterExtension != null &&
                report.FilterExtension.Found;

            if (hostFound && (!report.HostController.DeviceNodePresent ||
                !report.HostController.Started))
            {
                reasons.Add("The UDE host controller device node is present but " +
                    "is not started without a problem code.");
            }

            ViiperDriverReadinessState state;
            if (packageReadFailed)
            {
                // An unreadable machine is not an empty one: absence is not
                // proven, and neither is identity.
                state = ViiperDriverReadinessState.DetectedUnvalidated;
            }
            else if (!hostFound && !filterFound)
            {
                // Proven absence. The state carries the whole explanation, and
                // the validator's own "no packages found" diagnostic is written
                // for a report rather than for a status card.
                state = ViiperDriverReadinessState.Missing;
                reasons.Clear();
            }
            else if (clientReadFailed || result == null)
            {
                state = ViiperDriverReadinessState.DetectedUnvalidated;
            }
            else if (result.Passed)
            {
                state = result.Tier == ViiperDriverTier.Production
                    ? ViiperDriverReadinessState.Approved
                    : ViiperDriverReadinessState.ValidatedExperimental;
            }
            else
            {
                state = ViiperDriverReadinessState.DetectedUnvalidated;
            }

            // Reasons are deliberately not cleared for a match. A passing
            // result with a leftover concern would be a contradiction worth
            // showing, not one worth erasing.
            return new ViiperDriverReadiness(state, Distinct(reasons),
                BuildIdentities(report), result?.ReleaseLabel, result?.Tier,
                evaluatedAtUtc);
        }

        /// <summary>
        /// The state for a check that could not be run at all. Never better than
        /// <see cref="ViiperDriverReadinessState.DetectedUnvalidated"/>.
        /// </summary>
        public static ViiperDriverReadiness Unavailable(string reason) =>
            Unavailable(reason, DateTimeOffset.UtcNow);

        public static ViiperDriverReadiness Unavailable(string reason,
            DateTimeOffset evaluatedAtUtc) =>
            new ViiperDriverReadiness(
                ViiperDriverReadinessState.DetectedUnvalidated,
                new[]
                {
                    string.IsNullOrWhiteSpace(reason)
                        ? "The driver check could not be completed."
                        : reason,
                },
                Array.Empty<ViiperDriverComponentIdentity>(), null, null,
                evaluatedAtUtc);

        private static void AddTrustConcern(List<string> reasons,
            string componentName, ViiperDriverComponent component,
            ViiperSignatureTrust trust, ViiperDriverValidationResult result)
        {
            string concern = ViiperDriverValidator.DescribeTrustRejection(trust);
            if (trust == null || concern == null)
            {
                // Not evaluated (component absent) or clean. Neither is a reason.
                return;
            }

            // The authoritative result already names this exact problem for this
            // exact component; do not say it twice.
            if (result != null && !result.Passed &&
                result.FailedComponent == component &&
                result.Reason == ViiperDriverFailureReason.UntrustedSignature)
            {
                return;
            }

            reasons.Add(componentName + ": " + concern + ".");
        }

        private static IReadOnlyList<ViiperDriverComponentIdentity>
            BuildIdentities(ViiperDriverValidationReport report)
        {
            List<ViiperDriverComponentIdentity> identities =
                new List<ViiperDriverComponentIdentity>();

            AddPackageIdentity(identities, HostComponentName,
                report.HostController, report.HostControllerTrust);
            AddPackageIdentity(identities, FilterComponentName,
                report.FilterExtension, report.FilterExtensionTrust);
            AddClientIdentity(identities, report.UsbipClient,
                report.UsbipClientTrust);

            return identities;
        }

        private static void AddPackageIdentity(
            List<ViiperDriverComponentIdentity> identities, string componentName,
            ViiperDriverPackageInfo package, ViiperSignatureTrust trust)
        {
            if (package == null || !package.Found)
            {
                return;
            }

            identities.Add(new ViiperDriverComponentIdentity(componentName, true,
                new[]
                {
                    new ViiperDriverIdentityField("INF provider",
                        Text(package.Provider)),
                    new ViiperDriverIdentityField("INF name",
                        Text(package.InfName)),
                    new ViiperDriverIdentityField("DriverVer",
                        Text(package.DriverVersion?.ToString())),
                    new ViiperDriverIdentityField("Service",
                        Text(package.Service)),
                    new ViiperDriverIdentityField("Catalog trust",
                        DescribeTrust(trust, package.CatalogFile)),
                }));
        }

        private static void AddClientIdentity(
            List<ViiperDriverComponentIdentity> identities,
            ViiperUsbipClientInfo client, ViiperSignatureTrust trust)
        {
            if (client == null || !client.Found)
            {
                return;
            }

            identities.Add(new ViiperDriverComponentIdentity(ClientComponentName,
                true, new[]
                {
                    new ViiperDriverIdentityField("File name",
                        Text(client.FileName)),
                    new ViiperDriverIdentityField("ProductVersion",
                        Text(client.ProductVersion?.ToString())),
                    new ViiperDriverIdentityField("Signature trust",
                        DescribeTrust(trust, null)),
                }));
        }

        /// <summary>
        /// One line describing what the Windows trust APIs said about a
        /// catalog or file, including the catalog file name when there is one.
        /// The signer common name is reported because a wrong expected name has
        /// to be visible rather than silently failing closed.
        /// </summary>
        private static string DescribeTrust(ViiperSignatureTrust trust,
            string catalogFile)
        {
            string prefix = string.IsNullOrWhiteSpace(catalogFile)
                ? string.Empty
                : catalogFile + ": ";

            if (trust == null)
            {
                return prefix + "not evaluated";
            }

            string rejection = ViiperDriverValidator.DescribeTrustRejection(trust);
            if (rejection != null)
            {
                return prefix + rejection;
            }

            string signer = string.IsNullOrWhiteSpace(trust.ObservedSignerCommonName)
                ? "signer not reported"
                : trust.ObservedSignerCommonName;
            return prefix + "trusted, signed by " + signer;
        }

        private static IReadOnlyList<string> Distinct(List<string> reasons) =>
            reasons
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        private static string Text(string value) =>
            string.IsNullOrWhiteSpace(value) ? NotReported : value;
    }

    /// <summary>
    /// Evaluates driver readiness once per application session and hands out
    /// the cached answer, because a SetupAPI enumeration plus three
    /// WinVerifyTrust calls is not free and readiness only changes when
    /// something installs a driver.
    ///
    /// <para>The cache is explicit, not a timeout: <see cref="Refresh"/> is the
    /// only thing that re-reads the machine, and it exists so the Settings
    /// re-check button and the post-install path can ask again.</para>
    ///
    /// <para>Read-only. Nothing here elevates, installs, attaches, or writes.</para>
    /// </summary>
    public sealed class ViiperDriverReadinessProvider
    {
        private readonly Func<ViiperDriverValidationReport> inspect;
        private readonly object evaluationLock = new object();
        private ViiperDriverReadiness cached;
        private int evaluationCount;

        public ViiperDriverReadinessProvider(
            Func<ViiperDriverValidationReport> inspect)
        {
            this.inspect = inspect ??
                throw new ArgumentNullException(nameof(inspect));
        }

        /// <summary>The provider wired to the real OS inspectors.</summary>
        public static ViiperDriverReadinessProvider Default { get; } =
            new ViiperDriverReadinessProvider(() =>
                ViiperDriverGate.Default.Inspect(
                    ViiperDriverValidationCommand.ResolveUsbipExecutablePath()));

        /// <summary>How many times the machine has actually been read.</summary>
        public int EvaluationCount => Volatile.Read(ref evaluationCount);

        /// <summary>The cached answer, evaluating once on first use.</summary>
        public ViiperDriverReadiness Get()
        {
            ViiperDriverReadiness snapshot = Volatile.Read(ref cached);
            if (snapshot != null)
            {
                return snapshot;
            }

            lock (evaluationLock)
            {
                return cached ??= Evaluate();
            }
        }

        /// <summary>Discards the cache and reads the machine again.</summary>
        public ViiperDriverReadiness Refresh()
        {
            lock (evaluationLock)
            {
                return cached = Evaluate();
            }
        }

        /// <summary>
        /// Publishes a readiness derived from a report somebody else already
        /// paid for — the full diagnostic run, which inspects the same machine
        /// with the same gate. Avoids a second enumeration purely to refresh the
        /// card behind the report window.
        /// </summary>
        public ViiperDriverReadiness Adopt(ViiperDriverValidationReport report)
        {
            lock (evaluationLock)
            {
                Interlocked.Increment(ref evaluationCount);
                return cached = ViiperDriverReadinessResolver.Resolve(report);
            }
        }

        private ViiperDriverReadiness Evaluate()
        {
            Interlocked.Increment(ref evaluationCount);
            try
            {
                return ViiperDriverReadinessResolver.Resolve(inspect());
            }
            catch (Exception ex)
            {
                // The gate gathers defensively, so reaching here means something
                // outside it failed. Fail closed rather than report a state.
                return ViiperDriverReadinessResolver.Unavailable(
                    "The usbip-win2 driver check could not be completed (" +
                    ex.GetType().Name + ": " + ex.Message + ").");
            }
        }
    }
}

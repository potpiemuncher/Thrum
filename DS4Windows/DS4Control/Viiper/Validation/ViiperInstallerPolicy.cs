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
using System.Globalization;
using System.IO;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// What the setup script should do about the usbip-win2 kernel driver.
    /// </summary>
    public enum ViiperUsbipInstallAction
    {
        /// <summary>
        /// Nothing recognisable is installed. Fetch the pinned installer,
        /// verify it, and run it.
        /// </summary>
        InstallPinned,

        /// <summary>
        /// The pinned release is already the installed one. Do nothing.
        /// </summary>
        AlreadyPinned,

        /// <summary>
        /// A different release the manifest knows is installed. Report what it
        /// is and leave it completely alone — replacing a bound kernel package
        /// with an older one is not a repair.
        /// </summary>
        LeaveRecognisedReleaseAlone,

        /// <summary>
        /// Something is installed that cannot be matched to a manifest entry.
        /// Touch nothing and do not report success.
        /// </summary>
        RefuseUnrecognisedInstall,
    }

    /// <summary>
    /// A decision plus the log lines that justify it. The lines are the
    /// audit trail requirement 5 of plan task 2.4 asks for, produced by the
    /// same pure function that makes the call so the two cannot disagree.
    /// </summary>
    public sealed class ViiperInstallerDecision<TAction>
    {
        public ViiperInstallerDecision(TAction action, string summary,
            IReadOnlyList<string> lines)
        {
            Action = action;
            Summary = summary ?? string.Empty;
            Lines = lines ?? Array.Empty<string>();
        }

        public TAction Action { get; }

        /// <summary>One sentence naming the decision and its reason.</summary>
        public string Summary { get; }

        /// <summary>
        /// Every input the decision was made from, expected value beside
        /// observed value, in evaluation order.
        /// </summary>
        public IReadOnlyList<string> Lines { get; }
    }

    /// <summary>
    /// What was actually observed about a downloaded file. Split from the
    /// decision so the decision is a pure function of facts and the facts come
    /// from one thin, untested I/O helper rather than from the middle of the
    /// policy.
    /// </summary>
    public sealed class ViiperDownloadObservation
    {
        /// <summary>
        /// Name of the file that was actually inspected — the base name only,
        /// never a full path, because observations end up verbatim in reports
        /// and logs that must not carry user paths. Null or empty when no name
        /// was recorded.
        /// </summary>
        public string FileName { get; init; }

        /// <summary>The file exists and could be opened.</summary>
        public bool Exists { get; init; }

        public long SizeInBytes { get; init; }

        /// <summary>Hex SHA-256, or null when it could not be computed.</summary>
        public string Sha256 { get; init; }

        /// <summary>
        /// Whether Authenticode was evaluated at all. False when the pin does
        /// not require it; a pin that requires it and an observation that
        /// skipped it is an error, not a pass.
        /// </summary>
        public bool SignatureEvaluated { get; init; }

        /// <summary>Windows accepted the certificate chain under normal policy.</summary>
        public bool SignatureTrusted { get; init; }

        /// <summary>Common name read off the verified chain, or null.</summary>
        public string SignerCommonName { get; init; }

        /// <summary>Short non-sensitive reason a signature was rejected.</summary>
        public string SignatureDiagnostic { get; init; }

        /// <summary>
        /// Why the file could not be examined at all, or null. Producers
        /// redact account names before recording (exception messages embed
        /// the path they failed on), and the decision redacts again before
        /// quoting.
        /// </summary>
        public string ObservationError { get; init; }
    }

    /// <summary>Verdict on one downloaded file.</summary>
    public enum ViiperDownloadVerdict
    {
        /// <summary>Digest and (where required) signature both matched the pin.</summary>
        Approved,

        /// <summary>The file was not there, or could not be read.</summary>
        Unavailable,

        /// <summary>The bytes are not the pinned bytes.</summary>
        DigestMismatch,

        /// <summary>Windows would not accept the signature.</summary>
        SignatureNotTrusted,

        /// <summary>A valid signature, but not the expected publisher.</summary>
        UnexpectedSigner,
    }

    /// <summary>How a post-install validation attempt turned out.</summary>
    public enum ViiperPostInstallVerdict
    {
        /// <summary>The gate validated the installed package pair.</summary>
        Validated,

        /// <summary>The gate ran and refused the installed pair.</summary>
        Refused,

        /// <summary>
        /// The gate could not run, or its result could not be obtained. Treated
        /// exactly like a refusal: an unverifiable state blocks.
        /// </summary>
        CouldNotRun,
    }

    /// <summary>
    /// Every decision <c>extras/install-viiper-backend.ps1</c> makes, as pure
    /// total functions over observed facts.
    ///
    /// <para><b>Why this is C# and not PowerShell.</b> The admission rule is
    /// "the manifest decides", and the manifest is
    /// <see cref="ViiperDriverManifest"/> — a type whose own contract says it
    /// must not be duplicated into the UI, the broker or the installer. A
    /// PowerShell copy of the version table would be exactly that duplicate,
    /// and it would be the copy that decides whether a kernel driver gets
    /// installed. Keeping the decisions here means one table, one set of
    /// comparisons, and coverage from the test suite that already gates every
    /// merge.</para>
    ///
    /// <para>The script keeps the mechanical half — fetching bytes, running an
    /// installer, replacing a file — and consults these functions through
    /// <see cref="ViiperInstallerPolicyCommand"/> for every branch that can end
    /// in something being executed.</para>
    /// </summary>
    public static class ViiperInstallerPolicy
    {
        /// <summary>Setup finished and the installed package pair validated.</summary>
        public const int ScriptExitSuccess = 0;

        /// <summary>Setup refused, failed, or could not verify something.</summary>
        public const int ScriptExitFailed = 1;

        /// <summary>
        /// Setup completed its file work, but the driver package pair cannot be
        /// validated until Windows restarts. Distinct from success because
        /// nothing has been proven yet, and distinct from failure because
        /// nothing is known to be wrong.
        /// </summary>
        public const int ScriptExitRestartRequired = 3;

        /// <summary>
        /// Decides what to do about the installed usbip-win2 driver.
        ///
        /// <para>The primary input is the gate's four-state answer, not a file
        /// version. <c>usbip2_ude.sys</c> carries a DriverVer such as
        /// <c>1.45.29.368</c> that has nothing to do with the <c>0.9.7.x</c>
        /// release label, which is why upstream's <c>-ge 0.9.7.7</c> floor
        /// passes trivially on every install it has ever seen.</para>
        /// </summary>
        /// <param name="state">The gate's readiness state.</param>
        /// <param name="matchedReleaseLabel">
        /// The manifest release the installed pair matched, or null.
        /// </param>
        /// <param name="matchedTier">Tier of that release, or null.</param>
        /// <param name="reportedUninstallRelease">
        /// The release label a usbip-win2 uninstall entry reports, if any. Only
        /// consulted when the gate found no bound packages, so a
        /// half-installed or reboot-pending machine is not mistaken for an
        /// empty one.
        /// </param>
        /// <param name="pin">The release this project would install.</param>
        /// <param name="manifest">The releases the product recognises.</param>
        public static ViiperInstallerDecision<ViiperUsbipInstallAction>
            DecideUsbipInstall(ViiperDriverReadinessState state,
                string matchedReleaseLabel, ViiperDriverTier? matchedTier,
                string reportedUninstallRelease, ViiperPinnedDownload pin,
                ViiperDriverManifest manifest)
        {
            if (pin == null) throw new ArgumentNullException(nameof(pin));
            manifest ??= ViiperDriverManifest.ObservedBaselines;

            List<string> lines = new List<string>
            {
                "usbip-win2 pinned release: " + pin.ReleaseLabel + " (" +
                    pin.FileName + ").",
                "usbip-win2 installed state: " + DescribeState(state) +
                    "; matched release " + Present(matchedReleaseLabel) +
                    "; tier " + (matchedTier.HasValue
                        ? matchedTier.Value.ToString() : "(none)") + ".",
            };

            switch (state)
            {
                case ViiperDriverReadinessState.ValidatedExperimental:
                case ViiperDriverReadinessState.Approved:
                    if (LabelsMatch(matchedReleaseLabel, pin.ReleaseLabel))
                    {
                        return Decide(ViiperUsbipInstallAction.AlreadyPinned,
                            "usbip-win2 " + pin.ReleaseLabel +
                            " is already installed and matches the pinned " +
                            "release exactly; the driver step is skipped.",
                            lines);
                    }

                    lines.Add(
                        "The installed release is recognised but is not the " +
                        "pinned one. Recognising a release is not approving " +
                        "it, and replacing a bound kernel package with a " +
                        "different one is not a repair.");
                    return Decide(
                        ViiperUsbipInstallAction.LeaveRecognisedReleaseAlone,
                        "usbip-win2 " + Present(matchedReleaseLabel) +
                        " is installed. It is a release this build recognises " +
                        "as an experimental baseline, and it is left exactly " +
                        "as it is.",
                        lines);

                case ViiperDriverReadinessState.Missing:
                    return DecideWhenNothingIsBound(reportedUninstallRelease,
                        pin, manifest, lines);

                case ViiperDriverReadinessState.DetectedUnvalidated:
                    lines.Add(
                        "A usbip-win2 package is present that could not be " +
                        "matched to any release this build knows, or whose " +
                        "trust could not be established.");
                    return Decide(
                        ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
                        "Setup will not touch the installed usbip-win2 " +
                        "packages: they do not match any release this build " +
                        "recognises. Nothing is installed, removed or " +
                        "downgraded.",
                        lines);

                default:
                    // An enum value from a future build is not a licence to act.
                    lines.Add("Unrecognised readiness state value '" +
                        ((int)state).ToString(CultureInfo.InvariantCulture) +
                        "'; treated as unverifiable.");
                    return Decide(
                        ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
                        "Setup cannot establish what usbip-win2 packages are " +
                        "installed, so it will not touch them.",
                        lines);
            }
        }

        private static ViiperInstallerDecision<ViiperUsbipInstallAction>
            DecideWhenNothingIsBound(string reportedUninstallRelease,
                ViiperPinnedDownload pin, ViiperDriverManifest manifest,
                List<string> lines)
        {
            string reported = (reportedUninstallRelease ?? string.Empty).Trim();
            lines.Add("usbip-win2 uninstall entry reports: " + Present(reported) +
                ".");

            if (reported.Length == 0)
            {
                return Decide(ViiperUsbipInstallAction.InstallPinned,
                    "No usbip-win2 driver is installed. Setup will install the " +
                    "pinned release " + pin.ReleaseLabel + " after verifying it.",
                    lines);
            }

            if (LabelsMatch(reported, pin.ReleaseLabel))
            {
                lines.Add(
                    "The pinned release is registered but its packages are not " +
                    "bound. Re-running the pinned installer is the repair for " +
                    "that; it is the same release, so nothing is downgraded.");
                return Decide(ViiperUsbipInstallAction.InstallPinned,
                    "usbip-win2 " + pin.ReleaseLabel + " is registered but not " +
                    "in service. Setup will reinstall the same pinned release " +
                    "after verifying it.",
                    lines);
            }

            if (manifest.Releases.Any(release =>
                    LabelsMatch(release.ReleaseLabel, reported)))
            {
                lines.Add(
                    "A different recognised release is registered. Setup will " +
                    "not install over it: that would be a downgrade of a " +
                    "kernel driver, decided by this script rather than by the " +
                    "person who installed it.");
                return Decide(
                    ViiperUsbipInstallAction.LeaveRecognisedReleaseAlone,
                    "usbip-win2 " + reported + " is registered on this machine " +
                    "but its packages are not currently in service. Setup " +
                    "leaves it alone; a Windows restart may be needed.",
                    lines);
            }

            lines.Add(
                "The registered release is not one this build recognises.");
            return Decide(ViiperUsbipInstallAction.RefuseUnrecognisedInstall,
                "usbip-win2 " + reported + " is registered on this machine and " +
                "is not a release this build recognises. Setup will not " +
                "install, replace or remove it.",
                lines);
        }

        /// <summary>
        /// Decides whether a downloaded file may be executed or installed.
        /// Every branch other than <see cref="ViiperDownloadVerdict.Approved"/>
        /// means the file is not touched again.
        /// </summary>
        public static ViiperInstallerDecision<ViiperDownloadVerdict>
            DecideDownloadVerification(ViiperPinnedDownload pin,
                ViiperDownloadObservation observation)
        {
            if (pin == null) throw new ArgumentNullException(nameof(pin));

            // Every sentence about the file names the file that was examined,
            // not the file the pin expected: a corrupted staged copy reported
            // under the pinned name reads as an accusation against the
            // official artefact. Path.GetFileName is re-applied here so a
            // report can never carry a user path even if a caller records one.
            string observedName = Path.GetFileName(
                observation?.FileName ?? string.Empty).Trim();
            string inspected = observedName.Length == 0
                ? "the downloaded file"
                : observedName;

            List<string> lines = new List<string>
            {
                "Verifying " + inspected + " against pinned release " +
                    pin.ReleaseLabel + ".",
                "File name: expected " + pin.FileName + ", actual " +
                    (observedName.Length == 0
                        ? "(not recorded)" : observedName) + ".",
                "Source: " + pin.Url,
            };

            if (observation == null || !observation.Exists)
            {
                lines.Add("File present: expected yes, actual no" +
                    (observation?.ObservationError is string missingError &&
                        missingError.Length > 0
                        ? " (" + Redacted(missingError) + ")"
                        : string.Empty) + ".");
                return Decide(ViiperDownloadVerdict.Unavailable,
                    "Verification failed: " + inspected +
                    " is not present, so nothing about it can be verified.",
                    lines);
            }

            lines.Add("Size: expected " +
                pin.SizeInBytes.ToString(CultureInfo.InvariantCulture) +
                " bytes, actual " +
                observation.SizeInBytes.ToString(CultureInfo.InvariantCulture) +
                " bytes.");

            string actualDigest =
                ViiperPinnedDownload.NormalizeDigest(observation.Sha256);
            lines.Add("SHA-256: expected " + pin.Sha256 + ", actual " +
                (actualDigest.Length == 0 ? "(not computed)" : actualDigest) +
                ".");

            if (actualDigest.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(observation.ObservationError))
                {
                    lines.Add("Digest could not be computed: " +
                        Redacted(observation.ObservationError) + ".");
                }

                return Decide(ViiperDownloadVerdict.Unavailable,
                    "Verification failed: the SHA-256 of " + inspected +
                    " could not be computed, so it is treated as unverified.",
                    lines);
            }

            if (!pin.MatchesDigest(actualDigest))
            {
                return Decide(ViiperDownloadVerdict.DigestMismatch,
                    "Verification failed: " + inspected + " does not have " +
                    "the pinned SHA-256. The file is discarded and nothing is " +
                    "run from it.",
                    lines);
            }

            if (!pin.RequireAuthenticode)
            {
                lines.Add("Authenticode: not required for this component (" +
                    "upstream publishes it unsigned), so the pinned SHA-256 is " +
                    "the whole identity check.");
                return Decide(ViiperDownloadVerdict.Approved,
                    inspected + " matches the pinned SHA-256 for release " +
                    pin.ReleaseLabel + ".",
                    lines);
            }

            if (!observation.SignatureEvaluated)
            {
                lines.Add("Authenticode: expected a verified chain, actual " +
                    "(not evaluated).");
                return Decide(ViiperDownloadVerdict.Unavailable,
                    "Verification failed: the Authenticode signature of " +
                    inspected + " was never evaluated, and an unevaluated " +
                    "signature is not a valid one.",
                    lines);
            }

            lines.Add("Authenticode chain: expected trusted under normal " +
                "Windows policy, actual " +
                (observation.SignatureTrusted ? "trusted" : "not trusted") +
                (string.IsNullOrWhiteSpace(observation.SignatureDiagnostic)
                    ? string.Empty
                    : " (" + Redacted(observation.SignatureDiagnostic) + ")") +
                ".");

            if (!observation.SignatureTrusted)
            {
                return Decide(ViiperDownloadVerdict.SignatureNotTrusted,
                    "Verification failed: Windows does not accept the " +
                    "Authenticode signature on " + inspected + ".",
                    lines);
            }

            string signer = (observation.SignerCommonName ?? string.Empty).Trim();
            lines.Add("Authenticode signer: expected \"" +
                pin.ExpectedSignerCommonName + "\", actual " +
                (signer.Length == 0 ? "(not reported)" : "\"" + signer + "\"") +
                ".");

            if (!string.Equals(signer, pin.ExpectedSignerCommonName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Decide(ViiperDownloadVerdict.UnexpectedSigner,
                    "Verification failed: " + inspected + " carries a valid " +
                    "signature from a different publisher than the pinned one.",
                    lines);
            }

            return Decide(ViiperDownloadVerdict.Approved,
                inspected + " matches the pinned SHA-256 and is signed by " +
                "the pinned publisher.",
                lines);
        }

        /// <summary>
        /// Maps the exit code of <c>-viiperdriverdiagnostic</c> onto a verdict.
        /// Anything other than a clean pass blocks; "could not run" is a
        /// failure, not a neutral outcome.
        /// </summary>
        /// <param name="diagnosticRan">
        /// False when the diagnostic could not be started at all — for example
        /// when the application executable is not next to the script.
        /// </param>
        public static ViiperInstallerDecision<ViiperPostInstallVerdict>
            DecidePostInstallValidation(bool diagnosticRan, int exitCode)
        {
            List<string> lines = new List<string>();

            if (!diagnosticRan)
            {
                lines.Add("Post-install validation: the driver diagnostic could " +
                    "not be started.");
                return Decide(ViiperPostInstallVerdict.CouldNotRun,
                    "The installed usbip-win2 package pair could not be " +
                    "validated because the diagnostic could not run. An " +
                    "unverifiable state is treated as a failure.",
                    lines);
            }

            lines.Add("Post-install validation: driver diagnostic exit code " +
                exitCode.ToString(CultureInfo.InvariantCulture) + ".");

            switch (exitCode)
            {
                case ViiperDriverValidationCommand.ExitCodePassed:
                    return Decide(ViiperPostInstallVerdict.Validated,
                        "The installed usbip-win2 package pair matches a " +
                        "release this build recognises, and its catalogs are " +
                        "trusted.",
                        lines);

                case ViiperDriverValidationCommand.ExitCodeFailed:
                    return Decide(ViiperPostInstallVerdict.Refused,
                        "The installed usbip-win2 package pair was refused by " +
                        "the driver gate. Virtual controllers stay blocked " +
                        "until it validates.",
                        lines);

                case ViiperDriverValidationCommand.ExitCodeError:
                    return Decide(ViiperPostInstallVerdict.CouldNotRun,
                        "The driver diagnostic could not complete, so the " +
                        "installed package pair is unverified. An unverifiable " +
                        "state is treated as a failure.",
                        lines);

                default:
                    lines.Add("The exit code is not one the diagnostic " +
                        "documents (0 passed, 1 failed, 2 could not run).");
                    return Decide(ViiperPostInstallVerdict.CouldNotRun,
                        "The driver diagnostic returned an exit code this build " +
                        "does not recognise, so its result cannot be trusted.",
                        lines);
            }
        }

        /// <summary>
        /// The final exit code of the setup script, from the two facts that
        /// decide it. Kept here rather than in the script so the app-side
        /// interpretation in <see cref="DescribeInstallerExit"/> is provably the
        /// inverse of what the script produces.
        /// </summary>
        public static int ResolveScriptExitCode(ViiperPostInstallVerdict verdict,
            bool restartPending)
        {
            if (verdict == ViiperPostInstallVerdict.Validated)
            {
                return ScriptExitSuccess;
            }

            // A restart that Windows itself asked for explains an unvalidated
            // pair without anything being wrong. It is still not success.
            return restartPending ? ScriptExitRestartRequired : ScriptExitFailed;
        }

        /// <summary>
        /// What the application should tell the user, and do, when the setup
        /// script exits.
        /// </summary>
        public sealed class ViiperInstallerExitReport
        {
            public bool Succeeded { get; init; }

            /// <summary>Restart the application to pick up the new state.</summary>
            public bool RestartApplication { get; init; }

            /// <summary>True when the message should be shown as an error.</summary>
            public bool IsError { get; init; }

            public string Message { get; init; }
        }

        /// <summary>
        /// Interprets the script's exit code together with the freshly
        /// re-probed prerequisite state.
        /// </summary>
        /// <param name="exitCode">The script's process exit code.</param>
        /// <param name="ready">Whether the backend can now run.</param>
        /// <param name="logPath">Where the script wrote its decisions.</param>
        public static ViiperInstallerExitReport DescribeInstallerExit(
            int exitCode, bool ready, string logPath)
        {
            string productName = ProductInfo.ProductName;
            string logSuffix = string.IsNullOrWhiteSpace(logPath)
                ? string.Empty
                : "\n\nEvery decision setup made was written to:\n" + logPath;

            if (exitCode == ScriptExitSuccess && ready)
            {
                return new ViiperInstallerExitReport
                {
                    Succeeded = true,
                    RestartApplication = true,
                    IsError = false,
                    Message = "VIIPER setup finished. The installed usbip-win2 " +
                        "package pair was validated. Restarting " + productName +
                        ".",
                };
            }

            if (exitCode == ScriptExitRestartRequired)
            {
                return new ViiperInstallerExitReport
                {
                    Succeeded = false,
                    RestartApplication = false,
                    IsError = false,
                    Message = "VIIPER was installed, but Windows has to restart " +
                        "before the driver packages can be validated. Restart " +
                        "Windows once, then use Refresh." + logSuffix,
                };
            }

            if (exitCode == ScriptExitSuccess)
            {
                // Setup validated the driver, but the backend still is not
                // answering. Nothing is known to be broken; nothing is proven
                // working either.
                return new ViiperInstallerExitReport
                {
                    Succeeded = false,
                    RestartApplication = false,
                    IsError = false,
                    Message = "VIIPER setup reported success, but " + productName +
                        " cannot see every component as ready yet. Restart " +
                        "Windows once, then use Refresh." + logSuffix,
                };
            }

            return new ViiperInstallerExitReport
            {
                Succeeded = false,
                RestartApplication = false,
                IsError = true,
                Message = "VIIPER setup did not finish (exit code " +
                    exitCode.ToString(CultureInfo.InvariantCulture) + ").\n\n" +
                    "Setup refuses rather than guesses: an unverified download, " +
                    "an unrecognised driver package, or a backend it could not " +
                    "stop all end here, and none of them change anything on the " +
                    "machine." + logSuffix,
            };
        }

        /// <summary>
        /// What to do about VIIPER autostart entries that already exist.
        ///
        /// <para>Setup never creates them, so anything found was created by
        /// something else — a previous install, the upstream script, or
        /// <c>viiper.exe install</c> run by hand. Adopting that silently would
        /// leave a backend running before the application starts, which is a
        /// backend the application will never own, never stop, and — because
        /// neither mechanism passes <c>--update-notify none</c> — one whose
        /// self-updater is live.</para>
        /// </summary>
        /// <param name="status">What the read-only detector found.</param>
        /// <param name="removalRequested">
        /// Whether the user asked, in this run, for them to be removed.
        /// </param>
        public static ViiperInstallerDecision<ViiperAutostartPlanAction>
            PlanAutostartRemoval(ViiperAutostartStatus status,
                bool removalRequested)
        {
            List<string> lines = new List<string>();

            if (status == null)
            {
                lines.Add("VIIPER autostart: could not be inspected.");
                return Decide(ViiperAutostartPlanAction.CouldNotInspect,
                    "Setup could not check whether VIIPER starts at logon.",
                    lines);
            }

            if (!string.IsNullOrEmpty(status.InspectionError))
            {
                lines.Add("VIIPER autostart: inspection error - " +
                    Redacted(status.InspectionError) + ".");
                return Decide(ViiperAutostartPlanAction.CouldNotInspect,
                    "Setup could not check whether VIIPER starts at logon: " +
                    Redacted(status.InspectionError) + ".",
                    lines);
            }

            if (!status.Any)
            {
                lines.Add("VIIPER autostart: none found. Setup does not create " +
                    "any: " + ProductInfo.ProductName + " starts the backend " +
                    "when a profile needs it and stops it on exit.");
                return Decide(ViiperAutostartPlanAction.NothingToDo,
                    "VIIPER does not start at logon, and setup does not make " +
                    "it start at logon.",
                    lines);
            }

            foreach (ViiperAutostartEntry entry in status.Entries)
            {
                lines.Add("VIIPER autostart found: " + entry.Description +
                    " -> " + Redacted(entry.Target));
            }

            lines.Add("Neither autostart mechanism passes --update-notify none, " +
                "so a backend started by one of them runs with its self-updater " +
                "enabled (issue #8).");

            if (removalRequested)
            {
                return Decide(ViiperAutostartPlanAction.Remove,
                    "Removing " + status.Entries.Count +
                    " existing VIIPER autostart entr" +
                    (status.Entries.Count == 1 ? "y" : "ies") +
                    " at your request.",
                    lines);
            }

            lines.Add("Left in place: removing somebody else's autostart entry " +
                "without being asked is not setup's decision.");
            return Decide(ViiperAutostartPlanAction.OfferRemoval,
                "VIIPER is set to start at logon by an entry setup did not " +
                "create. " + ProductInfo.ProductName + " does not need it, and " +
                "Settings has one-click removal.",
                lines);
        }

        private static ViiperInstallerDecision<T> Decide<T>(T action,
            string summary, List<string> lines)
        {
            lines.Add("Decision: " + summary);
            return new ViiperInstallerDecision<T>(action, summary,
                lines.ToArray());
        }

        /// <summary>
        /// Everything this policy quotes from outside itself — exception
        /// messages, signature diagnostics, autostart targets — passes
        /// through the report redaction rule here, whatever its producer
        /// already did. A report must not depend on every producer
        /// remembering to redact.
        /// </summary>
        private static string Redacted(string text) =>
            ViiperDriverReportFormatter.RedactUserPathsInText(text);

        private static bool LabelsMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(NormalizeLabel(left), NormalizeLabel(right),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Release labels reach us as <c>0.9.7.7</c>, <c>v.0.9.7.7</c> and
        /// <c>v0.0.5</c> depending on who wrote them down. Only the leading
        /// <c>v</c>/<c>v.</c> is normalised away; the digits are compared
        /// literally, because "close enough" is how a floor comparison gets
        /// reinvented.
        /// </summary>
        private static string NormalizeLabel(string label)
        {
            string text = label.Trim();
            if (text.StartsWith("v.", StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring(2).Trim();
            }

            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring(1).Trim();
            }

            return text;
        }

        private static string DescribeState(ViiperDriverReadinessState state)
        {
            switch (state)
            {
                case ViiperDriverReadinessState.Missing:
                    return "no packages bound";
                case ViiperDriverReadinessState.DetectedUnvalidated:
                    return "present but unvalidated";
                case ViiperDriverReadinessState.ValidatedExperimental:
                    return "matches a recognised experimental baseline";
                case ViiperDriverReadinessState.Approved:
                    return "matches an approved release";
                default:
                    return "unknown";
            }
        }

        private static string Present(string value) =>
            string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }

    /// <summary>What setup should do about pre-existing VIIPER autostart.</summary>
    public enum ViiperAutostartPlanAction
    {
        /// <summary>No entry exists, and setup creates none.</summary>
        NothingToDo,

        /// <summary>Entries exist; report them and point at the removal switch.</summary>
        OfferRemoval,

        /// <summary>Entries exist and removal was explicitly requested.</summary>
        Remove,

        /// <summary>The check itself failed; never reported as "none".</summary>
        CouldNotInspect,
    }
}

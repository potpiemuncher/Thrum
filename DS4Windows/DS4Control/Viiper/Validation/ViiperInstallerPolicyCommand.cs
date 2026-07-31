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
using System.Security.Cryptography;
using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// The <c>-viiperinstallerpolicy</c> switch: the read-only decision service
    /// <c>extras/install-viiper-backend.ps1</c> consults before it does
    /// anything irreversible.
    ///
    /// <para><b>Contract with the script.</b> Every verb writes a UTF-8
    /// <c>key=value</c> file at <c>--out</c> and returns an exit code. Results
    /// go to a file rather than to stdout because this is a WPF (GUI subsystem)
    /// process: whether its console output reaches a caller's pipe depends on
    /// how the caller launched it, and a verification result that sometimes
    /// arrives is not a verification result. Lines beginning <c>log=</c> are the
    /// decision audit trail and the script copies them verbatim into
    /// <c>install.log</c>.</para>
    ///
    /// <para>Read-only with respect to the machine, with exactly one
    /// exception: <c>autostart --remove</c>, which is reached only when the
    /// person running setup asked for it. Nothing here installs, elevates,
    /// attaches, starts a backend, or touches a driver.</para>
    /// </summary>
    public static class ViiperInstallerPolicyCommand
    {
        /// <summary>The verb produced a decision. Read it from the out-file.</summary>
        public const int ExitDecided = 0;

        /// <summary>The verb produced a refusal.</summary>
        public const int ExitRefused = 1;

        /// <summary>
        /// The verb could not run: a bad argument list, an unwritable out-file,
        /// or an unexpected failure. The script treats this exactly like a
        /// refusal; it is distinct only so the cause is visible in the log.
        /// </summary>
        public const int ExitCouldNotRun = 2;

        private const string VerbPins = "pins";
        private const string VerbVerifyFile = "verify-file";
        private const string VerbUsbipDecision = "usbip-decision";
        private const string VerbValidateInstalled = "validate-installed";
        private const string VerbAutostart = "autostart";

        /// <summary>
        /// Runs one verb. <paramref name="args"/> is everything after the
        /// <c>-viiperinstallerpolicy</c> switch.
        /// </summary>
        public static int Run(IReadOnlyList<string> args)
        {
            List<string> output = new List<string>();
            int exitCode;

            try
            {
                exitCode = Dispatch(args ?? Array.Empty<string>(), output);
            }
            catch (Exception ex)
            {
                output.Add("error=" + Sanitize(DescribeException(ex)));
                output.Add("log=Installer policy could not run: " +
                    Sanitize(ViiperDriverReportFormatter.RedactUserPathsInText(
                        ex.Message)));
                exitCode = ExitCouldNotRun;
            }

            output.Insert(0, "exitcode=" +
                exitCode.ToString(CultureInfo.InvariantCulture));

            string outPath = ReadOption(args, "--out");
            if (!TryWrite(outPath, output))
            {
                // The script cannot read a decision it never received, and it
                // must not proceed on silence.
                return ExitCouldNotRun;
            }

            return exitCode;
        }

        private static int Dispatch(IReadOnlyList<string> args,
            List<string> output)
        {
            string verb = args.Count > 0 ? args[0].Trim().ToLowerInvariant() : null;
            switch (verb)
            {
                case VerbPins:
                    return EmitPins(output);
                case VerbVerifyFile:
                    return VerifyFile(args, output);
                case VerbUsbipDecision:
                    return DecideUsbip(args, output);
                case VerbValidateInstalled:
                    return ValidateInstalled(output);
                case VerbAutostart:
                    return PlanAutostart(args, output);
                default:
                    output.Add("error=unknown verb");
                    output.Add("log=Installer policy was asked for an unknown " +
                        "action '" + Sanitize(verb ?? string.Empty) + "'.");
                    return ExitCouldNotRun;
            }
        }

        /// <summary>
        /// Emits the pinned identities and the backend argument vector. The
        /// script holds no URL, digest or backend flag of its own; it asks for
        /// them here so there is exactly one place a pin can be changed and
        /// exactly one place a fallback could be introduced.
        /// </summary>
        private static int EmitPins(List<string> output)
        {
            foreach (ViiperPinnedDownload pin in ViiperInstallerPins.All)
            {
                string prefix = Key(pin.Component) + ".";
                output.Add(prefix + "release=" + pin.ReleaseLabel);
                output.Add(prefix + "filename=" + pin.FileName);
                output.Add(prefix + "url=" + pin.Url);
                output.Add(prefix + "sha256=" + pin.Sha256);
                output.Add(prefix + "size=" +
                    pin.SizeInBytes.ToString(CultureInfo.InvariantCulture));
                output.Add(prefix + "requireauthenticode=" +
                    (pin.RequireAuthenticode ? "true" : "false"));
                output.Add(prefix + "signer=" +
                    Sanitize(pin.ExpectedSignerCommonName ?? string.Empty));
                output.Add("log=Pinned " + Key(pin.Component) + ": " +
                    pin.FileName + " (" + pin.ReleaseLabel + "), SHA-256 " +
                    pin.Sha256 + ", " + (pin.RequireAuthenticode
                        ? "signed by \"" + pin.ExpectedSignerCommonName + "\""
                        : "unsigned upstream") + ".");
            }

            // The one argument vector that starts the backend, from the same
            // constant the application spawns with (issue #8). The script must
            // not spell it out again.
            output.Add("viiper.serverargs=" +
                string.Join(" ", ViiperBackendSpawn.ServerArguments));
            output.Add("viiper.updatenotifyenv=" +
                ViiperBackendSpawn.UpdateNotifyEnvironmentVariable);
            output.Add("viiper.updatenotifyvalue=" +
                ViiperBackendSpawn.UpdateNotifyDisabled);
            output.Add("log=Backend start arguments: " +
                string.Join(" ", ViiperBackendSpawn.ServerArguments) +
                " (the update notifier is disabled on every path that starts " +
                "the backend).");
            return ExitDecided;
        }

        private static int VerifyFile(IReadOnlyList<string> args,
            List<string> output)
        {
            string componentToken = ReadOption(args, "--component");
            if (!ViiperInstallerPins.TryParseComponent(componentToken,
                    out ViiperInstallerComponent component))
            {
                output.Add("error=unknown component");
                output.Add("log=Installer policy was asked to verify an unknown " +
                    "component '" + Sanitize(componentToken ?? string.Empty) +
                    "'.");
                return ExitCouldNotRun;
            }

            string path = ReadOption(args, "--path");
            if (string.IsNullOrWhiteSpace(path))
            {
                output.Add("error=missing path");
                output.Add("log=Installer policy was asked to verify a file " +
                    "without being told which one.");
                return ExitCouldNotRun;
            }

            ViiperPinnedDownload pin = ViiperInstallerPins.For(component);
            ViiperDownloadObservation observation = Observe(path, pin);
            ViiperInstallerDecision<ViiperDownloadVerdict> decision =
                ViiperInstallerPolicy.DecideDownloadVerification(pin, observation);

            output.Add("verdict=" + decision.Action);
            output.Add("summary=" + Sanitize(decision.Summary));
            output.Add("expectedsha256=" + pin.Sha256);
            output.Add("actualsha256=" + Sanitize(observation.Sha256 ?? string.Empty));
            output.Add("expectedsigner=" +
                Sanitize(pin.ExpectedSignerCommonName ?? string.Empty));
            output.Add("actualsigner=" +
                Sanitize(observation.SignerCommonName ?? string.Empty));
            AppendLog(output, decision.Lines);

            return decision.Action == ViiperDownloadVerdict.Approved
                ? ExitDecided
                : ExitRefused;
        }

        /// <summary>
        /// The only I/O in this file's decision path, kept deliberately thin:
        /// read the length, hash the bytes, and — when the pin requires it —
        /// ask Windows about the signature. Everything judgemental happens in
        /// <see cref="ViiperInstallerPolicy"/>.
        /// </summary>
        private static ViiperDownloadObservation Observe(string path,
            ViiperPinnedDownload pin)
        {
            // Base name only: the observation is quoted verbatim in reports
            // and logs, and those must not carry user paths.
            string fileName = Path.GetFileName(path);

            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists)
                {
                    return new ViiperDownloadObservation
                    {
                        FileName = fileName,
                        Exists = false,
                    };
                }
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is NotSupportedException)
            {
                return new ViiperDownloadObservation
                {
                    FileName = fileName,
                    Exists = false,
                    ObservationError = DescribeException(ex),
                };
            }

            string digest;
            try
            {
                using FileStream stream = File.OpenRead(path);
                digest = Convert.ToHexString(SHA256.HashData(stream));
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                return new ViiperDownloadObservation
                {
                    FileName = fileName,
                    Exists = true,
                    SizeInBytes = info.Length,
                    ObservationError = DescribeException(ex),
                };
            }

            if (!pin.RequireAuthenticode)
            {
                return new ViiperDownloadObservation
                {
                    FileName = fileName,
                    Exists = true,
                    SizeInBytes = info.Length,
                    Sha256 = digest,
                    SignatureEvaluated = false,
                };
            }

            ViiperSignatureTrust trust =
                new WinTrustAuthenticodeVerifier().VerifyFile(path);
            return new ViiperDownloadObservation
            {
                FileName = fileName,
                Exists = true,
                SizeInBytes = info.Length,
                Sha256 = digest,
                SignatureEvaluated = true,
                SignatureTrusted = trust.Trusted,
                SignerCommonName = trust.ObservedSignerCommonName,
                SignatureDiagnostic = trust.Diagnostic,
            };
        }

        private static int DecideUsbip(IReadOnlyList<string> args,
            List<string> output)
        {
            // The gate, not a file version. Read-only: it enumerates driver
            // packages and verifies catalog trust, and does nothing else.
            ViiperDriverReadiness readiness =
                ViiperSetupManager.RefreshDriverReadiness();

            ViiperInstallerDecision<ViiperUsbipInstallAction> decision =
                ViiperInstallerPolicy.DecideUsbipInstall(
                    readiness.State, readiness.ReleaseLabel, readiness.Tier,
                    ReadOption(args, "--uninstall-version"),
                    ViiperInstallerPins.UsbipWin2,
                    ViiperDriverManifest.ObservedBaselines);

            output.Add("action=" + decision.Action);
            output.Add("summary=" + Sanitize(decision.Summary));
            output.Add("readiness=" + readiness.State);
            output.Add("matchedrelease=" +
                Sanitize(readiness.ReleaseLabel ?? string.Empty));
            foreach (string reason in readiness.Reasons)
            {
                output.Add("log=Driver gate reason: " + Sanitize(reason));
            }

            AppendLog(output, decision.Lines);
            return decision.Action ==
                ViiperUsbipInstallAction.RefuseUnrecognisedInstall
                ? ExitRefused
                : ExitDecided;
        }

        /// <summary>
        /// Validates the package pair Windows actually bound, after the driver
        /// step. Runs <see cref="ViiperDriverValidationCommand.RunDiagnostic"/>
        /// — the same implementation, and the same 0/1/2 exit code, that the
        /// <c>-viiperdriverdiagnostic</c> switch runs — and reports the verdict
        /// through the out-file instead of through a console.
        ///
        /// <para>Going through the out-file rather than launching
        /// <c>-viiperdriverdiagnostic</c> as a second process is deliberate.
        /// That switch prints to an attached parent console and, when there is
        /// none, opens a modal report window; a setup script that is sometimes
        /// blocked on a dialog nobody can see is not a verification step.</para>
        /// </summary>
        private static int ValidateInstalled(List<string> output)
        {
            ViiperDriverDiagnosticRun run =
                ViiperDriverValidationCommand.RunDiagnostic();

            ViiperInstallerDecision<ViiperPostInstallVerdict> decision =
                ViiperInstallerPolicy.DecidePostInstallValidation(true,
                    run.ExitCode);

            output.Add("verdict=" + decision.Action);
            output.Add("summary=" + Sanitize(decision.Summary));
            output.Add("diagnosticexit=" +
                run.ExitCode.ToString(CultureInfo.InvariantCulture));
            output.Add("reportpath=" + Sanitize(run.DisplayPath ?? string.Empty));
            AppendLog(output, decision.Lines);
            if (!string.IsNullOrWhiteSpace(run.DisplayPath))
            {
                output.Add("log=Full driver report saved to " +
                    Sanitize(run.DisplayPath) + ".");
            }

            return decision.Action == ViiperPostInstallVerdict.Validated
                ? ExitDecided
                : ExitRefused;
        }

        private static int PlanAutostart(IReadOnlyList<string> args,
            List<string> output)
        {
            bool removalRequested = HasFlag(args, "--remove");
            ViiperAutostartStatus status = ViiperAutostart.Inspect();
            ViiperInstallerDecision<ViiperAutostartPlanAction> decision =
                ViiperInstallerPolicy.PlanAutostartRemoval(status,
                    removalRequested);

            output.Add("action=" + decision.Action);
            output.Add("summary=" + Sanitize(decision.Summary));
            output.Add("count=" + status.Entries.Count.ToString(
                CultureInfo.InvariantCulture));
            AppendLog(output, decision.Lines);

            if (decision.Action == ViiperAutostartPlanAction.Remove)
            {
                foreach (string outcome in ViiperAutostart.Remove(status.Entries))
                {
                    output.Add("log=" + Sanitize(outcome));
                }
            }

            return decision.Action == ViiperAutostartPlanAction.CouldNotInspect
                ? ExitCouldNotRun
                : ExitDecided;
        }

        private static void AppendLog(List<string> output,
            IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                output.Add("log=" + Sanitize(line));
            }
        }

        private static string Key(ViiperInstallerComponent component) =>
            component == ViiperInstallerComponent.UsbipWin2 ? "usbip" : "viiper";

        /// <summary>
        /// The one form an exception may take in an observation or an
        /// out-file: its type name for triage, and its message with any
        /// account name redacted, because Windows I/O messages embed the full
        /// path they failed on and everything recorded here is quoted
        /// verbatim in reports.
        /// </summary>
        private static string DescribeException(Exception ex) =>
            ex.GetType().Name + ": " +
            ViiperDriverReportFormatter.RedactUserPathsInText(ex.Message);

        /// <summary>
        /// The out-file is line-oriented, so a value may not contain a line
        /// break. Nothing emitted here is user-supplied text, but an exception
        /// message can be multi-line and would otherwise desynchronise the
        /// reader.
        /// </summary>
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r\n", " ").Replace('\r', ' ')
                .Replace('\n', ' ').Trim();
        }

        private static string ReadOption(IReadOnlyList<string> args, string name)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Count - 1; i++)
            {
                if (string.Equals(args[i], name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static bool HasFlag(IReadOnlyList<string> args, string name) =>
            args != null && args.Any(arg =>
                string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

        private static bool TryWrite(string path, IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(path, lines, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is NotSupportedException)
            {
                return false;
            }
        }
    }
}

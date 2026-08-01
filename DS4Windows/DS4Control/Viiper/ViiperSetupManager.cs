/*
DS4Windows
Copyright (C) 2026 hbashton

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace DS4Windows
{
    public sealed class ViiperPrerequisiteStatus
    {
        public bool ViiperInstalled { get; set; }
        public bool ServerRunning { get; set; }
        public bool UsbipInstalled { get; set; }
        public bool SetupScriptFound { get; set; }
        public string ViiperPath { get; set; }
        public string SetupScriptPath { get; set; }

        /// <summary>
        /// How far the installed usbip-win2 package pair could be validated.
        /// Deliberately separate from <see cref="Ready"/>: readiness answers
        /// "can the backend run", the tier answers "how much do we know about
        /// the kernel package it will run on". Null only before the first
        /// evaluation.
        /// </summary>
        public ViiperDriverReadiness DriverReadiness { get; set; }

        /// <summary>
        /// The backend can run: a USB/IP server is answering and a usbip-win2
        /// driver is installed.
        ///
        /// <para>Unchanged by the four-state driver validation on purpose.
        /// <see cref="Ready"/> has existing consumers — the profile-time
        /// prerequisite prompt, the output-device attach paths, the debugger,
        /// the first-run dialog, and the post-install restart branch — and all
        /// of them are asking the transport question, not the trust question.
        /// Making <see cref="Ready"/> false for
        /// <see cref="ViiperDriverReadinessState.DetectedUnvalidated"/> would
        /// silently turn a validation result into a functional refusal in
        /// flows that never opted into one. Refusal behaviour is a separate,
        /// explicit decision; read <see cref="DriverReadiness"/> for it.</para>
        /// </summary>
        public bool Ready => ServerRunning && UsbipInstalled;

        public string DisplayText
        {
            get
            {
                if (Ready)
                {
                    return "VIIPER ready";
                }

                if (!UsbipInstalled && !ViiperInstalled)
                {
                    return "VIIPER and usbip-win2 need setup";
                }

                if (!UsbipInstalled)
                {
                    return "usbip-win2 driver missing";
                }

                if (!ViiperInstalled)
                {
                    return "VIIPER helper missing";
                }

                return ServerRunning ? "VIIPER status unknown" : "VIIPER server not running";
            }
        }

        /// <summary>
        /// The component-by-component readout: which of the three legs
        /// (helper, driver, server) is present. Shared between the Settings
        /// card and the service-start log line so the two can never tell
        /// different stories about the same machine.
        /// </summary>
        public string ComponentSummary =>
            $"VIIPER helper: {(ViiperInstalled ? "installed" : "missing")}; " +
            $"usbip-win2: {(UsbipInstalled ? "installed" : "missing")}; " +
            $"server: {(ServerRunning ? "running" : "not running")}";

        /// <summary>
        /// The line the service logs when it starts. "Ready" is claimed only
        /// when <see cref="Ready"/> is true — the API probe answered and the
        /// driver is installed — because a log that says "ready" on a machine
        /// with neither misleads support triage (Phase 2 VM validation,
        /// incidental defect 1).
        /// </summary>
        public string StartupLogLine => Ready
            ? "VIIPER virtual-controller backend ready"
            : $"VIIPER virtual-controller backend not ready ({DisplayText}). {ComponentSummary}.";
    }

    /// <summary>
    /// One request, one connection, NUL-terminated path, response read until
    /// the server closes the socket. That is the whole VIIPER API framing, and
    /// this is the single place it is spelled out for callers that are not
    /// holding a device stream.
    /// </summary>
    public static class ViiperApiProbe
    {
        /// <summary>
        /// Sends <paramref name="path"/> and returns the response body, or null
        /// if the backend could not be reached or did not answer in time.
        /// </summary>
        public static string Request(string path, int timeoutMilliseconds = 1500)
        {
            try
            {
                using TcpClient tcp = new TcpClient
                {
                    NoDelay = true,
                    SendTimeout = timeoutMilliseconds,
                    ReceiveTimeout = timeoutMilliseconds,
                };

                IAsyncResult result = tcp.BeginConnect(
                    ViiperSetupManager.ApiHost, ViiperSetupManager.ApiPort, null, null);
                if (!result.AsyncWaitHandle.WaitOne(
                    TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                {
                    return null;
                }

                tcp.EndConnect(result);
                NetworkStream stream = tcp.GetStream();
                byte[] request = Encoding.UTF8.GetBytes(path + "\0");
                stream.Write(request, 0, request.Length);

                using MemoryStream body = new MemoryStream();
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    body.Write(buffer, 0, read);
                    if (body.Length > 512 * 1024)
                    {
                        break;
                    }
                }

                return Encoding.UTF8.GetString(body.ToArray()).Trim();
            }
            catch
            {
                return null;
            }
        }
    }

    public static class ViiperSetupManager
    {
        public const string ApiHost = "127.0.0.1";
        public const int ApiPort = 3242;
        public const string UsbipWin2ReleasesUrl = "https://github.com/vadimgrn/usbip-win2/releases";
        public const string ViiperReleasesUrl = "https://github.com/hbashton/VIIPER/releases";

        /// <summary>
        /// How long the backend is given to leave on its own after a console
        /// break before it is killed. Generous next to the few milliseconds it
        /// actually takes, and bounded because it runs inside the application's
        /// shutdown budget.
        /// </summary>
        private static readonly TimeSpan BackendStopGracePeriod =
            TimeSpan.FromSeconds(3);

        private const string InstallerScriptName = "install-viiper-backend.ps1";
        private static readonly object serverStartLock = new object();
        private static DateTime lastServerStartAttemptUtc = DateTime.MinValue;
        private static int promptShownThisSession;
        private static int installerRunning;

        /// <summary>
        /// The backend process this application started, if it started one.
        /// In-memory only, and never written for a server we merely found
        /// running — see <see cref="ViiperOwnedBackend"/>.
        /// </summary>
        private static ViiperOwnedBackend ownedBackend;

        /// <summary>
        /// Identity of the backend process this application started, or null
        /// when the backend was already running (or has not been started).
        /// </summary>
        public static ViiperOwnedBackend OwnedBackend => Volatile.Read(ref ownedBackend);

        public static bool IsViiperOutputType(OutContType type) => ViiperOutDevice.IsViiperType(type);

        /// <summary>
        /// The session's usbip-win2 driver validation result, evaluated once on
        /// first use. Read-only with respect to the system: it enumerates
        /// driver packages and verifies catalog trust, and does nothing else.
        /// </summary>
        public static ViiperDriverReadiness DriverReadiness =>
            ViiperDriverReadinessProvider.Default.Get();

        /// <summary>
        /// Re-reads the machine and replaces the cached driver validation
        /// result. The entry point behind the Settings re-check button and the
        /// post-install refresh; ordinary status polling must use
        /// <see cref="DriverReadiness"/> so the SetupAPI and WinVerifyTrust work
        /// happens once.
        /// </summary>
        public static ViiperDriverReadiness RefreshDriverReadiness() =>
            ViiperDriverReadinessProvider.Default.Refresh();

        public static ViiperPrerequisiteStatus GetStatus(bool tryStartServer = false)
        {
            string viiperPath = GetViiperExePath();
            string setupScriptPath = GetSetupScriptPath();
            ViiperPrerequisiteStatus status = new ViiperPrerequisiteStatus
            {
                ViiperPath = viiperPath,
                SetupScriptPath = setupScriptPath,
                ViiperInstalled = File.Exists(viiperPath),
                SetupScriptFound = File.Exists(setupScriptPath),
                // Unchanged, and deliberately not derived from the gate:
                // UsbipInstalled feeds Ready, and Ready is the transport
                // question. The gate's answer rides alongside in
                // DriverReadiness.
                UsbipInstalled = IsUsbipWin2Installed(),
                ServerRunning = CanPingServer(),
                DriverReadiness = DriverReadiness,
            };

            if (tryStartServer && !status.ServerRunning && status.ViiperInstalled)
            {
                TryStartServerOnce(viiperPath);
                status.ServerRunning = CanPingServer();
            }

            return status;
        }

        /// <summary>
        /// The one-time acknowledgement that virtual controllers depend on an
        /// experimental third-party kernel driver, asked at the moment the user
        /// first chooses a VIIPER output in a profile.
        ///
        /// <para>Once accepted it is persisted and never asked again; the switch
        /// in Settings is the way back out. Declining is recorded as nothing —
        /// the gate keeps refusing and the Output Slots banner keeps saying
        /// why — so a user who says no is not asked again in a loop by this
        /// path either: the flag stays false and the prompt only fires from a
        /// deliberate output-type change.</para>
        ///
        /// <para>Says nothing about audio. Controller-only emulation does not
        /// reach the known kernel defect, and folding the two disclosures
        /// together would make the audio one routine.</para>
        /// </summary>
        /// <returns>True when virtual controller output is acknowledged.</returns>
        public static bool EnsureExperimentalAcknowledgedWithPrompt(Window owner)
        {
            if (Global.ViiperExperimentalAcknowledged)
            {
                return true;
            }

            MessageBoxResult result = owner != null
                ? MessageBox.Show(owner,
                    ViiperExperimentalDisclosure.AcknowledgementBody,
                    ViiperExperimentalDisclosure.AcknowledgementTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No)
                : MessageBox.Show(
                    ViiperExperimentalDisclosure.AcknowledgementBody,
                    ViiperExperimentalDisclosure.AcknowledgementTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                AppLogger.LogToGui(
                    "Virtual controller output stays disabled: the experimental " +
                    "kernel driver notice was declined. It can be accepted later " +
                    "in Settings.", false);
                return false;
            }

            Global.ViiperExperimentalAcknowledged = true;
            Global.Save();
            return true;
        }

        public static bool EnsureReadyWithPrompt(Window owner, bool forcePrompt = false)
        {
            ViiperPrerequisiteStatus status = GetStatus(tryStartServer: true);
            if (status.Ready)
            {
                return true;
            }

            if (Volatile.Read(ref promptShownThisSession) == 1 && !forcePrompt)
            {
                return false;
            }

            Interlocked.Exchange(ref promptShownThisSession, 1);
            string message =
                "This profile uses a VIIPER virtual controller output.\n\n" +
                ProductInfo.ProductName + " needs two pieces installed before this can work:\n" +
                "- VIIPER helper/server\n" +
                "- usbip-win2 Windows USB/IP driver\n\n" +
                $"Current status: {status.DisplayText}\n\n" +
                "Install or repair VIIPER support now?";

            MessageBoxResult result = owner != null
                ? MessageBox.Show(owner, message, "VIIPER virtual controller setup", MessageBoxButton.YesNo, MessageBoxImage.Information)
                : MessageBox.Show(message, "VIIPER virtual controller setup", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                return false;
            }

            return LaunchInstaller(status, owner);
        }

        public static bool LaunchInstaller(ViiperPrerequisiteStatus status = null, Window owner = null)
        {
            status ??= GetStatus();
            if (!status.SetupScriptFound)
            {
                string message =
                    ProductInfo.ProductName + " could not find the bundled VIIPER setup script.\n\n" +
                    "Opening the VIIPER and usbip-win2 release pages instead.";
                if (owner != null)
                {
                    MessageBox.Show(owner, message, "VIIPER setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(message, "VIIPER setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Util.StartProcessHelper(ViiperReleasesUrl);
                Util.StartProcessHelper(UsbipWin2ReleasesUrl);
                return false;
            }

            if (Interlocked.CompareExchange(ref installerRunning, 1, 0) != 0)
            {
                ShowInstallerMessage(owner,
                    "VIIPER setup is already running. Finish the open setup window, then use Refresh to verify it.",
                    "VIIPER setup", MessageBoxImage.Information);
                return true;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{status.SetupScriptPath}\" -NoPause",
                    UseShellExecute = true,
                    Verb = "runas",
                };
                Process process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException(
                        "Windows did not start the setup process.");
                }

                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => InstallerProcess_Exited(process,
                    owner);
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Interlocked.Exchange(ref installerRunning, 0);
                ShowInstallerMessage(owner,
                    "VIIPER setup was canceled at the Windows administrator prompt. No changes were made.",
                    "VIIPER setup canceled", MessageBoxImage.Information);
                return false;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref installerRunning, 0);
                string message = $"Could not launch VIIPER setup: {ex.Message}";
                ShowInstallerMessage(owner, message, "VIIPER setup",
                    MessageBoxImage.Error);

                return false;
            }
        }

        private static void InstallerProcess_Exited(Process process,
            Window owner)
        {
            int exitCode = -1;
            try { exitCode = process.ExitCode; } catch { }
            try { process.Dispose(); } catch { }
            Interlocked.Exchange(ref installerRunning, 0);

            Application application = Application.Current;
            if (application?.Dispatcher == null ||
                application.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            application.Dispatcher.BeginInvoke(new Action(() =>
            {
                // The install is the one event that can change the answer, so
                // the session cache has to be discarded here rather than
                // reported stale to the restart branch below.
                RefreshDriverReadiness();
                ViiperPrerequisiteStatus refreshed = GetStatus(
                    tryStartServer: true);

                ViiperInstallerPolicy.ViiperInstallerExitReport report =
                    ViiperInstallerPolicy.DescribeInstallerExit(exitCode,
                        refreshed.Ready, InstallLogPath);

                if (report.Succeeded)
                {
                    Interlocked.Exchange(ref promptShownThisSession, 0);
                }

                AppLogger.LogToGui((report.Succeeded ? "SUCCESSFUL: " : string.Empty) +
                    report.Message.Replace("\n", " "), report.IsError, false);

                if (report.RestartApplication && RequestRestart())
                {
                    return;
                }

                if (!report.Succeeded)
                {
                    ShowInstallerMessage(owner, report.Message, "VIIPER setup",
                        report.IsError ? MessageBoxImage.Error :
                            MessageBoxImage.Warning);
                }
            }));
        }

        /// <summary>Where the setup script records every decision it made.</summary>
        public static string InstallLogPath => Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "VIIPER", "install.log");

        /// <summary>
        /// Queues the restart and begins shutting down. The replacement is
        /// started by the shutdown path itself, once the single-instance handle
        /// is released — see <see cref="PendingApplicationRestart"/> for why
        /// starting it here instead is issue #12.
        /// </summary>
        /// <returns>False when there was nothing to restart.</returns>
        private static bool RequestRestart()
        {
            // Global.exelocation, not a composed "<product>.exe" under
            // exedirpath: it is the executable actually running, so it survives
            // a rename, a portable copy, and the junction/Scoop case that
            // exelocation already resolves.
            if (!PendingApplicationRestart.Current.Request(Global.exelocation))
            {
                AppLogger.LogToGui("VIIPER setup succeeded, but " +
                    ProductInfo.ExeBaseName +
                    ".exe was not found for automatic restart.", true, true);
                return false;
            }

            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    Application.Current.Shutdown();
                }));
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui(
                    $"{ProductInfo.ProductName} failed to restart automatically after VIIPER install: {ex.Message}",
                    true, true);
                return false;
            }
        }

        private static void ShowInstallerMessage(Window owner, string message,
            string caption, MessageBoxImage image)
        {
            if (owner != null && owner.IsLoaded)
            {
                MessageBox.Show(owner, message, caption, MessageBoxButton.OK,
                    image);
            }
            else
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, image);
            }
        }

        private static string GetViiperExePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "VIIPER", "viiper.exe");
        }

        private static string GetSetupScriptPath()
        {
            return Path.Combine(Global.exedirpath, "extras", InstallerScriptName);
        }

        private static bool TryStartServerOnce(string viiperPath)
        {
            lock (serverStartLock)
            {
                if (CanPingServer())
                {
                    return true;
                }

                DateTime now = DateTime.UtcNow;
                if ((now - lastServerStartAttemptUtc).TotalSeconds < 3)
                {
                    return false;
                }

                lastServerStartAttemptUtc = now;
                return TryStartServer(viiperPath);
            }
        }

        private static bool TryStartServer(string viiperPath)
        {
            try
            {
                // The argument vector, including the mandatory
                // --update-notify none, comes from ViiperBackendSpawn so that a
                // test can assert it. See that class for why the flag is not
                // optional.
                Process process = Process.Start(
                    ViiperBackendSpawn.BuildServerStartInfo(viiperPath));
                if (process == null)
                {
                    return false;
                }

                RecordOwnership(process);
                System.Threading.Thread.Sleep(750);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Remembers a backend process as ours. Ownership is (id, start time):
        /// a process id on its own is reused by Windows and would eventually
        /// name somebody else's process.
        /// </summary>
        private static void RecordOwnership(Process process)
        {
            try
            {
                Volatile.Write(ref ownedBackend,
                    new ViiperOwnedBackend(process.Id, process.StartTime));
            }
            catch
            {
                // A process we cannot describe is a process we will not later
                // claim to own.
                Volatile.Write(ref ownedBackend, null);
            }
        }

        /// <summary>
        /// Stops the backend on application exit, if the setting allows it, if
        /// we started it, and if nothing else is using it.
        ///
        /// <para>Call this only once every virtual device has been unplugged
        /// and every usbip port detached. The policy re-checks that with the
        /// backend itself and refuses if anything is still registered, but the
        /// ordering is the caller's to get right: this must never run while one
        /// of our virtual devices is still attached.</para>
        /// </summary>
        /// <param name="log">Receives one line describing what was decided and why.</param>
        /// <param name="censusSource">Test seam; defaults to the live API.</param>
        /// <param name="pnpProbe">Test seam; defaults to the real PnP tree walk.</param>
        public static ViiperBackendStopMethod StopOwnedBackendOnExit(
            Action<string> log = null, IViiperBackendCensusSource censusSource = null,
            IViiperPnpAbsenceProbe pnpProbe = null)
        {
            ViiperOwnedBackend owned = OwnedBackend;
            Process process = owned?.TryResolve();
            try
            {
                bool alive = process != null;
                ViiperBackendCensus census = null;
                if (alive && Global.StopViiperBackendOnExit)
                {
                    census = (censusSource ?? new ViiperApiBackendCensusSource())
                        .TakeCensus();
                }

                // Handed to the policy as a deferred call so the SetupAPI walk
                // only runs when the census has already proven the backend
                // idle - it is the cross-check on the final verdict, not a
                // routine exit cost.
                Func<ViiperPnpAbsenceProof> pnpCrossCheck = () =>
                    (pnpProbe ?? new CmTreePnpAbsenceProbe()).Probe();

                ViiperBackendStopDecision decision = ViiperBackendStopPolicy.Decide(
                    Global.StopViiperBackendOnExit, owned, alive, census,
                    ViiperOwnedDeviceRegistry.Snapshot(), pnpCrossCheck);

                if (!decision.ShouldStop)
                {
                    log?.Invoke("VIIPER backend left running: " + decision.Reason + ".");
                    return ViiperBackendStopMethod.None;
                }

                ViiperBackendStopResult result = ViiperBackendStopper.Stop(
                    process, BackendStopGracePeriod);
                // ASCII only: NLog writes UTF-8 without a BOM and this file is
                // otherwise plain ASCII, so a reader defaulting to the system
                // codepage would render a dash here as mojibake.
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "VIIPER backend stop ({0}): {1} - {2}.",
                    owned, decision.Reason, result.Detail));

                if (result.Method == ViiperBackendStopMethod.Graceful ||
                    result.Method == ViiperBackendStopMethod.Killed)
                {
                    Volatile.Write(ref ownedBackend, null);
                }

                return result.Method;
            }
            catch (Exception ex)
            {
                log?.Invoke("VIIPER backend left running: stopping it threw " +
                    ex.GetType().Name + ": " + ex.Message + ".");
                return ViiperBackendStopMethod.Failed;
            }
            finally
            {
                try { process?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Classifies the backend on the API port for the Settings card and
        /// the startup log: is it ours, somebody's, or a leftover — and what
        /// is it holding. Read-only.
        /// </summary>
        /// <param name="serverResponding">
        /// Pass the ping result if one was just taken (the Settings refresh
        /// has it in hand); null probes again.
        /// </param>
        /// <param name="censusSource">Test seam; defaults to the live API.</param>
        public static ViiperUnownedBackendReport AssessUnownedBackend(
            bool? serverResponding = null,
            IViiperBackendCensusSource censusSource = null)
        {
            bool responding;
            try
            {
                responding = serverResponding ?? CanPingServer();
            }
            catch
            {
                responding = false;
            }

            ViiperOwnedBackend owned = OwnedBackend;
            bool alive = false;
            if (owned != null)
            {
                Process resolved = owned.TryResolve();
                alive = resolved != null;
                try { resolved?.Dispose(); } catch { }
            }

            ViiperBackendCensus census = null;
            if (responding && !(owned != null && alive))
            {
                census = (censusSource ?? new ViiperApiBackendCensusSource())
                    .TakeCensus();
            }

            return ViiperUnownedBackendPolicy.Assess(responding, owned, alive,
                census, ViiperOwnedDeviceRegistry.Snapshot());
        }

        /// <summary>
        /// The user-initiated stop of a backend this session does not own —
        /// the (d) affordance, and deliberately not a lifecycle change: it
        /// runs only from an explicit click, after the card has shown what
        /// the backend is holding.
        ///
        /// <para>The gate re-runs at commit time. Whatever the card said when
        /// the button was clicked, the state that counts is the one read
        /// here, so a backend that has started serving this session's own
        /// pads — or whose census stopped answering — refuses rather than
        /// proceeds. Stopping the process is the clean unplug path for
        /// anything still attached to it: the USB/IP peer disappears and the
        /// driver surprise-removes the devices, the same order VIIPER's own
        /// exit produces.</para>
        /// </summary>
        /// <param name="log">Receives one line describing what happened.</param>
        /// <param name="censusSource">Test seam; defaults to the live API.</param>
        /// <param name="listenerPidSource">Test seam; defaults to the socket table.</param>
        /// <param name="serverResponding">Test seam; null re-pings at commit time.</param>
        public static ViiperUnownedBackendStopOutcome StopUnownedBackend(
            Action<string> log = null,
            IViiperBackendCensusSource censusSource = null,
            Func<int?> listenerPidSource = null,
            bool? serverResponding = null)
        {
            ViiperUnownedBackendReport report =
                AssessUnownedBackend(serverResponding, censusSource);
            if (!report.OffersStop)
            {
                ViiperUnownedBackendStopOutcome refused =
                    ViiperUnownedBackendStopOutcome.Refused(
                        DescribeStopRefusal(report));
                log?.Invoke("VIIPER unowned backend not stopped: " +
                    refused.Reason + ".");
                return refused;
            }

            int? processId;
            try
            {
                processId = (listenerPidSource ??
                    ViiperBackendProcessLocator.FindApiListenerProcessId)();
            }
            catch
            {
                processId = null;
            }

            if (processId == null)
            {
                ViiperUnownedBackendStopOutcome refused =
                    ViiperUnownedBackendStopOutcome.Refused(
                        "could not identify the process listening on port " +
                        ApiPort.ToString(CultureInfo.InvariantCulture));
                log?.Invoke("VIIPER unowned backend not stopped: " +
                    refused.Reason + ".");
                return refused;
            }

            Process process = null;
            try
            {
                string identity;
                try
                {
                    process = Process.GetProcessById(processId.Value);
                    identity = string.Format(CultureInfo.InvariantCulture,
                        "{0} (pid {1})", process.ProcessName, process.Id);
                }
                catch (Exception ex)
                {
                    ViiperUnownedBackendStopOutcome refused =
                        ViiperUnownedBackendStopOutcome.Refused(
                            "the listening process (pid " + processId.Value +
                            ") could not be opened: " + ex.Message);
                    log?.Invoke("VIIPER unowned backend not stopped: " +
                        refused.Reason + ".");
                    return refused;
                }

                ViiperBackendStopResult result = ViiperBackendStopper.Stop(
                    process, BackendStopGracePeriod);
                ViiperUnownedBackendStopOutcome outcome =
                    ViiperUnownedBackendStopOutcome.From(result, identity);
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "VIIPER unowned backend stop ({0}; was holding {1}): {2}.",
                    identity, report.DescribeHoldings(), result.Detail));
                return outcome;
            }
            finally
            {
                try { process?.Dispose(); } catch { }
            }
        }

        private static string DescribeStopRefusal(
            ViiperUnownedBackendReport report)
        {
            switch (report.State)
            {
                case ViiperUnownedBackendState.NoBackend:
                    return "no backend is running";
                case ViiperUnownedBackendState.ManagedByThisApp:
                    return "the running backend is managed by this session; " +
                        "it stops with the app when the exit setting allows";
                case ViiperUnownedBackendState.UnownedServingThisApp:
                    return "the backend is serving this session's own " +
                        "controller(s); disconnect them first";
                case ViiperUnownedBackendState.UnownedInUse
                    when report.ServesThisApp:
                    return "the backend is serving this session's own " +
                        "controller(s) alongside others; disconnect them first";
                case ViiperUnownedBackendState.UnownedUnreadable:
                    return "what the backend is holding could not be read (" +
                        report.Detail + ")";
                default:
                    return "the backend's state changed while the request was in flight";
            }
        }

        private static bool CanPingServer()
        {
            string response = ViiperApiProbe.Request("ping", timeoutMilliseconds: 1000);
            return response?.IndexOf("VIIPER", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUsbipWin2Installed()
        {
            string driverPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "usbip2_ude.sys");
            if (File.Exists(driverPath))
            {
                return true;
            }

            return RegistryUninstallContains("USB/IP") ||
                RegistryUninstallContains("USBip") ||
                RegistryServiceExists("usbip2_ude") ||
                RegistryServiceExists("usbip2_filter");
        }

        private static bool RegistryServiceExists(string serviceName)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool RegistryUninstallContains(string displayName)
        {
            return RegistryHiveUninstallContains(RegistryView.Registry64, displayName) ||
                RegistryHiveUninstallContains(RegistryView.Registry32, displayName);
        }

        private static bool RegistryHiveUninstallContains(RegistryView view, string displayName)
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey uninstallKey = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallKey == null)
                {
                    return false;
                }

                return uninstallKey.GetSubKeyNames()
                    .Select(name => uninstallKey.OpenSubKey(name))
                    .Where(key => key != null)
                    .Any(key =>
                    {
                        using (key)
                        {
                            string value = key.GetValue("DisplayName") as string;
                            return value?.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                    });
            }
            catch
            {
                return false;
            }
        }
    }
}

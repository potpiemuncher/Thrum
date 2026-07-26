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
                UsbipInstalled = IsUsbipWin2Installed(),
                ServerRunning = CanPingServer(),
            };

            if (tryStartServer && !status.ServerRunning && status.ViiperInstalled)
            {
                TryStartServerOnce(viiperPath);
                status.ServerRunning = CanPingServer();
            }

            return status;
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
                ViiperPrerequisiteStatus refreshed = GetStatus(
                    tryStartServer: true);
                if (exitCode == 0 && refreshed.Ready)
                {
                    Interlocked.Exchange(ref promptShownThisSession, 0);
                    AppLogger.LogToGui(
                        "SUCCESSFUL: VIIPER setup finished successfully. Virtual controllers are ready. Restarting DS4Windows.",
                        false, false);
                    RestartDs4Windows();
                    return;
                }

                string logPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "VIIPER", "install.log");
                string message = exitCode == 0
                    ? "VIIPER was installed, but Windows is not reporting every component as ready yet. Restart Windows once, then click Refresh."
                    : $"VIIPER setup could not finish (exit code {exitCode}).\n\n" +
                      "If a viiper.exe process was still running, it may have blocked the VIIPER registration step. " +
                      "Close viiper.exe manually and run Repair again.\n\nReview the setup log for details:\n{logPath}";
                ShowInstallerMessage(owner, message, "VIIPER setup",
                    exitCode == 0 ? MessageBoxImage.Warning :
                        MessageBoxImage.Error);
            }));
        }

        private static void RestartDs4Windows()
        {
            string exePath = Path.Combine(Global.exedirpath, "DS4Windows.exe");
            if (!File.Exists(exePath))
            {
                AppLogger.LogToGui("VIIPER setup succeeded, but DS4Windows.exe was not found for automatic restart.", true, true);
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(2000);

                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    AppLogger.LogToGui(
                        $"Could not restart DS4Windows automatically after VIIPER install: {ex.Message}",
                        true, true);
                    return;
                }

                try
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        Application.Current.Shutdown();
                    }));
                }
                catch (Exception ex)
                {
                    AppLogger.LogToGui(
                        $"DS4Windows failed to restart automatically after VIIPER install: {ex.Message}",
                        true, true);
                }
            });
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
        public static ViiperBackendStopMethod StopOwnedBackendOnExit(
            Action<string> log = null, IViiperBackendCensusSource censusSource = null)
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

                ViiperBackendStopDecision decision = ViiperBackendStopPolicy.Decide(
                    Global.StopViiperBackendOnExit, owned, alive, census,
                    ViiperOwnedDeviceRegistry.Snapshot());

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

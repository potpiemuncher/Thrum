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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// How the managed VIIPER backend is launched.
    ///
    /// <para>The command line lives here, on its own, because one of its
    /// arguments is a security control rather than a preference. hbashton's
    /// VIIPER fork still points its built-in updater at the upstream project it
    /// was forked from (<c>internal/updater/updater.go</c> queries
    /// <c>api.github.com/repos/Alia5/VIIPER</c>), whose version line is
    /// permanently ahead of the fork's own. The check therefore reports an
    /// update every time, forever, and accepting it runs a remote install
    /// script through an <b>elevated</b> PowerShell with no pinned digest and
    /// no Authenticode check — a script that also installs the usbip-win2
    /// kernel driver, entirely outside this application's driver gate.</para>
    ///
    /// <para>So a backend we start is always started with update notifications
    /// off. Version and update policy for the backend belong to this
    /// application, not to a dialog the user did not ask for. See
    /// <c>ViiperBackendLifecycleTests</c>, which asserts the flag is present in
    /// both the arguments and the environment.</para>
    /// </summary>
    public static class ViiperBackendSpawn
    {
        /// <summary>The subcommand that starts the API/USB-IP server.</summary>
        public const string ServerCommand = "server";

        /// <summary>
        /// VIIPER's global update-notification flag
        /// (<c>internal/config/config.go</c>). Declared on the root command, so
        /// it is passed before the subcommand.
        /// </summary>
        public const string UpdateNotifyFlag = "--update-notify";

        /// <summary>
        /// The only value that makes the backend skip the update check
        /// entirely: <c>cmd/viiper/viiper.go</c> guards the whole updater on
        /// <c>cli.UpdateNotify != none</c>.
        /// </summary>
        public const string UpdateNotifyDisabled = "none";

        /// <summary>
        /// The environment form of the same setting. Passed as well as the
        /// flag: the flag is what takes effect, the variable is what any child
        /// or re-exec of the backend would inherit.
        /// </summary>
        public const string UpdateNotifyEnvironmentVariable = "VIIPER_UPDATE_NOTIFY";

        /// <summary>
        /// The exact argument vector used to start the backend, in order.
        /// </summary>
        public static IReadOnlyList<string> ServerArguments { get; } =
            new[] { UpdateNotifyFlag, UpdateNotifyDisabled, ServerCommand };

        /// <summary>
        /// Builds the start info for <c>viiper.exe server</c>.
        ///
        /// <para><see cref="ProcessStartInfo.CreateNoWindow"/> is not only
        /// cosmetic here: with <see cref="ProcessStartInfo.UseShellExecute"/>
        /// false it maps to <c>CREATE_NO_WINDOW</c>, which gives the console
        /// child a console of its own that is never displayed. That console is
        /// what <see cref="ViiperBackendStopper"/> later attaches to in order
        /// to ask the backend to shut down gracefully.</para>
        /// </summary>
        public static ProcessStartInfo BuildServerStartInfo(string viiperPath)
        {
            if (string.IsNullOrEmpty(viiperPath))
            {
                throw new ArgumentException("A backend path is required.",
                    nameof(viiperPath));
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = viiperPath,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
            };

            foreach (string argument in ServerArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment[UpdateNotifyEnvironmentVariable] =
                UpdateNotifyDisabled;
            return startInfo;
        }
    }

    /// <summary>
    /// Identity of a backend process this application started.
    ///
    /// <para>A bare process id is not an identity: Windows reuses process ids,
    /// and the gap between starting the backend and stopping it spans a whole
    /// session. The creation time pins the identity down — the pair
    /// (id, creation time) is unique for as long as anything can observe it, so
    /// a stale record can never resolve to somebody else's process.</para>
    ///
    /// <para>Deliberately in-memory only. Persisting ownership across runs
    /// would mean a crashed session could hand a later session a licence to
    /// kill a backend that a third party has since started.</para>
    /// </summary>
    public sealed class ViiperOwnedBackend
    {
        public ViiperOwnedBackend(int processId, DateTime startTime)
        {
            ProcessId = processId;
            StartTime = startTime;
        }

        public int ProcessId { get; }

        /// <summary>
        /// The value <see cref="Process.StartTime"/> reported for this process.
        /// Compared against a freshly read <see cref="Process.StartTime"/>, so
        /// both sides come through the same conversion and match exactly.
        /// </summary>
        public DateTime StartTime { get; }

        public bool Matches(int processId, DateTime startTime) =>
            ProcessId == processId && StartTime.Ticks == startTime.Ticks;

        /// <summary>
        /// Resolves this record to a live process, or null when the process has
        /// exited or the id now belongs to something else. Any failure to read
        /// the candidate resolves to null: an unverifiable claim of ownership
        /// is not ownership.
        /// </summary>
        public Process TryResolve()
        {
            Process candidate = null;
            try
            {
                candidate = Process.GetProcessById(ProcessId);
                if (!candidate.HasExited && Matches(candidate.Id, candidate.StartTime))
                {
                    return candidate;
                }
            }
            catch
            {
            }

            try { candidate?.Dispose(); } catch { }
            return null;
        }

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture, "pid {0} started {1:O}",
            ProcessId, StartTime);
    }

    /// <summary>A virtual device as the backend reports it.</summary>
    public readonly struct ViiperCensusDevice : IEquatable<ViiperCensusDevice>
    {
        public ViiperCensusDevice(uint busId, string devId, string type)
        {
            BusId = busId;
            DevId = devId ?? string.Empty;
            Type = type ?? string.Empty;
        }

        public uint BusId { get; }

        public string DevId { get; }

        public string Type { get; }

        public bool Equals(ViiperCensusDevice other) =>
            BusId == other.BusId &&
            string.Equals(DevId, other.DevId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ViiperCensusDevice other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(BusId, DevId);

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture, "bus {0} device {1}{2}",
            BusId, DevId,
            string.IsNullOrEmpty(Type) ? string.Empty : " (" + Type + ")");
    }

    /// <summary>
    /// What the backend says is still registered with it.
    /// </summary>
    public sealed class ViiperBackendCensus
    {
        private ViiperBackendCensus(bool succeeded, string failureReason,
            IReadOnlyList<uint> buses, IReadOnlyList<ViiperCensusDevice> devices)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Buses = buses ?? Array.Empty<uint>();
            Devices = devices ?? Array.Empty<ViiperCensusDevice>();
        }

        public bool Succeeded { get; }

        /// <summary>Why the census could not be taken; null on success.</summary>
        public string FailureReason { get; }

        public IReadOnlyList<uint> Buses { get; }

        public IReadOnlyList<ViiperCensusDevice> Devices { get; }

        public static ViiperBackendCensus Success(IReadOnlyList<uint> buses,
            IReadOnlyList<ViiperCensusDevice> devices) =>
            new ViiperBackendCensus(true, null, buses, devices);

        public static ViiperBackendCensus Failed(string reason) =>
            new ViiperBackendCensus(false,
                string.IsNullOrEmpty(reason) ? "unknown error" : reason,
                null, null);
    }

    /// <summary>
    /// Seam over "ask the backend what it is still hosting". Real
    /// implementation talks to the API; tests inject a fake.
    /// </summary>
    public interface IViiperBackendCensusSource
    {
        ViiperBackendCensus TakeCensus();
    }

    /// <summary>
    /// The set of virtual devices this process currently has registered with
    /// the backend.
    ///
    /// <para>Membership is the lifetime of a
    /// <c>ViiperVirtualDeviceLifetime</c>: an entry appears when that object is
    /// constructed and disappears when it is disposed, which is the same object
    /// that owns the usbip detach and the <c>bus/remove</c> call. So "still in
    /// this set" means "this application still believes it holds that
    /// device".</para>
    /// </summary>
    public static class ViiperOwnedDeviceRegistry
    {
        private static readonly object gate = new object();
        private static readonly HashSet<ViiperCensusDevice> owned =
            new HashSet<ViiperCensusDevice>();

        public static void Register(uint busId, string devId)
        {
            lock (gate)
            {
                owned.Add(new ViiperCensusDevice(busId, devId, null));
            }
        }

        public static void Unregister(uint busId, string devId)
        {
            lock (gate)
            {
                owned.Remove(new ViiperCensusDevice(busId, devId, null));
            }
        }

        public static IReadOnlyCollection<ViiperCensusDevice> Snapshot()
        {
            lock (gate)
            {
                return owned.ToArray();
            }
        }

        internal static void ResetForTests()
        {
            lock (gate)
            {
                owned.Clear();
            }
        }
    }

    /// <summary>The answer to "may we stop the backend now?".</summary>
    public sealed class ViiperBackendStopDecision
    {
        private ViiperBackendStopDecision(bool shouldStop, string reason)
        {
            ShouldStop = shouldStop;
            Reason = reason;
        }

        public bool ShouldStop { get; }

        /// <summary>Plain-language justification, written to the log either way.</summary>
        public string Reason { get; }

        public static ViiperBackendStopDecision Stop(string reason) =>
            new ViiperBackendStopDecision(true, reason);

        public static ViiperBackendStopDecision Leave(string reason) =>
            new ViiperBackendStopDecision(false, reason);
    }

    /// <summary>
    /// Decides whether the managed backend may be stopped on exit.
    ///
    /// <para><b>The signal.</b> The backend's own API is the evidence: at the
    /// point this runs, every virtual device this application created has
    /// already been unplugged, detached and removed, so the backend should be
    /// hosting nothing. <c>bus/list</c> plus <c>bus/{id}/list</c> answers
    /// "is it hosting anything?". Anything still registered means either
    /// another consumer is using this backend — a real DS4Windows install, or a
    /// second copy of this application — or that our own teardown did not
    /// finish. Both are reasons to leave the process alone, so the rule is the
    /// same for both: <b>nothing registered, or we do not stop</b>.</para>
    ///
    /// <para><b>What the signal cannot see.</b> It is a device census, not a
    /// client census: the API exposes no list of connected clients. A consumer
    /// that is attached but currently holds no device — one that has just
    /// started, or is between devices — is invisible to it, and a consumer
    /// could create a device in the moment between the census and the stop.
    /// Neither is fixable from the client side, and both are narrow. Every
    /// other uncertainty resolves the other way: a census that fails for any
    /// reason at all leaves the backend running, because a backend left running
    /// costs a few megabytes while a backend killed under a live consumer takes
    /// that consumer's controller away.</para>
    /// </summary>
    public static class ViiperBackendStopPolicy
    {
        public static ViiperBackendStopDecision Decide(
            bool settingEnabled,
            ViiperOwnedBackend ownedBackend,
            bool backendProcessAlive,
            ViiperBackendCensus census,
            IReadOnlyCollection<ViiperCensusDevice> ourLiveDevices)
        {
            if (!settingEnabled)
            {
                return ViiperBackendStopDecision.Leave(
                    "the \"stop the backend on exit\" setting is off");
            }

            if (ownedBackend == null)
            {
                return ViiperBackendStopDecision.Leave(
                    "the backend was already running before " +
                    ProductInfo.ProductName + " started, so it is not ours to stop");
            }

            if (!backendProcessAlive)
            {
                return ViiperBackendStopDecision.Leave(
                    "the backend we started (" + ownedBackend + ") is no longer running");
            }

            if (census == null || !census.Succeeded)
            {
                return ViiperBackendStopDecision.Leave(
                    "could not confirm the backend is idle (" +
                    (census?.FailureReason ?? "no census taken") + ")");
            }

            ourLiveDevices ??= Array.Empty<ViiperCensusDevice>();
            HashSet<ViiperCensusDevice> ours = new HashSet<ViiperCensusDevice>(ourLiveDevices);

            List<ViiperCensusDevice> foreign = census.Devices
                .Where(device => !ours.Contains(device)).ToList();
            List<ViiperCensusDevice> stillOurs = census.Devices
                .Where(device => ours.Contains(device)).ToList();

            if (foreign.Count > 0)
            {
                return ViiperBackendStopDecision.Leave(string.Format(
                    CultureInfo.InvariantCulture,
                    "another consumer is using it - {0} virtual device(s) " +
                    ProductInfo.ProductName + " did not create are still registered ({1})",
                    foreign.Count, Describe(foreign)));
            }

            if (stillOurs.Count > 0)
            {
                // Our own leftovers block a stop just as hard. Killing the
                // backend while one of our virtual devices is still attached is
                // the exact ordering the teardown path exists to avoid.
                return ViiperBackendStopDecision.Leave(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of our own virtual device(s) are still registered, so teardown has not finished ({1})",
                    stillOurs.Count, Describe(stillOurs)));
            }

            if (census.Buses.Count > 0)
            {
                // A bus carries no device on its own, but it is still state
                // somebody asked the backend to hold, and by this point ours
                // are gone. Treated as another consumer's.
                return ViiperBackendStopDecision.Leave(string.Format(
                    CultureInfo.InvariantCulture,
                    "another consumer is using it - {0} empty bus(es) are still registered ({1})",
                    census.Buses.Count,
                    string.Join(", ", census.Buses.Select(bus =>
                        bus.ToString(CultureInfo.InvariantCulture)))));
            }

            return ViiperBackendStopDecision.Stop(
                "we started it and it is hosting no buses or devices");
        }

        private static string Describe(IEnumerable<ViiperCensusDevice> devices) =>
            string.Join("; ", devices.Select(device => device.ToString()));
    }

    /// <summary>Outcome of an attempt to stop the backend process.</summary>
    public enum ViiperBackendStopMethod
    {
        /// <summary>Nothing was attempted.</summary>
        None,

        /// <summary>The process left on its own after a console break.</summary>
        Graceful,

        /// <summary>The console route did not work; the process was killed.</summary>
        Killed,

        /// <summary>Neither route ended the process.</summary>
        Failed,
    }

    public sealed class ViiperBackendStopResult
    {
        public ViiperBackendStopResult(ViiperBackendStopMethod method, string detail)
        {
            Method = method;
            Detail = detail;
        }

        public ViiperBackendStopMethod Method { get; }

        public string Detail { get; }
    }

    /// <summary>
    /// Stops a backend process: politely first, then decisively.
    ///
    /// <para>VIIPER's server installs a <c>signal.NotifyContext</c> for
    /// <c>os.Interrupt</c>/<c>SIGTERM</c> (<c>internal/cmd/server.go</c>), and
    /// Go's Windows runtime raises <c>os.Interrupt</c> for both
    /// <c>CTRL_C_EVENT</c> and <c>CTRL_BREAK_EVENT</c>. The backend is spawned
    /// windowless, but <c>CREATE_NO_WINDOW</c> still gives it a console of its
    /// own, so this process can join that console with
    /// <c>AttachConsole</c> and raise the event there.</para>
    ///
    /// <para>Raising it hits every process on that console, this one included,
    /// so a handler that swallows the event is installed first —
    /// <c>SetConsoleCtrlHandler(NULL, TRUE)</c> would not do, because it
    /// suppresses only <c>CTRL_C_EVENT</c> and the default handler for
    /// <c>CTRL_BREAK_EVENT</c> terminates the process that receives it.</para>
    ///
    /// <para>If any step fails — no console to attach to, the event will not
    /// raise, the backend does not leave within the grace period — the process
    /// is killed. That is an acceptable end for this particular backend: losing
    /// the USB-IP peer is the clean unplug path for a virtual device, cleaner
    /// than <c>usbip detach</c>, which can livelock while an audio pin is held.
    /// It is only acceptable <em>after</em> the devices are gone, which is what
    /// <see cref="ViiperBackendStopPolicy"/> guarantees before we get here.</para>
    /// </summary>
    public static class ViiperBackendStopper
    {
        private const uint CTRL_BREAK_EVENT = 1;

        private delegate bool ConsoleCtrlHandler(uint controlType);

        // Kept in a static field: the delegate must outlive the P/Invoke, or
        // the runtime may collect the thunk the console subsystem calls.
        private static readonly ConsoleCtrlHandler SwallowHandler =
            _ => true;

        private static readonly object stopGate = new object();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent,
            uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(
            ConsoleCtrlHandler handlerRoutine, bool add);

        public static ViiperBackendStopResult Stop(Process process,
            TimeSpan gracePeriod)
        {
            if (process == null)
            {
                return new ViiperBackendStopResult(ViiperBackendStopMethod.None,
                    "no backend process");
            }

            // One stopper at a time: the console attach/detach below is
            // process-wide state.
            lock (stopGate)
            {
                string gracefulDetail = TryGraceful(process, gracePeriod);
                if (gracefulDetail == null)
                {
                    return new ViiperBackendStopResult(
                        ViiperBackendStopMethod.Graceful,
                        "console break accepted; backend exited on its own");
                }

                try
                {
                    if (process.HasExited)
                    {
                        return new ViiperBackendStopResult(
                            ViiperBackendStopMethod.Graceful,
                            "backend had already exited");
                    }

                    process.Kill(entireProcessTree: true);
                    bool exited = process.WaitForExit(
                        (int)Math.Max(1000, gracePeriod.TotalMilliseconds));
                    return exited
                        ? new ViiperBackendStopResult(ViiperBackendStopMethod.Killed,
                            gracefulDetail + "; killed instead")
                        : new ViiperBackendStopResult(ViiperBackendStopMethod.Failed,
                            gracefulDetail + "; kill did not end the process");
                }
                catch (Exception ex)
                {
                    return new ViiperBackendStopResult(ViiperBackendStopMethod.Failed,
                        gracefulDetail + "; kill failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Returns null when the backend left gracefully, otherwise the reason
        /// the graceful route did not finish the job.
        /// </summary>
        private static string TryGraceful(Process process, TimeSpan gracePeriod)
        {
            bool attached = false;
            bool handlerInstalled = false;
            try
            {
                attached = AttachConsole((uint)process.Id);
                if (!attached)
                {
                    return "no console to attach to (error " +
                        Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")";
                }

                handlerInstalled = SetConsoleCtrlHandler(SwallowHandler, true);
                if (!handlerInstalled)
                {
                    // Sending the event without a handler of our own would take
                    // this process down with the backend.
                    return "could not install a console control handler";
                }

                if (!GenerateConsoleCtrlEvent(CTRL_BREAK_EVENT, 0))
                {
                    return "GenerateConsoleCtrlEvent failed (error " +
                        Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")";
                }

                return process.WaitForExit((int)Math.Max(
                    1000, gracePeriod.TotalMilliseconds))
                    ? null
                    : "backend did not exit within " +
                        gracePeriod.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s of the console break";
            }
            catch (Exception ex)
            {
                return "graceful stop threw: " + ex.Message;
            }
            finally
            {
                if (handlerInstalled)
                {
                    try { SetConsoleCtrlHandler(SwallowHandler, false); } catch { }
                }

                if (attached)
                {
                    try { FreeConsole(); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Takes the device census over the backend's own API.
    ///
    /// <para>Wire shape, matching the server: one request per connection, path
    /// (plus optional payload) terminated by a NUL byte, response read until
    /// the server closes the socket. Errors come back RFC 7807 shaped —
    /// <c>{"status":404,"title":"Not Found","detail":"bus 0 not found"}</c>.</para>
    /// </summary>
    public sealed class ViiperApiBackendCensusSource : IViiperBackendCensusSource
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

        private readonly Func<string, string> request;

        public ViiperApiBackendCensusSource()
            : this(path => ViiperApiProbe.Request(path, timeoutMilliseconds: 1500))
        {
        }

        public ViiperApiBackendCensusSource(Func<string, string> request)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public ViiperBackendCensus TakeCensus()
        {
            try
            {
                string busesRaw = request("bus/list");
                if (string.IsNullOrWhiteSpace(busesRaw))
                {
                    return ViiperBackendCensus.Failed("the backend did not answer bus/list");
                }

                ViiperApiProblem problem = TryReadProblem(busesRaw);
                if (problem != null)
                {
                    return ViiperBackendCensus.Failed("bus/list returned " + problem);
                }

                BusListResponse buses = JsonSerializer.Deserialize<BusListResponse>(
                    busesRaw, JsonOptions);
                uint[] busIds = buses?.Buses ?? Array.Empty<uint>();

                List<ViiperCensusDevice> devices = new List<ViiperCensusDevice>();
                foreach (uint busId in busIds)
                {
                    string devicesRaw = request(string.Format(
                        CultureInfo.InvariantCulture, "bus/{0}/list", busId));
                    if (string.IsNullOrWhiteSpace(devicesRaw))
                    {
                        return ViiperBackendCensus.Failed(string.Format(
                            CultureInfo.InvariantCulture,
                            "the backend did not answer bus/{0}/list", busId));
                    }

                    ViiperApiProblem busProblem = TryReadProblem(devicesRaw);
                    if (busProblem != null)
                    {
                        if (busProblem.Status == 404)
                        {
                            // The bus went away between the two calls. Nothing
                            // to count, and nothing suspicious about it.
                            continue;
                        }

                        return ViiperBackendCensus.Failed(string.Format(
                            CultureInfo.InvariantCulture,
                            "bus/{0}/list returned {1}", busId, busProblem));
                    }

                    DevicesListResponse listed =
                        JsonSerializer.Deserialize<DevicesListResponse>(devicesRaw, JsonOptions);
                    if (listed?.Devices == null)
                    {
                        continue;
                    }

                    foreach (CensusDeviceDto device in listed.Devices)
                    {
                        devices.Add(new ViiperCensusDevice(
                            device.BusId, device.DevId, device.Type));
                    }
                }

                return ViiperBackendCensus.Success(busIds, devices);
            }
            catch (Exception ex)
            {
                return ViiperBackendCensus.Failed(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static ViiperApiProblem TryReadProblem(string raw)
        {
            try
            {
                ViiperApiProblem problem =
                    JsonSerializer.Deserialize<ViiperApiProblem>(raw, JsonOptions);
                if (problem != null &&
                    (problem.Status != 0 || !string.IsNullOrEmpty(problem.Title)))
                {
                    return problem;
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private sealed class ViiperApiProblem
        {
            [JsonPropertyName("status")]
            public int Status { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("detail")]
            public string Detail { get; set; }

            public override string ToString() => string.Format(
                CultureInfo.InvariantCulture, "{0} {1}: {2}", Status, Title, Detail);
        }

        private sealed class BusListResponse
        {
            [JsonPropertyName("buses")]
            public uint[] Buses { get; set; }
        }

        private sealed class DevicesListResponse
        {
            [JsonPropertyName("devices")]
            public CensusDeviceDto[] Devices { get; set; }
        }

        private sealed class CensusDeviceDto
        {
            [JsonPropertyName("busId")]
            public uint BusId { get; set; }

            [JsonPropertyName("devId")]
            public string DevId { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
        }
    }
}

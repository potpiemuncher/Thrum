/*
Thrum
Copyright (C) 2026  Thrum contributors

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

using DS4WinWPF.DS4Control;
using DS4WinWPF.DS4Forms.ViewModels;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// The six read-only adapters behind <see cref="ThrumDiagnosticsCollector"/>.
    /// This type projects live process and Windows state directly into the
    /// deliberately narrow snapshot types; no raw HidHide list, audio endpoint
    /// ID, controller address or VIIPER device ID survives a method return.
    /// </summary>
    public sealed class ThrumDiagnosticsLiveSources
    {
        private readonly ControlService controlService;
        private readonly Func<ViiperDriverReadiness> readDriverReadiness;
        private readonly Func<bool, ViiperPrerequisiteStatus> readBackendStatus;
        private readonly Func<bool?, ViiperUnownedBackendReport>
            assessBackend;
        private readonly Func<bool> readHidHideInstalled;
        private readonly Func<bool> readThisAppWhitelisted;
        private readonly Func<IThrumDiagnosticsAudioEndpointReader>
            createAudioReader;

        public ThrumDiagnosticsLiveSources(ControlService controlService)
            : this(controlService,
                () => ViiperSetupManager.DriverReadiness,
                _ => ViiperSetupManager.GetStatus(tryStartServer: false),
                serverResponding => ViiperSetupManager.AssessUnownedBackend(
                    serverResponding),
                Global.IsHidHideInstalled,
                ReadLiveHidHideMembership,
                () => new WindowsDiagnosticsAudioEndpointReader())
        {
        }

        internal ThrumDiagnosticsLiveSources(ControlService controlService,
            Func<ViiperDriverReadiness> readDriverReadiness,
            Func<bool, ViiperPrerequisiteStatus> readBackendStatus,
            Func<bool?, ViiperUnownedBackendReport> assessBackend,
            Func<bool> readHidHideInstalled,
            Func<bool> readThisAppWhitelisted,
            Func<IThrumDiagnosticsAudioEndpointReader> createAudioReader)
        {
            this.controlService = controlService;
            this.readDriverReadiness = readDriverReadiness ??
                throw new ArgumentNullException(nameof(readDriverReadiness));
            this.readBackendStatus = readBackendStatus ??
                throw new ArgumentNullException(nameof(readBackendStatus));
            this.assessBackend = assessBackend ??
                throw new ArgumentNullException(nameof(assessBackend));
            this.readHidHideInstalled = readHidHideInstalled ??
                throw new ArgumentNullException(nameof(readHidHideInstalled));
            this.readThisAppWhitelisted = readThisAppWhitelisted ??
                throw new ArgumentNullException(
                    nameof(readThisAppWhitelisted));
            this.createAudioReader = createAudioReader ??
                throw new ArgumentNullException(nameof(createAudioReader));
        }

        public ThrumDiagnosticsCollector CreateCollector() =>
            new ThrumDiagnosticsCollector(ReadDriver, ReadBackend,
                ReadHidHide, ReadAudio, ReadSlots, ReadLinkHealth);

        /// <summary>
        /// Reads the driver provider's session cache. It deliberately never
        /// calls RefreshDriverReadiness or Recheck: diagnostics reports the
        /// evidence this session is already using instead of replacing it.
        /// </summary>
        public DiagnosticsDriverSection ReadDriver()
        {
            ViiperDriverReadiness readiness = readDriverReadiness() ??
                throw new InvalidOperationException(
                    "The driver readiness source returned no result.");

            // Do not reuse SettingsViewModel.ViiperDriverStatus. Apply would
            // raise PropertyChanged into live WPF bindings from this worker
            // thread, and its temporary "Checking" state is not evidence. A
            // throwaway formatter has no subscribers or dispatcher affinity.
            ViiperDriverStatusViewModel badge =
                new ViiperDriverStatusViewModel(
                    () => readiness, () => readiness);
            badge.Apply(readiness);

            return new DiagnosticsDriverSection
            {
                State = readiness.State.ToString(),
                BadgeText = badge.BadgeText,
                ReleaseLabel = readiness.ReleaseLabel,
                Tier = readiness.Tier?.ToString(),
                IsManifestMatch = readiness.IsManifestMatch,
                IsProductionApproved = readiness.IsProductionApproved,
                EvaluatedAtUtc = readiness.EvaluatedAtUtc,
                // Readiness failures can quote the PATH-resolved usbip.exe
                // location. The snapshot is already safe to copy, so redact
                // before the strings cross this live-reader boundary (the
                // formatter applies its own independent second pass).
                Reasons = readiness.Reasons.Select(reason =>
                    ViiperDriverReportFormatter.RedactUserPathsInText(
                        reason)).ToArray(),
                Identities = readiness.Identities
                    .Select(DescribeDriverIdentity).ToArray(),
            };
        }

        /// <summary>
        /// Probes and classifies the backend without ever launching or claiming
        /// it. The holdings projection reads device count and type only.
        /// </summary>
        public DiagnosticsBackendSection ReadBackend()
        {
            // The nearby operational call sites mostly pass true. That route
            // may start VIIPER and RecordOwnership; collecting diagnostics is a
            // read, so false is explicit and must stay explicit here.
            ViiperPrerequisiteStatus status = readBackendStatus(false) ??
                throw new InvalidOperationException(
                    "The VIIPER status source returned no result.");
            ViiperUnownedBackendReport report = assessBackend(
                status.ServerRunning) ?? throw new InvalidOperationException(
                    "The VIIPER backend assessment returned no result.");

            return new DiagnosticsBackendSection
            {
                HelperInstalled = status.ViiperInstalled,
                ServerRunning = status.ServerRunning,
                OwnershipState = report.State.ToString(),
                Detail = DescribeBackendDetail(report),
                PinnedVersion = ViiperInstallerPins.ViiperBackend.ReleaseLabel,
                Holdings = DescribeBackendHoldings(report),
            };
        }

        public DiagnosticsHidHideSection ReadHidHide()
        {
            bool installed = readHidHideInstalled();
            if (!installed)
            {
                return new DiagnosticsHidHideSection
                {
                    Installed = false,
                    ThisAppWhitelisted = null,
                };
            }

            try
            {
                return new DiagnosticsHidHideSection
                {
                    Installed = true,
                    ThisAppWhitelisted = readThisAppWhitelisted(),
                };
            }
            catch (Exception ex)
            {
                // Preserve the useful "installed" observation while making an
                // unreadable list explicit. The path-bearing list itself never
                // leaves ReadLiveHidHideMembership.
                return new DiagnosticsHidHideSection
                {
                    Installed = true,
                    ThisAppWhitelisted = null,
                    ReadFailure =
                        ViiperDriverReportFormatter.RedactUserPathsInText(
                            ex.GetType().Name + ": " + ex.Message),
                };
            }
        }

        public DiagnosticsAudioSection ReadAudio()
        {
            List<string> defaults = new List<string>();
            bool controllerRenderEndpointPresent = false;

            using (IThrumDiagnosticsAudioEndpointReader reader =
                createAudioReader())
            {
                foreach (DataFlow flow in new[]
                {
                    DataFlow.Render, DataFlow.Capture,
                })
                {
                    foreach (Role role in new[]
                    {
                        Role.Console, Role.Multimedia, Role.Communications,
                    })
                    {
                        string label = flow + "/" + role;
                        IThrumDiagnosticsAudioEndpoint endpoint = null;
                        try
                        {
                            // GetDefaultAudioEndpoint throws when a flow has no
                            // default. Guard every one of the six slots so one
                            // empty role does not discard the other five.
                            if (!reader.HasDefaultAudioEndpoint(flow, role))
                            {
                                defaults.Add(label + ": none");
                                continue;
                            }

                            endpoint = reader.GetDefaultAudioEndpoint(flow,
                                role);
                            string friendlyName = endpoint.FriendlyName;
                            defaults.Add(label + ": " +
                                (string.IsNullOrWhiteSpace(friendlyName)
                                    ? "(name not reported)"
                                    : friendlyName));
                        }
                        catch (Exception ex)
                        {
                            // FriendlyName is a driver property-store round
                            // trip. A device can disappear between the guard
                            // and that read; retain the other default slots.
                            defaults.Add(label + ": (could not read: " +
                                ex.GetType().Name + ")");
                        }
                        finally
                        {
                            endpoint?.Dispose();
                        }
                    }
                }

                foreach (IThrumDiagnosticsAudioEndpoint endpoint in
                    reader.EnumerateActiveRenderEndpoints())
                {
                    try
                    {
                        controllerRenderEndpointPresent |=
                            endpoint.IsControllerAudioEndpoint;
                    }
                    catch
                    {
                        // A disappearing endpoint is not evidence that the
                        // others are absent. Continue the active-render scan.
                    }
                    finally
                    {
                        endpoint?.Dispose();
                    }
                }
            }

            return new DiagnosticsAudioSection
            {
                // Read the consent flag directly. ViiperVirtualDeviceGuard's
                // Decide path also reads the driver gate and belongs to an
                // attach decision, not to this audio observation.
                VirtualAudioEndpointsAllowed =
                    Global.AllowExperimentalAudioEndpoints,
                DefaultEndpoints = defaults,
                ControllerRenderEndpointPresent =
                    controllerRenderEndpointPresent,
            };
        }

        public IReadOnlyList<DiagnosticsSlotRow> ReadSlots()
        {
            EnsureControlService();
            return ProjectSlots(controlService.OutputslotMan.OutputSlots,
                controlService.outputDevices,
                index => index >= 0 &&
                    index < controlService.DS4Controllers.Length
                        ? controlService.DS4Controllers[index]?.DisplayName
                        : null);
        }

        public IReadOnlyList<DiagnosticsLinkHealthRow> ReadLinkHealth()
        {
            EnsureControlService();
            return ProjectLinkHealth(
                controlService.OutputslotMan.OutputSlots);
        }

        public static ThrumDiagnosticsEnvironment ReadEnvironment()
        {
            string version = null;
            bool elevated = false;
            try { version = Global.exeversion; } catch { }
            try { elevated = Global.IsAdministrator(); } catch { }

            return new ThrumDiagnosticsEnvironment
            {
                AppVersion = version,
                OsVersion = Environment.OSVersion.VersionString,
                ProcessArchitecture =
                    RuntimeInformation.ProcessArchitecture.ToString(),
                Elevated = elevated,
            };
        }

        internal static IReadOnlyList<DiagnosticsSlotRow> ProjectSlots(
            IReadOnlyList<OutSlotDevice> slots,
            IReadOnlyList<OutputDevice> inputOutputs,
            Func<int, string> readInputDisplayName)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            if (inputOutputs == null)
            {
                throw new ArgumentNullException(nameof(inputOutputs));
            }

            if (readInputDisplayName == null)
            {
                throw new ArgumentNullException(nameof(readInputDisplayName));
            }

            List<DiagnosticsSlotRow> rows =
                new List<DiagnosticsSlotRow>(slots.Count);
            foreach (OutSlotDevice slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                OutputDevice output = slot.OutputDevice;
                int inputIndex = output == null
                    ? -1
                    : FindReferenceIndex(output, inputOutputs);

                // Do not read OutSlotDevice.InputDisplayString. It embeds the
                // Bluetooth MAC. The physical controller's hard-coded display
                // name is the only input label allowed into the snapshot.
                string inputDisplayName = inputIndex >= 0
                    ? readInputDisplayName(inputIndex)
                    : null;

                rows.Add(new DiagnosticsSlotRow
                {
                    // OutSlotDevice is zero-based; every user-facing slot
                    // elsewhere in the application is one-based.
                    Index = slot.Index + 1,
                    CurrentType = DisplayOutputType(slot.CurrentType,
                        output == null ? "Empty" : null),
                    PermanentType = DisplayOutputType(slot.PermanentType,
                        "None"),
                    InputDisplayName = inputDisplayName,
                    Status = DescribeSlotStatus(slot),
                });
            }

            return rows;
        }

        internal static IReadOnlyList<DiagnosticsLinkHealthRow>
            ProjectLinkHealth(IReadOnlyList<OutSlotDevice> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            List<DiagnosticsLinkHealthRow> rows =
                new List<DiagnosticsLinkHealthRow>();
            foreach (OutSlotDevice slot in slots)
            {
                if (slot?.OutputDevice is not ViiperOutDevice output)
                {
                    continue;
                }

                // Each Interlocked-backed property is individually tear-free.
                // Do not take the dispatch buffer's syncRoot to make this set
                // mutually atomic: that is the real-time speaker path's lock.
                ViiperFeedbackDispatchBuffer buffer =
                    output.FeedbackDispatchBuffer;
                rows.Add(new DiagnosticsLinkHealthRow
                {
                    // These counters belong to the virtual output device, not
                    // to any physical controller that happens to feed it.
                    Device = "slot " + (slot.Index + 1).ToString(
                        CultureInfo.InvariantCulture) + " " +
                        DisplayOutputType(slot.CurrentType, "VIIPER output"),
                    SpeakerEnqueued = buffer.SpeakerEnqueued,
                    SpeakerDropped = buffer.SpeakerDropped,
                    SpeakerExpired = buffer.SpeakerExpired,
                    SpeakerHighWater = buffer.SpeakerHighWater,
                    ControlEnqueued = buffer.ControlEnqueued,
                    ControlCoalesced = buffer.ControlCoalesced,
                    ControlDropped = buffer.ControlDropped,
                });
            }

            return rows;
        }

        internal static int FindReferenceIndex<T>(T target,
            IReadOnlyList<T> candidates) where T : class
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                if (ReferenceEquals(target, candidates[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static IReadOnlyList<string> DescribeBackendHoldings(
            ViiperUnownedBackendReport report)
        {
            List<string> holdings = new List<string>();
            AddDeviceHolding(holdings, "not created by this session",
                report.ForeignDevices);
            AddDeviceHolding(holdings, "created by this session",
                report.OurDevices);
            if (report.EmptyBuses.Count > 0)
            {
                // Bus IDs are unnecessary machine-local correlators here. The
                // count is all the user needs to understand registered state.
                holdings.Add(report.EmptyBuses.Count.ToString(
                    CultureInfo.InvariantCulture) + " empty bus(es)");
            }

            return holdings;
        }

        private static string DescribeDriverIdentity(
            ViiperDriverComponentIdentity identity)
        {
            if (identity == null)
            {
                return "(component not reported)";
            }

            if (!identity.Found)
            {
                return identity.Component + ": not found";
            }

            return identity.Component + (identity.Fields.Count == 0
                ? ": found"
                : ": " + string.Join(", ", identity.Fields.Select(
                    field => field.Display)));
        }

        private static string DescribeBackendDetail(
            ViiperUnownedBackendReport report)
        {
            return report.State switch
            {
                ViiperUnownedBackendState.NoBackend =>
                    "No backend responded; holdings were not enumerated.",
                ViiperUnownedBackendState.ManagedByThisApp =>
                    "This session manages the backend; a census was not taken.",
                ViiperUnownedBackendState.UnownedIdle =>
                    "The backend is not owned by this session and has nothing registered.",
                ViiperUnownedBackendState.UnownedServingThisApp =>
                    "The unowned backend is serving this session's virtual output(s).",
                ViiperUnownedBackendState.UnownedInUse =>
                    "The unowned backend has registered state this session cannot fully account for.",
                ViiperUnownedBackendState.UnownedUnreadable =>
                    "The backend responded, but its holdings could not be read: " +
                    ViiperDriverReportFormatter.RedactUserPathsInText(
                        report.Detail),
                _ => "Backend state was not recognized.",
            };
        }

        private static void AddDeviceHolding(List<string> holdings,
            string owner, IReadOnlyList<ViiperCensusDevice> devices)
        {
            if (devices == null || devices.Count == 0)
            {
                return;
            }

            string types = string.Join(", ", devices
                .GroupBy(device => string.IsNullOrWhiteSpace(device.Type)
                    ? "type not reported"
                    : device.Type)
                .Select(group => group.Count().ToString(
                    CultureInfo.InvariantCulture) + " " + group.Key));
            holdings.Add(devices.Count.ToString(CultureInfo.InvariantCulture) +
                " device(s) " + owner + ": " + types);
        }

        private static bool ReadLiveHidHideMembership()
        {
            using HidHideAPIDevice device =
                new HidHideAPIDevice(writeAccess: false);
            if (!device.IsOpen())
            {
                throw new InvalidOperationException(
                    "The HidHide control device could not be opened for reading.");
            }

            if (!device.TryGetWhitelist(out List<string> whitelist,
                out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            string thisApplication = ResolveHidHideApplicationPath(
                Global.exelocation);
            // The whitelist is discarded here. It contains full paths for
            // every cloaked application and must never reach a snapshot or a
            // diagnostic view-model.
            return whitelist.Any(path => string.Equals(path,
                thisApplication, StringComparison.OrdinalIgnoreCase));
        }

        internal static string ResolveHidHideApplicationPath(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException(
                    "The running executable path was not available.");
            }

            DirectoryInfo directory = new DirectoryInfo(
                Path.GetDirectoryName(exePath));
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                directory.LinkTarget != null)
            {
                // HidHide records the target of a directory junction (the
                // Scoop installation case), not the junction spelling the
                // process was launched through.
                exePath = Path.Combine(directory.LinkTarget,
                    Path.GetFileName(exePath));
            }

            string drive = Path.GetPathRoot(exePath)?.Replace("\\", "");
            if (string.IsNullOrEmpty(drive))
            {
                throw new InvalidOperationException(
                    "The running executable does not have a DOS drive path.");
            }

            StringBuilder target = new StringBuilder(512);
            if (NativeMethods.QueryDosDevice(drive, target,
                target.Capacity) == 0)
            {
                throw new InvalidOperationException(
                    "The executable's DOS device path could not be resolved.");
            }

            string deviceRoot = target.ToString();
            if (deviceRoot.StartsWith(@"\??\", StringComparison.Ordinal))
            {
                deviceRoot = deviceRoot.Remove(0, 4);
            }

            string remainder = exePath.Replace(drive, string.Empty);
            return Path.Combine(deviceRoot, remainder.TrimStart('\\'));
        }

        private static string DisplayOutputType(OutContType type,
            string noneText)
        {
            OutContType normalized = type.Normalize();
            return normalized == OutContType.None
                ? noneText
                : normalized.ToDisplayName();
        }

        private static string DescribeSlotStatus(OutSlotDevice slot)
        {
            bool attached = slot.CurrentAttachedStatus ==
                OutSlotDevice.AttachedStatus.Attached;
            bool bound = slot.CurrentInputBound ==
                OutSlotDevice.InputBound.Bound;
            return (attached ? "attached" : "not attached") + ", " +
                (bound ? "input bound" : "input unbound");
        }

        private void EnsureControlService()
        {
            if (controlService == null)
            {
                throw new InvalidOperationException(
                    "The controller service is not available.");
            }
        }
    }

    /// <summary>
    /// Small Core Audio seam. It keeps the guard and disposal behavior
    /// testable without constructing COM objects in the test process.
    /// </summary>
    internal interface IThrumDiagnosticsAudioEndpointReader : IDisposable
    {
        bool HasDefaultAudioEndpoint(DataFlow flow, Role role);

        IThrumDiagnosticsAudioEndpoint GetDefaultAudioEndpoint(
            DataFlow flow, Role role);

        IEnumerable<IThrumDiagnosticsAudioEndpoint>
            EnumerateActiveRenderEndpoints();
    }

    internal interface IThrumDiagnosticsAudioEndpoint : IDisposable
    {
        string FriendlyName { get; }

        bool IsControllerAudioEndpoint { get; }
    }

    internal sealed class WindowsDiagnosticsAudioEndpointReader :
        IThrumDiagnosticsAudioEndpointReader
    {
        private readonly MMDeviceEnumerator enumerator =
            new MMDeviceEnumerator();

        public bool HasDefaultAudioEndpoint(DataFlow flow, Role role) =>
            enumerator.HasDefaultAudioEndpoint(flow, role);

        public IThrumDiagnosticsAudioEndpoint GetDefaultAudioEndpoint(
            DataFlow flow, Role role) => new WindowsDiagnosticsAudioEndpoint(
                enumerator.GetDefaultAudioEndpoint(flow, role));

        public IEnumerable<IThrumDiagnosticsAudioEndpoint>
            EnumerateActiveRenderEndpoints()
        {
            MMDeviceCollection endpoints = enumerator.EnumerateAudioEndPoints(
                DataFlow.Render, DeviceState.Active);
            foreach (MMDevice endpoint in endpoints)
            {
                yield return new WindowsDiagnosticsAudioEndpoint(endpoint);
            }
        }

        public void Dispose() => enumerator.Dispose();
    }

    internal sealed class WindowsDiagnosticsAudioEndpoint :
        IThrumDiagnosticsAudioEndpoint
    {
        private MMDevice endpoint;

        public WindowsDiagnosticsAudioEndpoint(MMDevice endpoint)
        {
            this.endpoint = endpoint ??
                throw new ArgumentNullException(nameof(endpoint));
        }

        public string FriendlyName => endpoint.FriendlyName;

        public bool IsControllerAudioEndpoint =>
            DualSenseAudioPassthrough.IsControllerAudioEndpoint(endpoint);

        public void Dispose()
        {
            endpoint?.Dispose();
            endpoint = null;
        }
    }
}

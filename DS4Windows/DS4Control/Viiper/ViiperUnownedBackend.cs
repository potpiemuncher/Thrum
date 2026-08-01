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
using System.Linq;
using System.Runtime.InteropServices;

namespace DS4Windows
{
    /// <summary>
    /// What the running VIIPER backend is, from this process's point of view.
    ///
    /// <para>This classification exists for lifecycle invariant (d)'s
    /// follow-up. If this application dies hard while owning a backend it
    /// started, the backend and any attached virtual pad survive; the next
    /// session sees a backend it did not start and — correctly — refuses to
    /// touch it on exit. That refusal is the safe half of the design. The
    /// missing half was telling the user, who is otherwise left with a stale
    /// virtual controller and no in-app explanation. These states drive that
    /// diagnostics card.</para>
    /// </summary>
    public enum ViiperUnownedBackendState
    {
        /// <summary>Nothing answered on the API port.</summary>
        NoBackend,

        /// <summary>
        /// The backend that is running is the one this session started; the
        /// exit path manages it and the card has nothing to warn about.
        /// </summary>
        ManagedByThisApp,

        /// <summary>
        /// A backend this session did not start is running and hosting
        /// nothing: no devices, no buses.
        /// </summary>
        UnownedIdle,

        /// <summary>
        /// A backend this session did not start is running, and everything it
        /// hosts is a live device of this session — the normal shape when the
        /// user runs VIIPER themselves and this application attaches to it.
        /// </summary>
        UnownedServingThisApp,

        /// <summary>
        /// A backend this session did not start is hosting devices (or
        /// registered buses) this session cannot account for. Leftovers of a
        /// session that died hard look exactly like another consumer's live
        /// devices from here; only the user knows which it is, which is why
        /// this state gets a description and a button rather than an
        /// automatic action.
        /// </summary>
        UnownedInUse,

        /// <summary>
        /// A backend this session did not start is running, and the census
        /// that would say what it hosts failed. Nothing is offered on this
        /// state: consent to stop a backend means consent to what it is
        /// holding, and that could not be read.
        /// </summary>
        UnownedUnreadable,
    }

    /// <summary>
    /// The evidence behind a <see cref="ViiperUnownedBackendState"/>, in the
    /// units the card renders: which registered devices are this session's,
    /// which are not, and which buses hold no device at all.
    /// </summary>
    public sealed class ViiperUnownedBackendReport
    {
        public ViiperUnownedBackendReport(ViiperUnownedBackendState state,
            IReadOnlyList<ViiperCensusDevice> foreignDevices,
            IReadOnlyList<ViiperCensusDevice> ourDevices,
            IReadOnlyList<uint> emptyBuses,
            string detail)
        {
            State = state;
            ForeignDevices = foreignDevices ?? Array.Empty<ViiperCensusDevice>();
            OurDevices = ourDevices ?? Array.Empty<ViiperCensusDevice>();
            EmptyBuses = emptyBuses ?? Array.Empty<uint>();
            Detail = detail ?? string.Empty;
        }

        public ViiperUnownedBackendState State { get; }

        /// <summary>Registered devices this session cannot account for.</summary>
        public IReadOnlyList<ViiperCensusDevice> ForeignDevices { get; }

        /// <summary>Registered devices that are this session's live pads.</summary>
        public IReadOnlyList<ViiperCensusDevice> OurDevices { get; }

        /// <summary>Registered buses hosting no device at all.</summary>
        public IReadOnlyList<uint> EmptyBuses { get; }

        /// <summary>
        /// Supporting text: the census failure for
        /// <see cref="ViiperUnownedBackendState.UnownedUnreadable"/>, empty
        /// otherwise.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// True when stopping the backend would take one of this session's
        /// own live controllers down with it.
        /// </summary>
        public bool ServesThisApp => OurDevices.Count > 0;

        /// <summary>
        /// Whether the card may offer its stop button. Policy, not
        /// presentation: a stop is offered only when the user can be shown
        /// exactly what they would be stopping (idle, or in use with the
        /// holdings listed) and none of it is this session's own live
        /// controller. An unreadable census offers nothing — uninformed
        /// consent is not consent.
        /// </summary>
        public bool OffersStop =>
            State == ViiperUnownedBackendState.UnownedIdle ||
            (State == ViiperUnownedBackendState.UnownedInUse && !ServesThisApp);

        /// <summary>One line for the log, matching what the card shows.</summary>
        public string DescribeHoldings()
        {
            List<string> parts = new List<string>();
            if (ForeignDevices.Count > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} device(s) not created by this session: {1}",
                    ForeignDevices.Count,
                    string.Join("; ", ForeignDevices)));
            }

            if (OurDevices.Count > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} of this session's device(s): {1}",
                    OurDevices.Count, string.Join("; ", OurDevices)));
            }

            if (EmptyBuses.Count > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} empty bus(es): {1}", EmptyBuses.Count,
                    string.Join(", ", EmptyBuses.Select(bus =>
                        bus.ToString(CultureInfo.InvariantCulture)))));
            }

            return parts.Count == 0 ? "nothing registered"
                : string.Join("; ", parts);
        }
    }

    /// <summary>
    /// Classifies the running backend. Pure: every input is handed in, so
    /// every state is reachable from a test.
    /// </summary>
    public static class ViiperUnownedBackendPolicy
    {
        /// <param name="serverResponding">Whether the API ping answered.</param>
        /// <param name="ownedBackend">This session's ownership record, if any.</param>
        /// <param name="ownedBackendAlive">
        /// Whether that record still resolves to a live process. A record
        /// whose process is gone confers nothing: whatever is answering the
        /// port now is somebody else.
        /// </param>
        /// <param name="census">
        /// What the backend says it hosts. Only consulted for a responding,
        /// unowned backend; pass null otherwise.
        /// </param>
        /// <param name="ourLiveDevices">
        /// The devices this session currently holds, from
        /// <see cref="ViiperOwnedDeviceRegistry"/>.
        /// </param>
        public static ViiperUnownedBackendReport Assess(
            bool serverResponding,
            ViiperOwnedBackend ownedBackend,
            bool ownedBackendAlive,
            ViiperBackendCensus census,
            IReadOnlyCollection<ViiperCensusDevice> ourLiveDevices)
        {
            if (!serverResponding)
            {
                return new ViiperUnownedBackendReport(
                    ViiperUnownedBackendState.NoBackend, null, null, null, null);
            }

            if (ownedBackend != null && ownedBackendAlive)
            {
                return new ViiperUnownedBackendReport(
                    ViiperUnownedBackendState.ManagedByThisApp,
                    null, null, null, ownedBackend.ToString());
            }

            if (census == null || !census.Succeeded)
            {
                return new ViiperUnownedBackendReport(
                    ViiperUnownedBackendState.UnownedUnreadable,
                    null, null, null,
                    census?.FailureReason ?? "no census taken");
            }

            HashSet<ViiperCensusDevice> ours = ourLiveDevices == null
                ? new HashSet<ViiperCensusDevice>()
                : new HashSet<ViiperCensusDevice>(ourLiveDevices);

            List<ViiperCensusDevice> foreign = census.Devices
                .Where(device => !ours.Contains(device)).ToList();
            List<ViiperCensusDevice> oursPresent = census.Devices
                .Where(device => ours.Contains(device)).ToList();

            // A bus that hosts devices is described by those devices; the
            // extra signal worth naming is a bus with nothing on it, which is
            // registered state all the same.
            HashSet<uint> busesWithDevices = new HashSet<uint>(
                census.Devices.Select(device => device.BusId));
            List<uint> emptyBuses = census.Buses
                .Where(bus => !busesWithDevices.Contains(bus)).ToList();

            if (foreign.Count > 0 || emptyBuses.Count > 0)
            {
                return new ViiperUnownedBackendReport(
                    ViiperUnownedBackendState.UnownedInUse,
                    foreign, oursPresent, emptyBuses, null);
            }

            if (oursPresent.Count > 0)
            {
                return new ViiperUnownedBackendReport(
                    ViiperUnownedBackendState.UnownedServingThisApp,
                    null, oursPresent, null, null);
            }

            return new ViiperUnownedBackendReport(
                ViiperUnownedBackendState.UnownedIdle, null, null, null, null);
        }
    }

    /// <summary>
    /// What came of a user-initiated stop of an unowned backend: either it
    /// was refused before anything was touched, with the reason, or the
    /// stopper ran and this carries its result.
    /// </summary>
    public sealed class ViiperUnownedBackendStopOutcome
    {
        private ViiperUnownedBackendStopOutcome(bool attempted,
            ViiperBackendStopMethod method, string reason,
            string processIdentity)
        {
            Attempted = attempted;
            Method = method;
            Reason = reason ?? string.Empty;
            ProcessIdentity = processIdentity ?? string.Empty;
        }

        /// <summary>False when the gate refused before touching anything.</summary>
        public bool Attempted { get; }

        public ViiperBackendStopMethod Method { get; }

        /// <summary>The refusal reason, or the stopper's detail line.</summary>
        public string Reason { get; }

        /// <summary>"name (pid N)" of the process that was stopped, when one was.</summary>
        public string ProcessIdentity { get; }

        public bool Succeeded => Attempted &&
            (Method == ViiperBackendStopMethod.Graceful ||
             Method == ViiperBackendStopMethod.Killed);

        public static ViiperUnownedBackendStopOutcome Refused(string reason) =>
            new ViiperUnownedBackendStopOutcome(false,
                ViiperBackendStopMethod.None, reason, null);

        public static ViiperUnownedBackendStopOutcome From(
            ViiperBackendStopResult result, string processIdentity) =>
            new ViiperUnownedBackendStopOutcome(true,
                result?.Method ?? ViiperBackendStopMethod.None,
                result?.Detail, processIdentity);
    }

    /// <summary>
    /// One row of the IPv4 listener table, reduced to what the locator needs.
    /// </summary>
    public readonly struct ViiperTcpListenerRow
    {
        public ViiperTcpListenerRow(uint localAddressNetworkOrder,
            int localPort, uint state, int owningProcessId)
        {
            LocalAddressNetworkOrder = localAddressNetworkOrder;
            LocalPort = localPort;
            State = state;
            OwningProcessId = owningProcessId;
        }

        /// <summary>The dwLocalAddr DWORD exactly as the table carries it.</summary>
        public uint LocalAddressNetworkOrder { get; }

        /// <summary>Host-order port.</summary>
        public int LocalPort { get; }

        /// <summary>MIB_TCP_STATE; 2 is LISTEN.</summary>
        public uint State { get; }

        public int OwningProcessId { get; }
    }

    /// <summary>
    /// Finds the process behind the VIIPER API port.
    ///
    /// <para>A backend this session did not start left no process handle
    /// behind, so stopping it needs an identity, and the only honest one is
    /// "the process that owns the listening socket the API answered on". Name
    /// matching would find any viiper.exe, including one serving a different
    /// port; the socket table names the one that is actually this
    /// backend.</para>
    ///
    /// <para>Split OS-side / pure-side like the rest of this area: the table
    /// read is a P/Invoke, the row selection is a function of rows.</para>
    /// </summary>
    public static class ViiperBackendProcessLocator
    {
        private const uint MibTcpStateListen = 2;
        private const uint LoopbackNetworkOrder = 0x0100007F; // 127.0.0.1
        private const uint AnyAddress = 0;                    // 0.0.0.0
        private const int AfInet = 2;
        private const int TcpTableOwnerPidListener = 3;
        private const int ErrorInsufficientBuffer = 122;
        private const int NoError = 0;

        /// <summary>
        /// The process id listening on the API port, or null when it cannot
        /// be established. Null is an answer: the caller reports "could not
        /// identify the process" instead of guessing.
        /// </summary>
        public static int? FindApiListenerProcessId()
        {
            try
            {
                return FindListenerProcessId(ViiperSetupManager.ApiPort,
                    ReadIpv4Listeners());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Selects the listener for <paramref name="port"/>. Loopback binding
        /// is preferred, then the wildcard address, then anything else
        /// claiming the port — the API host is 127.0.0.1, so the closer the
        /// binding is to that, the stronger the identification.
        /// </summary>
        public static int? FindListenerProcessId(int port,
            IEnumerable<ViiperTcpListenerRow> rows)
        {
            if (rows == null)
            {
                return null;
            }

            List<ViiperTcpListenerRow> candidates = rows
                .Where(row => row.State == MibTcpStateListen &&
                    row.LocalPort == port)
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            foreach (uint preferred in new[] { LoopbackNetworkOrder, AnyAddress })
            {
                foreach (ViiperTcpListenerRow row in candidates)
                {
                    if (row.LocalAddressNetworkOrder == preferred)
                    {
                        return row.OwningProcessId;
                    }
                }
            }

            return candidates[0].OwningProcessId;
        }

        private static List<ViiperTcpListenerRow> ReadIpv4Listeners()
        {
            List<ViiperTcpListenerRow> rows = new List<ViiperTcpListenerRow>();

            int size = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref size, false,
                AfInet, TcpTableOwnerPidListener, 0);
            if (result != ErrorInsufficientBuffer || size <= 0)
            {
                return rows;
            }

            IntPtr table = Marshal.AllocHGlobal(size);
            try
            {
                result = GetExtendedTcpTable(table, ref size, false, AfInet,
                    TcpTableOwnerPidListener, 0);
                if (result != NoError)
                {
                    return rows;
                }

                int count = Marshal.ReadInt32(table);
                IntPtr rowPtr = IntPtr.Add(table, 4);
                int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                for (int i = 0; i < count; i++)
                {
                    MibTcpRowOwnerPid row =
                        Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                    rows.Add(new ViiperTcpListenerRow(row.LocalAddr,
                        DecodePort(row.LocalPort), row.State,
                        unchecked((int)row.OwningPid)));
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(table);
            }

            return rows;
        }

        /// <summary>
        /// dwLocalPort carries the port in network byte order in its low two
        /// bytes; the swap is spelled out rather than routed through socket
        /// helpers so the units are visible here.
        /// </summary>
        public static int DecodePort(uint dwLocalPort) =>
            (int)(((dwLocalPort & 0xFF) << 8) | ((dwLocalPort >> 8) & 0xFF));

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcpRowOwnerPid
        {
            public uint State;
            public uint LocalAddr;
            public uint LocalPort;
            public uint RemoteAddr;
            public uint RemotePort;
            public uint OwningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(IntPtr pTcpTable,
            ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder,
            int ulAf, int tableClass, uint reserved);
    }
}

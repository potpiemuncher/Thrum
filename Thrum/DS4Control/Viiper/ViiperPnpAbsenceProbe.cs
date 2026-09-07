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
using System.Runtime.InteropServices;
using System.Text;

namespace DS4Windows
{
    /// <summary>What the PnP cross-check could establish.</summary>
    public enum ViiperPnpAbsenceVerdict
    {
        /// <summary>
        /// Windows shows no device attached through the usbip-win2 controller
        /// — either the controller hosts nothing, or the controller itself is
        /// not present, in which case nothing can be attached through it.
        /// </summary>
        ProvenAbsent,

        /// <summary>
        /// Windows still shows at least one devnode attached through the
        /// usbip-win2 controller.
        /// </summary>
        DevicesPresent,

        /// <summary>
        /// The device tree could not be read far enough to answer. Not
        /// absence: "cannot tell" and "gone" are different verdicts, and only
        /// one of them permits a stop.
        /// </summary>
        Unproven,
    }

    /// <summary>
    /// The answer to "does Windows agree that no usbip-attached device
    /// remains?".
    ///
    /// <para>This exists for lifecycle invariant (c): prove exact-device
    /// absence before releasing the final protection. The backend census
    /// (<see cref="ViiperBackendCensus"/>) is the backend's <i>own</i> view of
    /// what it hosts; a devnode Windows still shows after the backend has
    /// forgotten it — the phantom case the old fork's present-only SetupAPI
    /// probe was written for — is invisible to it. This type carries the
    /// second opinion, taken from the PnP tree itself.</para>
    /// </summary>
    public sealed class ViiperPnpAbsenceProof
    {
        private ViiperPnpAbsenceProof(ViiperPnpAbsenceVerdict verdict,
            string detail, IReadOnlyList<string> devices)
        {
            Verdict = verdict;
            Detail = detail ?? string.Empty;
            Devices = devices ?? Array.Empty<string>();
        }

        public ViiperPnpAbsenceVerdict Verdict { get; }

        /// <summary>
        /// Plain-language support for the verdict: what proved absence, or why
        /// nothing could be proven. Empty for <see cref="ViiperPnpAbsenceVerdict.DevicesPresent"/>,
        /// where <see cref="Devices"/> is the evidence.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// One entry per device Windows still shows attached through the
        /// controller: the device instance ID, plus its problem code when it
        /// has one — a phantom devnode reads "(problem 24)" here, which is
        /// exactly the state that must not be mistaken for absence.
        /// </summary>
        public IReadOnlyList<string> Devices { get; }

        public static ViiperPnpAbsenceProof Absent(string detail) =>
            new ViiperPnpAbsenceProof(ViiperPnpAbsenceVerdict.ProvenAbsent,
                detail, null);

        public static ViiperPnpAbsenceProof Present(
            IReadOnlyList<string> devices) =>
            new ViiperPnpAbsenceProof(ViiperPnpAbsenceVerdict.DevicesPresent,
                null, devices);

        public static ViiperPnpAbsenceProof Unproven(string reason) =>
            new ViiperPnpAbsenceProof(ViiperPnpAbsenceVerdict.Unproven,
                string.IsNullOrEmpty(reason) ? "unknown error" : reason, null);

        public override string ToString()
        {
            switch (Verdict)
            {
                case ViiperPnpAbsenceVerdict.ProvenAbsent:
                    return "absent (" + Detail + ")";
                case ViiperPnpAbsenceVerdict.DevicesPresent:
                    return string.Format(CultureInfo.InvariantCulture,
                        "{0} device(s) present: {1}", Devices.Count,
                        string.Join("; ", Devices));
                default:
                    return "unproven (" + Detail + ")";
            }
        }
    }

    /// <summary>
    /// Seam over "ask Windows what is attached through the usbip-win2
    /// controller". The real implementation walks the PnP tree; tests inject
    /// a fake.
    /// </summary>
    public interface IViiperPnpAbsenceProbe
    {
        ViiperPnpAbsenceProof Probe();
    }

    /// <summary>
    /// Proves usbip-device absence from the Configuration Manager device tree.
    ///
    /// <para><b>Why the tree and not a device-ID filter.</b> A virtual pad
    /// attached over USB/IP carries the same <c>USB\VID_054C&amp;PID_0CE6</c>
    /// identity as a real one on a physical port, and the personas VIIPER can
    /// host make the ID list a moving target. Position is the stable fact:
    /// everything attached through usbip-win2 — and nothing else — lives under
    /// its emulated host controller. So the probe finds every present devnode
    /// whose hardware ID matches the controller
    /// (<see cref="ViiperDriverManifest.UdeHostControllerHardwareId"/>) and
    /// walks its subtree: root hubs are descended into, and every non-hub node
    /// found is reported as an attached device, without descending further —
    /// a composite pad's interface and HID children are that same device, not
    /// additional ones.</para>
    ///
    /// <para><b>What counts as present.</b> Membership in the tree, not
    /// health. A devnode with a problem code — including the problem-24
    /// "device not there" phantom that outlived teardown in the old fork — is
    /// still a devnode Windows can see, so it is reported (with its problem
    /// code) rather than skipped. A devnode whose status cannot be read is
    /// likewise reported: unreadable is not absent.</para>
    ///
    /// <para><b>Failure shape.</b> Never throws. Any error — enumeration,
    /// tree walk, ID read — becomes <see cref="ViiperPnpAbsenceVerdict.Unproven"/>,
    /// and the caller's policy treats that exactly like "present": the stop
    /// does not happen. The only cheap verdict here is the fail-closed
    /// one.</para>
    /// </summary>
    public sealed class CmTreePnpAbsenceProbe : IViiperPnpAbsenceProbe
    {
        private const int ErrorNoMoreItems = 259;
        private const uint CrSuccess = 0;
        private const uint CrNoSuchDevnode = 0x0000000D;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        // Root hubs enumerate as USB\ROOT_HUB / ROOT_HUB20 / ROOT_HUB30; the
        // prefix match covers all three without naming a controller
        // generation.
        private const string RootHubInstanceIdPrefix = @"USB\ROOT_HUB";

        // MAX_DEVICE_ID_LEN, plus the terminator.
        private const int DeviceIdBufferLength = 201;

        public ViiperPnpAbsenceProof Probe()
        {
            try
            {
                return ProbeCore();
            }
            catch (Exception ex)
            {
                return ViiperPnpAbsenceProof.Unproven(
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static ViiperPnpAbsenceProof ProbeCore()
        {
            List<uint> controllers = FindControllers(out string failure);
            if (failure != null)
            {
                return ViiperPnpAbsenceProof.Unproven(failure);
            }

            if (controllers.Count == 0)
            {
                return ViiperPnpAbsenceProof.Absent(
                    "the usbip-win2 host controller (" +
                    ViiperDriverManifest.UdeHostControllerHardwareId +
                    ") is not present, so nothing can be attached through it");
            }

            List<string> devices = new List<string>();
            string walkFailure = CollectAttachedDevices(controllers, devices);
            if (walkFailure != null)
            {
                return ViiperPnpAbsenceProof.Unproven(walkFailure);
            }

            return devices.Count > 0
                ? ViiperPnpAbsenceProof.Present(devices)
                : ViiperPnpAbsenceProof.Absent(
                    "the usbip-win2 host controller is present and hosts no attached device");
        }

        /// <summary>
        /// Every present devnode whose hardware IDs include the usbip-win2 UDE
        /// controller ID. All of them, not the first: a device under a second
        /// controller instance would otherwise be invisible to a probe whose
        /// whole point is proving absence.
        /// </summary>
        private static List<uint> FindControllers(out string failure)
        {
            failure = null;
            List<uint> found = new List<uint>();

            IntPtr deviceInfoSet = SetupDiGetClassDevsWithLastError(IntPtr.Zero,
                null, 0,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_ALLCLASSES);
            if (deviceInfoSet == InvalidHandleValue)
            {
                failure = "SetupDiGetClassDevs could not enumerate present devices (error " +
                    Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")";
                return found;
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    var deviceInfo = new NativeMethods.SP_DEVINFO_DATA
                    {
                        cbSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>(),
                    };
                    if (!SetupDiEnumDeviceInfoWithLastError(deviceInfoSet, index,
                        ref deviceInfo))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ErrorNoMoreItems)
                        {
                            break;
                        }

                        failure = "SetupDiEnumDeviceInfo failed while locating the usbip-win2 controller (error " +
                            error.ToString(CultureInfo.InvariantCulture) + ")";
                        return found;
                    }

                    if (HasControllerHardwareId(deviceInfoSet, ref deviceInfo))
                    {
                        found.Add((uint)deviceInfo.DevInst);
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return found;
        }

        private static bool HasControllerHardwareId(IntPtr deviceInfoSet,
            ref NativeMethods.SP_DEVINFO_DATA deviceInfo)
        {
            ulong propertyType = 0;
            int requiredSize = 0;
            if (NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet,
                ref deviceInfo, ref NativeMethods.DEVPKEY_Device_HardwareIds,
                ref propertyType, null, 0, ref requiredSize, 0))
            {
                return false;
            }

            if (requiredSize <= 0)
            {
                return false;
            }

            byte[] buffer = new byte[requiredSize];
            if (!NativeMethods.SetupDiGetDeviceProperty(deviceInfoSet,
                ref deviceInfo, ref NativeMethods.DEVPKEY_Device_HardwareIds,
                ref propertyType, buffer, buffer.Length, ref requiredSize, 0))
            {
                return false;
            }

            string raw = Encoding.Unicode.GetString(buffer);
            foreach (string id in raw.Split('\0',
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(id.Trim(),
                    ViiperDriverManifest.UdeHostControllerHardwareId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Walks the subtree of each controller. Returns null on success —
        /// with <paramref name="devices"/> holding one entry per attached
        /// device — or the reason the walk could not be completed.
        /// </summary>
        private static string CollectAttachedDevices(List<uint> controllers,
            List<string> devices)
        {
            // Nodes whose children still need visiting: the controllers
            // themselves and any root hub found under them. Non-hub nodes are
            // recorded and not descended into.
            Stack<uint> pending = new Stack<uint>();
            foreach (uint controller in controllers)
            {
                pending.Push(controller);
            }

            while (pending.Count > 0)
            {
                uint parent = pending.Pop();
                uint result = CM_Get_Child(out uint node, parent, 0);
                if (result == CrNoSuchDevnode)
                {
                    continue;
                }

                if (result != CrSuccess)
                {
                    return "CM_Get_Child returned CONFIGRET " +
                        result.ToString(CultureInfo.InvariantCulture);
                }

                while (true)
                {
                    string instanceId = GetDeviceInstanceId(node);
                    if (instanceId == null)
                    {
                        return "CM_Get_Device_ID failed for a devnode under the usbip-win2 controller";
                    }

                    if (instanceId.StartsWith(RootHubInstanceIdPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Push(node);
                    }
                    else
                    {
                        devices.Add(DescribeDevice(node, instanceId));
                    }

                    uint sibling = CM_Get_Sibling(out uint next, node, 0);
                    if (sibling == CrNoSuchDevnode)
                    {
                        break;
                    }

                    if (sibling != CrSuccess)
                    {
                        return "CM_Get_Sibling returned CONFIGRET " +
                            sibling.ToString(CultureInfo.InvariantCulture);
                    }

                    node = next;
                }
            }

            return null;
        }

        private static string GetDeviceInstanceId(uint devInst)
        {
            var buffer = new StringBuilder(DeviceIdBufferLength);
            uint result = CM_Get_Device_ID(devInst, buffer, buffer.Capacity, 0);
            if (result != CrSuccess)
            {
                return null;
            }

            string id = buffer.ToString().Trim();
            return string.IsNullOrEmpty(id) ? null : id;
        }

        private static string DescribeDevice(uint devInst, string instanceId)
        {
            uint result = CM_Get_DevNode_Status(out _, out uint problem,
                devInst, 0);
            if (result != CrSuccess)
            {
                // A node whose status cannot be read still exists; say so
                // rather than pretending it is healthy or absent.
                return instanceId + " (status unreadable)";
            }

            return problem == 0
                ? instanceId
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} (problem {1})", instanceId, problem);
        }

        // Local declarations with SetLastError, for the same reason
        // SetupApiDriverPackageInspector carries its own: the legacy
        // declarations lose the Win32 error, which makes normal
        // ERROR_NO_MORE_ITEMS termination indistinguishable from a failure.
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsWithLastError(
            IntPtr classGuid, string enumerator, int hwndParent, int flags);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiEnumDeviceInfo",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfoWithLastError(
            IntPtr deviceInfoSet, int memberIndex,
            ref NativeMethods.SP_DEVINFO_DATA deviceInfoData);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Child(out uint childDevInst,
            uint devInst, uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Sibling(out uint siblingDevInst,
            uint devInst, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode,
            EntryPoint = "CM_Get_Device_IDW")]
        private static extern uint CM_Get_Device_ID(uint devInst,
            StringBuilder buffer, int bufferLength, uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_DevNode_Status(out uint status,
            out uint problemNumber, uint devInst, uint flags);
    }
}

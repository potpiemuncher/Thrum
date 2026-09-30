using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// Keeps the USB-hosted Bluetooth radio out of selective suspend while a
    /// physical DualShock 4 audio lane is being armed. The DS4 speaker has a
    /// very small decoder FIFO; a radio resume pause is audible even when the
    /// application submitted every SBC frame on time.
    /// </summary>
    internal static class DualShock4BluetoothPowerPolicy
    {
        private static readonly Guid UsbSettingsSubgroup =
            new Guid("2a737441-1930-4402-8d77-b2bebba308a3");
        private static readonly Guid UsbSelectiveSuspendSetting =
            new Guid("48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
        private const uint Disabled = 0;
        private const uint ErrorSuccess = 0;
        private static int changedLogWritten;
        private static int failureLogWritten;

        // The values this app replaced, per power plan, so they can be put
        // back. The setting used to stay disabled for good, on battery too,
        // for every USB device.
        private static readonly object originalsLock = new object();
        private static readonly Dictionary<Guid, (uint Ac, uint Dc)> originals =
            new Dictionary<Guid, (uint Ac, uint Dc)>();

        internal static bool EnsureDisabledForActivePowerScheme()
        {
            IntPtr schemePointer = IntPtr.Zero;
            try
            {
                uint result = PowerGetActiveScheme(IntPtr.Zero,
                    out schemePointer);
                if (result != ErrorSuccess || schemePointer == IntPtr.Zero)
                {
                    LogFailure($"PowerGetActiveScheme returned {result}");
                    return false;
                }

                Guid scheme = Marshal.PtrToStructure<Guid>(schemePointer);
                Guid subgroup = UsbSettingsSubgroup;
                Guid setting = UsbSelectiveSuspendSetting;
                result = PowerReadACValueIndex(IntPtr.Zero, ref scheme,
                    ref subgroup, ref setting, out uint acValue);
                if (result != ErrorSuccess)
                {
                    LogFailure($"PowerReadACValueIndex returned {result}");
                    return false;
                }

                result = PowerReadDCValueIndex(IntPtr.Zero, ref scheme,
                    ref subgroup, ref setting, out uint dcValue);
                if (result != ErrorSuccess)
                {
                    LogFailure($"PowerReadDCValueIndex returned {result}");
                    return false;
                }

                if (!ShouldApply(acValue, dcValue))
                {
                    return true;
                }

                result = PowerWriteACValueIndex(IntPtr.Zero, ref scheme,
                    ref subgroup, ref setting, Disabled);
                if (result != ErrorSuccess)
                {
                    LogFailure($"PowerWriteACValueIndex returned {result}");
                    return false;
                }

                result = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme,
                    ref subgroup, ref setting, Disabled);
                if (result != ErrorSuccess)
                {
                    LogFailure($"PowerWriteDCValueIndex returned {result}");
                    return false;
                }

                lock (originalsLock)
                {
                    if (!originals.ContainsKey(scheme))
                    {
                        originals[scheme] = (acValue, dcValue);
                    }
                }

                result = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
                if (result != ErrorSuccess)
                {
                    LogFailure($"PowerSetActiveScheme returned {result}");
                    return false;
                }

                if (Interlocked.Exchange(ref changedLogWritten, 1) == 0)
                {
                    AppLogger.LogToGui(
                        "Turned off USB selective suspend in the active power plan while DualShock 4 Bluetooth audio plays. It is turned back on when the audio stops.",
                        false);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(ex.Message);
                return false;
            }
            finally
            {
                if (schemePointer != IntPtr.Zero)
                {
                    LocalFree(schemePointer);
                }
            }
        }

        /// <summary>
        /// Puts back the selective-suspend values this app replaced. Called
        /// when no DualShock 4 audio lane is running any more and when the
        /// service stops. A crash leaves the setting off, as before.
        /// </summary>
        internal static void RestoreIfChanged()
        {
            KeyValuePair<Guid, (uint Ac, uint Dc)>[] toRestore;
            lock (originalsLock)
            {
                if (originals.Count == 0)
                {
                    return;
                }

                toRestore = new KeyValuePair<Guid, (uint Ac, uint Dc)>[originals.Count];
                ((ICollection<KeyValuePair<Guid, (uint Ac, uint Dc)>>)originals).CopyTo(toRestore, 0);
                originals.Clear();
            }

            IntPtr activePointer = IntPtr.Zero;
            try
            {
                Guid active = Guid.Empty;
                if (PowerGetActiveScheme(IntPtr.Zero, out activePointer) == ErrorSuccess &&
                    activePointer != IntPtr.Zero)
                {
                    active = Marshal.PtrToStructure<Guid>(activePointer);
                }

                foreach (KeyValuePair<Guid, (uint Ac, uint Dc)> entry in toRestore)
                {
                    Guid scheme = entry.Key;
                    Guid subgroup = UsbSettingsSubgroup;
                    Guid setting = UsbSelectiveSuspendSetting;
                    uint acResult = PowerWriteACValueIndex(IntPtr.Zero, ref scheme,
                        ref subgroup, ref setting, entry.Value.Ac);
                    uint dcResult = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme,
                        ref subgroup, ref setting, entry.Value.Dc);
                    if (scheme == active)
                    {
                        PowerSetActiveScheme(IntPtr.Zero, ref scheme);
                    }

                    if (acResult != ErrorSuccess || dcResult != ErrorSuccess)
                    {
                        AppLogger.LogToGui(
                            $"Could not turn USB selective suspend back on in the power plan (error {(acResult != ErrorSuccess ? acResult : dcResult)}). It can be turned on again in Power Options > USB settings.",
                            true);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui(
                    $"Could not turn USB selective suspend back on in the power plan ({ex.Message}). It can be turned on again in Power Options > USB settings.",
                    true);
            }
            finally
            {
                if (activePointer != IntPtr.Zero)
                {
                    LocalFree(activePointer);
                }

                Interlocked.Exchange(ref changedLogWritten, 0);
            }
        }

        internal static bool ShouldApply(uint acValue, uint dcValue)
        {
            return acValue != Disabled || dcValue != Disabled;
        }

        private static void LogFailure(string reason)
        {
            if (Interlocked.Exchange(ref failureLogWritten, 1) == 0)
            {
                AppLogger.LogToGui(
                    $"Could not disable USB selective suspend for DualShock 4 Bluetooth audio ({reason}). Audio will continue, but the Bluetooth radio may introduce brief gaps.",
                    true);
            }
        }

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey,
            out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey,
            ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid,
            out uint valueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey,
            ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid,
            out uint valueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey,
            ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid,
            uint valueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey,
            ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid,
            uint valueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey,
            ref Guid schemeGuid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}

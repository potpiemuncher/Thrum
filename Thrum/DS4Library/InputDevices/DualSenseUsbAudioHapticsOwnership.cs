/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Threading;

namespace DS4Windows.InputDevices
{
    /// <summary>
    /// Arbitrates the DualSense actuators while the physical four-channel USB
    /// audio endpoint owns advanced haptics. Ordinary HID rumble must stand
    /// down until the last lease from the current device generation retires.
    /// </summary>
    internal sealed class DualSenseUsbAudioHapticsOwnership
    {
        private readonly object sync = new object();
        private readonly Action ownershipChanged;
        private int generation;
        private int leaseCount;
        private int active;

        internal DualSenseUsbAudioHapticsOwnership(Action ownershipChanged)
        {
            this.ownershipChanged = ownershipChanged;
        }

        internal bool Active => Volatile.Read(ref active) != 0;

        internal IDisposable Acquire()
        {
            int leaseGeneration;
            bool changed;
            lock (sync)
            {
                leaseGeneration = generation;
                changed = leaseCount++ == 0;
                if (changed)
                {
                    Volatile.Write(ref active, 1);
                }
            }

            if (changed)
            {
                ownershipChanged?.Invoke();
            }

            return new Lease(this, leaseGeneration);
        }

        internal void Reset(bool notify = true)
        {
            bool changed;
            lock (sync)
            {
                unchecked { generation++; }
                changed = leaseCount != 0;
                leaseCount = 0;
                Volatile.Write(ref active, 0);
            }

            if (changed && notify)
            {
                ownershipChanged?.Invoke();
            }
        }

        internal static bool SuppressOrdinaryMotorOwnership(byte[] report,
            int offset, bool active)
        {
            if (!active || report == null || offset < 0 ||
                offset + 39 >= report.Length || report[offset] != 0x02)
            {
                return false;
            }

            // Main-motor enable flags. Trigger flags and unrelated controller
            // state remain owned by the ordinary USB output report.
            report[offset + 1] &= 0xFC;
            report[offset + 3] = 0;
            report[offset + 4] = 0;
            // Improved rumble drives the same voice-coil actuators.
            report[offset + 39] &= 0xFB;
            return true;
        }

        internal bool WriteOrdinaryReport(byte[] report, int offset,
            Func<bool> writeReport)
        {
            lock (sync)
            {
                SuppressOrdinaryMotorOwnership(report, offset,
                    leaseCount != 0);
                return writeReport();
            }
        }

        private void Release(int leaseGeneration)
        {
            bool changed = false;
            lock (sync)
            {
                if (leaseGeneration != generation || leaseCount == 0)
                {
                    return;
                }

                leaseCount--;
                if (leaseCount == 0)
                {
                    Volatile.Write(ref active, 0);
                    changed = true;
                }
            }

            if (changed)
            {
                ownershipChanged?.Invoke();
            }
        }

        private sealed class Lease : IDisposable
        {
            private DualSenseUsbAudioHapticsOwnership owner;
            private readonly int generation;

            internal Lease(DualSenseUsbAudioHapticsOwnership owner,
                int generation)
            {
                this.owner = owner;
                this.generation = generation;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref owner, null)?.Release(generation);
            }
        }
    }
}

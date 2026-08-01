/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Collections.Generic;

namespace DS4Windows
{
    /// <summary>What input discovery should do with a candidate pad.</summary>
    internal enum UsbipInputVerdict
    {
        /// <summary>Not usbip-attached; the normal admission rules apply.</summary>
        Accept,

        /// <summary>
        /// One of this session's own live VIIPER outputs. Rejected quietly —
        /// its creation already logged, and it leaves with its lifetime.
        /// </summary>
        RejectOwnLiveOutput,

        /// <summary>
        /// Attached through usbip-win2 but not this session's. Rejected with
        /// one log line, because unlike our own output nothing else will ever
        /// explain to the user why this pad is being ignored.
        /// </summary>
        RejectUnmanagedImport,
    }

    /// <summary>
    /// Admission policy for pads attached through usbip-win2's emulated host
    /// controller: they are never input.
    ///
    /// <para><b>Why position decides, not ownership.</b> A pad under that
    /// controller is a virtual device being <i>served</i> to something — this
    /// session's own output, a leftover of a session that died hard, or
    /// another application's live controller. Ingesting the first recurses
    /// (our output becomes our input, which maps to another output); the
    /// in-memory path registry has always rejected it. The other two are the
    /// gap this policy closes: the registry dies with its session, so a
    /// leftover was re-ingested on the next start — the recursion the old
    /// startup port sweep existed to prevent, and the reason its removal
    /// (2026-07-31, after it disconnected another application's live pad)
    /// left this as the accepted residual. Recognising the pads intrinsically
    /// retires that residual without touching anything: a devnode's ancestry
    /// does not depend on who remembers creating it.</para>
    ///
    /// <para><b>What this deliberately gives up:</b> a <i>real</i> controller
    /// forwarded from another machine over usbip-win2 can no longer be used
    /// as input. On this project's machines usbip-win2 exists solely as
    /// VIIPER's transport, remote forwarding has VirtualHere as the supported
    /// route (see <c>CheckIfVirtualDevice</c>'s exclusion list), and the log
    /// line names the refusal — if the combination ever matters, it arrives
    /// as a feature request with its evidence attached, not as silence.</para>
    ///
    /// <para>Same shape as <see cref="MoonlightVirtualDevicePolicy"/>: the
    /// verdict is a pure function of the two probes' answers, so the rule is
    /// testable without a device tree.</para>
    /// </summary>
    internal static class UsbipAttachedInputPolicy
    {
        private static readonly object warnedLock = new object();
        private static readonly HashSet<string> warnedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static UsbipInputVerdict Decide(bool isOwnLiveOutput,
            bool isUsbipAttached)
        {
            if (isOwnLiveOutput)
            {
                return UsbipInputVerdict.RejectOwnLiveOutput;
            }

            return isUsbipAttached
                ? UsbipInputVerdict.RejectUnmanagedImport
                : UsbipInputVerdict.Accept;
        }

        /// <summary>
        /// Whether this rejection is the first for the path. Discovery re-runs
        /// on every hotplug, and a pad that is being deliberately ignored
        /// would otherwise be re-announced each time anything else arrives.
        /// </summary>
        internal static bool ShouldWarnOnce(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                return false;
            }

            lock (warnedLock)
            {
                return warnedPaths.Add(devicePath);
            }
        }

        /// <summary>
        /// The one line the user gets for a pad that exists but is ignored.
        /// It has to say what was seen, why it is not input, both readings of
        /// what it might be, and where to act if it is a leftover.
        /// </summary>
        internal static string DescribeRejectedImport(string devicePath) =>
            "Ignoring a controller attached through usbip-win2 (" +
            devicePath + "): pads on that controller are virtual outputs " +
            "being served by some application - possibly " +
            ProductInfo.ProductName + "'s own from a session that ended " +
            "abruptly, possibly another program's live controller - and are " +
            "never used as input. If it is a leftover, Settings > VIIPER " +
            "Virtual Controller Support > Backend process can clear it.";

        internal static void ResetForTests()
        {
            lock (warnedLock)
            {
                warnedPaths.Clear();
            }
        }
    }
}

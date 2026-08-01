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

using System;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The diagnostics report is a button that puts six subsystems' state on a
/// user's clipboard, and it exists so people can paste it into public issue
/// threads. Most of these tests are therefore leak tests.
///
/// <para>The three dangerous sources were identified by auditing each one
/// before any of this was written: HidHide's whitelist is every cloaked
/// application's full path on the machine (the account name, and effectively
/// the user's installed-game inventory); an output slot's input string carries
/// the physical controller's Bluetooth MAC; and audio endpoint IDs are stable
/// per-machine correlators. Each has a test below that fails if it comes
/// back.</para>
/// </summary>
[TestClass]
public class ThrumDiagnosticsReportFormatterTests
{
    private static ThrumDiagnosticsSnapshot MinimalSnapshot() =>
        new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            AppVersion = "0.9.0-beta.1",
            OsVersion = "Microsoft Windows NT 10.0.26200.0",
            ProcessArchitecture = "X64",
            Elevated = false,
        };

    [TestMethod]
    public void FormatRejectsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => ThrumDiagnosticsReportFormatter.Format(null));
    }

    [TestMethod]
    public void AnEmptySnapshotStillProducesAReadableReport()
    {
        // Every section absent is a legitimate state - the app may have been
        // running for two seconds. It must not throw, and it must not render a
        // blank that reads as "everything is fine".
        string report = ThrumDiagnosticsReportFormatter.Format(MinimalSnapshot());

        StringAssert.Contains(report, "Thrum diagnostics report");
        StringAssert.Contains(report, "usbip-win2 driver gate");
        StringAssert.Contains(report, "VIIPER backend");
        StringAssert.Contains(report, "HidHide");
        StringAssert.Contains(report, "audio endpoints");
        StringAssert.Contains(report, "output slots");
        StringAssert.Contains(report, "controller link health");
        StringAssert.Contains(report, "(not reported)");
    }

    [TestMethod]
    public void UserAccountNamesAreRedactedWhereverTheyAppear()
    {
        // Rule C from the existing report precedent: the report layer redacts
        // everything it quotes, regardless of whether the producer already did.
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Backend = new DiagnosticsBackendSection
            {
                Detail = @"failed reading C:\Users\patrick\AppData\Local\VIIPER\viiper.exe",
            },
            HidHide = new DiagnosticsHidHideSection
            {
                Installed = true,
                ReadFailure = @"denied for C:\Users\patrick\Games\thing.exe",
            },
            Driver = new DiagnosticsDriverSection
            {
                State = "Missing",
                Reasons = new[] { @"probe failed at C:\Users\patrick\x" },
            },
            CollectionFailures = new[] { @"slots: C:\Users\patrick\y unreadable" },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        Assert.IsFalse(report.Contains("patrick", StringComparison.OrdinalIgnoreCase),
            "an account name reached the report:\n" + report);
        StringAssert.Contains(report, @"\Users\<user>\");
    }

    [TestMethod]
    public void TheHidHideWhitelistIsNeverEnumerated()
    {
        // The single most dangerous source. The snapshot type has no field that
        // can carry the list, and the report says so out loud - so a future
        // change that adds one has to delete this assertion deliberately.
        ThrumDiagnosticsSnapshot snapshot = MinimalSnapshot();
        snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = snapshot.TimestampUtc,
            HidHide = new DiagnosticsHidHideSection
            {
                Installed = true,
                ThisAppWhitelisted = false,
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "not included by design");
        StringAssert.Contains(report,
            "it would list every");
        // And the failure mode is explained rather than left as a bare "no".
        StringAssert.Contains(report, "hiding");
    }

    [TestMethod]
    public void AnUnwhitelistedAppIsFlaggedForAttentionNotJustReported()
    {
        ThrumDiagnosticsSnapshot bad = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            HidHide = new DiagnosticsHidHideSection
            {
                Installed = true, ThisAppWhitelisted = false,
            },
        };
        ThrumDiagnosticsSnapshot good = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            HidHide = new DiagnosticsHidHideSection
            {
                Installed = true, ThisAppWhitelisted = true,
            },
        };

        StringAssert.Contains(ThrumDiagnosticsReportFormatter.Format(bad),
            "[ATTENTION]");
        Assert.IsFalse(
            ThrumDiagnosticsReportFormatter.Format(good).Contains("[ATTENTION]"),
            "a healthy HidHide state must not shout");
    }

    [TestMethod]
    public void TheBackendSectionSaysTheRunningVersionIsUnavailable()
    {
        // Nothing in the product asks the backend its version. Printing only
        // the pin would read as an observation; the report has to distinguish
        // "expected" from "running" or it is quietly lying.
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Backend = new DiagnosticsBackendSection
            {
                HelperInstalled = true,
                ServerRunning = true,
                PinnedVersion = "v0.0.5",
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "expected version");
        StringAssert.Contains(report, "v0.0.5");
        StringAssert.Contains(report, "running version");
        StringAssert.Contains(report, "not reported by the backend");
    }

    [TestMethod]
    public void AudioSectionStatesTheGuardIsAbsentRatherThanImplyingItWorks()
    {
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Audio = new DiagnosticsAudioSection
            {
                VirtualAudioEndpointsAllowed = false,
                DefaultEndpoints = new[] { "Render/Console: Speakers (Realtek)" },
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "default-endpoint guard");
        StringAssert.Contains(report, "not installed");
        StringAssert.Contains(report, "disabled (default)");
    }

    [TestMethod]
    public void SectionsThatCouldNotBeReadSaySoInsteadOfLookingHealthy()
    {
        // The invariant carried over from the driver gate and the stale-port
        // sweep: "could not look" must never render as "looked and saw
        // nothing".
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            CollectionFailures = new[]
            {
                "output slots: the control service was not running",
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "sections that could not be read");
        StringAssert.Contains(report, "the control service was not running");
        StringAssert.Contains(report, "[ATTENTION]");
    }

    [TestMethod]
    public void LinkHealthFlagsDropsAndSaysCountersResetOnReconnect()
    {
        // A reader seeing "dropped 0" should know that means "since this
        // connection", because Connect() zeroes every counter.
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            LinkHealth = new[]
            {
                new DiagnosticsLinkHealthRow
                {
                    Device = "slot 1 DualSense",
                    SpeakerEnqueued = 1000,
                    SpeakerDropped = 4,
                    ControlEnqueued = 50,
                },
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "reset on reconnect");
        StringAssert.Contains(report, "slot 1 DualSense");
        StringAssert.Contains(report, "[ATTENTION]");
    }

    [TestMethod]
    public void TheFooterPromisesExactlyWhatTheReportOmits()
    {
        // The promise is load-bearing: it is what makes the report safe to
        // paste. If a future change starts emitting one of these, this test
        // does not catch it - but the promise being present and specific is
        // what makes the omission reviewable.
        string report = ThrumDiagnosticsReportFormatter.Format(MinimalSnapshot());

        foreach (string promised in new[]
        {
            "read-only", "user account names", "device", "serials",
            "radio addresses", "driver store paths", "endpoint IDs",
        })
        {
            StringAssert.Contains(report, promised);
        }
    }

    [TestMethod]
    public void SlotRowsCarryNoBluetoothAddress()
    {
        // The live UI string this projects from is "$"{DisplayName} [{MacAddress}]"".
        // Only the display name may survive into the snapshot.
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Slots = new[]
            {
                new DiagnosticsSlotRow
                {
                    Index = 1,
                    CurrentType = "DualSense",
                    PermanentType = "None",
                    Status = "attached",
                    InputDisplayName = "Wireless Controller",
                },
            },
        };

        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);

        StringAssert.Contains(report, "Wireless Controller");
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(
                report, @"([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}"),
            "something shaped like a MAC address reached the report:\n" + report);
    }
}

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
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The collector's job is composition and failure isolation, so that is what
/// these pin. A diagnostics report matters most when something is broken, which
/// means the interesting cases are all the ones where a source misbehaves.
/// </summary>
[TestClass]
public class ThrumDiagnosticsCollectorTests
{
    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2026, 8, 1, 9, 30, 0, TimeSpan.Zero);

    private static ThrumDiagnosticsEnvironment Env() =>
        new ThrumDiagnosticsEnvironment
        {
            AppVersion = "0.9.0-beta.1",
            OsVersion = "Windows",
            ProcessArchitecture = "X64",
            Elevated = false,
        };

    [TestMethod]
    public void AllSourcesPresentAreAllCollected()
    {
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readDriver: () => new DiagnosticsDriverSection { State = "Missing" },
            readBackend: () => new DiagnosticsBackendSection { ServerRunning = true },
            readHidHide: () => new DiagnosticsHidHideSection { Installed = true },
            readAudio: () => new DiagnosticsAudioSection(),
            readSlots: () => new[] { new DiagnosticsSlotRow { Index = 1 } },
            readLinkHealth: () => new[] { new DiagnosticsLinkHealthRow { Device = "d" } },
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.AreEqual(FixedNow, snapshot.TimestampUtc);
        Assert.AreEqual("Missing", snapshot.Driver.State);
        Assert.IsTrue(snapshot.Backend.ServerRunning);
        Assert.IsTrue(snapshot.HidHide.Installed);
        Assert.IsNotNull(snapshot.Audio);
        Assert.AreEqual(1, snapshot.Slots.Count);
        Assert.AreEqual(1, snapshot.LinkHealth.Count);
        Assert.AreEqual(0, snapshot.CollectionFailures.Count);
    }

    [TestMethod]
    public void OneThrowingSourceDoesNotCostTheOthers()
    {
        // The case the whole design exists for. HidHide throwing is realistic:
        // its first touch does a SetupAPI enumeration and its list IOCTLs can
        // fail outright.
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readDriver: () => new DiagnosticsDriverSection { State = "Missing" },
            readHidHide: () => throw new InvalidOperationException("device unreachable"),
            readSlots: () => new[] { new DiagnosticsSlotRow { Index = 2 } },
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.AreEqual("Missing", snapshot.Driver.State, "driver was lost");
        Assert.AreEqual(1, snapshot.Slots.Count, "slots were lost");
        Assert.IsNull(snapshot.HidHide);
        Assert.AreEqual(1, snapshot.CollectionFailures.Count);
        StringAssert.Contains(snapshot.CollectionFailures[0], "HidHide");
        StringAssert.Contains(snapshot.CollectionFailures[0], "device unreachable");
    }

    [TestMethod]
    public void EverySourceThrowingStillProducesAReport()
    {
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readDriver: () => throw new Exception("a"),
            readBackend: () => throw new Exception("b"),
            readHidHide: () => throw new Exception("c"),
            readAudio: () => throw new Exception("d"),
            readSlots: () => throw new Exception("e"),
            readLinkHealth: () => throw new Exception("f"),
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.AreEqual(6, snapshot.CollectionFailures.Count);
        // And it must still render, because this is exactly when someone is
        // trying to report a problem.
        string report = ThrumDiagnosticsReportFormatter.Format(snapshot);
        StringAssert.Contains(report, "sections that could not be read");
    }

    [TestMethod]
    public void FailureTextIsRedacted()
    {
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readBackend: () => throw new InvalidOperationException(
                @"cannot open C:\Users\patrick\AppData\Local\VIIPER\viiper.exe"),
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.AreEqual(1, snapshot.CollectionFailures.Count);
        Assert.IsFalse(
            snapshot.CollectionFailures[0].Contains("patrick",
                StringComparison.OrdinalIgnoreCase),
            "an account name survived into a failure line: " +
            snapshot.CollectionFailures[0]);
        StringAssert.Contains(snapshot.CollectionFailures[0], @"\Users\<user>\");
    }

    [TestMethod]
    public void AnAbsentSourceIsNotAFailure()
    {
        // Omitting a reader is a caller's choice, not an error. It renders as
        // "(not reported)", which is honest; recording it as a failure would
        // cry wolf.
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readDriver: () => new DiagnosticsDriverSection { State = "Missing" },
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.AreEqual(0, snapshot.CollectionFailures.Count);
        Assert.IsNull(snapshot.Backend);
        Assert.IsNull(snapshot.HidHide);
    }

    [TestMethod]
    public void CollectNeverReturnsNullCollections()
    {
        // The formatter iterates these; a null would turn a diagnostics click
        // into a crash report.
        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readSlots: () => null,
            readLinkHealth: () => null,
            clock: () => FixedNow);

        ThrumDiagnosticsSnapshot snapshot = collector.Collect(Env());

        Assert.IsNotNull(snapshot.Slots);
        Assert.IsNotNull(snapshot.LinkHealth);
        Assert.IsNotNull(snapshot.CollectionFailures);
        ThrumDiagnosticsReportFormatter.Format(snapshot);
    }

    [TestMethod]
    public void CollectToleratesANullEnvironment()
    {
        ThrumDiagnosticsSnapshot snapshot =
            new ThrumDiagnosticsCollector(clock: () => FixedNow).Collect(null);

        Assert.IsNotNull(snapshot);
        ThrumDiagnosticsReportFormatter.Format(snapshot);
    }

    [TestMethod]
    public void SourcesAreEachInvokedExactlyOnce()
    {
        // Cheapness was audited per source and some are expensive - the backend
        // read does registry work and a live ping. Collecting twice would double
        // that and could also produce a self-inconsistent report.
        Dictionary<string, int> calls = new Dictionary<string, int>();
        int Count(string k) { calls[k] = calls.TryGetValue(k, out int n) ? n + 1 : 1; return 0; }

        ThrumDiagnosticsCollector collector = new ThrumDiagnosticsCollector(
            readDriver: () => { Count("driver"); return new DiagnosticsDriverSection(); },
            readBackend: () => { Count("backend"); return new DiagnosticsBackendSection(); },
            readHidHide: () => { Count("hidhide"); return new DiagnosticsHidHideSection(); },
            readAudio: () => { Count("audio"); return new DiagnosticsAudioSection(); },
            readSlots: () => { Count("slots"); return Array.Empty<DiagnosticsSlotRow>(); },
            readLinkHealth: () => { Count("link"); return Array.Empty<DiagnosticsLinkHealthRow>(); },
            clock: () => FixedNow);

        collector.Collect(Env());

        Assert.AreEqual(6, calls.Count);
        Assert.IsTrue(calls.Values.All(n => n == 1),
            "a source was read more than once: " +
            string.Join(", ", calls.Select(kv => kv.Key + "=" + kv.Value)));
    }
}

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

namespace DS4Windows
{
    /// <summary>
    /// Builds a <see cref="ThrumDiagnosticsSnapshot"/> from six independent
    /// sources.
    ///
    /// <para><b>Every source is a delegate.</b> Not for ceremony: the six differ
    /// wildly in cost and blast radius — the driver gate is a cached field read,
    /// the backend census does registry work and a live socket ping, HidHide
    /// does a SetupAPI enumeration on first touch. Injecting them keeps the
    /// composition testable without any of that, and it is the seam that lets a
    /// caller substitute a cheaper reader.</para>
    ///
    /// <para><b>One failing source must not cost the other five.</b> A
    /// diagnostics report is most valuable exactly when something is broken, so
    /// a section that throws is caught, recorded in
    /// <see cref="ThrumDiagnosticsSnapshot.CollectionFailures"/>, and the rest
    /// still collected. A section that fails is never rendered as an empty
    /// healthy section — "could not look" must not read as "looked and saw
    /// nothing", the same rule the driver gate and the stale-port sweep follow.</para>
    ///
    /// <para><b>This type never mutates anything.</b> It must not call
    /// <c>Refresh()</c> on the driver readiness (that discards the session cache
    /// and re-runs a SetupAPI + WinVerifyTrust sweep), must not start or stop
    /// the backend, and must not attach or detach a device. Collecting a report
    /// is a read.</para>
    /// </summary>
    public sealed class ThrumDiagnosticsCollector
    {
        private readonly Func<DiagnosticsDriverSection> readDriver;
        private readonly Func<DiagnosticsBackendSection> readBackend;
        private readonly Func<DiagnosticsHidHideSection> readHidHide;
        private readonly Func<DiagnosticsAudioSection> readAudio;
        private readonly Func<IReadOnlyList<DiagnosticsSlotRow>> readSlots;
        private readonly Func<IReadOnlyList<DiagnosticsLinkHealthRow>> readLinkHealth;
        private readonly Func<DateTimeOffset> clock;

        public ThrumDiagnosticsCollector(
            Func<DiagnosticsDriverSection> readDriver = null,
            Func<DiagnosticsBackendSection> readBackend = null,
            Func<DiagnosticsHidHideSection> readHidHide = null,
            Func<DiagnosticsAudioSection> readAudio = null,
            Func<IReadOnlyList<DiagnosticsSlotRow>> readSlots = null,
            Func<IReadOnlyList<DiagnosticsLinkHealthRow>> readLinkHealth = null,
            Func<DateTimeOffset> clock = null)
        {
            this.readDriver = readDriver;
            this.readBackend = readBackend;
            this.readHidHide = readHidHide;
            this.readAudio = readAudio;
            this.readSlots = readSlots;
            this.readLinkHealth = readLinkHealth;
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Collects every section. Never throws: a source that fails becomes a
        /// recorded failure line, because a report that refuses to render is
        /// useless precisely when it is needed.
        /// </summary>
        public ThrumDiagnosticsSnapshot Collect(ThrumDiagnosticsEnvironment environment)
        {
            environment ??= new ThrumDiagnosticsEnvironment();
            List<string> failures = new List<string>();

            return new ThrumDiagnosticsSnapshot
            {
                TimestampUtc = clock(),
                AppVersion = environment.AppVersion,
                OsVersion = environment.OsVersion,
                ProcessArchitecture = environment.ProcessArchitecture,
                Elevated = environment.Elevated,
                Driver = Read("driver gate", readDriver, failures),
                Backend = Read("VIIPER backend", readBackend, failures),
                HidHide = Read("HidHide", readHidHide, failures),
                Audio = Read("audio endpoints", readAudio, failures),
                Slots = Read("output slots", readSlots, failures)
                    ?? Array.Empty<DiagnosticsSlotRow>(),
                LinkHealth = Read("controller link health", readLinkHealth, failures)
                    ?? Array.Empty<DiagnosticsLinkHealthRow>(),
                CollectionFailures = failures,
            };
        }

        private static T Read<T>(string section, Func<T> source,
            List<string> failures) where T : class
        {
            if (source == null)
            {
                // An absent source is not a failure - a caller may deliberately
                // omit one. It renders as "(not reported)" rather than as an
                // error, which is honest either way.
                return null;
            }

            try
            {
                return source();
            }
            catch (Exception ex)
            {
                // Deliberately broad. These six readers reach registry, SetupAPI,
                // COM, sockets and live device objects between them; enumerating
                // their exception types here would be a guess, and guessing wrong
                // costs the whole report.
                failures.Add(section + ": " +
                    ViiperDriverReportFormatter.RedactUserPathsInText(
                        ex.GetType().Name + ": " + ex.Message));
                return null;
            }
        }
    }

    /// <summary>
    /// Non-sensitive environment facts for the report header. Separated from the
    /// readers for the same reason <see cref="ViiperDriverReportContext"/> is:
    /// so the collector can be exercised without an app around it.
    /// </summary>
    public sealed class ThrumDiagnosticsEnvironment
    {
        public string AppVersion { get; init; }

        public string OsVersion { get; init; }

        public string ProcessArchitecture { get; init; }

        public bool Elevated { get; init; }
    }
}

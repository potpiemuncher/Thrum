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
    /// An immutable, already-redacted picture of everything the diagnostics
    /// surface reports.
    ///
    /// <para><b>Why a snapshot instead of reading live state in the
    /// formatter.</b> This follows <see cref="ViiperDriverReportContext"/>: the
    /// caller does the OS reads and the redaction, and the formatter stays pure
    /// so it can be tested against fabricated input. It also keeps the six
    /// sources from being read at six different instants — a report whose
    /// sections disagree about whether the backend is running is worse than no
    /// report.</para>
    ///
    /// <para><b>Everything in here is safe to put on a clipboard.</b> That is
    /// the invariant of this type, and it is not a comment — the collector
    /// redacts on the way in and <c>ThrumDiagnosticsReportFormatterTests</c>
    /// asserts specific leaks cannot reappear. Three sources were audited as
    /// dangerous and are represented deliberately narrowly:</para>
    /// <list type="bullet">
    /// <item>HidHide's whitelist is a list of every cloaked application's full
    /// path on the machine — the account name and effectively the user's
    /// installed-game inventory. Only <see cref="HidHideThisAppWhitelisted"/>,
    /// a bool about <i>this</i> executable, is carried. The list never is.</item>
    /// <item>An output slot's input display string embeds the physical
    /// controller's Bluetooth MAC. Only the device's display name reaches
    /// <see cref="DiagnosticsSlotRow.InputDisplayName"/>.</item>
    /// <item>Audio endpoint friendly names are user-renameable and routinely
    /// carry a person's name. They are carried, because a diagnostics report
    /// that cannot say which device is default is useless — but the endpoint
    /// <i>IDs</i>, which are stable per-machine correlators, are not.</item>
    /// </list>
    /// </summary>
    public sealed class ThrumDiagnosticsSnapshot
    {
        /// <summary>When the collector took this picture.</summary>
        public DateTimeOffset TimestampUtc { get; init; }

        public string AppVersion { get; init; }

        public string OsVersion { get; init; }

        public string ProcessArchitecture { get; init; }

        /// <summary>Whether the app is running elevated. Explains a whole class
        /// of "it did nothing when I clicked it" reports.</summary>
        public bool Elevated { get; init; }

        public DiagnosticsDriverSection Driver { get; init; }

        public DiagnosticsBackendSection Backend { get; init; }

        public DiagnosticsHidHideSection HidHide { get; init; }

        public DiagnosticsAudioSection Audio { get; init; }

        public IReadOnlyList<DiagnosticsSlotRow> Slots { get; init; }
            = Array.Empty<DiagnosticsSlotRow>();

        public IReadOnlyList<DiagnosticsLinkHealthRow> LinkHealth { get; init; }
            = Array.Empty<DiagnosticsLinkHealthRow>();

        /// <summary>
        /// Sections the collector could not read, with the reason. A section
        /// that failed says so here rather than silently reporting defaults —
        /// "could not look" must never render as "looked and saw nothing".
        /// </summary>
        public IReadOnlyList<string> CollectionFailures { get; init; }
            = Array.Empty<string>();
    }

    /// <summary>usbip-win2 driver gate, from the session-cached readiness.</summary>
    public sealed class DiagnosticsDriverSection
    {
        /// <summary>The four-state gate verdict, as its enum name.</summary>
        public string State { get; init; }

        /// <summary>Human-facing badge text, mirroring the Settings card.</summary>
        public string BadgeText { get; init; }

        public string ReleaseLabel { get; init; }

        public string Tier { get; init; }

        public bool IsManifestMatch { get; init; }

        public bool IsProductionApproved { get; init; }

        public DateTimeOffset EvaluatedAtUtc { get; init; }

        /// <summary>Why the gate is not satisfied. Empty when it is.</summary>
        public IReadOnlyList<string> Reasons { get; init; }
            = Array.Empty<string>();

        /// <summary>
        /// Per-component identity lines (INF provider/name, DriverVer, service,
        /// catalog file name, signer). Deliberately path-free: the readiness
        /// projection already drops the driver-store trust path.
        /// </summary>
        public IReadOnlyList<string> Identities { get; init; }
            = Array.Empty<string>();
    }

    /// <summary>VIIPER backend process and census.</summary>
    public sealed class DiagnosticsBackendSection
    {
        public bool HelperInstalled { get; init; }

        public bool ServerRunning { get; init; }

        /// <summary>Ownership/census state name, or null when not assessed.</summary>
        public string OwnershipState { get; init; }

        /// <summary>Census/assessment detail, already redacted.</summary>
        public string Detail { get; init; }

        /// <summary>
        /// The version Thrum <i>expects</i>, from the installer pin.
        ///
        /// <para>There is no running-version field, and that is not an
        /// oversight: nothing in the product asks the backend what it is. The
        /// ping response body is discarded after a substring test, and no
        /// <c>--version</c> call or <c>FileVersionInfo</c> read against
        /// viiper.exe exists anywhere in the tree. Reporting the pin as though
        /// it were an observation would be a lie, so the report labels it
        /// "expected" and says the running version is not reported.</para>
        /// </summary>
        public string PinnedVersion { get; init; }

        /// <summary>Devices the backend hosts, described without identifiers.</summary>
        public IReadOnlyList<string> Holdings { get; init; }
            = Array.Empty<string>();
    }

    /// <summary>
    /// HidHide. Narrow on purpose — see the type-level note on why the
    /// whitelist itself is never carried.
    /// </summary>
    public sealed class DiagnosticsHidHideSection
    {
        public bool Installed { get; init; }

        /// <summary>
        /// Whether <i>this</i> executable is in the whitelist. The single most
        /// common HidHide failure is the app cloaking controllers from itself.
        /// </summary>
        public bool? ThisAppWhitelisted { get; init; }

        /// <summary>Set when the whitelist could not be read at all.</summary>
        public string ReadFailure { get; init; }
    }

    /// <summary>
    /// Audio endpoint state. Reports what is observable; asserts nothing about
    /// a guard, because there is not one.
    /// </summary>
    public sealed class DiagnosticsAudioSection
    {
        /// <summary>
        /// The 2.3 consent gate. When false the endpoint-takeover path is
        /// unreachable, which is the honest headline for this section.
        /// </summary>
        public bool VirtualAudioEndpointsAllowed { get; init; }

        /// <summary>
        /// Default endpoints by "Flow/Role", friendly names only — never the
        /// endpoint IDs, which are stable per-machine correlators.
        /// </summary>
        public IReadOnlyList<string> DefaultEndpoints { get; init; }
            = Array.Empty<string>();

        /// <summary>Whether a controller-owned render endpoint is present.</summary>
        public bool ControllerRenderEndpointPresent { get; init; }
    }

    /// <summary>One virtual output slot, projected read-only.</summary>
    public sealed class DiagnosticsSlotRow
    {
        public int Index { get; init; }

        public string CurrentType { get; init; }

        public string PermanentType { get; init; }

        /// <summary>
        /// The bound input device's display name only. The live UI string this
        /// derives from also carries the controller's Bluetooth MAC; that is a
        /// persistent hardware identifier and is dropped here.
        /// </summary>
        public string InputDisplayName { get; init; }

        public string Status { get; init; }
    }

    /// <summary>
    /// Per-device link counters. Per-device and per-connection by nature:
    /// every counter resets when a device reconnects, so a low number means
    /// "since this connection", not "since launch".
    /// </summary>
    public sealed class DiagnosticsLinkHealthRow
    {
        public string Device { get; init; }

        public long SpeakerEnqueued { get; init; }

        public long SpeakerDropped { get; init; }

        public long SpeakerExpired { get; init; }

        public long SpeakerHighWater { get; init; }

        public long ControlEnqueued { get; init; }

        public long ControlCoalesced { get; init; }

        public long ControlDropped { get; init; }
    }
}

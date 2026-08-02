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

using DS4Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// Presentation-only projection of an already-redacted diagnostics
    /// snapshot. Each of the six cards is always present, including before the
    /// first read and after a source failure, so missing evidence can never
    /// masquerade as an empty healthy section.
    /// </summary>
    public sealed class DiagnosticsPageViewModel : INotifyPropertyChanged
    {
        private IReadOnlyList<DiagnosticsCardViewModel> sections =
            WaitingCards();
        private ThrumDiagnosticsSnapshot snapshot;
        private bool busy;
        private string statusText = "Diagnostics have not been collected yet.";
        private string copyStatusText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public IReadOnlyList<DiagnosticsCardViewModel> Sections => sections;

        public ThrumDiagnosticsSnapshot Snapshot => snapshot;

        public bool IsBusy => busy;

        public bool CanRefresh => !busy;

        public bool CanCopy => !busy && snapshot != null;

        public string StatusText => statusText;

        public string CopyStatusText => copyStatusText;

        public void BeginRefresh()
        {
            busy = true;
            statusText = "Reading Windows, driver, backend, audio, and live controller state...";
            copyStatusText = string.Empty;
            RaiseAllChanged();
        }

        public void Apply(ThrumDiagnosticsSnapshot value)
        {
            snapshot = value ?? throw new ArgumentNullException(nameof(value));
            sections = BuildCards(value);
            busy = false;
            statusText = "Collected " + value.TimestampUtc.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) +
                (value.CollectionFailures.Count == 0
                    ? "."
                    : "; " + value.CollectionFailures.Count.ToString(
                        CultureInfo.CurrentCulture) +
                        " section(s) could not be read.");
            copyStatusText = string.Empty;
            RaiseAllChanged();
        }

        public void ApplyUnexpectedFailure(Exception exception)
        {
            string reason = exception == null
                ? "The collection task did not return a result."
                : ViiperDriverReportFormatter.RedactUserPathsInText(
                    exception.GetType().Name + ": " + exception.Message);
            snapshot = null;
            sections = SectionNames.Select(section =>
                DiagnosticsCardViewModel.Failed(section.Title,
                    section.Description, reason)).ToArray();
            busy = false;
            statusText = "Diagnostics could not be collected.";
            copyStatusText = string.Empty;
            RaiseAllChanged();
        }

        public void SetCopyStatus(string value)
        {
            copyStatusText = value ?? string.Empty;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(CopyStatusText)));
        }

        internal static IReadOnlyList<DiagnosticsCardViewModel> BuildCards(
            ThrumDiagnosticsSnapshot snapshot)
        {
            return new[]
            {
                BuildDriver(snapshot),
                BuildBackend(snapshot),
                BuildHidHide(snapshot),
                BuildAudio(snapshot),
                BuildSlots(snapshot),
                BuildLinkHealth(snapshot),
            };
        }

        private static DiagnosticsCardViewModel BuildDriver(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "driver gate";
            string failure = Failure(snapshot, key);
            if (snapshot.Driver == null)
            {
                return MissingCard(SectionNames[0], failure);
            }

            DiagnosticsDriverSection driver = snapshot.Driver;
            List<string> lines = new List<string>
            {
                "State: " + Text(driver.State),
                "Release: " + Text(driver.ReleaseLabel),
                "Tier: " + Text(driver.Tier),
                "Manifest match: " + YesNo(driver.IsManifestMatch),
                "Production approved: " +
                    YesNo(driver.IsProductionApproved),
                "Cached evaluation: " +
                    (driver.EvaluatedAtUtc == default
                        ? "not reported"
                        : driver.EvaluatedAtUtc.ToLocalTime().ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.CurrentCulture)),
            };
            lines.AddRange(driver.Reasons.Select(reason =>
                "Reason: " + reason));
            lines.AddRange(driver.Identities.Select(identity =>
                "Identity: " + identity));

            string badgeKind = driver.State switch
            {
                nameof(ViiperDriverReadinessState.Approved) => "Approved",
                nameof(ViiperDriverReadinessState.ValidatedExperimental) =>
                    "Experimental",
                nameof(ViiperDriverReadinessState.DetectedUnvalidated) =>
                    "Unverified",
                _ => "Missing",
            };
            return new DiagnosticsCardViewModel(SectionNames[0].Title,
                SectionNames[0].Description, Text(driver.BadgeText),
                badgeKind, lines);
        }

        private static DiagnosticsCardViewModel BuildBackend(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "VIIPER backend";
            string failure = Failure(snapshot, key);
            if (snapshot.Backend == null)
            {
                return MissingCard(SectionNames[1], failure);
            }

            DiagnosticsBackendSection backend = snapshot.Backend;
            List<string> lines = new List<string>
            {
                "Helper installed: " + YesNo(backend.HelperInstalled),
                "Server responding: " + YesNo(backend.ServerRunning),
                "Ownership: " + Text(backend.OwnershipState),
                "Expected version: " + Text(backend.PinnedVersion),
                "Running version: not reported by the backend",
                "Detail: " + Text(backend.Detail),
            };
            if (backend.Holdings.Count == 0)
            {
                lines.Add(backend.OwnershipState ==
                    nameof(ViiperUnownedBackendState.UnownedIdle)
                        ? "Holdings: nothing registered"
                        : "Holdings: not enumerated or none reported; see Detail above");
            }
            else
            {
                lines.AddRange(backend.Holdings.Select(holding =>
                    "Holding: " + holding));
            }

            return new DiagnosticsCardViewModel(SectionNames[1].Title,
                SectionNames[1].Description,
                backend.ServerRunning ? "Responding" : "Not responding",
                "Unknown", lines);
        }

        private static DiagnosticsCardViewModel BuildHidHide(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "HidHide";
            string failure = Failure(snapshot, key);
            if (snapshot.HidHide == null)
            {
                return MissingCard(SectionNames[2], failure);
            }

            DiagnosticsHidHideSection hidHide = snapshot.HidHide;
            List<string> lines = new List<string>
            {
                "Installed: " + YesNo(hidHide.Installed),
            };
            string badge = hidHide.Installed ? "Installed" : "Not installed";
            string badgeKind = "Missing";
            if (!string.IsNullOrWhiteSpace(hidHide.ReadFailure))
            {
                badge = "Whitelist unreadable";
                badgeKind = "Unverified";
                lines.Add("Whitelist: could not be read");
                lines.Add("Reason: " + hidHide.ReadFailure);
            }
            else if (hidHide.ThisAppWhitelisted.HasValue)
            {
                lines.Add(ProductInfo.ProductName + " whitelisted: " +
                    YesNo(hidHide.ThisAppWhitelisted.Value));
                if (!hidHide.ThisAppWhitelisted.Value)
                {
                    badge = "App not whitelisted";
                    badgeKind = "Unverified";
                    lines.Add("HidHide may be hiding controllers from " +
                        ProductInfo.ProductName + " itself.");
                }
            }
            else
            {
                lines.Add("Whitelist membership: not applicable or not reported");
            }

            lines.Add("Whitelist contents are not collected or displayed.");
            return new DiagnosticsCardViewModel(SectionNames[2].Title,
                SectionNames[2].Description, badge, badgeKind, lines);
        }

        private static DiagnosticsCardViewModel BuildAudio(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "audio endpoints";
            string failure = Failure(snapshot, key);
            if (snapshot.Audio == null)
            {
                return MissingCard(SectionNames[3], failure);
            }

            DiagnosticsAudioSection audio = snapshot.Audio;
            List<string> lines = new List<string>
            {
                "Virtual audio endpoints: " +
                    (audio.VirtualAudioEndpointsAllowed
                        ? "allowed by consent"
                        : "disabled (default)"),
                "Controller render endpoint present: " +
                    YesNo(audio.ControllerRenderEndpointPresent),
            };
            lines.AddRange(audio.DefaultEndpoints.Select(endpoint =>
                "Default: " + endpoint));
            if (audio.DefaultEndpoints.Count == 0)
            {
                lines.Add("Default endpoints: none reported");
            }

            return new DiagnosticsCardViewModel(SectionNames[3].Title,
                SectionNames[3].Description,
                audio.VirtualAudioEndpointsAllowed
                    ? "Endpoint creation allowed"
                    : "Endpoint creation disabled",
                "Unknown", lines);
        }

        private static DiagnosticsCardViewModel BuildSlots(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "output slots";
            string failure = Failure(snapshot, key);
            if (failure != null)
            {
                return DiagnosticsCardViewModel.Failed(
                    SectionNames[4].Title, SectionNames[4].Description,
                    failure);
            }

            List<string> lines = new List<string>();
            foreach (DiagnosticsSlotRow slot in snapshot.Slots)
            {
                string line = "Slot " + slot.Index.ToString(
                    CultureInfo.CurrentCulture) + ": " +
                    Text(slot.CurrentType) + "; permanent " +
                    Text(slot.PermanentType) + "; " + Text(slot.Status);
                if (!string.IsNullOrWhiteSpace(slot.InputDisplayName))
                {
                    line += "; input " + slot.InputDisplayName;
                }

                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                lines.Add("No output slots were reported.");
            }

            return new DiagnosticsCardViewModel(SectionNames[4].Title,
                SectionNames[4].Description,
                snapshot.Slots.Count.ToString(CultureInfo.CurrentCulture) +
                    " slot(s)", "Unknown", lines);
        }

        private static DiagnosticsCardViewModel BuildLinkHealth(
            ThrumDiagnosticsSnapshot snapshot)
        {
            const string key = "controller link health";
            string failure = Failure(snapshot, key);
            if (failure != null)
            {
                return DiagnosticsCardViewModel.Failed(
                    SectionNames[5].Title, SectionNames[5].Description,
                    failure);
            }

            List<string> lines = new List<string>
            {
                "Counters are per virtual output device and reset when it reconnects.",
                "Speaker dropped already includes expired; high water is queue depth, not a cumulative count.",
                "Speaker expiry is disabled for DualShock 4 outputs; zero there means not applicable.",
            };
            foreach (DiagnosticsLinkHealthRow row in snapshot.LinkHealth)
            {
                lines.Add(Text(row.Device) + " - speaker: enqueued " +
                    Number(row.SpeakerEnqueued) + ", dropped " +
                    Number(row.SpeakerDropped) + " (including expired " +
                    Number(row.SpeakerExpired) + "), high water " +
                    Number(row.SpeakerHighWater));
                lines.Add(Text(row.Device) + " - control: enqueued " +
                    Number(row.ControlEnqueued) + ", coalesced " +
                    Number(row.ControlCoalesced) + ", dropped " +
                    Number(row.ControlDropped));
            }

            if (snapshot.LinkHealth.Count == 0)
            {
                lines.Add("No active virtual output is visible in Output Slots.");
            }

            lines.Add("Audio-only VIIPER sidecars are not currently visible to this reader.");
            bool drops = snapshot.LinkHealth.Any(row =>
                row.SpeakerDropped > 0 || row.ControlDropped > 0);
            return new DiagnosticsCardViewModel(SectionNames[5].Title,
                SectionNames[5].Description,
                snapshot.LinkHealth.Count == 0
                    ? "No active virtual output"
                    : drops ? "Drops observed" : "No drops observed",
                drops ? "Experimental" : "Unknown", lines);
        }

        private static DiagnosticsCardViewModel MissingCard(
            (string Title, string Description) section, string failure) =>
            failure == null
                ? new DiagnosticsCardViewModel(section.Title,
                    section.Description, "Not reported", "Unknown",
                    new[] { "This section was not reported." })
                : DiagnosticsCardViewModel.Failed(section.Title,
                    section.Description, failure);

        private static string Failure(ThrumDiagnosticsSnapshot snapshot,
            string section)
        {
            string prefix = section + ":";
            string failure = snapshot.CollectionFailures.FirstOrDefault(
                value => value.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase));
            return failure == null
                ? null
                : failure.Substring(prefix.Length).Trim();
        }

        private static IReadOnlyList<DiagnosticsCardViewModel> WaitingCards()
            => SectionNames.Select(section =>
                new DiagnosticsCardViewModel(section.Title,
                    section.Description, "Waiting", "Unknown",
                    new[] { "Select Refresh to collect this section." }))
                .ToArray();

        private static string Text(string value) =>
            string.IsNullOrWhiteSpace(value) ? "not reported" : value;

        private static string YesNo(bool value) => value ? "yes" : "no";

        private static string Number(long value) =>
            value.ToString(CultureInfo.CurrentCulture);

        private void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

        private static readonly (string Title, string Description)[]
            SectionNames =
            {
                ("usbip-win2 driver gate",
                    "The session-cached package identity and trust verdict. Refresh does not re-run driver validation."),
                ("VIIPER backend",
                    "Helper availability, live loopback response, ownership, and redacted holdings."),
                ("HidHide",
                    "Driver presence and whether this application is allowed to see controllers. The whitelist itself stays private."),
                ("Audio endpoints",
                    "Current Windows defaults by flow and role, plus observable controller render endpoints."),
                ("Output slots",
                    "Virtual output assignment, permanent type, status, and safe input display names."),
                ("Controller link health",
                    "Per-virtual-output speaker and control dispatch counters for the current connection."),
            };
    }

    public sealed class DiagnosticsCardViewModel
    {
        public DiagnosticsCardViewModel(string title, string description,
            string badgeText, string badgeKind, IReadOnlyList<string> lines)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
            BadgeKind = badgeKind ?? "Unknown";
            Lines = lines ?? Array.Empty<string>();
        }

        public string Title { get; }

        public string Description { get; }

        public string BadgeText { get; }

        public string BadgeKind { get; }

        public IReadOnlyList<string> Lines { get; }

        public static DiagnosticsCardViewModel Failed(string title,
            string description, string reason) =>
            new DiagnosticsCardViewModel(title, description,
                "Could not read", "Unverified", new[]
                {
                    "This section could not be read.",
                    "Reason: " + (string.IsNullOrWhiteSpace(reason)
                        ? "not reported"
                        : reason),
                });

        public override string ToString() => Title + ": " + BadgeText;
    }
}

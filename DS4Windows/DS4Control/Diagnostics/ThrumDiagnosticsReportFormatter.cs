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
using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// Renders a <see cref="ThrumDiagnosticsSnapshot"/> as the text behind
    /// "Copy full report".
    ///
    /// <para>Deliberately the same shape as
    /// <see cref="ViiperDriverReportFormatter"/> — pure static, no OS access,
    /// no state, 62-column rules, <c>[OK]</c>/<c>[INFO]</c> tags — because the
    /// two reports get pasted into the same issue threads and should not look
    /// like they came from different products.</para>
    ///
    /// <para>Redaction is applied here as well as in the collector. That is not
    /// belt-and-braces for its own sake: the rule established in commit b9713fc
    /// is that "a report must not depend on every producer remembering to
    /// redact", so anything quoted from outside this type is passed through
    /// <see cref="ViiperDriverReportFormatter.RedactUserPathsInText"/> on the
    /// way out, whatever its provenance.</para>
    /// </summary>
    public static class ThrumDiagnosticsReportFormatter
    {
        private const string Ok = "[OK]";
        private const string Info = "[INFO]";
        private const string Attention = "[ATTENTION]";
        private const int LabelWidth = 22;
        private const int RuleWidth = 62;
        private const string NotReported = "(not reported)";

        public static string Format(ThrumDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            StringBuilder builder = new StringBuilder();
            AppendHeader(builder, snapshot);
            AppendDriver(builder, snapshot.Driver);
            AppendBackend(builder, snapshot.Backend);
            AppendHidHide(builder, snapshot.HidHide);
            AppendAudio(builder, snapshot.Audio);
            AppendSlots(builder, snapshot);
            AppendLinkHealth(builder, snapshot);
            AppendFailures(builder, snapshot);
            AppendFooter(builder);
            return builder.ToString();
        }

        private static void AppendHeader(StringBuilder builder,
            ThrumDiagnosticsSnapshot snapshot)
        {
            builder.AppendLine(ProductInfo.ProductName + " diagnostics report");
            builder.AppendLine(new string('=', RuleWidth));
            AppendField(builder, "generated (UTC)",
                snapshot.TimestampUtc.UtcDateTime.ToString(
                    "yyyy-MM-dd HH:mm:ss'Z'",
                    System.Globalization.CultureInfo.InvariantCulture));
            AppendField(builder, ProductInfo.ProductName + " version",
                snapshot.AppVersion);
            AppendField(builder, "operating system", snapshot.OsVersion);
            AppendField(builder, "process architecture",
                snapshot.ProcessArchitecture);
            AppendField(builder, "elevated", YesNo(snapshot.Elevated));
        }

        private static void AppendDriver(StringBuilder builder,
            DiagnosticsDriverSection driver)
        {
            AppendSection(builder, "usbip-win2 driver gate");
            if (driver == null)
            {
                builder.AppendLine("  " + NotReported);
                return;
            }

            AppendField(builder, "state", driver.State);
            AppendField(builder, "badge", driver.BadgeText);
            AppendField(builder, "release", driver.ReleaseLabel);
            AppendField(builder, "tier", driver.Tier);
            AppendField(builder, "manifest match", YesNo(driver.IsManifestMatch));
            AppendField(builder, "production approved",
                YesNo(driver.IsProductionApproved));
            AppendField(builder, "evaluated (UTC)",
                driver.EvaluatedAtUtc == default
                    ? NotReported
                    : driver.EvaluatedAtUtc.UtcDateTime.ToString(
                        "yyyy-MM-dd HH:mm:ss'Z'",
                        System.Globalization.CultureInfo.InvariantCulture));

            AppendList(builder, "reasons", driver.Reasons, Attention);
            AppendList(builder, "identities", driver.Identities, Info);
        }

        private static void AppendBackend(StringBuilder builder,
            DiagnosticsBackendSection backend)
        {
            AppendSection(builder, "VIIPER backend");
            if (backend == null)
            {
                builder.AppendLine("  " + NotReported);
                return;
            }

            AppendField(builder, "helper installed",
                YesNo(backend.HelperInstalled));
            AppendField(builder, "server responding",
                YesNo(backend.ServerRunning));
            AppendField(builder, "ownership", backend.OwnershipState);
            AppendField(builder, "detail", backend.Detail);
            AppendField(builder, "expected version", backend.PinnedVersion);
            // Stated rather than omitted: a reader who sees only "expected"
            // should know the running version is unavailable by design, not
            // missing by accident.
            AppendField(builder, "running version",
                "not reported by the backend");
            AppendList(builder, "holdings", backend.Holdings, Info);
        }

        private static void AppendHidHide(StringBuilder builder,
            DiagnosticsHidHideSection hidHide)
        {
            AppendSection(builder, "HidHide");
            if (hidHide == null)
            {
                builder.AppendLine("  " + NotReported);
                return;
            }

            AppendField(builder, "installed", YesNo(hidHide.Installed));
            if (!string.IsNullOrWhiteSpace(hidHide.ReadFailure))
            {
                AppendField(builder, "whitelist", "could not be read");
                AppendField(builder, "reason",
                    ViiperDriverReportFormatter.RedactUserPathsInText(
                        hidHide.ReadFailure));
            }
            else if (hidHide.ThisAppWhitelisted.HasValue)
            {
                AppendField(builder, "this app whitelisted",
                    YesNo(hidHide.ThisAppWhitelisted.Value),
                    hidHide.ThisAppWhitelisted.Value ? Ok : Attention);
                if (!hidHide.ThisAppWhitelisted.Value)
                {
                    builder.AppendLine("  " + ProductInfo.ProductName +
                        " is not in the HidHide whitelist, so it is hiding");
                    builder.AppendLine("  controllers from itself. Controllers " +
                        "will appear missing.");
                }
            }
            else
            {
                AppendField(builder, "this app whitelisted", NotReported);
            }

            // The whitelist itself is every cloaked application's full path on
            // this machine. It is not collected and must never be printed.
            AppendField(builder, "whitelist contents",
                "not included by design", Info);
        }

        private static void AppendAudio(StringBuilder builder,
            DiagnosticsAudioSection audio)
        {
            AppendSection(builder, "audio endpoints");
            if (audio == null)
            {
                builder.AppendLine("  " + NotReported);
                return;
            }

            AppendField(builder, "virtual endpoints",
                audio.VirtualAudioEndpointsAllowed
                    ? "allowed by consent"
                    : "disabled (default)");
            AppendField(builder, "controller endpoint",
                YesNo(audio.ControllerRenderEndpointPresent));
            // Stated as a fact about the product, not as a fault.
            AppendField(builder, "default-endpoint guard", "not installed");
            AppendList(builder, "current defaults", audio.DefaultEndpoints, Info);
        }

        private static void AppendSlots(StringBuilder builder,
            ThrumDiagnosticsSnapshot snapshot)
        {
            AppendSection(builder, "output slots");
            if (snapshot.Slots == null || snapshot.Slots.Count == 0)
            {
                builder.AppendLine("  none");
                return;
            }

            foreach (DiagnosticsSlotRow slot in snapshot.Slots)
            {
                builder.AppendLine("  " +
                    ("slot " + slot.Index.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
                        .PadRight(LabelWidth) +
                    ": " + Text(slot.CurrentType) +
                    " / permanent " + Text(slot.PermanentType) +
                    " / " + Text(slot.Status));
                if (!string.IsNullOrWhiteSpace(slot.InputDisplayName))
                {
                    builder.AppendLine("  " + string.Empty.PadRight(LabelWidth) +
                        "  input: " +
                        ViiperDriverReportFormatter.RedactUserPathsInText(
                            slot.InputDisplayName));
                }
            }
        }

        private static void AppendLinkHealth(StringBuilder builder,
            ThrumDiagnosticsSnapshot snapshot)
        {
            AppendSection(builder, "controller link health");
            if (snapshot.LinkHealth == null || snapshot.LinkHealth.Count == 0)
            {
                builder.AppendLine("  no active virtual output");
                return;
            }

            builder.AppendLine("  counters are per device and reset on reconnect " +
                Info);
            foreach (DiagnosticsLinkHealthRow row in snapshot.LinkHealth)
            {
                builder.AppendLine("  " + Text(row.Device));
                builder.AppendLine("    speaker  enqueued " + row.SpeakerEnqueued +
                    ", dropped " + row.SpeakerDropped +
                    ", expired " + row.SpeakerExpired +
                    ", high water " + row.SpeakerHighWater +
                    "  " + (row.SpeakerDropped > 0 || row.SpeakerExpired > 0
                        ? Attention : Ok));
                builder.AppendLine("    control  enqueued " + row.ControlEnqueued +
                    ", coalesced " + row.ControlCoalesced +
                    ", dropped " + row.ControlDropped +
                    "  " + (row.ControlDropped > 0 ? Attention : Ok));
            }
        }

        private static void AppendFailures(StringBuilder builder,
            ThrumDiagnosticsSnapshot snapshot)
        {
            if (snapshot.CollectionFailures == null ||
                snapshot.CollectionFailures.Count == 0)
            {
                return;
            }

            AppendSection(builder, "sections that could not be read");
            foreach (string failure in snapshot.CollectionFailures)
            {
                builder.AppendLine("  " +
                    ViiperDriverReportFormatter.RedactUserPathsInText(failure) +
                    "  " + Attention);
            }
        }

        private static void AppendFooter(StringBuilder builder)
        {
            AppendSection(builder, "notes");
            builder.AppendLine("  This report is read-only. Collecting it does " +
                "not install, start, stop,");
            builder.AppendLine("  attach or change anything.");
            builder.AppendLine("  It contains no user account names, device " +
                "instance paths, serials,");
            builder.AppendLine("  radio addresses, driver store paths or audio " +
                "endpoint IDs. The");
            builder.AppendLine("  HidHide whitelist is deliberately not included: " +
                "it would list every");
            builder.AppendLine("  cloaked application on this machine.");
        }

        private static void AppendSection(StringBuilder builder, string title)
        {
            builder.AppendLine();
            builder.AppendLine("-- " + title + " " +
                new string('-', Math.Max(3, RuleWidth - title.Length - 4)));
        }

        private static void AppendField(StringBuilder builder, string label,
            string value, string tag = null)
        {
            builder.AppendLine("  " + label.PadRight(LabelWidth) + ": " +
                Text(value) + (tag == null ? string.Empty : "  " + tag));
        }

        private static void AppendList(StringBuilder builder, string label,
            System.Collections.Generic.IReadOnlyList<string> values, string tag)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            builder.AppendLine("  " + label.PadRight(LabelWidth) + ":");
            foreach (string value in values)
            {
                builder.AppendLine("    - " +
                    ViiperDriverReportFormatter.RedactUserPathsInText(value) +
                    "  " + tag);
            }
        }

        private static string Text(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? NotReported
                : ViiperDriverReportFormatter.RedactUserPathsInText(value);

        private static string YesNo(bool value) => value ? "yes" : "no";
    }
}

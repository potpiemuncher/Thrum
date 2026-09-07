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

using DS4Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// The Settings backend-process card — the diagnostics affordance for
    /// lifecycle invariant (d).
    ///
    /// <para>The card answers the question a user with a stale virtual
    /// controller actually has: <i>what is this backend, why does the app not
    /// clean it up, and what can I do about it?</i> The stop-on-exit policy is
    /// deliberately conservative — a backend this session did not start is
    /// never stopped automatically — so when a session dies hard, its backend
    /// and pads outlive it and every later session correctly refuses to touch
    /// them. This card is where that situation stops being invisible.</para>
    ///
    /// <para>Wording rules, enforced by tests: the card never claims to know
    /// whether foreign devices are leftovers or another program's live
    /// controllers (it cannot know, so it says both); the stop button is
    /// offered exactly when <see cref="ViiperUnownedBackendReport.OffersStop"/>
    /// says so; and the confirmation spells out the consequence for the case
    /// where the devices turn out to be someone's live controllers.</para>
    ///
    /// <para>Pure and synchronous like the driver-status card:
    /// <see cref="Apply"/> takes a report and produces text; the view owns
    /// threading.</para>
    /// </summary>
    public sealed class ViiperBackendStatusViewModel : INotifyPropertyChanged
    {
        private ViiperUnownedBackendReport report;
        private bool busy;
        private string actionResultText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Null until the first <see cref="Apply"/>.</summary>
        public ViiperUnownedBackendReport Report => report;

        /// <summary>True while a refresh or a stop request is running.</summary>
        public bool IsBusy
        {
            get => busy;
            set
            {
                if (busy == value)
                {
                    return;
                }

                busy = value;
                RaiseAllChanged();
            }
        }

        public string Headline
        {
            get
            {
                if (busy)
                {
                    return "Checking the VIIPER backend...";
                }

                if (report == null)
                {
                    return "The VIIPER backend has not been checked yet.";
                }

                switch (report.State)
                {
                    case ViiperUnownedBackendState.NoBackend:
                        return "No VIIPER backend is running.";
                    case ViiperUnownedBackendState.ManagedByThisApp:
                        return "The running backend was started by " +
                            ProductInfo.ProductName + " and is managed by it" +
                            (string.IsNullOrEmpty(report.Detail)
                                ? "."
                                : " (" + report.Detail + ").");
                    case ViiperUnownedBackendState.UnownedIdle:
                        return "A backend " + ProductInfo.ProductName +
                            " did not start is running. It is hosting " +
                            "nothing, so it is harmless - but it is not " +
                            ProductInfo.ProductName +
                            "'s, so it is never stopped automatically.";
                    case ViiperUnownedBackendState.UnownedServingThisApp:
                        return "A backend " + ProductInfo.ProductName +
                            " did not start is serving this session's " +
                            "virtual controller(s). Leave it running while " +
                            "they are in use.";
                    case ViiperUnownedBackendState.UnownedInUse:
                        return "A backend " + ProductInfo.ProductName +
                            " did not start is running and hosting virtual " +
                            "devices " + ProductInfo.ProductName +
                            " cannot account for. If a previous session " +
                            "ended without cleaning up, these are its " +
                            "leftovers; if another program is using VIIPER, " +
                            "they are that program's controllers." +
                            (report.ServesThisApp
                                ? " Some of what it hosts are this " +
                                  "session's own controllers, so stopping " +
                                  "it from here is disabled - disconnect " +
                                  "them first."
                                : string.Empty);
                    default:
                        return "A backend " + ProductInfo.ProductName +
                            " did not start is running, and what it hosts " +
                            "could not be read (" + report.Detail + "). " +
                            "Nothing is offered for a backend that cannot " +
                            "be read.";
                }
            }
        }

        /// <summary>
        /// One line per thing the backend holds, in the order a reader
        /// resolves them: devices that are not ours, devices that are, then
        /// buses with nothing on them.
        /// </summary>
        public IReadOnlyList<string> HoldingLines
        {
            get
            {
                if (busy || report == null)
                {
                    return Array.Empty<string>();
                }

                List<string> lines = new List<string>();
                foreach (ViiperCensusDevice device in report.ForeignDevices)
                {
                    lines.Add(device + " - not created by this session");
                }

                foreach (ViiperCensusDevice device in report.OurDevices)
                {
                    lines.Add(device + " - this session's controller");
                }

                foreach (uint bus in report.EmptyBuses)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "bus {0} - registered but empty", bus));
                }

                return lines;
            }
        }

        public bool HasHoldings => HoldingLines.Count > 0;

        /// <summary>
        /// Mirrors <see cref="ViiperUnownedBackendReport.OffersStop"/>; the
        /// gating itself lives in the report so the rule is tested once.
        /// </summary>
        public bool ShowStopButton => !busy && report?.OffersStop == true;

        /// <summary>The outcome line of the last stop request, if any.</summary>
        public string ActionResultText => busy ? string.Empty : actionResultText;

        public bool HasActionResult => !string.IsNullOrEmpty(ActionResultText);

        /// <summary>
        /// The body of the confirmation dialog for the current report. Built
        /// here so the words that ask for consent are testable: they must
        /// list what would be stopped and name the consequence if those
        /// devices are in live use.
        /// </summary>
        public string BuildStopConfirmationBody()
        {
            ViiperUnownedBackendReport current = report;
            string holdings = current == null || current.State ==
                ViiperUnownedBackendState.UnownedIdle
                ? "It is hosting nothing."
                : "It is holding: " + current.DescribeHoldings() + ".";

            return "Stop the VIIPER backend process?\n\n" + holdings + "\n\n" +
                "If these are leftovers of a session that ended abruptly, " +
                "stopping the backend unplugs them cleanly. If another " +
                "program is using this backend, that program will lose its " +
                "virtual controller(s). " + ProductInfo.ProductName +
                " cannot tell the two apart - that is why this asks.";
        }

        public ViiperUnownedBackendReport Apply(
            ViiperUnownedBackendReport value)
        {
            report = value;
            RaiseAllChanged();
            return value;
        }

        /// <summary>Records what a stop request came to, success or refusal.</summary>
        public void ApplyStopOutcome(ViiperUnownedBackendStopOutcome outcome)
        {
            if (outcome == null)
            {
                return;
            }

            if (!outcome.Attempted)
            {
                actionResultText = "Not stopped: " + outcome.Reason + ".";
            }
            else if (outcome.Succeeded)
            {
                actionResultText = "Backend stopped (" +
                    outcome.ProcessIdentity + "; " +
                    (outcome.Method == ViiperBackendStopMethod.Graceful
                        ? "exited on its own"
                        : "had to be killed") + ").";
            }
            else
            {
                actionResultText = "The stop did not complete: " +
                    outcome.Reason + ".";
            }

            RaiseAllChanged();
        }

        /// <summary>
        /// One notification for the whole card; every property derives from
        /// the same report.
        /// </summary>
        private void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

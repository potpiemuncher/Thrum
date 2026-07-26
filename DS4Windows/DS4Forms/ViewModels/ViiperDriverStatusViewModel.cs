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

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// The Settings driver-status card.
    ///
    /// <para><b>The wording here is policy, not copy.</b> Three rules come out
    /// of the VM validation pass and are enforced by unit tests:</para>
    /// <list type="number">
    /// <item>Never recommend installing a package the manifest does not list.
    /// The card describes what is installed; the only install path it points at
    /// is the existing bundled setup, which targets a listed release.</item>
    /// <item>"Validated" is never "production approved". A manifest match at the
    /// experimental tier reads as <i>known package, experimental, known
    /// risk</i>, and the card says so on the same screen as the badge.</item>
    /// <item>A state that will restrict features says so plainly, in the state's
    /// own words, rather than leaving the user to discover it later.</item>
    /// </list>
    ///
    /// <para>Pure and synchronous by design: <see cref="Apply"/> takes a
    /// readiness and produces text. The view owns threading, so the SetupAPI and
    /// WinVerifyTrust work never runs on the dispatcher and the formatting stays
    /// testable without a WPF application.</para>
    /// </summary>
    public sealed class ViiperDriverStatusViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// The single sentence that must never be softened: no usbip-win2
        /// release is production-approved, so a match is evidence and nothing
        /// more.
        /// </summary>
        public const string NotProductionApprovedNote =
            "A match means " + ProductInfo.ProductName +
            " recognizes this exact package. It is not " +
            "production approval: no usbip-win2 release is approved for " +
            "production use today, and this one carries the known kernel " +
            "request-lifetime risk that affects virtual USB audio endpoints.";

        private readonly Func<ViiperDriverReadiness> read;
        private readonly Func<ViiperDriverReadiness> refresh;

        private ViiperDriverReadiness readiness;
        private bool busy;
        private string reportDisplayPath;
        private string reportText;
        private string reportFilePath;
        private string reportError;

        public ViiperDriverStatusViewModel()
            : this(null, null)
        {
        }

        /// <param name="read">Cached readiness source; defaults to the session cache.</param>
        /// <param name="refresh">Re-check source; defaults to a real re-read.</param>
        public ViiperDriverStatusViewModel(Func<ViiperDriverReadiness> read,
            Func<ViiperDriverReadiness> refresh)
        {
            this.read = read ?? (() => ViiperSetupManager.DriverReadiness);
            this.refresh = refresh ??
                (() => ViiperSetupManager.RefreshDriverReadiness());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Null until the first <see cref="Load"/> or <see cref="Apply"/>.</summary>
        public ViiperDriverReadiness Readiness => readiness;

        /// <summary>
        /// Drives the badge colour. Null before the first evaluation so the card
        /// can render a neutral "not checked yet" badge rather than claim a
        /// state it has not established.
        /// </summary>
        public ViiperDriverReadinessState? State => readiness?.State;

        /// <summary>False while a check or diagnostic is running.</summary>
        public bool CanInteract => !busy;

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

        public string BadgeText
        {
            get
            {
                if (busy)
                {
                    return "Checking";
                }

                if (readiness == null)
                {
                    return "Not checked";
                }

                return readiness.State switch
                {
                    ViiperDriverReadinessState.Missing => "Not installed",
                    ViiperDriverReadinessState.ValidatedExperimental =>
                        "Experimental - known package",
                    ViiperDriverReadinessState.Approved => "Production approved",
                    _ => "Unverified",
                };
            }
        }

        /// <summary>
        /// Which badge treatment the card uses. A plain token rather than the
        /// enum so the theme dictionary can trigger on it without depending on
        /// nullable-enum conversion, and so "checking" and "not checked yet" —
        /// which are not readiness states — get their own neutral look.
        /// </summary>
        public string BadgeKind
        {
            get
            {
                if (busy)
                {
                    return "Checking";
                }

                if (readiness == null)
                {
                    return "Unknown";
                }

                return readiness.State switch
                {
                    ViiperDriverReadinessState.Missing => "Missing",
                    ViiperDriverReadinessState.ValidatedExperimental =>
                        "Experimental",
                    ViiperDriverReadinessState.Approved => "Approved",
                    _ => "Unverified",
                };
            }
        }

        /// <summary>The one-line answer, stated as an observation.</summary>
        public string Headline
        {
            get
            {
                if (busy)
                {
                    return "Reading the installed usbip-win2 driver packages...";
                }

                if (readiness == null)
                {
                    return "The usbip-win2 driver packages have not been " +
                        "checked yet.";
                }

                switch (readiness.State)
                {
                    case ViiperDriverReadinessState.Missing:
                        return "No usbip-win2 driver package is installed. " +
                            "VIIPER virtual controllers need one.";
                    case ViiperDriverReadinessState.ValidatedExperimental:
                        return "The installed packages exactly match a package " +
                            "identity " + ProductInfo.ProductName +
                            " knows: usbip-win2 " +
                            DescribeRelease() + ", an experimental baseline.";
                    case ViiperDriverReadinessState.Approved:
                        return "The installed packages match usbip-win2 " +
                            DescribeRelease() +
                            ", a release accepted for production use.";
                    default:
                        return "A usbip-win2 driver is installed, but " +
                            ProductInfo.ProductName + " " +
                            "could not confirm which package it is.";
                }
            }
        }

        /// <summary>
        /// What the state means for the user, in plain terms. Empty for the two
        /// states that need no warning.
        /// </summary>
        public string RestrictionText
        {
            get
            {
                if (busy || readiness == null)
                {
                    return string.Empty;
                }

                switch (readiness.State)
                {
                    case ViiperDriverReadinessState.Missing:
                        return "Virtual controller output is unavailable until a " +
                            "driver is installed. Use Install / Repair VIIPER " +
                            "above, which installs the exact package version " +
                            ProductInfo.ProductName + " lists.";
                    case ViiperDriverReadinessState.DetectedUnvalidated:
                        return ProductInfo.ProductName +
                            " cannot vouch for this package, so virtual " +
                            "controller features will be restricted while it is " +
                            "in this state. " + ProductInfo.ProductName +
                            " does not recommend installing " +
                            "any package it does not list; the diagnostic below " +
                            "shows exactly what was found.";
                    default:
                        return string.Empty;
                }
            }
        }

        public bool HasRestriction => !string.IsNullOrEmpty(RestrictionText);

        /// <summary>
        /// Shown whenever a manifest entry matched. Both tiers get a note: the
        /// experimental one says a match is not approval, and the production one
        /// still names the tier rather than implying "safe".
        /// </summary>
        public string TierNote
        {
            get
            {
                if (busy || readiness == null)
                {
                    return string.Empty;
                }

                return readiness.State switch
                {
                    ViiperDriverReadinessState.ValidatedExperimental =>
                        NotProductionApprovedNote,
                    ViiperDriverReadinessState.Approved =>
                        "This release is recorded in " + ProductInfo.ProductName +
                        "'s manifest at the " +
                        "Production tier.",
                    _ => string.Empty,
                };
            }
        }

        public bool HasTierNote => !string.IsNullOrEmpty(TierNote);

        public IReadOnlyList<string> Reasons =>
            busy || readiness == null
                ? Array.Empty<string>()
                : readiness.Reasons;

        public bool HasReasons => Reasons.Count > 0;

        public IReadOnlyList<ViiperDriverComponentIdentity> Identities =>
            busy || readiness == null
                ? Array.Empty<ViiperDriverComponentIdentity>()
                : readiness.Identities;

        public bool HasIdentities => Identities.Count > 0;

        public string CheckedAtText => readiness == null
            ? string.Empty
            : "Checked " + readiness.EvaluatedAtUtc.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm") + ".";

        /// <summary>Where the last full diagnostic was saved, or the failure.</summary>
        public string ReportStatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(reportError))
                {
                    return "The report could not be saved: " + reportError;
                }

                return string.IsNullOrEmpty(reportDisplayPath)
                    ? string.Empty
                    : "Report saved to " + reportDisplayPath;
            }
        }

        public bool HasReportStatus => !string.IsNullOrEmpty(ReportStatusText);

        /// <summary>The last report's text, for the copy action.</summary>
        public string ReportText => reportText;

        /// <summary>The last report's file, for the open action; null if unsaved.</summary>
        public string ReportFilePath => reportFilePath;

        public bool HasReport => !string.IsNullOrEmpty(reportText);

        public bool CanOpenReport => !string.IsNullOrEmpty(reportFilePath);

        /// <summary>Populates from the session cache, evaluating on first use.</summary>
        public ViiperDriverReadiness Load() => Apply(read());

        /// <summary>The re-check button: discards the cache and reads again.</summary>
        public ViiperDriverReadiness Recheck() => Apply(refresh());

        public ViiperDriverReadiness Apply(ViiperDriverReadiness value)
        {
            readiness = value;
            RaiseAllChanged();
            return value;
        }

        /// <summary>
        /// Records the outcome of a full diagnostic run. The readiness is taken
        /// from the same pass, so the card and the report can never disagree
        /// about what is installed.
        /// </summary>
        public void ApplyDiagnostic(ViiperDriverDiagnosticRun run)
        {
            if (run == null)
            {
                return;
            }

            reportText = run.Text;
            reportFilePath = run.FilePath;
            reportDisplayPath = run.DisplayPath;
            reportError = run.WriteError;
            RaiseAllChanged();
        }

        private string DescribeRelease() =>
            string.IsNullOrWhiteSpace(readiness?.ReleaseLabel)
                ? "(release not reported)"
                : readiness.ReleaseLabel;

        /// <summary>
        /// One notification for the whole card. Every property is derived from
        /// the same readiness, so they change together and enumerating them
        /// would only be a list to forget to update.
        /// </summary>
        private void RaiseAllChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

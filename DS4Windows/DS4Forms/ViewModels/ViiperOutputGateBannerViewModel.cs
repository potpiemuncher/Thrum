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
using System.ComponentModel;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// The banner above the Output Slots table.
    ///
    /// <para>Output Slots is the page where a user plugs a virtual controller in
    /// by hand, so it is the page where a refusal has to be visible <i>before</i>
    /// they press the button and watch nothing happen. The banner states the
    /// same reason the gate would give, from the same
    /// <see cref="ViiperVirtualDeviceGate"/> call, so the page and the log can
    /// never disagree.</para>
    ///
    /// <para>Two things it deliberately does not do. It does not disable the
    /// Plug button - the refusal explains itself better than a greyed-out
    /// control does, and the gate is authoritative anyway. And it never claims a
    /// running controller is affected: everything already attached keeps
    /// working, which the audio row says out loud, because a user who reads
    /// "blocked" while their pad is plainly working will conclude the message is
    /// wrong.</para>
    /// </summary>
    public sealed class ViiperOutputGateBannerViewModel : INotifyPropertyChanged
    {
        private readonly Func<ViiperVirtualDeviceDecision> controllerDecision;
        private readonly Func<ViiperVirtualDeviceDecision> audioDecision;

        private ViiperVirtualDeviceDecision controller;
        private ViiperVirtualDeviceDecision audio;

        public ViiperOutputGateBannerViewModel()
            : this(null, null)
        {
        }

        public ViiperOutputGateBannerViewModel(
            Func<ViiperVirtualDeviceDecision> controllerDecision,
            Func<ViiperVirtualDeviceDecision> audioDecision)
        {
            this.controllerDecision = controllerDecision ??
                (() => ViiperVirtualDeviceGuard.Decide(
                    ViiperFeatureClass.ControllerOnly));
            this.audioDecision = audioDecision ??
                (() => ViiperVirtualDeviceGuard.Decide(ViiperFeatureClass.Audio));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Hidden in the only state that needs no explanation: everything is
        /// permitted. Anything else gets a line.
        /// </summary>
        public bool IsVisible => !string.IsNullOrEmpty(Text);

        /// <summary>
        /// "Blocked" when no new virtual controller can be created at all,
        /// "Limited" when only the audio class is refused. The distinction
        /// matters: one of them means the page does nothing, the other means the
        /// page works and one capability is missing.
        /// </summary>
        public string Headline
        {
            get
            {
                if (controller != null && !controller.Allowed)
                {
                    return "New virtual controllers are blocked";
                }

                if (audio != null && !audio.Allowed)
                {
                    return "Virtual audio endpoints are off";
                }

                return string.Empty;
            }
        }

        /// <summary>The reason, verbatim from the gate.</summary>
        public string Text
        {
            get
            {
                if (controller != null && !controller.Allowed)
                {
                    return controller.Reason;
                }

                if (audio != null && !audio.Allowed)
                {
                    return audio.Reason +
                        " Virtual controllers that are already plugged in are " +
                        "not affected and keep running.";
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Drives the banner's treatment. A refusal to create anything is an
        /// error; a missing capability the user switched off is a warning.
        /// </summary>
        public string Severity
        {
            get
            {
                if (controller != null && !controller.Allowed)
                {
                    return "Blocked";
                }

                return audio != null && !audio.Allowed ? "Limited" : "None";
            }
        }

        /// <summary>Re-asks the gate and republishes. Cheap: the readiness is cached.</summary>
        public void Refresh() =>
            // The audio answer is only meaningful when a controller can be
            // created at all; asking anyway keeps the property getters total.
            Apply(controllerDecision(), audioDecision());

        /// <summary>
        /// Publishes decisions somebody else computed. The view uses this so the
        /// first evaluation - which can cost a SetupAPI enumeration - happens on
        /// a worker thread and never on the dispatcher.
        /// </summary>
        public void Apply(ViiperVirtualDeviceDecision controllerDecisionValue,
            ViiperVirtualDeviceDecision audioDecisionValue)
        {
            controller = controllerDecisionValue;
            audio = audioDecisionValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}

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
using DS4WinWPF;

namespace DS4Windows
{
    public enum LogCategory
    {
        General,
        ViiperBackend,
        DriverUsbip,
        HidHide,
        Audio,
        Controller,
        Profile,
    }

    /// <summary>
    /// Classifies the product's existing GUI log messages without owning any
    /// service or UI state. Markers are intentionally broad subsystem names,
    /// not native-mode protocol details. LogViewModel caches the result on a
    /// LogItem before the item enters the observable collection.
    /// </summary>
    public static class LogClassifier
    {
        private static readonly string[] DriverMarkers =
        [
            "usbip", "USB/IP", "kernel driver", "driver package",
            "driver report", "driver notice",
        ];

        private static readonly string[] HidHideMarkers = ["HidHide"];

        private static readonly string[] ViiperMarkers =
        [
            "VIIPER", "virtual controller", "virtual-controller",
            "output slot", "output device was associated",
        ];

        private static readonly string[] AudioMarkers =
        [
            "audio", "haptic", "speaker", "microphone", "mic-in",
            "capture", "Opus",
        ];

        private static readonly string[] ProfileMarkers =
        [
            "profile", "Profiles.xml", "Actions.xml", "LinkedProfiles.xml",
        ];

        private static readonly string[] ControllerMarkers =
        [
            "controller", "gamepad", "DS4 Input",
        ];

        public static LogCategory Classify(LogItem item) =>
            Classify(item?.Message);

        public static LogCategory Classify(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return LogCategory.General;
            }

            // Specific shared prerequisites win over VIIPER. Real messages
            // can name both, for example a failed usbip detach or a VIIPER
            // output that HidHide could not hide.
            if (ContainsAny(message, DriverMarkers))
            {
                return LogCategory.DriverUsbip;
            }

            if (ContainsAny(message, HidHideMarkers))
            {
                return LogCategory.HidHide;
            }

            if (ContainsAny(message, ViiperMarkers))
            {
                return LogCategory.ViiperBackend;
            }

            // Audio producers consistently name the route or medium: audio
            // passthrough, haptics, speakers, microphones, capture, or Opus.
            if (ContainsAny(message, AudioMarkers))
            {
                return LogCategory.Audio;
            }

            // Profile must precede controller because Auto-Profile and the
            // ordinary profile-switch messages also name a controller slot.
            if (ContainsAny(message, ProfileMarkers))
            {
                return LogCategory.Profile;
            }

            if (ContainsAny(message, ControllerMarkers))
            {
                return LogCategory.Controller;
            }

            return LogCategory.General;
        }

        public static string GetDisplayName(LogCategory category)
        {
            return category switch
            {
                LogCategory.ViiperBackend => "VIIPER / backend",
                LogCategory.DriverUsbip => "Driver / USB/IP",
                LogCategory.HidHide => "HidHide",
                LogCategory.Audio => "Audio",
                LogCategory.Controller => "Controller",
                LogCategory.Profile => "Profile",
                _ => "General",
            };
        }

        private static bool ContainsAny(string message, string[] markers)
        {
            foreach (string marker in markers)
            {
                if (message.IndexOf(marker,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Pure predicate used by the Log tab's ICollectionView. It reads the
    /// category cached on LogItem and never reclassifies while filtering.
    /// </summary>
    public static class LogFilter
    {
        public static bool Matches(LogItem item, bool warningsOnly,
            LogCategory? category, string searchText)
        {
            if (item == null)
            {
                return false;
            }

            if (warningsOnly && !item.Warning)
            {
                return false;
            }

            if (category.HasValue && item.Category != category.Value)
            {
                return false;
            }

            return string.IsNullOrEmpty(searchText) ||
                (item.Message?.IndexOf(searchText,
                    StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }
    }

    public static class LogCopyFormatter
    {
        public static string Format(IEnumerable<LogItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            return string.Join(Environment.NewLine, items.Select(item =>
                $"{item.Datetime:G} [{LogClassifier.GetDisplayName(item.Category)}] {item.Message}"));
        }
    }
}

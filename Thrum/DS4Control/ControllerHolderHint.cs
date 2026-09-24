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
using System.Diagnostics;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// Explains why a controller could not be hidden (opened exclusively):
    /// another program already has it open.
    ///
    /// <para>Windows does not say which process holds a HID device without
    /// administrator rights, so this names the well-known controller programs
    /// that are running now as likely holders. Exclusive mode used to answer
    /// this failure by relaunching Thrum elevated (a UAC prompt, mid-game) to
    /// restart the device.</para>
    /// </summary>
    internal static class ControllerHolderHint
    {
        // Process name (no .exe) to the name users know.
        private static readonly (string Process, string Program)[] KnownPrograms =
        {
            ("steam", "Steam"),
            ("steamwebhelper", "Steam"),
            ("DS4Windows", "DS4Windows"),
            ("DSX", "DSX"),
            ("DualSenseX", "DSX"),
            ("reWASDEngine", "reWASD"),
            ("reWASDTray", "reWASD"),
            ("RemotePlay", "PS Remote Play"),
            ("InputMapper", "InputMapper"),
            ("BetterJoy", "BetterJoy"),
            ("x360ce", "x360ce"),
            ("JoyToKey", "JoyToKey"),
            ("antimicrox", "AntiMicroX"),
            ("upc", "Ubisoft Connect"),
            ("NVIDIA Share", "NVIDIA overlay"),
        };

        /// <summary>
        /// Known controller programs among <paramref name="processNames"/>,
        /// each once, in list order.
        /// </summary>
        internal static IReadOnlyList<string> MatchKnownPrograms(IEnumerable<string> processNames)
        {
            HashSet<string> running = new HashSet<string>(
                processNames.Where(name => !string.IsNullOrEmpty(name)),
                StringComparer.OrdinalIgnoreCase);
            return KnownPrograms
                .Where(known => running.Contains(known.Process))
                .Select(known => known.Program)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        internal static string BuildMessage(string controllerName,
            IReadOnlyList<string> runningPrograms)
        {
            string running = runningPrograms.Count switch
            {
                0 => "Close any program that uses controllers (for example Steam, a game or a browser)",
                1 => $"{runningPrograms[0]} is running and may be using it. Close it",
                _ => $"{string.Join(", ", runningPrograms.Take(runningPrograms.Count - 1))} and {runningPrograms[^1]} are running and may be using it. Close them",
            };
            return $"{controllerName} is open in another program, so Thrum could not hide it " +
                "and is using it in shared mode; games may see it twice. " +
                $"{running}, then reconnect the controller.";
        }

        internal static IReadOnlyList<string> RunningKnownPrograms()
        {
            List<string> names = new List<string>();
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (InvalidOperationException)
            {
                return Array.Empty<string>();
            }

            foreach (Process process in processes)
            {
                try
                {
                    names.Add(process.ProcessName);
                }
                catch (InvalidOperationException)
                {
                    // Exited while listing.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return MatchKnownPrograms(names);
        }
    }
}

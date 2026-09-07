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

using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    /// <summary>Which mechanism starts the backend at logon.</summary>
    public enum ViiperAutostartKind
    {
        /// <summary>
        /// The <c>HKCU\...\Run</c> value <c>VIIPER</c> that
        /// <c>viiper.exe install</c> writes.
        /// </summary>
        RegistryRunValue,

        /// <summary>
        /// The <c>RunVIIPER</c> logon task the VIIPER setup script registers.
        /// </summary>
        ScheduledTask,
    }

    public sealed class ViiperAutostartEntry
    {
        public ViiperAutostartEntry(ViiperAutostartKind kind, string name,
            string target)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Target = target ?? string.Empty;
        }

        public ViiperAutostartKind Kind { get; }

        /// <summary>The registry value name, or the task name.</summary>
        public string Name { get; }

        /// <summary>What it launches, as recorded by whoever created it.</summary>
        public string Target { get; }

        public string Description => Kind == ViiperAutostartKind.RegistryRunValue
            ? "Startup registry entry \"" + Name + "\""
            : "Logon scheduled task \"" + Name + "\"";
    }

    public sealed class ViiperAutostartStatus
    {
        public ViiperAutostartStatus(IReadOnlyList<ViiperAutostartEntry> entries,
            string inspectionError = null)
        {
            Entries = entries ?? Array.Empty<ViiperAutostartEntry>();
            InspectionError = inspectionError;
        }

        public IReadOnlyList<ViiperAutostartEntry> Entries { get; }

        /// <summary>
        /// Set when a lookup could not be completed. An entry that could not be
        /// read is never reported as absent.
        /// </summary>
        public string InspectionError { get; }

        public bool Any => Entries.Count > 0;

        /// <summary>
        /// The one line the Settings page shows. States what exists, never what
        /// will be done about it — removal is a separate, explicit click.
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(InspectionError))
                {
                    return "VIIPER autostart could not be checked: " + InspectionError;
                }

                if (!Any)
                {
                    return "VIIPER does not start at logon. " +
                        ProductInfo.ProductName +
                        " starts the backend only when a profile needs it.";
                }

                return "VIIPER starts at logon: " +
                    string.Join(", ", Entries.Select(entry => entry.Description)) +
                    ". A backend already running at logon is one " +
                    ProductInfo.ProductName + " will not manage or stop.";
            }
        }
    }

    /// <summary>
    /// Reads and, only when explicitly asked, removes the two VIIPER autostart
    /// mechanisms. Split behind an interface so the detection logic can be
    /// tested against fakes rather than by writing autostart entries onto a
    /// real machine.
    /// </summary>
    public interface IViiperAutostartSource
    {
        /// <summary>The Run value's data, or null when the value is absent.</summary>
        string ReadRunValue();

        /// <summary>The logon task's action, or null when the task is absent.</summary>
        string ReadScheduledTask();

        void DeleteRunValue();

        void DeleteScheduledTask();
    }

    /// <summary>
    /// Detection and removal of VIIPER's own autostart entries.
    ///
    /// <para>These entries are shared ecosystem state: they belong to the
    /// VIIPER install, not to this application, and a user may have created
    /// them deliberately or through some other tool. So detection is read-only
    /// and unconditional, removal happens only through
    /// <see cref="Remove"/>, and nothing here is ever called from a startup or
    /// shutdown path.</para>
    ///
    /// <para>Why surface them at all: a backend started at logon is running
    /// before this application does, which means it is never ours, is never
    /// stopped on exit, and — because neither mechanism passes
    /// <c>--update-notify none</c> — nags about updates from the wrong
    /// repository. The Settings page explains that; the user decides.</para>
    /// </summary>
    public static class ViiperAutostart
    {
        /// <summary>
        /// The <c>Run</c> value name <c>viiper.exe install</c> writes
        /// (<c>internal/cmd/install_windows.go</c>).
        /// </summary>
        public const string RunValueName = "VIIPER";

        public const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// The logon task name the VIIPER setup script registers. Deliberately
        /// not derived from <see cref="ProductInfo"/>: it is the backend's
        /// name, and it is shared with any other VIIPER install.
        /// </summary>
        public const string ScheduledTaskName = "RunVIIPER";

        public static ViiperAutostartStatus Inspect(
            IViiperAutostartSource source = null)
        {
            source ??= new RegistryAndTaskSchedulerSource();

            List<ViiperAutostartEntry> entries = new List<ViiperAutostartEntry>();
            List<string> errors = new List<string>();

            try
            {
                string runValue = source.ReadRunValue();
                if (!string.IsNullOrWhiteSpace(runValue))
                {
                    entries.Add(new ViiperAutostartEntry(
                        ViiperAutostartKind.RegistryRunValue, RunValueName, runValue));
                }
            }
            catch (Exception ex)
            {
                errors.Add("registry: " + ex.Message);
            }

            try
            {
                string task = source.ReadScheduledTask();
                if (!string.IsNullOrWhiteSpace(task))
                {
                    entries.Add(new ViiperAutostartEntry(
                        ViiperAutostartKind.ScheduledTask, ScheduledTaskName, task));
                }
            }
            catch (Exception ex)
            {
                errors.Add("task scheduler: " + ex.Message);
            }

            return new ViiperAutostartStatus(entries,
                errors.Count == 0 ? null : string.Join("; ", errors));
        }

        /// <summary>
        /// Removes exactly the entries listed in <paramref name="entries"/> and
        /// nothing else. Only ever reached from an explicit user action.
        /// </summary>
        /// <returns>One line per attempt, for the log and for the UI.</returns>
        public static IReadOnlyList<string> Remove(
            IReadOnlyList<ViiperAutostartEntry> entries,
            IViiperAutostartSource source = null)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            source ??= new RegistryAndTaskSchedulerSource();
            List<string> outcomes = new List<string>();

            foreach (ViiperAutostartEntry entry in entries)
            {
                try
                {
                    if (entry.Kind == ViiperAutostartKind.RegistryRunValue)
                    {
                        source.DeleteRunValue();
                    }
                    else
                    {
                        source.DeleteScheduledTask();
                    }

                    outcomes.Add(entry.Description + ": removed.");
                }
                catch (Exception ex)
                {
                    outcomes.Add(entry.Description + ": could not be removed (" +
                        ex.Message + ").");
                }
            }

            return outcomes;
        }

        /// <summary>
        /// The live machine. Reads are per-user (HKCU) and per-user tasks;
        /// nothing here touches HKLM or the VIIPER install folder.
        /// </summary>
        private sealed class RegistryAndTaskSchedulerSource : IViiperAutostartSource
        {
            public string ReadRunValue()
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(RunValueName) as string;
            }

            public string ReadScheduledTask()
            {
                using TaskService service = new TaskService();
                using Task task = service.FindTask(ScheduledTaskName);
                if (task == null)
                {
                    return null;
                }

                ExecAction action = task.Definition.Actions
                    .OfType<ExecAction>().FirstOrDefault();
                return action == null
                    ? ScheduledTaskName
                    : (action.Path + " " + action.Arguments).Trim();
            }

            public void DeleteRunValue()
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    RunKeyPath, writable: true);
                key?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            public void DeleteScheduledTask()
            {
                using TaskService service = new TaskService();
                using Task task = service.FindTask(ScheduledTaskName);
                if (task != null)
                {
                    service.RootFolder.DeleteTask(ScheduledTaskName, exceptionOnNotExists: false);
                }
            }
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// The HidHide entries Thrum itself added, kept on disk.
    ///
    /// <para>HidHide's lists are system-wide and outlive Thrum. Thrum used to
    /// remember its own entries in memory only, so after a crash, a power cut
    /// or a forced exit the physical pad stayed hidden from every other
    /// program; and because it could not tell its entries from the user's, it
    /// also took over and later deleted entries the user had added in the
    /// HidHide client (a hidden device, or an application allowed to see
    /// hidden devices). With this record Thrum removes only what it added, and
    /// takes back what an earlier session left behind.</para>
    /// </summary>
    internal sealed class HidHideOwnershipRecord
    {
        internal const string FileName = "HidHideOwnership.txt";
        private const string BlacklistTag = "hidden-device";
        private const string WhitelistTag = "allowed-application";
        private const string CloakTag = "cloaking-before";

        private readonly string path;
        private readonly HashSet<string> hiddenDevices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> allowedApplications =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private HidHideOwnershipRecord(string path, bool existed)
        {
            this.path = path;
            Existed = existed;
        }

        /// <summary>
        /// False until this build has written a record. Entries left by older
        /// builds (which kept no record) are then treated as Thrum's once.
        /// </summary>
        internal bool Existed { get; private set; }

        internal IReadOnlyCollection<string> HiddenDevices => hiddenDevices;

        /// <summary>HidHide's cloaking state before Thrum turned it on, if it did.</summary>
        internal bool? CloakingBefore { get; private set; }

        internal static HidHideOwnershipRecord Load(string directory)
        {
            string file = string.IsNullOrEmpty(directory) ? null :
                Path.Combine(directory, FileName);
            HidHideOwnershipRecord record = new HidHideOwnershipRecord(file,
                file != null && File.Exists(file));
            if (!record.Existed)
            {
                return record;
            }

            try
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0)
                    {
                        continue;
                    }

                    string tag = line.Substring(0, tab);
                    string value = line.Substring(tab + 1).Trim();
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    if (tag == BlacklistTag)
                    {
                        record.hiddenDevices.Add(value);
                    }
                    else if (tag == WhitelistTag)
                    {
                        record.allowedApplications.Add(value);
                    }
                    else if (tag == CloakTag && bool.TryParse(value, out bool before))
                    {
                        record.CloakingBefore = before;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                // Unreadable: behave as if nothing was recorded.
            }

            return record;
        }

        internal bool OwnsHiddenDevice(string instanceId) =>
            hiddenDevices.Contains(instanceId);

        internal bool OwnsAllowedApplication(string dosPath) =>
            allowedApplications.Contains(dosPath);

        internal void AddHiddenDevice(string instanceId)
        {
            if (hiddenDevices.Add(instanceId))
            {
                Save();
            }
        }

        internal void RemoveHiddenDevices(IEnumerable<string> instanceIds)
        {
            bool changed = false;
            foreach (string id in instanceIds)
            {
                changed |= hiddenDevices.Remove(id);
            }

            if (changed)
            {
                Save();
            }
        }

        internal void AddAllowedApplication(string dosPath)
        {
            if (allowedApplications.Add(dosPath))
            {
                Save();
            }
        }

        internal void RemoveAllowedApplication(string dosPath)
        {
            if (allowedApplications.Remove(dosPath))
            {
                Save();
            }
        }

        internal void SetCloakingBefore(bool? before)
        {
            if (CloakingBefore != before)
            {
                CloakingBefore = before;
                Save();
            }
        }

        private void Save()
        {
            if (path == null)
            {
                return;
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine("# HidHide entries added by Thrum. Thrum removes only these;");
            text.AppendLine("# entries you add in the HidHide client are left alone.");
            foreach (string id in hiddenDevices.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                text.Append(BlacklistTag).Append('\t').AppendLine(id);
            }

            foreach (string app in allowedApplications.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                text.Append(WhitelistTag).Append('\t').AppendLine(app);
            }

            if (CloakingBefore.HasValue)
            {
                text.Append(CloakTag).Append('\t').AppendLine(CloakingBefore.Value.ToString());
            }

            try
            {
                SafeFileWriter.WriteAllText(path, text.ToString());
                Existed = true;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                AppLogger.LogToGui($"Could not save the record of Thrum's HidHide entries ({path}): {ex.Message}", true);
            }
        }
    }
}

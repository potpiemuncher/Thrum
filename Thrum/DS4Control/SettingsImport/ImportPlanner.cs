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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// Discovers an existing DS4Windows configuration and describes what
    /// importing it would do.
    ///
    /// <para>The product's data folder moved to <c>%APPDATA%\Thrum</c> with the
    /// rename, which leaves anyone upgrading from DS4Windows staring at an
    /// empty configuration. This planner finds the old folder and inventories
    /// it; <see cref="ImportExecutor"/> copies. The split exists so the
    /// question "is there anything to offer?" can be answered without touching
    /// a single file.</para>
    ///
    /// <para><b>Nothing here transforms content.</b> The profile and settings
    /// XML format is deliberately unchanged — both products write
    /// <c>&lt;DS4Windows&gt;</c> as the root element — and the loader already
    /// runs <c>ProfileMigration</c> and
    /// <c>OutContTypeCompatibility.Normalize</c> over whatever it reads. So an
    /// import is a file copy followed by the ordinary load path, and an older
    /// configuration migrates exactly as it would have in place. Adding a
    /// transform here would mean maintaining a second, divergent migration.
    /// </para>
    /// </summary>
    public sealed class ImportPlanner
    {
        /// <summary>
        /// The folder under <c>%APPDATA%</c> that an existing install uses.
        /// This is a <b>foreign</b> product's folder name and must never be
        /// derived from <see cref="ProductInfo"/> — it stays spelled out even
        /// after this product is renamed again, because it names what we are
        /// reading from, not what we are. Both the old ds4windowsapp lineage
        /// and the hbashton fork use this same folder.
        /// </summary>
        public const string LegacySourceFolderName = "DS4Windows";

        /// <summary>Sub-folder holding the individual profile files.</summary>
        public const string ProfilesFolderName = "Profiles";

        /// <summary>
        /// Written into the target folder when the user declines the offer, so
        /// the offer is made exactly once. Its presence is the whole protocol;
        /// the contents are a human-readable explanation for anyone who finds
        /// the file and wonders what it does.
        /// </summary>
        public const string DeclineMarkerFileName = "import-declined.txt";

        /// <summary>
        /// Application settings file. Also the file whose absence defines a
        /// pristine configuration, together with the auto-profile rules.
        /// </summary>
        public const string AppSettingsFileName = "Profiles.xml";

        public const string AutoProfilesFileName = "Auto Profiles.xml";

        /// <summary>
        /// The single-file items, in the order they are presented. Profiles are
        /// enumerated separately because there are many of them.
        /// </summary>
        private static readonly (ImportItemKind Kind, string FileName)[]
            SingleFileItems =
            {
                (ImportItemKind.AppSettings, AppSettingsFileName),
                (ImportItemKind.AutoProfiles, AutoProfilesFileName),
                (ImportItemKind.Actions, "Actions.xml"),
                (ImportItemKind.LinkedProfiles, "LinkedProfiles.xml"),
                (ImportItemKind.ControllerConfigs, "ControllerConfigs.xml"),
                (ImportItemKind.OutputSlots, "OutputSlots.xml"),
            };

        private readonly IImportFileSystem fileSystem;

        public ImportPlanner() : this(new PhysicalImportFileSystem())
        {
        }

        public ImportPlanner(IImportFileSystem fileSystem)
        {
            this.fileSystem = fileSystem ??
                throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// <c>%APPDATA%\DS4Windows</c>. Not cached: the tests never call it,
        /// and the application calls it once per launch.
        /// </summary>
        public static string DefaultSourceDirectory() =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                LegacySourceFolderName);

        /// <summary>
        /// Inventories <paramref name="sourceDirectory"/> and works out where
        /// each file would land under <paramref name="targetDirectory"/>.
        /// Never throws for an absent or unreadable source; that case is an
        /// empty plan, which the caller reads as "nothing to offer".
        /// </summary>
        public ImportPlan CreatePlan(string sourceDirectory,
            string targetDirectory)
        {
            var items = new List<ImportItem>();

            if (string.IsNullOrWhiteSpace(sourceDirectory) ||
                string.IsNullOrWhiteSpace(targetDirectory))
            {
                return new ImportPlan(sourceDirectory, targetDirectory,
                    sourceExists: false, items);
            }

            // A source that is also the target would plan every file as a
            // collision with itself. That can only happen through a
            // misconfiguration, and the honest answer is "nothing to do".
            if (SamePath(sourceDirectory, targetDirectory))
            {
                return new ImportPlan(sourceDirectory, targetDirectory,
                    sourceExists: true, items);
            }

            bool sourceExists;
            try
            {
                sourceExists = fileSystem.DirectoryExists(sourceDirectory);
                if (sourceExists)
                {
                    CollectSingleFileItems(sourceDirectory, targetDirectory,
                        items);
                    CollectProfileItems(sourceDirectory, targetDirectory,
                        items);
                }
            }
            catch (Exception)
            {
                // An unreadable source (permissions, a folder that vanished
                // mid-scan) is indistinguishable from no source at all as far
                // as the offer is concerned. Planning must not be able to take
                // startup down.
                return new ImportPlan(sourceDirectory, targetDirectory,
                    sourceExists: false, new List<ImportItem>());
            }

            return new ImportPlan(sourceDirectory, targetDirectory,
                sourceExists, items);
        }

        private void CollectSingleFileItems(string sourceDirectory,
            string targetDirectory, List<ImportItem> items)
        {
            foreach ((ImportItemKind kind, string fileName) in SingleFileItems)
            {
                string sourcePath = Path.Combine(sourceDirectory, fileName);
                if (!fileSystem.FileExists(sourcePath))
                {
                    continue;
                }

                string targetPath = Path.Combine(targetDirectory, fileName);
                items.Add(new ImportItem(kind, fileName, sourcePath,
                    targetPath, fileSystem.FileExists(targetPath)));
            }
        }

        private void CollectProfileItems(string sourceDirectory,
            string targetDirectory, List<ImportItem> items)
        {
            string sourceProfiles =
                Path.Combine(sourceDirectory, ProfilesFolderName);
            if (!fileSystem.DirectoryExists(sourceProfiles))
            {
                return;
            }

            string targetProfiles =
                Path.Combine(targetDirectory, ProfilesFolderName);

            // The search pattern is re-checked against the real extension:
            // Win32 pattern matching still honours 8.3 short names, so "*.xml"
            // can also return a file called "profile.xmlbackup".
            IEnumerable<string> profiles = fileSystem
                .EnumerateFiles(sourceProfiles, "*.xml")
                .Where(path => string.Equals(Path.GetExtension(path), ".xml",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path),
                    StringComparer.OrdinalIgnoreCase);

            foreach (string sourcePath in profiles)
            {
                string fileName = Path.GetFileName(sourcePath);
                string targetPath = Path.Combine(targetProfiles, fileName);
                items.Add(new ImportItem(ImportItemKind.Profile,
                    Path.Combine(ProfilesFolderName, fileName), sourcePath,
                    targetPath, fileSystem.FileExists(targetPath)));
            }
        }

        /// <summary>
        /// True when the target holds neither application settings nor
        /// auto-profile rules, i.e. nothing a user could lose. This is the
        /// gate on offering an import at all.
        ///
        /// <para>Call it <b>before</b> any first-run dialog runs. The
        /// save-location dialog's "Appdata" button writes a stub
        /// <c>Profiles.xml</c> through <c>Global.SaveDefault</c>, so asking
        /// afterwards would report a genuinely empty configuration as an
        /// existing one.</para>
        /// </summary>
        public bool IsTargetPristine(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return false;
            }

            try
            {
                return !fileSystem.FileExists(
                           Path.Combine(targetDirectory, AppSettingsFileName)) &&
                       !fileSystem.FileExists(
                           Path.Combine(targetDirectory, AutoProfilesFileName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the user has already turned the offer down. Any failure to
        /// read is treated as "declined": the cost of a missed offer is that
        /// the user imports from Settings later, while the cost of a repeated
        /// offer is a dialog that will not go away.
        /// </summary>
        public bool WasOfferDeclined(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return true;
            }

            try
            {
                return fileSystem.FileExists(
                    Path.Combine(targetDirectory, DeclineMarkerFileName));
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Records the decline. Best effort: if the marker cannot be written
        /// the user sees the offer again next launch, which is a nuisance and
        /// not a failure, so it must not surface as an error.
        /// </summary>
        /// <returns>Whether the marker was written.</returns>
        public bool RecordOfferDeclined(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return false;
            }

            try
            {
                fileSystem.CreateDirectory(targetDirectory);
                fileSystem.WriteAllText(
                    Path.Combine(targetDirectory, DeclineMarkerFileName),
                    "This file records that the one-time offer to import an " +
                    "existing " + LegacySourceFolderName + " configuration " +
                    "was declined." + Environment.NewLine +
                    "Delete it to be offered the import again on the next " +
                    "launch." + Environment.NewLine);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SamePath(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd('\\', '/'),
                    Path.GetFullPath(right).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(left, right,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

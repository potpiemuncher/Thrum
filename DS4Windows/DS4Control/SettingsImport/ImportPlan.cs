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

using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    /// <summary>
    /// The kinds of thing a configuration folder holds. One kind per file the
    /// application loads at startup, plus <see cref="Profile"/>, which is the
    /// only many-per-plan kind.
    /// </summary>
    public enum ImportItemKind
    {
        /// <summary><c>Profiles.xml</c> — the application settings and the
        /// per-controller profile assignments, despite the name.</summary>
        AppSettings,

        /// <summary>One file from the <c>Profiles</c> folder.</summary>
        Profile,

        /// <summary><c>Actions.xml</c> — special actions.</summary>
        Actions,

        /// <summary><c>LinkedProfiles.xml</c> — profile bound to a controller
        /// MAC address.</summary>
        LinkedProfiles,

        /// <summary><c>ControllerConfigs.xml</c> — per-controller device
        /// options.</summary>
        ControllerConfigs,

        /// <summary><c>Auto Profiles.xml</c> — the auto-profile rules.</summary>
        AutoProfiles,

        /// <summary><c>OutputSlots.xml</c> — permanent output slot
        /// layout.</summary>
        OutputSlots,
    }

    /// <summary>
    /// One file the import would copy. Immutable: a plan is a description, and
    /// the executor is the only thing that acts on it.
    /// </summary>
    public sealed class ImportItem
    {
        public ImportItem(ImportItemKind kind, string relativePath,
            string sourcePath, string targetPath, bool targetExists)
        {
            Kind = kind;
            RelativePath = relativePath;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            TargetExists = targetExists;
        }

        public ImportItemKind Kind { get; }

        /// <summary>
        /// Path relative to the configuration folder, e.g. <c>Profiles.xml</c>
        /// or <c>Profiles\Default.xml</c>. This is what gets logged and shown,
        /// so that neither the log nor the dialog has to carry a full user
        /// path.
        /// </summary>
        public string RelativePath { get; }

        public string SourcePath { get; }

        public string TargetPath { get; }

        /// <summary>
        /// Whether the destination already held a file when the plan was made.
        /// Collisions are skipped, never overwritten; the executor re-checks at
        /// copy time so a plan that has gone stale cannot clobber anything.
        /// </summary>
        public bool TargetExists { get; }
    }

    /// <summary>
    /// The result of inspecting an import source: what is there, where each
    /// piece would go, and which destinations are already occupied. Producing
    /// one touches nothing, so it is safe to build a plan in order to decide
    /// whether an offer is worth showing at all.
    /// </summary>
    public sealed class ImportPlan
    {
        public ImportPlan(string sourceDirectory, string targetDirectory,
            bool sourceExists, IReadOnlyList<ImportItem> items)
        {
            SourceDirectory = sourceDirectory;
            TargetDirectory = targetDirectory;
            SourceExists = sourceExists;
            Items = items ?? new ImportItem[0];
        }

        public string SourceDirectory { get; }

        public string TargetDirectory { get; }

        /// <summary>
        /// Whether the source folder itself exists. A missing source and a
        /// present-but-empty source both produce an empty plan; they are
        /// distinguished here only so the log can say which one happened.
        /// </summary>
        public bool SourceExists { get; }

        public IReadOnlyList<ImportItem> Items { get; }

        public bool IsEmpty => Items.Count == 0;

        public int ProfileCount =>
            Items.Count(item => item.Kind == ImportItemKind.Profile);

        public int CollisionCount => Items.Count(item => item.TargetExists);

        public bool Contains(ImportItemKind kind) =>
            Items.Any(item => item.Kind == kind);
    }
}

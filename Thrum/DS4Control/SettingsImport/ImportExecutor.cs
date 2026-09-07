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
    /// <summary>What happened to one planned item.</summary>
    public enum ImportItemOutcome
    {
        /// <summary>The file was copied into the target.</summary>
        Copied,

        /// <summary>
        /// The destination already existed and was left exactly as it was.
        /// Not an error: it is what makes a re-run after a partial import do
        /// only the remaining work.
        /// </summary>
        SkippedExisting,

        /// <summary>The copy failed. The source is unaffected.</summary>
        Failed,
    }

    public sealed class ImportItemResult
    {
        public ImportItemResult(ImportItem item, ImportItemOutcome outcome,
            string failureMessage = null)
        {
            Item = item;
            Outcome = outcome;
            FailureMessage = failureMessage;
        }

        public ImportItem Item { get; }

        public ImportItemOutcome Outcome { get; }

        /// <summary>Exception message for <see cref="ImportItemOutcome.Failed"/>,
        /// otherwise null.</summary>
        public string FailureMessage { get; }
    }

    /// <summary>
    /// Per-item outcomes plus the counts a caller needs to describe the result
    /// without walking the list.
    /// </summary>
    public sealed class ImportResult
    {
        public ImportResult(IReadOnlyList<ImportItemResult> items)
        {
            Items = items ?? new ImportItemResult[0];
        }

        public IReadOnlyList<ImportItemResult> Items { get; }

        public int CopiedCount => Count(ImportItemOutcome.Copied);

        public int SkippedCount => Count(ImportItemOutcome.SkippedExisting);

        public int FailedCount => Count(ImportItemOutcome.Failed);

        public bool AnyFailed => FailedCount > 0;

        public bool AnyCopied => CopiedCount > 0;

        /// <summary>
        /// Whether the target now holds this kind of file, whether this run put
        /// it there or found it already present. The startup path uses it for
        /// <see cref="ImportItemKind.AppSettings"/> to decide that the
        /// configuration is no longer a first run.
        /// </summary>
        public bool Landed(ImportItemKind kind) =>
            Items.Any(result => result.Item.Kind == kind &&
                result.Outcome != ImportItemOutcome.Failed);

        public IEnumerable<ImportItemResult> Failures =>
            Items.Where(result =>
                result.Outcome == ImportItemOutcome.Failed);

        private int Count(ImportItemOutcome outcome) =>
            Items.Count(result => result.Outcome == outcome);
    }

    /// <summary>
    /// Copies the files an <see cref="ImportPlan"/> describes.
    ///
    /// <para>Three rules, all of them load-bearing:</para>
    /// <list type="number">
    /// <item><description><b>The source is read-only.</b> The only write this
    /// class performs is a copy into the target. <see cref="IImportFileSystem"/>
    /// has no delete and no move, so this is enforced by the seam and not only
    /// by review.</description></item>
    /// <item><description><b>Collisions are skipped, never
    /// overwritten.</b> Existence is re-checked immediately before each copy,
    /// so a plan built minutes earlier cannot clobber a file created since.
    /// </description></item>
    /// <item><description><b>A failure never unwinds what already
    /// succeeded.</b> There is no rollback and no cleanup: a half-done import
    /// leaves a configuration the application can still load, and re-running
    /// finishes the job because the files that landed are now
    /// collisions.</description></item>
    /// </list>
    /// </summary>
    public sealed class ImportExecutor
    {
        private readonly IImportFileSystem fileSystem;

        public ImportExecutor() : this(new PhysicalImportFileSystem())
        {
        }

        public ImportExecutor(IImportFileSystem fileSystem)
        {
            this.fileSystem = fileSystem ??
                throw new ArgumentNullException(nameof(fileSystem));
        }

        public ImportResult Execute(ImportPlan plan)
        {
            var results = new List<ImportItemResult>();
            if (plan == null || plan.IsEmpty)
            {
                return new ImportResult(results);
            }

            foreach (ImportItem item in plan.Items)
            {
                results.Add(ExecuteItem(item));
            }

            return new ImportResult(results);
        }

        private ImportItemResult ExecuteItem(ImportItem item)
        {
            try
            {
                // Per item rather than once up front: a directory that cannot
                // be created belongs to the item that needed it, so the rest of
                // the plan still runs.
                string directory = Path.GetDirectoryName(item.TargetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    fileSystem.CreateDirectory(directory);
                }

                if (fileSystem.FileExists(item.TargetPath))
                {
                    return new ImportItemResult(item,
                        ImportItemOutcome.SkippedExisting);
                }

                fileSystem.CopyFile(item.SourcePath, item.TargetPath);
                return new ImportItemResult(item, ImportItemOutcome.Copied);
            }
            catch (Exception ex)
            {
                // Deliberately broad. Every failure mode here — permissions, a
                // locked file, a full disk, a path that grew too long, a source
                // deleted mid-import — has the same correct response: record it
                // and carry on with the next item.
                return new ImportItemResult(item, ImportItemOutcome.Failed,
                    ex.Message);
            }
        }
    }
}

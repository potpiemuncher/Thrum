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
using System.IO;

namespace DS4Windows
{
    /// <summary>
    /// The seam between the settings importer and the file system.
    ///
    /// It exists so the planner and the executor can be unit tested against
    /// temporary directories and against injected failures, and so the set of
    /// operations the importer is allowed to perform is written down in one
    /// place: <b>there is no delete and no move</b>. The importer reads the
    /// source and writes new files into the target; it can do nothing else,
    /// which is what makes "the source is strictly read-only" a property of the
    /// interface rather than a promise in a comment.
    /// </summary>
    public interface IImportFileSystem
    {
        bool DirectoryExists(string path);

        bool FileExists(string path);

        /// <summary>
        /// Files directly inside <paramref name="path"/> matching
        /// <paramref name="searchPattern"/>. Returns an empty sequence when the
        /// directory does not exist rather than throwing, because a missing
        /// <c>Profiles</c> folder is an ordinary shape for an import source.
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path, string searchPattern);

        void CreateDirectory(string path);

        /// <summary>
        /// Copies a file, never overwriting. Implementations must fail rather
        /// than clobber an existing destination: the executor's skip-if-exists
        /// policy checks first, and this is the backstop for the race between
        /// that check and the copy.
        /// </summary>
        void CopyFile(string sourcePath, string destinationPath);

        void WriteAllText(string path, string contents);

        string ReadAllText(string path);
    }

    /// <summary>
    /// The real file system. Every method is a thin delegation; anything
    /// clever belongs in the planner or the executor, where it can be tested.
    /// </summary>
    public sealed class PhysicalImportFileSystem : IImportFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public bool FileExists(string path) => File.Exists(path);

        public IEnumerable<string> EnumerateFiles(string path,
            string searchPattern)
        {
            if (!Directory.Exists(path))
            {
                return new string[0];
            }

            return Directory.EnumerateFiles(path, searchPattern,
                SearchOption.TopDirectoryOnly);
        }

        public void CreateDirectory(string path) =>
            Directory.CreateDirectory(path);

        public void CopyFile(string sourcePath, string destinationPath) =>
            File.Copy(sourcePath, destinationPath, overwrite: false);

        public void WriteAllText(string path, string contents) =>
            File.WriteAllText(path, contents);

        public string ReadAllText(string path) => File.ReadAllText(path);
    }
}

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
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// Writes settings files so that a crash, a killed process or a power cut
    /// leaves either the old file or the new one, never a truncated one, and
    /// keeps the previous version as <c>&lt;name&gt;.bak</c>.
    ///
    /// <para>Settings used to be written with <c>new StreamWriter(path,
    /// false)</c>, which empties the file first. Losing power during a save
    /// (it runs on every settings change and on exit) left an empty or partial
    /// Profiles.xml; the next launch silently used defaults and the next save
    /// overwrote what was left.</para>
    /// </summary>
    internal static class SafeFileWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void WriteAllText(string path, string contents)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            // The temporary name must not end in .xml: profile folders are
            // listed with "*.xml".
            string temporaryPath = Path.Combine(directory,
                "." + Path.GetFileName(fullPath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temporaryPath,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                    FileOptions.WriteThrough))
                {
                    byte[] bytes = Utf8NoBom.GetBytes(contents);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, fullPath, fullPath + ".bak",
                            ignoreMetadataErrors: true);
                    }
                    catch (IOException) when (File.Exists(temporaryPath))
                    {
                        // ReplaceFile is not available on every volume (some
                        // network shares and non-NTFS drives used for portable
                        // copies). Same result in two steps.
                        File.Copy(fullPath, fullPath + ".bak", overwrite: true);
                        File.Move(temporaryPath, fullPath, overwrite: true);
                    }
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// <summary>
    /// What to do when a settings file cannot be read: use the backup that
    /// <see cref="SafeFileWriter"/> keeps, set the damaged file aside so the
    /// next save cannot overwrite it, and tell the user once, in plain words,
    /// after the main window is up. A damaged Profiles.xml used to be logged
    /// only to the log file and replaced with defaults on the next save.
    /// </summary>
    internal static class SettingsFileRecovery
    {
        private static string pendingNotice;

        /// <summary>
        /// A message for the user, or null. Read once by the app after the
        /// main window is shown.
        /// </summary>
        public static string TakePendingNotice() =>
            Interlocked.Exchange(ref pendingNotice, null);

        /// <param name="path">The file that failed to load.</param>
        /// <param name="description">Plain name of the file, for example
        /// "Thrum's settings".</param>
        /// <param name="tryLoad">Loads a given path; false if it cannot be
        /// read.</param>
        /// <returns>True if the backup was loaded.</returns>
        public static bool Recover(string path, string description,
            Func<string, bool> tryLoad)
        {
            string backupPath = path + ".bak";
            string keptAs = SetAside(path);
            bool restored = false;
            if (File.Exists(backupPath))
            {
                try
                {
                    restored = tryLoad(backupPath);
                    if (restored)
                    {
                        File.Copy(backupPath, path, overwrite: true);
                    }
                }
                catch (Exception ex) when (ex is IOException ||
                    ex is UnauthorizedAccessException)
                {
                }
            }

            string kept = keptAs != null
                ? $" The damaged file was kept as {Path.GetFileName(keptAs)}."
                : string.Empty;
            string message = restored
                ? $"{description} could not be read, so the previous copy was restored. Changes made since the last save may be missing.{kept}"
                : $"{description} could not be read, so default settings are being used.{kept}";
            AppLogger.LogToGui(message, true);
            Interlocked.Exchange(ref pendingNotice,
                pendingNotice == null ? message : pendingNotice + "\n\n" + message);
            return restored;
        }

        private static string SetAside(string path)
        {
            try
            {
                string stem = path + ".damaged-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string target = stem;
                for (int attempt = 2; File.Exists(target); attempt++)
                {
                    target = stem + "-" + attempt.ToString(CultureInfo.InvariantCulture);
                }

                File.Move(path, target);
                return target;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}

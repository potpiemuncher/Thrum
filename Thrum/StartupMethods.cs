/*
DS4Windows
Copyright (C) 2023  Travis Nickles

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
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.TaskScheduler;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace DS4WinWPF
{
    /// <summary>
    /// Creates, repairs and removes this product's Windows startup entry, the
    /// Startup-folder shortcut, and removes the elevated logon scheduled task
    /// that the former "Task" startup mode created.
    ///
    /// <para>That mode was removed: it started the app with administrator
    /// rights at every sign-in from a batch file in the program folder, which
    /// the user (and anything running as the user) can edit.</para>
    ///
    /// <para><b>Every name here comes from <c>ProductInfo</c>, and that is a
    /// safety property, not tidiness.</b> A user of this product very likely
    /// also has a real DS4Windows install with its own <c>RunDS4Windows</c>
    /// task and <c>DS4Windows.lnk</c> shortcut. Several paths below delete
    /// startup entries — removing the old task, repairing a moved executable,
    /// turning the option off — and none of them may be able
    /// to name an entry we did not create. <c>StartupEntryIdentityTests</c>
    /// asserts that the inherited names appear nowhere in the compiled
    /// application at all.</para>
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public static class StartupMethods
    {
        public static string lnkpath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + DS4Windows.ProductInfo.StartupShortcutName;

        /// <summary>
        /// The one spelling of the path of the helper batch file the old logon
        /// task ran, so removing the task removes the file too.
        ///
        /// <para>It used to be spelled three ways, which disagree at a drive
        /// root: <see cref="DS4Windows.Global.exedirpath"/> comes from
        /// <c>DirectoryInfo.FullName</c>, which keeps its trailing separator
        /// there, so <c>$@"{dir}\task.bat"</c> yields <c>C:\\task.bat</c> where
        /// <see cref="Path.Combine"/> yields <c>C:\task.bat</c>.</para>
        /// </summary>
        internal static string TaskBatPath { get; } =
            Path.Combine(DS4Windows.Global.exedirpath, "task.bat");

        public static bool HasStartProgEntry()
        {
            // Exception handling should not be needed here. Method handles most cases.
            // Deliberately the same path the writer and the deleter use: a
            // second spelling of it is how the settings page once ended up
            // checking for a different file than it created.
            bool exists = File.Exists(lnkpath);
            return exists;
        }

        public static bool HasTaskEntry()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.FindTask(DS4Windows.ProductInfo.StartupTaskName);
            return tasker != null;
        }

        public static void WriteStartProgEntry()
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            try
            {
                var lnk = shell.CreateShortcut(lnkpath);
                try
                {
                    string app = DS4Windows.Global.exelocation;
                    lnk.TargetPath = DS4Windows.Global.exelocation;
                    lnk.Arguments = "-m";
                    // Need to add the DS4Windows directory as cwd or
                    // language assemblies cannot be discovered
                    lnk.WorkingDirectory = DS4Windows.Global.exedirpath;

                    //lnk.TargetPath = Assembly.GetExecutingAssembly().Location;
                    //lnk.Arguments = "-m";
                    lnk.IconLocation = app.Replace('\\', '/');
                    lnk.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static void DeleteStartProgEntry()
        {
            if (File.Exists(lnkpath) && !new FileInfo(lnkpath).IsReadOnly)
            {
                File.Delete(lnkpath);
            }
        }

        public static bool CanWriteStartEntry()
        {
            bool result = false;
            if (!new FileInfo(lnkpath).IsReadOnly)
            {
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Removes the logon task <i>and</i> the helper batch file it ran.
        ///
        /// <para>The batch file is ours — older versions wrote it and nothing
        /// else creates it — so leaving it behind leaves an executable launcher
        /// in the install folder that no longer belongs to any setting. It also
        /// becomes residue the uninstall audit would have to explain.</para>
        /// </summary>
        public static void DeleteTaskEntry()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.FindTask(DS4Windows.ProductInfo.StartupTaskName);
            if (tasker != null)
            {
                ts.RootFolder.DeleteTask(DS4Windows.ProductInfo.StartupTaskName);
            }

            DeleteTaskBat(TaskBatPath);
        }

        /// <summary>
        /// <see cref="DeleteTaskEntry"/>, reporting failure instead of
        /// throwing. The task was registered with administrator rights, so a
        /// Thrum that is not elevated usually cannot delete it.
        /// </summary>
        public static bool TryDeleteTaskEntry()
        {
            try
            {
                DeleteTaskEntry();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes the helper batch file, reporting whether the file is gone
        /// rather than whether a delete happened — already-absent is success.
        ///
        /// <para><b>This never throws.</b> It runs on the path where the user
        /// switches startup off, and every caller of that path treats it as
        /// having succeeded. The file can legitimately be unavailable: the
        /// logon task may be executing it right now (cmd.exe holds it open),
        /// the install folder may be read-only, or a previous run may have
        /// removed it already. None of those should turn "turn startup off"
        /// into an error dialog — the scheduled task, which is the part that
        /// actually causes the app to launch, has already been removed by the
        /// time this runs.</para>
        /// </summary>
        internal static bool DeleteTaskBat(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // Cannot name a file, so cannot report one as gone.
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return true;
                }

                FileInfo info = new FileInfo(path);
                if (info.IsReadOnly)
                {
                    // We wrote this file, so a read-only attribute on it is our
                    // own residue to clear rather than a user preference to
                    // respect. DeleteStartProgEntry leaves read-only shortcuts
                    // alone because the user may have pinned one deliberately;
                    // nobody deliberately pins a generated batch file.
                    info.IsReadOnly = false;
                }

                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                // Deliberately broad, and deliberately silent. IOException
                // (locked), UnauthorizedAccessException (permissions) and the
                // directory-not-found family are all reachable here, and the
                // response to every one of them is the same: leave the file and
                // carry on disabling startup.
                return false;
            }
        }

        public static bool CheckStartupExeLocation()
        {
            string lnkprogpath = ResolveShortcut(lnkpath);
            return lnkprogpath != DS4Windows.Global.exelocation;
        }

        private static string ResolveShortcut(string filePath)
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            string result;

            try
            {
                var shortcut = shell.CreateShortcut(filePath);
                result = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
            }
            catch (COMException)
            {
                // A COMException is thrown if the file is not a valid shortcut (.lnk) file 
                result = null;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }

            return result;
        }
    }
}

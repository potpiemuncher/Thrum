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
    /// Creates, repairs and removes this product's two Windows startup
    /// entries: the elevated logon scheduled task and the Startup-folder
    /// shortcut.
    ///
    /// <para><b>Every name here comes from <c>ProductInfo</c>, and that is a
    /// safety property, not tidiness.</b> A user of this product very likely
    /// also has a real DS4Windows install with its own <c>RunDS4Windows</c>
    /// task and <c>DS4Windows.lnk</c> shortcut. Several paths below delete
    /// startup entries — switching between task and shortcut, repairing a
    /// moved executable, turning the option off — and none of them may be able
    /// to name an entry we did not create. <c>StartupEntryIdentityTests</c>
    /// asserts that the inherited names appear nowhere in the compiled
    /// application at all.</para>
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public static class StartupMethods
    {
        public static string lnkpath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + DS4Windows.ProductInfo.StartupShortcutName;
        private static string taskBatPath = Path.Combine(DS4Windows.Global.exedirpath, "task.bat");

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

        /// <summary>
        /// Repairs <b>our own</b> logon task when it points somewhere other
        /// than the current <c>task.bat</c> — typically after the application
        /// was moved. "Old" refers to a stale task of ours, not to a task
        /// belonging to the product this one was forked from: the lookup is
        /// <see cref="DS4Windows.ProductInfo.StartupTaskName"/> and must stay
        /// that way.
        /// </summary>
        public static void DeleteOldTaskEntry()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.FindTask(DS4Windows.ProductInfo.StartupTaskName);
            if (tasker != null)
            {
                foreach(Microsoft.Win32.TaskScheduler.Action act in tasker.Definition.Actions)
                {
                    if (act.ActionType == TaskActionType.Execute)
                    {
                        ExecAction temp = act as ExecAction;
                        if (temp.Path != taskBatPath)
                        {
                            ts.RootFolder.DeleteTask(DS4Windows.ProductInfo.StartupTaskName);
                            break;
                        }
                    }
                }
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

        public static void WriteTaskEntry()
        {
            DeleteTaskEntry();

            // Create new version of task.bat file using current exe
            // filename. Allow dynamic file
            RefreshTaskBat();

            TaskService ts = new TaskService();
            TaskDefinition td = ts.NewTask();
            td.Triggers.Add(new LogonTrigger());
            string dir = DS4Windows.Global.exedirpath;
            td.Actions.Add(new ExecAction($@"{dir}\task.bat",
                "",
                dir));

            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.DisallowStartIfOnBatteries = false;
            ts.RootFolder.RegisterTaskDefinition(DS4Windows.ProductInfo.StartupTaskName, td);
        }

        public static void DeleteTaskEntry()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.FindTask(DS4Windows.ProductInfo.StartupTaskName);
            if (tasker != null)
            {
                ts.RootFolder.DeleteTask(DS4Windows.ProductInfo.StartupTaskName);
            }
        }

        public static bool CheckStartupExeLocation()
        {
            string lnkprogpath = ResolveShortcut(lnkpath);
            return lnkprogpath != DS4Windows.Global.exelocation;
        }

        public static void LaunchOldTask()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.FindTask(DS4Windows.ProductInfo.StartupTaskName);
            if (tasker != null)
            {
                tasker.Run("");
            }
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

        private static void RefreshTaskBat()
        {
            string dir = DS4Windows.Global.exedirpath;
            string path = $@"{dir}\task.bat";
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using (StreamWriter w = new StreamWriter(fileStream))
            {
                string temp = string.Empty;
                w.WriteLine("@echo off"); // Turn off echo
                w.WriteLine("SET mypath=\"%~dp0\"");
                temp = $"cmd.exe /c start \"{DS4Windows.ProductInfo.StartupTaskName}\" %mypath%\\{DS4Windows.Global.exeFileName} -m";
                w.WriteLine(temp);
                w.WriteLine("exit");
            }
        }
    }
}

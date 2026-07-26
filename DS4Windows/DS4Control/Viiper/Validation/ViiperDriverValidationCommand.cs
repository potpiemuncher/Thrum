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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DS4Windows
{
    /// <summary>
    /// Implements the <c>-viiperdriverdiagnostic</c> switch: a read-only smoke test of
    /// the VIIPER driver identity/trust diagnostic against whatever usbip-win2
    /// install is on the machine.
    ///
    /// Read-only with respect to the controller, driver, and system. It never
    /// releases or suppresses the physical controller, never requests elevation, never runs a usbip attach, never
    /// starts the helper, the USB/IP server, or the ControlService, and never
    /// writes a setting or touches the driver. It only reads device, driver, and
    /// file state, and it runs as a normal user (the report states whether it ran
    /// elevated so a restricted read is visible instead of being papered over).
    /// </summary>
    public static class ViiperDriverValidationCommand
    {
        /// <summary>Validation passed.</summary>
        public const int ExitCodePassed = 0;

        /// <summary>Validation failed; VIIPER would refuse to start.</summary>
        public const int ExitCodeFailed = 1;

        /// <summary>The diagnostic itself could not run.</summary>
        public const int ExitCodeError = 2;

        private const int AttachParentProcess = -1;
        private const string ReportDirectoryName = ProductInfo.TempFolderName;
        private const string ReportFilePrefix = "viiper-driver-validation-";
        private const string WindowTitle = ProductInfo.DiagnosticWindowTitle;

        /// <summary>
        /// Runs the diagnostic and returns the process exit code: 0 when
        /// validation passed, non-zero otherwise, so the command is scriptable.
        /// </summary>
        public static int Run()
        {
            bool console = TryAttachParentConsole();

            string text;
            int exitCode;
            try
            {
                exitCode = BuildReport(out text);
            }
            catch (Exception ex)
            {
                text = ProductInfo.ProductName +
                    " VIIPER driver diagnostic could not run: " + ex.Message;
                exitCode = ExitCodeError;
            }

            Emit(text, console);
            return exitCode;
        }

        private static int BuildReport(out string text)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string usbipPath = ResolveUsbipExecutablePath();
            string reportPath = BuildReportFilePath(now);

            ViiperDriverValidationReport report =
                ViiperDriverGate.Default.Inspect(usbipPath);

            var context = new ViiperDriverReportContext
            {
                TimestampUtc = now,
                AppVersion = ReadAppVersion(),
                OsVersion = Environment.OSVersion.VersionString,
                ProcessArchitecture =
                    RuntimeInformation.ProcessArchitecture.ToString(),
                Elevated = ReadElevated(),
                UsbipExecutablePath =
                    ViiperDriverReportFormatter.RedactUserPath(usbipPath),
                ReportFilePath = DisplayReportPath(reportPath),
            };

            text = ViiperDriverReportFormatter.Format(report, context);
            string writeError = TryWriteReport(reportPath, text);
            if (writeError != null)
            {
                text += Environment.NewLine +
                    "  The report could not be saved to " +
                    DisplayReportPath(reportPath) + ": " + writeError +
                    Environment.NewLine;
            }

            return report.Result != null && report.Result.Passed
                ? ExitCodePassed
                : ExitCodeFailed;
        }

        private static string ResolveUsbipExecutablePath()
        {
            return ResolveUsbipExecutablePath(
                Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                File.Exists);
        }

        /// <summary>
        /// Mirrors VIIPER's runtime lookup: PATH, Program Files, then Program
        /// Files (x86). When usbip.exe is absent, returns the canonical expected
        /// Program Files path so the report identifies the missing component.
        /// </summary>
        internal static string ResolveUsbipExecutablePath(string pathValue,
            string programFiles, string programFilesX86,
            Func<string, bool> fileExists)
        {
            if (fileExists == null)
                throw new ArgumentNullException(nameof(fileExists));

            foreach (string folder in (pathValue ?? string.Empty)
                .Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                string candidate = Path.Combine(folder.Trim(), "usbip.exe");
                if (fileExists(candidate))
                    return candidate;
            }

            string[] candidates =
            {
                BuildProgramFilesCandidate(programFiles),
                BuildProgramFilesCandidate(programFilesX86),
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && fileExists(candidate))
                    return candidate;
            }

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }

            return "usbip.exe";
        }

        private static string BuildProgramFilesCandidate(string root)
        {
            return string.IsNullOrWhiteSpace(root)
                ? null
                : Path.Combine(root, "USBip", "usbip.exe");
        }

        private static bool ReadElevated()
        {
            try
            {
                return Global.IsAdministrator();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ReadAppVersion()
        {
            try
            {
                return Global.exeversion;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string BuildReportFilePath(DateTimeOffset timestamp)
        {
            string fileName = ReportFilePrefix +
                timestamp.ToUniversalTime().ToString("yyyyMMdd-HHmmss") +
                "Z.txt";
            return Path.Combine(Path.GetTempPath(), ReportDirectoryName, fileName);
        }

        /// <summary>
        /// Report location in a form a tester can paste into a bug report:
        /// %TEMP% expands in Explorer and in a shell, and carries no user name.
        /// </summary>
        private static string DisplayReportPath(string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                return null;

            string temp = Path.GetTempPath();
            if (!string.IsNullOrWhiteSpace(temp) && reportPath.StartsWith(temp,
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine("%TEMP%",
                    reportPath.Substring(temp.Length).TrimStart('\\'));
            }

            return ViiperDriverReportFormatter.RedactUserPath(reportPath);
        }

        /// <summary>Returns null on success, or the failure message.</summary>
        private static string TryWriteReport(string reportPath, string text)
        {
            try
            {
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(reportPath, text);
                return null;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is NotSupportedException ||
                ex is ArgumentException)
            {
                return ex.Message;
            }
        }

        private static void Emit(string text, bool console)
        {
            if (console)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine(text);
                    Console.Out.Flush();
                    return;
                }
                catch (IOException)
                {
                    // The attached console went away; fall back to the dialog.
                }
            }

            if (Environment.UserInteractive &&
                Process.GetCurrentProcess().SessionId != 0)
            {
                ShowReportWindow(text);
            }
        }

        /// <summary>
        /// DS4Windows is a WPF application with no console of its own, so a
        /// GUI-launched run gets the same text in a selectable, scrollable window
        /// it can be copied out of.
        /// </summary>
        private static void ShowReportWindow(string text)
        {
            try
            {
                var view = new System.Windows.Controls.TextBox
                {
                    Text = text,
                    IsReadOnly = true,
                    IsReadOnlyCaretVisible = true,
                    AcceptsReturn = true,
                    TextWrapping = System.Windows.TextWrapping.NoWrap,
                    VerticalScrollBarVisibility =
                        System.Windows.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility =
                        System.Windows.Controls.ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Margin = new System.Windows.Thickness(4),
                };

                var window = new System.Windows.Window
                {
                    Title = WindowTitle,
                    Width = 940,
                    Height = 620,
                    WindowStartupLocation =
                        System.Windows.WindowStartupLocation.CenterScreen,
                    Content = view,
                };
                window.ShowDialog();
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(text, WindowTitle,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Attaches to the console of the launching terminal, if any, and rebinds
        /// stdout to it. Returns false when the process has no parent console.
        /// </summary>
        private static bool TryAttachParentConsole()
        {
            try
            {
                if (!AttachConsole(AttachParentProcess))
                    return false;

                var output = new StreamWriter(Console.OpenStandardOutput())
                {
                    AutoFlush = true,
                };
                Console.SetOut(output);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(int processId);
    }
}

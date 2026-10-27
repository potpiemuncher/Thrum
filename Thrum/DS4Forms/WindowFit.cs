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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Keeps a window's minimum and initial size within the work area of the
    /// monitor it opens on. At 175-200% scaling a 1080p screen is only about
    /// 520-590 pixels tall to WPF, and windows with a larger MinHeight pushed
    /// their bottom row (the main window's Start/Stop footer, the first-run
    /// wizard's Next button) below the screen edge, even maximized.
    /// </summary>
    internal static class WindowFit
    {
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public static void ClampToWorkArea(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                PresentationSource source = PresentationSource.FromVisual(window);
                if (handle == IntPtr.Zero || source?.CompositionTarget == null)
                {
                    return;
                }

                MONITORINFO info = new MONITORINFO
                {
                    cbSize = Marshal.SizeOf<MONITORINFO>(),
                };
                IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
                if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                {
                    return;
                }

                Matrix toDip = source.CompositionTarget.TransformFromDevice;
                double workWidth = (info.rcWork.Right - info.rcWork.Left) * toDip.M11;
                double workHeight = (info.rcWork.Bottom - info.rcWork.Top) * toDip.M22;
                if (workWidth <= 0 || workHeight <= 0)
                {
                    return;
                }

                window.MinWidth = Math.Min(window.MinWidth, workWidth);
                window.MinHeight = Math.Min(window.MinHeight, workHeight);
                if (window.WindowState == WindowState.Normal)
                {
                    if (window.Width > workWidth)
                    {
                        window.Width = workWidth;
                    }

                    if (window.Height > workHeight)
                    {
                        window.Height = workHeight;
                    }
                }
            }
            catch (Exception)
            {
                // Sizing is cosmetic; never let it stop a window opening.
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}

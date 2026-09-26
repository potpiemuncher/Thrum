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
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DS4Windows
{
    /// <summary>
    /// Counts changes of the Windows default playback device.
    ///
    /// <para>A loopback capture opened on "the default device" stays on the
    /// device that was default when it opened. When Windows switches output
    /// (a headset connects, the user picks another device in the volume
    /// flyout) the old device is still present, so the capture neither fails
    /// nor rebinds: it records a device nothing plays to, and whatever it
    /// feeds goes silent while its status still reads active. Consumers keep
    /// the <see cref="Generation"/> they opened at and reopen when it
    /// moves.</para>
    ///
    /// <para>The callback only increments a counter. Windows calls it on its
    /// own thread, and doing COM work or taking locks there can deadlock the
    /// audio service.</para>
    /// </summary>
    internal static class DefaultRenderEndpointWatcher
    {
        private static readonly object registrationLock = new object();
        private static MMDeviceEnumerator enumerator;
        private static NotificationClient client;
        private static int registered;
        private static int generation;

        /// <summary>
        /// Changes every time the default playback device for the Multimedia
        /// role changes. Starts watching on first use.
        /// </summary>
        public static int Generation
        {
            get
            {
                EnsureRegistered();
                return Volatile.Read(ref generation);
            }
        }

        private static void EnsureRegistered()
        {
            if (Volatile.Read(ref registered) != 0)
            {
                return;
            }

            lock (registrationLock)
            {
                if (registered != 0)
                {
                    return;
                }

                try
                {
                    // Both are kept for the life of the process: the
                    // registration lives on this enumerator, and Windows
                    // holds only a COM reference to the client.
                    enumerator = new MMDeviceEnumerator();
                    client = new NotificationClient();
                    enumerator.RegisterEndpointNotificationCallback(client);
                }
                catch (Exception exception)
                {
                    // Without the audio service there is nothing to follow;
                    // captures then behave as they did before.
                    AppLogger.LogToGui(
                        $"Could not watch for changes of the default audio output: {exception.Message}",
                        false);
                }

                Volatile.Write(ref registered, 1);
            }
        }

        private sealed class NotificationClient : IMMNotificationClient
        {
            public void OnDefaultDeviceChanged(DataFlow flow, Role role,
                string defaultDeviceId)
            {
                if (flow == DataFlow.Render && role == Role.Multimedia)
                {
                    Interlocked.Increment(ref generation);
                }
            }

            public void OnDeviceStateChanged(string deviceId,
                DeviceState newState)
            {
            }

            public void OnDeviceAdded(string pwstrDeviceId)
            {
            }

            public void OnDeviceRemoved(string deviceId)
            {
            }

            public void OnPropertyValueChanged(string pwstrDeviceId,
                PropertyKey key)
            {
            }
        }
    }
}

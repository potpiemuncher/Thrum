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
using System.IO;

namespace DS4Windows
{
    /// <summary>Why a queued restart did or did not start a replacement.</summary>
    public enum ViiperRestartLaunchOutcome
    {
        /// <summary>Nothing asked for a restart.</summary>
        NotRequested,

        /// <summary>
        /// The single-instance handle has not been released yet. Launching now
        /// is the defect this class exists to prevent.
        /// </summary>
        SingleInstanceStillHeld,

        /// <summary>A replacement was already started for this request.</summary>
        AlreadyLaunched,

        /// <summary>The replacement was started.</summary>
        Launched,

        /// <summary>Starting the replacement threw.</summary>
        LaunchFailed,
    }

    /// <summary>
    /// A restart that has been asked for but must not happen yet.
    ///
    /// <para><b>The defect this fixes</b>
    /// (<a href="https://github.com/potpiemuncher/Thrum/issues/12">issue
    /// #12</a>). The inherited implementation started the replacement process
    /// and only then asked the dispatcher to shut down. The replacement reaches
    /// startup in a few hundred milliseconds; the original is still inside an
    /// up-to-eight-second controller teardown and still owns the named
    /// single-instance event. So the replacement found an instance already
    /// running, signalled it, and exited — and the original then finished
    /// exiting too. The user was left with nothing running, right after the
    /// install whose purpose was to make virtual controllers work. In this tree
    /// it is worse: the shutdown also stops the VIIPER backend this process
    /// owns, so the end state is no application and no backend.</para>
    ///
    /// <para><b>The fix is an ordering, so the ordering is enforced rather than
    /// commented.</b> A request only records intent.
    /// <see cref="Launch"/> refuses to start anything until
    /// <see cref="MarkSingleInstanceReleased"/> has been called, and that call
    /// lives at exactly one place: immediately after the shutdown path closes
    /// the single-instance event. A future edit that moves the launch earlier
    /// does not reintroduce the race, it fails and says why.</para>
    ///
    /// <para><b>What happens to the backend across the restart.</b> Deliberately
    /// nothing special: the ordinary stop-on-exit policy from plan task 2.4b
    /// runs, the owned backend is stopped with the rest of the shutdown, and
    /// the replacement starts a fresh one on demand when a profile needs it.
    /// The alternative — exempting an install-driven restart from stop-on-exit —
    /// would leave a backend running that the new instance does not own and
    /// therefore would never stop, converting a temporary special case into a
    /// permanent orphan. A few hundred milliseconds of backend downtime during
    /// a restart nobody is playing through is the cheaper side of that
    /// trade.</para>
    /// </summary>
    public sealed class PendingApplicationRestart
    {
        private readonly object gate = new object();
        private string executablePath;
        private bool singleInstanceReleased;
        private bool launched;

        /// <summary>The instance the application's shutdown path drains.</summary>
        public static PendingApplicationRestart Current { get; } =
            new PendingApplicationRestart();

        /// <summary>True once a restart has been asked for and not yet run.</summary>
        public bool IsRequested
        {
            get { lock (gate) { return executablePath != null && !launched; } }
        }

        /// <summary>The executable a launch would start, or null.</summary>
        public string RequestedExecutable
        {
            get { lock (gate) { return executablePath; } }
        }

        /// <summary>
        /// Records that the application should be replaced by a fresh instance
        /// once this one has finished shutting down.
        /// </summary>
        /// <param name="exePath">
        /// The executable to start. Callers pass <c>Global.exelocation</c> — the
        /// executable actually running — rather than a composed
        /// <c>&lt;product&gt;.exe</c>, so a renamed or relocated copy still
        /// restarts itself.
        /// </param>
        /// <param name="fileExists">Test seam; defaults to the file system.</param>
        /// <returns>False when there is nothing runnable to queue.</returns>
        public bool Request(string exePath, Func<string, bool> fileExists = null)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return false;
            }

            fileExists ??= File.Exists;
            if (!fileExists(exePath))
            {
                return false;
            }

            lock (gate)
            {
                executablePath = exePath;
                launched = false;
                return true;
            }
        }

        /// <summary>
        /// Called once the named single-instance event has been closed and the
        /// shutdown is complete. Until this runs, a replacement would see this
        /// process as the running instance and exit immediately.
        /// </summary>
        public void MarkSingleInstanceReleased()
        {
            lock (gate) { singleInstanceReleased = true; }
        }

        /// <summary>
        /// Starts the queued replacement, if the ordering allows it.
        /// </summary>
        /// <param name="start">
        /// Starts the process. Injected so the ordering can be tested without
        /// spawning anything.
        /// </param>
        /// <param name="log">Receives one line describing the outcome.</param>
        public ViiperRestartLaunchOutcome Launch(Action<string> start,
            Action<string> log = null)
        {
            string path;
            lock (gate)
            {
                if (executablePath == null)
                {
                    return ViiperRestartLaunchOutcome.NotRequested;
                }

                if (launched)
                {
                    log?.Invoke("A replacement " + ProductInfo.ProductName +
                        " instance was already started; not starting another.");
                    return ViiperRestartLaunchOutcome.AlreadyLaunched;
                }

                if (!singleInstanceReleased)
                {
                    log?.Invoke("Restart of " + ProductInfo.ProductName +
                        " was skipped: the single-instance handle is still " +
                        "held, so a replacement would exit immediately.");
                    return ViiperRestartLaunchOutcome.SingleInstanceStillHeld;
                }

                launched = true;
                path = executablePath;
            }

            try
            {
                start(path);
                log?.Invoke("Restarted " + ProductInfo.ProductName +
                    " after VIIPER setup. The backend is started again on " +
                    "demand by the new instance.");
                return ViiperRestartLaunchOutcome.Launched;
            }
            catch (Exception ex)
            {
                log?.Invoke("Could not restart " + ProductInfo.ProductName +
                    " after VIIPER setup: " + ex.Message);
                return ViiperRestartLaunchOutcome.LaunchFailed;
            }
        }
    }
}

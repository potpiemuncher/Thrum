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

namespace DS4Windows
{
    /// <summary>
    /// The single source of truth for every product-identity string the
    /// application presents to Windows, to itself, or to the network.
    ///
    /// Nothing in here is a preference: each member is coupled to something
    /// outside the C# source (the csproj, a WPF resource URI, a kernel object
    /// name, a scheduled task, a GitHub repository), and each XML doc below
    /// states what breaks when the value stops matching its consumer. A rebrand
    /// edits this file; call sites are not supposed to hold literals.
    ///
    /// Every member is a compile-time constant so that the existing
    /// <see cref="Global"/> constants can keep delegating here without changing
    /// their <c>const</c>-ness, and so that the composed values (resource
    /// prefixes, IPC names, URLs) cannot drift apart from their parts.
    ///
    /// Guard tests in <c>DS4WindowsTests/ProductIdentityTests.cs</c> assert the
    /// couplings that a compiler cannot: notably that
    /// <see cref="ExeBaseName"/> equals the app assembly's real name, and that
    /// every pack URI built from these prefixes actually resolves.
    /// </summary>
    public static class ProductInfo
    {
        /// <summary>
        /// The product's display name. Used as the caption of message boxes and
        /// of the diagnostic report window, and as the HTTP <c>User-Agent</c>.
        /// Cosmetic on its own, but it is the root of the composed IPC object
        /// names below, so changing it renames those kernel objects too.
        /// </summary>
        public const string ProductName = "Thrum";

        /// <summary>
        /// The main window's title bar text. The <c>-command</c> client process
        /// locates the already-running instance with
        /// <c>FindWindow(className, WindowTitle)</c>, so this must match the
        /// running window's actual <c>Title</c> exactly or every command-line
        /// IPC call silently no-ops. <c>MainWindow</c> assigns its
        /// <c>Title</c> from this member for that reason.
        /// </summary>
        public const string WindowTitle = ProductName;

        /// <summary>
        /// The assembly name of the application executable, i.e. the csproj
        /// <c>AssemblyName</c> without the extension. Every WPF pack URI in the
        /// tree names this assembly; if it stops matching <c>AssemblyName</c>,
        /// all resource lookups (icons, controller artwork, themes) throw at
        /// runtime and the satellite language assemblies stop resolving. This
        /// is the value the reflection guard test pins to the real assembly.
        /// </summary>
        public const string ExeBaseName = "Thrum";

        /// <summary>
        /// Lower-case form of <see cref="ExeBaseName"/>, for the places that
        /// need a case-insensitive token in a case-sensitive context: the
        /// self-invoked command-line switches. It has to be spelled out because
        /// <c>ToLowerInvariant()</c> is not constant-foldable; a guard test
        /// asserts the two stay in step.
        /// </summary>
        public const string ExeBaseNameLowerInvariant = "thrum";

        /// <summary>
        /// Folder name under <c>%APPDATA%</c> holding profiles, actions, linked
        /// profiles, auto profiles and output-slot state. Changing it points the
        /// app at an empty configuration; existing users need an import path.
        /// </summary>
        public const string AppDataFolderName = ProductName;

        /// <summary>
        /// Folder name under <c>%LOCALAPPDATA%</c>. Same consequence as
        /// <see cref="AppDataFolderName"/>. Note this is the app's own folder;
        /// the VIIPER backend keeps its separate <c>%LOCALAPPDATA%\VIIPER</c>
        /// folder, which is shared ecosystem state and must not be renamed.
        /// </summary>
        public const string LocalAppDataFolderName = ProductName;

        /// <summary>
        /// Folder name created under <c>%TEMP%</c> for diagnostic reports.
        /// Purely a scratch location; a mismatch only strands old reports.
        /// </summary>
        public const string TempFolderName = ProductName;

        /// <summary>
        /// Name of the named event used for single-instance detection and for
        /// waking the already-running instance. Two builds that share this name
        /// see each other as the same product: a second build will refuse to
        /// start, or will hand its window over to the wrong process. A rebranded
        /// build MUST take a fresh GUID here so it can run beside the original.
        ///
        /// This GUID was generated for Thrum and deliberately differs from the
        /// inherited DS4Windows one (<c>{a52b5b20-d9ee-4f32-8518-307fa14aa0c6}</c>),
        /// so a Thrum install and a real DS4Windows install never see each
        /// other as second instances of themselves.
        /// </summary>
        public const string SingleInstanceEventName =
            "{21c16c88-2c23-4389-91a1-e6613bab7255}";

        /// <summary>
        /// Memory-mapped file holding the main window's Win32 class name, which
        /// the <c>-command</c> client reads before calling <c>FindWindow</c>.
        /// Shared with another product means the client aims at the wrong
        /// window class; unmatched between the two halves of this app means the
        /// client never finds the window at all.
        /// </summary>
        public const string IpcClassNameMmfName =
            ProductName + "_IPCClassName.dat";

        /// <summary>
        /// Memory-mapped file carrying the string result of a
        /// <c>-command query.*</c> call back to the client process. Must match
        /// on both ends or queries return empty.
        /// </summary>
        public const string IpcResultDataMmfName =
            ProductName + "_IPCResultData.dat";

        /// <summary>
        /// Auto-reset event the background process signals once it has written
        /// the query result. Must match on both ends or the client waits out its
        /// full ten-second timeout and reports nothing.
        /// </summary>
        public const string IpcResultDataReadyEventName =
            ProductName + "_IPCResultData_ReadyEvent";

        /// <summary>
        /// Mutex serialising concurrent <c>-command query.*</c> clients so they
        /// cannot interleave writes to <see cref="IpcResultDataMmfName"/>.
        /// Sharing it with an unrelated product costs throughput; failing to
        /// share it between this app's own clients corrupts results.
        /// </summary>
        public const string IpcResultDataSingleTaskMutexName =
            ProductName + "_IPCResultData_SingleTaskMtx";

        /// <summary>
        /// Name of the Task Scheduler task used for the "run at logon,
        /// elevated" startup option. Lookup, creation and deletion all use this
        /// exact string, so a change orphans any task an older build created:
        /// the app then reports startup as disabled while Windows keeps
        /// launching the old entry.
        ///
        /// Distinct from the VIIPER backend's own <c>RunVIIPER</c> task, which
        /// is shared ecosystem state and deliberately not derived from here.
        /// </summary>
        public const string StartupTaskName = "Run" + ProductName;

        /// <summary>
        /// File name of the shortcut dropped in the user's Startup folder for
        /// the non-elevated startup option. Same orphaning hazard as
        /// <see cref="StartupTaskName"/>.
        /// </summary>
        public const string StartupShortcutName = ExeBaseName + ".lnk";

        /// <summary>
        /// Stem of the log file names. Kept lower case with an underscore
        /// rather than composed from <see cref="ProductName"/>, because the
        /// file name convention is independent of the product's display
        /// casing.
        /// </summary>
        private const string LogFileBaseName = "thrum_log";

        /// <summary>
        /// The NLog file target's file name, applied over the config file at
        /// startup by <c>LoggerHolder</c>. <c>NLog.config</c> declares a
        /// placeholder of the same name because NLog requires the attribute to
        /// be present; that placeholder is XML and cannot consume this
        /// constant, so it has to be changed by hand alongside this one. If
        /// they diverge, the bug-report instructions and any early-startup
        /// failure path name a file that nothing else writes.
        /// </summary>
        public const string LogFileName = LogFileBaseName + ".txt";

        /// <summary>
        /// NLog archive file name pattern for rolled log files. The <c>{#}</c>
        /// placeholder is substituted by NLog, not by string interpolation.
        /// </summary>
        public const string LogArchiveFileName = LogFileBaseName + "_{#}.txt";

        /// <summary>
        /// <c>owner/repo</c> of the GitHub repository that publishes this
        /// product's releases. Root of the release URLs below: point it at the
        /// wrong repository and the update check offers a different product's
        /// builds for download.
        ///
        /// Still points at the upstream repository: the update feed cutover is
        /// a separate change from the assembly rename, because it also has to
        /// disable auto-update until this product ships an updater of its own.
        /// Until then a manual update check simply offers upstream's page.
        /// </summary>
        public const string ReleaseOwnerRepo = "hbashton/DS4Windows";

        /// <summary>Project page, used by the About window's source link.</summary>
        public const string ProjectUri =
            "https://github.com/" + ReleaseOwnerRepo;

        /// <summary>
        /// Human-facing releases page. This is where a manual update check
        /// sends the user when in-app updating is unavailable.
        /// </summary>
        public const string ReleasesPageUri = ProjectUri + "/releases";

        /// <summary>
        /// GitHub REST endpoint listing all releases. Drives channel selection
        /// in <see cref="ReleaseChannelPolicy"/>; a wrong value makes every
        /// update check fail or, worse, succeed against a foreign repository.
        /// </summary>
        public const string ReleasesApiUri =
            "https://api.github.com/repos/" + ReleaseOwnerRepo + "/releases";

        /// <summary>GitHub REST endpoint for the latest release only.</summary>
        public const string LatestReleaseApiUri = ReleasesApiUri + "/latest";

        /// <summary>
        /// <c>owner/repo</c> of the external updater executable's repository.
        /// Separate from <see cref="ReleaseOwnerRepo"/> because the updater is
        /// a different project; it is also the reason a rebranded build must
        /// not keep this value, since running the upstream updater would
        /// install the upstream product over this one.
        /// </summary>
        public const string UpdaterOwnerRepo = "hbashton/DS4Updater";

        /// <summary>Releases page of the external updater project.</summary>
        public const string UpdaterReleasesPageUri =
            "https://github.com/" + UpdaterOwnerRepo + "/releases";

        /// <summary>
        /// GitHub REST endpoint for the newest updater release, used to decide
        /// whether the bundled updater is stale.
        /// </summary>
        public const string UpdaterLatestReleaseApiUri =
            "https://api.github.com/repos/" + UpdaterOwnerRepo +
            "/releases/latest";

        /// <summary>
        /// File name of the 64-bit external updater. It is both the release
        /// asset name to download and the file name to place next to the
        /// executable, so the two uses must agree or the app re-downloads the
        /// updater on every check.
        /// </summary>
        public const string UpdaterExeName = "DS4Updater.exe";

        /// <summary>32-bit counterpart of <see cref="UpdaterExeName"/>.</summary>
        public const string UpdaterExeNameX86 = "DS4Updater_x86.exe";

        /// <summary>
        /// Absolute pack URI prefix for resources compiled into the app
        /// assembly. Must name the same assembly as
        /// <see cref="ExeBaseName"/> (and therefore the csproj
        /// <c>AssemblyName</c>) or every image, icon and theme loaded through
        /// it throws at runtime rather than at build time.
        /// </summary>
        public const string AssemblyResourcePrefix =
            "pack://application:,,,/" + ExeBaseName + ";";

        /// <summary>
        /// Relative pack URI prefix for the <c>Resources</c> folder. Same
        /// assembly-name coupling as <see cref="AssemblyResourcePrefix"/>;
        /// this is the one the tray icons and controller artwork use.
        /// </summary>
        public const string ResourcesPrefix =
            "/" + ExeBaseName + ";component/Resources";

        /// <summary>
        /// File name of a satellite resources assembly. The language-pack check
        /// looks for this name inside each culture folder under the probing
        /// path, so it must track the app assembly name or every downloaded
        /// language pack is reported as missing.
        /// </summary>
        public const string LanguageAssemblyName =
            ExeBaseName + ".resources.dll";

        /// <summary>
        /// Marker file written next to the executable recording which release
        /// channel produced this install. Renaming it makes an existing install
        /// look like a fresh one and resets the channel to the default.
        /// </summary>
        public const string InstalledReleaseFileName =
            ExeBaseName + ".release";

        /// <summary>
        /// <c>User-Agent</c> sent with GitHub API requests. GitHub rejects
        /// requests without one, so this must stay non-empty.
        /// </summary>
        public const string HttpUserAgent = ProductName;

        /// <summary>
        /// Caption of the window and message box used by the read-only
        /// <c>-viiperdriverdiagnostic</c> report. Cosmetic.
        /// </summary>
        public const string DiagnosticWindowTitle =
            ProductName + " - VIIPER driver diagnostic";
    }
}

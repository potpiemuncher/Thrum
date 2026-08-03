# Identity map

Every place the product's identity is written down, what it is coupled to, and
which rebrand pull request is responsible for changing it.

This is the checklist for the rebrand. It exists because the identity is not one
string: it is an assembly name that every WPF resource URI depends on, a set of
kernel object names that decide whether two builds can coexist, a scheduled task
that outlives an uninstall, an XML root element that every saved profile
contains, and about two hundred translated user-visible strings. Missing one of
them produces a runtime failure in a corner of the app that nobody exercises
until a user does.

**Sweep basis:** `git grep -in "ds4windows"` plus a targeted sweep for the
`DS4W*` icon asset names. First taken on the branch that introduced
`DS4Control/ProductInfo.cs`, then re-run and re-classified line by line on the
flip branch, and re-run again on the import branch; every row below carries a
disposition and nothing is uncategorised.

**Total raw hits: 1637 before the flip, 1644 after it, 1702 after the import
pull request, 1805 after the icons/updater/version pull request, 1659 after the
localization sweep.** Most of that total is not identity at all — see
[Bulk categories](#bulk-categories) — so the number to watch is the
per-category breakdown below, not the total. For four pull requests the count
*rose* while the identity literals themselves went away: the flip added
explanatory prose to this file and to `ProductInfo`, and the import change added
the audit sections below, the [smoke checklist](smoke-rebrand.md), and six new
source files whose GPL header and `namespace DS4Windows` line each contribute
three hits before a line of logic is written.

The localization sweep is the first one where it falls, by 146, and it falls in
the places that mattered: `.resx` −137, the generated designers −13, `.cs` −15,
`.xaml` −7, against +16 in documentation (this file's new sections and the
progress log) and +10 in tests, where the old name appears as guard needles.
What is left in the resource files is the deliberate remainder catalogued in
§10: dead keys, upstream links, foreign executable names, and keys whose *names*
still spell the old product because a key is a contract.

The icons/updater/version change repeated the pattern: +52 hits, all of them
prose (this file's revised rows, the progress log entry, the new tests' XML
documentation explaining *why* the external updater is gone) plus four new
guard-test needles. The literals it removed are larger than the ones it added
— the whole `DS4Updater` pipeline, three `DS4W*.ico` file names, two resource
references to them, and a dead localized string naming `DS4Updater.exe`.

The flip's re-sweep found eight anchors the first pass had missed. Each is
marked **(found in 1.2)** below: a duplicated `%APPDATA%` folder literal, five
`DS4WINDOWS_*` diagnostic environment variables, two persisted audio
pseudo-endpoint prefixes, the tray tooltip and balloon captions, five
message-box captions outside `App.xaml.cs`, and a hard-coded assembly name in
`utils/inject_deps_path.py`.

## Disposition legend

| Tag | Meaning |
|---|---|
| **DONE (1.1)** | Now reads from `ProductInfo`; value unchanged. |
| **DONE (1.2+1.3)** | Landed by the flip pull request: `ProductInfo`'s values, the assembly rename, and the XAML / manifest / script sweep. Covers plan tasks 1.2, 1.3 and 1.5. |
| **DONE (1.4+1.5)** | Landed by the import pull request: the one-time copy-import of an existing DS4Windows configuration, and the startup-name and HidHide audits that close out task 1.5. |
| **DONE (1.6+1.7+1.9)** | Landed by the icons/updater/version pull request: the placeholder icon set and its generator, the About box, the release-feed cutover and removal of the external updater, and the version reset. |
| **localization PR** | `.resx` user-visible strings (plan task 1.8). |
| **KEEP** | Deliberately unchanged. Reason given per row. |
| **DECIDE** | Needs an explicit decision before the flip; default recorded. |

---

## 1. Single source of truth

`DS4Windows/DS4Control/ProductInfo.cs` (new, this PR) holds every identity
constant. All members are compile-time constants, composed from `ProductName` /
`ExeBaseName` wherever the value is genuinely derived, so the composed forms
cannot drift from their parts. Each member's XML doc states what breaks if it
stops matching its consumer.

Guard tests: `DS4WindowsTests/ProductIdentityTests.cs` (10 tests). The load
bearing one is `ExeBaseNameMatchesTheApplicationAssemblyName`, which reflects on
the app assembly — it fails if the csproj `AssemblyName` and
`ProductInfo.ExeBaseName` are ever changed independently. The resource tests
resolve every tray icon, every battery icon, the four controller artwork images
and one absolute-prefix pack URI, so a broken prefix fails CI instead of
throwing when a user opens the page that needs it.

---

## 2. Assembly, resources and manifest

The fragile cluster. `AssemblyName` and the three prefixes must move together in
one commit.

| Anchor | Location | Disposition |
|---|---|---|
| `AssemblyName` = `Thrum` | `DS4Windows/DS4WinWPF.csproj:10` | **DONE (1.2)** |
| `ASSEMBLY_RESOURCE_PREFIX` | `DS4Control/ScpUtil.cs:679` | **DONE (1.1)** → `ProductInfo.AssemblyResourcePrefix` |
| `RESOURCES_PREFIX` | `DS4Control/ScpUtil.cs:680` | **DONE (1.1)** → `ProductInfo.ResourcesPrefix` |
| `LANGUAGE_ASSEMBLY_NAME` | `DS4Control/ScpUtil.cs:683` | **DONE (1.1)** → `ProductInfo.LanguageAssemblyName` |
| `assemblyIdentity name="Thrum.app"` | `DS4Windows/app.manifest:3` | **DONE (1.2)** — XML, cannot consume a constant |
| 4 XAML pack URIs `/Thrum;component/Resources/*.png` | `DS4Forms/ProfileEditor.xaml:100,107,114,121` | **DONE (1.2)** |
| 24 legacy `lex:ResxLocalizationProvider` attachment sets | 24 of 26 localized `DS4Forms/*.xaml` files | **RETIRED (issue #72)** — all provider attachments and the external XML namespace URI were removed. All 26 files now bind `lex` to `DS4WinWPF.DS4Forms.Localization`; 602 live localization expressions resolve through the in-house `LocExtension`. |
| `ThemeResourceTests` relative pack URIs (2) | `DS4WindowsTests/ThemeResourceTests.cs` | **DONE (1.2)** — now composed from `ProductInfo.ExeBaseName` |
| `PackageProjectUrl`, `RepositoryUrl` | `DS4WinWPF.csproj:29,31` | **DONE (1.2)** → `https://github.com/potpiemuncher/Thrum`. Package metadata. `ProductInfo.ReleaseOwnerRepo` caught up in **1.7**. |
| Solution/project names `DS4WinWPF`, `DS4WindowsTests` | `DS4WindowsWPF.sln`, project files | **KEEP** — project/namespace identity, out of Phase 1 scope (no `RootNamespace` change) |
| `namespace DS4Windows` / `DS4WinWPF`, 272 declaration lines + 686 qualified `DS4Windows.Type` references | tree-wide `.cs` | **KEEP** — namespaces are not product identity; renaming them is a separate, purely cosmetic churn with a large conflict cost against `upstream-track` |

> **Note on `RootNamespace`.** The csproj sets `RootNamespace=DS4WinWPF` while
> `AssemblyName=DS4Windows`. Only `AssemblyName` feeds pack URIs. The flip PR
> changes `AssemblyName` alone.

---

## 3. Data locations

| Anchor | Location | Disposition |
|---|---|---|
| `%APPDATA%\Thrum` (`appDataPpath`) | `DS4Control/ScpUtil.cs:636` | **DONE (1.3)** → `ProductInfo.AppDataFolderName`, value flipped. The one-time copy-import from `%APPDATA%\DS4Windows` landed in **1.4**, below. |
| `%LOCALAPPDATA%\Thrum` (`localAppDataPpath`) | `DS4Control/ScpUtil.cs:637` | **DONE (1.3)** → `ProductInfo.LocalAppDataFolderName`, value flipped |
| `%TEMP%\Thrum` diagnostic report folder | `DS4Control/Viiper/Validation/ViiperDriverValidationCommand.cs:50` | **DONE (1.3)** → `ProductInfo.TempFolderName`, value flipped |
| `%TEMP%\<product>.GameBarProbe.*.txt` | `DS4Control/GameBarIntegration.cs:829` | **DONE (1.2)** → `ProductInfo.ProductName` |
| `Update Files\DS4Windows` cleanup path in the elevated updater batch | `DS4Control/Util.cs:305` | **DONE (1.7)** — the whole `ElevatedCopyUpdater` method is gone, so both hard-coded literals inside it went with it. It was unreachable anyway: nothing ever passed `deleteUpdatesDir: true`. |
| `%APPDATA%\<product>\Logs` for the Bluetooth speaker diagnostic dump | `DS4Control/DualShock4BluetoothSpeakerPassthrough.cs:4023` | **DONE (1.2)** — **(found in 1.2)**. An independent hard-coded copy of the AppData folder name that bypassed `Global.appDataPpath` entirely; a `ScpUtil`-only flip would have left these dumps in the old product's folder. Now `ProductInfo.AppDataFolderName`. (It still ignores portable mode — a pre-existing bug, out of scope here.) |
| `%LOCALAPPDATA%\VIIPER` install dir | `extras/install-viiper-backend.ps1:9` | **KEEP** — VIIPER ecosystem state, shared with upstream installs |

### Import source — a foreign name, deliberately hard-coded

The one-time import (plan task 1.4) reads a configuration that belongs to a
different product. Its folder name is therefore **not** identity in the sense
the rest of this document uses: it must not track `ProductInfo`, and it must
survive any future rename of ours unchanged.

| Anchor | Location | Disposition |
|---|---|---|
| `LegacySourceFolderName = "DS4Windows"` — the `%APPDATA%` folder the import reads | `DS4Control/SettingsImport/ImportPlanner.cs` | **DONE (1.4)** — **KEEP the literal.** Both the ds4windowsapp lineage and the hbashton fork use this folder, so one constant covers both. A test asserts it differs from `ProductInfo.AppDataFolderName`, which is the mistake worth catching: deriving it from our own identity would make the importer read its own target. |
| `import-declined.txt` marker in `%APPDATA%\Thrum` | `DS4Control/SettingsImport/ImportPlanner.cs` | **DONE (1.4)** — ours; sits in the product data folder, so it moves with `ProductInfo.AppDataFolderName` automatically. Its presence is the entire "asked exactly once" protocol. |
| Config file names read by the importer (`Profiles.xml`, `Auto Profiles.xml`, `Actions.xml`, `LinkedProfiles.xml`, `ControllerConfigs.xml`, `OutputSlots.xml`, `Profiles\*.xml`) | `DS4Control/SettingsImport/ImportPlanner.cs` | **KEEP** — file-format names, identical in both products; see the file-format rule below. |

### Config file format — do not flip

| Anchor | Location | Disposition |
|---|---|---|
| XML root element `<DS4Windows>` | `[XmlRoot("DS4Windows")]` in `DS4Control/DTOXml/ProfileDTO.cs:34`; `WriteStartElement("DS4Windows")` in `DS4Control/ProfileMigration.cs:286,423,504`; `CreateElement("DS4Windows")` and the `SelectSingleNode("DS4Windows/...")` / `"/DS4Windows/Control"` XPaths in `ScpUtil.cs:1497,1507,4706,4707,4722,5787,5823` | **KEEP** — this is the on-disk file format, not the brand. Renaming it invalidates every existing profile, every exported profile users share, and the import wizard's source format. |
| `" Made with <product> version {0} "` XML comments (5) | `ScpUtil.cs:4658,4716,8760,8820`, `OutputSlotPersist.cs:78` | **DONE (1.2)** → `ProductInfo.ProductName`; cosmetic, inside written files |
| `" <product> Configuration Data. {0} "` XML comments (2) | `ScpUtil.cs:4657,4713` | **DONE (1.2)** → `ProductInfo.ProductName`; cosmetic |

---

## 4. Single instance and IPC

Getting these wrong is what makes a rebranded build hijack the original's
command-line IPC, or refuse to start beside it. The plan's acceptance criterion
"side-by-side run with real DS4Windows does not cross-talk" is exactly this
table.

| Anchor | Location | Disposition |
|---|---|---|
| Single-instance `EventWaitHandle` GUID | `App.xaml.cs:71` | **DONE (1.3)** → `ProductInfo.SingleInstanceEventName`, freshly generated `{21c16c88-2c23-4389-91a1-e6613bab7255}`. It differs from the inherited `{a52b5b20-…}`, so Thrum and a real DS4Windows install never see each other as second instances of themselves. |
| `DS4Windows_IPCClassName.dat` MMF (create + open) | `App.xaml.cs:695,716` | **DONE (1.1)** → `ProductInfo.IpcClassNameMmfName` |
| `DS4Windows_IPCResultData.dat` MMF (create + open) | `App.xaml.cs:742,791` | **DONE (1.1)** → `ProductInfo.IpcResultDataMmfName` |
| `DS4Windows_IPCResultData_ReadyEvent` (create + open) | `App.xaml.cs:574,788` | **DONE (1.1)** → `ProductInfo.IpcResultDataReadyEventName` |
| `DS4Windows_IPCResultData_SingleTaskMtx` | `App.xaml.cs:559` | **DONE (1.1)** → `ProductInfo.IpcResultDataSingleTaskMutexName` |
| `FindWindow(className, "DS4Windows")` | `App.xaml.cs:540` | **DONE (1.1)** → `ProductInfo.WindowTitle` |
| Main window `Title` | `DS4Forms/MainWindow.xaml` | **DONE (1.3)** — the XAML attribute was **removed**, not re-spelled. The constructor’s `Title = ProductInfo.WindowTitle` is now the only source, so the `FindWindow` target and the real title cannot diverge. |
| WM_COPYDATA `-command` handler | `DS4Forms/MainWindow.xaml.cs:1522` (handler)  | no identity literal; the protocol shape is unchanged |
| OSC command word `"ds4windows"` and 26 OSC addresses `/ds4windows/monitor/...` | `DS4Control/ControlService.cs:324,361,2218,3110,4122,4155–4257` | **DECIDE** — external wire protocol consumed by user OSC setups. Default: **keep**, and document the address namespace as a compatibility surface. |
| `--thrum-gamebar-probe` CLI switch | `DS4Control/GameBarIntegration.cs:60` | **DONE (1.2)** → composed from `ProductInfo.ExeBaseNameLowerInvariant`; self-invoked only, no external consumer |
| Process-name comparison | `DS4Control/GameBarIntegration.cs:1114` | **DONE (1.2)** → `ProductInfo.ExeBaseName` |
| Five message-box captions that are exactly the product name | `LanguagePackControl.xaml.cs:61`, `ProfileEditor.xaml.cs:2052,2931`, `SaveWhere.xaml.cs:85`, `StickCalibrationWindow.xaml.cs:41,53` | **DONE (1.2)** → `ProductInfo.ProductName` — **(found in 1.2)**. The 1.1 pass converted the four captions in `App.xaml.cs` but did not sweep the rest of the tree for the same pattern. |
| HidHide fallback `ExeName` | `DS4Control/ControlService.cs:791` | **DONE (1.2)** → `ProductInfo.ExeBaseName`. See the HidHide audit below. |
| Worker thread names `"… Game Bar API Poll"`, `"… UIA Poll"` | `DS4Control/GameBarIntegration.cs:449,554` | **DONE (1.2)** → `ProductInfo.ProductName`; cosmetic (debugger only) |
| Five `DS4WINDOWS_*` diagnostic environment variables: `…_DUALSENSE_PCM_TRACE_DIRECTORY`, `…_DS4_AUDIO_DRIFT_MODE`, `…_DS4_AUDIO_TRANSPORT_MODE`, `…_DS4_AUDIO_DIAGNOSTIC_CAPTURE`, `…_VIIPER_STATE_RATE_HZ` | `DualSenseBluetoothSpeakerPassthrough.cs:595`, `DualShock4AudioDrift.cs:21`, `DualShock4AudioTransport.cs:31`, `DualShock4BluetoothSpeakerPassthrough.cs:223`, `Viiper/ViiperOutDevice.cs:35` | **KEEP** — **(found in 1.2)**. Same class as the OSC namespace: an external control surface a human sets before launching, with no in-tree consumer that a rename would fix. Renaming them silently invalidates every debugging runbook that names them, and no test would catch it. See open decision 4. |
| Audio pseudo-endpoint prefixes `DS4Windows:AudioHapticsApp:` and `DS4Windows:AudioHapticsAuto:` | `DS4Control/ProcessLoopbackWaveCapture.cs:18,19` | **KEEP** — **(found in 1.2)**. These are not display strings: the composed id is stored as a profile's capture-source setting, so they are on-disk file-format values in the same sense as the `<DS4Windows>` root element. Flipping them silently resets every per-app audio-haptics capture selection. |
| Audio pseudo-endpoint ids `DS4Windows:AutoDetectDualSenseGameAudio` and `DS4Windows:DefaultSystemAudio` | `DS4Control/DualSenseAudioPassthrough.cs:36,37` | **KEEP** — **(found in 1.8)**. The two the 1.2 sweep missed, and the same class as the pair above: both are compared with `StringComparison.Ordinal` against the persisted per-profile capture-source setting. Flipping either silently resets "auto-detect game audio" and "system audio" on every existing profile. |
| Named pipe prefix `DS4Windows.DualSenseAudioPacer.` | `DS4Library/InputDevices/DualSenseBluetoothAudioPacer.cs:271` | **DONE (1.8)** → `ProductInfo.ProductName` — **(found in 1.8)**. A kernel object name, so §4's category, but safe to move: the parent composes it with its own PID and a fresh GUID and hands it to the helper on the command line, so both ends agree by construction and no other process can be listening. |
| Auto game-audio detection excludes the running app by **process name** | `DS4Control/AutomaticGameAudioDetector.cs` | **DONE (1.8)** — **(found in 1.8), and a real regression from the rename.** The exclusion set listed `ds4windows`, so before 1.2 this application could never be chosen as "the game" whose audio to capture. After the rename to `Thrum.exe` nothing excluded us any more. `ProductInfo.ExeBaseNameLowerInvariant` is now in the set; the inherited `ds4windows` entry stays, because a real DS4Windows install running alongside is not a game either. |

### HidHide audit — **verdict: no hard-coded name; nothing to fix** (1.5)

Category: *runtime-derived identity*. The question the plan asks is whether the
HidHide whitelist registration could register the wrong executable after a
rename — for instance by naming `DS4Windows.exe`, or by composing a name from a
constant instead of asking the OS what is running.

| Path | What it uses | Verdict |
|---|---|---|
| `Global.exelocation` (`DS4Control/ScpUtil.cs:581`) | `Process.GetCurrentProcess().MainModule.FileName`, then resolves a junction/symlink directory to its real target (the Scoop case) | **Dynamic.** The whole chain starts from the OS's answer for *this* process. It cannot name a product it is not. |
| `ControlService.CheckHidHidePresence` (`:778`) | On the startup call the arguments are empty, so it takes `ExePath = Global.exelocation` and `ExeName = ProductInfo.ExeBaseName`; converts the path to its DOS-device form and adds *that* to the whitelist | **Dynamic path, identity used only for the log line.** `ExeName` never reaches HidHide; it appears in "… not found in HidHide whitelist. Adding to list". |
| `AutoProfilesViewModel:522` | Passes the user-chosen game executable path and file name | **Dynamic**, and unrelated to product identity. |
| `ControlService.UpdateHidHideAttributes` (`:855`) | Reads the active state and the device blacklist; no executable name involved at all | **N/A.** |
| `HidHideAPIDevice` (`DS4Control/HidHideAPIDevice.cs`) | Opens the control device `\\.\HidHide` and issues IOCTLs | **N/A.** The device name is HidHide's own and is not ours to rename. |
| `Global.hidHideInstalled` → `IsHidHideInstalled()` (`ScpUtil.cs:1200`) | Probes for the system device `root\HidHide` | **N/A.** |

No change was needed. The smoke checklist ([smoke-rebrand.md](smoke-rebrand.md),
item 10) covers the part no unit test can: that the entry HidHide actually shows
is this build's `Thrum.exe` at the folder it really runs from.

---

## 5. Startup, scheduled task, shortcut, log

| Anchor | Location | Disposition |
|---|---|---|
| Startup shortcut `DS4Windows.lnk` (2 sites) | `StartupMethods.cs:30,36` | **DONE (1.1)** → `ProductInfo.StartupShortcutName` |
| Same literal, **duplicated** outside `StartupMethods` | `DS4Forms/ViewModels/SettingsViewModel.cs:637` | **DONE (1.1)** — converted here too; it was an independent copy and would have been missed by a `StartupMethods`-only flip |
| Scheduled task `RunDS4Windows` (7 sites: find ×4, register, delete ×2, plus the `start` console title in `task.bat`) | `StartupMethods.cs:43,90,100,138,144,147,160,202` | **DONE (1.1)** → `ProductInfo.StartupTaskName` |
| NLog runtime file name `ds4windows_log.txt` + archive `ds4windows_log_{#}.txt` | `LoggerHolder.cs:42,43` | **DONE (1.1)** → `ProductInfo.LogFileName` / `LogArchiveFileName`. These are the *effective* names: the bootstrap overrides the config file. |
| NLog config placeholder `fileName="thrum_log.txt"` | `DS4Windows/NLog.config:8` | **DONE (1.2)** — edited directly. It is XML consumed by NLog before any managed identity constant exists, so it cannot delegate to `ProductInfo`; NLog requires the attribute even though `LoggerHolder` replaces it. |
| Bug-report instruction naming `thrum_log.txt` | `.github/ISSUE_TEMPLATE/bug_report.md:25` | **DONE (1.2)** |
| VIIPER at-logon task `RunVIIPER` | `extras/install-viiper-backend.ps1` | **KEEP** — VIIPER's own task, shared with upstream DS4Windows installs. Uninstall may remove it only if we created it (ownership marker, plan task 5.4). The script only ever *registers* it; nothing in the tree unregisters a scheduled task other than our own. |

### Startup-entry safety audit — **verdict: every delete path is scoped to us** (1.5)

The renames in 1.2/1.3 are only half the job. The other half is that a user of
this product very likely still has a real DS4Windows install, whose
`RunDS4Windows` task and `DS4Windows.lnk` shortcut are **not ours to touch** —
and several code paths here delete startup entries. Every one was re-read:

| Path | Names it can reach | Verdict |
|---|---|---|
| `StartupMethods.DeleteStartProgEntry` | `lnkpath` only | Scoped |
| `StartupMethods.DeleteTaskEntry` | `ProductInfo.StartupTaskName` | Scoped |
| `StartupMethods.DeleteOldTaskEntry` | `ProductInfo.StartupTaskName` | Scoped. **Misleading name**: "old" means a stale task *of ours* pointing at a moved `task.bat`, not the product we forked from. A doc comment now says so, because the obvious "fix" — making it look for the inherited name — is exactly the bug. |
| `StartupMethods.CheckStartupExeLocation` | Resolves `lnkpath` | Scoped |
| `SettingsViewModel` constructor (`:480–517`) | Deletes the shortcut when both entries exist; deletes and rewrites it when the executable moved; `DeleteOldTaskEntry` + `WriteTaskEntry` for the task branch | Scoped — all through `StartupMethods` |
| `SettingsViewModel_RunAtStartupChanged` / `_RunStartProgChanged` / `_RunStartTaskChanged` | Same | Scoped |

**No legacy-cleanup path exists**, so there was nothing to fence or delete. Two
duplicated path expressions were collapsed instead: `HasStartProgEntry` and
`SettingsViewModel.CheckStartupOptions` each composed their own copy of the
shortcut path, and both now read `StartupMethods.lnkpath`. Both copies happened
to be correct; the point is that a rename has to be able to miss only one place.

Guard: `DS4WindowsTests/StartupEntryIdentityTests.cs` (4 tests). The load-bearing
one scans the compiled application for `RunDS4Windows` and `DS4Windows.lnk` and
fails if either appears anywhere in it — not just in `StartupMethods` — with a
positive control asserting the same scan does find `RunThrum` and `Thrum.lnk`,
so a vacuous pass is impossible.

---

## 6. Updater and release feed

| Anchor | Location | Disposition |
|---|---|---|
| `GITHUB_RELEASES_API_URI`, `GITHUB_LATEST_RELEASE_API_URI` | `DS4Control/ScpUtil.cs:3453,3454` | **DONE (1.7)** — aliases of `ProductInfo.ReleasesApiUri` / `LatestReleaseApiUri`; both now resolve to `potpiemuncher/Thrum`. |
| `ProductInfo.ReleaseOwnerRepo` | `DS4Control/ProductInfo.cs` | **DONE (1.7)** → `potpiemuncher/Thrum`. Every other release URL is composed from it, so they cannot disagree about whose builds to offer. |
| Latest-release URL used by `DownloadUpstreamVersionInfo` | `MainWindowsViewModel.cs:863` | **DONE (1.7)** — the method had no callers and was deleted. It wrote a `version.txt` that only the equally dead `Check_Version` read. |
| `DS4Updater.exe` / `DS4Updater_x86.exe`, `hbashton/DS4Updater` API and page URLs | `ProductInfo.cs`, `MainWindowsViewModel.cs` | **DONE (1.7)** — **all five constants deleted, not repointed.** There is no Thrum updater to name yet, and a repointed constant is an invitation to wire the pipeline back up. |
| `Util.ElevatedCopyUpdater` and its `%TEMP%` `updatercopy.bat` elevation | `DS4Control/Util.cs:290` | **DONE (1.7)** — deleted with its single caller. This was the only elevation anywhere in the update path. |
| `MainWindowsViewModel.RunUpdaterCheck` / `LauchDS4Updater` / `DownloadUpstreamUpdaterVersion` | `MainWindowsViewModel.cs:802–939` | **DONE (1.7)** — deleted. Together they were the download, the elevated copy and the launch. |
| `MainWindow.Check_Version` | `MainWindow.xaml.cs:335` | **DONE (1.7)** — deleted; no callers, and a second older copy of the same update flow. |
| `Changelog.CheckNewerVersionExists` and the `_latestVersion` cache field | `ScpUtil.cs:3535` | **DONE (1.7)** — deleted; no callers. |
| `hbashton/DS4Updater` release-tag links (2) | `MainWindow.xaml.cs:328,390` | **DONE (1.7)** — both gone with the methods that contained them. |
| Three hard-coded `"DS4Windows Updater"` message-box captions | `MainWindow.xaml.cs:275,2031,2035` | **DONE (1.7)** → `ProductInfo.ProductName`. Same class as the five captions found in 1.2. |
| `PleaseDownloadUpdater` resource string (4 languages) | `Properties/Resources*.resx` | **DONE (1.7)** — deleted. It told the user to download and rename `DS4Updater.exe`, describing a feature that no longer exists, and it was the last thing in the neutral resources naming the updater binary. |
| `InstalledReleaseFileName = "DS4Windows.release"` | `DS4Control/ReleaseChannelPolicy.cs:11` | **DONE (1.1)** → `ProductInfo.InstalledReleaseFileName` (derived from `ExeBaseName`, so it flipped with the assembly rename) |
| Project link `https://github.com/hbashton/DS4Windows` | `DS4Forms/About.xaml.cs:45` | **DONE (1.1)** → `ProductInfo.ProjectUri`, value flipped in **1.7**. A separate, deliberate hbashton link now exists in the About box's lineage credits — that one is attribution and must *not* track `ProductInfo`. |
| Contributors link `…/blob/main/contributors.txt` | `DS4Forms/About.xaml.cs:100` | **DONE (1.1)**, value flipped in **1.7** |
| HTTP `User-Agent: DS4Windows` (2) | `App.xaml.cs:621,638` | **DONE (1.1)** → `ProductInfo.HttpUserAgent` |
| `newest.txt` (contained `4.0.2.1`) | `DS4Windows/newest.txt` | **DONE (1.9)** → `0.9.0`. Re-verified dead: `post-build.py` writes its copy to the *repository root* from the CI version argument, never to this file, and no code in the tree reads either copy. The root copy is now `.gitignore`d as the build artifact it is. |
| `Changelog.json`, `Changelog.min.json` | `DS4Windows/` | **DONE (1.7)** — **deleted.** Re-verified against `ChangelogWindow` as the task required, because "nothing reads it" is exactly the kind of finding that is wrong once: that window does **not** read them. It goes `ChangelogViewModel` → `Changelog.GetChangelogMarkdown` → an HTTP GET of the releases API, and renders GitHub release bodies as markdown. The two files were 219 KB of stale 3.3.3 data with no reader in the csproj, the workflows, the build scripts or the C#. Closes open decision 2. |
| `hbashton/VIIPER` releases URL | `DS4Control/Viiper/ViiperSetupManager.cs:69` | **KEEP** — the backend's own repository |

---

## 7. Icons and artwork

| Anchor | Location | Disposition |
|---|---|---|
| `ApplicationIcon` | `DS4WinWPF.csproj:11` | **DONE (1.6)** → `Resources\Thrum.ico`. Also **de-duplicated**: the inherited value named a second copy of the icon at the project root (`DS4Windows/DS4W.ico`) that had to be kept in step with `Resources/DS4W.ico` by hand. There is now one file. |
| Tray icon assets `DS4W.ico`, `DS4W - White.ico`, `DS4W - Black.ico` | `Resources/`, csproj `None`+`Resource` entries (6 lines) | **DONE (1.6)** → `Thrum.ico`, `Thrum - White.ico`, `Thrum - Black.ico`. The three inherited files were **deleted**, not kept alongside. |
| `TrayIconChoice` → icon map (5 entries) | `DS4Control/ScpUtil.cs:1022—1029` | **DONE (1.6)** → composed from `ProductInfo.AppIconFileName` / `WhiteTrayIconFileName` / `BlackTrayIconFileName`, so the map cannot name a file the identity does not. |
| Battery tray icons `0.ico` — `100.ico` + fallback | `DS4Forms/ViewModels/TrayIconViewModel.cs:505—516` | **DONE (1.6)** — **contents replaced, names kept.** The view model composes these paths arithmetically from the battery percentage; the names describe a level, not a brand, so renaming them would have meant rewriting that switch to buy nothing. The fallback now uses `ProductInfo.AppIconFileName`. |
| Dead `ResXFileRef` entries `DS4W`, `DS4W___White` | `Properties/Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` | **DONE (1.6)** — deleted. Nothing read `Properties.Resources.DS4W*`, but a file reference to a deleted `.ico` is a *build* failure, so these had to move with the files either way. `DS4` → `DS4.ico` stays: that one is device artwork and its target still exists. |
| Icon provenance notice | `Resources/ICONS.NOTICE.txt` (new) | **DONE (1.6)** — states that the icons are project-owned, GPL, generated by `utils/generate-thrum-icons/`, and explicitly placeholders. Ships next to `ControllerArtwork.NOTICE.txt`. |
| Icon generator | `utils/generate-thrum-icons/` (new) | **DONE (1.6)** — a committed `dotnet run` tool that renders the mark and assembles the multi-resolution `.ico` files. Deliberately **not** in `DS4WindowsWPF.sln`: it is authoring tooling, not a product component, and does not belong on the CI critical path. |
| Tray tooltip text, balloon title and tray title | `DS4Forms/ViewModels/TrayIconViewModel.cs:32,34,35` | **DONE (1.2)** → `ProductInfo.ProductName` — **(found in 1.2)**. Three literals; without them the tray would still have introduced itself as DS4Windows after every other rename landed. |
| Shell header text and its `D` monogram | `DS4Forms/Themes/BridgeShellStyles.xaml:459` | **DONE (1.2)** — the header is bound to `{x:Static identity:ProductInfo.ProductName}` and the monogram letter is now `T`. Left as a text monogram in 1.6: the icon set is an explicit placeholder, so replacing one placeholder with another inside the shell chrome would have been motion rather than progress. |
| About header `"DS4Windows - hbashton Build (Version "` | `DS4Forms/About.xaml:12` | **DONE (1.6)** — the literal was **removed** from the XAML rather than re-spelled. It was a half-sentence with the version appended in the constructor, which is precisely the shape of string a rebrand walks past. The label's content is now assigned wholly in the constructor from `ProductInfo.ProductName` and `Global.exeDisplayVersion`. |
| Controller artwork `DualShock 4 / DualSense / DualSense Edge / Switch 2 Pro Controller.png`, `DS4-Config_*.png`, `DS4 Config.png`, … | `Resources/` | **KEEP** — these name the *devices*, not the product, and are licensed per `Resources/ControllerArtwork.NOTICE.txt`. Covered by the new resource guard test. |
| `ds4winwpf_screen_20200412.png` (repo screenshot) | repo root | **DONE (1.6)** — deleted. Nothing in the tree referenced it and the README that once embedded it was replaced in 0.5, so it was an orphaned picture of another product's user interface. A replacement waits for a UI worth screenshotting. |

---

## 8. Scripts, workflows and tooling

| Anchor | Location | Disposition |
|---|---|---|
| `Thrum.deps.json`, output dir rename to `Thrum`, zip name `Thrum_{version}_{arch}`, manifest `.thrum-managed-files.txt` | `utils/post-build.py:33,39,56,64` | **DONE (1.2)** |
| `re.compile(r"^DS4Windows/")` matching the entry assembly's `deps.json` library key | `utils/inject_deps_path.py:16` | **DONE (1.2)** — **(found in 1.2)**. The nastiest miss in the sweep: this script rewrites the app's own library `path` to `./`, and with a stale pattern it would have matched nothing, exited 0, and produced a package that only fails at launch. It now derives the assembly name from the `deps.json` filename, so it cannot go stale again. |
| Install path, release API, download URL, zip name, folder move, desktop shortcut, exe name (12 hits) | `ds4w.bat` | **DEFER**. Inert legacy helper: nothing in the build, the workflows, or the app references it. It gets rewritten or deleted in a later phase rather than half-flipped now. |
| Artifact name `Thrum_…`, packaged folder name, step summary text | `.github/workflows/ci-build.yml:85,91,98,102` | **DONE (1.2)**. The test-project and csproj *paths* are directory names and stay. |
| Zip asset names | `.github/workflows/release.yml:54,56` | **DONE (1.2)**. |
| Version XPath `DS4Windows/DS4WinWPF.csproj` → `.//AssemblyVersion`, `.//Version` | `.github/workflows/release.yml:33,34` | **DONE (1.9)** — repointed at the new root `Directory.Build.props`. This is the trap in moving version properties out of a project file: the step parses XML by path, so it would have kept succeeding against a csproj that no longer declares a version — `find()` returns `None` and the failure surfaces as an unhelpful `AttributeError` mid-release. |
| Bug-report template product name and log file name | `.github/ISSUE_TEMPLATE/bug_report.md:25,30` | **DONE (1.2)** |
| `Thrum-VIIPER-Setup` temp dir and User-Agent (3 hits), plus 4 product-name mentions in the script’s own log and error text | `extras/install-viiper-backend.ps1:12,70,89,177,238,244,323` | **DONE (1.2)**. `RunVIIPER` and `%LOCALAPPDATA%\VIIPER` are untouched. |
| `!DS4Windows/libs/x64/`, `!DS4Windows/libs/x86/` | `.gitignore:333,334` | **KEEP** — project *directory* paths, not identity |
| Root `/newest.txt` | `.gitignore` | **DONE (1.9)** — added. `post-build.py` writes it on every local package run, so it turned up as an untracked file inviting an accidental commit of a build artifact. |
| Bezier editor web app strings + Ryochan7 wiki links (8 hits) | `DS4Windows/BezierCurveEditor/build.js`, `index.html` | **KEEP** for now — vendored third-party web app; the wiki links are upstream documentation. Revisit in the localization PR if the in-app text is reachable. |

---

## 9. Tests

113 hits across `DS4WindowsTests/`. Almost all are `using DS4Windows;` and
qualified type references, which stay. The identity-bearing ones:

| Anchor | Location | Disposition |
|---|---|---|
| 2 relative pack URIs | `ThemeResourceTests.cs` | **DONE (1.2)** — composed from `ProductInfo.ExeBaseName` |
| Profile XML fixtures containing `<DS4Windows>` roots | `ProfileTests.cs`, `ProfileMigrationTests.cs`, `AppSettingsTests.cs`, `MappingTests.cs` | **KEEP** — file-format fixtures, see §3 |
| Report formatter expectations | `ViiperDriverReportFormatterTests.cs` | **DONE (1.2)** — the header assertion and the `%TEMP%` fixture path are composed from `ProductInfo`, so they cannot drift again |
| New guard tests | `ProductIdentityTests.cs` | n/a — these enforce the rest |
| Startup-entry guard tests, incl. the literal scan for `RunDS4Windows` / `DS4Windows.lnk` | `StartupEntryIdentityTests.cs` | **DONE (1.5)** — the two inherited names appear here *on purpose*, as the needles. This is the only file in the repository that should contain them. |
| Import tests | `SettingsImportTests.cs` | **DONE (1.4)** — the `DS4Windows` source folder name appears as the expected value of `ImportPlanner.LegacySourceFolderName`. |
| Icon guard tests | `IconResourceTests.cs` | **DONE (1.6)** — every icon loads through **both** `System.Drawing.Icon` and `BitmapFrame`, and still carries uncompressed frames at 16/24/32/48. The last of those is the one that matters: the negative control proved that a PNG-only icon passes the `System.Drawing.Icon` load test, so the load tests alone would not have caught the regression they exist to catch. |
| Update-feed guard tests | `UpdateFeedTests.cs` | **DONE (1.7)** — the four `DS4Updater` artefacts appear here *on purpose*, as the binary scan's needles. Along with `StartupEntryIdentityTests.cs`, this is one of only two files in the repository that should contain them. |
| Version compatibility tests | `VersionCompatibilityTests.cs` | **DONE (1.9)** — `app_version="4.0.2.1"` and `config_version` fixtures. These carry inherited version strings as *data being read*, which is the point of the file. |
| Localization sweep guards | `LocalizationSweepTests.cs` | **DONE (1.8)** — 7 tests. The flipped values name this product and not the old one; the `CustomExeNameInfo` exceptions are pinned individually; the upstream wiki link survived; every hand-added designer property resolves to a real key; every import format string still has its placeholders; all 23 satellites load; no satellite declares a key the neutral file lacks. The old name appears here as needles, so this is a third file that should contain it on purpose. |
| In-house localization guards | `LocalizationMarkupExtensionTests.cs` | **DONE (issue #72)** — 6 tests cover both constructors, bare/dotted/`Resources:` keys, exact prefix routing, visible misses, `CurrentUICulture` without caching, STA-loaded template/style syntax, every one of the 602 live XAML expressions, and zero legacy runtime references. |
| Satellite resolution guards | `SatelliteAssemblyResolutionTests.cs` | **DONE (issue #6)** — 10 tests. The satellite path is a pure function of `AppContext.BaseDirectory`, `Global.PROBING_PATH` and `ProductInfo.LanguageAssemblyName`, so no working directory can enter it; the handler is registered from a module initializer before any of this assembly's code runs; and it stays inert for everything that is not a `.resources` assembly. |

---

## 10. User-visible strings and translations

**DONE (1.8).** The sweep was value-only: not one key, comment key, entry count
or file encoding moved. 195 token replacements across 29 `.resx` files, plus 24
`.cs`/`.xaml` prose literals and 17 new neutral keys for the import dialog.

| Set | Files | Disposition |
|---|---|---|
| `Translations/Strings*.resx` (neutral + 24) | 25 | **DONE (1.8)** — 180 replacements in 9 allowlisted keys |
| `Properties/Resources*.resx` (neutral + 3) | 4 | **DONE (1.8)** — 15 replacements in 4 allowlisted keys |
| `Strings.Designer.cs`, `Resources.Designer.cs` | 2 | **DONE (1.8)** — the "Looks up a localized string similar to …" echoes were hand-synced for the changed neutral values. These files are **checked in and the command-line build does not regenerate them**, so the echo comments drift silently; properties and `GetString("<key>")` arguments were not touched. |
| Hard-coded English UI text and log messages in `.cs` | 16 literals across 13 files | **DONE (1.8)** → `ProductInfo.ProductName` |
| Hard-coded English text in `.xaml` | 7 literals across 6 files | **DONE (1.8)** — flipped to the literal `Thrum`. XAML text cannot consume a constant without splitting the sentence into `Run`s, and these are unlocalized English prose to begin with. |
| Import dialog (from 1.4) | `ImportSettingsDialog.xaml(.cs)`, `ImportPlanSummary.cs`, `App.xaml.cs` | **DONE (1.8)** — 17 new `Import.*` keys in the **neutral file only**; see below |

### The flip allowlist, and why it is an allowlist

A blanket `DS4Windows` → `Thrum` pass over every `<value>` would have been
wrong in four different ways, so the sweep flipped only keys that were
classified first. The script parses each file, decides from the *decoded*
value, applies the substitution to the raw `<value>` span so entities and CRLF
survive, and then re-parses and asserts that the key list, the key order and
every untargeted value are unchanged and that each targeted value decodes to
exactly the intended string.

| Key | Family | Note |
|---|---|---|
| `CheckUpdateStartup`, `AntiDeadzoneTooltip`, `DualSRumbleForceGenericRescale_Tip`, `TurnOffDS4WindowsTemporarily`, `FirstLaunch.DeviceIntroText`, `SaveWhere.AppDataDescText`, `Welcome.Step5HelpText`, `Welcome.WinTitle` | Strings | plain token swap |
| `CustomExeNameInfo` | Strings | token swap in the 24 translations; the **neutral value was rewritten by hand**, because a swap cannot fix a sentence promising that `DS4Updater` will rename our executable — that pipeline was deleted in 1.7. `DS4Windows.exe` and `InputMapper.exe` stay: they are the names a game's block looks for. |
| `DS4Update`, `LanguagePackApplyRestartRequired`, `StoppedDS4Windows`, `UpToDate` | Resources | plain token swap. `UpToDate` closes open decision 5. |

`TurnOffDS4WindowsTemporarily`, `StoppedDS4Windows`, `DS4WindowsCannotEditHere`
and `IfRemovingDS4Windows` keep **keys** that spell the old name. A key is a
contract with 24 translated files, with `lex:Loc` in XAML and with the checked-in
designer; renaming one buys nothing a user can see and breaks all three.

### Skipped hits

| Hit | Category | Reason |
|---|---|---|
| `Resources.QuitOtherPrograms` (4 files) | KEEP-UPSTREAM | its only token is inside `https://github.com/Ryochan7/DS4Windows/wiki/Exclusive-Mode-…`. The flip script protects URL spans and reports them; a test asserts the link survived. |
| `Strings.CustomExeNameInfo` — `DS4Windows.exe`, `InputMapper.exe` | KEEP-SOURCE | the processes a game detects. Renaming them to ours makes the explanation false. |
| `MainWindow.xaml:746` "Support DS4Windows" + PayPal button | KEEP-SOURCE | see open decision 6 — the link pays the upstream maintainer, so the label is currently accurate and must not move without the link. |
| `About.xaml` lineage credits (5), `About.xaml.cs` project links (3), `ControllerRegisterOptionsWindow.xaml` Moonlight doc link, `ControlService.cs:1651` KB link | KEEP-UPSTREAM | §11 attribution. |
| `<DS4Windows>` XML root, XPaths, `ImportPlanner.LegacySourceFolderName`, the four audio pseudo-endpoint ids, five `DS4WINDOWS_*` env vars, OSC addresses and command word | KEEP-TECH | on-disk format and external control surfaces (§3, §4). Verified: **none of these is a `.resx` value** — the string resources contain no protocol strings at all. |
| 11 `.resx` values in keys with zero references | DEAD | listed below, unchanged and not removed. |
| `App.xaml.cs:77` comment, `LogViewModel.cs:48` commented-out line, `AppSettingsDTO.cs:52` commented-out block, `StartupMethods.cs:110` `<see cref>` | KEEP | not user-visible; a `cref` must name the real type. |
| `utils/post-build.py:35` comment "DS4Updater uses this manifest…" | DEFER | describes a tool deleted in 1.7. Build-script prose, not identity; belongs with whatever phase revisits the packaging manifest. |

### Dead strings — enumerated, not deleted

The plan asked 1.8 to purge dead ViGEm strings. **Key removal is not safe in a
value-only pull request**: every key is echoed by a checked-in designer property
and by up to 24 translated files, so removing one is a four-file edit per key
with a build break as the failure mode. They are catalogued here instead, for a
cleanup phase that can regenerate the designers in the same commit.

Method: a key counts as referenced only when it is reached the way the codebase
actually reaches it — `Strings.<Key with dots as underscores>` in C#, or a
`lex:Loc` / `lex:LocExtension` key token in XAML for
`Strings.resx`; `Resources.<Key>` for `Properties/Resources.resx`. A bare
identifier of the same name does not count, which is what an earlier pass got
wrong. Issue #72 retired the two `lex:BLoc` uses. The in-house extension is now
the only dynamic `ResourceManager.GetString(variable)` path, and its variable is
the literal `Key` from each XAML expression; `LocalizationMarkupExtensionTests`
enumerates that complete live set and proves every key resolves.

**`Translations/Strings.resx`: 28 of 463 entries unreferenced.** Issue #72
corrected three live bindings that had been misspelled, so `DeadZone X`,
`DeadZone Y` and `PresetIntroText` left this list; its new tooltip key is live.

`AdvancedSupport`, `ControllerSupportMoonlight`,
`DS4LightbarPassthruDisabled`, `DualSRumbleModePassthru`,
`DualSRumbleSpecificSettings`, `EnableOutputDataToDS4`,
`EnableOutputDataToDS4Tip`, `FullBtnPull`, `FutureNetNotInstalled`, `HidHide`,
`HidNinja`, `HipFireDelay`, `ID`, `Mode`, `Net8NoticeWin.WinTitle`,
`Net8NotInstalledWinNotice`, `New`, `OK`, `Other`,
`SelectedProfile`, `Status`, `StickInputCurveTooltip`, `TwoStageMode`,
`UpgradeNetCaption`, `ViGEm117MinNeeded`, `ViGEmPluginFailure`,
`Welcome.Step1HelpText`, `Welcome.Step1Text`.

The three the plan named are all here: `ViGEm117MinNeeded`,
`ViGEmPluginFailure`, and the first-launch ViGEmBus step pair
(`Welcome.Step1Text` = "Step 1: Install ViGEmBus Driver", plus its help text).
`WelcomeDialog.xaml` starts at step 2, so the step-1 button was removed from the
view and its strings were left behind. **The DsHidMini text is not dead and is
not listed** — DS3 support still uses it.

**`Properties/Resources.resx`: 111 of 175 entries unreferenced.** This whole
family is WinForms-era leftovers; the live 64 are the survivors. The dead list
is machine-derivable with the rule above and is not reproduced in full here;
the eight that carry the product name are `CannotMoveFiles`, `CloseDS4W`,
`CloseMinimize`, `CopyComplete`, `DS4WindowsCannotEditHere`,
`IfRemovingDS4Windows`, `RunAtStartup`, `UACTask`.

Dead values were **not** flipped. A string no user can reach is not part of a
user-visible sweep, and flipping text that is scheduled for deletion adds churn
to 24 translated files for nothing. Three of them (`CopyComplete`,
`DS4WindowsCannotEditHere`, `RunAtStartup`) have live code-literal twins in
`App.xaml.cs` and `MainWindow.xaml` — *those* were flipped.

### Import dialog — neutral-only keys, deliberately

17 new keys under the `Import.` prefix, added to `Translations/Strings.resx`
only. The 24 translated files fall back to neutral. Machine-translating them
would put unreviewed text in front of users in 24 languages, which is worse than
English.

`Import.CollisionCountPlural`, `Import.CollisionCountSingular`,
`Import.FooterText`, `Import.HeadingText`, `Import.ImportButton`,
`Import.KindActions`, `Import.KindAppSettings`, `Import.KindAutoProfiles`,
`Import.KindControllerConfigs`, `Import.KindLinkedProfiles`,
`Import.KindOutputSlots`, `Import.PartialFailureText`,
`Import.ProfileCountPlural`, `Import.ProfileCountSingular`,
`Import.SourceText`, `Import.StartFreshButton`, `Import.WinTitle`.

`ImportSettingsDialog.xaml` became the 24th provider-attached XAML file in 1.8.
Issue #72 removed that attachment with the other 23 and now routes the dialog's
unchanged `lex:Loc` expressions through the in-house extension.

### Indonesian has never shipped

`Translations/Strings.idn.resx` produces **no satellite assembly**: `idn` is not
a culture name (Indonesian is `id`), so MSBuild drops it without an error and
`post-build.py`'s hard-coded language list creates an empty `Lang\idn\` folder
for it. 24 translated files, 23 shipped satellites. Left as found — renaming a
resource file changes which translations ship, which is not a value-only
change — and guarded by `EveryExpectedTranslationShipsAsASatellite`, whose
negative control is adding `idn` back to the expected list.

---

## 11. Attribution and lineage — intentionally kept

| Anchor | Location | Why |
|---|---|---|
| GPL headers `DS4Windows / Copyright (C) … Travis Nickles / hbashton / DS4Windows contributors` | every source file | GPL requires the notices be preserved. These name the upstream *project*, not our product. |
| `Copyright` / `Authors` / `Company` csproj metadata | `DS4WinWPF.csproj:25–28` | **DONE (1.2)** — `Authors`/`Company` are `Thrum project`; `Copyright` names the Thrum project first, preserves the whole DS4Windows lineage, and states GPL-3.0-or-later. |
| `ryochan7.github.io/ds4windows-site` links (About page, keyboard-mouse troubleshooting KB) | `About.xaml.cs:40`, `ControlService.cs:1651` | Upstream documentation that still applies. |
| `github.com/Ryochan7`, `github.com/schmaldeo/DS4Windows` (Moonlight doc), DsHidMini | `About.xaml.cs`, `ControllerRegisterOptionsWindow.xaml:29` | Lineage and third-party project links. |
| `hbashton` copyright headers on VIIPER-related files | `DS4Control/Viiper/*` | Upstream authorship. |
| `RunVIIPER`, `%LOCALAPPDATA%\VIIPER`, `hbashton/VIIPER` | `extras/install-viiper-backend.ps1`, `ViiperSetupManager.cs:69` | VIIPER ecosystem names, shared with other installs. Renaming them would fork the backend's own identity, which the plan explicitly forbids. |
| `DS4`, `DualShock`, `DualSense`, `Switch 2 Pro` in device names, enum members, artwork file names, VID/PID tables | tree-wide | Device identities, not product identity. |

---

## 12. Version, and why it is not identity

Version numbering is not in the same category as the names above, but it is
recorded here because the version reset happened in the same pull request and
because one version number in this repository *is* an identity anchor.

| Anchor | Location | Disposition |
|---|---|---|
| `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion` | root `Directory.Build.props` (new) | **DONE (1.9)** — `0.9.0-beta.1` / `0.9.0.0` / `0.9.0.0`. Both projects inherit; the per-csproj copies were removed. |
| `InformationalVersion` base-commit suffix | root `Directory.Build.props` | **DONE (1.9)** — `(base: hbashton DS4Windows 4.0.2.1 @ 5d2724a)`. **This one is identity.** It is the only place in a shipped binary that says which upstream tree it derives from, which GPL correspondence needs and support triage needs. A guard test asserts both the commit and the base version survive. |
| `app_version` XML attribute | `AppSettingsDTO`, `ProfileDTO`, `OutputSlotPersistDTO`, `ProfileMigration` | **KEEP, and keep write-only.** Every one of the three DTO properties declares `set { }`: the value is parsed off disk and discarded. That is what makes a configuration written by DS4Windows 4.0.2.1 loadable by a build numbered 0.9.0, and it is now pinned by test rather than left as an observation. |
| `config_version` (`CONFIG_VERSION = 5`, `APP_CONFIG_VERSION = 2`) | `ScpUtil.cs:675,676` | **KEEP, unchanged.** File-format versions, independent of the product version. Renumbering the product must not renumber the format, or every existing file changes meaning. `APP_CONFIG_VERSION` is never compared to anything; `CONFIG_VERSION` is compared exactly once, one-directionally. |

---

## Bulk categories

Where the 1637 raw hits actually live:

| Category | Hits |
|---|---|
| C# `namespace` / `using` / `using static` declaration lines | 272 |
| C# qualified type references (`DS4Windows.Global`, `DS4Windows.OutContType`, …) | 686 |
| `.resx` translations (29 files) | 223 |
| Test sources | 113 |
| Markdown and text docs | 88 |
| Generated `*.Designer.cs` | 34 |
| XAML (23 of which are `DefaultAssembly`, 4 are pack URIs) | 40 |
| Everything else — the actual identity constants and literals catalogued above | remainder |

The first two rows (958 hits, 59% of the total) are namespace plumbing that no
Phase 1 pull request touches.

---

## Open decisions

1. **OSC address namespace** (`/ds4windows/monitor/...`, command word
   `ds4windows`). External integrations bind to these paths. Default: keep, and
   document them as a stable compatibility surface. Revisit only with a
   migration story.
2. ~~**`Changelog.json`**.~~ **CLOSED (1.7): deleted.** Re-verified against
   `ChangelogWindow` first, since "nothing reads it" is a finding that only has
   to be wrong once. It is not the reader: it fetches the releases API and
   renders release bodies. See §6.
3. **Namespace rename** (`DS4Windows` / `DS4WinWPF` → product namespaces). Not
   in Phase 1. It is pure churn against `upstream-track` and buys nothing a
   user can see; if it is ever done, it should be a single mechanical commit
   immediately after an upstream merge.
4. **`DS4WINDOWS_*` diagnostic environment variables** (five of them, listed in
   §4). Kept by the flip on the same reasoning as the OSC namespace. Decide
   whether to rename them to `THRUM_*`, or to accept both spellings for a
   release and then drop the old one. Either way it needs a test, since nothing
   in the tree currently proves a variable is still read.
5. ~~**`Resources.UpToDate` says "DS4Windows application is up-to-date."**~~
   **CLOSED (1.8): flipped**, together with the `FakeExeName` tooltip
   (`Strings.CustomExeNameInfo`), whose neutral value was rewritten because the
   `DS4Updater` sentence in it described a feature deleted in 1.7. See §10.
6. **The in-app "Support DS4Windows" card pays the upstream maintainer.**
   `MainWindow.xaml:746` labels a PayPal button that
   `MainWindow.xaml.cs:1002` points at `paypal.me/hbashton`. 1.8 left both
   alone: the label is *accurate* as it stands, and flipping only the text
   would solicit donations for this product and route them elsewhere, which is
   worse than the inherited wording. It is a product decision, not a rebrand
   one — keep the card as upstream support and label it so, keep it and change
   the destination, or remove it. Whichever is chosen, the label and the link
   must move together.
7. **Dead resource keys.** 31 in `Strings.resx` and 111 in
   `Properties/Resources.resx` have no reference (§10). Removing a key means
   editing the checked-in designer and up to 24 translated files in the same
   commit, so 1.8 enumerated them instead of deleting them. A cleanup phase
   should take the whole list at once, with the designer regeneration in the
   same change.
8. **Indonesian never ships** (§10): `Strings.idn.resx` is not a culture name,
   so no satellite is emitted. Renaming it to `Strings.id.resx` would ship
   ~600 already-written translations; it needs a `csproj` update and a check
   that `post-build.py`'s hard-coded language list follows.

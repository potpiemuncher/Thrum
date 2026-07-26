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
pull request.** Most of that total is not identity at all — see
[Bulk categories](#bulk-categories) — so the number to watch is the
per-category breakdown below, not the total. The count keeps *rising* while the
identity literals themselves go away: the flip added explanatory prose to this
file and to `ProductInfo`, and the import change added the audit sections below,
the [smoke checklist](smoke-rebrand.md), and six new source files whose GPL
header and `namespace DS4Windows` line each contribute three hits before a line
of logic is written. The genuinely new *literals* are two, both of them
deliberate: the import source folder name, and the inherited startup entry names
used as needles by the guard test that proves they appear nowhere else.

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
| **icons+updater PR** | Icon and artwork assets, About box, update feed cutover (plan tasks 1.6, 1.7). |
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
| 23 XAML `lex:ResxLocalizationProvider.DefaultAssembly="Thrum"` | 23 `DS4Forms/*.xaml` files | **DONE (1.2)** — all 23. A missed one kills that page’s localization at runtime only, which is why the count is written down. The 1.4 import dialog is a 24th XAML file with **no** `lex` bindings: its text is English in the code-behind until the localization pull request gives it `.resx` keys, at which point it needs this attribute too. |
| `ThemeResourceTests` relative pack URIs (2) | `DS4WindowsTests/ThemeResourceTests.cs` | **DONE (1.2)** — now composed from `ProductInfo.ExeBaseName` |
| `PackageProjectUrl`, `RepositoryUrl` | `DS4WinWPF.csproj:29,31` | **DONE (1.2)** → `https://github.com/potpiemuncher/Thrum`. Package metadata only — `ProductInfo.ReleaseOwnerRepo` still points upstream until the update feed cuts over. |
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
| `Update Files\DS4Windows` cleanup path in the elevated updater batch | `DS4Control/Util.cs:305` | icons+updater PR |
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
| `GITHUB_RELEASES_API_URI`, `GITHUB_LATEST_RELEASE_API_URI` (`hbashton/DS4Windows`) | `DS4Control/ScpUtil.cs:3453,3454` | **DONE (1.1)** → `ProductInfo.ReleasesApiUri` / `LatestReleaseApiUri`; values flip in icons+updater PR |
| Latest-release URL used by `DownloadUpstreamVersionInfo` | `DS4Forms/ViewModels/MainWindowsViewModel.cs:863` | **DONE (1.1)** |
| `DS4Updater.exe` / `DS4Updater_x86.exe` (5 sites) | `MainWindowsViewModel.cs:800,824,834,908` | **DONE (1.1)** → `ProductInfo.UpdaterExeName` / `UpdaterExeNameX86` |
| `hbashton/DS4Updater` releases API + download URL | `MainWindowsViewModel.cs:804,833` | **DONE (1.1)** → `ProductInfo.UpdaterLatestReleaseApiUri` / `UpdaterReleasesPageUri` |
| `hbashton/DS4Updater` release-tag links (2) | `DS4Forms/MainWindow.xaml.cs:328,390` | **DONE (1.1)** → `ProductInfo.UpdaterReleasesPageUri` |
| `InstalledReleaseFileName = "DS4Windows.release"` | `DS4Control/ReleaseChannelPolicy.cs:11` | **DONE (1.1)** → `ProductInfo.InstalledReleaseFileName` (derived from `ExeBaseName`, so it flips with the assembly rename) |
| Project link `https://github.com/hbashton/DS4Windows` | `DS4Forms/About.xaml.cs:45` | **DONE (1.1)** → `ProductInfo.ProjectUri` |
| Contributors link `…/blob/main/contributors.txt` | `DS4Forms/About.xaml.cs:100` | **DONE (1.1)** → composed from `ProductInfo.ProjectUri` |
| HTTP `User-Agent: DS4Windows` (2) | `App.xaml.cs:621,638` | **DONE (1.1)** → `ProductInfo.HttpUserAgent` |
| `newest.txt` (contains `4.0.2.1`) | `DS4Windows/newest.txt` | **DEFER** to the version reset (plan task 1.9). Not read by any code in this tree — and `post-build.py` writes its copy to the *repository root*, never to this file, so the committed one is already dead. |
| `Changelog.json` | `DS4Windows/Changelog.json` (16 hits, stale `3.3.3` data) | icons+updater PR. **Not referenced by any code** — no reader exists in this tree; treat as a dead asset to replace or delete. |
| `hbashton/VIIPER` releases URL | `DS4Control/Viiper/ViiperSetupManager.cs:69` | **KEEP** — the backend's own repository |

---

## 7. Icons and artwork

| Anchor | Location | Disposition |
|---|---|---|
| `ApplicationIcon = DS4W.ico` | `DS4WinWPF.csproj:11` | icons+updater PR |
| Tray icon assets `DS4W.ico`, `DS4W - White.ico`, `DS4W - Black.ico` | `Resources/`, csproj `None`+`Resource` entries (6 lines) | icons+updater PR |
| `TrayIconChoice` → icon map (5 entries) | `DS4Control/ScpUtil.cs:1022–1029` | icons+updater PR (the prefix already comes from `ProductInfo`; only the file names change) |
| Battery tray icons `0.ico` … `100.ico` + `DS4W.ico` fallback | `DS4Forms/ViewModels/TrayIconViewModel.cs:505–516` | icons+updater PR (fallback only) |
| Tray tooltip text, balloon title and tray title | `DS4Forms/ViewModels/TrayIconViewModel.cs:32,34,35` | **DONE (1.2)** → `ProductInfo.ProductName` — **(found in 1.2)**. Three literals; without them the tray would still have introduced itself as DS4Windows after every other rename landed. |
| Shell header text and its `D` monogram | `DS4Forms/Themes/BridgeShellStyles.xaml:459` | **DONE (1.2)** — the header is bound to `{x:Static identity:ProductInfo.ProductName}` and the monogram letter is now `T`. A real logo lands with the icons pull request. |
| About header `"DS4Windows - hbashton Build (Version "` | `DS4Forms/About.xaml:12` | icons+updater PR |
| Controller artwork `DualShock 4 / DualSense / DualSense Edge / Switch 2 Pro Controller.png`, `DS4-Config_*.png`, `DS4 Config.png`, … | `Resources/` | **KEEP** — these name the *devices*, not the product, and are licensed per `Resources/ControllerArtwork.NOTICE.txt`. Covered by the new resource guard test. |
| `ds4winwpf_screen_20200412.png` (repo screenshot) | repo root | **DEFER** to the icons pull request. It is a screenshot of the old UI under the old brand, so renaming the file buys nothing; it gets replaced or deleted with the visual identity. |

---

## 8. Scripts, workflows and tooling

| Anchor | Location | Disposition |
|---|---|---|
| `Thrum.deps.json`, output dir rename to `Thrum`, zip name `Thrum_{version}_{arch}`, manifest `.thrum-managed-files.txt` | `utils/post-build.py:33,39,56,64` | **DONE (1.2)** |
| `re.compile(r"^DS4Windows/")` matching the entry assembly's `deps.json` library key | `utils/inject_deps_path.py:16` | **DONE (1.2)** — **(found in 1.2)**. The nastiest miss in the sweep: this script rewrites the app's own library `path` to `./`, and with a stale pattern it would have matched nothing, exited 0, and produced a package that only fails at launch. It now derives the assembly name from the `deps.json` filename, so it cannot go stale again. |
| Install path, release API, download URL, zip name, folder move, desktop shortcut, exe name (12 hits) | `ds4w.bat` | **DEFER**. Inert legacy helper: nothing in the build, the workflows, or the app references it. It gets rewritten or deleted in a later phase rather than half-flipped now. |
| Artifact name `Thrum_…`, packaged folder name, step summary text | `.github/workflows/ci-build.yml:85,91,98,102` | **DONE (1.2)**. The test-project and csproj *paths* are directory names and stay. |
| Zip asset names | `.github/workflows/release.yml:54,56` | **DONE (1.2)**. The csproj-parse and publish *paths* are directory names and stay. |
| Bug-report template product name and log file name | `.github/ISSUE_TEMPLATE/bug_report.md:25,30` | **DONE (1.2)** |
| `Thrum-VIIPER-Setup` temp dir and User-Agent (3 hits), plus 4 product-name mentions in the script’s own log and error text | `extras/install-viiper-backend.ps1:12,70,89,177,238,244,323` | **DONE (1.2)**. `RunVIIPER` and `%LOCALAPPDATA%\VIIPER` are untouched. |
| `!DS4Windows/libs/x64/`, `!DS4Windows/libs/x86/` | `.gitignore:333,334` | **KEEP** — project *directory* paths, not identity |
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

---

## 10. User-visible strings and translations

Bulk-counted, not enumerated. The localization PR owns all of it.

| Set | Files | Hits | Disposition |
|---|---|---|---|
| English `Translations/Strings.resx` | 1 | 14 | localization PR — replace the literal product name with a placeholder |
| English `Properties/Resources.resx` | 1 | 16 | localization PR |
| Non-English `.resx` (24 languages, both families) | 27 | 193 | localization PR — scripted conservative replace **only** where the token is the literal product name; log every skipped hit for translators |
| Generated `*.Designer.cs` | `Properties/Resources.Designer.cs`, `Translations/Strings.Designer.cs` | 34 | regenerated automatically from the `.resx`; never hand-edited |
| Hard-coded English UI text and log messages in `.cs` | `App.xaml.cs` (104 file-wide hits, of which the identity-bearing ones are now converted), `SettingsViewModel.cs` (83), `ControlService.cs` (28), and ~50 other files | — | localization PR — prose sentences such as "Copy complete, please relaunch DS4Windows…". Deliberately **not** touched in 1.1: they are sentences, not identity constants, and mechanically substituting them would produce a large, unreviewable diff. |
| Hard-coded English text in `.xaml` (tooltips, labels) | `MainWindow.xaml`, `WelcomeDialog.xaml`, `ViiperDebuggerWindow.xaml`, `ControllerRegisterOptionsWindow.xaml`, `About.xaml` | ~13 | localization PR |

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
2. **`Changelog.json`**. Nothing reads it. Replace with our own changelog feed
   in the icons+updater PR, or delete it.
3. **Namespace rename** (`DS4Windows` / `DS4WinWPF` → product namespaces). Not
   in Phase 1. It is pure churn against `upstream-track` and buys nothing a
   user can see; if it is ever done, it should be a single mechanical commit
   immediately after an upstream merge.
4. **`DS4WINDOWS_*` diagnostic environment variables** (five of them, listed in
   §4). Kept by the flip on the same reasoning as the OSC namespace. Decide
   whether to rename them to `THRUM_*`, or to accept both spellings for a
   release and then drop the old one. Either way it needs a test, since nothing
   in the tree currently proves a variable is still read.

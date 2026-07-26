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
`DS4W*` icon asset names, taken on the branch that introduced
`DS4Control/ProductInfo.cs`.

**Total raw hits: 1637.** Most of that total is not identity at all — see
[Bulk categories](#bulk-categories) — so the number to watch is the
per-category breakdown below, not the total.

## Disposition legend

| Tag | Meaning |
|---|---|
| **DONE (1.1)** | Now reads from `ProductInfo`; value unchanged. |
| **flip PR** | The commit that changes `ProductInfo`'s values, renames the assembly, and sweeps XAML/manifest/scripts. Covers plan tasks 1.2, 1.3, 1.5, 1.9. |
| **import-wizard PR** | Data-folder rename plus the one-time copy-import of the old configuration (plan task 1.4). |
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
| `AssemblyName` = `DS4Windows` | `DS4Windows/DS4WinWPF.csproj:10` | flip PR |
| `ASSEMBLY_RESOURCE_PREFIX` | `DS4Control/ScpUtil.cs:679` | **DONE (1.1)** → `ProductInfo.AssemblyResourcePrefix` |
| `RESOURCES_PREFIX` | `DS4Control/ScpUtil.cs:680` | **DONE (1.1)** → `ProductInfo.ResourcesPrefix` |
| `LANGUAGE_ASSEMBLY_NAME` | `DS4Control/ScpUtil.cs:683` | **DONE (1.1)** → `ProductInfo.LanguageAssemblyName` |
| `assemblyIdentity name="DS4Windows.app"` | `DS4Windows/app.manifest:3` | flip PR (XML, cannot consume a constant) |
| 4 XAML pack URIs `/DS4Windows;component/Resources/*.png` | `DS4Forms/ProfileEditor.xaml:100,107,114,121` | flip PR |
| 23 XAML `lex:ResxLocalizationProvider.DefaultAssembly="DS4Windows"` | 23 `DS4Forms/*.xaml` files | flip PR |
| `ThemeResourceTests` relative pack URIs (2) | `DS4WindowsTests/ThemeResourceTests.cs:23,29` | flip PR |
| `PackageProjectUrl`, `RepositoryUrl` | `DS4WindowsWPF.csproj:29,31` | icons+updater PR |
| Solution/project names `DS4WinWPF`, `DS4WindowsTests` | `DS4WindowsWPF.sln`, project files | **KEEP** — project/namespace identity, out of Phase 1 scope (no `RootNamespace` change) |
| `namespace DS4Windows` / `DS4WinWPF`, 272 declaration lines + 686 qualified `DS4Windows.Type` references | tree-wide `.cs` | **KEEP** — namespaces are not product identity; renaming them is a separate, purely cosmetic churn with a large conflict cost against `upstream-track` |

> **Note on `RootNamespace`.** The csproj sets `RootNamespace=DS4WinWPF` while
> `AssemblyName=DS4Windows`. Only `AssemblyName` feeds pack URIs. The flip PR
> changes `AssemblyName` alone.

---

## 3. Data locations

| Anchor | Location | Disposition |
|---|---|---|
| `%APPDATA%\DS4Windows` (`appDataPpath`) | `DS4Control/ScpUtil.cs:636` | **DONE (1.1)** → `ProductInfo.AppDataFolderName`; value flips in import-wizard PR |
| `%LOCALAPPDATA%\DS4Windows` (`localAppDataPpath`) | `DS4Control/ScpUtil.cs:637` | **DONE (1.1)** → `ProductInfo.LocalAppDataFolderName`; value flips in import-wizard PR |
| `%TEMP%\DS4Windows` diagnostic report folder | `DS4Control/Viiper/Validation/ViiperDriverValidationCommand.cs:50` | **DONE (1.1)** → `ProductInfo.TempFolderName` |
| `%TEMP%\DS4Windows.GameBarProbe.*.txt` | `DS4Control/GameBarIntegration.cs:829` | flip PR |
| `Update Files\DS4Windows` cleanup path in the elevated updater batch | `DS4Control/Util.cs:305` | icons+updater PR |
| `%LOCALAPPDATA%\VIIPER` install dir | `extras/install-viiper-backend.ps1:9` | **KEEP** — VIIPER ecosystem state, shared with upstream installs |

### Config file format — do not flip

| Anchor | Location | Disposition |
|---|---|---|
| XML root element `<DS4Windows>` | `[XmlRoot("DS4Windows")]` in `DS4Control/DTOXml/ProfileDTO.cs:34`; `WriteStartElement("DS4Windows")` in `DS4Control/ProfileMigration.cs:286,423,504`; `CreateElement("DS4Windows")` and the `SelectSingleNode("DS4Windows/...")` / `"/DS4Windows/Control"` XPaths in `ScpUtil.cs:1497,1507,4706,4707,4722,5787,5823` | **KEEP** — this is the on-disk file format, not the brand. Renaming it invalidates every existing profile, every exported profile users share, and the import wizard's source format. |
| `" Made with DS4Windows version {0} "` XML comments (4) | `ScpUtil.cs:4658,4716,8760,8820`, `OutputSlotPersist.cs:78` | flip PR — cosmetic, inside written files |
| `" DS4Windows Configuration Data. {0} "` XML comments (2) | `ScpUtil.cs:4657,4713` | flip PR — cosmetic |

---

## 4. Single instance and IPC

Getting these wrong is what makes a rebranded build hijack the original's
command-line IPC, or refuse to start beside it. The plan's acceptance criterion
"side-by-side run with real DS4Windows does not cross-talk" is exactly this
table.

| Anchor | Location | Disposition |
|---|---|---|
| Single-instance `EventWaitHandle` GUID `{a52b5b20-…}` | `App.xaml.cs:71` | **DONE (1.1)** → `ProductInfo.SingleInstanceEventName`; **new GUID required in flip PR** |
| `DS4Windows_IPCClassName.dat` MMF (create + open) | `App.xaml.cs:695,716` | **DONE (1.1)** → `ProductInfo.IpcClassNameMmfName` |
| `DS4Windows_IPCResultData.dat` MMF (create + open) | `App.xaml.cs:742,791` | **DONE (1.1)** → `ProductInfo.IpcResultDataMmfName` |
| `DS4Windows_IPCResultData_ReadyEvent` (create + open) | `App.xaml.cs:574,788` | **DONE (1.1)** → `ProductInfo.IpcResultDataReadyEventName` |
| `DS4Windows_IPCResultData_SingleTaskMtx` | `App.xaml.cs:559` | **DONE (1.1)** → `ProductInfo.IpcResultDataSingleTaskMutexName` |
| `FindWindow(className, "DS4Windows")` | `App.xaml.cs:540` | **DONE (1.1)** → `ProductInfo.WindowTitle` |
| Main window `Title="DS4Windows"` | `DS4Forms/MainWindow.xaml:17` | flip PR (XAML). **Mitigated now:** `MainWindow`'s constructor assigns `Title = ProductInfo.WindowTitle` with the identical value, so the `FindWindow` target and the real title already come from one constant. |
| WM_COPYDATA `-command` handler | `DS4Forms/MainWindow.xaml.cs:1522` (handler)  | no identity literal; the protocol shape is unchanged |
| OSC command word `"ds4windows"` and 26 OSC addresses `/ds4windows/monitor/...` | `DS4Control/ControlService.cs:324,361,2218,3110,4122,4155–4257` | **DECIDE** — external wire protocol consumed by user OSC setups. Default: **keep**, and document the address namespace as a compatibility surface. |
| `--ds4windows-gamebar-probe` CLI switch | `DS4Control/GameBarIntegration.cs:57` | flip PR — self-invoked only, no external consumer |
| Process-name comparison `"DS4Windows"` | `DS4Control/GameBarIntegration.cs:1114` | flip PR — **must** become `ProductInfo.ExeBaseName`; it is an exe-name coupling that no test currently covers |
| HidHide fallback `ExeName = "DS4Windows"` | `DS4Control/ControlService.cs:791` | flip PR |
| Worker thread names `"DS4Windows Game Bar API Poll"`, `"… UIA Poll"` | `DS4Control/GameBarIntegration.cs:449,554` | flip PR — cosmetic (debugger only) |

---

## 5. Startup, scheduled task, shortcut, log

| Anchor | Location | Disposition |
|---|---|---|
| Startup shortcut `DS4Windows.lnk` (2 sites) | `StartupMethods.cs:30,36` | **DONE (1.1)** → `ProductInfo.StartupShortcutName` |
| Same literal, **duplicated** outside `StartupMethods` | `DS4Forms/ViewModels/SettingsViewModel.cs:637` | **DONE (1.1)** — converted here too; it was an independent copy and would have been missed by a `StartupMethods`-only flip |
| Scheduled task `RunDS4Windows` (7 sites: find ×4, register, delete ×2, plus the `start` console title in `task.bat`) | `StartupMethods.cs:43,90,100,138,144,147,160,202` | **DONE (1.1)** → `ProductInfo.StartupTaskName` |
| NLog runtime file name `ds4windows_log.txt` + archive `ds4windows_log_{#}.txt` | `LoggerHolder.cs:42,43` | **DONE (1.1)** → `ProductInfo.LogFileName` / `LogArchiveFileName`. These are the *effective* names: the bootstrap overrides the config file. |
| NLog config placeholder `fileName="ds4windows_log.txt"` | `DS4Windows/NLog.config:8` | flip PR — **must be edited directly**. It is XML consumed by NLog before any managed identity constant is available, so it cannot delegate to `ProductInfo`. NLog requires the attribute to be present even though `LoggerHolder` replaces it. |
| Bug-report instruction naming `ds4windows_log.txt` | `.github/ISSUE_TEMPLATE/bug_report.md:25` | flip PR |
| VIIPER at-logon task `RunVIIPER` | `extras/install-viiper-backend.ps1` | **KEEP** — VIIPER's own task, shared with upstream DS4Windows installs. Uninstall may remove it only if we created it (ownership marker, plan task 5.4). |

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
| `newest.txt` (contains `4.0.2.1`) | `DS4Windows/newest.txt`, generated by `utils/post-build.py:49–51` | flip PR (version reset, plan task 1.9). Not read by any code in this tree. |
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
| Shell header text `"DS4Windows"` | `DS4Forms/Themes/BridgeShellStyles.xaml:459` | flip PR |
| About header `"DS4Windows - hbashton Build (Version "` | `DS4Forms/About.xaml:12` | icons+updater PR |
| Controller artwork `DualShock 4 / DualSense / DualSense Edge / Switch 2 Pro Controller.png`, `DS4-Config_*.png`, `DS4 Config.png`, … | `Resources/` | **KEEP** — these name the *devices*, not the product, and are licensed per `Resources/ControllerArtwork.NOTICE.txt`. Covered by the new resource guard test. |
| `ds4winwpf_screen_20200412.png` (repo screenshot) | repo root | flip PR (or delete — no longer referenced by the rewritten README) |

---

## 8. Scripts, workflows and tooling

| Anchor | Location | Disposition |
|---|---|---|
| `DS4Windows.deps.json`, output dir rename to `DS4Windows`, zip name `DS4Windows_{version}_{arch}`, manifest `.ds4windows-managed-files.txt` | `utils/post-build.py:33,39,56,64` | flip PR |
| Install path, release API, download URL, zip name, folder move, desktop shortcut, exe name (12 hits) | `ds4w.bat` | flip PR (also rename the script) |
| Test project path, publish path, artifact name `DS4Windows_…`, packaged folder name, step summary text (7 hits) | `.github/workflows/ci-build.yml:41,75,85,91,98,102` | flip PR |
| csproj parse path, publish path, zip names (5 hits) | `.github/workflows/release.yml:33,34,48,54,56` | flip PR |
| Bug-report template product name and log file name | `.github/ISSUE_TEMPLATE/bug_report.md:25,30` | flip PR |
| `DS4Windows-VIIPER-Setup` temp dir and User-Agent (3 hits) | `extras/install-viiper-backend.ps1:12,70,89` | flip PR |
| `!DS4Windows/libs/x64/`, `!DS4Windows/libs/x86/` | `.gitignore:333,334` | **KEEP** — project *directory* paths, not identity |
| Bezier editor web app strings + Ryochan7 wiki links (8 hits) | `DS4Windows/BezierCurveEditor/build.js`, `index.html` | **KEEP** for now — vendored third-party web app; the wiki links are upstream documentation. Revisit in the localization PR if the in-app text is reachable. |

---

## 9. Tests

113 hits across `DS4WindowsTests/`. Almost all are `using DS4Windows;` and
qualified type references, which stay. The identity-bearing ones:

| Anchor | Location | Disposition |
|---|---|---|
| 2 relative pack URIs | `ThemeResourceTests.cs:23,29` | flip PR |
| Profile XML fixtures containing `<DS4Windows>` roots | `ProfileTests.cs`, `ProfileMigrationTests.cs`, `AppSettingsTests.cs`, `MappingTests.cs` | **KEEP** — file-format fixtures, see §3 |
| Report formatter expectations | `ViiperDriverReportFormatterTests.cs` | flip PR if the report header text changes |
| New guard tests | `ProductIdentityTests.cs` | n/a — these enforce the rest |

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
| `Copyright` / `Authors` / `Company` csproj metadata | `DS4WinWPF.csproj:25–28` | Rewritten in the flip PR to add Thrum while keeping the upstream lineage line. |
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

# Plan progress log

One dated entry per implementation session: tasks completed, deviations from
the phased plan, and evidence. A task is not done until its listed
verification has run.

The phase and task numbers below refer to the phased plan held in the
maintainer's private workspace. That plan is not published in this repository;
this log is the public record of what was actually executed.

---

## 2026-07-25 — Phase 0 complete (bootstrap)

**Session scope:** Phase 0, tasks 0.1 through 0.7.

### Decisions recorded

- **D1 — Product name and repository visibility.** The product is **Thrum**.
  The repository is **public from creation**, not private-then-opened. The
  name satisfies the naming criteria set in the plan: no "DS4", "DualSense",
  "PlayStation", "Switch", Sony, or Nintendo marks; ASCII and safe as an
  executable name; not colliding with an existing project in this space.
- **D2 — Descriptor provenance.** **Adopted.** Thrum ships emulated Sony and
  Nintendo device identities the way the surrounding ecosystem already does,
  under a required discipline of provenance dossiers, SHA-256 hash
  inventories, and hard no-serial / no-PII redaction rules. Recorded in
  [ADR-0003](ADR-0003-descriptor-provenance.md).

### 0.1 — Preserve the driver-validation layer (done earlier this date)

Executed in the maintainer's private workspace clone before this repository
existed.

- Commit **`48b5952`** — "Add read-only VIIPER driver-validation diagnostic",
  on branch `feature/viiper-driver-validation-diagnostic`.
- Parent **`5d2724a`** = the upstream hbashton/DS4Windows `main` head at the
  time (v4.0.2.1-dualsense-beta).
- Size: **11 files, +4,145 insertions**, no deletions.
  - *Correction to the plan:* an earlier draft of the plan estimated this work
    at "~2,700 lines". The committed reality is 4,145 insertions across 11
    files. Later references should use the measured figure.
- Verification: full **x64 Release** test suite **514 passed / 0 failed**, run
  against exactly this tree state before the commit was made.
- Safety backup: git bundle `viiper-driver-validation-20260725.bundle`,
  SHA-256
  `9C7DED54CDE75DFDA758CC10F7312B998ACB122D9809C2E2F23D923B1CC37CF0`,
  retained in the maintainer's private workspace. Not committed here.
- Not rebased and not squashed: VM validation evidence references this exact
  tree.
- Note: the workspace clone had no git identity configured; a repository-local
  identity (`potpiemuncher` /
  `potpiemuncher@users.noreply.github.com`) had to be set before the commit
  would succeed. The same repository-local identity is set in the Thrum
  working clone.

### 0.3 — Repository created and history imported

- Repository: **<https://github.com/potpiemuncher/Thrum>**, **public**,
  created empty (no auto-initialised README, `.gitignore`, or license file —
  history comes from the import).
- Topics: `gamepad`, `controller`, `dualsense`, `dualshock4`, `joycon`,
  `haptics`, `wpf`, `dotnet`, `viiper`.
- Import: full history of hbashton/DS4Windows pushed from the workspace clone.
  No rewriting, no squashing, no force-push, **no tags pushed**.
  - `main` -> `5d2724a`
  - `feature/viiper-driver-validation-diagnostic` -> `48b5952`
- `main` became the default branch automatically as the first branch pushed.
- **Verification (Phase 0 acceptance criterion): CI is green on the unmodified
  import.** The repository's inherited `ci-build.yml` ran on the `main` import
  push and succeeded (test job with the upstream three-test filter, plus the
  x64 publish and packaging job).

### 0.3 (cont.) — Working clone and upstream tracking wired

- Working clone created from GitHub in the maintainer's private workspace.
- Repository-local git identity set: `potpiemuncher` /
  `potpiemuncher@users.noreply.github.com`.
- Remote added: `upstream = https://github.com/hbashton/DS4Windows.git`;
  fetched.
- Branch **`upstream-track`** created at `5d2724a` (the import base) and
  pushed to origin. It carries no Thrum commits, per
  [ADR-0002](ADR-0002-upstream-tracking.md). CI green on this branch as well.

### 0.3 (cont.) — Validation layer merged into `main`

- `git merge --no-ff origin/feature/viiper-driver-validation-diagnostic` into
  `main`.
- Merge commit **`4ce5f84`** — clean merge, no conflicts, 11 files / +4,145
  insertions, matching the branch exactly.
- The merge message records that the layer is read-only (no elevation, no
  device I/O, no install or teardown), that the full x64 Release suite was
  514/514 at exactly this tree, and that the safety bundle is retained
  privately.
- **CI green on the merge commit.**
- Direct pushes to `main` were used during bootstrap by design; branch
  protection was enabled afterwards (below) so that it did not block the
  bootstrap itself.

### 0.4 / 0.6 / 0.7 — Architecture decision records

- [ADR-0001 — Repository topology](ADR-0001-repo-topology.md) (task 0.4):
  independent repository with imported full history rather than a GitHub fork;
  VIIPER consumed as a pinned release binary and never vendored; backend
  contributions routed through the maintainers' separate VIIPER fork.
- [ADR-0002 — Upstream tracking policy](ADR-0002-upstream-tracking.md)
  (task 0.7): `upstream` remote, fast-forward-only `upstream-track` mirror,
  merge-never-rebase, monthly-and-before-every-release cadence, minimal-diff
  conflict resolution in engine files, and a ~15k-added-line divergence budget
  alarm outside `docs/`, `installer/`, and branding paths.
- [ADR-0003 — Descriptor provenance](ADR-0003-descriptor-provenance.md)
  (task 0.6): the D2 decision, its rationale, and its requirements.

### 0.5 — Governance files

- **`README.md`** — replaced in full. The inherited README advertised the
  upstream fork's downloads, update feed, support links, and a funding link;
  keeping it would have misrepresented this repository. The replacement states
  the product, a pre-alpha bootstrap status (no releases; still running under
  the internal DS4Windows identity until the rebrand phase), lineage and
  attribution, the driver-safety position, build-from-source instructions
  including the three-test CI filter and why it exists, and the
  GPL-3.0-or-later licence with corresponding-source terms. No CI badges, no
  download links, no funding links.
- **`SECURITY.md`** — new. Crash dumps contain kernel memory and must never be
  attached to public issues; private reporting via GitHub private
  vulnerability reporting; redacted source-level analysis is the sharing norm;
  one-paragraph summary of the known usbip-win2 experimental status.
- **`CONTRIBUTING.md`** — replaced (see deviations: the inherited file was
  lowercase `contributing.md` and was renamed). Prerequisites, canonical x64
  Release build and test invocation, pull-request discipline, the VM-only rule
  for kernel-driver work, the minimal-diff policy for engine files, the no-PII
  rule for committed content, and GPL header and attribution preservation.
- **`NOTICE.txt`** — audited and extended (see deviations). A "To audit"
  section was appended listing every required entry that is not yet documented,
  rather than fabricating licence text for entries that have not been verified
  against the distributed artifacts.

### Deviations from the plan

1. **`THIRD-PARTY-NOTICES.txt` does not exist in this tree.** The equivalent
   inherited file is `NOTICE.txt`, and it is incomplete: it covers Crc32, Font
   Awesome, the 1€ Filter, vJoyInterfaceCS, RNNoise and its .NET wrapper, and
   the Switch 2 Pro artwork adaptation. Missing from it are VIIPER, usbip-win2,
   HidHide, NAudio, Concentus, the vendored SbcSharp, the bundled
   FakerInputWrapper and SharpOSC binaries, WPFLocalizeExtension,
   H.NotifyIcon, MdXaml, Ookii.Dialogs.Wpf,
   DotNetProjects.Extended.Wpf.Toolkit, a cross-reference to the controller
   artwork notice, and the egormanga (SAxense) and awalol (DS5Dongle) protocol
   research credits. Per the plan's instruction not to fabricate notices, these
   were appended to `NOTICE.txt` as an explicit "To audit" section that
   asserts nothing about their licence terms. **The audit itself remains
   open** and must close before the first release.
2. **No `.github/FUNDING.yml` existed**, so there was nothing to remove. The
   inherited README did carry a personal funding link; it is gone with the
   README replacement.
3. **`contributing.md` was lowercase.** Renamed with `git mv` to
   `CONTRIBUTING.md` so GitHub recognises it as the contribution guide, then
   rewritten. Recorded here because it is a case-only rename and can surprise
   contributors on case-insensitive filesystems.
4. **ADR numbering collision.** The plan referred to an installer-technology
   ADR as "ADR-0003" in a later phase, while the descriptor-provenance ADR was
   also assigned 0003. Descriptor provenance holds the number, as instructed;
   the installer ADR takes the next free number when written. Noted in
   ADR-0003 itself.
5. **`upstream-track` starts behind current upstream, deliberately.** It was
   created at the import base `5d2724a` as instructed, but upstream `main` had
   already advanced **4 commits** to `8a2b715` by import time (`db21d7e`,
   `fac5467`, `3937d26`, `8a2b715` — all VIIPER installer hardening). So the
   first ADR-0002 merge cycle has real work waiting on day one; this is not a
   drift problem, just a starting offset.
6. **`docs/dev/` is new.** The tree already contains an unrelated inherited
   `doc/dev/` (singular) with translation and profile-version notes. Thrum's
   own developer documentation lives in `docs/dev/`; the inherited directory
   was left untouched. Worth consolidating in a later documentation pass.
7. **No global git configuration was modified.** A repository-local credential
   helper delegating to the GitHub CLI was set in the workspace clone and in
   the Thrum clone, so that pushes authenticate without changing the
   maintainer's machine-wide git settings.
8. **Tags were not pushed.** The source clone holds 21 inherited tags; none
   were pushed to the new repository, per instruction. Thrum's own tags start
   fresh at its first release.

### Branch protection and repository settings

Applied **after** all bootstrap pushes, so they did not have to fight it.

- `main` protected: force-pushes disabled, deletion disabled, pull requests
  required with **0 required approving reviews** (a solo maintainer must be
  able to merge their own pull requests), admins not enforced, no push
  restrictions, no required status checks yet.
- Required status checks are deliberately deferred to the CI/CD phase, once
  the check names are confirmed against the reworked workflows. Until then a
  required check naming a job that does not exist would deadlock every merge.
- Private vulnerability reporting: enabled (see the verification section of
  this entry's session report for the exact result).

### Next steps

- **Phase 1 — Rebrand without breakage.** Kick off with task 1.1: the identity
  map (`docs/dev/identity-map.md`) plus a `ProductInfo` single source of truth,
  landed as a pure mechanical refactor first and a value flip second so each
  commit is reviewable on its own. The identity anchors are already enumerated
  in the plan; re-verify every line number with grep before editing, since
  they drift.
- **[EXT] Upstream offers**, per the audit's contribution sequence, tracked
  here as they move:
  - File the maintainer-facing driver-safety proposal issue on
    hbashton/DS4Windows.
  - Offer the read-only driver-validation diagnostic as a pull request
    (adapted to upstream naming).
  - Offer installer verification hardening once package policy is agreed.
  - hbashton/VIIPER pull request #3 (release artifact stamping) is open and
    awaiting maintainer approval to run its workflows; VIIPER release pinning
    depends on it, with a locally verified copy as the interim fallback.
  - usbip-win2 issue #181 (request-lifetime race) is filed; watch for
    maintainer response before considering the optional driver-fix track.
- Close the `NOTICE.txt` audit opened in deviation 1.

---

## 2026-07-25 — Phase 1.1 (identity map + `ProductInfo`, pure refactor)

**Session scope:** Phase 1, task 1.1. Branch `phase1/identity-productinfo`.

**Intent: zero behaviour change.** Every string value in this change is
byte-identical to the one it replaced. The application still presents itself as
DS4Windows in every respect — same window title, same data folders, same
scheduled task, same IPC object names, same update feed. What changed is where
those strings are written down.

### Identity map

[`docs/dev/identity-map.md`](identity-map.md) — a categorised inventory of every
occurrence of the product identity, with a disposition for each: which later
Phase 1 pull request flips it, or why it is deliberately kept.

Sweep basis: `git grep -in "ds4windows"` (1,637 raw hits) plus a targeted sweep
for the `DS4W*` icon assets. The raw total is misleading: 272 hits are
`namespace`/`using` declaration lines and 686 are qualified type references
(`DS4Windows.Global` and friends), so 59% of the total is namespace plumbing
that no Phase 1 pull request touches. The remaining categories are counted in
the map: 223 `.resx` translation hits across 29 files, 113 in tests, 88 in
documentation, 40 in XAML (23 of them `DefaultAssembly`, 4 pack URIs), 34 in
generated designer files, and the identity constants themselves.

Three findings from the sweep were not in the plan's anchor table and are worth
calling out:

1. **The XML root element of every config and profile file is `<DS4Windows>`**
   (`[XmlRoot("DS4Windows")]`, `WriteStartElement`, and a set of XPaths). This is
   the on-disk file format, not the brand. It is marked KEEP: flipping it would
   invalidate every existing profile, every profile users have exported and
   shared, and the source format the import wizard has to read.
2. **The OSC integration exposes `/ds4windows/monitor/...` addresses** (26 of
   them) and accepts `ds4windows` as a command word. That is an external wire
   protocol that user setups bind to. Recorded as an open decision; default is
   to keep it and document it as a compatibility surface.
3. **`SettingsViewModel` held its own private copy of the `DS4Windows.lnk`
   startup-shortcut literal**, independent of `StartupMethods`. A flip that
   only edited `StartupMethods` would have left the settings checkbox reading
   the wrong file. Converted in this change.

### `ProductInfo`

New `DS4Windows/DS4Control/ProductInfo.cs` (namespace `DS4Windows`), 31 public members
covering product name, window title, executable base name, the three data-folder
names, the single-instance event, four IPC object names, the scheduled task and
startup shortcut, the log file and its archive pattern, the release repository
and its API/page URLs, the updater repository and executable names, the two pack
URI prefixes, the satellite assembly name, the installed-release marker, the
HTTP user agent, and the diagnostic window title. Every member carries an XML
doc stating what breaks if it stops matching its consumer.

**Every member is a compile-time constant**, composed from `ProductName` /
`ExeBaseName` where the value is genuinely derived (for example
`AssemblyResourcePrefix = "pack://application:,,,/" + ExeBaseName + ";"`). Two
consequences: the existing `const` fields in `Global`, `Changelog`,
`ReleaseChannelPolicy` and `ViiperDriverValidationCommand` keep delegating
without any `const` → `static readonly` conversion, and the composed values
cannot drift from their parts.

**No `const` was converted to `static readonly`.** This was checked before
choosing the design: `ASSEMBLY_RESOURCE_PREFIX` (35 call sites),
`RESOURCES_PREFIX` (44), `LANGUAGE_ASSEMBLY_NAME` (1) and
`InstalledReleaseFileName` (1) are all used in string interpolation or as
ordinary arguments — none in an attribute argument, a `case` label, or a default
parameter value. The all-`const` design made the question moot, but the check
also found the one place where `const`-ness is genuinely load-bearing:
`[XmlRoot("DS4Windows")]` on `ProfileDTO`, which is an attribute argument (and
is KEEP anyway, per the file-format finding above).

### Consumers converted

- `ScpUtil.cs` — `appDataPpath`, `localAppDataPpath`, the
  `ASSEMBLY_RESOURCE_PREFIX` / `RESOURCES_PREFIX` / `LANGUAGE_ASSEMBLY_NAME`
  trio, `Changelog.GITHUB_RELEASES_API_URI` and `GITHUB_LATEST_RELEASE_API_URI`.
  The public names are unchanged, so no call site churned.
- `App.xaml.cs` — single-instance event GUID, all four IPC object names at both
  create and open sites, the `FindWindow` window-title target, the two HTTP
  user-agent headers, and the four message-box captions that were exactly the
  product name.
- `MainWindow.xaml.cs` — the constructor now assigns
  `Title = ProductInfo.WindowTitle` (identical value) so that the window title
  and the `-command` client's `FindWindow` target provably come from one
  constant; plus the two updater release-tag links.
- `StartupMethods.cs` — startup shortcut path (2), scheduled task name (7
  sites, including the `start` console title written into `task.bat`).
- `SettingsViewModel.cs` — the duplicated startup-shortcut literal.
- `MainWindowsViewModel.cs` — updater executable names, updater releases API and
  download URLs, product latest-release API URL.
- `ReleaseChannelPolicy.cs` — `InstalledReleaseFileName`.
- `ViiperDriverValidationCommand.cs` — `%TEMP%` report directory, report window
  title, and the failure-message product name.
- `About.xaml.cs` — project and contributors links.
- `LoggerHolder.cs` — the log file name and archive pattern. These are the
  *effective* names: the bootstrap overrides whatever `NLog.config` declares.

### Consumers deliberately deferred

- **`NLog.config`** — XML read by NLog before any managed constant exists. It
  keeps a `ds4windows_log.txt` placeholder that NLog requires the attribute to
  carry; `LoggerHolder` replaces it at startup. The flip pull request edits the
  file directly. Recorded in the identity map.
- **All XAML** — per the task's constraint, no XAML was touched. The 4 pack
  URIs, 23 `DefaultAssembly` attributes, the `MainWindow` `Title` and the
  About-box header flip in the assembly-rename pull request.
- **`csproj` / `app.manifest` / `.sln`** — `AssemblyName`, `ApplicationIcon`,
  `assemblyIdentity`. Same pull request.
- **Prose and log messages** (sentences such as "Copy complete, please relaunch
  DS4Windows…"). Substituting these mechanically would produce a large,
  unreviewable diff and they belong with the string sweep. Deferred to the
  localization pull request.
- **`utils/post-build.py`, `ds4w.bat`, `.github/workflows/*`, the issue
  template, `extras/install-viiper-backend.ps1`'s `DS4Windows-VIIPER-Setup`
  strings** — build and packaging tooling, flipped alongside the assembly
  rename.
- **`newest.txt`, `Changelog.json`** — data files. `Changelog.json` turns out to
  have no reader anywhere in the tree; noted as an open decision.
- **VIIPER ecosystem names** (`RunVIIPER`, `%LOCALAPPDATA%\VIIPER`,
  `hbashton/VIIPER`) and **upstream attribution** (GPL headers, Ryochan7 and
  schmaldeo links, the ds4windows-site documentation links) — KEEP.

### Guard tests

New `DS4WindowsTests/ProductIdentityTests.cs`, 10 tests:

- `ExeBaseNameMatchesTheApplicationAssemblyName` — reflects on the app assembly
  and asserts `ProductInfo.ExeBaseName` equals its real name. This is the test
  that fails if the csproj `AssemblyName` and `ProductInfo` are ever changed
  independently.
- Three composition assertions for `AssemblyResourcePrefix`, `ResourcesPrefix`
  and `LanguageAssemblyName`.
- `GlobalIdentityConstantsDelegateToProductInfo` — catches a future edit that
  re-hardcodes one of the six aliases.
- `IpcObjectNamesAreDistinctAndNamespaced` — the four IPC object names must be
  distinct and prefixed by the product name, which is what lets a rebranded
  build coexist with the original.
- Four resource-existence tests using `Application.GetResourceStream`: every
  `TrayIconChoice` mapping, all eleven battery tray icons, the four controller
  artwork PNGs, and one absolute-prefix pack URI.

The resource tests deliberately do **not** construct a `System.Windows.
Application`: WPF permits only one per process for the process's lifetime and
`ThemeResourceTests` already creates and shuts one down, so a second instance
would throw. They resolve pack URIs on an STA thread without one, which works.

**Negative control run:** with `ProductInfo.ExeBaseName` temporarily set to
`"Thrum"`, 5 of the 10 tests failed — the reflection guard plus all four
resource tests (`FileNotFoundException: Could not load file or assembly
'Thrum'`). The guards are not vacuous. The value was restored and the file
verified byte-identical before committing.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — succeeded, 17
  warnings, all pre-existing.
- Full suite with the repository's CI invocation
  (`dotnet test .\DS4WindowsTests\DS4WindowsTests.csproj -c Release
  -p:Platform=x64 --filter "Name!=CheckSettingsSave&Name!=CheckWriteProfile&
  Name!=CheckJaysProfileRead"`): **524 passed / 0 failed**, up from the 514
  baseline by exactly the 10 new tests.
- The GUI application was not launched (task 1.1 needs no runtime smoke test;
  the IPC and startup paths get theirs in the flip pull request, where the
  values actually change).

### Deviations from the plan

1. **`MainWindow.xaml.cs` gained a `Title` assignment.** The plan's constraint
   was no XAML edits, and the `FindWindow` title dependency is one of the
   listed anchors. Assigning `Title = ProductInfo.WindowTitle` in the
   constructor — the identical value — makes the window title and the IPC
   client's search string provably one constant without touching XAML. The XAML
   `Title="DS4Windows"` remains and is now redundant; the flip pull request
   removes it.
2. **Two consumers outside the task's list were converted.**
   `SettingsViewModel.cs:637` (the duplicated startup-shortcut literal, for the
   reason given above) and `About.xaml.cs` (project and contributors links,
   two lines, and the natural home for `ProductInfo.ProjectUri`).
3. **`NLog.config` was not made programmable** because it already is: the
   bootstrap in `LoggerHolder` overrides both the file name and the archive
   pattern, and the config file's value is a placeholder NLog demands. So the
   effective names now come from `ProductInfo` and only the placeholder is left
   for the flip pull request. This is the outcome the task asked for, reached by
   a shorter route than expected.
4. **No `const` → `static readonly` conversions were needed** (see above).
5. **Plan line numbers had drifted**, as the plan warned. The identity map cites
   verified post-change line numbers; the notable corrections against the plan's
   Part 2 table are `ScpUtil.cs` 677/678/681 → 679/680/683 and the
   `MainWindow.xaml.cs` WM_COPYDATA handler at 1522, not ~1518.

### Next steps

- **Phase 1.2** — the assembly and resource rename, as one atomic commit:
  `AssemblyName` → `Thrum`, the XAML sweep, `app.manifest`, and the
  post-build script's `Lang/` handling. The identity map's "flip PR" rows are
  the checklist; `ProductIdentityTests` is the safety net.
- Before that flip, decide the two open items the map records: the OSC address
  namespace, and what happens to the unreferenced `Changelog.json`.

---

## 2026-07-25 — Phase 1.2 + 1.3 (the atomic rename to Thrum)

**Session scope:** Phase 1, tasks 1.2 and 1.3, plus the identity values they
unlock from task 1.5. Branch `phase1/rename-flip`, one commit.

**Intent: this is the behaviour change.** Task 1.1 moved every identity string
into `ProductInfo` without altering a single value. This entry is the other
half: the values change, the assembly is renamed, and the application stops
presenting itself as DS4Windows to Windows, to itself, and to its own build
output. Nothing here is a refactor; every line is a deliberate change of what
the product is called.

### What flipped

| Surface | Before | After |
|---|---|---|
| Assembly / executable | `DS4Windows.exe` | `Thrum.exe` |
| Satellite assemblies | `DS4Windows.resources.dll` | `Thrum.resources.dll` |
| Pack URI authority | `/DS4Windows;component` | `/Thrum;component` |
| `assemblyIdentity` | `DS4Windows.app` | `Thrum.app` |
| Window title | `DS4Windows` | `Thrum` |
| `%APPDATA%` / `%LOCALAPPDATA%` / `%TEMP%` folder | `DS4Windows` | `Thrum` |
| Scheduled task | `RunDS4Windows` | `RunThrum` |
| Startup shortcut | `DS4Windows.lnk` | `Thrum.lnk` |
| Log file / archive | `ds4windows_log.txt` / `ds4windows_log_{#}.txt` | `thrum_log.txt` / `thrum_log_{#}.txt` |
| Installed-release marker | `DS4Windows.release` | `Thrum.release` |
| Game Bar probe switch | `--ds4windows-gamebar-probe` | `--thrum-gamebar-probe` |
| Package folder / zip / artifact | `DS4Windows`, `DS4Windows_{v}_{arch}` | `Thrum`, `Thrum_{v}_{arch}` |
| Managed-files manifest | `.ds4windows-managed-files.txt` | `.thrum-managed-files.txt` |

### Instance and IPC namespace (task 1.3)

The point of this table is coexistence: a Thrum install and a real DS4Windows
install must not see each other. Every name below is now distinct from
upstream's.

| Object | New name |
|---|---|
| Single-instance `EventWaitHandle` | `{21c16c88-2c23-4389-91a1-e6613bab7255}` |
| Class-name MMF | `Thrum_IPCClassName.dat` |
| Result-data MMF | `Thrum_IPCResultData.dat` |
| Result-ready event | `Thrum_IPCResultData_ReadyEvent` |
| Result single-task mutex | `Thrum_IPCResultData_SingleTaskMtx` |
| `FindWindow` title target | `Thrum` |

The GUID was generated fresh for this change and hard-coded. It deliberately
differs from the inherited `{a52b5b20-d9ee-4f32-8518-307fa14aa0c6}`; sharing it
would have made a second product refuse to start, or hand its window to the
wrong process. The four IPC names are composed from `ProductInfo.ProductName`,
so they moved as a set, and `IpcObjectNamesAreDistinctAndNamespaced` asserts
they stay distinct and prefixed.

`MainWindow.xaml`'s `Title` attribute was **removed** rather than re-spelled.
The constructor's `Title = ProductInfo.WindowTitle` is now the only source, so
the running window's title and the `-command` client's `FindWindow` target
cannot drift apart by construction.

The `-command` protocol shape is unchanged: same WM_COPYDATA handler, same
verbs, same result-MMF layout. Only the object names moved.

### The fragile cluster (task 1.2)

- **`AssemblyName`** → `Thrum`, together with `ProductInfo.ExeBaseName`. These
  two are what `ExeBaseNameMatchesTheApplicationAssemblyName` pins to each
  other; changing one alone fails CI, which is the entire reason that test
  exists.
- **23 `lex:ResxLocalizationProvider.DefaultAssembly`** attributes across 23
  XAML files. This is the highest-risk line item in the change and the one with
  the worst failure mode: a missed attribute does not fail the build or any
  test, it silently kills localization on exactly one page, at runtime, for
  users who are not running English. All 23 were swept and the count verified.
- **4 pack URIs** in `ProfileEditor.xaml` (`/DS4Windows;component/Resources/*`).
- **`app.manifest`** `assemblyIdentity`, and **`NLog.config`**'s file-name
  placeholder — both XML that is read before any managed constant exists, so
  both had to be edited by hand.
- **`ThemeResourceTests`**' two relative pack URIs now compose from
  `ProductInfo.ExeBaseName` instead of naming the assembly literally.
- **`BridgeShellStyles.xaml`**'s shell header is now bound to
  `{x:Static identity:ProductInfo.ProductName}`, and its `D` monogram is a `T`.
  A real logo is the icons pull request's job; leaving a `D` next to "Thrum"
  was not an option.

### Satellite assembly mechanism (verified, not assumed)

The chain that has to survive an assembly rename, end to end:

1. The csproj sets no `SatelliteResourceLanguages`, so MSBuild emits every
   culture as `<culture>/<AssemblyName>.resources.dll`. After the rename that
   is `Thrum.resources.dll` — confirmed in the publish output.
2. `utils/post-build.py` moves each culture folder under `Lang/`.
3. `DS4Windows/runtimeconfig.template.json` declares
   `additionalProbingPaths: ["./Lang/"]`, which the SDK emits into
   `Thrum.runtimeconfig.json`. **This is the mechanism** — there is no custom
   `AssemblyResolve` handler anywhere in the tree, and the template contains no
   assembly name, so it is inherently rename-safe.
4. `Global.PROBING_PATH` (`"Lang"`) and `Global.LANGUAGE_ASSEMBLY_NAME` are used
   only by `LanguagePackViewModel` to *enumerate* installed language packs for
   the settings dropdown. The latter already composes from
   `ProductInfo.LanguageAssemblyName`, so it followed the rename automatically.

Verified in the publish output: 31 `Thrum.resources.dll` files, and 23 culture
folders under `Lang/` after packaging.

**Smoke item (runtime, not unit-verifiable):** actually switching the UI
language in the running app and confirming the strings change. The build and
packaging halves are proven; the load half needs the GUI.

### Build tooling

`utils/inject_deps_path.py` was the most dangerous find of the session and was
**not** in the identity map. It rewrites the entry assembly's library `path` to
`./` inside `deps.json`, matching it with a hard-coded `re.compile(r"^DS4Windows/")`.
After the rename that pattern matches nothing, the script exits 0, and the
package is broken in a way no build step reports — it only shows up when the
application is launched. It now derives the assembly name from the `deps.json`
filename it was handed, so it cannot go stale again. Verified against the real
output: the pattern matched `Thrum/4.0.2.1-dualsense-beta` and set its path.

`utils/post-build.py`, `.github/workflows/ci-build.yml` and
`.github/workflows/release.yml` were updated for the deps.json name, the
package folder name, the zip and artifact names, and the run-summary text. The
*directory* paths (`.\DS4Windows\DS4WinWPF.csproj`,
`.\DS4WindowsTests\DS4WindowsTests.csproj`) are project folders, not identity,
and stay.

### Tests

- `ViiperDriverReportFormatterTests` asserted the literal report header
  `"DS4Windows VIIPER driver validation"` and a literal `%TEMP%\DS4Windows\…`
  fixture path. Both now compose from `ProductInfo`, and the formatter builds
  the header from `ProductInfo.ProductName`, so the assertion and the
  production string can never disagree again.
- One test added: `LowerInvariantExeBaseNameMatchesExeBaseName`. The Game Bar
  probe switch needs a lower-case token, `ToLowerInvariant()` is not
  constant-foldable, so `ProductInfo.ExeBaseNameLowerInvariant` has to be
  spelled out — and nothing but a test keeps it honest. This is why the suite
  total is 525 rather than the 524 the plan predicted.
- Two test-local temp file names (`ds4windows-dualsense-trace-*.wav`) renamed;
  scratch files with no coupling.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **succeeded, 0
  errors**, 17 warnings, all pre-existing and identical to the 1.1 baseline.
- CI's exact publish invocation
  (`dotnet publish .\DS4Windows\DS4WinWPF.csproj -c Release /p:platform=x64 -o .\bin\x64\Release\output`)
  — succeeded. Output contains `Thrum.exe`, `Thrum.dll`, `Thrum.deps.json`,
  `Thrum.runtimeconfig.json` and 31 `Thrum.resources.dll` satellites.
  **Zero files matching `DS4Windows*` or `*ds4w*` anywhere in the tree.**
- CI's packaging step (`python .\utils\post-build.py …`) — succeeded, produced
  `bin\x64\Release\Thrum\` and `Thrum_<version>_x64.zip`, with 23
  `Thrum.resources.dll` under `Lang/` and `.thrum-managed-files.txt` present.
  The workflow's `Copy-Item` source path and artifact path both still exist.
- Full suite with the repository's CI filter: **525 passed / 0 failed.** All
  ten pre-existing guard tests pass on the flipped values — including the five
  that PR #1 recorded as failing until `AssemblyName` matched `ExeBaseName`.
  They were the completion detector for this change and they are green.
- `git grep -in "ds4windows"` re-sweep: 1,644 hits, every one classified
  against the identity map (see below). The count rose from 1,637 because this
  change *added* explanatory prose to the map and to `ProductInfo`, while the
  identity literals themselves went away.
- The GUI application was not launched.

### Leftover audit

Every remaining hit falls in a KEEP or DEFER category:

**KEEP — not product identity.** Namespace plumbing (242 declarations + 612
qualified type references, 52% of the total); GPL headers and lineage
attribution (200); the `<DS4Windows>` config XML root element and its XPaths
(26); the OSC address namespace and command word (33); project, solution and
directory paths (70); the vendored Bezier editor web app (6).

**KEEP — decided during this change.** The five `DS4WINDOWS_*` diagnostic
environment variables, and the two `DS4Windows:AudioHaptics*` pseudo-endpoint
prefixes. Both are covered under "new anchors" below.

**DEFER.** `.resx` translated values (188) and generated designer files (29) to
the localization pull request; `.cs` and `.xaml` English prose (112) likewise;
updater executable names, release-feed URLs, `newest.txt`, `Changelog.json` and
the About-box header (16) to the icons+updater pull request; `ds4w.bat` (7) to
whichever later phase rewrites or deletes it.

**DOC.** 84 hits in repository documentation, including this log and the
identity map itself.

### New anchors found by the re-sweep

Eight anchors the 1.1 sweep missed. All are now recorded in the identity map,
marked **(found in 1.2)**.

1. **`utils/inject_deps_path.py`'s hard-coded assembly name** — described
   above. Fixed, and made self-deriving.
2. **A duplicated `%APPDATA%` folder literal** in
   `DualShock4BluetoothSpeakerPassthrough.cs`, which built
   `%APPDATA%\DS4Windows\Logs` directly instead of going through
   `Global.appDataPpath`. A `ScpUtil`-only flip would have left Bluetooth audio
   diagnostic dumps in the old product's folder. Fixed. (It still ignores
   portable mode — a pre-existing bug, deliberately left alone.)
3. **Tray tooltip, balloon title and tray title** — three literals in
   `TrayIconViewModel`. Without them the tray would have kept introducing
   itself as DS4Windows after every other rename landed. Fixed.
4. **Five message-box captions** outside `App.xaml.cs`. The 1.1 pass converted
   the four captions in that one file but never swept the tree for the same
   pattern. Fixed.
5. **Five `DS4WINDOWS_*` diagnostic environment variables** —
   `…_DUALSENSE_PCM_TRACE_DIRECTORY`, `…_DS4_AUDIO_DRIFT_MODE`,
   `…_DS4_AUDIO_TRANSPORT_MODE`, `…_DS4_AUDIO_DIAGNOSTIC_CAPTURE`,
   `…_VIIPER_STATE_RATE_HZ`. **Kept**, on the same reasoning as the OSC
   namespace: they are an external control surface a human sets before
   launching, renaming them invalidates every debugging runbook that names
   them, and no test in the tree would catch a mistake. Recorded as open
   decision 4 in the map.
6. **Two audio pseudo-endpoint prefixes**, `DS4Windows:AudioHapticsApp:` and
   `DS4Windows:AudioHapticsAuto:`. These are not display strings: the composed
   identifier is persisted as a profile's capture-source setting, which makes
   them on-disk file-format values in the same sense as the `<DS4Windows>` root
   element. **Kept** — flipping them would silently reset every per-app
   audio-haptics capture selection.

### Deviations from the plan

1. **`PackageProjectUrl` and `RepositoryUrl` moved in this change**, although
   the identity map assigned them to the icons+updater pull request. They are
   package metadata with no runtime consumer, and leaving a freshly renamed
   assembly pointing at `hbashton/DS4Windows` as its repository was not worth a
   second pull request. `ProductInfo.ReleaseOwnerRepo` — the value that
   actually drives update checks — is untouched and still points upstream,
   with a doc comment saying why.
2. **`ds4w.bat` was not flipped**, although the identity map listed it under
   the flip pull request. Nothing in the build, the workflows, or the
   application references it; half-renaming a dead script would produce a file
   that is neither working nor honestly legacy. Re-categorised as DEFER in the
   map.
3. **`ds4winwpf_screen_20200412.png` was not renamed**, same reasoning: it is a
   screenshot of the old UI under the old brand, so it is replaced or deleted
   with the visual identity, not renamed now. Re-categorised as DEFER.
4. **`newest.txt` was left alone**, per the task's deferral to the version
   reset. Worth recording *why* it does not matter: `post-build.py` writes its
   copy to the repository root, not to `DS4Windows/newest.txt`, so the
   committed file is already dead and no code in the tree reads it.
5. **One test was added**, making the suite 525 rather than the predicted 524.
   Reason under "Tests" above.
6. **`ProductInfo` gained a 32nd member**, `ExeBaseNameLowerInvariant`, for the
   same reason.
7. **Four extra product-name mentions were flipped in
   `extras/install-viiper-backend.ps1`** beyond the three the map listed. They
   are the script's own log and error text, including "Launch setup from
   DS4Windows so Windows can request it automatically" — an instruction that
   would have been simply wrong after the rename. The VIIPER ecosystem names in
   that script (`RunVIIPER`, `%LOCALAPPDATA%\VIIPER`, the release URL) are
   untouched, as required.
8. **The identity map was updated in this change**, not left as a historical
   snapshot. It is described in its own header as the checklist for the
   rebrand, so leaving 25 rows saying "flip PR" after the flip would make it
   misleading. Every completed row now carries its outcome, the eight new
   anchors are added, and three rows were re-categorised as DEFER with reasons.

### Smoke items queued for the rebrand smoke script

None of these are unit-testable; they need the GUI and, in one case, a real
DS4Windows install.

- `-command query.appversion` against a running instance, and second-instance
  forwarding, on the new IPC names.
- Side-by-side run with a real DS4Windows install: neither hijacks the other's
  single-instance event or `-command` IPC.
- UI language switch, proving `Lang/<culture>/Thrum.resources.dll` actually
  loads.
- Scheduled task `RunThrum` and startup shortcut `Thrum.lnk` create, detect and
  delete correctly.
- HidHide whitelist registration under the new executable name.
- Log file appears as `thrum_log.txt`.

### Next steps

- **Phase 1.4 — import wizard.** The data folders now point at
  `%APPDATA%\Thrum`, so an existing DS4Windows user currently sees an empty
  configuration. The one-time copy-import from `%APPDATA%\DS4Windows` is the
  next pull request and should not wait.
- **Phase 1.6 / 1.7 — icons and update feed.** `ApplicationIcon` is still
  `DS4W.ico` and `ProductInfo.ReleaseOwnerRepo` still points at
  `hbashton/DS4Windows`. The updater cutover matters for safety, not just
  branding: the inherited `DS4Updater.exe` path would install DS4Windows over
  Thrum.
- **Phase 1.8 — string sweep**, the 188 `.resx` hits plus the 112 prose hits.
- **Phase 1.9 — version reset**, which also disposes of `newest.txt`.
- Decide open decision 4 (the `DS4WINDOWS_*` environment variables) and the two
  older open items the map still carries.

---

## 2026-07-25 — Phase 1.4 + 1.5 remainder (settings import, startup-name and HidHide audits)

**Session scope:** Phase 1, task 1.4 in full, plus the audits that close out
task 1.5. Branch `phase1/import-and-startup`.

**The problem this solves.** The rename moved the data folder to
`%APPDATA%\Thrum`, so from the moment PR #2 merged, anyone with an existing
DS4Windows configuration starts this product with nothing: no profiles, no
auto-profile rules, no controller settings. That is the single worst first
impression the rebrand could make, and it is entirely mechanical to fix,
because the file format did not change with the name.

### What the importer is, and what it deliberately is not

It copies files. That is the whole design, and it is a decision rather than a
shortcut: the profile and settings XML is byte-compatible between the two
products (the root element is still `<DS4Windows>`, kept as a **file format**
per the identity map), and the loader already runs `ProfileMigration` and
`OutContTypeCompatibility.Normalize` over everything it reads. So the import
runs before `Global.Load()` and then gets out of the way — an imported
configuration migrates on load exactly as it would have migrated in place.
Transforming content in the importer would mean maintaining a second migration
path that only ever runs once, and diverges silently.

**New code**, all under namespace `DS4Windows`:

| File | Role |
|---|---|
| `DS4Control/SettingsImport/ImportFileSystem.cs` | The seam: `IImportFileSystem` + `PhysicalImportFileSystem`. The interface has **no delete and no move**, so "the source is read-only" is a property of the type rather than a promise in a comment. |
| `DS4Control/SettingsImport/ImportPlan.cs` | `ImportItemKind`, `ImportItem`, `ImportPlan`. Immutable description of what would be copied where, with per-item collision flags. |
| `DS4Control/SettingsImport/ImportPlanner.cs` | Discovery: source folder, the six single-file items, `Profiles\*.xml`. Also owns the pristine check and the decline marker. |
| `DS4Control/SettingsImport/ImportExecutor.cs` | Copying, per-item outcomes, counts. |
| `DS4Control/SettingsImport/ImportPlanSummary.cs` | The dialog's summary lines, kept out of the view so they are testable and reusable by a later Settings entry point. |
| `DS4Forms/ImportSettingsDialog.xaml` (+ code-behind) | The offer. |

Three execution rules, each of which is a test:

1. **Skip, never overwrite.** Existence is re-checked immediately before every
   copy, so a plan built minutes earlier cannot clobber a file created since.
2. **A failure never unwinds what succeeded.** No rollback, no cleanup. A
   half-done import leaves a configuration the application can still load.
3. **Re-running finishes the job**, because everything that landed is now a
   collision and gets skipped.

The planner also filters `*.xml` results by their real extension: Win32 pattern
matching still honours 8.3 short names, so a `Profiles\Old.xmlbackup` would
otherwise be planned as a profile.

### First-run wiring, and the ordering trap it had to avoid

The offer sits in `App.Application_Startup` between the logger and
`Global.Load()`. Four gates, all of which must pass: the resolved data folder is
the appdata one, that folder held no configuration when this launch began, the
user has not already declined, and the plan is non-empty.

The non-obvious part is *when* "held no configuration" is sampled. It cannot be
sampled at the offer site, because `SaveWhere`'s Appdata button calls
`Global.SaveDefault`, which writes a stub `Profiles.xml` into the target — so by
the time control reaches the offer, a genuinely empty configuration can already
look like an existing one. The flag is therefore taken immediately after
`Global.FindConfigLocation()`, before any dialog can run, and passed in. (On a
truly fresh machine `SaveDefault` fails harmlessly because the folder does not
exist yet, but relying on that would have been luck rather than design.)

The second ordering hazard is on the way out. A successful import makes the
configuration non-pristine, and the remaining first-run steps generate and save
defaults — `AttemptSave()` writes `Profiles.xml` and `SaveAsNewProfile(0,
"Default")` writes `Profiles\Default.xml`, both straight over what was just
imported. So the helper returns the first-run flag, and clears it (and
`Global.firstRun`, which `MainWindow` reads for window placement) once a
configuration exists.

**Dialog behaviour.** Title from `ProductInfo`; a summary listing the profile
count and one line per other kind found, plus a line for any collisions;
Import (default button, Alt+I) and Start fresh (`IsCancel`, Alt+F, Escape).
`Start fresh` carries no click handler on purpose — `IsCancel` alone closes the
window and leaves `ImportRequested` false, so nothing can race the built-in
cancel behaviour into setting `DialogResult` twice. **Every exit that is not
the Import button counts as declining**, including the title-bar close, which is
what makes "asked exactly once" true however the dialog is dismissed. Declining
writes `%APPDATA%\Thrum\import-declined.txt`; failing to write it is logged and
tolerated, because a repeated offer is a nuisance and a crashed startup is not.

**Portable mode never offers.** In exe-directory mode the configuration folder
is the install folder: the user asked for something self-contained that can be
moved to another machine or run beside a real DS4Windows install for testing.
Seeding it from one roaming profile's per-user state would quietly carry that
configuration wherever the folder travels, and the save-location dialog already
offers to adopt a configuration sitting next to the executable. The reasoning is
recorded on the dialog class, where the next person to wonder will look.

A partial import raises one message box naming the counts, says what was kept,
and says that nothing in the source changed. It does not ask the user to clean
anything up, because there is nothing to clean up.

### 1.5 remainder — startup-entry safety audit. Verdict: **already scoped; no legacy path existed**

Every path that deletes or repairs a startup entry was re-read:
`DeleteStartProgEntry`, `DeleteTaskEntry`, `DeleteOldTaskEntry`,
`CheckStartupExeLocation`, the `SettingsViewModel` constructor's repair block
(both entries present → delete the shortcut; executable moved → delete and
rewrite; task branch → `DeleteOldTaskEntry` + `WriteTaskEntry`) and the three
`RunAtStartup*` change handlers. All of them reach exactly two names, both from
`ProductInfo`. **No legacy-cleanup path targeting `RunDS4Windows` or
`DS4Windows.lnk` exists anywhere in the tree**, so there was nothing to delete
or fence. `extras/install-viiper-backend.ps1` only ever *registers* `RunVIIPER`;
nothing in the repository unregisters a scheduled task other than our own.

Two findings worth recording rather than shrugging at:

1. **`DeleteOldTaskEntry` is misleadingly named.** "Old" means a stale task *of
   ours* pointing at a moved `task.bat` — not the product this one was forked
   from. It now carries a doc comment saying so, because the obvious "fix" a
   future reader might apply (make it look for the inherited name) is precisely
   the bug this audit exists to prevent.
2. **Two duplicated copies of the shortcut path were collapsed.**
   `StartupMethods.HasStartProgEntry` and
   `SettingsViewModel.CheckStartupOptions` each composed the Startup-folder path
   independently instead of using `StartupMethods.lnkpath`. Both copies were
   correct; the point is that a rename only has to miss one place, and the 1.1
   sweep already caught this exact species of bug once in `SettingsViewModel`.

The guard is `DS4WindowsTests/StartupEntryIdentityTests.cs` (4 tests). The
load-bearing one reads the compiled application off disk and searches it for
`RunDS4Windows` and `DS4Windows.lnk`, decoded as UTF-16 at both byte alignments
because a metadata string literal can start at an odd offset. A hit means *some*
code path can name a real DS4Windows install's startup entry — the scan does not
care which class it lives in, which is the point. It carries a positive control
asserting the same scan does find `RunThrum` and `Thrum.lnk`, so it cannot pass
vacuously.

*Negative control run:* with the test's `LegacyStartupTaskName` needle
temporarily set to `RunThrum`, both the scan test and the name-difference test
failed with the intended messages; restored and re-verified green.

### 1.5 remainder — HidHide audit. Verdict: **no hard-coded name; nothing to fix**

The whitelist path derives entirely from the running process.
`Global.exelocation` is `Process.GetCurrentProcess().MainModule.FileName` with
junction/symlink resolution (the Scoop case), `CheckHidHidePresence` converts
that path to its DOS-device form and whitelists *that*, and
`ProductInfo.ExeBaseName` appears only in the log line "… not found in HidHide
whitelist. Adding to list" — it never reaches HidHide.
`UpdateHidHideAttributes` deals in device instance IDs and no executable name at
all; `HidHideAPIDevice` opens `\\.\HidHide`, which is HidHide's own device name
and not ours to rename; `Global.hidHideInstalled` probes the `root\HidHide`
system device. The auto-profile caller passes the user's chosen game path, which
is unrelated to product identity. Recorded as a table in the identity map under
the category *runtime-derived identity*.

### Smoke checklist

[`docs/dev/smoke-rebrand.md`](smoke-rebrand.md) — twelve items, each with steps
and an expected result: import accepted, source provably untouched, import
declined and remembered (including Escape and the title-bar close), portable
mode never offering, imported profiles loading with legacy output types
normalized, `-command` IPC and second-instance forwarding on the new object
names, side-by-side with a real DS4Windows install, the language switch actually
loading `Lang\<culture>\Thrum.resources.dll`, `RunThrum` and `Thrum.lnk`
create/remove **with an explicit check that the DS4Windows entries are
untouched**, HidHide showing `Thrum.exe` at its real path, `thrum_log.txt`, and
tray/theme/window identity. No absolute paths.

One correction while writing it: the plan (and the session brief) name
`-command query.appversion` as the IPC smoke test. **That verb does not exist**
in this tree. The handler's syntax is `query.<device#>.<property>` — the
checklist uses `query.1.apprunning` and `query.1.profilename`, which are real.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **succeeded, 0
  errors**, 17 warnings, all pre-existing and identical to the 1.1/1.2 baseline.
- Full suite with the repository's CI filter: **548 passed / 0 failed**, up from
  525 by exactly the 23 new tests (19 import, 4 startup-entry).
- The GUI application was **not** launched. Everything the dialog and the
  startup wiring do that a unit test cannot reach is in the smoke checklist.
- `git grep -in "ds4windows"`: 1,702 hits, up 58 from the flip. Every new hit is
  prose (the audit sections, the smoke checklist) or new-file boilerplate — a
  GPL header plus a `namespace DS4Windows` line is three hits before a file
  contains any logic. Exactly two new *literals* were added, both deliberate and
  both catalogued: the import source folder name, and the inherited startup
  names used as the guard test's needles.

### Test inventory

| Area | Tests | What they pin |
|---|---|---|
| Planning | 8 | Missing source → empty plan; present-but-empty source → empty plan; full source → all six single-file items plus every profile; partial source → only what exists; non-`.xml` files excluded (incl. the 8.3 `*.xmlbackup` case); collisions flagged; source == target → empty plan; the default source is `%APPDATA%\DS4Windows` **and differs from our own data folder name** |
| Execution | 6 | Full plan copies everything and leaves the source byte-identical; collisions skipped without overwriting; re-run copies only what is missing; an injected copy failure leaves the other items copied, reports the failure, and does not touch the source; a re-run after a failure finishes the import; an empty plan does not even create the target folder |
| Offer state | 3 | Absent/config-less target is pristine (`Actions.xml` alone does not count); either `Profiles.xml` or `Auto Profiles.xml` ends that; the decline marker survives across planner instances |
| Summary text | 2 | Counts, plural/singular, collision warning |
| Startup entries | 4 | Names composed from `ProductInfo`; both differ from the inherited ones; `lnkpath` is the product shortcut in the Startup folder; the inherited names appear nowhere in the compiled application |

Real temporary directories throughout (`TestContext.TestRunDirectory`, falling
back to the process temp path), never a hard-coded location, cleaned up in
teardown. The one injected failure uses the `IImportFileSystem` seam — which is
the reason the seam exists, since making one specific destination unwritable and
nothing else is otherwise awkward and non-deterministic.

### Deviations from the plan

1. **`query.appversion` does not exist**; the smoke checklist uses the real
   query syntax. See above.
2. **The pristine check is sampled before the first-run dialogs, not at the
   offer site.** The plan's wording ("after `FindConfigLocation()` resolves to
   the appdata mode") would have put the sample after `SaveWhere` had already
   been able to write a stub `Profiles.xml`. The offer still *runs* where the
   plan says; only the flag is taken earlier.
3. **`Global.firstRun` is cleared after a successful import.** Not in the task
   text, but without it the first-run bootstrap overwrites the imported
   `Profiles.xml` and `Profiles\Default.xml` with generated defaults, which
   would have made the whole feature silently useless.
4. **Two duplicated startup-path expressions were collapsed** (finding 2 in the
   audit above). A two-line change outside the task's literal scope, taken
   because the audit's verdict is "provably scoped" and three independent
   spellings of one path is the opposite of provable.
5. **A sixth importer file, `ImportPlanSummary.cs`, was added** beyond the
   planner/executor pair the task named. The dialog's wording is worth testing
   and worth reusing from a later Settings-page entry point; leaving it inside
   the view would have made it neither.
6. **The dialog has no `lex` localization bindings.** Its text is English in the
   code-behind. Inventing `.resx` keys here would push untranslated entries into
   24 language files ahead of the localization pull request that owns them;
   recorded in the identity map so that pull request picks it up.
7. **No `Settings` entry point for a later import.** The task scoped this to
   first run, and the decline marker makes the offer one-shot. Anyone who
   declines and changes their mind currently has to delete
   `import-declined.txt`. A Settings button belongs with the first-run flow
   rework in plan task 4.7; noted below.

### Next steps

- **Phase 1.6 / 1.7 — icons and update feed.** Still the highest-value
  remaining Phase 1 work, and 1.7 is a safety item: the inherited
  `DS4Updater.exe` path would install DS4Windows over this product.
- **Phase 1.8 — string sweep.** It now also owns the import dialog's text.
- **Phase 1.9 — version reset.**
- Run [`smoke-rebrand.md`](smoke-rebrand.md) once a build is in front of the
  maintainer. Items 1, 3, 7 and 9 are the ones that cannot be inferred from CI.
- Offer a re-import from Settings as part of plan task 4.7's first-run rework,
  so declining is recoverable without deleting a marker file by hand.

---

## 2026-07-25 — Phase 1.6 + 1.7 + 1.9 (visual identity, release-feed cutover, version reset)

**Session scope:** Phase 1, tasks 1.6, 1.7 and 1.9. Branch
`phase1/icons-updater-version`.

Three tasks in one pull request because they are one story: this is the change
where the product stops borrowing another project's face, another project's
update feed, and another project's version number. Task 1.7 is also the last
genuinely unsafe thing the rebrand inherited — see below.

### 1.7 first, because it is a safety fix rather than branding

Before this change, `ProductInfo.ReleaseOwnerRepo` still pointed at
`hbashton/DS4Windows`, and the update path still worked exactly as upstream
wrote it:

1. check `hbashton/DS4Windows` releases,
2. offer the user an update,
3. on "yes", download `DS4Updater.exe` from `hbashton/DS4Updater`,
4. copy it next to our executable — **through an elevated `.bat` script** if the
   install needs admin,
5. launch it with `--launchExe Thrum.exe`.

`DS4Updater` installs DS4Windows. So a Thrum user who clicked "yes" would have
had this product downloaded over, overwritten by, and replaced with the product
it was forked from, with a UAC prompt in the middle of it. The auto-check was
live too: `CheckWhen` defaults to 24 hours, and the `#if !BETA_VERSION` guard
around the startup check is inert because `BETA_VERSION` is not defined in any
configuration in the csproj.

**The whole pipeline is deleted, not repointed.** What remains: the manual
"Check for updates" button and the startup auto-check still query *our*
releases API and still compare through `ReleaseChannelPolicy`; if a newer
release exists, the existing dialog appears with its release notes, and "yes"
opens the releases page in the user's browser. No download, no elevated copy,
no process launch. `Util.ElevatedCopyUpdater` — the only elevation anywhere in
the update path — is gone entirely.

Deleted with it, all provably dead once their callers went:
`MainWindowsViewModel.RunUpdaterCheck`, `LauchDS4Updater`,
`DownloadUpstreamUpdaterVersion`, `DownloadUpstreamVersionInfo`;
`MainWindow.Check_Version` (a second, older copy of the update flow that had no
callers at all); `Changelog.CheckNewerVersionExists` and its `_latestVersion`
cache; five `ProductInfo` updater constants; and the `PleaseDownloadUpdater`
resource string in four languages, which told the user to download and rename
`DS4Updater.exe` by hand.

Repointed rather than deleted: `ReleaseOwnerRepo` → `potpiemuncher/Thrum`.
Every other release URL is composed from it, so they cannot disagree.

**Exact user-visible flow after the cutover.**

| Situation | What the user sees |
|---|---|
| Manual check, no releases published (today's reality) | Message box titled "Thrum": the app is up to date. An informational line is written to the log saying no releases have been published yet. |
| Manual check, release feed unreachable | Message box titled "Thrum" reporting the failure; the HTTP status is logged. |
| Manual check, newer release exists | The existing updater dialog, showing that release's notes. **Yes** opens `https://github.com/potpiemuncher/Thrum/releases` in the browser and the app keeps running. **No** / **Skip this version** behave as before. |
| Startup auto-check (on by default, every 24 h) | Silent when up to date; otherwise the same dialog and the same browser-only outcome. |
| Changelog button | Release notes from our repository, or "No release notes yet." while there are none. |

### Zero releases: verified against the actual API shape, not assumed

The task asked which shape our code hits, and the two endpoints differ in a way
that matters. `GET /repos/{owner}/{repo}/releases` — the **list** endpoint —
answers **200 with `[]`** for a repository that has published nothing.
`/releases/latest` answers **404**. Our update check uses the list endpoint, so
"no releases yet" arrives as an ordinary empty array rather than as something
indistinguishable from a network failure.

`SelectPreferredRelease([])` already returned null and `ShouldUpdate(null, …)`
already returned false, so the *verdict* was correct before this change. What
was missing was that it was silent: an empty feed, an unreachable feed and a
malformed feed all produced the same "no update" with nothing in the log.
`CheckNewerReleaseExists` now distinguishes them and logs the first two. A test
pins that the update check does not depend on `/releases/latest`, since
switching to it would turn today's normal state into an error path.

`ChangelogWindow` and `UpdaterWindow` both render an empty feed as an explicit
"No release notes yet." rather than a blank window, which reads as a broken
feature rather than an accurate one.

### `Changelog.json` — re-verified before deleting, and the verification mattered

PR #1 recorded "no reader exists". That is the kind of finding that only has to
be wrong once, and the task named the specific suspect: `ChangelogWindow`, which
might read the JSON or might fetch remotely.

Traced end to end. `ChangelogWindow` → `ChangelogViewModel.DisplayChangelog` →
`Changelog.GetChangelogMarkdown(true)` → `GetChangelog(true)` → an HTTP GET of
`GITHUB_RELEASES_API_URI`. It renders GitHub release bodies as markdown and
never touches the file. (`ChangelogViewModel` still *imports* `System.Text.Json`
and `System.IO` without using them — vestigial from the old local-file design,
which is exactly what makes the file look live at a glance.)

So the finding held. `Changelog.json` (123 KB) and `Changelog.min.json` (96 KB)
are stale 3.3.3 data with no reader in the C#, the csproj, the workflows or the
build scripts — they were not even copied to the output directory. **Both
deleted.** Nothing needed pointing at a raw URL, because the changelog reader is
the release feed, and the release feed moved in 1.7 on its own.

### 1.6 — a placeholder icon set that is honest about being one

`ApplicationIcon` was still `DS4W.ico`, the tray still showed DS4Windows's icon,
and the eleven battery tray icons were upstream's.

**What the mark is.** A rounded square in a deep violet with a bold white T. It
is a placeholder and the notice file says so in those words. The colour brief
was "not PlayStation blue"; violet is also clear of Xbox green and Nintendo red.

**Generated, not drawn.** `utils/generate-thrum-icons/` is a committed
`dotnet run` tool. It is deliberately **not** in `DS4WindowsWPF.sln`: it is
authoring tooling, not a product component, and does not belong on the CI
critical path. Verified idempotent — a second run produced byte-identical files.

The reason the generator is committed rather than just its output is the frame
recipe, and the frame recipe is the whole engineering content of this task.
Each icon carries seven frames in two encodings: **uncompressed 32-bit BMP at
16, 24, 32 and 48**, and **PNG at 64, 128 and 256**. WPF reads either. GDI —
which is where H.NotifyIcon takes the tray icon — selects a frame by size
*before* decoding, so the small BMP frames are load bearing. Recovering that
recipe by inspecting a finished `.ico` is tedious and easy to get subtly wrong,
so the intended way to replace these placeholders is to change the drawing code
and re-run the tool.

**Files.** `Thrum.ico`, `Thrum - White.ico`, `Thrum - Black.ico`, and
`0.ico`…`100.ico`. The monochrome variants are a solid plate with the T knocked
out to transparency, so one shape works on any taskbar tint. The battery icons
keep their inherited numeric names on purpose — the tray view model composes
those paths arithmetically from the percentage, the names describe a level
rather than a brand, and renaming them would have meant rewriting that switch
to buy nothing. They carry the base mark plus a proportional bottom bar,
colour-coded red / amber / green, and no digits: legible numerals need about
nine pixels of height and there are fourteen to spend on the entire icon at
16x16.

Deleted: `Resources/DS4W.ico`, `Resources/DS4W - White.ico`,
`Resources/DS4W - Black.ico`, and `DS4Windows/DS4W.ico` — the last being a
*second copy* of the application icon at the project root that `ApplicationIcon`
named and that had to be kept in step with the `Resources` copy by hand.
`ApplicationIcon` now points at `Resources\Thrum.ico`; there is one file.

`Resources/ICONS.NOTICE.txt` states that the icons are project-owned, GPL,
generated in-repo, and pending real design. It ships beside
`ControllerArtwork.NOTICE.txt`.

**About box.** Product name and version from `ProductInfo` and the assembly, a
GPL-3.0-or-later notice carrying the warranty disclaimer that section 5(d)
requires an interactive program to keep showing, lineage credits with links
(hbashton for DS4Windows and VIIPER, Schmaldeo, Ryochan7, Jays2Kings,
electrobrains, InhexSTER, plus the community and translators), and the
repository link. The inherited `ryochan7.github.io/ds4windows-site` link and its
dead handler are gone. The header label is the interesting one: it read
`"DS4Windows - hbashton Build (Version "` with the version appended in the
constructor — a half-sentence literal, which is exactly the shape of string a
rebrand walks past. It is now assigned whole, in code, from `ProductInfo`.

### 1.9 — version reset

Root `Directory.Build.props` holds `0.9.0-beta.1` / `0.9.0.0` / `0.9.0.0` /
`0.9.0-beta.1 (base: hbashton DS4Windows 4.0.2.1 @ 5d2724a)`. Both projects
inherit; the four per-csproj properties were removed and replaced with a comment
saying why a local value must not come back (a local value silently wins, which
is how upstream's four properties drifted apart in the first place).

`release.yml` reads `AssemblyVersion` and `Version` **by XPath**, and pointed at
the csproj. Repointed at `Directory.Build.props`. This is the trap in moving
version properties: `ElementTree.find()` returns `None` rather than failing, so
the step would have kept running against a csproj that no longer declares a
version and produced an unhelpful `AttributeError` in the middle of a release.

### The `app_version` compatibility question — verdict: **cannot misbehave**

Required check, and the sharp end of the version reset: the product version went
*backwards*, 4.0.2.1 → 0.9.0, while every settings file and every profile a
DS4Windows user brings across — including everything the 1.4 importer copies —
carries `app_version="4.0.2.1"` or older. From the running application's point
of view those files were written by a newer build.

Four independent findings, each sufficient on its own:

1. **`app_version` has no reader.** All three DTO properties that bind the
   attribute (`AppSettingsDTO`, `ProfileDTO`, `OutputSlotPersistDTO`) declare
   `set { }` — an empty body. `XmlSerializer` parses the value off disk, hands
   it to a method that does nothing, and it is gone. No field, no comparison,
   no log. `ProfileMigration` reads `config_version` and never `app_version`.
2. **`APP_CONFIG_VERSION` — the constant stamped into `Profiles.xml` — is never
   compared to anything.** It is write-only. `BackingStore.Load()` contains no
   version logic at all and does not construct a `ProfileMigration`; its only
   failure path is `catch (InvalidOperationException)` on genuinely malformed
   XML.
3. **`CONFIG_VERSION` is compared exactly once and one-directionally**
   (`ProfileMigration.cs:86`, `configFileVersion >= 1 && configFileVersion <
   Global.CONFIG_VERSION`), and only for profile files. A value at or above ours
   falls to the pass-through branch at `ScpUtil.cs:5571` and loads verbatim.
   `Migrate()`'s switch has a `default: break;`, so even an out-of-range value
   is a silent no-op.
4. **No user-facing string anywhere mentions a newer or incompatible
   configuration.** Swept `.cs`, `.xaml` and every `.resx` for
   newer/downgrade/incompatible; every hit is updater plumbing, driver notices,
   or GPL prose. No `XmlSerializer` `UnknownAttribute`/`UnknownElement` handler
   is wired anywhere in the tree, so unrecognised header attributes are ignored
   by design rather than by luck.

Two things that look like risks and are not, named so nobody mistakes them
later: `PostProcessLoad` does re-parse the stored `LastVersionChecked` string
during load, but its only failure mode is blanking that update memo; and
`Global.exeversion` feeds the *writer* side of all three DTOs, so the first save
after upgrade simply rewrites the header to ours.

The one real gap was test coverage: `AppSettingsTests`'s fixture root is a bare
`<Profile>` with no header attributes at all, so nothing exercised a populated
`app_version`. Closed — see below.

`ReleaseChannelPolicy.IsPrereleaseBuild("0.9.0-beta.1")` → **true** (the regex
alternation matches `beta`), and `TryParseReleaseVersion` extracts `0.9.0`.
Worth checking rather than assuming: the marker moved from a trailing word
("4.0.2.1 DualSense Beta") to a semver suffix, a different shape entirely. Now
covered, including the negative case that plain `"0.9.0"` is *not* classified as
a prerelease.

### Tests: 548 → 570

22 new, in three files.

| File | Tests | What they pin |
|---|---|---|
| `IconResourceTests.cs` | 5 | Icon file names compose from `ProductInfo`; all five tray choices point at our own icons; all fourteen icons load through `System.Drawing.Icon` **and** `BitmapFrame`; all fourteen still carry uncompressed frames at 16/24/32/48. |
| `UpdateFeedTests.cs` | 6 | The feed names `potpiemuncher/Thrum`; every release URL composes from one constant; the check uses the list endpoint and not `/releases/latest`; the five updater constants are absent by reflection; the five updater methods are absent by reflection; and no `DS4Updater` artefact appears anywhere in the compiled application. |
| `VersionCompatibilityTests.cs` | 11 | A 4.0.2.1 header loads; four different headers produce identical state; saving rewrites the header from the running build; the format versions did not move with the product version; a newer-`config_version` profile passes through untouched (with an older-profile positive control); `0.9.0-beta.1` classifies as a prerelease; and the built assembly carries the reset version and the base commit. |

**Negative controls, all run and all fired:**

- Regenerated every icon with PNG-only frames:
  `EveryIconCarriesUncompressedFramesAtTheShellSizes` failed with 56 specific
  complaints. **The other four icon tests passed** — including the
  `System.Drawing.Icon` load test, which accepted the PNG-only files. That is
  the finding worth recording: the load test alone would *not* have caught the
  regression it looks like it catches, and the frame-composition test is the
  real guard. Generator restored and output verified byte-identical.
- Added `"Thrum.release"` (genuinely present) to the updater needle list:
  `NothingInTheApplicationCanNameTheExternalUpdater` failed, so the scan is not
  looking at an empty haystack. It also carries a permanent positive control
  asserting it can find our own releases URL.
- Rebuilt the application with the old `4.0.2.1` / `"4.0.2.1 DualSense Beta"`
  version and re-ran: `TheApplicationAssemblyCarriesTheResetVersion` and
  `TheInformationalVersionRecordsTheUpstreamBaseCommit` both failed.
  `TheBuiltAssemblyCarriesAPrereleaseInformationalVersion` correctly still
  passed — both versions are prereleases, so it is a channel guard, not a
  version-reset detector.
- Made one settings fixture differ by header:
  `TheHeaderVersionCannotInfluenceWhatIsLoaded` failed, so its comparison is
  sensitive to what it claims to compare.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64 --no-incremental`
  — **succeeded, 0 errors**. Ten distinct warnings, identical to the inherited
  baseline; none new.
- Full suite with the repository's CI filter: **570 passed / 0 failed**, up from
  548 by exactly the 22 new tests.
- CI's publish invocation and `utils/post-build.py` run locally. Packaged output
  verified:
  - `Thrum.exe` FileVersion **0.9.0.0**, ProductVersion **`0.9.0-beta.1 (base:
    hbashton DS4Windows 4.0.2.1 @ 5d2724a)`**.
  - The embedded application icon extracted from the packaged `Thrum.exe` is the
    new mark.
  - Both notices ship under `Resources\`.
  - Zip named `Thrum_0.9.0-beta.1_x64.zip`.
  - **Zero files matching `DS4W*`, `DS4Updater*` or `Changelog*` anywhere in the
    package.**
- `git grep -in "ds4windows"`: 1,754 hits, up 52. Every new hit is prose — this
  entry, the revised identity-map rows, and the new tests' documentation
  explaining why the updater is gone — plus four deliberate guard-test needles.
  The literals removed outweigh those added.
- The GUI application was **not** launched.

### Deviations from the plan

1. **`Changelog.json` was deleted rather than repointed.** The task offered
   either outcome depending on what `ChangelogWindow` turned out to read. It
   reads the releases API, so there was nothing to point at a raw URL and no
   minimal changelog to commit; the changelog feed moved with `ReleaseOwnerRepo`
   for free.
2. **`DS4Windows/DS4W.ico` at the project root was deleted, not replaced.** It
   was a duplicate of the `Resources` copy that `ApplicationIcon` named
   separately. One file is now the source for both uses.
3. **Two dead `ResXFileRef` entries were removed** (`DS4W`, `DS4W___White`, from
   `Resources.resx`, `Resources.ru.resx` and the generated designer file).
   Nothing read `Properties.Resources.DS4W*`, but a file reference to a deleted
   `.ico` is a *build* failure, so they had to move with the files regardless.
   The `DS4` entry stays: it points at `DS4.ico`, which is device artwork and
   still exists.
4. **`PleaseDownloadUpdater` was deleted from four `.resx` files**, although
   `.resx` values otherwise belong to the localization pull request. The
   justification is that it is *dead*, not that it is misbranded: it described a
   feature that no longer exists. It was also the last thing in the neutral
   resources naming `DS4Updater.exe`, which is what lets the guard test use that
   as a needle.
5. **`Resources.UpToDate` was left saying "DS4Windows application is
   up-to-date."** This is visibly wrong prose in a flow this pull request owns,
   and fixing it was tempting. Left alone deliberately: it is a `.resx` value
   with 24 translations, and the reason 1.8 has sole ownership of those 223 hits
   is so they get one scripted pass with a translator log instead of four pull
   requests each fixing the English and abandoning the rest. Recorded as open
   decision 5 in the identity map so 1.8 does not have to rediscover it.
6. **Three hard-coded `"DS4Windows Updater"` message-box captions were
   converted** to `ProductInfo.ProductName`. Same class as the five captions
   found in 1.2 — code literals, not resources.
7. **`ds4winwpf_screen_20200412.png` was deleted**, closing a DEFER the identity
   map assigned to this pull request. Nothing referenced it and the README that
   embedded it was replaced in 0.5, so it was an orphaned picture of another
   product's user interface.
8. **Root `/newest.txt` was added to `.gitignore`.** `post-build.py` writes it on
   every local package run, so it appears as an untracked file inviting an
   accidental commit of a build artifact. The tracked `DS4Windows/newest.txt` —
   still dead, still not read by anything — is set to `0.9.0` as the task
   specified.
9. **`release.yml`'s version XPath was repointed** at `Directory.Build.props`.
   Not called out in the task text, but moving the properties without it would
   have broken the release workflow silently.
10. **The generator is not in the solution.** It builds and runs via
    `dotnet run --project utils/generate-thrum-icons`, inherits the root
    `Directory.Build.props` like everything else, and is invisible to
    `dotnet restore` / `dotnet build` of `DS4WindowsWPF.sln`.
11. **The shell header monogram stayed a letter `T`.** 1.2 left a note that "a
    real logo lands with the icons pull request". The icon set is an explicit
    placeholder, so swapping one placeholder for another inside the shell chrome
    would have been motion rather than progress.

### Smoke items queued (need the GUI)

- Tray icon appearance at 100% and 125%/150% DPI, on a light and a dark taskbar,
  for all four non-battery choices — the 16px BMP frame is what the shell picks
  and no test can judge how it *looks*.
- Battery tray icon changing as a real controller discharges, including the
  0-to-10% transition where the bar is one pixel of red.
- "Check for updates" against the live (empty) feed: the up-to-date message box,
  and the log line.
- The About box's layout at the default window size, and every hyperlink.
- The Changelog button with no releases published: "No release notes yet."

### Next steps

- **Phase 1.8 — string sweep.** The last Phase 1 task. It now also owns the
  import dialog's English text (from 1.4), `Resources.UpToDate`, and the
  `FakeExeName` tooltip that still names DS4Updater.
- Run [`smoke-rebrand.md`](smoke-rebrand.md) and the icon items above once a
  build is in front of the maintainer.
- Phase 1 acceptance then needs a side-by-side run against a real DS4Windows
  install.
- Still open from earlier sessions: the `NOTICE.txt` audit (0.5 deviation 1),
  the OSC address namespace, and the `DS4WINDOWS_*` environment variables.

---

## 2026-07-25 — Phase 1.8 (product-name sweep over the string resources)

**Session scope:** Phase 1, task 1.8. Branch `phase1/localization-sweep`.
**This completes Phase 1's code scope.** The only Phase 1 item left is the
manual smoke checklist [`smoke-rebrand.md`](smoke-rebrand.md), which needs the
maintainer and a running build; nothing in it is CI-reachable.

**What this change is.** The rename landed in 1.2 and the application has
presented itself as Thrum to Windows ever since — but it has kept telling the
*user* it is DS4Windows, in nine languages' worth of tooltips, in the welcome
dialog's title bar, in the update-check message box, and in a dozen log lines.
This is the pass that fixes the words. It changes `.resx` **values** and English
prose literals, and nothing else: no key, no comment key, no entry count, no
file encoding and no line ending moved.

### Inventory first, flip second

Every hit was classified before anything was edited, because a blanket
`DS4Windows` → `Thrum` pass over the string resources is wrong in four
different ways at once. The categories, and what they cost if you get them
wrong:

| Category | What it is | Cost of flipping it anyway |
|---|---|---|
| FLIP | the string names *us* | — |
| KEEP-SOURCE | the string correctly names the *other* product | the sentence becomes false |
| KEEP-UPSTREAM | attribution, lineage, upstream documentation links | a 404, or a credit removed |
| KEEP-TECH | file format, on-disk setting values, external control surfaces | silent data loss |
| DEAD | zero references from live code | churn in 24 files for text nobody sees |

The full classification, with the per-key reasons, is in the identity map's
[section 10](identity-map.md#10-user-visible-strings-and-translations). The
KEEP-TECH row came back empty on the resource side, which was worth confirming
rather than assuming: **no `.resx` value in either family is a protocol string.**
All of those (`<DS4Windows>`, the OSC addresses, the `DS4WINDOWS_*` environment
variables, the four audio pseudo-endpoint ids) live in `.cs` constants.

### The scripted pass, and why it is not a regex

The token `DS4Windows` is language-invariant, so a mechanical swap is safe in
all 24 languages. Getting it *reviewable* was the harder half. The script:

1. parses each file with a real XML parser and reads the **decoded** value, so
   entities and escaping are never guessed at;
2. decides from that decoded text, skipping any occurrence inside a URL;
3. applies the substitution to the raw `<value>` span rather than
   re-serialising the document — the token contains no character XML ever
   escapes, so the two agree, and the file's BOM, its CRLF endings and every
   other byte survive untouched;
4. re-parses the result and asserts the key list, the key order, and every
   untargeted value are identical, and that each targeted value decodes to
   exactly the intended string. Any disagreement aborts before writing.

That is what keeps the diff at 138 changed lines across 29 files instead of a
whole-file reformat, and it is why the change is readable at all.

Re-running it is a no-op: the applied run and a second run produce an identical
diffstat.

### Per-file flip counts

195 token replacements. The neutral files were hand-reviewed line by line; `ja`,
`zh-Hans` and `ru` were read in full for mojibake and none was found, and an
automated check confirms no file gained a stray LF or lost its BOM.

| `Translations/Strings*.resx` | tokens | values | | `Translations/Strings*.resx` | tokens | values |
|---|---:|---:|---|---|---:|---:|
| *(neutral)* | 13 | 9 | | ms | 10 | 6 |
| ar | 2 | 2 | | nl | 13 | 9 |
| cs | 4 | 1 | | pl | 4 | 1 |
| de | 7 | 4 | | pt | 5 | 2 |
| el | 10 | 6 | | pt-BR | 5 | 2 |
| es | 5 | 2 | | ru | 11 | 7 |
| fi | 8 | 5 | | se | 6 | 3 |
| fr | 7 | 4 | | tr | 10 | 6 |
| he | 4 | 2 | | uk-UA | 5 | 2 |
| hu-HU | 5 | 2 | | vi | 7 | 4 |
| idn | 10 | 6 | | zh-Hans | 12 | 8 |
| it | 8 | 5 | | zh-Hant | 5 | 2 |
| ja | 4 | 1 | | **subtotal** | **180** | **101** |

| `Properties/Resources*.resx` | tokens | values |
|---|---:|---:|
| *(neutral)* | 4 | 4 |
| ja | 4 | 4 |
| ru | 3 | 3 |
| zh-hans | 4 | 4 |
| **subtotal** | **15** | **15** |

`he` is the case that justifies the case-insensitive match: its two values spell
the token `DS4WINDOWS`. Agglutinative and case-inflecting languages keep their
suffixes on the new stem — Finnish "DS4Windowsin" becomes "Thrumin", Turkish
"DS4Windows'u" becomes "Thrum'u" — which is the intended conservative outcome
for a literal token swap and is flagged for translators rather than
second-guessed here.

### Skipped hits

| Hit | Category | Reason |
|---|---|---|
| `Resources.QuitOtherPrograms` (4 files) | KEEP-UPSTREAM | its only token is inside `github.com/Ryochan7/DS4Windows/wiki/…`. This key is *in* the script's allowlist so the URL guard is exercised and reported on every run rather than asserted in prose, and a test pins that the link survived. |
| `Strings.CustomExeNameInfo` — `DS4Windows.exe`, `InputMapper.exe` (25 files) | KEEP-SOURCE | the process names a game's input block looks for. |
| "Support DS4Windows" + PayPal button (`MainWindow.xaml`) | KEEP-SOURCE | the link pays the upstream maintainer, so the label is accurate today. Flipping only the text would solicit donations for this product and route them elsewhere. Raised as open decision 6. |
| About-box lineage credits (5), project links (3), Moonlight doc link, keyboard-mouse KB link | KEEP-UPSTREAM | section 11 attribution. |
| 11 `.resx` values whose keys have zero references | DEAD | enumerated below. |
| `App.xaml.cs:77` comment, `LogViewModel.cs:48` commented-out line, `AppSettingsDTO.cs:52` commented-out block, a `<see cref>` in `StartupMethods.cs` | KEEP | not user-visible; a `cref` has to name the real type. |
| `utils/post-build.py`'s "DS4Updater uses this manifest" comment | DEFER | build-script prose describing a tool deleted in 1.7. |

### Dead strings: enumerated, deliberately not deleted

The plan asked this task to purge the dead ViGEm strings. **It does not**, and
that is a considered deviation. Every key is echoed by a checked-in designer
property and by up to 24 translated files, so deleting one key is a
twenty-six-file edit whose failure mode is a build break — exactly the kind of
change that does not belong in a pass whose safety argument is "values only".
They are catalogued in the identity map instead, for a cleanup phase that can
regenerate both designers in the same commit.

The three the plan named are all confirmed dead and listed: `ViGEm117MinNeeded`,
`ViGEmPluginFailure`, and the first-launch ViGEmBus step pair
(`Welcome.Step1Text` "Step 1: Install ViGEmBus Driver" plus `Welcome.Step1HelpText`
— `WelcomeDialog.xaml` starts at step 2, so the button was removed from the view
and its strings were left behind). The DsHidMini text is **not** dead and is not
listed; DS3 support still uses it.

Totals: **31 of 462** entries in `Strings.resx` and **111 of 175** in
`Properties/Resources.resx` have no reference — 142 keys.

Getting that number right took two passes. A first scan searched for the key
name as a bare word and reported far fewer dead keys, because `RunAtStartup`,
`UACTask` and a dozen others collide with unrelated identifiers — a view-model
property, a method name. The scan that counts is the one that looks for the
reference *forms the codebase actually uses*: `Strings.<Key with dots as
underscores>` in C# or a `lex:Loc` / `lex:BLoc` / `lex:LocExtension` key token
in XAML for the `Strings` family, `Resources.<Key>` for the other. It is
complete because **nothing in the tree looks a resource up dynamically** — no
`ResourceManager.GetString(variable)`, no computed `lex` key — which was checked
before trusting the result.

Dead values were not flipped. Three of them (`CopyComplete`,
`DS4WindowsCannotEditHere`, `RunAtStartup`) have live code-literal twins in
`App.xaml.cs` and `MainWindow.xaml`; *those* were flipped, which is why the
strings a user can actually reach are all correct even though their resource
namesakes still read DS4Windows.

### The one value that was rewritten rather than swapped

`Strings.CustomExeNameInfo` is the Settings "custom exe name" help text. A token
swap would have produced a sentence promising that **DS4Updater** will keep a
renamed copy of Thrum up to date — describing a pipeline deleted in 1.7. The
neutral value was rewritten by hand: the dead sentence is replaced with what the
feature actually does (the app keeps a renamed copy of itself beside the
original), `DS4Windows.exe` and `InputMapper.exe` stay because they name what a
game detects, and the example becomes `whyme_Thrum`. The 24 translations got the
token swap only, so they still carry the stale `DS4Updater` sentence in their own
language; that is the single largest item in the translator backlog and it is
recorded as such rather than machine-translated.

`Resources.UpToDate` — "DS4Windows application is up-to-date.", flagged by 1.7 as
visibly wrong prose in a flow that pull request owned — is flipped. Open
decision 5 is closed.

### Deferred prose (`.cs` and `.xaml`)

16 C# literals across 13 files now compose from `ProductInfo.ProductName`: the
three startup log lines and their Log-tab duplicates, the x86 build warning and
its caption, two settings-relocation message boxes, the auto-profile
"turning … off/on" debug lines, four `ControlService` log sentences, the Game Bar
repair notice, two VIIPER setup message bodies, the VIIPER debugger's exe line
and its detach reason, the profile and special-action file-dialog filters, the
Bezier-editor failure, the audio-pacer error, a worker thread name and the vJoy
initialisation failure.

7 XAML literals were flipped to the plain word `Thrum`: three `MainWindow`
tooltips, the FakerInput description in the About box, the Moonlight
accept-everything tooltip, the VIIPER debugger's explanation and the welcome
dialog's backend line. These are unlocalized English prose with no `lex`
binding, and splitting a sentence into `Run`s to consume a constant would make
the markup worse than the literal it replaces.

### Import dialog: localizable at last (PR #3 deviation 6)

17 new keys under an `Import.` prefix, in `Translations/Strings.resx` **only**.
`ImportSettingsDialog.xaml` gains `lex:Loc` for its two buttons and becomes the
24th file carrying `lex:ResxLocalizationProvider.DefaultAssembly="Thrum"`; the
code-behind, `ImportPlanSummary` and the partial-failure message box in
`App.xaml.cs` all read from the resources through `string.Format`.

**Untranslated keys, for whoever picks up translation:**
`Import.CollisionCountPlural`, `Import.CollisionCountSingular`,
`Import.FooterText`, `Import.HeadingText`, `Import.ImportButton`,
`Import.KindActions`, `Import.KindAppSettings`, `Import.KindAutoProfiles`,
`Import.KindControllerConfigs`, `Import.KindLinkedProfiles`,
`Import.KindOutputSlots`, `Import.PartialFailureText`,
`Import.ProfileCountPlural`, `Import.ProfileCountSingular`,
`Import.SourceText`, `Import.StartFreshButton`, `Import.WinTitle`. Each carries
a `<comment>` explaining its placeholders. They fall back to neutral in all 24
languages, which is deliberate: machine-translating them would put unreviewed
text in front of users in 24 languages, and that is worse than English.

Two consequences worth spelling out. First, `ImportPlanSummary`'s output now
depends on the resource lookup, so `SettingsImportTests` pins
`Strings.Culture` to the invariant culture in setup and restores it in
teardown — without that, its two English assertions would quietly become
machine-culture-dependent. Second, the designer properties for these keys were
**written by hand**, which is the next section's problem.

### The checked-in designer files

`Strings.Designer.cs` and `Resources.Designer.cs` are committed, and the
command-line build never regenerates them. Two consequences, both handled:

- Their `/// Looks up a localized string similar to …` comments echo the neutral
  values, so they go stale the moment a value changes. All 13 changed echoes
  were re-synced by script, editing only the doc-comment span that precedes each
  changed property — the property names and their `GetString("<key>")` arguments
  are outside the edited range by construction.
- The 17 new `Import_*` properties had to be added by hand, alphabetically,
  matching the generator's exact shape. A hand-written property naming a key
  that does not exist compiles cleanly and returns `null` at runtime, so there
  is now a test that resolves every one of them.

### Findings the sweep turned up

Two are real bugs, one is a translation that has never shipped.

1. **The auto game-audio detector stopped excluding this application.**
   `AutomaticGameAudioDetector` keeps a set of process names that are never "the
   game" whose audio should be captured, and it listed `ds4windows`. After the
   1.2 rename to `Thrum.exe`, nothing in that set matched us any more, so the
   detector could pick this application as its own capture source. Fixed: the
   set now contains `ProductInfo.ExeBaseNameLowerInvariant`, and keeps
   `ds4windows` too, because a real DS4Windows install running alongside is not
   a game either. **This is a regression the rename introduced and no test
   caught**; it is in the identity map as a found anchor.
2. **Two more persisted audio endpoint ids** —
   `DS4Windows:AutoDetectDualSenseGameAudio` and `DS4Windows:DefaultSystemAudio`
   in `DualSenseAudioPassthrough` — are the pair the 1.2 sweep missed when it
   found the two in `ProcessLoopbackWaveCapture`. Same class, same disposition:
   **KEEP**, because they are compared ordinally against a persisted per-profile
   setting and flipping them silently resets that setting. Now recorded, so the
   next sweep does not have to rediscover them.
3. **Indonesian has never shipped.** `Translations/Strings.idn.resx` produces no
   satellite assembly: `idn` is not a culture name (Indonesian is `id`), so
   MSBuild drops it silently and `post-build.py`'s hard-coded language list
   creates an empty `Lang\idn\` folder where the translation should be. 24
   translated files, 23 satellites. Left as found — renaming a resource file
   changes which translations ship, which is not a value-only change — and now
   guarded, so it cannot happen again unnoticed.

One more, moved rather than merely noted: the named pipe between the audio pacer
and its helper process was called `DS4Windows.DualSenseAudioPacer.<pid>.<guid>`.
It is a kernel object name, so section 4's category. Safe to rename because the
parent composes it and passes it to the child on the command line, so both ends
agree by construction.

### Tests: 570 → 577

New `DS4WindowsTests/LocalizationSweepTests.cs`, 7 tests. Every one pins
`Strings.Culture` and `Resources.Culture` to the invariant culture first;
without that, a machine whose UI culture has a satellite would read the
translation and the assertions would be about the wrong string.

| Test | What it pins |
|---|---|
| `EveryFlippedNeutralStringNamesThisProduct` | the 12 plain-swap values name Thrum and do not name DS4Windows — and the key that deliberately still spells the old name still resolves |
| `TheCustomExeNameHelpKeepsTheForeignNamesAndDropsTheDeadUpdater` | the rewritten help text keeps `DS4Windows.exe` and `InputMapper.exe`, names Thrum, and no longer mentions `DS4Updater` |
| `TheUpstreamWikiLinkSurvivedTheSweep` | the Ryochan7 wiki URL is intact and was not rebranded — the positive control for the URL guard |
| `EveryImportDialogKeyResolvesToNeutralText` | all 17 hand-written designer properties resolve to real, non-empty resx keys |
| `TheImportDialogFormatStringsCarryTheirPlaceholders` | every `string.Format` target has exactly its expected `{0..n}` set |
| `EveryExpectedTranslationShipsAsASatellite` | all 23 satellites load |
| `EverySatelliteOnlyDeclaresKeysTheNeutralFileDefines` | no translated file declares a key the neutral file lacks |

**Negative controls, all run, all fired:**

- Put `Welcome to DS4Windows` back in the neutral file →
  `EveryFlippedNeutralStringNamesThisProduct` failed naming the exact key.
- Typo'd one designer key to `Import.WinTitleTypo` →
  `EveryImportDialogKeyResolvesToNeutralText` failed with the property name.
- Added a `ZZZ.OrphanProbe` key to `Strings.de.resx` →
  `EverySatelliteOnlyDeclaresKeysTheNeutralFileDefines` failed with `de: ZZZ.OrphanProbe`.
- Dropped `{1}` from `Import.SourceText` →
  `TheImportDialogFormatStringsCarryTheirPlaceholders` failed.
- Repointed the wiki URL at our own repository →
  `TheUpstreamWikiLinkSurvivedTheSweep` failed.
- Added `idn` to the expected-satellite list →
  `EveryExpectedTranslationShipsAsASatellite` failed with `idn`. **This is the
  control that matters most**: it proves the test would have caught the
  Indonesian problem, which shipped unnoticed for years.

The last test carries one allowlisted exception, `el:
ProfileEditor.VirtualTrigButtonOutput` — a key the Greek file declares and the
neutral file does not, inherited and unreachable. Left alone rather than deleted
from a translation or invented in English.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64 --no-incremental` —
  **succeeded, 0 errors**, 17 warnings, identical to the inherited baseline. A
  malformed `.resx` fails the satellite build, so this is a real guard on all 29
  edited files, not a formality.
- Full suite with the repository's CI filter: **577 passed / 0 failed**, up from
  570 by exactly the 7 new tests.
- CI's publish invocation and `utils/post-build.py` run locally: **23
  `Lang\<culture>\Thrum.resources.dll` satellites** in the packaged output and
  in `Thrum_0.9.0-beta.1_x64.zip`, no file in the package matching `DS4W*`.
- `git grep -in "ds4windows"`: **1,805 → 1,659, down 146.** The first Phase 1
  change where the number falls, and it falls in the right places: `.resx` −137,
  designer −13, `.cs` −15, `.xaml` −7, against +16 in documentation (this entry
  and the identity map's new sections) and +10 in tests (the new guards' needles).
- The GUI application was **not** launched.

### Deviations from the plan

1. **Dead keys were listed, not purged.** The task text anticipated this and
   asked for the list; recorded here as a deviation from the *plan's* task 1.8
   wording ("purge dead ViGEm strings"), with the reasoning above.
2. **Dead values were not flipped either.** The sweep's allowlist is live keys
   only. A string no user can reach is not part of a user-visible sweep, and
   the dead-key list is the honest record.
3. **One neutral value was rewritten rather than token-swapped**
   (`CustomExeNameInfo`), because the swap alone would have left a sentence
   describing the deleted updater. Its 24 translations are flagged for
   retranslation.
4. **`QuitOtherPrograms` is in the allowlist even though it must not change.**
   Keeping it there makes the script *report* the URL-protected occurrence on
   every run, so the decision is mechanical output rather than a claim in a
   document.
5. **Two fixes outside the localization category were taken**: the game-audio
   detector's exclusion set (a rename regression) and the audio-pacer pipe name
   (a §4 identity anchor). Both were found by this sweep, both are one line,
   and leaving a known rename regression in place to protect a scope boundary
   would have been the wrong trade.
6. **`SettingsImportTests` gained a culture pin.** Moving the summary wording
   into resources would otherwise have turned two hermetic tests into
   machine-culture-dependent ones.
7. **The partial-import failure message box in `App.xaml.cs` was localized too**,
   although the task scoped the import work to the dialog's own code-behind.
   Leaving one of the feature's four user-visible strings hard-coded would have
   been arbitrary.

### Next steps

- **Phase 1 code scope is complete.** What remains for Phase 1 acceptance is the
  manual pass: [`smoke-rebrand.md`](smoke-rebrand.md) plus the icon and
  update-feed items queued by 1.6/1.7, and the side-by-side run against a real
  DS4Windows install. Items 1, 3, 7 and 9 of the checklist are the ones no CI
  run can stand in for. Two additions from this session: switch the UI language
  and confirm a translated page still reads correctly after the value edits, and
  check the import dialog renders its resource-driven text at the dialog's fixed
  520×360 size.
- Open decisions carried forward: the OSC address namespace (1), the
  `DS4WINDOWS_*` environment variables (4), the "Support DS4Windows" PayPal card
  (6, new), the 142 dead resource keys (7, new), and shipping the Indonesian
  translation (8, new). Plus the `NOTICE.txt` audit from 0.5.

---

## 2026-07-26 — Issue #6: satellite assemblies resolved against the working directory

**Session scope:** [issue #6](https://github.com/potpiemuncher/Thrum/issues/6),
found by the Phase 1 smoke pass ([`smoke-rebrand.md`](smoke-rebrand.md) item 8).
Branch `fix/satellite-probing`. Not a plan task: an inherited bug the rebrand
smoke checklist surfaced.

### The bug

Changing the UI language did nothing. The setting persisted, the app restarted,
the interface stayed English — and nothing anywhere said why.

The satellites live at `<install>\Lang\<culture>\Thrum.resources.dll`, because
`utils/post-build.py` sweeps every culture folder MSBuild emits into one `Lang`
folder rather than scattering 25 of them through the install root. Nothing in
the CLR looks there, so `runtimeconfig.template.json` declares

```json
"additionalProbingPaths": [ "./Lang/" ]
```

That path is **relative**, and the host turns it into an absolute path against
the process **current working directory**, at startup, before any managed code
runs. Start the application anywhere else and it finds none of its 23
translations.

The `Environment.CurrentDirectory = exeDir` that `App.Application_Startup`
already performs is not a fix and is in fact the proof: by the time it runs the
host has long since baked the probing path.

The failure is silent by construction. A missing satellite is not an error — it
is the signal to fall back to the neutral resources, which is exactly what a
correctly configured English install looks like.

### Where it bites

- **Logon scheduled task** (`RunThrum`, the elevated startup option) — Task
  Scheduler gives the process `C:\Windows\System32`.
- **Startup shortcut** or any `.lnk` with an empty "Start in".
- **A terminal in another folder**, which is how the smoke pass hit it.

Every one of those is a normal way to start this application.

### The fix

New `DS4Windows/DS4Control/SatelliteAssemblyResolver.cs`: an
`AssemblyLoadContext.Default.Resolving` handler that maps a requested
`<name>.resources` assembly onto
`Path.Combine(AppContext.BaseDirectory, Global.PROBING_PATH, <culture>, <name>.dll)`.
`AppContext.BaseDirectory` is the folder holding the executable regardless of
where the process was started, which is the whole of the change.

**Registered from a `[ModuleInitializer]`, not from startup.** The handler has
to be in place before the first resource lookup, and `Application_Startup` is
already too late to guarantee that: by then the WPF entry point has constructed
`App` — running its static field initializers — and `InitializeComponent` has
applied `App.xaml`, whose merged dictionaries and `WPFLocalizeExtension` markup
can reach the resource manager. A static constructor on `App` is earlier but
still runs after that type's own field initializers. A module initializer is
emitted into the module's `.cctor`, which the runtime runs before *any* method
of this assembly executes, `Main` included. Nothing in this assembly is earlier.

The price of being that early is that it must not fail: an exception there is a
`TypeInitializationException` before `Main` and the process never starts. So it
does one thing and swallows, and `Installed` records whether it worked.

Four properties the handler has to have, and why:

| Property | Reason |
|---|---|
| Answers only for simple names ending `.resources`, null otherwise | A resolving handler that answers for ordinary assemblies can shadow the real one. This one is inert for everything else. |
| Walks parent cultures, `pt-BR` → `pt`, stopping before invariant | `CultureInfo.Parent`, not "strip the last segment", because they disagree (`zh-TW`'s parent is `zh-Hant`). Falls back to the textual walk for a culture ICU does not know. |
| Never throws | Returning null is what lets the runtime fall back to the neutral resources. Throwing out of assembly resolution takes the lookup, and whatever was rendering, with it. |
| Refuses a culture name that is not a legal folder name | The culture arrives inside the requested assembly name, so it is untrusted input; it is checked before it is combined into a path. |

It also runs after everything else, by construction: the default context raises
`Resolving` only once the host probing paths *and* the CLR's own
`<base>\<culture>\` satellite probe have both failed, so it can never shadow an
assembly the runtime would otherwise have found.

**The satellite file name is composed from the requested simple name, not from
`ProductInfo.LanguageAssemblyName`.** That is deliberate and it fixes more than
it was aimed at: `post-build.py` moves *every* culture folder under `Lang/`, so
a dependency's satellites end up there too. The packaged x64 build contains
eight `Microsoft.Win32.TaskScheduler.resources.dll` files under
`Lang\{de,es,fr,it,pl,ru,zh-CN,zh-Hant}\` which were unreachable in exactly the
same way and are now resolved by the same handler. A test pins that the
composition still produces `ProductInfo.LanguageAssemblyName` for our own
satellites, so the general form cannot drift away from the specific one
unnoticed. The probing folder is `Global.PROBING_PATH`, split on `;` the way
`LanguagePackViewModel` splits it — the folder the language packs are *listed*
from and the folder they are *loaded* from now cannot disagree.

### `additionalProbingPaths` was kept

Three reasons, none of them inertia. It still resolves everything in the common
case — launch from the install folder and this handler is never called at all.
It cannot conflict: the handler only ever runs after host probing has failed, so
there is no path on which both fire. And the template is inherited verbatim from
upstream, so removing it would add a fork delta for no gain, against the
mergeability rule.

### Tests: 577 → 587

New `DS4WindowsTests/SatelliteAssemblyResolutionTests.cs`, 10 tests. The primary
guard is the **pure mapping function**: `CandidatePaths` takes the base
directory as an argument, so no working directory can enter the answer even in
principle, and it touches no file system. One narrow integration test then shows
the mapping is not merely self-consistent — a real satellite really loads
through it — with the working directory moved to `C:\Windows\System32`.

| Test | What it pins |
|---|---|
| `TheHandlerIsInstalledBeforeAnyOfThisAssemblysCodeRuns` | reading `Installed` is itself a call into the module, so the runtime must have run the module initializer to answer it — a true here *is* the "registered before `Main`" guarantee |
| `ASatelliteMapsUnderTheBaseDirectorysProbingFolder` | the one candidate is composed from `Global.PROBING_PATH` and `ProductInfo.LanguageAssemblyName`, the same constants the packaging uses |
| `EveryCandidateIsRootedAtTheGivenBaseDirectory` | every candidate is absolute and under the given base — a relative candidate is one the working directory can still move |
| `TheAnswerDoesNotDependOnTheWorkingDirectory` | identical output either side of a `SetCurrentDirectory` |
| `AParentCultureIsTriedAfterTheSpecificOne` | `pt-BR` then `pt`, in that order |
| `TheChainStopsBeforeTheInvariantCulture` | no `Lang\<empty>` candidate; a request with no culture is not a satellite request |
| `NothingButASatelliteIsHandled` | inert for `Thrum`, `NAudio`, `Thrum.resourcesx`, `resources`, empty, null name and null base |
| `ACultureNameThatIsNotAFolderNameIsRefused` | a culture reporting `..\..\Windows\System32` yields no candidate |
| `TheHandlerLoadsARealSatelliteWithTheWorkingDirectoryElsewhere` | a genuine `de` satellite, in the packaged `Lang\<culture>\` layout, loads with the working directory in `System32` |
| `AMissingSatelliteIsNullAndNotAnException` | absent file ⇒ null, so the neutral fallback still applies |

The class is `[DoNotParallelize]`. This assembly declares no
`[assembly: Parallelize]`, so MSTest already runs it sequentially and the two
tests that move the working directory cannot race; the attribute states the
requirement so that enabling parallelism later fails loudly instead of flaking.
Both restore the previous directory in a `finally`. The integration test loads
into a collectible context of its own, because the same satellite is already in
the default context in this process and a second copy of one identity there
would fail for reasons unrelated to what is being tested.

**Negative controls, all run, all fired, source restored byte-identical
afterwards:**

- Removed `[ModuleInitializer]` → the registration test failed, naming the
  consequence.
- Made the candidate relative again — `Path.Combine(probingPath, …)`, i.e. the
  behaviour being fixed → **5 tests failed**, the integration test among them.
  That is the control that matters: with a relative path and the working
  directory in `System32`, the satellite is not found.
- Dropped the `.resources` suffix check → `NothingButASatelliteIsHandled` failed
  with `The resolver claimed a non-satellite assembly: 'Thrum'`.
- Dropped the folder-name guard → the hostile-culture test failed.
- Dropped the parent-culture walk → `AParentCultureIsTriedAfterTheSpecificOne`
  failed.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**,
  17 warnings, identical to the inherited baseline.
- Full suite with the repository's CI filter: **587 passed / 0 failed**, up from
  577 by exactly the 10 new tests.
- CI's publish invocation and `utils/post-build.py` run locally: 23
  `Lang\<culture>\Thrum.resources.dll` in the packaged output, unchanged.

**The packaged application, launched twice, satellites counted in the live
process** with `(Get-Process Thrum).Modules`. Same package layout, same
persisted `UseLang=de`, same 20-second settle, only the working directory
differs. The "before" row is a build of this same tree with the resolver file
removed, measured the same way, so the two rows are directly comparable rather
than quoted from the issue:

| Build | `-WorkingDirectory <install>` | `-WorkingDirectory C:\` |
|---|---:|---:|
| before (no resolver) | 23 | **0** |
| after (resolver) | 23 | **23** |

In the fixed `C:\` run the loaded modules report their real paths as
`<install>\Lang\<culture>\Thrum.resources.dll`, so they came through the
handler and not through some other probe. Each run was shut down with
`Thrum.exe -command shutdown` and confirmed gone before the next; no process was
left running.

`%APPDATA%\Thrum` was backed up before the runs. `UseLang` was already `de`, so
nothing had to be set; the only difference afterwards is the timestamp comment
the app rewrites into `Profiles.xml` on every exit. No setting changed.

### Deviations

1. **The packaging step ran in `bin\x64\Release2\` for the measurements, not
   `bin\x64\Release\`.** An unrelated process on this machine held a handle to
   the pre-existing `bin\x64\Release\Thrum` folder — almost certainly a File
   Explorer window left open by the smoke pass that found this bug — so
   `post-build.py`'s `rmtree`/`rename` could not replace it. The script ran
   unmodified against a sibling path, which changes nothing about the package it
   produces. The canonical folder was repopulated from the fresh build
   afterwards; the empty directory entry itself could not be removed and will
   clear when the holder does.
2. **The handler is general over `.resources`, not specific to this product.**
   Scoping it to `ProductInfo.LanguageAssemblyName` would have left the eight
   `Microsoft.Win32.TaskScheduler` satellites broken, for no safety gained — the
   handler is a fallback that runs only after the runtime has already failed.
3. **A smoke-checklist step was added rather than only a code fix.** Item 8 of
   `smoke-rebrand.md` now has a step 5 that starts the packaged executable from
   another working directory. The checklist found this bug once by accident; it
   should find it on purpose next time.

### Worth reporting upstream

**Yes.** The `Lang/` layout, the relative `additionalProbingPaths` and the
absence of any resolving handler are all inherited unchanged from
hbashton/DS4Windows, and the mechanism does not depend on the assembly name, so
upstream loses its translations under exactly the same conditions — including
its own "run at logon" scheduled task. The fix is one self-contained file plus a
test file and touches no engine code, which makes it a clean candidate for the
contribution sequence.

---

## 2026-07-26 — Phase 1 smoke pass (acceptance) and the two issues it found

**Session scope:** running `docs/dev/smoke-rebrand.md` end to end with the
maintainer present, on build **`d644d33`**. This is the acceptance gate Phase 1
was waiting on.

### Result

**Phase 1 accepted.**

| Item | Result |
|---|---|
| 1 | PASS |
| 2 | PASS |
| 3 | PASS — all three dismissal paths (button, Escape, title-bar X) |
| 4 | PASS |
| 5 | PASS |
| 6 | PASS |
| 7 | PASS |
| 8 | PASS |
| 8a | PASS |
| 9 | PASS |
| 10 | **N/A** — HidHide is not installed on the test machine |
| 11 | PASS |
| 12 | PASS |

One failure was found *during* the pass and fixed before it ended:
[#6](https://github.com/potpiemuncher/Thrum/issues/6), satellite assemblies
resolved against the working directory, fixed in
[PR #7](https://github.com/potpiemuncher/Thrum/pull/7) — its own entry is above.

Two issues were filed for later rather than fixed in the pass:

- [#8](https://github.com/potpiemuncher/Thrum/issues/8) — the managed VIIPER
  backend self-updates from the wrong repository through an elevated remote
  script. Folded into plan task 2.4b, because it is fixed on the spawn path.
- [#9](https://github.com/potpiemuncher/Thrum/issues/9) — orphaned `task.bat`.
  Deferred to Phase 5.4.

### Practical note for every future smoke pass: sample logs *after* the app exits

NLog's async target buffers, so a running session's lines reach disk late. Read
the log while the app is still up and it will look like the code never ran.
This cost real debugging time during this pass — an event was observed in the UI
and was simply absent from the file until the app was closed.

Two consequences worth internalising:

- **Close the app first, then read the log.** Not "wait a bit". The flush
  happens in `App.CleanShutdown` (`LogManager.Flush()` / `LogManager.Shutdown()`).
- **Each run rotates the previous file** into
  `%APPDATA%\Thrum\Logs\thrum_log_<date>.<n>.txt`. Evidence from the run before
  last is in an archived file, not in `thrum_log.txt`. Search the whole folder,
  not just the live file.

---

## 2026-07-26 — Phase 2.4b (backend lifecycle ownership) + issue #8

**Session scope:** plan task 2.4b in full, and
[issue #8](https://github.com/potpiemuncher/Thrum/issues/8), which the plan
folds into it because both live on the backend spawn path.

The starting position, established by live testing earlier the same day: Thrum
starts `viiper.exe server` on demand and the server **outlives the app** — a
running server's parent process id belonged to a Thrum that had already exited.
The machine has no VIIPER autostart of either kind, so the backend there is
purely on-demand.

### What was built

**Ownership is (process id, process start time), in memory only.**
`ViiperSetupManager` records the pair when it spawns the backend and exposes it
as `OwnedBackend`. A process id on its own is not an identity — Windows reuses
them, and the gap between spawning the backend and stopping it is a whole
session — so a record resolves to a live process only when both halves still
match. Nothing is persisted: a crashed session must not hand a later session a
licence to kill a backend a third party has since started.

**The consumer signal is the backend's own device census, and it fails safe.**
By the time the stop is considered, every virtual device Thrum created has
already been unplugged, detached and removed, so an idle backend is hosting
nothing at all. `bus/list` plus `bus/{id}/list` answers "is it hosting
anything?", and anything still registered blocks the stop:

| What the census shows | Decision | Why |
|---|---|---|
| a device we did not create | leave running | another consumer — a real DS4Windows install, or a second copy of this app |
| a device we *did* create | leave running | our own teardown has not finished; killing now is exactly the ordering the teardown exists to avoid |
| an empty bus | leave running | state somebody asked the backend to hold; ours are gone by this point |
| census failed for any reason | leave running | an unverifiable claim of idleness is not idleness |
| nothing registered | **stop** | the only case that is affirmatively safe |

Limits, recorded in the source next to the policy: this is a *device* census,
not a *client* census — the API exposes no list of connected clients, so a
consumer that is attached while holding no device is invisible to it, and a
consumer could create a device in the window between the census and the stop.
Neither is fixable from the client side and both are narrow. Everything else
resolves toward leaving the process alone, because a backend left running costs
a few megabytes and a backend killed under a live consumer costs that consumer
its controller.

**Graceful stop works; `Kill` is only the fallback.** VIIPER's server installs
`signal.NotifyContext` for `os.Interrupt`/`SIGTERM`, and Go's Windows runtime
raises `os.Interrupt` for `CTRL_BREAK_EVENT` as well as `CTRL_C_EVENT`. The
backend is spawned windowless, but `CreateNoWindow` maps to `CREATE_NO_WINDOW`,
which still gives the child a console — one that is simply never displayed. So
the app joins that console with `AttachConsole` and raises the event there.

Two details that are easy to get wrong, both verified rather than assumed:

- The event reaches **every** process on that console, including ours. A
  handler that swallows it is installed first.
  `SetConsoleCtrlHandler(NULL, TRUE)` is *not* sufficient — it suppresses only
  `CTRL_C_EVENT`, and the default handler for `CTRL_BREAK_EVENT` terminates the
  process that receives it.
- `--update-notify` is declared on VIIPER's root command, so it precedes the
  `server` subcommand.

This was proved before any of it was written into the app, with a throwaway
`WinExe` harness (no console of its own, mirroring Thrum) that spawned the real
backend and measured the result: `AttachConsole` succeeded, and the backend
exited with code 0 **within 5 ms** of the console break, with the API port
closed immediately afterwards. Escalation to `Process.Kill` exists and is
acceptable for this backend — losing the USB-IP peer is the clean unplug path,
cleaner than `usbip detach`, which can livelock while an audio pin is held — but
it did not have to run.

**Ordering.** The stop is called from `App.CleanShutdown`, after the
`rootHub.Stop`/`ShutDown` task has completed, which is what unplugged the pads,
detached the usbip ports and sent `bus/remove`. It is **skipped entirely** when
that task times out, because a timeout means we do not know the teardown
finished.

**Issue #8 — the backend's self-updater is disabled at spawn.** Every backend
Thrum starts is now started with `--update-notify none` *and*
`VIIPER_UPDATE_NOTIFY=none`. The flag is what takes effect; the variable is what
a re-exec would inherit. `cmd/viiper/viiper.go` guards the entire updater on
`cli.UpdateNotify != none`, so this is a complete disable rather than a
suppressed dialog. Three tests assert the argument vector and the environment,
so it cannot regress silently.

**Autostart visibility.** Settings now reports VIIPER's own logon entries — the
`HKCU\...\Run` value `VIIPER` written by `viiper.exe install`, and the
`RunVIIPER` logon task the setup script registers — with a one-click removal
that is guarded by a confirmation naming exactly what will be deleted.
Detection is read-only and unconditional; nothing is removed without that click.
A lookup that throws is reported as *unchecked*, never as *absent*.

**The setting.** "Stop the backend when Thrum exits", **default ON**, persisted
as `<StopViiperBackendOnExit>` through the existing `AppSettingsDTO` pattern. It
uses the string-proxy form (`[XmlIgnore] bool` + `[XmlElement] string`) rather
than a plain `bool` element, for two reasons that both matter for a default-on
flag: a config written before the element existed leaves the setter unrun so the
`true` initializer survives, and a malformed value is ignored instead of
throwing out of `Deserialize` — which `BackingStore.Load` handles by abandoning
the entire settings file. The view model saves on change rather than on exit,
because the setting is *read* during exit.

### Tests: 587 → 626

39 new tests across two files. `DS4WindowsTests/ViiperBackendLifecycleTests.cs`
(27) and `DS4WindowsTests/ViiperAutostartTests.cs` (12). Everything runs against
fakes: an injected request function for the census, an injected source for the
autostart lookups. No test touches the registry, the task scheduler, or a real
backend.

The autostart tests are deliberately fake-only. Neither mechanism exists on the
test machine, and creating one there in order to test deleting it would mean
writing autostart entries onto somebody's PC to prove we can remove them.

**Negative controls, all run, all fired, source restored afterwards:**

- Dropped `--update-notify none` from the spawn vector → 2 failures, including
  the one that names the consequence.
- Made ownership compare the process id only, ignoring start time → 3 failures,
  among them the test that builds a record from a live process and checks a
  shifted start time no longer resolves. That is the control that matters: it
  shows the reuse guard is exercised against the real API, not just arithmetic.
- Made a foreign device stop blocking the shutdown →
  `ADeviceWeDidNotCreateIsTreatedAsAnotherConsumer` failed.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**, no
  new warnings.
- Full suite with the repository's CI filter: **626 passed / 0 failed**, up from
  587 by exactly the 39 new tests.

**The packaged application, run twice, with process evidence.** Both runs
non-elevated (`-command shutdown` cannot reach an elevated instance from a
non-elevated shell), both preceded by a confirmed-clean process table.

*A backend Thrum started is stopped.* Thrum launched with no `viiper.exe`
running; a child appeared with **parent process id equal to Thrum's own**, and a
command line of `viiper.exe --update-notify none server` — the issue #8 fix,
observed on the live process rather than inferred from the source. After
`-command shutdown`: that process id gone, and no `viiper.exe` on the machine at
all. The log line, read after exit:

```
VIIPER backend stop (pid <n> started <t>): we started it and it is hosting no
buses or devices - console break accepted; backend exited on its own.
```

*A backend started externally is left alone.* `viiper.exe server` started
directly from a shell (parent = that shell, no `--update-notify` argument), then
Thrum launched and exited. Thrum did **not** spawn a second backend — one
`viiper.exe` throughout — and after Thrum exited the external process was still
running with an unchanged start time. Log line:

```
VIIPER backend left running: the backend was already running before Thrum
started, so it is not ours to stop.
```

The settings file was backed up before the runs. Afterwards it differs by
exactly two lines: the timestamp comment the app rewrites on every exit, and the
new `<StopViiperBackendOnExit>True</StopViiperBackendOnExit>` persisting at its
default. No other setting changed, and no `viiper.exe` or `Thrum.exe` was left
behind.

### Deviations and things left open

1. **The "another consumer's device" branch is covered by unit tests only, not
   live.** Exercising it for real needs a virtual device attached to a backend
   Thrum owns, and Part 3's kernel-driver rule puts device attach behind a VM
   checkpoint or explicit per-session approval. Neither applied to this session.
   The branch is covered by tests plus a negative control that proves the guard
   is load-bearing; the *ownership* branch was exercised live.
2. **An empty bus blocks the stop.** Slightly stricter than "any device blocks
   the stop". By the time the check runs, ours are gone, so a bus that is still
   there was asked for by somebody else. In practice the idle backend reports
   `{"buses":[]}` and the stop proceeds — confirmed in both packaged runs.
3. **`AppSettingsTests.CheckSettingsSave` was left failing.** It compares
   serialized output to a hardcoded XML literal, and that literal was already
   stale before this change — it is missing `ProfileChangedNotification`,
   `UseMoonlight`, `UseAdvancedMoonlight` and `VerboseStartupLogging`. It is one
   of the three snapshot tests the CI filter excludes and that the plan
   regenerates in Phase 6. Adding one more element to a fixture that cannot pass
   would not have made it pass. The round trip *is* covered, by four new tests
   that serialize and deserialize the DTO directly instead of comparing against
   a snapshot.
4. **Our own bundled `extras/install-viiper-backend.ps1` registers
   `RunVIIPER`.** So the detection added here will fire for anyone who used it.
   That is the installer's problem to fix, under task 2.4 and the Phase 5.3
   constraint that the installer must not register autorun; noting it here so
   the two tasks stay connected.
5. **Log strings are ASCII.** NLog writes UTF-8 without a BOM and the rest of
   the file is plain ASCII, so a dash outside ASCII in a decision line renders
   as mojibake for anyone reading the log with a system-codepage editor. Caught
   on the first packaged run and changed before the second.

### Worth reporting upstream

**Yes, one item, already written up.** The updater in hbashton/VIIPER pointing
at `Alia5/VIIPER` is near-certainly an unintended fork leftover: the fork's
updater checks the parent repository, whose version line is permanently ahead,
and offers to install the parent's build over the fork. Issue #8 records the
detail. Our fix protects backends *we* start; it does nothing for a backend
started by the `RunVIIPER` task or by the user, which is the other half of why
autostart entries are now surfaced in Settings. The upstream report stays [EXT]
under the plan's contribution sequence.

---

## 2026-07-26 — First upstream merge cycle (`5d2724a..8a2b715`), a Phase 2 prerequisite

**Session scope:** the first merge cycle under
[ADR-0002](ADR-0002-upstream-tracking.md), plus the written analysis that makes
it worth doing. No feature work: the code diff is conflict resolution only.

Full analysis: [`upstream-delta-2026-07-26.md`](upstream-delta-2026-07-26.md).

### The merge

`upstream-track` fast-forwarded `5d2724a` → **`8a2b715`**, four commits, all
VIIPER installer hardening, 2 files, +214/-23:

| Commit | Subject |
|---|---|
| `db21d7e` | Harden VIIPER installer against conversion and task registration failures |
| `fac5467` | Use only hbashton VIIPER repo in installer source lookup |
| `3937d26` | Make VIIPER setup close running viiper before registration |
| `8a2b715` | Improve VIIPER install completion behavior |

`git merge --no-ff upstream-track` produced **zero textual conflicts**. Every
file flagged as high-risk before the merge — `ScpUtil.cs`, `ProductInfo.cs`,
`App.xaml.cs`, `StartupMethods.cs`, `Directory.Build.props`, the workflows, the
resx set, the rest of `DS4Control/Viiper/` — was untouched upstream. Our 2.4b
additions to `ViiperSetupManager.cs` and upstream's edits landed in disjoint
regions, and our installer-script changes are branding-only against upstream's
functional ones.

Which is exactly the case worth being suspicious of. The real conflicts were
semantic, and a clean `git merge` reported none of them.

### The three semantic conflicts

1. **`8a2b715` reintroduced our product's old identity.** Its new
   `RestartDs4Windows` composes `Path.Combine(Global.exedirpath,
   "DS4Windows.exe")` and names DS4Windows in four user-visible strings. In our
   tree that path does not exist, so the feature would have silently degraded to
   a log line naming a product that is not installed. **Ours wins**, on the
   ground that this is precisely the coupling `ProductInfo` exists to own.
   Resolved with `Global.exelocation` rather than a composed
   `ExeBaseName + ".exe"` — it is the executable actually running, so it also
   survives a portable copy under a different filename and the junction/Scoop
   case that `exelocation` already resolves and that upstream's own comment says
   it cares about. Method renamed `RestartApplication`.
2. **`{logPath}` shown literally.** `3937d26` split the failure dialog into
   three concatenated fragments and put the `$` only on the first. One
   character, fixed here, reported as an upstream defect.
3. **Upstream's auto-restart cannot restart — taken as-is, recorded, not
   fixed.** `RestartApplication` starts the replacement process *before*
   shutting the current one down, so the new instance finds the single-instance
   event still held and exits immediately while the old one finishes
   `CleanShutdown`. In our tree it is worse: the restart is reached right after
   `GetStatus(tryStartServer: true)`, which may have started and recorded
   ownership of the backend, so `StopOwnedBackendOnExit` then stops it — leaving
   no app and no backend after an install whose purpose was to provide one.
   Fixing the ordering is a behaviour change to upstream's new feature and was
   out of scope for a merge PR; it must land before any release.

Nothing of ours was dropped to take upstream's version. Two upstream changes are
genuinely better than what we had and are kept verbatim: `Stop-ViiperProcesses`
turning a best-effort `Stop-Process` into a retrying, escalating, **fail-closed**
check, and `ConvertTo-VersionFromObject` making a version probe total.

### Task 2.4: what upstream did and did not do for us

Of 2.4's seven requirements, **one** is satisfied upstream (the pinned exact
usbip-win2 release URL), **two** are partial (atomic install with `.previous` —
atomic yes, but the backup is deleted on success; and decision logging — good
for the decisions that exist, and the verification decisions do not exist yet),
and **four** are still entirely ours: SHA-256 before execution, Authenticode
subject verification before execution, post-install validation of the actual
package *pair* (only `usbip2_ude.sys`'s FileVersion is read; `usbip2_filter.sys`
never is, and a version floor is not a validation), and no-silent-acceptance —
which is two separate holes, an `-ge 0.9.7.7` floor that silently accepts the
known-risk 0.9.7.8, and a VIIPER asset resolver that installs whatever the newest
non-draft release happens to be. There is no `Get-FileHash` and no
`Get-AuthenticodeSignature` anywhere in `extras/`.

So 2.4's scope barely shrinks — but its *shape* changes, because it now has to be
written on top of `Stop-ViiperProcesses`, `Register-ViiperRunTask` and
`ConvertTo-VersionFromObject` instead of the code it was drafted against.

### `RunVIIPER`: still registered, and now harder

**Yes, and upstream pushed in the opposite direction from our 5.3 constraint.**
The merged script creates **two** autostart mechanisms, unconditionally, on every
install and repair: the `HKCU\…\Run` value `VIIPER` written by
`viiper.exe install`, and the `RunVIIPER` at-logon task. `db21d7e` gave the task
a `schtasks.exe` fallback so registration now succeeds where it previously
aborted; `3937d26` made the `viiper.exe install` step more mandatory, stopping
every backend first and throwing an actionable error if it still fails.

Three consequences for 2.4, none implemented here:

- The removal is not "delete one block". The `viiper.exe install` invocation must
  go or be replaced too, and it is currently load-bearing for upstream's new
  error handling — so 2.4 has to decide what "Registering VIIPER" still means.
- Neither entry passes `--update-notify none`, and neither does the script's own
  `Start-AndVerifyViiper` (pre-existing, untouched by upstream and by 2.4b). A
  backend started by any of the three has the issue #8 updater fully live. Our
  2.4b fix covers only backends we spawn — which is why 2.4b surfaces both
  autostart entries in Settings, and why that detection is not transitional.
- Mergeability: our removal will rewrite lines upstream has just worked on. Per
  ADR-0002 §4, prefer a shape that leaves upstream's functions intact and changes
  only whether they are *called*.

`fac5467` is the one upstream change that helps directly: it reverts a same-day
addition of `Alia5/VIIPER` as a second release source, so the bundled script will
not install a foreign VIIPER build. That is the installer-side complement to our
runtime issue #8 fix.

### Effect on the rest of Phase 2

2.2, 2.3 and 2.5: no change. 2.1: no design change, but `8a2b715` added a second
consumer of readiness (the restart branches on `refreshed.Ready`), so the
four-state enum needs a usable "ready" projection and that decision should key
off the new states. 2.4b: every conclusion holds, and its recorded open
follow-up — our own script registers `RunVIIPER` — is confirmed and strengthened.
Phase 5.3 inherits the two-mechanism finding.

### Verification

All seven regression-critical invariants re-checked after the merge and intact:
`--update-notify none` on spawn (with its three assertions), stop-on-exit
ownership and its `StopViiperBackendOnExit` round trip, the `[ModuleInitializer]`
satellite resolver, `ProductInfo` values, the identity sweep, and the import
wizard plus its `import-declined.txt` marker.

The identity sweep is the one that moved: the merge added **8** `DS4Windows`
occurrences and the resolution removed exactly those 8, so `git grep -ic
ds4windows` totals **1715** on both `main` and the merge branch. A targeted
anchor sweep returns only the pre-existing documented residue — historical
comments, and the `DS4Updater` mention in the custom-exe-name help string across
22 translations that the 1.8 sweep left alone because it names a different
product.

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**, 17
  pre-existing warnings.
- Full suite with the CI filter — **626 passed / 0 failed**. No delta: upstream
  added no tests, and neither of its two files has coverage upstream or here.
- Divergence budget (ADR-0002 §5): **11,987** added lines outside `docs/` and
  `*.md`, against the ~15,000 alarm. No review triggered.

### Deviations and things left open

1. **The auto-restart defect is merged, not fixed** (above). Highest-priority
   follow-up on the installer path; must not reach a release.
2. **Nothing in the installer script was executed.** Running it installs the
   usbip-win2 kernel driver, which Part 3 rule 1 puts behind a TESTENV
   checkpoint. The whole 2.4 and `RunVIIPER` analysis is source-level reading of
   the merged script. When 2.4 takes its [VM] pass, the restart defect and both
   autostart registrations should be observed directly rather than inferred.
3. **`Stop-ViiperProcesses` contradicts 2.4b's ownership policy** — the script
   kills every `viiper.exe`, including another consumer's, where the app refuses
   to. Defensible for an explicit elevated install (you cannot replace a running
   image otherwise) and left alone, but 2.4 should state the two policies
   together. Our ownership record is safe against it: matching on (pid, start
   time) means a killed backend simply fails to resolve and the exit path does
   nothing.
4. **Three upstream reports are drafted and unfiled [EXT]:** the restart
   ordering, the `{logPath}` fragment, and the three unflagged backend spawns.

---

## 2026-07-26 — Phase 2.1 + 2.2: four-state driver readiness and the Settings status card

**Session scope:** tasks **2.1** (wire `ViiperDriverGate` into readiness) and
**2.2** (Settings driver-status card). Explicitly not 2.3, 2.4 or 2.5: this PR
computes and shows the state and changes no behaviour that depends on it.

The fail-closed validation layer landed in Phase 0 and has been reachable only
from `-viiperdriverdiagnostic` and its tests ever since. It is now part of the
product.

### The four-state mapping

`ViiperDriverReadinessResolver.Resolve` maps one read-only
`ViiperDriverValidationReport` onto the state. The order of the checks *is* the
fail-closed policy, so it is written out rather than left implicit:

| # | Condition | State |
|---|---|---|
| 1 | report is null | `DetectedUnvalidated` |
| 2 | package enumeration threw | `DetectedUnvalidated` |
| 3 | enumeration completed, neither package found | `Missing` |
| 4 | client read threw, or no validation result | `DetectedUnvalidated` |
| 5 | validation passed, matched tier `Production` | `Approved` |
| 6 | validation passed, matched tier `ExperimentalBaseline` | `ValidatedExperimental` |
| 7 | anything else (one of the pair missing, mixed pair, unlisted version, wrong provider/INF/architecture, unhealthy node, test-signed, expired, revoked, developer-signed, untrusted, wrong publisher, trust verification threw) | `DetectedUnvalidated` |

Two properties this ordering is chosen for:

- **Only rows 5 and 6 can produce a state better than `DetectedUnvalidated`,
  and both require the authoritative `ViiperDriverValidator.Validate` to have
  passed.** No amount of partial evidence gets there.
- **Only row 3 can produce `Missing`, and it requires the enumeration to have
  completed.** An unreadable machine is not an empty one — that distinction is
  the reason row 2 sits above row 3, and it has its own test.

`Approved` is unreachable in the shipped product: the manifest has no
`Production` entry, and `RealManifest_HasNoProductionEntry` guards that it
cannot appear by accident. The tier is exercised through
`ViiperDriverManifest.FromReleases` with a fabricated 9.9.9.9 Production entry —
a new `internal` seam, so proving the path did not require weakening the real
manifest.

Reasons are carried on every non-`Missing` state and are deliberately **not**
cleared on a match: a passing result with a leftover trust concern would be a
contradiction worth showing rather than erasing. Identity is exposed as a
projection (`ViiperDriverComponentIdentity`), never the raw
`ViiperDriverPackageInfo`, because that record carries `TrustEvaluationPath` — a
driver-store path that must not reach the UI, a log, or a report. A test walks
every rendered string to assert it never does.

### What `Ready` means now, and who was affected

**`ViiperPrerequisiteStatus.Ready` is unchanged: `ServerRunning &&
UsbipInstalled`.** The tier rides alongside as a new `DriverReadiness` property.
`UsbipInstalled` also keeps its existing weak heuristic rather than being
re-derived from the gate.

This is a deliberate decision, not an oversight. `Ready` has six kinds of
consumer — `EnsureReadyWithPrompt` (the profile-time prompt), `ViiperOutDevice`
×2 (attach paths), `ViiperBackendDebugger` ×5, `WelcomeDialog` ×2,
`MainWindowsViewModel`, and upstream's new `InstallerProcess_Exited` restart
branch — and every one of them is asking the *transport* question: can the
backend run. Making `Ready` false for `DetectedUnvalidated` would silently
convert a validation result into a functional refusal in flows that never opted
into one, inside a PR whose whole point is that it changes no behaviour.
Refusal is 2.3 and 2.5, where it is an explicit, disclosed decision.

Callers affected in this PR: **none behaviourally**. Two mechanical changes:

1. `GetStatus` now also populates `DriverReadiness` from the session cache.
2. `InstallerProcess_Exited` calls `RefreshDriverReadiness()` before
   `GetStatus`. An install is the one event that can change the answer, so the
   cache must not be reported stale to the `refreshed.Ready` branch that
   `8a2b715` added. The restart condition itself is untouched — it is upstream's
   new feature and it has a known ordering defect
   (`upstream-delta-2026-07-26.md` §3.3) that belongs to whoever fixes that.

### Caching

`ViiperDriverReadinessProvider` evaluates once per session and hands out the
cached answer; `Refresh()` is the only thing that re-reads the machine. Measured
cost of one pass on the dev PC: the whole `-viiperdriverdiagnostic` process runs
in ~1.0 s wall clock including .NET startup, so the inspection itself is a few
hundred milliseconds — against the up-to-1000 ms TCP timeout `CanPingServer`
already spends in the same method.

`Adopt(report)` publishes a readiness derived from a report somebody already
paid for, so "Run full diagnostic" refreshes the card without a second
enumeration.

### The card

In the existing VIIPER group in Settings, as a `BridgeCardStyle` card: state
badge, headline, tier note, restriction line, reason list, detected package
identity per component, and Re-check / Run full diagnostic / Copy report / Open
report.

Wording is enforced by tests, not by review habit:

- `NoStateRecommendsInstallingAnUnlistedPackage` walks all four states and fails
  on "download", "latest version", "newest", "upgrade to", "usbip-win2
  releases", "github.com". The only install path the card points at is the
  existing bundled setup, which targets a listed release.
- `ValidatedExperimental` badges as **"Experimental - known package"** and is
  asserted never to contain "approved"; only `Approved` may say "Production
  approved". The tier note states plainly that a match is not production
  approval and names the kernel request-lifetime risk.
- `Missing` and `DetectedUnvalidated` both say what will be restricted. The
  restriction does not exist yet — that is 2.3 — so the text is future tense and
  accurate today.

Colour carries meaning and is stated once in `BridgeShellStyles`: green only for
production-approved, **amber** for a recognised experimental package, red for
unverified, grey for absent and for not-yet-checked. Amber is deliberately not
green. That needed a new `WarningColor` brush, added to **both** theme
dictionaries; `ThemeResourceTests` gained a `[DataTestMethod]` that asserts
light and dark define the same 14 brush keys, so the next new brush cannot land
in one dictionary only.

`RunDiagnostic()` is a new public entry point on
`ViiperDriverValidationCommand`; `Run()` (the CLI) now calls it. There is one
implementation, so the card's report and the command's report cannot diverge.
`RedactUserPath` and the `%TEMP%`-relative display path are untouched, and the
card shows the display path, never the real one.

### Reuse, not forks

- `ViiperDriverReportFormatter` — not touched. The card reuses the report
  through `RunDiagnostic()`.
- `ViiperDriverValidator.RejectTrust` was extracted to a public
  `DescribeTrustRejection` and now backs both the fail-closed decision and every
  trust string the card shows, so the decision and its explanation cannot drift.
- `ResolveUsbipExecutablePath()` became `internal` so readiness resolves the
  same path the diagnostic does instead of reimplementing the lookup.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**,
  14 pre-existing warnings.
- Full suite with the CI filter — **663 passed / 0 failed**, from a 626
  baseline: +24 readiness/mapping/provider tests, +11 view-model tests, +2 theme
  parity rows. `AppSettingsTests.CheckSettingsSave` remains excluded and remains
  stale for the reason recorded in the 2.4b entry.
- **Manual check on the packaged build, non-elevated** (`dotnet publish` +
  `utils/post-build.py`, driven through UI Automation). Settings → VIIPER →
  the card rendered:
  - badge **"Experimental - known package"** in amber;
  - "The installed packages exactly match a package identity Thrum knows:
    usbip-win2 0.9.7.8, an experimental baseline.";
  - the not-production-approval note in full;
  - UDE host controller — `USBIP-WIN2` / `usbip2_ude.inf` / `1.45.29.368` /
    `usbip2_ude` / `usbip2_ude.cat: trusted, signed by Microsoft Windows
    Hardware Compatibility Publisher`;
  - filter extension — `usbip2_filter.inf` / `1.45.28.868` / service
    `(not reported)` / same catalog trust;
  - usbip.exe client — `0.9.7.8`, `trusted, signed by Cloudyne Systems
    (Scheibling Consulting AB)`;
  - no reason list, which is correct for a match.

  Re-check updated the timestamp; Run full diagnostic wrote
  `%TEMP%\Thrum\viiper-driver-validation-20260726-174707Z.txt` (5,778 bytes),
  opened the report window with content identical to the CLI run, and the card
  gained "Report saved to %TEMP%\...". Copy report put all 5,778 characters on
  the clipboard; Open report opened the saved file. App shut down via
  `-command shutdown`; no Thrum process and no orphaned `viiper.exe` left
  behind (2.4b stopped the backend it had started).

### Deviations

1. **This machine has usbip-win2 0.9.7.8 installed, not 0.9.7.7.** The task
   brief predicted 0.9.7.7. The observed DriverVers are `1.45.29.368` /
   `1.45.28.868` and the client reports `0.9.7.8`, which is the manifest's
   known-risk baseline, not the installer-targeted one. Both are
   `ExperimentalBaseline`, so the state is the predicted
   `ValidatedExperimental` — but the identities on screen are the other
   release's. Worth knowing before any [VM] matrix work assumes the dev PC
   mirrors the 0.9.7.7 checkpoint. **Nothing was installed, changed, or
   upgraded to establish this.**
2. **The Settings VIIPER refresh moved off the dispatcher thread.** Not in the
   task's scope, but forced by it: `GetStatus` now also does a SetupAPI
   enumeration and catalog verification, and that method was already being
   called synchronously from the `MainWindow` constructor alongside a
   1000 ms-timeout TCP ping and a Task Scheduler query. The probes now run on a
   background task and apply to the UI through the dispatcher. Strictly less
   blocking than before.
3. **Reasons are not suppressed on a match**, where a stricter reading of "the
   reason list when not ValidatedExperimental/Approved" would suppress them. A
   passing result carrying a trust concern should be visible, not hidden; the
   card binds the list to whether it is empty. In practice it is empty on every
   match.
4. **`ViiperDriverValidationCommand.ShowReportWindow` gained an owner
   parameter** so the card's report window centres on the main window. The
   command path passes null and is unchanged.
5. **Not verified, and cannot be here:** every state other than
   `ValidatedExperimental`. `Missing`, `DetectedUnvalidated` and `Approved` are
   covered by unit tests against fakes; seeing them rendered against a real
   machine needs the TESTENV no-driver and tampered-INF checkpoints, which is
   the [VM] pass Phase 2's verification section already schedules.
6. **Spotted in passing, not fixed:** the Settings "Run At Startup" helper text
   still reads "Tells Windows to start DS4Windows after login" — a Phase 1.8
   localization-sweep miss in the string resources.

---

## 2026-07-26 — Phase 2.3 + 2.5: experimental gating, risk disclosure, runtime guardrails

**Session scope:** tasks **2.3** (experimental gating + risk disclosure) and
**2.5** (runtime guardrails), plus the Phase 1.8 sweep miss found during the 2.2
manual pass. Explicitly not 2.4 (installer): nothing here installs, elevates,
attaches or modifies a driver.

2.1/2.2 made the four-state readiness visible. This is the change that makes it
*do* something — and the first change in this repo that can refuse to create a
virtual device.

### The feature-class split

Everything turns on one fact from Part 2's risk model: the confirmed usbip-win2
request-lifetime defect is reachable **only** through virtual USB audio endpoint
teardown. So features split in two, and the split is a property of the VIIPER
device type, not of the user-facing feature name:

| Class | What is in it |
|---|---|
| **ControllerOnly** | `xbox360`, `ns2pro`, and the HID-only Sony personas — `dualsensecombinedext`, `dualsenseext`, `dualsense`, the Edge equivalents, `dualshock4`. No USB audio interface is created, so the race is not reachable. |
| **Audio** | Anything creating or opening a virtual audio/mic endpoint: every `…audioduplex*` and `…audioonly*` type, the `…micv2` types, and the audio-only sidecar. |

### The gating decision

`ViiperVirtualDeviceGate.Decide(state, class, acknowledged, audioEnabled,
alreadyAttached)` is pure, total, and reads nothing else. All **64**
combinations are enumerated in `ViiperVirtualDeviceGateTests`:

| # | readiness | class | attached | ack | audio opt-in | result |
|---|---|---|---|---|---|---|
| 1 | *any* | *any* | **yes** | *any* | *any* | **allow** — running session |
| 2 | `Missing` | any | no | any | any | block `DriverMissing` |
| 3 | `DetectedUnvalidated` | any | no | any | any | block `DriverUnvalidated` |
| 4 | `ValidatedExperimental` | ControllerOnly | no | no | any | block `ExperimentalNotAcknowledged` |
| 5 | `ValidatedExperimental` | ControllerOnly | no | yes | any | **allow** |
| 6 | `ValidatedExperimental` | Audio | no | no | any | block `ExperimentalNotAcknowledged` |
| 7 | `ValidatedExperimental` | Audio | no | yes | **no** | block `AudioClassNotEnabled` |
| 8 | `ValidatedExperimental` | Audio | no | yes | yes | **allow** |
| 9 | `Approved` | any | no | any | any | **allow** |
| 10 | *unknown enum value* | any | no | any | any | block `DriverUnvalidated` |

Four properties this ordering is chosen for:

- **Row 1 comes first, unconditionally.** Tearing down a live audio endpoint is
  the exact operation the kernel defect is reached through, so a gate that
  yanked one would cause the crash it exists to prevent. Mid-game teardown is
  also simply hostile. Gating is about *new allocations*.
- **Rows 2 and 3 cannot be bought with consent.** Consent accepts a known risk;
  it does not override an unproven one. `ConsentCannotUnlockAnUnprovenDriver`
  pins that.
- **The acknowledgement is checked before the audio opt-in** (row 6 before row
  7). A user who has consented to nothing hears about the driver first, not
  about audio.
- **Row 9 is the only row where audio needs no opt-in**, and `Approved` is
  unreachable by design. That is what stops the tier being decorative.

### Where the gate is wired

Two choke points, both minimal-diff:

1. **`ViiperOutDevice.Connect()`** — the one place a virtual USB device is
   brought into existence, for primary outputs *and* the sidecar. It already
   threw `IOException` on a prerequisite failure and every caller already
   handles that, so a refusal needs no new error path. It asks twice: refusal of
   the controller class throws; refusal of the audio class is not fatal for a
   primary output, it just starts the persona ladder below the audio rungs.
   Recovery (`TryRecoverStream`) reopens the *existing* device and never passes
   here, so an in-flight session is untouched by construction.
2. **`PlayStationFeatureOutputPolicy.GetAudioOnlySidecarType`** — gained a
   required `audioClassAllowed` parameter (no default, so every call site must
   say what it consulted).

### What changed about the automatic sidecar

Before: with a Sony pad on Bluetooth and an Xbox or Switch profile output, the
policy created an audio-only virtual DualSense **by itself**. Observed live
during the Phase 1 smoke pass as a second usbip port. Ordinary use on this
machine therefore exercised the audio-endpoint teardown path, unprompted.

After: `ControlService.EnsurePlayStationFeatureOutput` asks the gate before the
policy, and the policy refuses without consent. Two details matter:

- The gate is asked with `alreadyAttached` = "a sidecar for this controller is
  connected right now". `CheckProfileOptions` runs on every profile change, so
  without that, switching the setting off mid-session would *disconnect* a live
  audio endpoint on the next profile change — the race trigger. The switch
  applies to the next connection, never the current one.
- Refusals are logged once per controller per reason, not on every profile
  change, but at least once: a user whose controller speaker silently stopped
  appearing has to be able to find out why.

The persona ladder gained three named HID-only tails
(`CreateDualSenseHidOnlyStream`, `…Edge…`, `…DualShock4…`). They were already
the final fallbacks; naming them lets the gated path enter the ladder below the
audio rungs without duplicating the tail.

### The two persisted flags

`ViiperExperimentalAcknowledged` and `AllowExperimentalAudioEndpoints`, both
**default off**, both in the string-proxy DTO form. That form matters more here
than for the 2.4b setting: a config written before these elements existed must
read as *no consent given*, and a malformed value must read as off rather than
throw out of `Deserialize` and take the whole settings file with it. Both are
tested.

The Settings checkboxes bind **`OneWay`** and are driven by `Checked`/
`Unchecked` handlers, not `Click`. A consent gate must be impossible to flip
without the disclosure, and `Click` only covers the input paths WPF routes
through `OnClick` — during the manual pass a UI-automation toggle changed the
box without raising it. The state-change events fire whatever moved the box.
The cost is three echoes to filter (the binding applying a stored `true` at
startup, the handler writing what it just decided, the corrective un-tick after
a decline), all handled by one comparison: act only when the requested value
differs from the stored one. Verified live that a stored `true` produces no
dialog at startup.

### The disclosure wording

Shipped verbatim, and asserted by `ViiperExperimentalDisclosureTests` rather
than by review habit. One-time acknowledgement, shown from the Settings switch
and from the profile editor's output-type change:

> Virtual controllers in Thrum are presented to Windows through usbip-win2, a
> third-party kernel-mode USB/IP driver that is not developed by this project
> and is not approved for production use by anyone.
>
> A kernel driver runs inside Windows itself. If it faults, Windows stops with a
> blue screen; Thrum cannot catch that or recover from it.
>
> Plain controller output - buttons, sticks, triggers, rumble, lightbar - does
> not use the driver path that carries the known defect, and Thrum has run those
> lifecycles cleanly in testing. Virtual audio and microphone endpoints do use
> it, and stay switched off until you enable them separately.
>
> Continue and use virtual controllers?

Per-enablement audio-class confirmation (shown **every** time the switch is
turned on, because the risk does not fade with familiarity and the installed
package can change between sessions):

> You are about to let Thrum create virtual USB audio and microphone endpoints
> (controller speaker, headset jack and pad microphone) through the usbip-win2
> kernel driver.
>
> The risk, plainly: usbip-win2 has a confirmed defect in how it retires
> in-flight USB requests. When a virtual audio endpoint is torn down - closing a
> game, switching profiles, unplugging the pad, shutting down - an audio
> transfer that completes at the same moment can corrupt kernel memory and stop
> Windows with a blue screen. It has been reproduced on this project's own
> hardware.
>
> This is a defect in usbip-win2, not in Thrum. It is reported upstream as
> usbip-win2 issue #181 (https://github.com/vadimgrn/usbip-win2/issues/181).
> Thrum orders its own teardown as carefully as it can, but the fault is inside
> the kernel driver and cannot be fully prevented from outside it. No usbip-win2
> release is known to have fixed it.
>
> Installed package: usbip-win2 0.9.7.8, an experimental baseline Thrum
> recognises.
> Recognising a package is not approving it. Thrum has no usbip-win2 release on
> its approved list, and does not suggest installing any release other than the
> one its own setup installs.
>
> You do not need this for controller support. Buttons, sticks, triggers,
> rumble, gyro, touchpad and lightbar all work with these endpoints switched
> off, and that configuration does not reach the defect.
>
> Turn virtual audio endpoints on?

The "Installed package" line is composed from the session readiness, so it names
whatever is present; the four states each have their own true sentence and
`EveryStateDescribesWhatIsInstalled` covers all of them. Issue #181 is
*referenced*, never summarised as a fact about the reader's release — that is
what keeps the page true on a machine with a package nobody has examined.

Wording is enforced negatively too: `NoDisclosureRecommendsAnUnlistedPackage`
and `NoRefusalRecommendsAnUnlistedPackage` fail on "download", "latest version",
"newest", "upgrade to", "update usbip", "install a newer", "/releases".

### Surfacing the blocked state

- **Output Slots banner** (`ViiperOutputGateBannerViewModel`), refreshed when the
  tab is selected and when a consent switch moves, evaluated on a worker thread
  so the first readiness pass never blocks the dispatcher. Red **"New virtual
  controllers are blocked"** when nothing can be created; amber **"Virtual audio
  endpoints are off"** when only the audio class is refused — and the amber row
  says running controllers are unaffected, because a user looking at a working
  pad while reading "blocked" concludes the message is wrong and stops reading
  the next one.
- **Log**: the refusal reason verbatim, from the same `Decide` call, plus one
  line per consent decision (what was shown, what was answered).
- The 2.2 card's `DetectedUnvalidated` text moved from "will be restricted" to
  the present tense, now that it is true, and gained the "already plugged in
  keep running" promise.

### Phase 1.8 sweep miss, fixed and guarded

The 2.2 pass found "Tells Windows to start DS4Windows after login" still in the
Settings tooltip. Root cause: 1.8's reference scan decided a
`Properties/Resources` key was dead if no C# file said `Resources.<Key>`. It did
not know the **`{lex:Loc Resources:<Key>}`** XAML form. Re-running the sweep
with that form added found **three** live keys still naming the old product, not
one:

| File | Keys | Tokens |
|---|---|---|
| `Properties/Resources.resx` | `RunAtStartup`, `UACTask` | 2 |
| `Properties/Resources.ru.resx` | `RunAtStartup`, `UACTask`, `CloseMinimize` | 3 |
| `Properties/Resources.zh-hans.resx` | `RunAtStartup`, `UACTask` | 2 |

Flipped with the same value-only, URL-guarded, re-parse-and-assert script 1.8
used, so the diff is 7 lines across 3 files with every BOM and CRLF intact. The
two checked-in designer doc comments were synced. `QuitOtherPrograms` stays as
it is — its only token is inside the upstream wiki URL, which the guard confirms
on every run.

The guard is now `NoXamlReachableTooltipStillNamesTheOldProduct`: the 17
XAML-reachable `Resources:` keys, each checked with URLs stripped, against
`ProductInfo.ProductName`. It also records `BtPollRate` — bound twice in
`ProfileEditor.xaml` and declared in **no** resource file, so that tooltip has
always rendered empty. Inherited from upstream; recorded rather than dropped
from the list, because dropping it would hide it.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**, 14
  pre-existing warnings.
- Full suite with the CI filter — **696 passed / 0 failed**, from the 663
  baseline: +33. `AppSettingsTests.CheckSettingsSave` remains excluded and
  remains stale for the reason recorded in the 2.4b entry.
- **Manual pass on the packaged build, non-elevated**, with the maintainer's
  real configuration (backed up first, restored byte-identical afterwards —
  hashes re-verified). `usbip.exe port`:

  | State | `usbip port` |
  |---|---|
  | before launch | *(no imported devices)* |
  | app running, **no consent recorded** | *(no imported devices)* |
  | acknowledgement accepted, **audio off**, one X360 output plugged | `Port 01 … Xbox360 Controller (045e:028e)` — **one port, no second Sony port** |
  | plus a DualSense output plugged, audio still off | adds `Port 02 … DualSense wireless controller (PS5) (054c:0ce6)` — the pad itself, still **no audio sidecar port** |
  | both unplugged | *(no imported devices)* |

  With no consent recorded the backend's own census answered `{"buses":[]}`:
  nothing was created at all, which is the guardrail working. The Output Slots
  banner read **"New virtual controllers are blocked"** with the acknowledgement
  reason; after accepting it, **"Virtual audio endpoints are off"** with the
  audio reason.

  The DualSense plugged with audio consent off negotiated a **HID-only persona**
  — the census reports `type: "dualsense"` with `speakerInterfaceActive: false`
  and `microphoneInterfaceActive: false` — and the log carried
  *"Virtual DualSense output is starting without its audio and microphone
  interfaces."* followed by the full reason. Both disclosures were read verbatim
  from the live dialogs (above). Decline reverted the switch and recorded
  nothing; accept persisted immediately; a restart read both flags back
  correctly (`ack` on, `audio` off) with no dialog at startup.

  No Thrum process and no stray `viiper.exe` left behind; `usbip port` empty at
  the end.

### Deviations and gaps

1. **The physical DualSense could not be brought into the test.** It is paired
   and connected over Bluetooth, but its HID interface delivered nothing and
   Thrum reported "No Controllers Connected" for the whole session; waking a
   sleeping pad needs a physical button press. So the *bound-pad* sidecar
   scenario — a real Sony pad on BT with an X360 profile output — was not
   exercised live, and neither was controller input (manual check (c)). What was
   exercised instead: the same gate, the same policy call, the same audio-class
   refusal, on virtual outputs plugged by hand, plus the full unit coverage of
   `GetAudioOnlySidecarType` under both consent values. **Queued as an [HW]
   item.**
2. **The Default profile's output was temporarily switched to `ViiperX360`** to
   set up the documented sidecar scenario, then restored. The maintainer's
   profile is `ViiperDualSense`, not X360 as the task brief assumed.
3. **Consent was recorded and then un-recorded.** The manual pass accepted both
   disclosures to test them; the config was restored from the pre-test backup
   afterwards, so neither flag is set on the maintainer's machine and the
   elements are absent from `Profiles.xml` again. Consent is theirs to give.
4. **`Checked`/`Unchecked` instead of `Click`** on the two consent checkboxes —
   see above. Not what the plan implied, and the better design for this purpose.
5. **`ViiperDriverStatusViewModel.RestrictionText` was reworded** for
   `DetectedUnvalidated`. Outside the letter of 2.3/2.5, but the 2.2 text said
   "will be restricted" precisely because the restriction did not exist yet.
6. **Not verified live, and cannot be here:** the `Missing`, `Approved` and real
   `DetectedUnvalidated` rows of the table. Unit-tested against fakes; seeing
   them refuse against a real machine needs the TESTENV checkpoints, which is
   the [VM] pass Phase 2 already schedules.

### [HW] queue

- Wake the DualSense (PS button) and re-run manual check (a)/(c) with the
  Default profile on an X360 output: confirm exactly one usbip port for the pad
  and **no** `054c:0ce6` sidecar with audio consent absent, then confirm normal
  controller input. Roughly five minutes with the maintainer present.

## 2026-07-26 — Fix: two ProfileEditor tooltips bound to a misspelled resource key

Reported as "`BtPollRate` is declared in no resource file", with the fix being to
author the key. Investigation contradicted the premise, so the fix is different
and smaller.

`BTPollRate` **already exists** — neutral, `.ru` and `.zh-hans`, plus a checked-in
designer property — carrying "Determines the poll rate used for the DS4 hardware
when connected via Bluetooth. (Applies on profile save)". The two
`ProfileEditor.xaml` tooltips bound to `BtPollRate` (lower-case `t`), and
`ResourceManager.GetString` is case-sensitive, so both resolved to null and
rendered empty.

Authoring a second key would have shipped a near-duplicate English-only string
beside an already-translated one, and left two keys one character apart for the
next reader to trip over. Fixed the two bindings instead: one character each,
and the Russian and Chinese translations come back with them.

- `DS4Windows/DS4Forms/ProfileEditor.xaml` — both `Resources:BtPollRate`
  bindings now `Resources:BTPollRate`.
- `DS4WindowsTests/LocalizationSweepTests.cs` — `XamlReachableResourcesKeys`
  retargeted to the real key; the `KnownMissingResourcesKeys` entry dropped, so
  the dictionary is now empty. Its doc comment records why, and warns that a
  key must be confirmed absent **case-sensitively** before being recorded as
  missing — a case-insensitive comparison reports the wrong spelling as present.

The neutral English text was deliberately left as-is. Enriching it (poll interval
versus bandwidth and battery, default of 4 ms / 250 Hz) was considered and not
done unilaterally: it would leave the `ru` and `zh-hans` values describing less
than the English one, and translation drift is a decision for the maintainer, not
a side effect of a typo fix.

No new key, so the neutral-only policy for new keys does not apply here.

Suite: **696 passed / 0 failed** (CI filter), unchanged from baseline — the pass
is itself the check, since the key is listed as XAML-reachable with no
known-missing entry, so a null lookup would fail the test.

---

## 2026-07-26 — Phase 2.4: installer hardening (pins, verification, autostart removal, issue #12)

**Session scope:** plan task **2.4**, written on top of the upstream merge
analysed in `upstream-delta-2026-07-26.md`. Nothing in this session installed,
upgraded, removed or touched a driver, service, scheduled task, registry Run
value or the VIIPER install directory. The setup script was never executed; it
was parsed (AST only), and the decision layer it consults was exercised
directly.

### The shape of the change, and why

Upstream's four commits left four of the seven 2.4 requirements entirely ours,
and the honest reading of the remaining three is that the script was making
security decisions it had no way to make well: a `-ge 0.9.7.7` floor against a
version probe that reads the FileVersion of `usbip2_ude.sys` — a DriverVer such
as `1.45.29.368`, which is not the release label and compares greater than every
floor anyone would write. The floor passed trivially on every install it has
ever seen, including this machine's.

So the decisions moved into C# and the script kept the mechanical half. The
deciding argument is not "C# is nicer to test": it is that the admission rule is
*the manifest decides*, the manifest is `ViiperDriverManifest`, and its own
contract says it must not be duplicated into the UI, the broker or the
installer. A PowerShell copy of the version table would have been exactly that
duplicate — and it would have been the copy deciding whether a kernel driver
gets installed.

New surface:

| File | What it owns |
|---|---|
| `Viiper/Validation/ViiperInstallerPins.cs` | The two exact artefacts setup may fetch: URL, SHA-256, size, whether Authenticode is required and from whom, and **how the digest was obtained**. |
| `Viiper/Validation/ViiperInstallerPolicy.cs` | Pure, total decisions: download verdict, usbip install action, post-install verdict, script exit code, the app-side reading of that exit code, autostart plan. |
| `Viiper/Validation/ViiperInstallerPolicyCommand.cs` | The read-only `-viiperinstallerpolicy` verb surface the script consults. |
| `DS4Control/PendingApplicationRestart.cs` | Issue #12's ordering, enforced rather than commented. |

### Testing approach, and why it is not Pester

The brief offered (a) dot-sourceable script functions plus a Pester suite in CI,
or (b) decisions in testable C# with the script as thin orchestration, and asked
for whichever puts the fail-closed logic under real tests.

(b), for three reasons. The manifest argument above is the first and decisive
one. Second, MSTest already gates every merge here; a Pester job would be a
second harness, a second runner dependency and a second place a filter can go
stale, bought for logic that would still have to reach into C# for the version
table. Third — and this is the part worth stating plainly — the *shape* of (b)
is what makes the properties testable at all: `DecideDownloadVerification` is a
function from observed facts to a verdict, so "valid signature, unexpected
subject" is three lines of test instead of a signing fixture.

67 new tests, all pure:

- `ViiperInstallerPolicyTests` (46) — correct digest; wrong digest; missing
  file; null observation; uncomputable digest; valid signature with an
  unexpected subject; untrusted signature; absent signature; **a signature that
  was never evaluated** (the failure shape that reads exactly like a pass);
  unsigned component approved on its digest alone and refused on a wrong one;
  version not in the manifest (three spellings); version newer than pinned;
  already-installed pinned version; already-installed recognised-but-different
  version; registered-but-not-bound in all four flavours; unknown enum value;
  post-install 0/1/2, an undocumented code, and never-started; every exit-code
  mapping in both directions; the autostart plan including an unreadable state.
- `PendingApplicationRestartTests` (10) — the #12 ordering, below.
- `ViiperInstallerScriptTests` (11) — **the weakest tests here, and labelled as
  such in the file.** Matching text in a script proves the text is there. They
  exist for the four properties that are properties of *absent* code — no
  autostart creation, no backend start without the update flag, no URL or digest
  outside the pins, no deletion of the rollback backup — where a regression is
  silent: the script keeps working, it just stops being safe.

### Requirement by requirement

1. **Pinned + digest + Authenticode before execution.** `Get-VerifiedPinnedFile`
   is the only way an artefact reaches disk, and its next statement is the
   verification call. Refusal deletes the file and throws. Verified live against
   the genuine signed installer: digest and subject both matched and were logged
   expected-beside-actual; a one-byte-flipped copy was refused with both digests
   in the log.
2. **Post-install validation of the pair.** A `validate-installed` verb runs
   `ViiperDriverValidationCommand.RunDiagnostic()` — the same implementation and
   the same 0/1/2 the `-viiperdriverdiagnostic` switch runs — and the script
   branches on it. Deliberately not launched as `-viiperdriverdiagnostic` in a
   second process: that switch prints to an attached parent console and, when
   there is none, opens a **modal report window**. A setup step that can block
   on a dialog nobody can see is not a verification step. Exit 2 and "could not
   run at all" are both failures.
3. **No silent acceptance of an unlisted release.** The floor is gone; the
   primary input is the gate's four-state answer, not a file version. Verified
   live on this machine: `readiness=ValidatedExperimental`,
   `matchedrelease=0.9.7.8`, `action=LeaveRecognisedReleaseAlone` —
   *"It is a release this build recognises as an experimental baseline, and it
   is left exactly as it is."* Nothing was installed over it and no downgrade
   was attempted. VIIPER is pinned to an exact asset by version **and** digest;
   `Get-GithubReleaseAsset` and the newest-non-draft walk are deleted.
4. **Atomic install, rollback retained.** `.previous` is no longer deleted on
   success, and its path is logged. Rollback that exists only inside the install
   window is not rollback — the failure it guards against is a backend that
   installs cleanly and then misbehaves.
5. **Log every decision.** Every decision function returns its audit lines
   together with its verdict, from the same call, so the two cannot disagree.
   The script copies every `log=` line into `install.log` verbatim.
6. **Both autostart mechanisms removed.** `viiper.exe install` and
   `Register-ViiperRunTask` are gone, and with them the `$registrationSafeToRun`
   dance they were load-bearing for. `Stop-ViiperProcesses` survives, now needed
   only by the atomic install, keeping upstream's retry/escalate/fail-closed
   behaviour verbatim. A pre-existing entry is detected through 2.4b's read-only
   detector, reported, and removed only with `-RemoveViiperAutostart` or the
   Settings button — never adopted.
7. **Issue #8 closed on every path.** `Start-AndVerifyViiper` takes its argument
   vector from `ViiperBackendSpawn.ServerArguments` (via the `pins` verb) rather
   than spelling out `server`, so the script cannot drift from the application,
   and it sets `VIIPER_UPDATE_NOTIFY` as well. With both autostart entries gone,
   no path remains that starts an update-nagging backend.
8. **Issue #12 fixed.** Below.

### Issue #12: the ordering, and what happens to the backend

`RestartApplication` no longer starts anything. It records intent;
`CleanShutdown` starts the replacement **after** `threadComEvent.Close()`, and
`PendingApplicationRestart.Launch` refuses outright until
`MarkSingleInstanceReleased()` has been called. The ordering is a precondition,
not a comment: an edit that moves the launch earlier fails a test that says why.

**The backend across the restart is deliberately not special-cased.**
Stop-on-exit runs as usual, the owned backend goes down with the app, and the
new instance starts a fresh one on demand. The alternative — exempting an
install-driven restart — would leave a backend running that the new instance
does not own and would therefore never stop, turning a temporary special case
into a permanent orphan. A few hundred milliseconds of downtime during a restart
nobody is playing through is the cheaper side of that trade.

### Verification

- `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0 errors**, 14
  pre-existing warnings.
- Full suite with the CI filter — **763 passed / 0 failed**, from the 696
  baseline (+67). `AppSettingsTests.CheckSettingsSave` remains excluded and
  remains stale for the reason recorded in the 2.4b entry.
- Script **parsed** with `[Parser]::ParseFile` — 0 errors, 2,609 tokens, five
  declared parameters. Parsing is not execution; the script was never run.
- **Live read-only pass** of every policy verb against the packaged build, using
  the genuine artefacts already retained in the workspace. Results as quoted
  above, plus: the pinned VIIPER asset approved on its digest with the
  "unsigned upstream" line; a missing file reported as `Unavailable` rather than
  as a mismatch; `validate-installed` returning `Validated`; `autostart`
  returning `NothingToDo` with count 0, matching this machine's known state.

### Deviations

1. **Post-install validation goes through `validate-installed`, not a second
   `-viiperdriverdiagnostic` process.** Same implementation, same exit codes, no
   modal-dialog hazard. Recorded because the brief named the switch.
2. **VIIPER is pinned to the public v0.0.5 asset, not to a bundled copy.** The
   plan allowed bundling ours until hbashton/VIIPER#3 lands. Unnecessary: the
   public asset has a stable digest that two independent sources agree on, and
   pinning by digest already immunises us against the mis-stamp. The mis-stamped
   embedded version is recorded in the pin so nothing ever validates by it.
   Bundling stays available for Phase 5.2 through `-ViiperBackendFile`.
3. **`-UsbipInstallerFile` / `-ViiperBackendFile` added.** Not in the brief.
   They let the VM run sheet's negative cases travel the real code path rather
   than a parallel one, and they are not a bypass: a staged file replaces the
   download and nothing else, verified by the same call against the same pin.
4. **A third exit code (3) exists.** "Installed, but the pair cannot be
   validated until Windows restarts" is neither success nor failure, and
   collapsing it into either would have meant lying in one direction.
5. **Not verified, and cannot be here:** the script end to end. Running it
   installs a kernel driver, which Part 3 rule 1 puts behind a TESTENV
   checkpoint. The decision layer it consults is verified live; the
   orchestration around it is source-level only.

### For the VM run sheet

`PHASE2-VM-VALIDATION-PREP-20260726.md` Phase B needs no change to be runnable,
and gains a cheaper route: B1 and B2 can be done **without installing anything**
by calling `Thrum.exe -viiperinstallerpolicy verify-file --component usbip
--path <staged> --out <report>` directly — that is the exact call the script
gates on. To exercise them through the script instead, pass
`-UsbipInstallerFile <staged>`. B4's "no autostart was created" assertion is now
also covered by a unit test, but the live enumeration is still worth capturing.

## 2026-07-26 — Fix #17: advanced-haptics lane reported a policy choice as a fault

Seen live minutes after the [HW] verification: with the DualSense persona and
audio-class consent absent — the new default — the Overview card sat on
**"Needs attention — The enabled advanced haptics lane could not be armed."**
Nothing was broken. The lane was switched off on purpose.

`advancedHapticsRequired` in `GetControllerRuntimeSignals` derived the
requirement from the output persona alone. It predates the 2.3 consent gate, so
it did not know that without audio-class consent the persona ladder selects a
HID-only variant and the V4 atomic audio+haptics carrier is deliberately absent.
`EvaluateLane` then mapped `Unavailable` to a fault in `Attention`.

Fix: the requirement now asks the same gate the persona ladder asked, via
`ViiperVirtualDeviceGuard.Decide(ViiperFeatureClass.Audio, alreadyAttached:
laneLive)`, and the decision moved into a pure
`ControllerRuntimeStatusPolicy.EvaluateAdvancedHapticsLane` so it is testable
without a `ControlService`. A live carrier outranks the flag: consent turned off
mid-session leaves the lane up, required, and healthy, so a later real failure
still surfaces.

**The microphone lane was checked and deliberately left alone.** It has a
similar shape — requirement from a user toggle, no reference to consent — but it
is not the same defect: `dualSenseMicrophonePassthrough` routes the *physical*
pad's microphone over Bluetooth and does not depend on virtual audio endpoints,
so a failure there is genuine and must keep surfacing. Silencing it would have
hidden real breakage to quiet a false alarm.

Six tests, including the inverse case (consent given, carrier still absent ⇒
still `Attention`). Negative control run: dropping the `audioClassPermitted`
term reproduced the bug — `Expected:<NotRequired>. Actual:<Unavailable>` — and
the guard test failed; restored and re-verified.

Suite: **769 passed / 0 failed** (CI filter), from 763.

---

## 2026-07-30 — Phase 2 VM validation pass (and the two installer blockers it found)

**Session scope:** the deferred Phase 2 VM pass, run against `7b87d1e` on the Windows 11 25H2
test VM under the hardened posture (Secure Boot on, test signing off, VBS and Memory Integrity
running). Branch `fix/installer-clean-machine-blockers`.

**Outcome: Phases A, B and D pass; Phase C is half-blocked. Two blocking defects were found in
`extras/install-viiper-backend.ps1`, both fixed here.** Zero bugchecks, zero dumps, zero PnP
faults across the whole run. No virtual audio endpoint was created at any point, so nothing in
this pass went near the usbip-win2 #181 teardown path.

### The two blockers

Task 2.4 recorded that the setup script end to end was "not verified, and cannot be here"
because running it installs a kernel driver. This is what was behind that gap. Both failed
**closed** — nothing was installed either time — so they were broken features, not unsafe ones.

1. **Setup could not start on a clean machine.** `Get-UsbipRegisteredRelease` returns `""`
   when no USB/IP uninstall entry exists — the ordinary first-time case — and that empty
   string was passed as the value of `--uninstall-version`. `Start-Process` validates
   `-ArgumentList` as not-null-or-empty *per element*, so setup aborted with
   `Cannot validate argument on parameter 'ArgumentList'` before verifying anything.
2. **The driver installer was launched interactively.** `/S` is NSIS's silent switch;
   usbip-win2 ships an Inno Setup installer, which ignores unrecognised switches. The wizard
   opened and waited for a human — observed as process `USBip-0.9.7.7-x64.tmp`, window title
   `Setup - USBip`, blocking eleven minutes until killed.

Fixed with two guards for the first (empty arguments dropped in `Invoke-InstallerPolicy`, and
the option omitted at the call site) and Inno's own switches for the second, hoisted into
`$script:UsbipSilentArguments`. Suite **771 passed / 0 failed**, from 769.

**Neither was detectable by the tests that exist.** Every guard on this script is static text
analysis — the file's own header calls them the weakest tests in the change set. One defect is
a runtime parameter-validation rule, the other a third-party installer's command-line dialect.
The two new tests are also static: they pin the fixes but would not have found the bugs. A
scripted end-to-end install in the VM belongs in the release gate.

### What passed

- **Phase A (`Missing`).** Card reads *Not installed* with no package identity and no version
  recommendation; both consent checkboxes off by default, the audio one naming issue #181 and
  the blue-screen consequence. Output Slots carries *"New virtual controllers are blocked"*.
  `-viiperdriverdiagnostic` exits 1 with no side effects, and a restricted-token run recorded
  `elevated : no`, proving it never asks for elevation.
- **Phase B (installer).** Corrupt and bad-signature artefacts refused fail-closed with
  expected-vs-actual logged; genuine file approved (positive control). After the fixes: install
  completes exit 0, pair validation confirms **UDE 21.14.27.907 / filter 21.14.27.661** with
  both catalogs trusted, no autostart of either kind, zero VIIPER processes at logon after
  reboot, backend starts with `--update-notify none`, and a repair skips the driver step
  (`AlreadyPinned`) while retaining `viiper.exe.previous`.
- **Phase C (2.4b only).** The deferred ownership branch passes live: a backend Thrum did not
  start survives Thrum's exit, same PID and start time, logged as *"the backend was already
  running before Thrum started, so it is not ours to stop."*
- **Phase D (`DetectedUnvalidated`).** Under a deliberately weakened posture on its own branch,
  a test-signed `develop@63e5c8f0` build was installed and **loaded**. The gate refused it:
  `readiness=DetectedUnvalidated`, `action=RefuseUnrecognisedInstall`, `verdict=Refused`,
  diagnostic exit 1, on both `DriverVer` and publisher. The decisive line is
  `signature trusted: yes` next to `publisher accepted: no` — Windows considered the driver
  validly signed because the test certificate was trusted, and the gate refused anyway. It does
  not delegate its decision to whatever the machine has been told to trust. The mixed-pair case
  came free: the two packages carry different `DriverVer`s and both were reported independently.

### What is still open

- **C1–C4 live gating** needs a physical controller in the guest; the VM has none and the run
  sheet scopes passthrough out. The VIIPER local API was probed as a workaround and does not
  answer plain HTTP on `:3242`.
- **The 2.4b census branch** ("Thrum-owned backend + a foreign device ⇒ backend survives")
  cannot be isolated without a controller, because Thrum only starts a backend when a profile
  needs one.
- **Installer exit code 3** (installed, validation deferred to a restart) was never reached —
  the pair bound without a reboot.
- **B2's signature branch cannot be exercised by a negative:** both bad artefacts were refused
  on the digest first, and reaching the signature comparison needs a file whose SHA-256 already
  matches the pin. The branch is live and runs on the genuine file, where both lines match.

### Release finding outside Phase 2's scope

The CI and release packages are **framework-dependent**, and the guest could not start
`Thrum.exe` at all: *"You must install .NET Desktop Runtime to run this application."* The pass
proceeded with a self-contained publish, as the 2026-07-25 pass did. A first-time user without
.NET 8 Desktop Runtime meets that dialog, and nothing in the plan currently owns the decision to
bundle the runtime, detect and offer it, or ship self-contained. It belongs to Phase 5.

### Incidental defects logged (not fixed here)

1. `thrum_log.txt` records `INFO|VIIPER virtual-controller backend ready` when no helper and no
   driver are installed, one line before ten warnings that `usbip.exe` does not exist. The UI is
   correct; the log would mislead support triage. *(Fixed below, 2026-07-30.)*
2. That warning is emitted ten times in one second (~100 ms apart) then stops. Bounded, but one
   line or a summary is enough. *(Fixed below, 2026-07-30.)*
3. Accessibility: Output Slots rows and the driver card's identity rows expose raw .NET type
   names (`DS4WinWPF.DS4Forms.ViewModels.SlotDeviceEntry`,
   `DS4Windows.ViiperDriverComponentIdentity`) as their accessible names, on more than one page.
   *(Fixed below, 2026-07-30.)*
4. Verification refusals name the pinned filename rather than the file actually inspected, so a
   corrupted staged copy is reported as *"USBip-0.9.7.7-x64.exe does not have the pinned
   SHA-256"* — which reads as an accusation against the official artefact. *(Fixed below,
   2026-07-30.)*

---

## 2026-07-30 — Fix: verification refusals named the pinned file, not the file inspected

Incidental defect 4 from the VM pass above. `DecideDownloadVerification` built every sentence
about the file from `pin.FileName`, so verifying the corrupted staged copy
`USBip-0.9.7.7-x64.CORRUPT.exe` produced *"Verification failed: USBip-0.9.7.7-x64.exe does not
have the pinned SHA-256"* — an accusation against the official artefact the policy never looked
at. The decision function could not do better: the observation carried size, digest and
signature facts, but not the name of the file they were observed on.

`ViiperDownloadObservation` now records `FileName` — the base name only, never a full path,
because decision lines are copied verbatim into `install.log` and reports must not carry user
paths — and `Observe` fills it from `--path` on every return path, including the file-missing
and unreadable ones. Summaries name that file; the pin's filename stays in the audit trail as
the expected side of a new `File name: expected …, actual …` line, the same
expected-beside-actual shape the size, digest and signer lines already use. `Path.GetFileName`
is re-applied inside the decision function itself, so a future caller that records a full path
still cannot put one into a report. When no name was recorded at all (a null observation), the
text says "the downloaded file" rather than guessing.

The setup script needs no change: it copies `summary` and `log=` lines verbatim and parses
neither.

Negative control: reverting the digest-mismatch summary to `pin.FileName` failed both new
regression tests (`ARefusalNamesTheFileItInspectedNotThePinnedArtefact`,
`AFullPathRecordedInTheObservationNeverReachesTheReport`); restored and re-verified.

Suite: **774 passed / 0 failed** (CI filter), from 771.

---

## 2026-07-30 — Fix: startup log claimed a VIIPER backend the machine did not have

Phase 2's incidental defects 1 and 2, both visible in `phaseA-thrum-log.txt` from the bare VM:
`INFO|VIIPER virtual-controller backend ready` on a machine with no helper, no driver and no
server, followed by the same `usbip.exe was not found` warning ten times in one second.

**The "ready" line** (`ControlService.Start`) was unconditional — it announced its neighbour,
the KB+M handler line, not a probe. It now reads `ViiperSetupManager.GetStatus()` — the same
probe the Settings card reads — and claims ready only when `Ready` (server answering + driver
installed) is true; otherwise it names what is missing:
`VIIPER virtual-controller backend not ready (VIIPER and usbip-win2 need setup). VIIPER helper:
missing; usbip-win2: missing; server: not running.` The component readout moved onto
`ViiperPrerequisiteStatus.ComponentSummary` and the Settings card now composes its text from the
same property, so the log and the card cannot drift apart. The probe result also lands in the
verbose startup diagnostic, bracketed by begin/end lines, since `GetStatus` can spend up to a
second on the API ping.

**The tenfold warning** came from the stale-port sweep: with no active ports it demands ten
consecutive clean snapshots 100 ms apart, and `GetImportedPorts` logged every failed query on
its own. Failed queries are now handed to the caller: the two retry loops
(`DetachStaleLocalViiperPorts`, `FindLocalViiperPort`) count them and log one summary after the
pass — `VIIPER could not query usbip ports (10 attempts): usbip.exe was not found.` — through
`DescribePortQueryFailures`, whose single-failure form is byte-identical to the old line so
existing triage notes still match. The single-shot caller (`DetachDuplicateLocalViiperPorts`)
still warns immediately. Sweep behaviour — attempt caps, sleeps, the clean-snapshot rule — is
deliberately unchanged; this entry is about what the log says, not what the sweep does.

Nine tests pin the ready line, the bare-machine line, the stopped-server line, Ready's
transport-not-helper semantics, the shared component readout, and the 0/1/N/blank
query-failure forms. Suite: **783 passed / 0 failed** (CI filter), from 774 after the
defect-4 fix merged alongside.

---

## 2026-07-30 — Fix: report text quoted from outside could carry the account name

The follow-up the defect-4 fix flagged. `Observe` recorded raw exception messages in
`ViiperDownloadObservation.ObservationError`, and Windows I/O messages embed the full path they
failed on (*"Access to the path 'C:\Users\&lt;name&gt;\...' is denied."*), so a locked or
unreadable download would have written the account name into the decision lines the setup
script copies into `install.log`. The same shape existed in three more places: the WinTrust
verifier's catch-all diagnostic (`"trust verification threw: " + ex.Message`), the policy
command's top-level `error=`/`log=` lines, and the autostart lines that quote an entry's
target — a command line that frequently lives under the profile.

The rule stays the formatter's: `RedactUserPath` replaces the account segment with `<user>`
and keeps the rest of the path, because the path shape is the triage value. The new
`ViiperDriverReportFormatter.RedactUserPathsInText` applies that same rule to every profile
path embedded in prose (in text, the account name is also ended by a quote or whitespace, not
only by the next separator). Producers redact at the source — `Observe` and the command's
`Run` catch now record `ExceptionType: redacted message`, and the WinTrust catch redacts its
diagnostic — and `ViiperInstallerPolicy` redacts again at every line that quotes outside text,
so a report does not depend on every producer remembering. Fixed diagnostics ("certificate
expired", "no valid signature") and path-free error text pass through unchanged;
`DisplayPath` was already built redacted and needed nothing.

Negative control: neutering the policy's `Redacted` helper failed all three new report-flow
tests; restored and re-verified. One test needed its probe account renamed to
`leakedaccountname` — the first choice, "somebody", is a word the autostart prose
legitimately contains, which the failing run pointed out.

Suite: **793 passed / 0 failed** (CI filter) in a clean worktree of this commit, from 783.
(The shared tree also carries unrelated in-flight work, so its totals are larger.)

---

## 2026-07-30 — Fix: list rows announced raw .NET type names to screen readers

Incidental defect 3 from the VM pass above. WPF's `ItemAutomationPeer` derives a list item's
UIA Name from, in order: `AutomationProperties.Name` on the generated container, the
container's plain text, then the data item's `ToString()`. A `ListView` row whose columns come
from cell templates has no container name and no plain text, so every Output Slots row was
announced as `DS4WinWPF.DS4Forms.ViewModels.SlotDeviceEntry`, and the driver card's plain
`ItemsControl`s announced `DS4Windows.ViiperDriverComponentIdentity` /
`…ViiperDriverIdentityField` for every identity row.

Every affected class now overrides `ToString()` with the text the row already displays —
`TriggerLabControl.ProfileChoice` set the repo precedent. The tempting XAML alternative
(`ItemContainerStyle` binding `AutomationProperties.Name`) is wrong for the two `GridView`
lists here: the dark theme ships an implicit app-level `ListViewItem` style whose template is
the `GridViewRowPresenter`, the default theme has none and falls back to the OS style, and
themes swap at runtime — any `BasedOn` choice renders rows wrong in one theme or the other.
`ToString()` is theme-independent, covers every current and future list of the same class, and
is what the peer's fallback reads live on each query.

The sweep (heuristic: every `ItemsSource` in XAML and code-behind whose element type lacked
both a `DisplayMemberPath` at the binding site and a `ToString()` override) found the same
defect on twelve more classes, all fixed the same way: `LogItem` (Log tab rows announced only
the type name — time and message composed now), `ProfileEntity` (Profiles tab cards and every
profile dropdown), `CompositeDeviceModel` (Controllers tab cards and the sidebar controller
list), `DeviceListItem` (controller-options device list), `ProgramItem` (Auto Profiles rules),
`SpecialActionItem` (special actions rows), `MappedControl` (profile editor mapping cards),
`MacroStepItem` (macro recorder steps), `SwipeProfileItem` (swipe-profiles checklist),
`PresetOption` (preset picker), `LangPackItem` (language picker), plus `SlotDeviceEntry` and
the two driver-identity records above.

Verified live against the Release build run from a scratch copy with a portable config marker
(the machine's real configuration untouched): UIA dumps of the Output Slots, Settings,
Profiles, Auto Profiles and Log pages contain zero names matching `DS4Win(WPF|dows)\.`, where
the VM evidence (`uia-outputslots-missing.txt`, `uia-settings-validated.txt`) showed one per
row. Output Slots rows now read `Slot 1: Empty, requested Dynamic`; the driver card rows read
`UDE host controller`, `INF name: usbip2_ude.inf`, and so on; Log rows read the timestamped
message; the Profiles card reads the profile name.

Eight `AccessibleNameTests` pin the composed strings for the classes constructible without
hardware or a WPF `Application` (slot entries empty and input-bound, both identity records,
log item, profile entity, swipe item, language item). Negative control: renaming the
`SlotDeviceEntry.ToString()` override failed exactly its two tests; restored and re-verified.
Suite: **791 passed / 0 failed** (CI filter), from 783.

---

## 2026-07-31 — Third-party licence audit (closes the Phase 0 `TO AUDIT` section)

**Session scope:** the `NOTICE.txt` audit opened as deviation 1 of the Phase 0 entry on
2026-07-25 and carried since as the top release blocker. Branch
`chore/third-party-notice-audit`.

**Method: the distributed artifact is the authority.** Licences were read from each NuGet
package's own `.nuspec` or bundled licence file in the local cache, from in-tree licence files
and source headers for vendored code, and from the upstream repository for bundled binaries that
carry no metadata. GPL compatibility was checked against the FSF's published licence list rather
than from memory. Full working, including a per-entry evidence table, is in
[`third-party-audit.md`](third-party-audit.md).

**The inventory came from a publish, not the csproj**, because the two differ. Two traps that
cost real attention: `Microsoft.Win32.TaskScheduler.dll` is **not** a Microsoft product (its
`CompanyName` is "GitHub Community" — it is dahall/TaskScheduler), so filtering an inventory by
`Microsoft.*` hides it; and `ICSharpCode.AvalonEdit.dll` and `XAMLMarkupExtensions.dll` appear in
no `PackageReference` at all because they are transitive.

### What the audit resolved

24 components now carry a verified licence with recorded provenance — MIT, BSD-3-Clause,
Apache-2.0, Ms-PL, one public-domain dedication — covering every NuGet package that ships an
assembly, every vendored source tree, every bundled binary, the native RNNoise library, and the
three external components Thrum requires but does not redistribute (VIIPER GPL-3.0, usbip-win2
BSD-2-Clause, HidHide).

### What the audit found that nobody had catalogued

1. **The inherited notice was wrong in both directions.** It credited **Font Awesome**, which is
   not in the product — a full-tree search for the name and for any `.ttf`/`.otf`/`.woff` file
   returned exactly one hit, the notice itself. Entry removed.
2. **A 431 KB vendored JavaScript bundle ships uncredited.**
   `DS4Windows/BezierCurveEditor/build.js` is a webpack build of `gre/bezier-easing-editor` (MIT)
   embedding its npm dependency tree including React. It was not in the 2026-07-25 `TO AUDIT`
   list either.

### Three items are unresolved and are stated in NOTICE.txt as release-blocking

1. **`FakerInputWrapper.dll` has no licence grant at all.** `Ryochan7/FakerInputWrapper` has no
   LICENSE file, no source headers, no csproj licence metadata and an empty README. We
   redistribute the assembly in every release; absent a grant there is no permission to do so.
   The native `FakerInputDll.dll` from the separate `Ryochan7/FakerInput` repo **is** MIT — only
   the managed wrapper is affected. Inherited from upstream, not introduced here.
2. **Three Ms-PL assemblies link into a GPL-3.0 program** (DotNetProjects.Extended.Wpf.Toolkit,
   WPFLocalizeExtension, transitive XAMLMarkupExtensions). The FSF states Ms-PL is "incompatible
   with the GNU GPL". Also inherited from upstream DS4Windows, which ships the same three, so the
   exposure is not new — but a first public release is when it starts to matter. Needs a
   maintainer decision, informed by counsel if wanted; the notice states the FSF's position, not
   legal advice. Apache-2.0 components are fine (GPLv3-compatible).
3. **Two vendored items cannot be cleanly licensed as they stand**: `OneEuroFilter.cs` has no
   header and the 1€ filter authors list the C# port as unverified with no licence stated
   (105 lines; reimplement from the CHI 2012 paper or port a BSD version), and the Bezier bundle
   above cannot have its embedded dependencies enumerated from a minified artifact.

### Guard tests

`DS4WindowsTests/ThirdPartyNoticeTests.cs`, 4 tests: every `PackageReference` and every DLL under
`DS4Windows/libs` must be named in `NOTICE.txt`; the three authoritative notice files
(SbcSharp, ControllerArtwork, ICONS) must exist and stay cross-referenced; the UNRESOLVED section
must keep saying it is release-blocking while it exists.

They verify **presence of an entry, not correctness of a licence** — correctness needs the
artifact, which is what the audit document is for. They exist to prevent the silent case, which
is precisely how Font Awesome came to be credited for something absent while a 431 KB bundle
shipped uncredited. Negative control: stripping every occurrence of `WpfScreenHelper` and
`SharpOSC` fails both list tests with their intended messages; restored and re-verified.

Suite: **805 passed / 0 failed**, from 801.

---

## 2026-07-31 — Phase 3.1: lifecycle invariant gap-diff

**Session scope:** Phase 3, task 3.1 — the invariant gap-diff against the maintainer's old
DS4Windows native-mode fork. Branch `phase3/lifecycle-invariant-gap-diff`. Deliverable:
[`lifecycle-invariants.md`](lifecycle-invariants.md).

**The finding that matters for planning: there is far less to port than the plan assumed.** The
plan budgeted 3–6 sessions expecting a large body of old-fork containment work to need adapting.
Most of it is either already present in Thrum in a different form, or made moot by the
architecture — because the risky component the invariants were written around, an in-process
USB/IP helper holding a WASAPI render lease on a virtual audio endpoint, **does not exist in
Thrum**, and the audio endpoints that made it necessary are off by default since 2.3.

Three of the six invariants (c, d, e) are phrased in terms of *render protection*. Thrum holds
none. Read literally they are N/A; read for their purpose — never release a safety hold until the
dangerous thing is provably gone — they re-derive onto the census/ownership model from 2.4b, and
that is how they were assessed.

| invariant | verdict | action |
|---|---|---|
| (a) retired generation emits no late success | Present (feedback), partial (state writer) | port the drain barrier to the writer — small |
| (b) UNLINK cannot strand or double-complete | moved to VIIPER; **ownership gap found** | report upstream |
| (c) prove exact-device absence | re-derived, Present via census, fail-closed | optional PnP cross-check, low priority |
| (d) parent death retains a protection | N/A — architecture prevents the dangerous case | Phase 4 diagnostics affordance |
| (e) timeout ≠ permission to kill | Present via the census gate | none; window documented |
| (f) unproven removal blocks reuse | partial — cleans up rather than blocks | port the refusal branch — small |

### The upstream finding (Phase 3.4's deliverable, arrived early)

VIIPER's Go server handles the *stranding* half of invariant (b) deliberately — `server.go:929`
carries the comment "Cancellation must never strand a later URB behind this one." The
*exactly-once* half has a hole. Its UNLINK handler claims ownership correctly (`server.go:805-828`:
look up under `pendingMu`, delete, reply `-ECONNRESET` only `if found`). The ISO-IN completion
path does not make the symmetric check — `server.go:967-971` deletes unconditionally, discards
the result, and `writeRet` (`server.go:614`) applies no ownership test. An UNLINK arriving after
the completion goroutine's last context check (`server.go:955`) and before it takes the mutex
produces **both** a `RET_UNLINK(-ECONNRESET)` and a `RET_SUBMIT` for one seqnum.

Static reading, not reproduced, narrow window — but the consequence is a client handed a response
for a request it no longer owns, which is the same shape as the kernel-side defect class in
usbip-win2 #181 and exactly what its PR #182 arbitration prevents on the other side of the wire.
Fix is the same shape as VIIPER's own UNLINK handler: make the delete the ownership claim.

### Verification

Every file:line citation in the deliverable was checked against the current tree (9 Thrum
citations re-read and confirmed; VIIPER citations read from `upstream-hbashton-viiper`). No code
changed in this task — it is an analysis deliverable, and 3.3 is where the two small ports land.

---

## 2026-07-31 — Fix: a bare `dotnet build`/`dotnet test` could not compile (AnyCPU default)

Build configuration only; no product code or tests changed.

Origin: inherited. Upstream keeps `libs\` next to the solution, so its project file says
`<HintPath>..\libs\$(Platform)\...`; this repository imported the tree with `libs\` inside
`DS4Windows\` and kept the HintPaths, which have therefore pointed at a directory that never
existed here. x64/x86 builds still resolved FakerInputWrapper and SharpOSC — but only because the
SDK's default item globbing picks the DLLs up as `None` items and ResolveAssemblyReference's
`{CandidateAssemblyFiles}` fallback matches them by simple name. On AnyCPU — the MSBuild default
for a bare `dotnet build`/`dotnet test` against a csproj — the csproj strips both `libs\` trees
from the item list, the fallback has nothing to match, and compilation dies with CS0246 before a
single test runs. The app csproj even carried `<Platform Condition="'$(Platform)' == ''">x64
</Platform>`, visibly intending an x64 default, but Microsoft.Common.props has already defaulted
an unset Platform to `AnyCPU` by the time a project body evaluates, so that condition never fired.

The fix: HintPaths now name the real location (`libs\$(Platform)\...`, project-relative), so
resolution is declared rather than accidental; the existing x64-default line also remaps the
`AnyCPU` default; the test project gets the same remap; and `AnyCPU` leaves the test project's
`<Platforms>`, since it was never buildable. An explicit `-p:Platform` is a global property and
still overrides the remap, so CI (`-p:Platform=x64`) and x86 builds behave exactly as before.

Negative control: at the same base commit without the fix, a bare `dotnet test` against the test
csproj reproduces `error CS0246: ... 'SharpOSC' could not be found`; with the fix the same
command builds as x64 and runs the full suite — 808 tests, the three known snapshot failures and
nothing else. Property evaluation probed both ways: bare evaluation yields `Platform=x64` with
`bin\x64\` outputs; `-p:Platform=x86` still yields `Platform=x86` with `bin\x86\` outputs. Fresh
Release builds of the solution succeed for x64 and x86 with no MSB3245 and the warning count
unchanged from baseline (17).

Suite: **805 passed / 0 failed** (CI filter), unchanged from 805 — build configuration only.

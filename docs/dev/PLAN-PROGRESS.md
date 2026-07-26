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

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

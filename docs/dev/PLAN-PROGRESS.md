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

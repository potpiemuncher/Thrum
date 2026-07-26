# ADR-0002: Upstream tracking policy

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** the Thrum maintainers
- **Depends on:** [ADR-0001](ADR-0001-repo-topology.md)

## Context

Thrum's history was imported from hbashton/DS4Windows and continues to
diverge from it. Upstream keeps shipping: input-device fixes, VIIPER backend
protocol work, installer changes, UI modernization. Thrum wants that work.

Divergence is not symmetric, though. Thrum changes cluster in new files
(driver validation, gating, diagnostics), in documentation, in installer and
branding — while upstream changes cluster in the shared engine files that
Thrum also has to touch occasionally. Without a policy, the two drift until
merging becomes a project of its own and upstream fixes silently stop
arriving.

An imported-history repository (ADR-0001) makes real merges possible, because
the two histories share genuine ancestry. This ADR defines how that ability is
actually used.

## Decision

**1. Remote and mirror branch.**

Every clone configures:

```
upstream = https://github.com/hbashton/DS4Windows.git
```

The branch **`upstream-track`** mirrors upstream `main` and contains **no
Thrum commits, ever**. It is updated by fast-forward only:

```
git fetch upstream
git checkout upstream-track
git merge --ff-only upstream/main
git push origin upstream-track
```

If that `--ff-only` merge is ever refused, upstream has rewritten history —
stop and investigate; do not force the branch into shape.

**2. Merge, never rebase.**

Upstream is integrated by merging `upstream-track` into `main`:

```
git checkout main
git merge --no-ff upstream-track
```

Rebasing Thrum's work onto upstream is prohibited. It would rewrite published
history, invalidate the commit identities that VM validation evidence and
release records reference, and re-resolve every historical conflict on every
sync.

**3. Cadence.**

- **At least monthly**, whether or not anything looks urgent.
- **Before every release branch is cut**, without exception.
- **Immediately** for an upstream fix to a defect that affects Thrum users.

A merge that is skipped is recorded in `docs/dev/PLAN-PROGRESS.md` with the
reason. Silence is not an acceptable record.

**4. Conflicts in engine files are resolved minimal-diff.**

When a merge conflicts in a shared engine file (`ScpUtil.cs`,
`ControlService.cs`, `ViiperOutDevice.cs`, `DualSenseDevice.cs`, the large WPF
code-behinds, and their neighbours), the resolution takes **upstream's shape**
and reapplies Thrum's change as the smallest possible addition on top. Do not
reformat, do not reorder, do not carry forward a local cleanup that caused the
conflict in the first place. If a Thrum change keeps conflicting, that is
evidence the change belongs in a new file behind a seam, or belongs upstream —
fix the cause, not the merge.

Every merge is verified before it is pushed: x64 Release build plus the full
test suite. A merge that has not been tested is not finished.

**5. Divergence budget alarm.**

After each merge, measure:

```
git diff upstream-track...main --stat
```

If added lines **outside** `docs/`, `installer/`, and branding paths exceed
approximately **15,000**, that triggers a review — not a block. The review
asks, for each large cluster: can this be offered upstream, can it move behind
a seam into new files, or is it a deliberate product difference Thrum owns on
purpose? Record the outcome in the progress log.

The number is a smoke alarm, not a limit. It exists because fork delta grows
quietly and is only ever cheap to reduce early.

## Rationale

- **Merging preserves the evidence chain.** Driver-safety work is validated
  against exact tree states in a VM; those records name commits. Rebasing
  would make every such reference dangle.
- **A clean mirror branch makes divergence measurable.** `upstream-track` with
  zero local commits is what gives `git diff upstream-track...main` a
  trustworthy meaning, and what lets `git log upstream-track..main` list
  exactly what Thrum has added.
- **Monthly beats "when needed".** Merge pain grows superlinearly with
  distance; a fixed cadence keeps every merge small enough to review honestly.
- **Minimal-diff resolution is the same discipline as the contribution rule.**
  It is stated in `CONTRIBUTING.md` for authoring and here for merging,
  because they are the same lever seen from two ends.
- **The budget encodes the mergeability-first principle.** Until the product
  is established, a smaller delta against upstream is a feature: it means
  upstream fixes keep arriving cheaply and Thrum's improvements can still be
  handed back.

## Consequences

- `main` accumulates merge commits. Accepted; history stays truthful.
- Contributors must keep engine-file diffs small — enforced in review.
- The mirror branch must never be pushed to with anything but a fast-forward,
  and must never be used as a base for feature work.
- Upstream-portable work should be offered upstream as it is written, not
  batched up later, because the budget alarm only measures what has already
  accumulated.
- Import base for reference: upstream `5d2724a` (v4.0.2.1-dualsense-beta),
  2026-07-25. Upstream had already moved past it at import time; the first
  scheduled merge cycle picks that up.

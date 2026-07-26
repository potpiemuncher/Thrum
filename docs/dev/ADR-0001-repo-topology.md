# ADR-0001: Repository topology

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** the Thrum maintainers
- **Supersedes:** none

## Context

Thrum is a new product built on two existing GPL-3.0 projects:

- **hbashton/DS4Windows** — the application: .NET 8 WPF, controller input,
  profiles, mapping, audio haptics.
- **hbashton/VIIPER** — the backend: a Go daemon that presents virtual USB
  controllers over USB/IP, driven by the application over a local socket.

Both continue to develop. Thrum needs to keep receiving their work while
diverging from it in identity, defaults, and safety policy. Three questions
had to be settled before any code moved:

1. Should the application repository be a GitHub fork of DS4Windows, or an
   independent repository?
2. Should VIIPER be vendored into the application repository, or consumed as
   an external artifact?
3. Where do backend changes get written?

Two constraints shaped the answer. First, GitHub permits **one fork per
account per network**, and the maintainer's account already holds a fork
inside the DS4Windows network (`potpiemuncher/DS4Windows`, from the older
`ds4windowsapp` lineage) — so a second fork in that network is not available
without giving up the first. Second, VIIPER is a separately built and
separately released Go binary with its own kernel-driver prerequisite; it is
not a library the application links against.

## Decision

**1. The application repository is an independent repository with full
imported history, not a GitHub fork.**

`potpiemuncher/Thrum` was created empty and seeded by pushing the complete
history of hbashton/DS4Windows into it (import base `5d2724a`,
v4.0.2.1-dualsense-beta), plus the Thrum maintainers' driver-validation
branch. No commits were rewritten, squashed, or rebased.

**2. VIIPER is consumed as a pinned release binary. Its source is never
vendored into this repository.**

The application locates, verifies, and launches a specific VIIPER release
identified by version **and** SHA-256. It does not build VIIPER, does not
contain a copy of its source, and does not track its source tree here.

**3. Backend contributions flow through a separate VIIPER fork.**

Changes to VIIPER are written in the maintainers' own fork of
hbashton/VIIPER and offered upstream as pull requests there. They do not
appear in this repository in any form.

## Rationale

**Independent repository, imported history.**

- The one-fork-per-network limit is already consumed, so "just fork it" was
  not actually on the table.
- A fork's GitHub presentation ("forked from …", issues and pull requests
  defaulting toward the parent, a shared network) frames the work as a patch
  set on someone else's product. Thrum is a distinct product with a distinct
  name, distinct defaults, and a driver-safety posture upstream does not
  share. The repository should say that.
- Nothing is lost technically. Mergeability lives in git remotes, not in
  GitHub's fork relationship: every clone carries
  `upstream = https://github.com/hbashton/DS4Windows.git`, and the
  `upstream-track` branch mirrors upstream `main` (see
  [ADR-0002](ADR-0002-upstream-tracking.md)). Merges work exactly as they
  would from a fork.
- Full history was imported rather than squashed to a single import commit.
  It preserves authorship for GPL attribution, keeps `git blame` and
  `git log --follow` useful across the inherited 127k-line codebase, and lets
  upstream merges resolve against real common ancestors instead of an
  artificial root.
- Cost accepted: pull requests cannot be opened directly from Thrum to
  upstream through the GitHub fork UI. Upstream offers are made from the
  maintainers' existing fork or from a branch pushed there. This is a small,
  per-contribution inconvenience, paid rarely.

**VIIPER as a pinned binary, not vendored source.**

- It is a different language and toolchain (Go), with a different release
  cadence and its own kernel-driver prerequisite. Vendoring it would make
  every Thrum build depend on a Go build and would merge two release
  processes that need to stay separate.
- The relationship is a runtime protocol over a local socket, not a
  compile-time dependency. There is no linkage to vendor.
- Pinning is a safety requirement, not a convenience. Thrum's whole
  differentiator is that it refuses to run unverified components: the backend
  is admitted by exact version plus SHA-256, and the kernel driver beneath it
  by manifest match. A vendored source tree would obscure exactly the
  artifact identity the safety layer depends on.
- Users may already have VIIPER installed from a real DS4Windows install.
  Treating it as an external, shared, version-checked component makes
  "detect and adopt" a coherent behaviour instead of a conflict.

**Backend contributions through a separate fork.**

- VIIPER stays an upstream-tracked project; Thrum does not fork the product,
  only contributes to it. Keeping the two repositories separate keeps the
  contribution reviewable by its actual maintainer and keeps Thrum's history
  free of Go changes it does not build.

## Consequences

- Every clone of this repository configures a local `upstream` remote and a
  `upstream-track` branch. Contributors get this from the documented setup;
  it is not automatic on `git clone`.
- Upstream-bound patches require an extra hop through the maintainers' fork.
- The build never produces `viiper.exe`. Anything that needs a backend at
  build or test time uses fakes or a pinned, verified local copy.
- The pinned VIIPER version and its hash become release-gating facts that must
  be recorded per release, alongside the corresponding-source reference.
- Divergence is measurable. `git diff upstream-track...main` is meaningful
  because the histories share real ancestry — see ADR-0002 for the budget
  alarm built on that.

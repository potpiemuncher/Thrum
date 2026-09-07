# VIIPER driver validation diagnostic

This directory is a read-only identity and trust check for the installed
usbip-win2 package pair.

**What it is wired into (plan task 2.1):** VIIPER readiness. `ViiperSetupManager`
resolves a four-state answer — `Missing` / `DetectedUnvalidated` /
`ValidatedExperimental` / `Approved` — through `ViiperDriverReadinessProvider`,
caches it for the session, and exposes it on `ViiperPrerequisiteStatus`
alongside the unchanged `Ready` flag. The Settings driver-status card renders
it (task 2.2).

**Gating is live** (tasks 2.3 and 2.5 landed; validated in the VM as Phase C, and
exercised repeatedly on hardware since). Reaching a state now changes what the
application does: virtual-device creation is refused unless the experimental
driver has been explicitly acknowledged, and the refusal is surfaced to the user
with the reason. A representative log line:

```
Refused to create the virtual Xbox 360 output. Virtual controllers run on a
third-party kernel driver that is still experimental, and that has not been
acknowledged yet.
```

Virtual audio and microphone endpoints are additionally off by default and gated
separately, because the confirmed kernel defect is in endpoint teardown.

`ViiperPrerequisiteStatus.Ready` still means "the backend can run" and is
unaffected by the tier — that separation is deliberate and unchanged.

**Read-only guarantees, unchanged.** Nothing here installs, uninstalls,
elevates, attaches, detaches, starts a server, releases a controller, or writes
a setting. The only write anywhere in the directory is the diagnostic report
file under `%TEMP%`. Readiness resolution performs exactly the same reads the
diagnostic does — a SetupAPI enumeration and Windows trust-API verification —
and it happens once per session unless something explicitly refreshes it.

Two observed usbip-win2 identities are present so the SetupAPI and
WinVerifyTrust paths can be exercised on disposable Windows 11 snapshots:

- 0.9.7.7 records the exact UDE, filter, and client versions extracted offline
  from the signed x64 installer currently targeted by hbashton's VIIPER
  installer script. The x86 package has not been inspected and is not claimed.
- 0.9.7.8 records the earlier local controlled-test dossier.

Matching either entry is not production approval. The entries are diagnostic
baselines only, and package identity/signing alone does not establish that a
release is safe to run.

**Correction on 0.9.7.8's root cause.** This file previously attributed it to a
reproduced request-lifetime failure in `usbip2_ude.sys`. That was our working
theory and it is **not** the cause: the defect was subsequently confirmed to be
an `argv<>` overrun in `usbip2_filter`. Our own patch for the lifetime races was
submitted as usbip-win2 PR #182 and the maintainer's separate fix addresses the
real overrun. 0.9.7.8 remains known-risk, but for that reason, not this one.

Status of the original pre-enforcement checklist:

1. ~~collect an installed report on a clean, checkpointed VM without attaching a
   controller~~ — done; evidence in `vm-validation-reports/`.
2. ~~agree whether either release may be used experimentally~~ — resolved as
   explicit user acknowledgement rather than a blanket decision.
3. ~~decide whether Thrum, VIIPER, or both enforce the manifest~~ — Thrum
   enforces, via `ViiperInstallerPins` digest pinning.
4. ~~add a read-only user-facing diagnostic~~ — the Diagnostics page.
5. ~~add download verification and post-install validation~~ — landed with the
   hardened installer path (VM Phase B).
6. **Still open:** production approval stays gated on an upstream release
   containing the confirmed fix. Until such a release exists, the gate reports
   `Production approved: no` for every known package, which is the intended
   outcome rather than a gap.

The offline tests use fake inspectors and trust verifiers. They do not install
a driver, request elevation, attach a virtual device, or require a controller.

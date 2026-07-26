# VIIPER driver validation diagnostic

This directory is a read-only identity and trust check for the installed
usbip-win2 package pair.

**What it is wired into (plan task 2.1):** VIIPER readiness. `ViiperSetupManager`
resolves a four-state answer — `Missing` / `DetectedUnvalidated` /
`ValidatedExperimental` / `Approved` — through `ViiperDriverReadinessProvider`,
caches it for the session, and exposes it on `ViiperPrerequisiteStatus`
alongside the unchanged `Ready` flag. The Settings driver-status card renders
it (task 2.2).

**What it is still not wired into:** installation, virtual-device attachment,
controller release, or any refusal. Reaching a state does not change what the
application will do; gating on the state is task 2.3, and runtime guardrails are
task 2.5. `ViiperPrerequisiteStatus.Ready` deliberately still means "the backend
can run" and is unaffected by the tier.

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
baselines only; 0.9.7.8 remains known-risk after a reproduced request-lifetime
failure in `usbip2_ude.sys`, and package identity/signing alone does not
establish that 0.9.7.7 is safe to run.

Before runtime enforcement:

1. collect an installed 0.9.7.7 report on a clean, checkpointed VM without
   attaching a controller;
2. agree with maintainers whether either release may be used experimentally;
3. decide whether DS4Windows, VIIPER, or both enforce the manifest;
4. add a read-only user-facing diagnostic command;
5. add download verification and post-install validation; and
6. keep production approval gated on a signed fixed driver or an accepted
   replacement driver strategy.

The offline tests use fake inspectors and trust verifiers. They do not install
a driver, request elevation, attach a virtual device, or require a controller.

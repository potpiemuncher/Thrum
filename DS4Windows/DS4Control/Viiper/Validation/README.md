# VIIPER driver validation diagnostic

This directory is a work-in-progress, read-only diagnostic foundation. It is
not wired into VIIPER readiness, installation, virtual-device attachment, or
controller release.

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

# Handoff — state of Thrum as of 2026-08-11

Written for whoever picks this project up next. It says what is true now, what
is *not* true despite appearing so, and which mistakes this project has already
made so they are not made twice.

`main` is at `41f3a1d`. Suite ~1027 tests, 0 failing. Build: 0 errors,
17 known warnings.

## Read these first, in this order

1. `README.md` — what Thrum is, and the driver-safety posture. Not optional
   context: the driver situation shapes most design decisions here.
2. `NOTICE.txt` — three unresolved third-party licence items, each with a
   recorded decision. **Recorded is not resolved.** A future release must
   re-examine each rather than inheriting these.
3. `CONTRIBUTING.md` — especially "Kernel-driver work is [VM]-only" and the
   minimal-diff policy for inherited engine files.
4. `docs/dev/PLAN-PROGRESS.md` — the running ledger, ending with the release and
   Phase 5 section.
5. This file.

## The one thing that was broken, and where it stands

**Beta 1 could not create a virtual DualSense.** Its VIIPER v0.0.6 backend
answered `400 Bad Request: unknown device type: dualsense` because Thrum asked
for the legacy `dualsenseext` name, which v0.0.6 had dropped in favour of the
PadSense V5 personas. Evidence: issue #70 and
`vm-validation-reports/viiper-006-dualsense-regression-20260810/REPORT.md`.

Two things have happened since. The V5-first negotiation (#70) creates the
DualSense correctly and was VM-validated against v0.0.6. And the backend pin
has moved to **VIIPER v0.1.2** (2026-09-06), which registers the same V5
names; that made the v0.0.5 rollback in #79 moot. **The v0.1.2 pin has only
been checked statically and by the installer-path tests — the plug validation
in `docs/viiper-backend-upgrade-path.md` has not been run against it yet.**

## What is genuinely verified, and what only looks it

Verified on real hardware (a physical DualSense, BT and USB): profile switching,
rumble that stops itself, adaptive triggers, lightbar and identify, gyro, live
input, touchpad, charging, transport switching, and **Audio Haptics streaming to
the pad's actuators over Bluetooth with no virtual controller and no driver**.

Verified in the VM: first-run setup, the hardened installer including refusals,
the driver gate's four states, diagnostics redaction 9/9.

**Not verified, and the release notes say so:** Audio Haptics over USB (#65),
virtual DualSense against the current backend (#70/#79), any controller other
than DualSense, multiple simultaneous controllers, long-duration stability,
keyboard-only navigation (#51).

## Mistakes this project already made — do not repeat them

**A green suite is not working software.** Two crashes shipped that 974 passing
tests could not see (#48 wizard, #55 input tester), both XAML faults that throw
only when a template instantiates. Two more defects were found only by unpacking
the *published* artifact (#75) and by looking at the running app (the donation
card, PR #77). Guards now exist for each class — `XamlStaticResourceTests`,
`XamlBindingModeTests`, `NoDonationSolicitationTests`,
`ReleasePackagingTests` — but the habit matters more than the guards: **run the
app, unpack the artifact.**

**"It starts" is not "it works".** The v0.0.6 pin passed validation that
exercised the installer path and never created a device. A backend pin is not
validated until one virtual device of **every supported type** has plugged
successfully. That needs no hardware — see below.

**Verify the verification.** Several checks in this project passed while testing
nothing: a `Select-String -Recurse` that errored into a null result and reported
"clean"; a UTF-16 binary scan that missed strings on odd byte offsets; a negative
control whose mutation was overridden by a later line. If a check cannot fail,
it is not a check. Mutate the thing and watch it fail before trusting a pass.

**Hex and paths do not survive hand-copying.** Machine-diff digests. A verifier
agent once derived a hash correctly and then transcribed it with two characters
missing.

## Techniques worth reusing

**Create a virtual controller with no physical input.** Mark an output slot
permanent in `%APPDATA%\Thrum\OutputSlots.xml`;
`ControlService.AssignInitialDevices()` plugs it with `InputIndex = -1`:

```xml
<OutputSlots app_version="0.9.0.0">
  <Slot idx="0"><DeviceType>ViiperDualSense</DeviceType></Slot>
</OutputSlots>
```

Types: `ViiperX360`, `ViiperDS4`, `ViiperDualSense`, `ViiperDualSenseEdge`,
`ViiperSwitch2Pro`. This is how #70 was answered, and it makes device-creation
checks runnable unattended.

**Driving the TESTENV guest with no credentials or network:** the harness in
`../vm-phase2-evidence-20260730/` — `vm-drive.ps1` for keyboard and console
screenshots, `vm-rpc.ps1` plus the guest runner for a KVP command channel,
`Copy-VMFile` for staging. Restore a checkpoint, clear the Windows setup nags
(Tab, **screenshot to confirm focus**, Enter — blind key presses open the wrong
thing), then Alt+Y for the runner's UAC prompt.

**USB passthrough into the guest is not available.** DDA was investigated and
refuted: on this host it is a one-way door (`Disable-PnpDevice` persists across
reboot; `Enable-PnpDevice` needs a privilege the working token lacks) and would
be blocked at `Add-VMAssignableDevice` regardless, since Windows 11 client
supports only GPU and NVMe assignment. Full working in
`vm-validation-reports/usb-controller-map-20260808.md`.

## Hard constraints

- **Never test kernel-driver paths on the dev PC.** It runs usbip-win2 0.9.7.8,
  the build with the confirmed corruption defect. Use TESTENV.
- **Never enable test signing, or weaken Secure Boot / VBS / HVCI**, anywhere.
- **Never attach a crash dump to a public issue** — they contain kernel memory.
  See `SECURITY.md`.
- **No personal data in committed content or issues** — no names, emails,
  machine names, MACs, or `C:\Users\...` paths.
- Virtual **audio/mic endpoints** stay off by default; that is the teardown path
  with the kernel defect.

## Upstream, and the one event that unblocks a chain

usbip-win2 **PR #182 (ours) is merged into `master`**, alongside vadimgrn's
`4139f44f6` root-cause fix for the corruption. **No release carries either.**
When a release is cut from master, it triggers: add a candidate tier to
`ViiperDriverManifest`, revisit the pin in `ViiperInstallerPins`, rewrite the
audio-consent risk text that currently says no release is production-approved,
re-run the VM installer path, and tick plan item 3.5. Until then nothing changes.

Also open: hbashton/VIIPER PRs #3 and #7 (both ours), and
Ryochan7/FakerInputWrapper#1, where he has said he will apply **LGPL** (which
resolves NOTICE item 1) and then archive — when it lands, snapshot the library
source for our own LGPL source-provision obligation.

## Waiting on the maintainer, not on engineering

- The VIIPER pin rollback decision (#79).
- Whether to report the DualSense regression upstream.
- Buying a code-signing certificate. Certum's **Open Source Code Signing in the
  Cloud**, EUR 49/yr, looks like the route; see ADR-0005 including its
  amendment. Tooling is built and proven with a throwaway certificate; only the
  success path is unproven because that needs a real one.

## The workspace outside this repo

The checkout lives in `C:\Users\patri\PS5Haptics`, which was reorganised on 2026-09-06 and
has its own `README.md` describing the layout. The short version: `Thrum/` (this repo),
`DS4Windows/` (the hbashton fork the BT haptics code came from), `upstream-hbashton-viiper/`,
`usbip-win2-fix/` (the PR #182 tree), then `docs/` (the original phased plan, VM runbooks,
upstream PR drafts, agent prompts), `evidence/` (every VM report, crash triage and
measurement run — the reusable guest harness is in `evidence/vm/vm-phase2-evidence-20260730/`),
`packages/`, `reference/`, `tools/` (Codex bridge and scripts) and `_archive/`. Nothing in
this repo references those paths, so moving them again only affects memory notes and the
harness scripts.

## Beta 2 queue

#51 keyboard nav re-verify · #65 Audio Haptics over USB · #66 double stream start
· #67 user guide rewrite (it still describes the pre-Phase-4 tabbed UI) · #70/#79
VIIPER pin · #71/#72 Ms-PL replacement · #75 ship NOTICE and COPYING inside the
archive.

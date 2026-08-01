# Phase 3.2 — does a virtual DualSense steal Windows' default audio endpoints?

Measurement run 2026-08-01 on `Win 11 25H2 Test ENV` (Windows 11 25H2 build 26200), against
usbip-win2 **0.9.7.7** with Secure Boot **on** and test signing **off** — the hardened posture,
and the release that sits on the *good* side of the `usbip2_filter` bisect (see
[lifecycle-invariants.md](lifecycle-invariants.md) and usbip-win2#181), so this exercised the
audio-endpoint path at materially lower risk than when the takeover was first observed.

Raw log: `vm-audio-default-32/endpoint-log.jsonl` (5 probes). Checkpoint taken immediately
before the first attach: `phase3.2-before-audio-attach-20260801`.

## Result: promotion is real, total, and repeats on every attach

Before attach the guest had **no audio endpoints at all** — a Gen 2 Hyper-V VM has no emulated
sound card, so all six default slots were empty.

On attach of the synthetic composite DualSense (the byte-exact captured Sony descriptor set,
including the UAC1 speaker and microphone), Windows enumerated
`MEDIA  DualSense Wireless Controller` and immediately made it the default for **all six**
role/flow combinations:

| slot | after attach |
|---|---|
| Render / Console | `Speakers (DualSense Wireless Controller)` |
| Render / Multimedia | `Speakers (DualSense Wireless Controller)` |
| Render / Communications | `Speakers (DualSense Wireless Controller)` |
| Capture / Console | `Headset Microphone (DualSense Wireless Controller)` |
| Capture / Multimedia | `Headset Microphone (DualSense Wireless Controller)` |
| Capture / Communications | `Headset Microphone (DualSense Wireless Controller)` |

Detach released all six cleanly — back to no default, no dangling reference, no PnP device in a
problem state. A second attach/detach cycle behaved identically.

**Endpoint identity was stable across both attaches**: the same GUIDs each time
(`{0.0.0.00000000}.{8ba1afad-…}` render, `{0.0.1.00000000}.{dcda2840-…}` capture), and the
MMDevices registry accumulated **zero** stale not-present instances over two cycles. That is a
real difference from the 2026-07-20 observation on the dev PC, which had ~10 stale
`Speakers / DualSense Wireless Controller` instances — identity churn that made every attach look
like a brand-new device and so guaranteed promotion. Checked read-only on the dev PC while
writing this: it now has **0** such entries in both Render and Capture, so that condition is not
currently reproducible there either.

Health across the whole run: zero bugchecks, zero minidumps, no `MEMORY.DMP`, zero PnP problems,
`usbip port` empty at the end.

## What this does not establish

**The VM had no incumbent audio device, so "became default" was uncontested.** The operative
user-facing complaint — *the pad takes over from the headset I was using* — is a displacement,
and displacement cannot be measured where there is nothing to displace. Round 2 does not close
this either: after the first detach there was still no other endpoint, so the
"restore the endpoint that was default at removal" policy path also had nothing to displace.

This is a structural limit of the environment, not an oversight. A Gen 2 Hyper-V guest has no
sound card, and adding one means either enhanced-session mode (which replaces the guest's own
endpoint list with Remote Audio, defeating the measurement) or installing a third-party virtual
audio driver into a VM we deliberately keep offline.

## Verdict on the guard

**Justified, and scoped to audio-class consent — but not yet ported.**

What the measurement *does* prove is the half that matters most for deciding: Windows treats
these endpoints as promotion candidates and takes **all six slots**, every time, including
`Communications` on both flows. The capture side deserves particular note — a pad microphone
silently becoming the default communications device is the kind of thing that surfaces as "my
voice chat broke" rather than "my audio moved", and is harder for a user to diagnose.

Combined with the direct field observation from the old fork (the takeover *was* experienced on
the real PC, with this same descriptor set, on the 2026-07-19 unguarded sessions), the mechanism
is established well enough to say a guard is the right eventual answer.

It is **not** ported in this change, for two reasons worth stating rather than quietly acting on:

1. **The displacement case is still unmeasured**, and it is the one the guard exists to prevent.
   Porting 726 LOC plus its notification plumbing to stop a behaviour not yet observed
   displacing anything would be building on inference. The same discipline was applied to the
   `usbip2_filter` fix earlier: no baseline failure, no claim of a verified fix.
2. **Nothing ships exposed to it today.** Audio-class endpoints are off by default behind the
   2.3 consent gate, so no user reaches this path without explicitly opting in. It is not a beta
   blocker, which means there is time to close (1) properly rather than guess.

### Closing the gap cheaply

The missing measurement is small and precise: **one attach on a machine that already has a
default render endpoint**, checking whether the pad displaces it. Options, cheapest first:

- The **dev PC** already satisfies the precondition (Sonar is the incumbent — see the workspace
  note on that setup). It must **not** be used: it runs usbip-win2 0.9.7.8, which is on the *bad*
  side of the filter bisect. Ruled out on safety, not convenience.
- Give the **VM an incumbent endpoint**: install a virtual audio cable driver in the guest, or
  attach a second synthetic device. The latter is currently blocked because `usbip attach` takes
  no TCP port option (`-r` is host-only), so a second server instance is unreachable; adding a
  port flag to the test server would fix that.
- Wait for a usbip-win2 release carrying the filter fix, then measure on real hardware where the
  original failure was seen.

  **Status as of 2026-08-01: the fix exists, but not yet in a release.** vadimgrn landed
  [`4139f44`](https://github.com/vadimgrn/usbip-win2/commit/4139f44f6a87c8b1b71d1015c1bce9443cb86688)
  on `develop`, taking a different route from the community patch that preceded it: the
  `libdrv::argv` templates are deleted outright, the filter allocates `StackSize + 1` locations
  and calls `IoSetNextIrpStackLocation` so it owns the slot it writes to, and the `ude` side
  moves its stashed arguments out of the IRP entirely. We reviewed it by reading — verdict
  correct and complete, with no surviving instance of the bug class anywhere in that tree — and
  said plainly on the issue that this is **not** an empirical confirmation, because our VM never
  reproduced the corruption at baseline and a clean patched run therefore proves nothing.

  Two consequences here. The fix also **explains the field variance** that made this bug look
  machine-specific: the old write went past `sizeof(IRP) + StackSize * sizeof(IO_STACK_LOCATION)`,
  so whether it corrupted the next pool block or landed in the allocator's rounding slack
  depended on how deep the device stack under the filter was. That retires the open question in
  our earlier write-up. And this option is now waiting on a *release*, not on a fix — when one
  ships, the driver manifest gains a candidate baseline, the dev PC stops being ruled out, and
  this measurement becomes cheap.

### If and when it is ported

The old fork's `NativeModeAudioDefaultGuard` (796 LOC in `DS4Windows-native-mode-pr`, 632 LOC of
tests, interface-seamed via `INativeModeAudioEndpointAccessor` /
`INativeModeAudioNotificationSource`) is the right starting point and adapts cleanly. Required
changes for Thrum:

- **Identity**: match on the VIIPER device rather than the native-mode helper's endpoint. The
  friendly names observed here (`Speakers (DualSense Wireless Controller)`,
  `Headset Microphone (DualSense Wireless Controller)`) are not a safe key on their own — a real
  DualSense produces the same names. Key on the endpoint's device instance / container id, tied
  to the VIIPER-created device.
- **Both flows**: the old guard's policy must cover Capture as well as Render. This measurement
  shows the microphone is promoted just as aggressively.
- **Lifetime**: snapshot at device creation, subscribe to endpoint/default-change notifications,
  revert an unwanted change, restore on removal, fail-closed on teardown — the policy is already
  correct as written.
- **Surface**: guard state belongs on the Phase 4 diagnostics page, as the plan already notes.

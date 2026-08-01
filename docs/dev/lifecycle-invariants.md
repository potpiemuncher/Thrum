# Lifecycle invariants — gap-diff against the old fork

Phase 3, task 3.1. Source of the invariants: the containment work in the maintainer's
DS4Windows native-mode fork (`NATIVE-MODE-UPSTREAM-PR-BODY-R2.md`, and the `NativeMode*` classes
in `DS4Windows-native-mode-pr/`, ~5,700 LOC with ~2,900 LOC of tests). Target of the diff:
Thrum as of `76d34d8`, plus the VIIPER Go server it drives.

Verdicts are **Present**, **Port**, **Re-derived**, or **N/A**, each with the evidence that
justifies it. Where a gap is real, it says what would close it and how much that is worth.

## Why these cannot be transplanted literally

The old fork and Thrum solve the same problem with different machinery, and every invariant was
written against the old machinery's failure modes. The audit that started this project warned
"adapt, don't transplant"; this is what that means concretely:

| | old fork (native mode) | Thrum |
|---|---|---|
| who serves USB/IP | a .NET helper (`VirtualDualSenseUsbip`) that DS4Windows spawns and owns | **VIIPER**, a Go server, a separate product with its own lifetime |
| who owns the virtual device | the helper, for the lifetime of the app session | VIIPER; Thrum asks for one over a local HTTP API |
| virtual audio endpoint | always present (composite UAC1 device) | **absent by default** — Phase 2.3 puts audio-class behind an explicit consent gate |
| render protection | DS4Windows *and* the helper each hold a silent WASAPI render lease on the exact virtual endpoint | **none — the concept does not exist in Thrum** |
| default-audio guard | `NativeModeAudioDefaultGuard`, 796 LOC | none |
| teardown authority | DS4Windows drives ordered teardown of a process it owns | Thrum stops a backend **only** if it started it and the census proves it idle (2.4b) |

Three of the six invariants (c, d, e) are phrased in terms of *render protection*. Thrum holds
no render lease, because it has no in-process helper and, by default, no virtual audio endpoint
at all. Read literally they are N/A. Read for their **purpose** — never release a safety hold
until the dangerous thing is provably gone — they re-derive onto Thrum's census/ownership model,
and that is how they are assessed below.

---

## (a) A retired ISO/stream generation emits no later success

**Purpose.** Once a stream is retired, nothing from that generation may still complete, so a
late success cannot be attributed to a live session.

**Verdict: Present** on the feedback path, **partial** on the state-writer path.

Thrum carries real generation machinery in `ViiperOutDevice`: `streamGeneration`,
`feedbackDispatchGeneration`, `stateWriterGeneration`, `microphoneWorkerGeneration`
([ViiperOutDevice.cs:283-288](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)). Dequeued
feedback items carry the generation they were queued under and are dropped if it has moved:

```csharp
if (!IsFeedbackDispatchGenerationActive(generation) ||
    streamItemGeneration != Interlocked.Read(ref streamGeneration))
{
    Interlocked.Increment(ref feedbackSpeakerStale);
    continue;
}
```
— [ViiperOutDevice.cs:1623-1629](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)

The check runs **inside a read lock** on `feedbackDispatchGenerationBarrier`, and teardown drains
it by taking the write lock and immediately releasing
([ViiperOutDevice.cs:1561-1576](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)). That is
a genuine drain barrier: after `WaitForFeedbackDispatchCallbacks()` returns, no dispatch from the
retired generation is in flight or can start. `Disconnect()` bumps every generation *first*, then
drains, then disposes the stream — the correct order
([ViiperOutDevice.cs:1126-1171](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)).

> **Closed in 3.3.** The state writer now takes a read lease on its own
> `stateWriteGenerationBarrier` around the generation check *and* the write, and
> `Disconnect()` drains it via `WaitForStateWriteCallbacks()` before disposing the stream.
> Stream recovery stays outside the lease deliberately — it can dispose and rebuild the
> stream, and holding a read lease while `Disconnect` waits for the write lease would risk
> a deadlock rather than prevent one. Verdict is now **Present** on both paths. The gap as
> originally found is described below.

**Gap.** The state-writer path has the generation check
([ViiperOutDevice.cs:1904-1910](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)) but **no
drain barrier**. `Disconnect()` disposes the stream at line 1170 and only afterwards joins the
writer thread, with a bounded `Join(500)`. A writer that passes `IsStateWriterCurrent` and is
then descheduled can reach `WriteState` on a disposed stream, and a writer that does not exit
within 500 ms is simply left behind. In practice the generation check closes almost all of the
window and the throw is caught, so this is a robustness gap rather than an observed defect —
but it is the one place where the pattern applied to feedback was not applied to the writer.

**Worth doing:** give the state writer the same drain barrier the feedback path already has.
Small, self-contained, testable against fakes.

---

## (b) UNLINK/cancellation cannot strand an ordered completion

**Purpose.** Cancelling one request must not leave a later one stuck behind it, and a cancelled
request must not also complete.

**Verdict: Moved to VIIPER — and a real ownership gap is visible there.**

This invariant no longer has anything to bite on inside Thrum: Thrum does not implement USB/IP.
It now lives in `hbashton/VIIPER`'s Go server, which handles both halves explicitly.

**The stranding half is handled**, and deliberately so — the code says as much:

```go
// Cancellation must never strand a later URB behind this one.
defer func() { signalNext(time.Now()) }()
```
— `internal/server/usb/server.go:929-930`

**The exactly-once half has a hole.** The UNLINK handler claims ownership properly: it looks the
sequence up under `pendingMu`, deletes it, and only replies `-ECONNRESET` **if it was the one to
find it** (`status = 0` otherwise, meaning "already completed") — `server.go:805-828`.

The ISO-IN completion path does not make the symmetric check. Its early-exit paths delete and
return without writing (`server.go:935, 951, 958`), which is correct, but the success path does:

```go
pendingMu.Lock()
delete(pending, seq)          // return value ignored
pendingMu.Unlock()

if err := writeRet(seq, ...); err != nil {
```
— `server.go:967-971`

`delete` is unconditional and its result is discarded, and `writeRet` (`server.go:614`) applies
no ownership test — it serialises on `writeMu` and writes a `RET_SUBMIT` for whatever seqnum it
is handed. So if an UNLINK for that sequence arrives after the completion goroutine's last
context check (`server.go:955`) but before it takes the mutex at 967, the UNLINK finds the entry,
cancels, and replies `RET_UNLINK(-ECONNRESET)`; the completion goroutine then deletes nothing,
notices nothing, and also writes `RET_SUBMIT`. **Two replies for one seqnum.**

The fix is the same shape as the UNLINK handler's own logic — make the delete the ownership
claim:

```go
pendingMu.Lock()
_, owned := pending[seq]
delete(pending, seq)
pendingMu.Unlock()
if !owned { return }          // UNLINK already answered for this seqnum
```

**Caveats, stated because they matter.** This is a static reading; it has not been reproduced,
and the window is narrow. But the consequence is worth taking seriously: a client that receives
`RET_SUBMIT` for a sequence it has already unlinked is being handed a response for a request it
no longer owns — the same shape as the kernel-side defect class in
[usbip-win2#181](https://github.com/vadimgrn/usbip-win2/issues/181), and exactly what
`usbip-win2` PR #182's ownership arbitration exists to prevent on the other side of the wire.

**This is Phase 3.4's deliverable, surfaced early.**

> **Filed 2026-08-01 as [hbashton/VIIPER#7](https://github.com/hbashton/VIIPER/pull/7)** — a PR
> rather than an issue, because issues and discussions are both disabled on that repo. Verified
> against upstream `main` @ `308e9b2` first, which also widened the finding: the **generic** async
> IN completion at `server.go:1013-1018` has the identical shape, not just the ISO-IN path. The
> fix extracts the removal into a `claimPending` helper used by all three sites, with tests
> including 64 goroutines contending for one seqnum across 200 rounds under `-race`, and a
> negative control in which the pre-fix semantics report "64 winners, want exactly 1".

---

## (c) Prove exact-device absence before releasing the final protection

**Purpose.** Do not declare teardown finished on a probe that cannot distinguish "gone" from
"cannot tell". The old fork was explicit that a problem-24 phantom devnode is not absence.

**Verdict: N/A as written; Re-derived and largely Present in a different form.**

There is no final render protection to release, so the literal invariant has no subject. Its
purpose re-derives to: *do not treat the backend as idle unless the exact devices are provably
gone.* Thrum does exactly that, at the API level rather than the PnP level:

```csharp
if (census == null || !census.Succeeded)
{
    return ViiperBackendStopDecision.Leave(
        "could not confirm the backend is idle (" + ... + ")");
}
```
— [ViiperBackendLifecycle.cs:380-385](../../DS4Windows/DS4Control/Viiper/ViiperBackendLifecycle.cs)

"Cannot tell" is treated as "do not act" — the invariant's core requirement. It then refuses to
stop if any foreign device, any of **our own** leftover devices, or even an empty bus remains
(lines 390-426), with the reasoning recorded in the code:

> Our own leftovers block a stop just as hard. Killing the backend while one of our virtual
> devices is still attached is the exact ordering the teardown path exists to avoid.

> **Closed 2026-07-31.** The cross-check now exists. `CmTreePnpAbsenceProbe` finds every
> present devnode carrying the UDE controller's hardware ID (`ROOT\USBIP_WIN2\UDE` — all of
> them, not the first) and walks the CM tree beneath: root hubs are descended, and every
> non-hub devnode is reported as an attached device, problem code included — a problem-24
> phantom reads `USB\VID_054C&PID_0CE6\... (problem 24)`, which is exactly the state that
> must not pass for absence. Position, not identity: a VID/PID filter would both miss
> personas and match a real pad on a physical port; what makes a devnode usbip-attached is
> living under the emulated controller. `ViiperBackendStopPolicy.Decide` consults the probe
> only after every census gate has passed — a second opinion on the final idle verdict, not
> a first probe — and judges it fail-closed: devices present, unproven, a null result and a
> thrown exception all leave the backend running. One deliberate asymmetry with the census:
> a *missing controller* is proven absence (nothing can be attached through a controller
> that is not there), while a failed census stays a failed census — devices can be alive
> behind an API that will not answer, but not under a devnode that does not exist. Verdict
> is now **Present** at both levels. The gap as originally found is described below.

**Gap.** Absence is proven from VIIPER's census — VIIPER's own view of what it hosts — not from
Windows PnP. If the two disagree (a devnode Windows still shows after VIIPER has forgotten it —
precisely the phantom case the old fork's `present-only SetupAPI probe` was written for), Thrum
believes VIIPER. Thrum does have SetupAPI machinery available
([ViiperDriverInspectors.cs](../../DS4Windows/DS4Control/Viiper/Validation/ViiperDriverInspectors.cs)),
but it is used for driver-package enumeration, not for exact-device presence.

**Worth doing only if it can bite:** a PnP cross-check matters when a phantom can outlive the
census. Cheap to add as a second opinion before the final "idle" verdict; not urgent while
audio-class endpoints (the case that produced phantoms in the old fork) are off by default.

---

## (d) Parent/helper death retains at least one protection through teardown

**Purpose.** A hard-killed parent must not drop the last protection while the device is still
attached — the ordering that produced the 0x139 bugchecks.

**Verdict: N/A as written; the dangerous case is prevented by architecture.**

In the old fork this was load-bearing because DS4Windows *held* a protection the device depended
on: kill the parent and the render pin closed while the device was still attached. In Thrum the
device's lifetime belongs to VIIPER, a separate process. If Thrum dies — cleanly or not — the
backend keeps running and the device stays up. There is no protection to drop, and the ordering
that caused the crashes cannot occur from Thrum's side.

2.4b makes the intent explicit for the case where Thrum is merely *exiting*: a backend it did not
start is left alone, verified live in the Phase 2 VM pass — same PID, same start time, with
`VIIPER backend left running: the backend was already running before Thrum started, so it is not
ours to stop.`

> **Affordance shipped 2026-07-31.** `ViiperUnownedBackendPolicy` classifies the backend on
> the API port — managed by this session, unowned and idle, unowned but serving this
> session's own pads, unowned and holding devices this session cannot account for, or
> unreadable — and a **Backend process** card in the Settings VIIPER section renders the
> verdict with the holdings listed. The stop button appears exactly when the report offers
> it: unowned, holdings readable, and none of them this session's live controller. It asks
> before acting, and the confirmation names the one thing the card cannot know — leftovers
> of a dead session and another program's live controllers are indistinguishable from here,
> so it says what happens in the second case. The gate re-runs at commit time, so a stale
> card cannot stop a backend that has started serving this session; the process is
> identified by its socket (the owner of the API port's listener via `GetExtendedTcpTable`),
> never by executable name; and stopping it is the clean unplug path — the USB/IP peer
> disappears and the driver surprise-removes the devices, the same order VIIPER's own exit
> produces. Startup logs one warning line when an unowned backend is holding devices,
> pointing at the card. Deliberately still not a lifecycle change: nothing happens without
> a click, and the next session continues to leave unowned backends alone.

**Gap (low severity, real).** The inverse case has no handling: if Thrum dies hard while owning a
backend it started, the backend and any attached device survive with nothing to reclaim them. A
search for reconciliation or orphan-adoption logic finds none. The next Thrum start sees a
backend it did not start and correctly leaves it alone — so the user is left with a stale virtual
controller and no in-app way to clear it. The old fork's design called for startup reconciliation
here.

This is untidy, not unsafe: nothing is half-torn-down, and no protection has been released early.
Worth a Phase 4 diagnostics affordance ("a backend is running that Thrum does not own — devices:
…, [stop it]") more than a lifecycle change.

---

## (e) A timeout is not permission to kill the protection holder

**Purpose.** Killing on timeout is how the old fork's teardown became unsafe; a stop that cannot
complete must leave protections up and report incomplete cleanup instead.

**Verdict: Present, via a different mechanism — but read the ordering carefully.**

Thrum *does* kill: `ViiperBackendLifecycle` escalates from `CTRL_BREAK_EVENT` to
`process.Kill(entireProcessTree: true)` ([ViiperBackendLifecycle.cs:547](../../DS4Windows/DS4Control/Viiper/ViiperBackendLifecycle.cs)),
which looks like a direct violation. It is not, because of what has to be true before that line
is reachable: the stop **decision** (above, invariant c) refuses to stop at all unless the census
succeeded *and* reported zero foreign devices, zero of our own devices, and zero buses. By the
time a kill can happen, the backend is proven to be holding nothing.

So the invariant is satisfied by removing the subject rather than by protecting it: there is no
protection holder to kill, because a backend that still holds anything is never stopped. Census
failure is fail-closed (`Leave`), which is the same instinct as the old fork's "unproven ⇒ do not
release".

**Residual risk, worth stating.** The proof is a point-in-time census taken before the stop. A
device created between the census and the kill would be killed along with the backend. The window
is small and requires a second consumer creating a device at exactly that moment, but it is the
one place the guarantee is "checked recently" rather than "held".

---

## (f) Unproven removal blocks reuse

**Purpose.** Never create a second virtual device while the previous one's removal is unproven —
the old fork blocked a second attach when the exact virtual parent was present *or* the probe
could not prove absence.

**Verdict: Partial — Thrum cleans up rather than blocks.**

> **Closed in 3.3, and the sweep turned out to be fail-open in a second way.**
> `DetachStaleLocalViiperPorts` now returns a `ViiperStalePortSweep`, and the ladder entry
> `CreateDeviceStream()` refuses creation when it is not `Cleared`.
>
> The extra finding: if *every* `usbip port` query failed, the loop saw no ports, detached
> nothing, counted that as a clean snapshot, and reported a clean window — turning "could
> not look" into "looked and saw nothing". The sweep now counts only snapshots whose query
> actually succeeded, so an unobservable machine is `Unproven` rather than clean. That is
> the exact case the invariant names ("the probe cannot prove absence") and it was silently
> passing before.
>
> The check moved from `CreateDeviceAndOpenStream` to the ladder entry, because the former
> runs once per persona rung and the sweep can take seconds; it now runs once per creation
> attempt. `IOException` matches how the audio gate refuses a few lines above, so callers
> that already handle a refused creation handle this too. The verdict rules are extracted
> into `DecideStaleSweep` and covered by seven tests.

> **Revised 2026-07-31: the sweep was reading a heuristic as ownership, and it cost a user
> their controller.** `DetachStaleLocalViiperPorts` identified "ours" as *any* import from a
> localhost usbip URL carrying a known controller VID/PID and not registered by this
> process. That is also an exact description of a **different application's live virtual
> pad**. On the first evening two such applications ran side by side — Thrum under test and
> the maintainer's native-mode DS4Windows build serving a DualSense — Thrum's startup sweep
> detached the other one's controller mid-game.
>
> The mistaken premise was that a local import can be attributed from the port table at all.
> It cannot: the usbip link records the *serving* side, never the consumer, so a dead
> session's leftover and a live consumer's device are the same row. The sweep is now
> `ObserveLocalImports` and detaches nothing. It reads the table, names any local import
> this session does not manage in one log line pointing at the (d) backend-process card —
> which *can* attribute leftovers, via the backend census, and clear them with consent — and
> leaves them alone.
>
> **What this costs (f), stated plainly.** The invariant's own subject is unaffected: "our
> device's removal is unproven" stays impossible, because our removals are transactional
> in-session — the lifetime object that created a port detaches that exact port — and every
> new device gets a fresh bus from `bus/create`, so a foreign import is not a reuse hazard.
> 3.3's fail-closed rule is kept intact: an unreadable port list still refuses creation.
> What is genuinely given up is *automatic* cleanup of a leftover from a session that died
> hard. That is now a consented user action rather than a silent one, which is the same
> trade (d) makes, and for the same reason: this application cannot prove the thing is
> abandoned.
>
> Two narrower attribution bugs surfaced while proving the above, both able to detach
> somebody else's device on their own. `FindLocalViiperPort` matched our just-created device
> by bus id alone, and usbip bus ids are small integers every server counts from the bottom,
> so two local servers can both serve a `1-7`; it now refuses on ambiguity (`-1`, rolling
> the creation back) instead of adopting the first hit, and prefers the `usbipPort` the
> backend reports in the create response over scanning at all. `DetachDuplicateLocalViiperPorts`
> is now scoped to the `usbip://host:port/` prefix of the import we confirmed as ours, so a
> same-bus-id device on a *different* local server is out of reach. Sixteen tests.

`CreateDeviceAndOpenStream` opens with:

```csharp
ViiperUsbipPortManager.DetachStaleLocalViiperPorts();
```
— [ViiperOutDevice.cs:4748](../../DS4Windows/DS4Control/Viiper/ViiperOutDevice.cs)

followed, after creation, by `DetachDuplicateLocalViiperPorts(...)` (line 4764). Creation failure
rolls back properly — port unregistered, device removed, bus removed (lines 4767-4778).

That is *clean up and proceed*, not *block until proven*. The distinction matters in the case the
old fork guarded: if the stale port cannot be detached, or if the probe that enumerates local
ports cannot answer, the code has no "refuse to create" branch — it continues to `bus/create`.

Mitigating context: Phase 2.3's `ViiperVirtualDeviceGate` sits in front of all of this and blocks
device creation outright unless the driver gate is satisfied and consent is present, which is a
much coarser but very effective gate — nothing gets created on an unhealthy machine at all. The
Phase 2 VM pass exercised that path repeatedly.

**Worth doing:** make an un-detachable stale port a refusal rather than a warning. Small change,
directly in the invariant's spirit, and testable through the existing port-manager seam.

---

## Summary

| invariant | verdict | action |
|---|---|---|
| (a) retired generation emits no late success | **Present** — both paths, since 3.3 | done |
| (b) UNLINK cannot strand or double-complete | **Moved to VIIPER**; stranding handled, **ownership gap found** | **Report upstream** — this is Phase 3.4's finding, arrived early |
| (c) prove exact-device absence | **Present** at both levels — census since 3.1, PnP cross-check since 2026-07-31; fail-closed | done |
| (d) parent death retains a protection | **N/A** — architecture prevents the dangerous case | done — unowned-backend diagnostics card + consented stop, 2026-07-31 |
| (e) timeout ≠ permission to kill | **Present** via the census gate | None; document the check-then-kill window |
| (f) unproven removal blocks reuse | **Present** — since 3.3; unreadable port list still refuses. Revised 2026-07-31: the sweep no longer detaches imports it cannot attribute, after it disconnected another application's live pad | done |

**The headline for Phase 3 planning: there is far less to port than the plan assumed.** The plan
budgeted 3–6 sessions on the assumption that a large body of old-fork containment work would need
adapting. Most of it turns out to be either already present in a different form (a, c, e) or made
moot by the architecture (d) — because the risky component, the in-process USB/IP helper holding
a render lease on a virtual audio endpoint, does not exist in Thrum, and the audio endpoints that
made it necessary are off by default.

**Status after 3.3:** (a) and (f) are closed by the two small ports this diff scoped, both
tested. (c) and (e) were already satisfied in re-derived form, (d) is moot by architecture, and
what is left is one optional hardening (c's PnP cross-check), one Phase 4 UI affordance (d), and
**the upstream finding (b)** — filed as
[hbashton/VIIPER#7](https://github.com/hbashton/VIIPER/pull/7) with the fix, tests and a negative
control.

**Status after the 2026-07-31 follow-up pass:** the two remaining local items are done — (c)'s
PnP cross-check (second opinion on the final idle verdict, fail-closed) and (d)'s diagnostics
affordance (the Settings backend-process card with its consented stop). Every invariant in this
document is now either **Present**, prevented by architecture with its residue surfaced to the
user, or **filed upstream**. What deliberately remains open: (e)'s check-then-kill window
(documented, not closable from the client side) and the (b) fix riding upstream review.

Task 3.2 (default-audio-endpoint takeover guard) is unaffected by this diff and needed its own
[VM] verification — Thrum has no equivalent of `NativeModeAudioDefaultGuard`, and whether it
needs one was a measurement, not a code read.

**That measurement has since been run**; see
[audio-default-endpoint-measurement.md](audio-default-endpoint-measurement.md). Summary: on
attach, Windows promotes the virtual DualSense to **all six** default slots — render *and*
capture, across Console, Multimedia and Communications — and does so again on every re-attach,
with a stable endpoint identity and clean release on detach. The guard is therefore justified and
scoped to audio-class consent, but is deliberately **not ported yet**: the displacement case (the
pad taking over from an endpoint already in use) cannot be measured in a Gen 2 VM, which has no
sound card of its own, and that is the specific behaviour the guard exists to prevent.

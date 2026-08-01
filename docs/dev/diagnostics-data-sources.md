# Diagnostics data sources

Phase 4, task 4.3. What fills each section of `ThrumDiagnosticsSnapshot`, and the constraints
that make the obvious wiring wrong.

`ThrumDiagnosticsCollector` takes six `Func<T>` delegates. This document records where each
one's data actually comes from, because the answer was not obvious for any of them and three
of the six have a way to read them that looks right and is not.

Mapped against `main` @ `c984e2c`.

## The rule every source obeys

**Collecting a report is a read.** No source may start or stop the backend, refresh the driver
gate, attach or detach a device, register ownership, or write a setting. This is not tidiness:
a diagnostics page is what a user opens when something is already wrong, and a "diagnostic"
that changes the state it is reporting on destroys the evidence.

The second rule is the PII containment in `ThrumDiagnosticsSnapshot`'s type comment, which the
formatter tests enforce.

## Summary

| section | reachable | cost | notes |
|---|---|---|---|
| driver gate | yes | free warm, seconds cold | session cache; never `Refresh()` |
| VIIPER backend | yes | **blocking TCP, ~6 s worst case** | must not claim ownership |
| HidHide | yes | SetupAPI on first touch | whitelist must not escape the delegate |
| audio endpoints | yes | **hundreds of ms per endpoint** | COM, per-slot guards required |
| output slots | yes | microseconds | MAC hazard on the adjacent property |
| link health | **needs one accessor** | microseconds | counters are private |

## Collection must not run on the UI thread

Three of the six can block for seconds, and two of those are the ones most likely to be slow
exactly when the user is diagnosing a problem.

- **Audio.** Every `MMDevice.FriendlyName` read round-trips to the audio driver's property
  store. `AudioEndpointChoiceCache.cs:24-30` already records the incident: *"Some audio drivers
  take hundreds of milliseconds to answer an individual property query, which made the whole
  window appear hung."* Six default slots plus one read per active render endpoint.
- **Backend.** One ping plus a census: `1 + (1 + B)` blocking loopback round trips for `B`
  registered buses, at 1000–1500 ms of timeout each. A backend that accepts the connection and
  then stalls costs roughly `(2 + 3*(1+B))` seconds. The common cases are fast — nothing
  listening gives an immediate RST, a healthy backend answers in single-digit milliseconds —
  but the slow path is reachable and is exactly the "something is wrong" case.
- **Driver gate, cold only.** The session cache makes the warm path a single volatile read, but
  the first `Get()` in the process runs a SetupAPI enumeration plus up to three
  `WinVerifyTrust` calls, and a concurrent caller blocks on `evaluationLock` until it finishes.
  `MainWindow`'s constructor normally warms it on a background task, so this only bites a user
  who opens diagnostics during startup.

`MainWindow.xaml.cs:2055` is the existing precedent for running this kind of work on a
background task and marshalling only the result back.

## Per source

### Driver gate → `DiagnosticsDriverSection`

Read `ViiperSetupManager.DriverReadiness`, which forwards to the provider's session cache. On
the warm path it is one `Volatile.Read`. It never returns null — a failed evaluation returns an
`Unavailable` readiness rather than throwing.

**Never call `RefreshDriverReadiness()` or `ViiperDriverStatusViewModel.Recheck()`.** Those
discard the cache and re-run the whole sweep, which is both expensive and a mutation of shared
session state.

For the badge string, construct a **throwaway** `ViiperDriverStatusViewModel` rather than
reusing `SettingsViewModel.ViiperDriverStatus`. The live one fails two ways: mid-recheck its
`BadgeText` is `"Checking"` and would badge a perfectly good state as unknown, and calling
`Apply()` on it raises `PropertyChanged` into live WPF bindings from whatever thread the
collector is on. The view-model has no dispatcher affinity, so a throwaway instance is safe off
the UI thread — the unit tests already construct it that way.

`Identities` was verified path-free: the projection reads the catalog **file name**, never
`ViiperDriverPackageInfo.TrustEvaluationPath` (the driver-store path). `Reasons` is the one
channel that can carry a user path, because it embeds raw exception messages and the usbip.exe
location is resolved from `%PATH%`.

Two semantic traps worth not papering over: `EvaluatedAtUtc` is the *cached* evaluation's
timestamp and can be much older than the report's own — the formatter deliberately prints them
as separate fields. And a `Missing` verdict clears `Reasons`, so an empty list there is proven
absence, not a failed read; a read failure fails closed to `DetectedUnvalidated` instead.

### VIIPER backend → `DiagnosticsBackendSection`

**Pass `tryStartServer: false` explicitly, and say why in a comment.** It is the default, but
**9 of the 11 call sites in the tree pass `true`**, and `true` is the only route to
`TryStartServer` → `RecordOwnership` — the app claiming ownership of a backend it just
launched. Anyone copying the nearest example gets the wrong one.

`Holdings` must be built from `.Count` and `.Type` only. `DevId` is a per-device identifier and
it leaks through three members that all look like the convenient choice:
`ViiperUnownedBackendReport.DescribeHoldings()`, `ViiperCensusDevice.ToString()`, and
`ViiperBackendStatusViewModel.HoldingLines`. Any string concatenation with a census device
silently calls the leaking `ToString`.

`Holdings` is also unavoidably empty for `NoBackend` **and** for `ManagedByThisApp`, because the
census is skipped when the backend is ours. Rendering both as an empty list makes "not
enumerated" read as "hosts nothing" — the exact conflation the snapshot's contract forbids. Say
which it is.

**There is no running version, confirmed.** The ping response body is discarded after a
substring test; no `--version` call and no `FileVersionInfo` read against `viiper.exe` exists
anywhere. `PinnedVersion` is a compile-time constant, and the formatter's "expected version" /
"running version: not reported by the backend" split is what keeps that honest. The tree also
has no awareness that anything past v0.0.5 exists, so the section cannot claim an upgrade is
available.

### HidHide → `DiagnosticsHidHideSection`

The section carries three fields and deliberately no list. The whitelist is every cloaked
application's full path on the machine — the account name plus, effectively, the user's
installed-game inventory. If the only available API returns the whole list, membership must be
tested and the list discarded **inside the delegate**, so it never reaches the snapshot.

`ReadFailure` exists so an unreadable whitelist is distinguishable from an empty one.

### Audio endpoints → `DiagnosticsAudioSection`

**Guard every default slot with `HasDefaultAudioEndpoint(flow, role)` before reading it.**
`GetDefaultAudioEndpoint` throws `COMException` when a flow has no default device, and this is
a real case, not a theoretical one — the Phase 3.2 guest had zero endpoints and all six slots
empty. One unguarded empty flow costs the whole section.

Wrap `FriendlyName` per slot rather than per section, so one endpoint disappearing mid-read
does not lose the other five. Dispose every `MMDevice` on the thread that created it: they have
finalizers, and an undisposed one is released from the finalizer thread in a different
apartment.

Three mutations to avoid: `RegisterEndpointNotificationCallback` (subscribes process-global
audio state), `AudioEndpointChoiceCache.RefreshAsync()` (writes static fields and spawns a
task), and reading the consent flag through `ViiperVirtualDeviceGuard.Decide(...)` (which also
reads the driver gate, dragging it into the audio section). Reading the stale endpoint cache
instead of refreshing it is *worse* than either: it starts empty and is only populated by the
profile editor, so a cold diagnostics click would report `ControllerRenderEndpointPresent=false`
— "could not look" rendering as "looked and saw nothing".

Endpoint **IDs**, `InstanceId`, `PKEY_Device_InterfaceKey` and `PKEY_Device_ControllerDeviceId`
must never reach the snapshot; they are the stable per-machine correlators. Friendly names may,
and the snapshot's comment accepts that they are user-renameable and can carry a person's name.

`ControllerRenderEndpointPresent` cannot distinguish a VIIPER-created endpoint from a
physically connected DualSense — the names are identical, per
`audio-default-endpoint-measurement.md`. It is honest as named; the UI must not relabel it
"virtual endpoint present".

### Output slots → `IReadOnlyList<DiagnosticsSlotRow>`

**Do not call `OutputSlotManager.GetOutSlotDevice()`.** It performs an unlocked
`Dictionary.TryGetValue` while the deferred plug/removal paths mutate that dictionary under a
write lock. A concurrent read across a resize can throw, or spin in a corrupted bucket chain —
an infinite loop that the collector's `catch` cannot rescue. Scan `ControlService.outputDevices`
by `ReferenceEquals` instead; that reads only array slots and reference identity.

**Do not construct `CurrentOutDeviceViewModel` or `SlotDeviceEntry`** to reuse their formatting.
Both subscribe to events in their constructors, which mutates the manager's and the slots'
event lists and leaks the handlers for the process lifetime. Duplicate the handful of lines.

Only `DS4Device.DisplayName` may reach the snapshot. `OutSlotDevice.InputDisplayString`,
`SlotDeviceEntry.InputSlotDisplayString` and `SlotDeviceEntry.ToString()` all carry the
controller's Bluetooth MAC — none may be read, not even to strip the bracketed part.
`DisplayName` itself was verified safe: it comes from a hard-coded product-name table and has no
user-rename path, so it can never carry a person's name.

`OutSlotDevice.Index` is 0-based but every log line and every other UI surface renders
`Index + 1`. The formatter prints the row's `Index` raw, so pass `Index + 1` or the report will
disagree with everything else the user can see.

### Link health → `IReadOnlyList<DiagnosticsLinkHealthRow>`

**This is the one source that needs new code.** The counters live on
`ViiperFeedbackDispatchBuffer`, reachable only through `ViiperOutDevice.feedbackDispatchBuffer`
— a private field with no accessor. One read-only property is required.

The counters are read through `Interlocked`, so each is individually tear-free, but the set can
be mutually skewed by a few frames. **Do not "fix" that by taking `syncRoot`** — that is the
real-time audio path's lock, held across a per-frame buffer copy at speaker frame rate. The skew
is cosmetically irrelevant in a report. The backing fields are plain `long`, not `volatile`;
correctness comes entirely from every access going through `Interlocked`.

Three things a reader will misinterpret unless the UI labels them:

- The counters are **per virtual output device, not per physical controller**. Frames carry a
  device index tag but the counters are buffer-wide aggregates. Do not label a row with a
  physical controller's identity.
- **`SpeakerDropped` already includes `SpeakerExpired`** — the expiry path increments both.
  Presenting them as independent totals double-counts. `SpeakerExpired` is also structurally
  always 0 for DualShock 4, where expiry is disabled; 0 there means "not applicable", not
  "healthy".
- **`SpeakerHighWater` is a queue depth**, not a cumulative count, and saturates at capacity
  (8 for DualSense, 16 for DualShock 4). Comparing it to `SpeakerEnqueued` is meaningless.

Two known gaps, recorded rather than fixed: audio-only sidecars are separate `ViiperOutDevice`
instances that are **not** members of `OutputSlots`, so a slots-only implementation reports
`SpeakerEnqueued = 0` for a Bluetooth DualSense — precisely the configuration this page exists
to diagnose. And `DiagnosticsLinkHealthRow` has no fields for the ordered-control (native
haptics) ring, so dropped haptics frames are currently invisible.

## Provenance

Mapped 2026-08-01 by six parallel agents, one per source, each followed by a verification pass
that re-opened every cited file to confirm the symbol exists, is accessible, and does what the
mapping claimed — which is how several of the traps above were found. The full per-source
output, including exact symbols and line numbers, is kept outside the repo at
`vm-validation-reports/diagnostics-source-map-20260801/`.

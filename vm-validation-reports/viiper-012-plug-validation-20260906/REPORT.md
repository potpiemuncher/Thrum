# VIIPER v0.1.2 plug validation — 2026-09-06

The pin moved to VIIPER v0.1.2 in PR #83 with static checks and installer-path
tests only. This is the runtime pass the upgrade-path doc asks for: **one
virtual device of every output type Thrum supports, plugged and unplugged
against v0.1.2, with no hardware.**

## Result

| Output type | Result | Device type VIIPER registered | VID:PID | Evidence |
| --- | --- | --- | --- | --- |
| `ViiperX360` | **PASS** | `xbox360` | `045e:028e` | `plug-01-ViiperX360.txt` |
| `ViiperDS4` | **PASS** | `dualshock4`, stream `microphoneInput=true speakerOutput=true frameVersion=3` | `054c:05c4` | `plug-02-ViiperDS4.txt`, `plug-02b-ViiperDS4-persona-recheck.txt` |
| `ViiperDualSense` | **PASS** | `dualsensecombinedaudioduplexv5`, `frameVersion=5`, first attempt, no fallback | `054c:0ce6` | `plug-03-ViiperDualSense.txt` |
| `ViiperDualSenseEdge` | **PASS** | `dualsenseedgecombinedaudioduplexv5`, `frameVersion=5`, first attempt, no fallback | `054c:0df2` | `plug-04-ViiperDualSenseEdge.txt` |
| `ViiperSwitch2Pro` | **PASS** | `ns2pro` | `057e:2069` | `plug-05-ViiperSwitch2Pro.txt` |

Every run also produced a **clean unplug**: no VIIPER buses left
(`bus/list -> {"buses":[]}`), no usbip import, no present PnP device, and both
the backend process and the two usbip-win2 services still running.

Installer path, lifecycle and frame-type findings are below. **No Thrum defect
was found; nothing was changed in the application for this pass.**

## Environment

| Item | Value |
| --- | --- |
| VM | `Win 11 25H2 Test ENV` (TESTENV), Windows 10.0.26200, Secure Boot on, no network, no physical controller |
| From checkpoint | `viiper-006-installer-validated-20260803` |
| Driver | usbip-win2 **0.9.7.7** (UDE 21.14.27.907, filter 21.14.27.661) unchanged throughout — the pin was not touched |
| App | `main` @ `d14018c`, self-contained win-x64 publish; digests in `build-info.txt` |
| Backend | VIIPER v0.1.2, installed by Thrum's own setup from the digest-verified staged zip |
| Checkpoints | `viiper-012-baseline-20260906` then `viiper-012-five-types-plugged-20260906` then `viiper-012-validated-20260906` then `viiper-012-validated-endstate-20260906` |

Nothing virtual was ever plugged on the host. All plugging happened in the guest.

## Method

`ControlService.AssignInitialDevices()` plugs any output slot marked permanent
with `InputIndex = -1`, so a single `%APPDATA%\Thrum\OutputSlots.xml` is enough
to create a virtual device on a guest with nothing attached:

```xml
<OutputSlots app_version="0.9.0.0">
  <Slot idx="0"><DeviceType>ViiperX360</DeviceType></Slot>
</OutputSlots>
```

Each run: write the slot, start Thrum, wait for the device, record the VIIPER
census, `usbip port` and the PnP tree, wait 30 s and census again, then close
Thrum's shell window through UI Automation (a real shutdown, not a kill) and
record the teardown.

The census is taken **independently of Thrum**, by a host-driven script that
speaks VIIPER's own local API. That API is a NUL-terminated line protocol on
TCP 127.0.0.1:3242, **not HTTP** — `Invoke-WebRequest` against it simply times
out, which is worth recording because it is how one earlier attempt mistook a
healthy backend for a dead one.

## Installer path (Install / Repair to v0.1.2)

Thrum's own `extras/install-viiper-backend.ps1` was run in the guest against the
staged archive (`guest-02-installer-console.txt`, `guest-05-viiper-install.log`).
Everything the upgrade-path doc promises was observed:

- archive size **4809388 = expected** and SHA-256 **66A9BBD4...4A46 = expected**;
- payload `viiper.exe` size **11407872 = expected** and SHA-256 **2EB92FF3...FB6A = expected**;
- `viiper.exe` and `licenses.txt` installed to `%LOCALAPPDATA%\VIIPER`, previous
  backend kept as `viiper.exe.previous` (`guest-03-post-install-state.txt`);
- the installed binary reports `Version: v0.1.2 (f5d097b)`;
- **no autostart** created — no `RunVIIPER` Run value, no scheduled task;
- backend started as `viiper.exe --update-notify none server`;
- the driver pair re-validated: **PASS**, 0 observed mismatches, matched release
  0.9.7.7 / tier ExperimentalBaseline (`guest-06-driver-diagnostic-report.txt`);
- exit code 0, "Setup complete. VIIPER is ready for Thrum."

### Negative control — tampered archive

A copy of the zip with **one byte flipped** at offset 2000000 (`0x65` to `0x64`)
and its size left identical was fed to the same setup run
(`guest-04-negative-control-tampered-archive.txt`). Setup refused, named the
mismatch, and changed nothing:

```
Size: expected 4809388 bytes, actual 4809388 bytes.
SHA-256: expected 66A9BBD4...4A46, actual CF09E4BE...3969.
Decision: Verification failed: viiper-windows-amd64.zip does not have the
pinned SHA-256. The file is discarded and nothing is run from it.
SETUP_EXIT=1
installed viiper.exe AFTER  = 2EB92FF3...FB6A   (unchanged = True)
```

The size line matching while the digest line fails is the point: the refusal is
the digest check firing, not a truncation heuristic.

### A second negative control, found by accident

The **first** `ViiperX360` attempt failed, and it failed correctly. The guest
had never acknowledged the experimental-driver disclosure, so the gate refused
before any device was created:

```
WARN|Refused to create the virtual Xbox 360 output. Virtual controllers run on
a third-party kernel driver that is still experimental, and that has not been
acknowledged yet.
```

`ViiperExperimentalAcknowledged` and `AllowExperimentalAudioEndpoints` were then
set to `True` in the guest's settings — the same two switches a user turns on in
Settings — and the run passed. That is a live demonstration that the plug path
can fail and that these measurements would have shown it.

## What each plug run showed

`ViiperDualSense`, as the representative case:

```
bus/1/list -> {"devices":[{"busId":1,"devId":"1","vid":"0x054c","pid":"0x0ce6",
               "type":"dualsensecombinedaudioduplexv5", ...}]}

Port 01: device in use at High Speed(480Mbps)
         Sony Corp. : DualSense wireless controller (PS5) (054c:0ce6)
           -> usbip://localhost:3241/1-1

OK | MEDIA    | DualSense Wireless Controller | USB\VID_054C&PID_0CE6&MI_00\...
OK | HIDClass | USB Input Device              | USB\VID_054C&PID_0CE6&MI_03\...
OK | USB      | USB Composite Device          | USB\VID_054C&PID_0CE6\...
```

VIIPER's own log for the same moment, with no fallback line before it and no
`DualSense requires V5 stream version` anywhere in the run:

```
msg="DualSense device instantiated" edge=false vid=1356 pid=3302 interfaces=6
msg="DualSense V5 stream configured" microphoneInput=true speakerOutput=true
    microphoneInterfaceEvents=false physicalInputMetadata=false frameVersion=5
```

**The stream was alive, not merely present.** Two censuses 30 s apart show
`inputStatesSelected` rising 977 to 31104 while `inputStatesReceived` stays 1 —
the backend is presenting state to Windows continuously from a single received
state, which is what a virtual pad with no physical input should do. The same
30-second liveness check passed for all five types.

The Edge is the identical shape one persona up:
`dualsenseedgecombinedaudioduplexv5`, `054c:0df2`, `edge=true`, `frameVersion=5`.

`ViiperDS4` deserves a note. The census reports its `type` as plain
`dualshock4` for every DS4 persona, so the census alone looks like a HID-only
fallback. The server log shows it is not: Thrum negotiated the audio duplex
rung — `DualShock 4 input stream configured ... microphoneInput=true
speakerOutput=true frameVersion=3` — with no fallback line. That re-check is
`plug-02b-ViiperDS4-persona-recheck.txt`. The census `type` field is a family
name for DS4 and a persona name for DualSense, and reading it as the latter for
both would have produced a wrong report.

## Frame-type tolerance (0x84) — partly answered

`frametype-01-realtime-haptics-probe.txt`,
`frametype-02-audio-teardown-and-crash-check.txt`.

Real speaker traffic was driven through the virtual DualSense: Windows promoted
the pad's USB audio endpoint to default (as the earlier native-mode measurement
predicted), three system WAVs were played, and the backend recorded

```
speakerPayloadsReceived=1250  speakerBytesReceived=2400000
speakerPayloadsDropped=0      speakerWriteFailures=0
```

Thrum stayed connected throughout — `inputStatesSelected` continued rising
30264 to 47982 across the audio — and the server log for the whole run, at
`--log.level trace`, contains **zero** `level=ERROR` and **zero** `level=WARN`
lines.

**What could not be established:** whether any of those frames carried type
`0x84`. VIIPER v0.1.2 never names a frame type in its log at any level — a
search of the full 2502-line trace log for `0x84`, `0x83`, `realtime` and
`frameType` returns 0 matches. So the server log cannot answer the question it
was asked. What *is* established is the property the question protects: with
2.4 MB of real speaker traffic flowing over the v0.1.2 V5 stream, Thrum neither
errored nor disconnected. Thrum's reader also drops any frame type other than
`0x81`/`0x82`/`0x83` without error by construction (`ViiperOutDevice.cs`, the
`activeStreamSupportsDirectSpeaker` read loop), so a `0x84` frame cannot break
it — but that is a code reading, not a measurement.

Deciding it by measurement needs a frame-counting client of our own, or an
upstream log line naming the type. Neither was in scope here.

## Lifecycle

| Invariant | Result | Evidence |
| --- | --- | --- |
| Thrum exit with no consumers stops the backend it started | **PASS** — viiper processes 0 to 1 to 0 | `lifecycle-01-owned-backend-stops.txt` |
| Thrum exit with a foreign device present does not | **PASS** — viiper process still 1 after exit | `lifecycle-02-foreign-device-keeps-backend.txt` |

Both decisions are stated by the app in its own log, which is what makes the
pair a control rather than two coincidences — same code path, opposite census,
opposite answer:

```
VIIPER backend stop (pid 9460 ...): we started it, it is hosting no buses or
devices, and Windows shows no device attached through the usbip-win2
controller - console break accepted; backend exited on its own.

VIIPER backend left running: another consumer is using it - 1 virtual
device(s) Thrum did not create are still registered (bus 2 device 1 (xbox360)).
```

The foreign device was a real one: created on its own bus through VIIPER's API
by a separate process that held its stream open, exactly as a second consumer
would. Thrum also reported the foreign usbip import and left it alone.

## No crash, and the audio teardown path specifically

The audio path is the one carrying the confirmed usbip-win2 request-lifetime
defect, so it was checked explicitly after the speaker traffic above. After a
graceful Thrum exit: no buses, no usbip import, no `VID_054C` PnP device,
backend and both services still running, and

```
minidumps=0   memory_dmp=False   bugcheck_events=0
uptime_boot=09/06/2026 16:31:58   (unchanged - the guest never restarted)
```

One clean teardown is not a refutation of that defect; it is one observation
that this run did not hit it.

## What this does **not** prove

The guest had no physical controller and none can be passed through
(`usb-controller-map-20260808.md`). So this pass says nothing about:

- **physical input** — no buttons, sticks or gyro reached any virtual pad;
- **motor feedback** — no rumble, adaptive trigger or lightbar output was
  observed on real hardware, only accepted by the backend;
- **audio payload correctness** — 2400000 bytes of speaker PCM were carried and
  written without loss, but nothing verified that what came out was the
  waveform that went in, and no microphone capture was exercised
  (`microphoneInterfaceActive` stayed false in every run);
- **multiple simultaneous virtual pads**, and **long-duration stability** beyond
  the 30-second and roughly two-minute windows measured here.

Those remain a maintainer hardware pass.

## Harness note for the next pass

Thrum shows an "Update Available!" window at startup even on a guest with no
network, and that window owns the process's `MainWindowHandle`. So
`Process.CloseMainWindow()` closes the popup and leaves the app running, and a
UI Automation close that matches the title loosely closes the wrong window. The
shell window has to be matched by exact name `Thrum`. Getting this wrong costs a
forced kill, which skips `rootHub.Stop` and leaves an empty VIIPER bus behind —
which then looks exactly like an unplug defect and is not one.

## Files

| File | What it holds |
| --- | --- |
| `build-info.txt` | commit, publish digests, staged backend digests, guest facts |
| `guest-00-initial-state.txt` | baseline guest state before anything was staged |
| `guest-01-staged-digests.txt` | digests of everything copied into the guest |
| `guest-02-installer-console.txt` | the setup run that installed v0.1.2 |
| `guest-03-post-install-state.txt` | installed files, version stamp, backend command line, autostart absence |
| `guest-04-negative-control-tampered-archive.txt` | the one-byte tamper and its refusal |
| `guest-05-viiper-install.log` | VIIPER install log from the guest |
| `guest-06-driver-diagnostic-report.txt` | post-install driver package validation |
| `plug-01` to `plug-05` files | one per output type: censuses, `usbip port`, PnP, Thrum log, VIIPER server slice |
| `plug-02b-ViiperDS4-persona-recheck.txt` | the DS4 persona question, settled from the server log |
| `frametype-01` and `frametype-02` files | the speaker-traffic probe and the crash check after it |
| `lifecycle-01` and `lifecycle-02` files | backend stop-on-exit, both directions |

Full VIIPER server logs stay in the guest at `C:\p2\v012\logs\` — a debug-level
DualSense run is far larger than this directory should carry; the slice that
decides each question is quoted in the per-run file.

# VM installer and plug validation of the 0.9.0-beta.2 pins — 2026-09-19

**Pins under test:** usbip-win2 **0.9.8.0** and the project's VIIPER fork build
`thrum-v0.1.2-usbip0980.1` (upstream v0.1.2 plus the 0.9.8.0 version gate and the
1120-byte `plugin_hardware` attach layout). Digests, build commits and guest
facts are in `build-info.txt` (first pass, build `1eec3b1`) and
`B2-build-info.txt` (re-run, build `35c69e8` = `beta2-repin` @ `6388b85` merged
with this evidence branch).

**Guest:** `Win 11 25H2 Test ENV`, Windows 10.0.26200, Secure Boot on, Memory
Integrity running, test signing off, network adapter disconnected for every
scenario, no physical controller. Every time below is guest time.

## Result

| Scenario | Result |
| --- | --- |
| **A** — clean machine, no usbip-win2 (the ordinary first install) | **PASS** |
| **B** — upgrade over 0.9.7.7 + upstream VIIPER v0.1.2, first pass, build `1eec3b1` | **FAIL** — the upgrade hung; a restart during the hang bugchecked the guest (0x9F). Led to the fix below. |
| **B2a** — same condition, build with the fix: setup must refuse and change nothing | **PASS** |
| **B2b** — upgrade straight after a Windows restart, then plug | **PASS** |

## Scenario A — clean machine (from `phase2-nousbip-baseline-20260730`)

| Step | Result | Evidence |
| --- | --- | --- |
| usbip decision `InstallPinned`; size, SHA-256, Authenticode chain and signer verified; installer exit 0, no restart requested | PASS | `A-guest-02-installer-console.txt`, `A-guest-05-viiper-install-log.txt` |
| Installed identities: UDE `23.56.48.757`, filter `23.56.30.686`, `usbip.exe` `0.9.8.0`, both packages WHCP-signed; driver diagnostic 0 mismatches, again after a restart | PASS | `A-guest-03-post-install-state.txt`, `A-guest-06-driver-diagnostic-report.txt`, `A-guest-12-driver-diagnostic-after-restart.txt` |
| VIIPER: archive and payload digests checked, `viiper.exe` + `licenses.txt` placed, no autostart, backend logs "Auto-attach prerequisites satisfied" | PASS | `A-guest-05-viiper-install-log.txt`, `A-guest-08-viiper-startup-log.txt` |
| Re-run is idempotent (`AlreadyPinned`, exit 0) | PASS | `A-guest-07-installer-rerun-console.txt` |
| Negative control: one byte flipped in a copy of the zip, size unchanged → refused naming the digest mismatch, installed binary untouched | PASS | `A-guest-04-negative-control-tampered-archive.txt` |
| Install / Repair after eight attach cycles: exit 0 | PASS | `A-guest-09-installer-after-use-console.txt` |

Plugs (permanent output slot, no hardware). For every run: attached on the first
attempt with no fallback line; debug server log 0 `ERROR` / 0 `WARN`; `usbip port`
showed the import; two censuses 30 s apart showed the device; after a graceful
Thrum exit no bus, no import, no present PnP device; Thrum stopped the backend
it had started.

| Output type | Virtual audio endpoints | Negotiated | Result | Evidence |
| --- | --- | --- | --- | --- |
| `ViiperX360` | off | `xbox360` | PASS | `A-plug-01-ViiperX360.txt` |
| `ViiperDS4` | off | `dualshock4` | PASS (see finding 3) | `A-plug-02-ViiperDS4.txt`, `A-plug-02b-…-endpoint-recheck.txt` |
| `ViiperDualSense` | off | `dualsensegamepadv5` | PASS | `A-plug-03-ViiperDualSense.txt` |
| `ViiperDualSenseEdge` | off | `dualsenseedgegamepadv5` | PASS | `A-plug-04-ViiperDualSenseEdge.txt` |
| `ViiperSwitch2Pro` | off | `ns2pro` | PASS | `A-plug-05-ViiperSwitch2Pro.txt` |
| `ViiperDualSense` | on | `dualsensecombinedaudioduplexv5` | PASS | `A-plug-06-…-audio-on.txt`, `A-plug-09-…-after-restart.txt` |
| `ViiperDualSenseEdge` | on | `dualsenseedgecombinedaudioduplexv5` | PASS | `A-plug-07-…-audio-on.txt` |
| `ViiperDS4` | on | `dualshock4` + duplex v3 stream | PASS | `A-plug-08-ViiperDS4-audio-on.txt` |

usbip-win2 PR #188 watch (unload hang after an attach, fixed upstream, not in
0.9.8.0): guest restart after use was down within about 15 s, final shutdown
4 s. Not reproduced by anything Thrum's flow does.

## Scenario B, first pass — the failure (from `viiper-012-validated-endstate-20260906`)

1. A virtual DS4 was plugged and unplugged on the old stack before upgrading, so
   0.9.7.7 had attached a device in the boot session in which it was then asked
   to unload. (`B-plug-00-…-PRE-UPGRADE-0977-upstream012.txt`)
2. Negative control, PASS: on 0.9.7.7 the fork backend exits 1 with "VIIPER
   requires usbip-win2 0.9.8.0 … (found 0.9.7.7)" while the upstream backend
   runs — the reason the upgrade action exists.
   (`B-guest-02-negative-control-new-backend-on-0977.txt`)
3. Setup returned `UpgradeRecognisedToPinned`; the installer verified and
   launched 0.9.7.7's uninstaller, whose
   `devnode.exe remove ROOT\USBIP_WIN2\UDE root` **never returned**:
   `DiUninstallDevice` opened in `setupapi.dev.log` and never closed, one thread
   in wait reason `Executive`, still blocked twelve minutes later, with a stuck
   "USBip Uninstall" window on screen. (`B-guest-03-installer-console.txt`,
   `B-guest-04a/04b-upgrade-hang-*.txt`, `B-screen-setup1-hang-b.png`)
4. A graceful guest restart requested while that removal was pending ended,
   about six minutes later, at a `DRIVER_POWER_STATE_FAILURE (0x9F)` stop
   screen. **The restart turned the hang into a bugcheck; setup did not crash
   the guest by itself.** No minidump, no `MEMORY.DMP`, no 1001 event; the
   screenshot is the only record. (`B-screen-shutdown-hang.png`,
   `B-guest-05-crash-record.txt`)
5. Non-destructive: after the reboot the guest was on an intact 0.9.7.7 with the
   upstream backend untouched.

**Fix (`beta2-repin` `6388b85`):** `UpgradeRecognisedToPinned` is only returned
on a positive observation that no virtual device was attached this Windows
session; otherwise `RestartBeforeUpgrade` — change nothing, exit 4, ask for a
restart and Install / Repair. Same commit: a re-run no longer overwrites
`viiper.exe.previous` with the current executable.

## Scenario B2 — the fix (from `beta2-0980-B-baseline-20260919`)

**B2a — refusal after an attach this session: PASS**

- DS4 plugged and unplugged on the old stack; no port left imported.
- Setup: `RestartBeforeUpgrade` → `Virtual device attached since Windows
  started: yes` → `RestartBeforeUpgrade`, exit **4**, summary contains "Nothing
  was changed. Restart Windows…". (`B2-guest-05-setup-refusal-console.txt`)
- No installer process was launched: a 150 ms process watcher saw nothing (the
  same watcher logged 22 start/exit lines during the real upgrade, so its
  silence means something).
- Nothing changed — machine-compared fingerprints identical before and after,
  and after two further refusals: `usbip.exe` 0.9.7.7, both driver packages,
  bound driver, services, `viiper.exe` still upstream `2EB92FF3…8FB6A` with the
  same modification time, `setupapi.dev.log` the same size.
  (`B2-guest-03`, `-06`, `-09` fingerprints)
- The old stack still works: X360 plugged and unplugged after the refusal.
- Same result with a pad plugged and a port imported; the running pad was
  untouched. (`B2-guest-07-…port-imported-console.txt`)
- In-app Install / Repair shows the exit-4 message ("Setup changed nothing yet
  … Restart Windows, then run Install / Repair again").
  (`B2-guest-08-inapp-exit4-message.txt`, cropped screenshot)

**B2b — upgrade straight after a restart: PASS**

- Normal restart after three attaches on 0.9.7.7: 24 s, no hang.
- Nothing plugged → observation `no` → `UpgradeRecognisedToPinned` → installer
  verified → exit 0, no restart requested. Driver step 13.4 s; the old
  uninstaller ran 2.7 s and the `devnode remove` that blocked for 12+ minutes
  in the first pass returned in 1.1 s. (`B2-guest-12-setup-upgrade-console.txt`)
- Driver diagnostic PASS on the 0.9.8.0 entry, 0 mismatches; `viiper.exe` is the
  fork build `877050…EB145`; **`viiper.exe.previous` is upstream
  `2EB92FF3…8FB6A`**; backend logs "Auto-attach prerequisites satisfied".
  (`B2-guest-15-driver-diagnostic-report.txt`, `B2-guest-13` fingerprint)
- Re-run: `AlreadyPinned`, "already matches the verified payload", exit 0,
  fingerprint identical including both modification times — the rollback copy
  survives. (`B2-guest-17` fingerprint)
- Plugs on the upgraded stack, same per-run evidence as Scenario A: `xbox360`
  (`045e:028e`), `dualsensecombinedaudioduplexv5` at `frameVersion=5`
  (`054c:0ce6`, speaker and microphone endpoints appeared and were gone after
  unplug), `dualshock4` audio off (`054c:05c4`). All PASS.
  (`B2-plug-01` … `B2-plug-06`)
- Detector on one machine: `yes` after the attach, `no` after the restart; an
  independent re-implementation of its inputs predicted the script's answer
  each time; it never returned `unknown`. (`B2-guest-02`, `-04`, `-10`, `-19`)
- Final shutdown 11 s. No minidumps, no `MEMORY.DMP`, no bugcheck events.

## What this does NOT prove

- No physical input, no haptic or motor feel, no audio played (every speaker
  counter is 0), no real game. Those stay a hardware pass.
- A Fast Startup (hybrid) session: every boot in this guest is boot type 0, so
  the detector's "older of LastBootUpTime and the newest cold boot" rule was
  never stressed.
- An upgrade from 0.9.7.8, and an `unknown` detector result.
- That the `usbip port` fast path itself answered `yes` in the port-imported
  run (the slower check would have said `yes` too by then).
- Whether the old uninstaller's window flashes during a successful upgrade (it
  lived 2.7 s between two 5-second screenshots).
- The first-run wizard in Scenario A was bypassed: config files were seeded and
  setup was run from an elevated shell. The in-app button was exercised in B2a
  only.
- The Inno exit code on success is inferred from the script logging neither
  1641/3010 nor a failure.

## Findings left open

1. **Windows Security firewall prompt on first backend start** (fresh machine).
   The backend listens on `[::]:3241` and `[::]:3242`. With the prompt dismissed
   Windows created Block rules and every plug still worked
   (`A-screen-firewall-prompt-viiper.png`, `A-guest-10-…`). Binding to loopback
   (`--usb.addr`, `--api.addr`) needs its own test: the driver connected from
   `[::1]`.
2. **After an exit-4 result the Settings status text is stale** until Refresh
   ("server: not running" while the backend was serving a pad).
3. **The audio-off virtual DS4 still creates audio endpoints.**
   `CreateDualShock4HidOnlyStream` requests plain `dualshock4`, which carries
   the audio function on this backend; identical on 0.9.7.7 with upstream
   v0.1.2, so pre-existing and not a regression of this pin. Contradicts the
   gate's stated boundary. DualSense and Edge audio-off personas are HID only.
4. **Offline update check** opens a modal "Failed to retrieve latest version"
   box that blocks closing the window until dismissed.
5. The exit-4 message box and the Thrum log print the `install.log` location as
   a full user-profile path where the driver diagnostic uses a redacted form.
6. After an upgrade the root hub instance changes, so device nodes left by the
   old driver point at a hub that is no longer among the controller's children.
   Harmless: the detector is not consulted once the pin matches.
7. The fork backend's refusal text still says "run the DS4Windows VIIPER setup".
   That wording lives in the fork, not in this repository.

## Checkpoints

`beta2-0980-A-baseline-20260919`, `beta2-0980-A-validated-20260919`,
`beta2-0980-B-baseline-20260919`,
`beta2-0980-B-failed-uninstall-hang-then-0x9F-20260919`,
`beta2-0980-B2-validated-20260919`. The VM was left powered off with its adapter
reconnected as found.

## Harness deviations worth knowing next time

- The VM's adapter was found connected with a default route; it was
  disconnected for every scenario. Only `A-baseline` carries the connected
  adapter.
- The Scenario A source checkpoint would not resume (the VM service account is
  denied the attached ISO); the restored VM's saved state was discarded and the
  guest cold-booted, for both scenarios. The checkpoints are untouched.
- The KVP runner was absent from the Scenario A checkpoint and was staged with
  `Copy-VMFile`.
- The guest clock is two hours behind the host.

# VIIPER backend architecture

VIIPER is Thrum's only virtual-controller backend. Thrum offers five output
types — Xbox 360, DualShock 4, DualSense, DualSense Edge, Switch 2 Pro — which
VIIPER presents through usbip-win2 as complete USB devices, including the
applicable Sony audio interfaces.

**Which of those five actually work depends on the backend release.** The
pinned pair is now usbip-win2 **0.9.8.0** with this project's build of VIIPER
**v0.1.2** for it (see *Pinned backend release*). **All five output types were
plugged and unplugged on that pair on 2026-09-19**, on a clean machine through
Thrum's own installer, with the DualSense, Edge and DS4 audio personas as well,
and again on a machine upgraded from 0.9.7.7. Evidence, including the upgrade
hang that pass found and the fix it validated:
`vm-validation-reports/beta2-0980-installer-validation-20260919/REPORT.md`.
With virtual audio endpoints off the DualSense personas negotiate
`dualsensegamepadv5` / `dualsenseedgegamepadv5`; with them on,
`dualsensecombinedaudioduplexv5` / `dualsenseedgecombinedaudioduplexv5`.

The table below is the previous pin's record (upstream v0.1.2 on 0.9.7.7,
2026-09-06), kept because the device names and IDs are unchanged:

| Output type | On upstream v0.1.2 (previous pin) |
| --- | --- |
| `ViiperX360` | **Plugged and unplugged on v0.1.2** as `xbox360`, `045e:028e` |
| `ViiperDualSense` | **Plugged and unplugged on v0.1.2** as `dualsensecombinedaudioduplexv5`, `frameVersion=5`, first attempt with no fallback, `054c:0ce6` |
| `ViiperDualSenseEdge` | **Plugged and unplugged on v0.1.2** as `dualsenseedgecombinedaudioduplexv5`, `frameVersion=5`, first attempt with no fallback, `054c:0df2` |
| `ViiperSwitch2Pro` | **Plugged and unplugged on v0.1.2** as `ns2pro`, `057e:2069` |
| `ViiperDS4` | **Plugged and unplugged on v0.1.2** as `dualshock4` with the audio duplex stream (`microphoneInput=true speakerOutput=true frameVersion=3`), `054c:05c4` |

All five were measured on 2026-09-06 in `Win 11 25H2 Test ENV` from checkpoint
`viiper-006-installer-validated-20260803`, with usbip-win2 0.9.7.7 and no
physical controller. Evidence:
`vm-validation-reports/viiper-012-plug-validation-20260906/REPORT.md`.

The v0.0.6 story (the pin refused `dualsenseext`, so the DualSense could not be
created - issues #70 and #79) was a *client-side* name mismatch, fixed by the
V5-first negotiation below, not a backend regression that needed a rollback.
That fix was validated against v0.0.6; v0.1.2 registers the same V5 names.

## User setup

Thrum checks VIIPER and usbip-win2 at startup. When either component is
missing, the app offers its bundled self-elevating setup. Setup installs both
components, starts the server for verification with its update notifier
disabled, and verifies its local API. It creates no autostart entry; a
pre-existing `RunVIIPER` task or registry entry is reported and removed only
when explicitly requested. Settings also provides Install / Repair and Refresh
actions.

## Pinned backend release

**Since 2026-09-19 (0.9.0-beta.2) the pinned pair is usbip-win2 0.9.8.0 and the
project's own VIIPER fork build.** The two move together because they must:

- **usbip-win2 0.9.8.0** (2026-09-07) is the first release that carries the
  filter memory-corruption fix `4139f44` and the UDE request-lifetime hardening
  this project contributed (usbip-win2 PR #182). `USBip-0.9.8.0-x64.exe`,
  26,390,744 bytes, SHA-256
  `81F426741F7EE2ED991FEBE24A22DACA8400B6AE2F171054E3FB404897E15D39`,
  Authenticode-signed by the same publisher as earlier releases; it installs
  UDE `23.56.48.757` and filter `23.56.30.686`, both attestation-signed by
  Microsoft.
- **Upstream VIIPER cannot run on it.** v0.1.2 and every later upstream tag up
  to 2026-09-19 refuse to start unless `usbip --version` prints exactly
  `0.9.7.7`, and their native attach sends that release's 1100-byte
  `plugin_hardware` request. 0.9.8.0 adds `char serial[16]` and
  `bool wsk_events`; because MSVC pads the inherited
  `imported_device_location` base to its own size, the struct is **1120** bytes
  with `serial` at offset 1100, and the driver rejects any other length.
- **So Thrum pins a fork build:** `potpiemuncher/VIIPER`, tag
  `thrum-v0.1.2-usbip0980.1` — upstream `v0.1.2` (`f5d097b`) plus three commits:
  accept 0.9.8.0, match the 1120-byte layout, and a build workflow that mirrors
  upstream's windows/amd64 leg step for step (the `thrum-` tag prefix keeps
  upstream's own `v*.*.*` release workflow, which publishes client packages,
  from firing). The framed audio/haptics protocol is untouched. VIIPER is
  GPL-3.0; the exact source is the tag and the source archive on the same
  release. The patch is also kept at
  `docs/dev/patches/viiper-0.1.2-usbip-0.9.8.0.patch`.

The two backend identities:

- `viiper-windows-amd64.zip`: 4,809,446 bytes, SHA-256
  `C2EFAF1E5AE5EE93EFB5838C1B49272049D3615E0C7F12E96670A5CA05EB97A8`
- the extracted `viiper.exe`: 11,407,872 bytes, SHA-256
  `877050102C2D415561893ED9393955E4D6FEA50647AFE53FA69387EFFD4EB145`,
  stamped `v0.1.2-usbip0980.1 (f28cab3)`

Both digests are reported by the fork's workflow in `SHA256SUMS.txt` beside the
asset and were recomputed locally from the downloaded release asset on
2026-09-19; the executable was hashed independently after extraction. The
archive contains exactly `viiper.exe` and `licenses.txt`, as upstream's does.
Before pinning, the published binary was run on a physical machine with
0.9.8.0: `xbox360` and `dualsensecombinedaudioduplexv5` both attached through
the native IOCTL.

**Consequences worth knowing before touching either pin again:**

- This backend requires 0.9.8.0 *exactly*. Setup therefore upgrades a recognised
  older driver (`UpgradeRecognisedToPinned`) instead of leaving it alone; a
  newer or unorderable release is still left alone and an unrecognised one is
  still refused.
- A machine that also runs hbashton's DS4Windows shares `%LOCALAPPDATA%\VIIPER`
  and the usbip-win2 install with it. After Thrum's setup, that DS4Windows'
  own backend (which wants 0.9.7.7) will not start. Not a supported pairing.
- When upstream VIIPER accepts 0.9.8.0, the pin should go back to an upstream
  asset and the fork can be retired.
- usbip-win2 0.9.8.0 has one known defect relevant to setup: after a successful
  attach, a host-controller restart or driver unload can hang (usbip-win2
  PR #188, fixed in `develop` on 2026-09-12, unreleased). Thrum's normal flow
  never restarts the host controller; a driver uninstall or upgrade after use
  is safest straight after a reboot.

The previous pin, for the record (2026-09-06 to 2026-09-19): upstream VIIPER
v0.1.2, `viiper-windows-amd64.zip` 4,809,388 bytes SHA-256 `66A9BBD4…14A46`,
`viiper.exe` SHA-256 `2EB92FF3…8FB6A`, with usbip-win2 0.9.7.7.

Setup downloads (or accepts a staged local copy of) the exact release archive,
checks its size and digest before extraction, extracts into a temporary
directory, checks the executable's size and digest, and only then places it in
`%LOCALAPPDATA%\VIIPER`. Either mismatch refuses the install. The archive's
`licenses.txt` is installed beside `viiper.exe`; it is upstream's third-party
licence roll-up and is part of the installed backend material.

The version stamp is shown only as a human-readable diagnostic cross-check. It
is never a validation input: the archive and payload digests are the
identities, with no version floor and no fallback to a latest release.

**Driver pin and backend pin are coupled by the attach ABI, and the driver pin
is still chosen on its own merits.** 0.9.7.7 was pinned until 2026-09-19 because
0.9.7.8 is the release the corruption was reproduced on and nothing newer
existed; upstream VIIPER happening to require 0.9.7.7 was a coincidence, not the
reason. The pin moved to 0.9.8.0 because that release carries the fixes and had
ten days of daily use on a physical machine with virtual audio endpoints on and
no bugcheck. The backend pin moved *because* the driver pin did: a backend that
speaks the wrong `plugin_hardware` layout cannot attach at all.

## The v0.0.6 to v0.1.2 delta

343 commits across releases v0.0.7, v0.0.9, v0.1.0 (no changes) and v0.1.2.
What was checked against Thrum's client before re-pinning, all by reading the
v0.1.2 source and the shipped binary - none of it is runtime evidence:

- **Device-type names.** v0.1.2 registers `xbox360`, `dualshock4`,
  `dualshock4audioduplexv3`, `dualshock4audioonlyduplexv3`, `ns2pro`,
  `keyboard`, `mouse`, and the DualSense V5 family. The three V5 names Thrum
  negotiates first (`dualsensecombinedaudioduplexv5`,
  `dualsenseaudioonlyduplexv5`, `dualsenseedgecombinedaudioduplexv5`) are
  unchanged. New aliases exist that Thrum does not request: `...gamepadv5`
  (no audio interfaces), `...v5events` (adds ordered output lifecycle events)
  and `...v5rawinput...` (negotiates a 53-byte input state instead of 33).
- **Framed stream contract.** Magic `VPCM`, version byte, 16-byte header,
  33-byte input state, 48-byte output report and 64-byte input report are
  unchanged. v0.1.2 *requires* frame version 0x05 on every DualSense persona;
  Thrum's V5-first path sends 0x05. The V4/V3/V2 fallbacks would be rejected
  by v0.1.2 and exist only for older backends.
- **New server-to-client frame types.** v0.1.2 adds `0x84`
  (realtime haptics, published before the 480-frame speaker boundary) and
  `0x85` (microphone interface state, only on the `...events` aliases). The
  paired `0x83` atomic frame is still sent unchanged. Thrum's reader handles
  `0x81`/`0x82`/`0x83` and drops any other frame type without error, so it
  keeps working and simply does not use the lower-latency lane yet.
- **API authentication (new in v0.0.9).** Remote clients must authenticate;
  localhost clients are still accepted unauthenticated unless
  `VIIPER_API_REQUIRE_LOCALHOST_AUTH` is set. Thrum only talks to localhost.
- **Xbox 360 subtypes (breaking in v0.0.9).** The change is in the generated
  client libraries; the `xbox360` device type takes no subtype on the create
  endpoint that Thrum uses.
- **CLI.** `serve` still accepts `--update-notify none`, which Thrum passes.

## PadSense V5 transport compatibility

Thrum negotiates the PadSense V5 personas (introduced in v0.0.6, unchanged in
v0.1.2) first:
`dualsensecombinedaudioduplexv5`, `dualsenseaudioonlyduplexv5`, and
`dualsenseedgecombinedaudioduplexv5`. These streams use VPCM frame version 5,
474-byte feedback, 1,920-byte microphone PCM, and atomic feedback-plus-speaker
generations. The older V4, V3, V2, and HID-only names remain as fallbacks for
older backends.

This was validated in `Win 11 25H2 Test ENV` from checkpoint
`viiper-006-installer-validated-20260803`, with VIIPER 0.0.6 and usbip-win2
0.9.7.7. The pre-fix build exhausted its legacy names and received
`400 Bad Request: unknown device type`. The V5-first build created an explicit
virtual DualSense through Thrum's Output Slots UI. Independent checks showed:

- API type `dualsensecombinedaudioduplexv5`, VID/PID `054c:0ce6`, and an active
  speaker stream across two censuses 30 seconds apart;
- a live `usbip://localhost:3241/1-1` import;
- the HID game-controller, composite USB, media, speaker, and microphone
  interfaces present and healthy; and
- a clean Unplug: no VIIPER buses, no usbip import, no present DualSense PnP
  devices, and a still-running backend and usbip service.

The VM had no physical controller passed through. This proves V5 negotiation,
stream ownership, attach/enumeration, audio-interface creation, stability, and
teardown; it does not claim physical input, motor feedback, or non-zero audio
payload validation. Those three checks remain a maintainer hardware pass.
## How to validate a backend pin

**The v0.1.2 pin has had this plug validation.** It was run on 2026-09-06 from
checkpoint `viiper-006-installer-validated-20260803`: all five output types
plugged and unplugged cleanly, the installer path passed including its
tampered-archive refusal, and backend stop-on-exit behaved in both directions.
What it does *not* cover — physical input, motor feedback, audio payload
correctness, and whether v0.1.2 ever emitted a `0x84` frame (its log never names
a frame type) — is stated in
`vm-validation-reports/viiper-012-plug-validation-20260906/REPORT.md`.
Re-run this procedure for the next pin.

The lesson from the v0.0.6 refresh, recorded so it is not repeated: that pin
was validated by exercising the **installer path only** — digests, refusal on a
tampered archive, backend startup, API reachable — and it passed while shipping
a backend that could not create the project's headline device. "The backend
starts" and "the backend can serve what we ask of it" are different claims.

**A pin is not validated until one virtual device of every type Thrum supports
has been plugged successfully against it.** That needs no hardware: mark an
output slot permanent in `%APPDATA%\Thrum\OutputSlots.xml` and
`ControlService.AssignInitialDevices()` will plug it with `InputIndex = -1`,
i.e. with no physical controller present.

```xml
<OutputSlots app_version="0.9.0.0">
  <Slot idx="0"><DeviceType>ViiperDualSense</DeviceType></Slot>
</OutputSlots>
```

Then confirm with `usbip port` in the guest and the Thrum log. Valid
`DeviceType` values are `ViiperX360`, `ViiperDS4`, `ViiperDualSense`,
`ViiperDualSenseEdge`, `ViiperSwitch2Pro`.

## Profile migration

The retired serialized values `X360` and `DS4` remain readable solely for
backward compatibility. They normalize immediately to `ViiperX360` and
`ViiperDS4`; new saves never write the retired values.

## Runtime containment

Thrum records locally created VIIPER Sony interfaces before normal HID
enumeration and rejects them as physical inputs. Moonlight/Sunshine virtual
controllers use a separate opt-in admission policy, so accepting streamed
controllers cannot make Thrum recursively ingest its own output.

## Feedback and audio

VIIPER feedback is read by `ViiperOutDevice` and routed to the currently bound
physical controller. Xbox/standard rumble, Sony lightbar output, adaptive
triggers, advanced haptics, speaker playback, and microphone capture are
translated according to the physical controller's capabilities.

# VIIPER backend architecture

VIIPER is Thrum's only virtual-controller backend. Thrum offers five output
types — Xbox 360, DualShock 4, DualSense, DualSense Edge, Switch 2 Pro — which
VIIPER presents through usbip-win2 as complete USB devices, including the
applicable Sony audio interfaces.

**Which of those five actually work depends on the backend release.** The pin
is now VIIPER **v0.1.2** (2026-08-27). Status per output type:

| Output type | On pinned v0.1.2 |
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

Thrum pins VIIPER v0.1.2 as two identities because upstream now publishes a
zip rather than a bare executable:

- `viiper-windows-amd64.zip`: 4,809,388 bytes, SHA-256
  `66A9BBD4535C9914752E59E1426DAB8F318F6A441367A7EAB6563E6674A14A46`
- the extracted `viiper.exe`: 11,407,872 bytes, SHA-256
  `2EB92FF3E82ABE292E531B6D35B10341396BF2A83FFDE6532FAEC8374B48FB6A`

Both digests were computed locally on 2026-09-06 from the downloaded release
asset; the archive digest matches the digest GitHub reports for that asset, and
the executable was hashed independently after extraction. The archive contains
exactly `viiper.exe` and `licenses.txt`, as v0.0.6's did.

Setup downloads (or accepts a staged local copy of) the exact release archive,
checks its size and digest before extraction, extracts into a temporary
directory, checks the executable's size and digest, and only then places it in
`%LOCALAPPDATA%\VIIPER`. Either mismatch refuses the install. The archive's
`licenses.txt` is installed beside `viiper.exe`; it is upstream's third-party
licence roll-up and is part of the installed backend material.

The executable is stamped `v0.1.2 (f5d097b)`. That stamp is shown only as a human-readable diagnostic
cross-check. It is never a validation input: the archive and payload digests
are the identities, with no version floor and no fallback to a latest release.

VIIPER 0.1.2, like 0.0.6, gates its own startup on the supported usbip-win2
0.9.7.7 attach ABI (the binary carries only that version string and links to
the `v.0.9.7.7` release page), which happens to agree with Thrum's driver pin.

**That agreement is a coincidence, not the reason for the driver pin, and the
distinction matters.** An earlier revision of this file said Thrum "therefore"
keeps 0.9.7.7 because the backend requires it — which invites the conclusion that
moving the backend pin frees the driver pin to move. It does not.
The real reason is in `ViiperInstallerPins.cs`: 0.9.7.7 is pinned because
**0.9.7.8 is the release the request-lifetime corruption was reproduced on**.
The driver pin does not move when the backend pin moves. (Nor is there anywhere
to move it to: usbip-win2 PR #182 and the maintainer's root-cause fix are merged
to `master`, but no release carries them — the newest release is still
`v.0.9.7.8`.)

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

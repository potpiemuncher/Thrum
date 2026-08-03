# VIIPER backend architecture

VIIPER is Thrum's only virtual-controller backend. It exposes Xbox 360,
DualShock 4, DualSense, DualSense Edge, and Switch 2 Pro devices through
usbip-win2 as complete USB devices, including the applicable Sony audio
interfaces.

## User setup

Thrum checks VIIPER and usbip-win2 at startup. When either component is
missing, the app offers its bundled self-elevating setup. Setup installs both
components, starts the server for verification with its update notifier
disabled, and verifies its local API. It creates no autostart entry; a
pre-existing `RunVIIPER` task or registry entry is reported and removed only
when explicitly requested. Settings also provides Install / Repair and Refresh
actions.

## Pinned backend release

Thrum pins VIIPER v0.0.6 as two identities because upstream now publishes a
zip rather than a bare executable:

- `viiper-windows-amd64.zip`: 4,735,340 bytes, SHA-256
  `6EC76B298AF402AC65BA21F00DFFC9D3DA36909BDD1C909AEE9047FE4F9B0D1B`
- the extracted `viiper.exe`: 11,223,552 bytes, SHA-256
  `90254E1352BFF7607DBEE0819F0750032F76C52CD9BF54150D21267224BA8F7A`

Setup downloads (or accepts a staged local copy of) the exact release archive,
checks its size and digest before extraction, extracts into a temporary
directory, checks the executable's size and digest, and only then places it in
`%LOCALAPPDATA%\VIIPER`. Either mismatch refuses the install. The archive's
`licenses.txt` is installed beside `viiper.exe`; it is upstream's third-party
licence roll-up and is part of the installed backend material.

The executable is correctly stamped `v0.0.6 (e85575d)`, built
`2026-07-31T01:52:14Z`. That stamp is shown only as a human-readable diagnostic
cross-check. It is never a validation input: the archive and payload digests
are the identities, with no version floor and no fallback to a latest release.

VIIPER 0.0.6 gates its own startup on the supported usbip-win2 0.9.7.7 attach
ABI. Thrum therefore keeps its existing usbip-win2 0.9.7.7 pin unchanged; the
backend and driver pins are deliberately a coherent pair.

The real v0.0.5-to-v0.0.6 delta is 13 commits, including PadSense-native
DualSense V5 audio transport, restoration of the safe usbip-win2 0.9.7.7
attach ABI, startup gating on that ABI, and release-workflow fixes that produce
the correct embedded stamp. The v0.0.6 release note saying "No changes" is not
an accurate description of the artifact delta.

## PadSense V5 transport compatibility

Thrum negotiates the v0.0.6 PadSense personas first:
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

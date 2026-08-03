# DualSense Bluetooth Audio and Haptics

## Scope

This branch keeps three DualSense feedback paths separate:

| Path | Source | Physical controller transport |
| --- | --- | --- |
| Rumble, adaptive triggers, lightbar, LEDs | DualSense HID output report `0x02` | normal DualSense HID output (`0x31` on Bluetooth) |
| Advanced haptics | VIIPER virtual UAC channels 3/4, **or** any Windows render endpoint via Audio Haptics | Bluetooth HID `0x36`, packet `0x12`, 3 kHz signed stereo PCM |
| Controller speaker audio | selected Windows render endpoint, including VIIPER's virtual `Wireless Controller` endpoint | Bluetooth HID `0x35`, packet `0x13`, 48 kHz stereo Opus |
| Controller microphone | physical DualSense Opus or DualShock 4 SBC microphone frames | VIIPER virtual DualSense/Edge 48 kHz stereo or DualShock 4 16 kHz mono UAC capture endpoint |

The channels are intentionally not mixed. In particular, the advanced-haptics PCM stream is never converted to generic rumble or routed to the controller speaker.

**Report IDs, stated precisely, because getting them wrong misdirects debugging.**
The BT descriptor declares `0x32`–`0x39` as container reports, and more than one
of them can carry a PID `0x12` haptics packet — SAxense streams haptics on the
142-byte `0x32`, which is where the protocol notes and most public references
start.

**Thrum does not.** `DualSenseHapticsStreamer` streams on report **`0x36`**
(`HAPTICS_REPORT_ID`/`AUDIO_REPORT_ID`) — 398 bytes, PID `0x12` packet at offsets
76-141 — because that container also carries controller state and the optional
Opus lane, so one owner can publish all three coherently. In this codebase `0x32`
(`STATE_SETUP_REPORT_ID`) is used only for the one-off amplifier setup at stream
start.

So "the haptics report is `0x32`" is true of the protocol literature and false of
this implementation. Carrying the first into a Thrum debugging session makes a
transport-ownership problem look like a missing mutex between two `0x32` writers,
when the real contention is over `0x36`. See `dev/bt-haptics-attenuation.md`.

Related, and already known before it bit us: firmware rumble emulation and
haptics streaming are mutually exclusive. Asserting the motor flags or the
improved-rumble bit in `0x31` while streaming silences the stream, which is why
`DualSenseDevice` has four separate `hapticsStreamActive` guards.

**Advanced haptics no longer require a virtual controller.** Audio Haptics can
capture any Windows render endpoint and stream the derived PCM straight to a
physically connected DualSense over Bluetooth — no VIIPER, no USB/IP, no driver
(issue #58, confirmed on hardware 2026-08-03). The VIIPER UAC path below remains
the route for games that address the controller as an audio device.

This path is **Bluetooth-only**. Over USB the streamer does not run and audio
haptics still need a virtual controller; the Audio Haptics page says so rather
than reporting success. Tracked as issue #65.

Speaker and microphone routing follows the emulated controller selected by the
profile, not the physical model. A physical DualSense can therefore feed a
virtual DualShock 4 audio endpoint, and a physical DualShock 4 can feed a
virtual DualSense endpoint. Thrum selects the matching virtual render
endpoint automatically and converts microphone PCM to the virtual endpoint's
native sample rate and channel layout.

## In-game setup

1. Install a VIIPER build containing the DualSense UAC interface.
2. Select **DualSense** in the Thrum profile.
3. Connect a physical DualSense or DualSense Edge over Bluetooth.
4. In the profile's **Controller audio** section, enable **Stream audio to controller**.
5. Select the virtual `Wireless Controller` / DualSense render endpoint as **Audio source**.
6. In the game, select that same endpoint when the game provides a controller-audio output choice.

The VIIPER virtual audio endpoint is created by VIIPER's USB Audio Class function. Thrum does not create a fake Windows audio endpoint in user mode. Windows audio endpoints require a driver-backed device interface; creating one separately would require an installed, signed virtual audio driver.

## Bluetooth speaker processing

The profile can optionally process the physical Bluetooth controller-speaker stream before Opus encoding:

- **Dynamic range: Balanced** raises quieter detail while restraining loud effects.
- **Dynamic range: Strong** applies a narrower range for larger volume differences.
- **Bass/body boost** adds 0-6 dB around 200 Hz and filters unusable sub-bass below 70 Hz.

Selecting a DualSense or DualSense Edge as the emulated controller initializes the profile to **Balanced** and **3 dB** of bass/body boost. The user can tune or disable those values afterward. The processor is stereo-linked and bufferless, so it adds no look-ahead frame or transport latency. **Off** and **0 dB** preserve the original PCM path. These controls affect speaker audio only; advanced-haptics channels remain untouched.

## Implementation references

This is an independent implementation based on publicly documented packet behavior, not a copy of PadForge source code.

- [SAxense](https://apps.sdore.me/SAxense) documents the Bluetooth `0x32` haptics transport. Its source is MPL-2.0.
- [dualsense-bt-haptics](https://github.com/awalol/dualsense-bt-haptics) documents the Bluetooth controller speaker packet grammar and Opus framing. It is MIT licensed.
- [PadForge](https://github.com/hifihedgehog/PadForge) was used as a behavioral reference for per-controller sequencing, separate haptics and speaker lanes, and WASAPI loopback architecture. PadForge is CC BY-NC-SA 4.0, so its source code is not included here.

## Diagnostics

When the virtual audio interface is active, a VIIPER traffic capture should contain:

- `audio-haptics-out` for host audio written to the virtual UAC OUT endpoint.
- `saxense-hid-0x32` for the generated Bluetooth haptics report.

If the Windows `Wireless Controller` audio endpoint has an error state, remove stale VIIPER DualSense devices, restart VIIPER, then recreate the output. The endpoint descriptor changed after the initial experimental build, so Windows can retain an old failed device instance until the virtual device is recreated.

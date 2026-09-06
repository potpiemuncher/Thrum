# Bluetooth haptics attenuation: transport-ownership analysis

## Result

The attenuation is not caused by the PCM conversion, gain, capture level, or
write cadence. The ported streamer was correct for the older tree's transport
model, but it retained two direct `HidDevice` writes that bypassed Thrum's
single-owner Bluetooth output subsystem.

The steady stream is a 398-byte report `0x36`, not a bare report `0x32`:
`DualSenseHapticsStreamer.BuildHapticsReport` and `BuildAudioReport` put the
PID `0x12` haptics packet at offsets 76-141. The one-off amplifier setup is the
142-byte report `0x32`. In the read-only reference tree both writes go directly
to `HidDevice.WriteOutputReportViaInterrupt` at
`DS4Library/InputDevices/DualSenseHapticsStreamer.cs:594` and `:997`.

That is safe in the reference implementation because its `DualSenseDevice`
has only the ordinary event-driven controller-state writer: `PrepareOutReport`
feeds `WriteReport`, whose Bluetooth branch writes report `0x31` directly at
`DS4Library/InputDevices/DualSenseDevice.cs:1427-1433`. The reference has no
combined `0x36` writer, real-time writer, audio-pacer process, speaker session,
or transport-ownership handoff.

It is not safe in Thrum. Before this fix, the copied streamer still performed
those two direct writes, while `DualSenseDevice` could independently write or
queue combined `0x36` reports through all of these paths:

- VIIPER/legacy haptics `0x32` is converted to combined `0x36` by
  `WriteBluetoothHapticsOutputReport` (`DualSenseDevice.cs:3075`).
- A legacy speaker `0x35` is converted rather than written to hardware by
  `WriteBluetoothSpeakerAudioOutputReport` (`:3298`).
- VIIPER combined state/haptics `0x36` enters
  `WriteBluetoothCombinedHapticsAudioOutputReport` (`:3320`).
- Controller state which would normally be report `0x31` is merged into the
  cached `0x36` after the combined transport is enabled
  (`UsesCombinedBluetoothOutputTransport` at `:2460` and
  `FlushPreparedOutputReport` at `:3480-3503`).
- Speaker callbacks are paired by `BeginBluetoothAtomicSpeakerFrame` and
  `EndBluetoothSpeakerGeneration` (`:859` and `:881`), then queued through the
  fixed-cadence audio pacer or the in-process real-time writer.
- Microphone enable/disable is applied to the same cached state by
  `SetBluetoothMicrophoneStreaming` (`:3968`).
- Shutdown's final legacy empty report is only sent after the pacer and
  real-time owner have retired (`:2468-2497`).

The combined paths serialize publication with
`bluetoothCombinedTransportWriteLock`, reject writes during
`bluetoothAudioLifecycleTransitioning` or
`bluetoothOutputTransportStopping`, and maintain one Sony report sequence.
The copied streamer participated in none of those rules.

## Mechanism for “buzz at reconfigure, then silence”

There are two code-supported variants of the same ownership bug.

1. On a haptics-only configuration, `Configure` calls `StopLocked` and
   `StartLocked` (`DualSenseHapticsStreamer.cs:259-276`). The restarted stream
   prebuffers and begins sending fresh `0x36` frames every 10.667 ms. Because
   those direct writes did not call `EnsureBluetoothCombinedOutputTransport`,
   they did not set `BluetoothCombinedOutputTransportEnabled`. The next dirty
   light/trigger/controller-state flush therefore selected the direct
   `WriteReport` branch and put a competing `0x31` on the pad. Thrum's existing
   comment at `DualSenseDevice.cs:3484-3487` records the relevant firmware
   behavior: a competing `0x31` can interrupt both `0x36` audio lanes.
2. If the streamer's optional listening-audio output is enabled, restart first
   sends the amplifier-unmute `0x32` from `SendAudioVolumeSetup`, so the brief
   vibration at reconfigure has an additional concrete arming event. The next
   uncoordinated state/combined write can then replace that state just as above.

If another Thrum feature had already enabled the combined transport, the
failure mode was instead two owners of the same HID handle and Sony sequence:
the direct streamer interleaved with queued real-time/pacer `0x36` work. Clean
HID completion statistics do not detect that semantic collision; every write
can complete successfully while later state supersedes earlier state.

The code proves the bypass, the competing branches, and the missing ownership
transition. The exact firmware reaction (“this particular later report muted
the actuator stream on this pad”) still requires a hardware trace or run; it
cannot be proven by unit tests alone.

## Fix

`DualSenseHapticsStreamer` no longer owns a `HidDevice`. Its steady frames now
enter `DualSenseDevice.WriteBluetoothHapticsStreamerOutputReport`
(`DualSenseDevice.cs:3097`):

- haptics-only `0x36` uses the existing combined-state publication path;
- `0x36` containing a fresh PID `0x13`/`0x16` Opus lane is queued intact through
  the pacer or real-time writer, so integrating haptics does not silently remove
  the streamer's optional listening audio;
- the first report enables the combined transport, so later ordinary controller
  state is merged instead of emitted as a competing `0x31`;
- sequence, CRC, stop, lifecycle, speaker-clock, and writer ownership are now
  shared with Thrum's other Bluetooth producers.

The one-off amplifier setup enters
`WriteBluetoothHapticsStreamerAmplifierSetup` (`:3192`). It holds the combined
transport lock, retires the pacer and in-process writer, performs the `0x32`
write only after ownership is released, and lets the next steady frame recreate
the normal combined owner. This is deliberately a once-per-stream-start
handoff, not a per-frame retirement.

Unit coverage in `DualSenseBluetoothAudioTransportTests` asserts that the
streamer cannot regain a private `HidDevice`, validates the accepted combined
layout, distinguishes haptics-only reports, preserves both recognized
listening-audio packet types, and rejects malformed haptics lengths.

## Connect ordering

Previously `PrepareConnectedInputControllerSettingEvents` called
`device.LoadStoreSettings` before `LoadProfile`. The added
`DualSenseDevice.LoadStoreSettings` hook therefore mapped the pre-profile
default (`Off`) and its refresh returned early because
`hapticsStreamerReady` was false. `CheckProfileOptions` later pushed the loaded
profile, but still before `device.StartUpdate` made the streamer ready. A later
refresh happened to recover this ordering.

The pre-profile call has been removed from `DualSenseDevice.LoadStoreSettings`.
Profile and auto-profile changes still apply through `CheckProfileOptions`, and
fresh-connect setup now explicitly calls `ApplyAudioHapticsDeviceOptions`
immediately after `device.StartUpdate` (`ControlService.cs:2381-2390`). At that
point both prerequisites are deterministic: the profile is loaded and the
physical streamer is ready. The temporary `[DIAG]` messages remain in
`ApplyAudioHapticsDeviceOptions` and `RefreshHapticsStreamerState` for the next
hardware run.

## Evidence boundary and next hardware run

Proven by code/build/tests:

- the reference streamer and the pre-fix port had byte-identical conversion and
  cadence logic;
- the pre-fix Thrum streamer bypassed every combined-transport lock and owner;
- the fixed streamer has no physical HID handle and all of its writes cross the
  device-owned transport boundary;
- ordinary `0x31` state switches to merge mode as soon as the first streamer
  report enables the combined transport;
- profile-backed options are reapplied only after readiness on fresh connect.

## Hardware run — completed 2026-08-03

The run this section previously listed as outstanding has happened: real
DualSense over Bluetooth, SteelSeries Sonar - Gaming as the source, on the
development PC.

Confirmed:

- **sustained actuator strength**, reported as strong rather than the pre-fix
  "smallest amount of vibration";
- **strength holds under the contention the fix was written for** — tested with a
  virtual Xbox 360 controller plugged in and associated, which is the
  configuration that was failing. This is the result that matters; passing only
  the no-virtual-controller case would have proven nothing about ownership;
- transport behaviour under real radio conditions: `underruns=0 drops=0
  stallSkips=0`, 7-11 slow writes, `maxWrite` 26-30 ms across several sampling
  windows;
- the connect ordering fix, observed in the log: the pre-readiness refresh
  reports `ready=False` and the real apply lands after `StartUpdate` with
  `mode=SystemAudio ... conType=BT`.

Still **not** verified, and not claimed:

- **optional controller listening audio coexisting with another Thrum speaker
  producer.** The run had no speaker producer configured, so the queue-intact
  path for `0x36` frames carrying an Opus lane is still code-only.
- **that no firmware-specific state field is being attenuated.** Strength is
  now subjectively equal to the known-good build; nothing measured the actuator
  signal itself, so a small systematic difference would not have been detected.
- the wired USB Audio Haptics route. Issue #65 now uses the physical
  four-channel DualSense render endpoint (haptics on channels 3/4) and suppresses
  competing HID motor ownership only while that output is live, but the result
  is still code-verified rather than felt on hardware.

Two defects were found by the run rather than by the code: the stream restarts
twice on connect (#66), and USB users were incorrectly rejected (#65). The #65
implementation is complete in code; its remaining evidence is a wired hardware
pass covering strength, ordinary-rumble contention, unplug/failure status and
ordinary-rumble restoration.

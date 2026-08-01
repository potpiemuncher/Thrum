# Frontend modernization

Thrum keeps the runtime and WPF binding layer it inherited from DS4Windows intact while
adopting the navigation, spacing, cards, descriptions, and progressive-disclosure patterns
used by the DS5 Bridge companion app.

Phase 4 tracks the remaining per-page work; `docs/dev/ui-modernization-status.md` holds the
current inventory of which pages have adopted the card shell and which have not.

## Why the frontend remains WPF

DS5 Bridge's companion is an Electron/React application backed by a small vendor-HID
protocol. Thrum has hundreds of mature WPF bindings and event handlers connected
directly to controller, profile, output-slot, automation, and diagnostic services. Replacing
that layer with Electron would require a second public API for nearly the entire program and
would make regressions easy to miss.

The modernization therefore treats DS5 Bridge as the visual and information-architecture
reference. `BridgeShellStyles.xaml` provides the shared shell and component geometry, while
the existing view models remain the source of truth. Theme-specific colors stay in the
existing light and dark theme dictionaries, so runtime theme switching continues to work.

## Current navigation and feature coverage

Nothing in the existing UI has been removed. The main shell exposes:

- **Controllers**: connection type, access status, battery, selected profile, per-device
  profile linking, profile editing/creation, temporary lightbar color, and wireless
  disconnect behavior.
- **Profiles**: create, edit, rename, duplicate, delete, import, and export.
- **Auto Profiles**: application-driven profile and controller-slot switching.
- **Output Slots**: virtual controller slot inspection and control.
- **Settings**: ordinary startup, notification, charging, appearance, and update preferences.
- **Advanced settings**: VIIPER setup, OSC input/output, UDP server and smoothing, language,
  Steam/custom executable compatibility, process priority, absolute-mouse monitor, device
  registration, driver/update utilities, and diagnostics.
- **Log**: live status messages, export, clear, and detailed-message inspection.

The profile editor keeps the interactive controller mapping canvas and makes the dense
settings rail explicit:

- **Controls**: complete button, stick, trigger, touch, gyro, keyboard, mouse, macro, and
  unbound mapping support.
- **Special Actions**: create, edit, remove, enable, and export action definitions.
- **Controller Readings**: live input, dead-zone, and drift inspection.
- **Axis Config**: left/right stick radial and axial dead zones, anti-dead zones, max zones,
  output curves, rotations, outer bindings, delta acceleration, flick stick, L2/R2 tuning,
  and six-axis acceleration.
- **Lightbar**: normal color, battery color, flash behavior, empty color, and passthrough.
- **Touchpad**: mouse, controls, mouse joystick, absolute mouse, passthrough, tap/double-tap,
  scroll, trackball, smoothing, inversion, and click behavior.
- **Gyro**: controls, mouse, mouse joystick, directional swipe, passthrough, steering wheel,
  trigger conditions, toggles, smoothing, jitter compensation, and inversion.
- **Advanced**: virtual output type and disable switch, output hooks, debouncing, rumble and
  DualSense rumble translation, controller speaker and microphone passthrough, mute-button
  lighting, input readout, mouse acceleration, touchpad toggle, DS4 output data, Game Bar,
  launch-with-profile, idle disconnect, wireless polling, and absolute-mouse options.

## Rules for the next UI passes

1. Backend settings remain authoritative; views do not maintain shadow copies.
2. A setting may move under **Advanced**, but it is not removed or silently reset.
3. Common pages use one title, one short description, flat content, and bordered cards.
4. Visible helper text is preferred to unexplained acronyms or tooltip-only documentation.
5. Device-specific controls remain visible only when their existing availability binding
   says the device supports them.
6. New DS5 Bridge-derived features are separate follow-up work, not part of the visual
   migration.

## Follow-up feature seams

All three seams this document originally listed as future work have since been built. They are
kept here with their outcomes, because the seam each one describes is still the constraint that
governs changes to it.

- **Audio Haptics** — built. `AudioHapticsService` and `AudioHapticsProcessor` sit behind the
  Audio Haptics page, with settings in `ProfileFeatureSettings`. The original constraint still
  applies: audio capture must not be coupled to a page's lifetime.
- **Adaptive-trigger library** — built. `TriggerLabPreset`, `TriggerLabPresetCatalog` and
  `TriggerLabCustomProfile` back the Trigger Lab page, with encoding in
  `TriggerLabEffectEncoder`. Note that these types live in `ProfileFeatureSettings` and are
  serialized through `ProfileDTO`, so the original "persist named presets *independently* of
  controller profiles" goal is only partly met — worth confirming before building anything that
  assumes a preset outlives the profile that names it.
- **Controller artwork** — built. `ControllerUiCapabilities.ImageResourceName` maps each
  `InputDeviceType` to a device-specific asset (DualShock 4, DualSense, DualSense Edge, Switch 2
  Pro), with `HasControllerArtwork` for the types that have none. The shell no longer assumes
  every controller is a DualSense; task 4.2 extends this policy rather than branching in views.

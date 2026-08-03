# Thrum User Guide

Thrum reads supported physical controllers, applies a profile, and can present a
virtual controller that games recognize. It also exposes DualSense features such
as adaptive triggers, controller audio, and audio-driven haptics.

This guide describes the current navigation-rail interface and first-run wizard.
Thrum is pre-release software, and its virtual-controller backend uses an
experimental third-party kernel driver. Read the safety section before enabling
virtual output.

## Before you start

- Use a 64-bit Windows system. Thrum and its VIIPER backend are currently x64-only.
- Do not run Thrum and DS4Windows at the same time. Both applications can try to
  claim the same physical controller.
- Connecting and testing a physical controller does not require VIIPER. VIIPER is
  needed when a game must see an emulated Xbox, PlayStation, or Switch controller.
- A DualSense can receive Audio Haptics directly over Bluetooth without VIIPER or
  usbip-win2 when the source is the Windows mix or a render endpoint.

If you are building Thrum yourself, follow the requirements and commands in the
[README](README.md#building).

## First-run setup

On a fresh configuration, Thrum opens **Set up Thrum** before starting the
controller service. The wizard has seven named stages. The import stage appears
only when a pristine app-data location and compatible DS4Windows settings are
both found; importing an existing configuration can also skip the new-device
defaults stage.

1. **Welcome to Thrum** explains the application and the optional VIIPER backend.
2. **Choose where Thrum stores its data** defaults to **App data
   (recommended)**, under `%APPDATA%\Thrum`. Expand **Advanced: portable data
   location** only when you intentionally want settings beside `Thrum.exe`.
   Portable mode is unavailable when the program folder is not writable.
3. **Import existing settings** reviews any compatible DS4Windows configuration
   found in the legacy data folder. Import skips files already present and does
   not modify the source. Choose **Start fresh** in the review dialog to decline.
4. **Choose supported controller types** keeps DualShock 4 enabled and lets you
   add DualSense/Edge, Switch Pro, Joy-Con, or DualShock 3. DualShock 3 also
   requires DsHidMini. These choices remain available under **Settings > Device
   options**.
5. **Backend and driver status** checks VIIPER and usbip-win2. **Install / Repair
   VIIPER** runs the guided setup; **Recheck status** reads the state again.
   Skipping this stage is safe, and Thrum offers setup again if a profile later
   requests virtual output.
6. **Connect a controller** reminds you to use USB or Bluetooth. Detection begins
   after the wizard because the mapping service is deliberately not started
   during setup.
7. **You are ready to start** creates a Default profile when starting fresh and
   opens the main window.

Before the data location is committed, **Cancel setup** exits without starting
Thrum. Afterwards the button changes to **Finish later** and completes the safe
configuration work needed to continue.

## The main window

The navigation rail contains ten pages, in this order:

1. Overview
2. Controllers
3. Audio Haptics
4. Trigger Lab
5. Profiles
6. Auto Profiles
7. Output Slots
8. Diagnostics
9. Settings
10. Log

The footer shows the latest status message. The **Start**/**Stop** button controls
the mapping service; it normally starts automatically after launch. **About**
opens version, project, lineage, and contributor information.

## Virtual output and the consent gate

Virtual controllers are presented through VIIPER and the third-party
`usbip-win2` kernel driver. Thrum refuses to create a new virtual device when the
driver is missing or when its installed package cannot be matched to the pinned
identity and trust manifest.

Setup and consent are separate:

1. **Settings > VIIPER Virtual Controller Support** installs or repairs the pinned
   backend and shows the detected driver status.
2. **Use virtual controllers (experimental kernel driver)** records that you read
   the one-time disclosure. No currently listed usbip-win2 release is treated as
   production-approved. A kernel-driver fault can stop Windows, and Thrum cannot
   catch or recover from it.
3. **Allow virtual audio and microphone endpoints** is a separate, default-off
   switch. Enabling it shows a second disclosure every time because those virtual
   endpoints reach a confirmed usbip-win2 teardown defect that can corrupt kernel
   memory and crash Windows. The upstream report is
   [usbip-win2 #181](https://github.com/vadimgrn/usbip-win2/issues/181).

You do not need virtual audio endpoints for buttons, sticks, triggers, rumble,
gyro, touchpad, or lightbar output. Do not enable them merely to get ordinary
controller emulation working. Turning either setting off does not tear down a
device that is already running; the new policy applies on the next connection.

Driver-free Audio Haptics is a different path. A physical DualSense or DualSense
Edge connected over Bluetooth can receive the Windows mix or a selected render
endpoint directly. Direct Bluetooth output does not serve an individual app
session, and the driver-free direct path does not run over USB. The Audio Haptics
status card reports when capture is active but no usable output path exists.

## 1. Overview

![Overview page](docs/images/tour/overview.png)

Overview is the working dashboard for the selected controller. It shows battery,
connection type, input latency, access state, current profile, emulated device,
and controller startup status.

The page also provides profile-backed quick controls for feedback strength,
emulated device, and supported controller-audio options. **Quick actions** opens
the full profile editor, controller details, temporary lightbar controls, or a
wireless disconnect action. **Test inputs** opens the live input tester.

The input tester replaces the old workflow that treated controller readings as a
profile-editor tab. It compares raw and mapped axes, plots sticks and trigger
travel against the active profile, shows buttons, gyro, accelerometer, and
touchpad state, and provides bounded rumble/lightbar tests plus stick calibration.

## 2. Controllers

![Controllers page](docs/images/tour/controllers.png)

Controllers lists every device Thrum currently manages. Each card shows identity,
connection and access state, battery, active profile, and a temporary lightbar
color.

- Choose a profile from **Active profile** to apply it to that controller.
- Enable **Link profile/ID** to reuse that profile whenever the same physical
  controller reconnects.
- Select **Test inputs** for the live tester.
- Select **Edit** for the active profile, or use its drop-down to make a new one.
- Right-click the connection-status icon to disconnect a wireless controller.

If a controller is connected but absent from this page, see
[Troubleshooting](#troubleshooting).

## 3. Audio Haptics

![Audio Haptics page](docs/images/tour/audio-haptics.png)

Audio Haptics converts captured audio energy into the native DualSense haptic
lane. Its settings are saved in the selected controller's active profile.

1. Turn on **Enabled**.
2. Choose a source: the Windows mix, a render endpoint, a running app and its
   children, or the emulated controller-audio endpoint.
3. Use **Low**, **Medium**, or **High** as a starting gain, then adjust the slider.
4. Choose **Mix** to add audio detail to game haptics, or **Replace** to use only
   the audio-derived feel.
5. Tune bass focus, response, ramp, and fade only after confirming the live input
   meter and status card respond to the source.

**Automatically follow games** can switch to recognized games while retaining the
selected app as a fallback. For an app-session source, **Play app through
controller** can also send that app to the controller speaker or AUX headset.

The important status is the output status, not only the moving input meter. If it
says **Capturing, but not reaching the controller**, read the reason shown beside
the source. Direct Bluetooth haptics serves the Windows mix and render endpoints;
an app session or controller-audio source needs a virtual output path.

For transport details and controller-speaker setup, see
[DualSense Bluetooth Audio and Haptics](docs/dualsense-bluetooth-audio-haptics.md).

## 4. Trigger Lab

![Trigger Lab page](docs/images/tour/trigger-lab.png)

Trigger Lab designs persistent adaptive-trigger effects for L2 and R2 in the
selected profile.

- **Linked** mirrors one effect design across both triggers; **Split** keeps
  independent L2 and R2 designs.
- Each trigger has its own **Active** switch, preset/effect selection, preview,
  reset, game-rumble vibration, and full-pull action.
- The page-level **Enabled** switch controls the lab override. An active lab
  effect overrides adaptive-trigger output arriving from a game.
- The user preset library is stored under the selected Thrum data folder,
  independently of controller profiles. It supports save, rename, delete, JSON
  import, and selected/all export.

The preset library remains available without a connected controller. Profile
controls stay unavailable until Thrum has a compatible selected controller and
active profile.

## 5. Profiles

![Profiles page](docs/images/tour/profiles.png)

Profiles hold mappings and device behavior. The page can create, edit, duplicate,
rename, delete, import, and export profiles. Search filters the profile cards;
double-clicking a card opens it for editing.

When creating a profile, start with the preset closest to the output a game
expects. Xbox 360 is the broad XInput-compatible choice. PlayStation and Switch
outputs are useful when a game or tool expects those device families, but all
virtual outputs still pass through the VIIPER gate.

### Profile editor

![Profile editor](docs/images/tour/profile-editor.png)

The editor has these main sections:

- **Controls** maps controller inputs to controller, keyboard, mouse, macro, or
  unbound outputs. Select a control on the controller map or double-click its row.
- **Special Actions** creates multi-input actions and manages protected Trigger
  Lab effects.
- **Axis Config** tunes stick and trigger dead zones, curves, output limits, and
  motion axes.
- **Lightbar**, **Touchpad**, and **Gyro** contain their device-specific behavior.
- **Audio Haptics** and **Trigger Lab** edit the same profile-backed settings as
  their main navigation pages.
- **Advanced** selects the emulated controller and contains rumble, controller
  audio, latency, compatibility, and output options.

Use **Search settings** to jump to a label in the editor. **Apply** tests changes
without closing; **Save profile** persists them.

![Control remapping dialog](docs/images/tour/remapping-dialog.png)

## 6. Auto Profiles

![Auto Profiles page](docs/images/tour/auto-profiles.png)

Auto Profiles switches controller profiles when a matching application or window
is active.

1. Use **Add programs** to import Steam games, Start-menu entries, a directory,
   another executable, or a window-title rule.
2. Select the rule and assign a profile per controller, or choose **All** for the
   same profile across controllers.
3. Use the device and window-title fields to narrow a rule when necessary.
4. Select **Save**. Rules can be duplicated, removed, and moved up or down when
   their matching order matters.

The page also offers options to return to the Default profile when no rule
matches, show debug messages, and choose how display switching is handled.

## 7. Output Slots

![Output Slots page](docs/images/tour/output-slots.png)

Output Slots shows the physical input assignment and requested/current virtual
device for each of Thrum's eight slots. It also reports the XInput slot number and
whether the output is active.

Select a row, then use **Plug** or **Unplug** to control a virtual device manually.
A **Dynamic** reservation follows demand; a **Permanent** reservation keeps the
chosen virtual-device type assigned to that slot. Select **Accept** after changing
the reservation.

Read the banner above the table before pressing **Plug**:

- **New virtual controllers are blocked** means the driver is missing,
  unvalidated, or not yet acknowledged. The banner gives the exact remedy.
- **Virtual audio endpoints are off** means ordinary controller output remains
  available but the separate audio-class opt-in is disabled.

Existing attached devices continue running when the policy changes. The gate is
authoritative for every new allocation, even if a button remains clickable.

## 8. Diagnostics

Diagnostics collects a read-only, redacted snapshot in the background. **Refresh**
does not install, start, stop, attach, or change anything. It reports:

- usbip-win2 driver-gate identity and trust state;
- VIIPER helper/backend reachability and redacted holdings;
- HidHide installation and whether Thrum is whitelisted;
- audio endpoint defaults and virtual-audio consent state;
- output-slot assignments; and
- per-connection controller link-health counters.

Use **Copy full report** when opening an issue. The report omits the HidHide
whitelist and other unnecessary private data, but review it before sharing it.

## 9. Settings

![Settings page](docs/images/tour/settings.png)

The top of Settings contains everyday options: physical-controller hiding,
startup behavior, notifications, Bluetooth disconnect, charging, logging,
appearance, and update checks.

**VIIPER Virtual Controller Support** contains:

- guided **Install / Repair VIIPER** and status refresh;
- the read-only usbip-win2 identity/trust card and full diagnostic report;
- backend ownership and holdings, with a guarded stop action only when Thrum can
  prove it is safe to offer;
- the virtual-controller acknowledgement; and
- the separate virtual audio/microphone opt-in.

Expand **Advanced settings** for OSC, UDP motion data, language, compatibility,
process, monitor, device-registration, and maintenance controls. The **Utils**
area opens the data/profile folders, manual settings import, Windows controller
tools, HidHide, driver setup, update checks, and the changelog.

## 10. Log

Log shows live service events by time, subsystem category, and message. Search
within the current buffer, select a category, or enable **Warnings only** to
narrow it. Double-click a row for its detailed message.

Use **Copy selection** for a small excerpt or **Export** for the complete current
buffer. **Clear** affects only the visible log buffer; it does not change profiles
or controller settings.

## Troubleshooting

### A connected controller does not appear

1. Confirm the footer button says **Stop**, which means the service is running.
   If it says **Start**, select it.
2. Open **Settings > Device options** and enable the physical controller family.
3. Close DS4Windows or any other mapper that may already own the device.
4. Open **Diagnostics** and check the HidHide card. If HidHide is installed but
   Thrum is not whitelisted, use the HidHide configuration client from Settings.
5. Reconnect the same controller by USB or Bluetooth and check **Log** for its
   detection/access message.

### A game sees both the physical and virtual controller

This is double input. Install/configure HidHide, enable **Hide DS4 Controller** in
Settings, and make sure Thrum is present in HidHide's application whitelist. Do
not disable the physical HID device in Device Manager as a routine workaround.

### A virtual controller is blocked

Open **Output Slots** and read the banner, then open the VIIPER section in
Settings. Use **Install / Repair VIIPER** when the guided status calls for setup or
repair, then use the driver card's **Re-check** after an external change. If the
package is unvalidated, the card lists what could not be confirmed. Thrum
intentionally fails closed rather than accepting an unknown or merely signed package.

If the recognized driver is experimental, read and accept **Use virtual
controllers (experimental kernel driver)**. The virtual-audio checkbox is not a
general-purpose fix and should remain off unless you specifically need those
endpoints and accept their separate risk.

### Audio Haptics has input but no controller output

1. Read the Audio Haptics status and the reason appended to the selected source.
2. For the driver-free route, connect a DualSense/Edge over Bluetooth and select
   the Windows mix or a render endpoint.
3. For an app-session or controller-audio source, confirm a permitted virtual
   output exists in **Output Slots**.
4. Open **Log** and search for audio/haptics stream start, source resolution, or
   health messages.

### Reporting a problem

Copy the Diagnostics report, export the relevant Log buffer, and include the
controller model, connection type, profile output type, and exact steps. Crash
dumps can contain kernel memory: follow [SECURITY.md](SECURITY.md) and never post
a dump publicly.

## Data and profiles

The recommended data location is `%APPDATA%\Thrum`. Portable mode stores the same
configuration beside `Thrum.exe`. **Settings > Advanced settings > Utils > Open
data folder** opens the active location, and **Import settings...** can run the
safe importer later.

Profiles can be exported for backup or sharing. Treat imported profiles as
configuration from another person: review their mappings, special actions,
launch-with-profile settings, and emulated output before using them.

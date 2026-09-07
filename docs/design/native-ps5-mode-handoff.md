# Native PS5 mode — design handoff summary

Source: Claude Design project "Thrum Native PS5 Prototype" (2026-09-06), files
`Thrum Native PS5 Prototype.dc.html`, `Thrum Interaction Map.dc.html`,
`Thrum Artboards.dc.html` and `design_handoff_thrum_native_ps5/README.md`.
The HTML there is a design reference, not code; this page records the parts
of the contract the implementation is held to. Implemented 2026-09-06 (see
`docs/dev/PLAN-PROGRESS.md`, same date).

## The control

One card on Overview, "Native PS5 mode", shown only when the selected physical
pad is a DualSense or DualSense Edge. Other pads keep the "Emulated device"
combo in that slot. A 36×20 switch mirrors the profile's emulated device; a
badge, a "Games see …" line, one explanatory line and one muted line describe
the state; a CTA button appears in states 2 and 3.

## N1 state table

| # | Name | Predicate | Badge | Border |
|---|---|---|---|---|
| 1 | Off | not DualSense/Edge output; driver known; backend running; consent given | "Off", muted | neutral |
| 2 | Needs setup | driver Missing or Unverified or not checked, or backend not running | "Not installed" muted / "Unverified" danger / "Backend not running" / "Checking" | neutral |
| 3 | Needs consent | driver known, `ViiperExperimentalAcknowledged` false | "Experimental - known package", warning | neutral |
| 4 | On | output is virtual DualSense/Edge; not 5 or 6 | "On", success | success |
| 5 | On, haptics via Bluetooth | 4 ∧ wireless ∧ Audio Haptics enabled with a system or endpoint source ∧ audio consent off | "On · haptics via Bluetooth", success | success |
| 6 | On, haptics via virtual pad | 4 ∧ USB ∧ `AllowExperimentalAudioEndpoints` | "Experimental, unverified", warning, plus `AudioClassSummary` | warning |

Rule that must hold: **Bluetooth + Audio Haptics + no virtual audio endpoint =
state 5 = green.** A working default must never read as broken.

The switch position is the profile's output type even when prerequisites are
missing (states 2/3 with the switch on): the card shows the setup badge and
CTA beside it rather than lying in either direction.

## Setup sheet (N2)

Slide-over, 560 px, full height, scrim behind, Esc closes at any step, focus
returns to the switch. Four steps on a left rail (done = success fill + tick,
current = accent ring, step 4 dashed = optional). Rail rows are not focusable.

1. **Install backend** — driver-status badge/headline/reasons/identities from
   the Settings card model; Install / Repair (elevated, may be cancelled at
   UAC: "VIIPER setup was canceled at the Windows administrator prompt. No
   changes were made." shown inline); Recheck. Continue enabled only when the
   package is known and the backend answers. Copy carries
   `NotProductionApprovedNote`. Never proceeds on an unverified package.
2. **Read and accept** — the full `AcknowledgementBody`, scrollable and a
   keyboard stop; a checkbox that is never pre-checked and saves immediately
   through the existing Settings setter; Continue enabled only when ticked.
3. **Turn on** — waits for a DualSense when none is connected (steps 1 and 2
   need none); otherwise pad · transport · profile summary and three bullets
   (emulated device, Hide DS4 Controller on, games see a virtual DualSense on
   the next connection). After success: "On" badge, HidHide warning if
   applicable, and the optional step 4 link.
4. **Haptics over the virtual pad** (optional, USB only) — the audio-endpoint
   consent with its every-time disclosure (`BuildAudioClassBody`),
   `AudioClassSummary` in warning colour, the default-audio-device takeover
   warning (N4) and a disabled "Restore my previous default device" (N5,
   "Not available in this build").

## Turn-off

A confirmation dialog: games will see the type the profile used before (N8,
`<PreviousOutputContDevice>`, fallback Xbox 360); the change applies on the
next connection; a virtual pad already running is not torn down by this
switch or by the consent settings - Unplug on Output Slots does that. Buttons:
Turn off / Keep on / Open Output Slots.

## Bindings

- Turn on: `MainWindowsViewModel.SelectedOutputController = ViiperDualSense`
  (Edge for an Edge) - the setter records the previous type and raises
  `OutputControllerChosen`, which runs `EnsureReadyWithPrompt` then
  `EnsureExperimentalAcknowledgedWithPrompt`; then the profile is flushed and
  `SettingsViewModel.HideDS4Controller = true` with the service restart the
  checkbox performs.
- Consent: `SettingsViewModel.ViiperExperimentalAcknowledged` /
  `AllowExperimentalAudioEndpoints` setters only, OneWay checkboxes, never
  pre-checked.
- Card inputs: `Global.OutContType`, `ViiperSetupManager.DriverReadiness`,
  `GetStatus(tryStartServer: knownPackage).Ready` (both read on a worker and
  handed to the view model), the two consent flags, `IsWireless`,
  `audioHapticsSettings[dev].Enabled/Source`, `Global.hidHideInstalled` and
  `DS4Device.CurrentExclusiveStatus`.

## Out of scope for the first implementation

The Controllers-card chip (N7), the wizard fast path (N3), the stat-card slot
swap on Overview, and the handoff's full dark-theme token remap. The seven new
brushes exist in both dictionaries with values that fit the current palette.

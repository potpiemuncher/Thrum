# Thrum

Thrum is a Windows controller application. It reads supported PlayStation and
Nintendo controllers — DualShock 4, DualSense, DualSense Edge, DualShock 3,
Switch Pro, and Joy-Con (left, right, and joined) — applies per-profile
mappings, and presents virtual controllers that games recognise: Xbox 360,
DualShock 4, DualSense, DualSense Edge, and Switch 2 Pro. Virtual output is
provided by the VIIPER backend. Thrum also turns game and desktop audio into
DualSense haptic feedback, forwards adaptive-trigger effects, and adds a
fail-closed driver-safety layer that refuses to run virtual-device features on
a kernel driver package it cannot positively identify.

Two caveats on that paragraph, because it describes intent and the current
release falls short of it in two known ways:

- **Virtual DualSense output did not work in beta 1.** The shipped build asked
  its VIIPER v0.0.6 backend for a device name it no longer registered
  (`unknown device type: dualsense`); Xbox 360 output worked. Beta 2 fixes that
  with the V5-first negotiation and moves the pinned pair to usbip-win2 0.9.8.0
  and this project's build of VIIPER v0.1.2 for it — see
  `docs/viiper-backend-upgrade-path.md` for why a fork build and what was
  validated.
- **The full "plugged-in" DualSense feel is opt-in.** Native PS5 mode gives
  games a virtual DualSense with adaptive triggers and rumble out of the box.
  Game-authored haptics and the pad's speaker additionally need the virtual
  audio endpoints switch (setup step 4), which stays off by default. With it on
  they are relayed to the pad over Bluetooth or USB. Bluetooth has had daily
  use on real hardware; USB has not (issue #65).

## Using Thrum

### What you need

- **Windows 10 version 2004 or later, or Windows 11, 64-bit.** Thrum is
  x64-only.
- A supported controller, connected by USB or Bluetooth.
- Administrator rights **only during setup**, to approve the driver installers.
  Thrum itself runs as a normal user.
- Do not run DS4Windows at the same time. Both apps claim the same controller.

### Install

1. Download `Thrum_<version>_x64.zip` from the
   [Releases](https://github.com/potpiemuncher/Thrum/releases) page.
2. Check it against the SHA-256 on the release page. In PowerShell:
   `Get-FileHash .\Thrum_<version>_x64.zip`
3. Extract it to a folder you own, for example
   `%LOCALAPPDATA%\Programs\Thrum`. There is no installer.
4. Run `Thrum.exe`. The build is not code-signed yet, so Windows may show
   "Windows protected your PC". Choose **More info > Run anyway** only after
   the hash matches.
5. The first-run wizard walks you through the rest: where settings are kept,
   which controller types to support, and the optional virtual-controller
   backend. The [User Guide](USERGUIDE.md#first-run-setup) explains each step.

Virtual controllers (the Xbox 360, DualShock 4 or DualSense that games see) use
an experimental kernel driver. Thrum creates one only after you accept its
notice: tick the box under it in the wizard's backend step, or later turn on
**Settings > VIIPER Virtual Controller Support > Use virtual controllers
(experimental kernel driver)**. Reading your physical controller does not need
any of this.

### What setup may install

| Component | Needed for | Where it comes from |
| --- | --- | --- |
| usbip-win2 (kernel driver) | Virtual controllers | Its signed upstream installer, checked by Thrum before it runs |
| VIIPER backend | Virtual controllers | A pinned release, downloaded to `%LOCALAPPDATA%\VIIPER` |
| HidHide (optional) | Hiding the physical controller from games | Its own installer |
| FakerInput (optional) | Keyboard and mouse output that works in more apps | Its own installer |
| DsHidMini | DualShock 3 only | Its own installer |
| VB-CABLE (optional) | The USB/legacy microphone route only | [vb-audio.com](https://vb-audio.com/Cable/); Thrum does not include it |
| Microsoft Visual C++ Redistributable (optional) | FakerInput output and microphone noise suppression | [Microsoft](https://aka.ms/vs/17/release/vc_redist.x64.exe); most PCs already have it |

Nothing else is needed. The release zip includes the .NET runtime.

### Updating

Thrum checks for a new release once a day and, if there is one, offers to open
the release page. It never downloads or installs anything by itself.

To update: exit Thrum (tray icon > **Exit**), then extract the new zip **over
the same folder**. Settings in `%APPDATA%\Thrum` are kept. If you chose
portable data during setup, your settings are in that folder, so extract over
it rather than into a new folder.

### Uninstalling

1. If you turned on **Settings > Run at startup**, turn it off.
2. Exit Thrum and delete its folder.
3. To remove your settings and profiles too, delete `%APPDATA%\Thrum` (or the
   portable data next to `Thrum.exe`).
4. Remove the drivers you no longer want from **Settings > Apps** in Windows:
   usbip-win2, HidHide, FakerInput. Delete `%LOCALAPPDATA%\VIIPER` to remove
   the backend.

### Privacy

Thrum does not send your audio, microphone, controller input or settings
anywhere. Audio Haptics, speaker and microphone passthrough are processed on
your PC and go only to your controller or to the virtual controller on your PC.

The only thing Thrum fetches from the internet is the list of releases from
GitHub, once a day, to tell you about updates. The network features you can
turn on yourself (OSC, the DSU motion server, OpenRGB) send controller state,
never audio.

One exception to be aware of in the current backend: the VIIPER process
listens for local virtual-USB connections on all network interfaces, and the
first time it starts Windows Firewall asks whether to allow it. **Choose
Cancel (don't allow).** Thrum works the same, because it only uses the
connection on your own PC, and blocking it keeps other computers on your
network from reaching the virtual controller.

### Getting help

- The [User Guide](USERGUIDE.md) covers every page and has a troubleshooting
  section.
- Logs are in `%APPDATA%\Thrum\Logs` (or `Logs` next to `Thrum.exe` in
  portable mode). **Settings > Advanced settings > Utils > Open data folder**
  opens the right place.
- When reporting a problem, include the log, your controller model, USB or
  Bluetooth, and the steps. Read [SECURITY.md](SECURITY.md) first if Windows
  crashed.

## Status

**Pre-release, version 0.9.0-beta.2.** This repository was seeded on 2026-07-25
from the DS4Windows lineage.

The **user-facing identity is rebranded**: the assembly and executable are
`Thrum`, settings live in `%APPDATA%\Thrum`, and the window titles and log
banner say Thrum. The solution and project files are `Thrum.sln`,
`Thrum/Thrum.csproj` and `Thrum.Tests/Thrum.Tests.csproj`. What remains on the
DS4Windows name is *internal*: the `DS4Windows` namespaces, the `DS4WinWPF`
root namespace, and the `<DS4Windows>` root element of the profile file
format, which existing profiles depend on.

Running alongside a real DS4Windows install is not a supported configuration:
both will contend for the same physical controller, and hbashton's DS4Windows
shares the VIIPER and usbip-win2 installs with Thrum while requiring a
different usbip-win2 release, so after Thrum's setup its virtual controllers
stop working.

**Releases are pre-releases**, currently `v0.9.0-beta.2`. Each is a
self-contained win-x64 zip, so it does **not** need the .NET 8 Desktop Runtime
installed. There is still **no installer**: unzip, run `Thrum.exe`, and let its
setup install the two driver-side components.

The build is **unsigned**, so Windows shows "Windows protected your PC". Verify
what you downloaded against the SHA-256 published with the release rather than
trusting or ignoring that warning. `NOTICE.txt` and `COPYING` ship inside the
archive.

Read the release notes before installing: they list what is verified on real
hardware and — deliberately at equal length — what is not.

To publish the same self-contained package from source:

```powershell
dotnet publish .\Thrum\Thrum.csproj -c Release -r win-x64 --self-contained true
```

## User guide

See the [Thrum User Guide](USERGUIDE.md) for the first-run wizard, the ten-page
navigation rail, profiles, Audio Haptics, Trigger Lab, and VIIPER safety gates.

## Lineage and attribution

Thrum continues the DS4Windows line of work and is not a replacement for the
projects it builds on:

- **DS4Windows** — originated by Jays2Kings, carried forward by Ryochan7, then
  Schmaldeo, then hbashton, together with the wider DS4Windows contributor
  community. This repository imports the full history of
  [hbashton/DS4Windows](https://github.com/hbashton/DS4Windows) and tracks it
  as an upstream (see `docs/dev/ADR-0002-upstream-tracking.md`).
- **VIIPER** — the virtual-controller backend, by hbashton
  ([hbashton/VIIPER](https://github.com/hbashton/VIIPER)), GPL-3.0. Thrum
  consumes VIIPER as a pinned release binary and never vendors its source
  (see `docs/dev/ADR-0001-repo-topology.md`).
- **usbip-win2** — the kernel USB/IP driver VIIPER depends on, by vadimgrn
  ([vadimgrn/usbip-win2](https://github.com/vadimgrn/usbip-win2)),
  BSD-2-Clause. It is an **external prerequisite**: Thrum does not contain,
  redistribute, or sign it.
- **HidHide** — an external, optional prerequisite used to hide physical
  controllers from games.
- **Protocol research** — controller-protocol work shared publicly by
  egormanga (SAxense) and awalol (DS5Dongle), among others.

Full third-party attribution is in [`NOTICE.txt`](NOTICE.txt).

## Driver safety

Virtual controllers on Windows require a kernel driver. VIIPER's is
`usbip-win2`, and the Thrum maintainers classify every currently published
release of it as **experimental**, not production-approved.

This is not a formality. A request-lifetime race in `usbip-win2` has been
reproduced and confirmed at source level: virtual **USB audio endpoint**
teardown can overlap in-flight isochronous completions, corrupting kernel heap
and producing bugchecks `0xA` / `0x139`. Controller-only emulation (no audio,
mic, or advanced-haptics endpoints) does not exercise that path. The defect is
filed upstream as usbip-win2 issue #181.

Thrum's response is a **read-only driver diagnostic**, already present in this
tree (`Thrum/DS4Control/Viiper/Validation/`, entry point
`-viiperdriverdiagnostic`). It inspects the installed driver package against a
pinned manifest — exact package versions, INF driver versions, Authenticode
subject — and reports what it finds. It performs no elevation, no device I/O,
and no install or teardown. Its verdict is fail-closed: a package the manifest
does not list is treated as unvalidated, never as acceptable. "Signed" is not
"kernel-safe"; the manifest decides admission, not the signature.

That diagnostic **is** wired into a runtime gate, and has been since Phase 2 —
this paragraph previously said it was future work, contradicting the opening
section of this very file. Virtual-device creation is refused until the
experimental driver is explicitly acknowledged, and the audio-class features
that reach the known race are default-off behind a second flag
(`ViiperExperimentalAcknowledged`, `AllowExperimentalAudioEndpoints`; both
default false). Both were validated in the VM and are exercised on every run.

What remains genuinely future work is **production approval**, which stays
blocked until an upstream release carries the fix. Our fix for the corruption is
merged upstream as usbip-win2 PR #182, alongside the maintainer's own root-cause
fix — but no released build contains either, so every published release is still
classed experimental and the gate still reports `Production approved: no`.

Crash dumps from this ecosystem contain kernel memory. Read
[`SECURITY.md`](SECURITY.md) before reporting a crash, and never attach a dump
to a public issue.

## Building

Requirements: Windows, the
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and Python
3.10+ only if you also want the packaging step. **x64 is the canonical
platform**; VIIPER is x64-only.

```powershell
dotnet restore
dotnet build .\Thrum.sln -c Release -p:Platform=x64
```

Run the test suite:

```powershell
dotnet test .\Thrum.Tests\Thrum.Tests.csproj -c Release -p:Platform=x64
```

## License

Thrum is licensed under the **GNU General Public License, version 3 or later**
(GPL-3.0-or-later). The complete license text is in [`COPYING`](COPYING).

The corresponding source for any Thrum build is this repository at the release
tag that produced it — for the current release, tag `v0.9.0-beta.1` (commit
`8132946`). For a build you made yourself, it is this repository at the commit
you built.

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

## Status

**Pre-alpha bootstrap.** This repository was seeded on 2026-07-25 from the
DS4Windows lineage and has not been rebranded yet — the application still
builds and runs under its internal `DS4Windows` identity (assembly name, data
folder, window title, scheduled-task and IPC names). Renaming that identity is
a dedicated upcoming phase, and until it lands, running this alongside a real
DS4Windows install is not supported.

There are **no releases**. There are no installers, no prebuilt binaries, and
no update feed. The only supported way to run Thrum today is to build it from
source (see [Building](#building)).

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
tree (`DS4Windows/DS4Control/Viiper/Validation/`, entry point
`-viiperdriverdiagnostic`). It inspects the installed driver package against a
pinned manifest — exact package versions, INF driver versions, Authenticode
subject — and reports what it finds. It performs no elevation, no device I/O,
and no install or teardown. Its verdict is fail-closed: a package the manifest
does not list is treated as unvalidated, never as acceptable. "Signed" is not
"kernel-safe"; the manifest decides admission, not the signature.

Wiring that diagnostic into a runtime gate — including default-off, explicitly
acknowledged opt-in for the audio-class features that reach the known race —
is the next phase of work.

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
dotnet build .\DS4WindowsWPF.sln -c Release -p:Platform=x64
```

Run the test suite:

```powershell
dotnet test .\DS4WindowsTests\DS4WindowsTests.csproj -c Release -p:Platform=x64 --filter "Name!=CheckSettingsSave&Name!=CheckWriteProfile&Name!=CheckJaysProfileRead"
```

Those three excluded tests are stale upstream XML snapshot fixtures whose
hardcoded expected output predates fields the current serializer emits; they
fail on a clean checkout independently of any change made here. The fixtures
are regenerated and the filter removed in a later phase.

## License

Thrum is licensed under the **GNU General Public License, version 3 or later**
(GPL-3.0-or-later). The complete license text is in [`COPYING`](COPYING).

The corresponding source for any Thrum build is this repository at the release
tag that produced it. Because there are no releases yet, the corresponding
source is this repository at the commit you built.

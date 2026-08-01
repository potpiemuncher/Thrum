# Contributing to Thrum

Thrum is in pre-alpha bootstrap. The codebase is an imported DS4Windows tree
that has not been rebranded yet, so expect internal names, paths, and strings
that still say `DS4Windows`. That is deliberate and is being handled as a
dedicated phase; please do not open drive-by renaming pull requests.

## Prerequisites

- **Windows.** The application is WPF on
  `net8.0-windows10.0.19041.0`; it does not build or run on Linux or macOS.
- **.NET 8 SDK.** Get it from
  <https://dotnet.microsoft.com/download/dotnet/8.0>.
- **Python 3.10+**, only if you need the packaging step (`utils/post-build.py`).
- Visual Studio 2022 or Rider are convenient but not required; the command
  line below is the source of truth.

**x64 Release is the canonical configuration.** VIIPER is x64-only, and CI
builds and tests x64 Release. A change that only builds in Debug or only on
`AnyCPU` is not done.

## Build and test

```powershell
dotnet restore
dotnet build .\DS4WindowsWPF.sln -c Release -p:Platform=x64
dotnet test .\DS4WindowsTests\DS4WindowsTests.csproj -c Release -p:Platform=x64
```

Run the **full** suite before opening a pull request, not just the tests near
your change. Report failures verbatim; do not summarise them.

## Pull request discipline

- Work on a feature branch; never commit directly to `main`.
- One logical change per pull request. Mechanical refactors and behaviour
  changes go in separate commits so each is reviewable on its own.
- CI must be green. A pull request with a red or skipped required check is not
  ready for review.
- Include tests for anything testable. The existing suites run against fakes
  and interface seams, not hardware — follow that pattern rather than adding
  tests that need a controller plugged in.
- Describe what you verified and how. "Builds fine" is not verification.

## Kernel-driver work is [VM]-only

Anything that installs, uninstalls, attaches, detaches, or stresses the
`usbip-win2` kernel driver — including any change to virtual devices that
expose USB **audio** interfaces, and any teardown-path change — must be
exercised **only inside a disposable Windows test virtual machine**, restored
from a clean checkpoint, with crash dumps configured. Never on a development
or daily-driver machine.

There is a reproduced, source-confirmed request-lifetime race in usbip-win2
(upstream issue #181) that corrupts kernel heap during virtual audio endpoint
teardown and produces bugchecks `0xA` / `0x139`. Controller-only emulation
does not reach it; audio, microphone, and advanced-haptics endpoints do.

Non-negotiable rules:

- Never enable Windows test signing.
- Never weaken Secure Boot, VBS, or Memory Integrity, in any environment.
- Never download and run a driver package without verifying a pinned SHA-256
  **and** its Authenticode signature first. A version floor (`>= x.y.z`) is
  never sufficient.
- Fail closed. If the package cannot be positively identified, treat it as
  unvalidated.

State in the pull request which of these your change touches, and what you ran
in the VM.

## Minimal-diff policy for engine files

Thrum tracks upstream DS4Windows and merges from it regularly; see
`docs/dev/ADR-0002-upstream-tracking.md`. Every line we change in a shared
engine file is a line that can conflict on every future merge.

So: **prefer new files and new types over edits to existing ones.** When you
must touch an existing engine file (for example `ScpUtil.cs`,
`ControlService.cs`, `ViiperOutDevice.cs`, `DualSenseDevice.cs`, or the large
WPF code-behinds), keep the diff minimal and mechanical — add a call site,
add a seam, do not reformat, do not reorder, do not opportunistically clean
up. Large-scale refactoring of inherited files needs to be agreed first.

A smaller delta against upstream is a feature, not a compromise. Improvements
that upstream would want are also offered upstream.

## No personal data in committed content

Nothing committed to this repository — code, docs, tests, fixtures, logs,
reports, commit messages, or pull request text — may contain personal names,
personal email addresses, machine or account names, device serial numbers, or
local filesystem paths (`C:\Users\...`). Sanitise sample logs and diagnostic
output before pasting; the report formatter's user-path redaction is the
standard to match.

Crash dumps are covered separately and more strictly by
[`SECURITY.md`](SECURITY.md): never attach one to a public issue.

Set your git identity to something you are comfortable publishing; a GitHub
`users.noreply.github.com` address is fine and preferred.

## Licensing

Thrum is GPL-3.0-or-later. By contributing you agree your contribution is
licensed the same way.

- **Preserve existing license headers.** Do not strip or rewrite them when
  editing an inherited file.
- **Preserve attribution.** The DS4Windows lineage credits, the VIIPER and
  usbip-win2 credits, and the protocol-research credits in the README and
  `NOTICE.txt` are license and courtesy obligations, not decoration.
- **New third-party code must be recorded.** Adding a dependency, vendoring
  source, or importing an asset means adding a matching entry to
  `NOTICE.txt` in the same pull request, with its license and origin. Do not
  add anything GPL-incompatible.

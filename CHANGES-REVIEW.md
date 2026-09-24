# Pre-release review — change log

One line per change made during the pre-release review, with the reason.
Grouped by review phase. Findings that were reviewed but deliberately *not*
changed are listed in `RELEASE-READINESS.md`, not here.

Baseline: `main` @ `29ce29f` (0.9.0-beta.2).

## Phase 1 — Warnings

### Build warnings (baseline: 11 unique, reported by MSBuild as 19)

- `Mapping.cs`: removed unused static field `macroEndIndex` — CS0414; the value was never read.
- `Mouse.cs`: removed unused fields `currentToggleGyroMouse` / `currentToggleGyroStick` and their resets — CS0414; written in `ResetToggleGyroModes` but never read.
- `TrayIconViewModel.cs`: removed unused field `prevBattery` — CS0414.
- `DS4Device.cs`: removed unused field `outputPendCount` and the commented-out line that referenced it — CS0414.
- `ScpUtil.cs`: `catch (InvalidOperationException e)` → `catch (InvalidOperationException)` in `Load()` — CS0168; the variable was unused.
- `CurrentOutDeviceViewModel.cs`: `Refresh()` now raises `InputSlotNumChanged` like every sibling `*Changed` event — CS0067; the event was declared for the `InputSlotNum` property but never raised. Removing it instead could make a future WPF binding to that property leak its view model.
- `DualSenseDevice.cs` / `DualSenseBluetoothSpeakerPassthrough.cs`: removed the `bluetoothCombinedSpeakerStaleHapticsSilenced` counter and its `staleHapticsSilenced=` log field — CS0649; nothing ever incremented it, so the diagnostics line always printed 0.
- `Translations/Strings.ru.resx`: removed the second `Program` / `RunAs` / `Task` entries — MSB3568; MSBuild already ignored them, so the Russian text users see is unchanged.
- `UdpServer.cs`: `throw ex;` → `throw;` when the DSU socket fails to bind — CA2200; rethrowing the variable discarded the original stack trace.

### Build configuration

- `Thrum.csproj`: turned on the .NET analyzers (`EnableNETAnalyzers` was explicitly `false`) at `AnalysisLevel 8.0`, `AnalysisMode Default`. The stricter levels were measured and deliberately not enabled; see `RELEASE-READINESS.md` → Phase 1.
- `Directory.Build.props`: when `CI=true` (always true on GitHub Actions), compiler, analyzer, MSBuild and NuGet warnings are errors — so zero warnings stays zero. Local builds still only warn.
- `global.json` (new): pins the .NET 8 SDK feature band (`8.0.400`, roll forward within 8.0.x). CI was silently building with the runner's newest SDK (.NET 10), not the documented .NET 8 SDK — that is why CI reported a CS0649 that local builds did not.
- `ci-build.yml`, `release.yml`: `actions/checkout@v4→v5`, `setup-dotnet@v4→v5`, `setup-python@v5→v6` (release: `@v3→v6`), `upload-artifact@v4→v6` — every run printed "Node.js 20 is deprecated" and `punycode` deprecation warnings.
- `duplicate.yml`, `fix-shipped.yml`, `out-of-scope.yml`: `dessant/support-requests@v2→v5` — v2 runs on Node 12. `issue-close-reason: 'completed'` keeps v2's close reason (v5 defaults to "not planned").
- `.github/dependabot.yml`: added the `github-actions` ecosystem (monthly) — only NuGet was watched, which is how the workflow actions drifted onto deprecated Node runtimes.

## Phase 6 — Other (found early)

- `docs/dev/HANDOFF.md`: replaced a local `C:\Users\<account>\...` path with a neutral description — CONTRIBUTING.md forbids account names and local paths in committed content.
- `docs/dev/patches/viiper-0.1.2-usbip-0.9.8.0.patch`: `From:` headers now use the project's GitHub no-reply address instead of a personal email address — same rule. (Both remain in git history; see `RELEASE-READINESS.md`.)
- `ThrumDiagnostics*Tests.cs`: the sample user name in the redaction tests is now `somebody`, as in the other redaction tests, instead of a real first name. Test meaning unchanged.

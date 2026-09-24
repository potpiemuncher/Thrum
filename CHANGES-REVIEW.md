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
- `docs/dev/ADR-0005-code-signing.md`: added a dated amendment — two of its premises are out of date: EV certificates stopped giving instant SmartScreen trust in 2024, and Azure Artifact Signing now accepts individual developers in Canada.

### Warnings, prompts and banners on a normal launch

- `MainWindowsViewModel.CheckDrivers`: no longer opens the "Install or repair VIIPER support now?" box at every launch when VIIPER is not ready — it was forced past the once-per-session guard, raised from a background thread with no owner, and held back controller detection until answered. It still starts an installed backend as before; the install offers remain the wizard, the output-type choice and the refusal when a virtual output is plugged (the design `App.xaml.cs` already documented).
- Update check: "Skip this version" now works — the choice was saved but never read, and opening the dialog erased it, so the update box returned every 24 h. The skip is an exact tag match (`ReleaseChannelPolicy.IsSkippedRelease`, tested) so skipping one beta does not hide the next; choosing Update withdraws it; the manual Check for updates button still shows skipped releases.
- `ControlService.Start`: the usbip port sweep runs only when usbip-win2 is installed — otherwise it logged a WARN ("could not query usbip ports") on every start and resume for everyone without the driver.
- `ControlService.Start`: the "Windows UAC Conflictions" line (every start for every non-admin user, linking to DS4Windows' website) moved to verbose startup diagnostics.
- `ScpUtil.LoadLinkedProfiles`: a missing LinkedProfiles.xml (the normal state until a profile is linked) is no longer logged as "can't be found" at every launch; the two parse-error messages no longer dereference a possibly-null `InnerException`, which would have crashed on a corrupt file.
- `ViiperSetupManager`: a successful setup started from the first-run wizard no longer restarts the app under the open wizard — the restart ran before the first-run marker existed, so the wizard came back while the old process kept starting up. The restart now waits until the main window is shown (`AllowRestartAfterSetup`); during the wizard nothing needs it.
- `App.xaml.cs` first-run bootstrap: creates `Profiles\Default.xml` and slot assignments only where they are missing. First run is inferred from a marker file, so it could be true over surviving settings, and it overwrote the user's Default profile and pointed every controller at it.
- Settings > Driver setup: waits for the elevated setup window asynchronously (the main window showed "Not Responding" for as long as it was open), restarts the controller service afterwards if it was running (it stayed stopped), and says so plainly when the administrator prompt is declined (it was swallowed).
- Overview status card: Audio Haptics with automatic game detection is "Ready" while waiting for a game, not amber "Needs attention" on every launch. Real capture failures still show. **This reverses a choice recorded in PLAN-PROGRESS (2026-09-06)**; the pinned test is updated, and a new test pins the new rule.
- Automatic game audio: a game that refuses capture is reported once and retried with a growing delay (5 s → 5 min); it was retried and logged as a warning every 500 ms (~7,200 lines an hour on Windows 10, where it always fails).
- Audio Haptics source check: an app remembered by path or name is "waiting to start", not a red "select it again" error, and following one app or a detected game is refused up front on Windows builds before 20348 (it needs Windows 11) instead of failing silently. Tests added.
- Native PS5 card: exclusive access counts as hiding the pad, so the "HidHide is not installed, games may also see the physical pad" warning no longer shows on every launch when the pad is already hidden.
- Output Slots: the "Virtual audio endpoints are off" note uses neutral styling when audio is off only because it was never switched on (the default and recommended state). A controller-blocked banner is unchanged, and any other audio refusal still shows as a warning. Pinned test updated.
- `ControlService`: the HidHide "could not be hidden" warning is skipped when an exclusive open already hides the pad; the per-launch "no PlayStation audio interface was created" paragraph and `ViiperOutDevice`'s "starting without its audio interfaces" line go to verbose diagnostics when the only reason is that audio endpoints are off (other reasons still log); the rate-cap line moved to diagnostics; the gate's own refusal log was removed because every caller already logs it (it printed twice per connect).
- DualSense Edge output on a standard DualSense: the "native feedback is not forwarded" notice is information, not a warning, on each connect — the configuration is valid.
- Stick-mouse without FakerInput: the tray toast is gone (it returned at every launch because its flag was per session); the log line stays.
- `DS4Device` / `DualSenseDevice` read loops: when Stop cancels the pending read, the loop exits quietly — it used to log a read-failure warning, send a "kick" report and fire a second device removal alongside Stop's own teardown. A normal power-off, dead battery or out-of-range (timeout or error 1167) now goes to diagnostics, since the removal handler already reports it in plain words; any other read error is still a warning.
- `DS3Device`: idle disconnect cannot work on a DS3, so the idle clock restarts instead of logging "disconnecting due to idle disconnect" on every poll until the pad is touched.
- `HidDevice.ReadSerial`: a controller without a serial number is reported as information in plain words, not a "WARNING: Failed to read serial#" on every connect.
- `AudioHapticsService`: "Audio Haptics requires a DualSense" is information, once per slot per session, not a warning on every profile load.
- `Util.LogAssistBackgroundTask`: a failed background task writes its stack trace to the log file only; the app shows one sentence.
- `DualSenseHapticsStreamer`: the 30-second "BT stream health" telemetry is gated on verbose logging, like Audio Haptics' own telemetry.
- Profile editor: opening it no longer runs the VIIPER readiness probe on the UI thread (and could no longer show the experimental-driver notice) — only a user's change of output type does.

### PowerShell scripts (linter: PSScriptAnalyzer 1.25.0, Warning and Error)

Baseline: 42 warnings across `extras/install-viiper-backend.ps1`, `extras/sign-release.ps1` and `utils/measure-runtime.ps1`; now 0.

- `install-viiper-backend.ps1`: each of the six empty `catch { }` blocks now writes a `Write-Verbose` line saying why the error is ignored — behaviour unchanged (verbose output is off), but the reason is on record. Private helpers renamed to singular nouns (`Get-RunningViiperProcess`, `Stop-ViiperProcess`; only called inside the script). Seven em dashes in comments replaced with ASCII so the file has no non-ASCII bytes (Windows PowerShell 5.1, which runs setup, reads a BOM-less file as ANSI).
- `sign-release.ps1` / `measure-runtime.ps1`: values used inside functions are passed as parameters instead of read from script scope (the analyzer flagged them as unused); the measurement helpers use non-state-changing verbs.
- Suppressions, each justified in the script: `PSAvoidUsingWriteHost` in all three (interactive console scripts: the text is for the person running them, not pipeline output); `PSUseShouldProcessForStateChangingFunctions` for two private helpers of the setup script (it runs as one elevated unit and offers no `-WhatIf`).
- Left as is: four Information-level `PSAvoidUsingPositionalParameters` notes in the setup script (not warnings; changing call style in the VM-validated installer buys nothing).
- `ci-build.yml`: new `lint-scripts` job fails the build on any PSScriptAnalyzer warning.
- `sign-release.ps1`: signs `Thrum.resources.dll` (the satellites are Thrum's own; the comment called them Microsoft's) and `*.ps1` (the setup script runs elevated) by default, and a new `-IncludeUnsignedThirdParty` switch signs bundled third-party binaries that carry no signature. Syntax-checked; the signing path itself still needs a real certificate to run.

## Phase 6 — Other (found early)

- `docs/dev/HANDOFF.md`: replaced a local `C:\Users\<account>\...` path with a neutral description — CONTRIBUTING.md forbids account names and local paths in committed content.
- `docs/dev/patches/viiper-0.1.2-usbip-0.9.8.0.patch`: `From:` headers now use the project's GitHub no-reply address instead of a personal email address — same rule. (Both remain in git history; see `RELEASE-READINESS.md`.)
- `ThrumDiagnostics*Tests.cs`: the sample user name in the redaction tests is now `somebody`, as in the other redaction tests, instead of a real first name. Test meaning unchanged.

## Phase 3 — Performance

- `utils/measure-runtime.ps1` (new): times cold and warm starts and runs an idle or active soak (CPU, private memory, handles, threads, GDI/USER objects) with growth rates, closing Thrum through its own `-command shutdown` — runtime numbers cannot be measured in the review's Linux container, so this gives the owner a repeatable way to measure them on Windows. It needs no admin rights and changes nothing on the machine.

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

## Phase 2 — Code review fixes

Findings come from a full read of the codebase (every file, by 33 reviewers)
followed by independent skeptic verification of every Medium-or-worse
finding. Severity is the verified severity.

### Crashes, hangs and deadlocks

- `ScpUtil.DebouncingMsHasChanged` (Critical): null-safe invoke — saving a profile with a changed debounce value before any controller had connected crashed the app (no subscribers yet).
- `AudioHapticsService` (High): wired USB Audio Haptics output is retired from the thread pool when NAudio reports it stopped. NAudio raises `PlaybackStopped` on its own playback thread and `WasapiOut.Dispose` joins that thread, so the old in-handler dispose made the thread join itself and hang forever while holding the output lock. Confirmed against NAudio 2.2.1's source.
- `DualSenseAudioPassthrough` (High): the replaced capture is disposed after the lock is released — `StopCapture` runs under `syncRoot` and NAudio's `Dispose` joins the capture thread, which could be waiting for `syncRoot` in `Capture_DataAvailable`: a restart or profile change deadlocked. The handler also ignores a buffer from a capture that has just been replaced.
- `ProcessLoopbackWaveCapture` (High): exceptions on its monitor and capture threads after `Dispose` (which joins with a 1.2 s limit, then disposes what they use) are dropped instead of escaping — the `when (not disposed)` filters let them end the whole process.
- `DualSenseDevice.DrainQueuedInputEvents` (High): queued actions run after `eventQueueLock` is released. A profile switch that unplugged the virtual pad waited for VIIPER's feedback callbacks, which call `queueEvent` and need that lock: the input and feedback threads deadlocked mid-game.
- `ControlService` OSC (High): the server callback is wrapped so an unreadable packet (an OSC bundle, a short address, a missing argument, an out-of-range controller number — anything on the network can send one) is dropped and reported once, instead of crashing the app on SharpOSC's thread; `packet as OscMessage` replaces a hard cast. A port already in use or a bad sender address now logs a plain error instead of aborting the controller service start (which left Start/Stop disabled); turning the OSC server off after Stop no longer throws; OSC sends check that the sender actually started.

- Auto Profiles (Critical): scanning a folder skips subfolders the user cannot read (`C:\Program Files\WindowsApps`, a drive's System Volume Information) instead of crashing the app on the first one; a folder that cannot be read at all shows a plain message, and the page is always re-enabled afterwards. Hidden and system files are still scanned, as before.
- Profile editor, Gyro Calibration (Critical): with no controller connected in the slot it now says so instead of crashing.
- Special Actions, Launch Program (Critical): Save with no program chosen asks for one instead of crashing (and no longer stores an empty path). A failed save keeps the editor's controls working; it used to unbind them first.
- Duplicate Profile (High): the name box starts empty each time, an existing profile name (including the source's own) is refused with a message, and a copy failure is reported. It used to reuse the last name, overwrite that profile without asking, list it twice, or crash on a same-name copy.
- Import Profile (High): a file already in the Profiles folder is reported instead of copied onto itself (a crash); replacing an existing profile asks first, reloads any controller using it so the old settings are not saved back over it, and never lists it twice; a copy failure is reported. The profile list refuses duplicate names.
- Export Profile (High): the profile is no longer held open while the dialog is up and left open on Cancel (which made a later save, rename or delete of it fail). The dialog suggests the profile's name.
- Switch Pro and Joy-Con HID writes (High): a write that is still in flight is always cancelled and drained before its stack buffer goes away, and a timeout of 0 (what those drivers pass) waits up to 500 ms. It used to return immediately with the write still pending, reporting failure and letting the kernel complete it into freed stack memory. Needs a check on real Nintendo pads.
- Profile editor, DS3 pitch/roll simulation (Medium): the service restarts once, in order, after a successful save (and only if it is running). It used to queue two overlapping Start/Stop toggles before the save, which could leave the service off.
- Macro recorder (High): closing it with the window's X (or Alt+F4) puts the touchpad mode back and stops recording, like Cancel. It used to leave the touchpad in Passthru (saved into the profile) and, mid-recording, remapping switched off for every controller.
- Trigger Lab presets (High): every save, rename, delete or import re-reads the library file first, and a Trigger Lab shown again reloads it. The main tab and the profile editor each hold a copy, and a change in one used to silently delete presets saved in the other. Tested.
- Overview refresh (Medium): battery and ID changes no longer refresh the Overview from the controller's input thread (the 250 ms UI timer already picks them up); the Native PS5 sheet handler also marshals to the UI thread. The old path could throw on WPF objects off the UI thread, which ends the process.
- VIIPER debugger (Medium): device probes consult the same driver-safety gate as the product and are skipped with the reason when it refuses.
- Profile editor output prompt (Phase 1 follow-up): unbinding the editor (Select Preset) also no longer counts as choosing an output, so it cannot show the VIIPER notice.

- First-run wizard (Medium): choosing a data location no longer writes an empty settings file over an existing `Profiles.xml`. First run is inferred from `Auto Profiles.xml`, so deleting only that file reran the wizard and erased the user's settings. Tested.
- Auto Profiles rules (Medium): a rule whose profile was renamed or deleted is skipped with one plain log line per profile per session. Loading the missing profile used to blank the slot and unplug its virtual controller, retried every second while the program stayed in front.

### Leaks

- `ProfileDTO.SharedSerializer` (High): one cached serializer for profile files. `new XmlSerializer(type, overrides)` generates an assembly .NET never unloads, and one was built per profile load and save, so every profile switch leaked. Tested (instance reuse, and a source guard against building one per call).
- `DS4Device.SetupDebouncer` (Medium): the debounce-changed subscription holds the device weakly, replaces the device's previous one, and removes itself once the device is gone — every connection used to add a closure to a static event that kept each disconnected controller alive for the session.

### Audio output format (wired USB)

- `AudioHapticsService` (High) and `DualSenseAudioPassthrough` (High): samples are encoded against the standard form of the endpoint's `WAVE_FORMAT_EXTENSIBLE` mix format. Its `Encoding` reads Extensible, not IeeeFloat, so USB Audio Haptics wrote int32 PCM into a float32 stream (near-silent, full-scale spikes, NaN) and USB speaker passthrough matched no branch at all and wrote silence. The provider and `WasapiOut` keep the extensible format and its channel mask. The speaker writer also stops allocating a byte array per sample on the audio path. Tested.

## Phase 6 — Other

- `.github/workflows/release.yml`: runs the test suite before building the release package. A release is built from the tagged commit, which may never have been through CI, and used to publish without running any tests.
- `RELEASE-READINESS.md` (new): verdict, before/after measures, decisions needed, manual steps (including code-signing setup), a pre-release test checklist and every finding with its severity and status.

### Found early

- `docs/dev/HANDOFF.md`: replaced a local `C:\Users\<account>\...` path with a neutral description — CONTRIBUTING.md forbids account names and local paths in committed content.
- `docs/dev/patches/viiper-0.1.2-usbip-0.9.8.0.patch`: `From:` headers now use the project's GitHub no-reply address instead of a personal email address — same rule. (Both remain in git history; see `RELEASE-READINESS.md`.)
- `ThrumDiagnostics*Tests.cs`: the sample user name in the redaction tests is now `somebody`, as in the other redaction tests, instead of a real first name. Test meaning unchanged.

## Phase 3 — Performance

- `utils/measure-runtime.ps1` (new): times cold and warm starts and runs an idle or active soak (CPU, private memory, handles, threads, GDI/USER objects) with growth rates, closing Thrum through its own `-command shutdown` — runtime numbers cannot be measured in the review's Linux container, so this gives the owner a repeatable way to measure them on Windows. It needs no admin rights and changes nothing on the machine.
- Game Bar compatibility (High): the Game Bar API probe, which starts a hidden copy of Thrum.exe each time, runs every 150 ms only while the overlay is visible or for 10 s after Thrum opens it, and once a second otherwise; it stops for the session if the API is not present. It used to run every 150 ms for the whole session (thousands of process launches an hour, and a pattern antivirus products flag). Window-based detection still runs on every check. Opening Game Bar with the keyboard may now take up to a second to be noticed.
- Timer resolution on Windows 11 (Medium): Thrum opts out of Windows 11 ignoring its 1 ms timer request while its windows are hidden (its normal state in the tray). 1 ms waits in the VIIPER input-rate limiter, Audio Haptics and the DualShock 4 speaker lanes became ~15.6 ms when minimised. No effect on Windows 10.
- DualShock 4 Bluetooth audio power setting (Medium): the USB selective-suspend values Thrum turns off in the active power plan are put back when no DS4 audio lane is running and when the service stops. They used to stay off permanently, on battery too, for every USB device. The log line now says so. A crash still leaves the setting off.
- Audio Haptics writer (Medium): waits on a high-resolution waitable timer and spins only for the last ~0.75 ms, as the Bluetooth audio pacer does. The old wait yielded in a loop for the last 1.5 ms of every 10.667 ms packet: about 12-14% of a core per controller while Audio Haptics was on, silent or not. Same packet timing.
- Battery tray icon (Medium): only a change of icon is posted to the UI thread. It queued a dispatcher operation (and allocated) for every input report of the primary controller.

## Phase 4 — Audio

- Default playback device changes (High): a new `DefaultRenderEndpointWatcher` (one Windows endpoint-notification callback that only counts changes) lets every capture bound to "the default device" reopen on the new default: Audio Haptics' default "System audio" source, the DualSense and DualShock 4 Bluetooth speaker lanes, the wired DualSense speaker passthrough and the Bluetooth haptics streamer. They used to keep looping back the old device after Windows switched output (headset connected, output picked in the volume flyout), so haptics and the controller speaker went silent while their status still read active.
- Lost audio sources (High): the speaker lanes and the haptics streamer reopen a device capture that stopped on its own (device unplugged, audio service restarted), once a second with back-off, and say so in plain words once per loss. They used to log the raw error and stay silent until the profile was reloaded. App (process) captures are left to their own detection as before.
- DualShock 4 Bluetooth speaker (High): each 8 ms tick reads exactly one tick of loopback audio. It read up to 1024 frames and encoded only 384, discarding the rest and filling other ticks with silence (choppy, crackling speaker audio).
- DualSense Bluetooth headset audio (High): "headset only" audio (packet type 0x96) goes through the paced audio lane like speaker audio. It was treated as a control report, skipping the prime gate and the 10.667 ms schedule, and after each sound could send hundreds of reports a second. Tested.
- Surround playback devices (Medium): the Bluetooth speaker lanes fold quad, 5.1 and 7.1 loopback down to stereo, keeping the centre channel (dialogue) and the surrounds; front channels keep full level, so stereo content is exactly as loud as before, and LFE is left out. They used to keep only front left and right, so on a surround endpoint (many gaming headsets present virtual 7.1) dialogue never reached the controller speaker. Tested.

## Phase 5 — Ready for real users

- Settings files (High): `Profiles.xml`, each profile file and `Auto Profiles.xml` are written to a temporary file, flushed to disk and swapped in, keeping the previous version as `<name>.bak`. They used to be emptied and rewritten in place, so a crash or power cut during a save (every settings change, and on exit) left a truncated file. New `.bak` files appear next to them. Tested.
- Damaged `Profiles.xml` (High): if it cannot be read, Thrum loads the `.bak` copy, keeps the damaged file as `Profiles.xml.damaged-<time>`, and says so once in a dialog after the window opens. It used to log one line to the log file, run on defaults (profile assignments and the experimental-driver acknowledgement lost) and overwrite the damaged file on the next save. Tested.
- Log files (Low): each log file is capped at 10 MB (with the existing 7 archives, about 80 MB at most). One long session used to grow a single file without limit.
- Log tab (Low): keeps the most recent 10,000 lines instead of every line for the life of the process; the log file still has everything.
- Crashes (Medium): an unrecoverable error now shows one dialog saying Thrum has to close and where the log file is, after controllers are released. The app used to disappear with no message.
- High-DPI screens (Medium): the main window, the live input tester and the first-run wizard keep their minimum and opening size within the monitor's work area. At 175-200% scaling on a 1080p screen their bottom row (the Start/Stop footer, the wizard's Next button) was below the screen edge, even maximized.
- README (docs): a "Using Thrum" section for end users: requirements, install with hash check, what setup may install and why, updating, uninstalling, privacy (with advice to decline the Windows Firewall prompt for the VIIPER backend, which only needs connections on the same PC), and where logs are.
- NOTICE.txt (docs): paths updated from the pre-rename `DS4Windows/` folder to `Thrum/` (each checked to exist); FakerInput, the Microsoft Visual C++ runtime and VB-CABLE added to the external components Thrum requires but does not ship.

## Owner decisions

Made by the owner on 2026-09-24 from the recommendations in `RELEASE-READINESS.md` (numbers as there).

- #1 Exclusive mode ("Hide DS4 Controller", Native PS5) (High): when another program already has the controller open, Thrum stays in shared mode and says so in the log (and once per controller per session in the tray), naming any well-known controller programs that are running (Steam, DS4Windows, DSX, reWASD and others). It used to relaunch itself with a UAC prompt, mid-game, to restart the device, and blocked the device thread for up to 30 s while waiting. Windows cannot say which process holds a HID device without administrator rights, so the named programs are likely holders, not certain ones. Running Thrum as administrator still restarts the device as before. The old shared-mode warning was never shown (its call was commented out). Tested.
- #2 Driver Setup and the Welcome dialog (High): Driver Setup opens the setup window without administrator rights, and its HidHide and FakerInput buttons open the vendors' release pages. It used to run the whole window elevated, which let it start the user-writable `viiper.exe` as administrator, and downloaded the installers to `%TEMP%` and ran them elevated with no integrity check. VIIPER setup still asks for administrator rights itself.
- #3 Run at startup (High): the "Task" mode is gone; Run at startup always uses the Startup-folder shortcut. The Task mode registered a highest-privilege logon task that ran `task.bat` from the user-writable program folder. An existing task is removed and replaced with the shortcut the next time Thrum runs with administrator rights (which the task itself provides at the next sign-in); until then the log explains how to delete it in Task Scheduler. `-runtask` now starts Thrum normally instead of running the task. The settings card with the Program/Task choice and its UAC shield is gone.
- #4 VIIPER setup script (High): in a signed release (Thrum.exe carries a signature), setup runs only if the script is signed by the same publisher, and runs under `-ExecutionPolicy AllSigned`, so PowerShell checks the script in the elevated process. The setup window first says what to answer if PowerShell asks whether to trust the publisher, and waits with the reason if the script did not run. Unsigned builds keep `Bypass`. `extras/sign-release.ps1` already signs the script. Tested.
- #5 First-run wizard (High): the Backend step shows the experimental-driver notice in full with an unticked checkbox; ticking it records the same consent as the Settings switch and saves at once. Setup used to install the backend without ever asking, so a new user's virtual controller was refused on every connect until they found the switch in Settings. Tested.
- #6 .NET 10 (High): Thrum, its tests and the icon tool target .NET 10 (LTS, supported until November 2028); `global.json`, CI and the release workflow use the .NET 10 SDK. The release bundles its own runtime, and .NET 8's support ends on 2026-11-10, after which users would stop getting its security fixes. Two source changes were needed: the `System.Memory` package reference is removed (it is part of the framework, and the .NET 10 SDK rejects the redundant reference), and `ProfileEditor.xaml.cs` aliases WPF's `ContextMenu` and `MenuItem`, because .NET 10's Windows Forms brings back types with those names. The unpacked package grows from about 195 MB (527 files) to 216 MB (536 files), and the zip from about 79 MB to 86 MB. Build: 0 warnings.

## Owner testing (2026-09-25)

Found by the owner running the CI build on their PC.

- First-run wizard consent checkbox (bug from decision #5): the box's label read "System.Windows.Controls.TextBlock"; the checkbox style shows its content as text, so the label is now passed as text.
- Update prompt (Medium): a build of 0.9.0-beta.2 made outside the release workflow (a CI artifact or a local build) has no release marker file and was offered beta.2 as an update at every start. A build whose version is the offered release is now treated as that release. Tested.
- HidHide (Low): the Native PS5 card's link said "Open the HidHide client" even when HidHide is not installed (it opens the download page then); it now says "Get HidHide" in that case. The first-run wizard's Backend step also offers HidHide as an optional download when it is missing; setup used to never mention it. Tested.
- Overview Active Profile box (Low): it was wider (196 px) than its card leaves room for (180 px), so its right edge was cut off; it now fills the card.
- First-run Backend step (Low): with VIIPER and usbip-win2 installed it read "VIIPER server not running" and offered "Install / Repair", although the step deliberately does not start the server; it now says VIIPER is installed and starts with Thrum, and the button reads "Repair VIIPER". Tested.
- Missing setup script (Low): the message now names the file and says antivirus software may have removed it (with where to look), instead of only "could not find the bundled VIIPER setup script".

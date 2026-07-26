# Upstream delta — hbashton/DS4Windows `5d2724a..8a2b715`

- **Date:** 2026-07-26
- **Merge cycle:** the first one under [ADR-0002](ADR-0002-upstream-tracking.md)
- **`upstream-track`:** `5d2724a` → `8a2b715` (fast-forward, no rewrite)
- **Scope of upstream's change:** 2 files, +214 / -23. Everything upstream did in
  this window is VIIPER installer hardening. Nothing else in the tree moved.

This document exists because the merge itself was cheap and the *analysis* is
the point: the four commits land squarely on top of plan task **2.4**, which was
written before they existed.

---

## 1. What upstream actually changed

| Commit | Subject | Files | Substance |
|---|---|---|---|
| `db21d7e` | Harden VIIPER installer against conversion and task registration failures | `extras/install-viiper-backend.ps1` | Version-string parsing made total; logon-task registration made survivable and given a `schtasks.exe` fallback; the usbip version probe wrapped in try/catch. Also **added `Alia5/VIIPER` as a second release source**. |
| `fac5467` | Use only hbashton VIIPER repo in installer source lookup | same | Reverts the `Alia5/VIIPER` half of the previous commit. |
| `3937d26` | Make VIIPER setup close running viiper before registration | `install-viiper-backend.ps1`, `ViiperSetupManager.cs` | New `Stop-ViiperProcesses` / `Get-RunningViiperProcesses`; atomic install now **fails closed** if a running backend cannot be stopped; registration step stops the backend first; failure dialog explains the running-process cause. |
| `8a2b715` | Improve VIIPER install completion behavior | `ViiperSetupManager.cs` | Replaces the "setup finished successfully" message box with a log line plus an automatic application restart (`RestartDs4Windows`). |

### Commit-by-commit detail

**`db21d7e` — parsing and task-registration robustness.**

Three independent hardenings:

1. `ConvertTo-VersionFromObject` replaces two bare `[Version]$text` casts. The
   registry filter also stops assuming `DisplayName` is a string — a `REG_MULTI_SZ`
   or absent value used to make `-match` throw out of the whole probe. This is
   straightforwardly better than what we had and we keep it verbatim.
2. The whole `Get-UsbipInstalledVersion` call is now wrapped so a probe failure
   degrades to "unknown version" instead of aborting setup.
3. `Register-ViiperRunTask` extracts the logon-task registration, catches a
   failure of the `ScheduledTasks` module, and falls back to `schtasks.exe`.
   A total failure is now a yellow log line rather than a thrown exception — the
   install completes without the task. See §4: this pulls in the opposite
   direction from our own constraint.

The `Alia5/VIIPER` addition in this commit is the same fork-leftover class of
problem as [issue #8](https://github.com/potpiemuncher/Thrum/issues/8) — the
installer would have fallen back to the *parent* project's releases, which do not
carry the audio-only device types or the V4 atomic audio/haptics frames we
negotiate. Upstream caught it themselves 16 minutes later.

**`fac5467` — source narrowed back to `hbashton/VIIPER` only.**

Directly useful to us and worth stating plainly: the bundled script will not
silently install a foreign VIIPER build. It is the installer-side counterpart to
the runtime fix we shipped in 2.4b. The two together close both halves of the
"wrong VIIPER" exposure *except* for a backend a user installs by hand.

**`3937d26` — the genuinely valuable one.**

Before: `Install-ViiperAtomically` did a best-effort
`Get-Process viiper | Stop-Process -Force -ErrorAction SilentlyContinue`, slept
300 ms, and then attempted `[IO.File]::Replace` — which fails with a sharing
violation if the process is still holding the image, leaving the install in the
`catch` path. After: `Stop-ViiperProcesses` retries up to 12 times, escalates to
`taskkill /T /F` from the third attempt, logs each PID it stops, and **returns
false rather than lying**; the caller throws with an actionable message. The
registration step got the same treatment. This is a real fail-closed improvement
in a path that previously failed open, and it is upstream's, not ours. We keep
it unchanged.

One caveat for task 2.4 to reconcile, not a merge problem: `Stop-ViiperProcesses`
kills **every** `viiper.exe` on the machine, including a backend another consumer
is using. Our 2.4b runtime policy is the exact opposite — it refuses to stop a
backend it did not start or that is hosting anything. The script's behaviour is
defensible (an install is an explicit, elevated, user-initiated act, and you
cannot replace a running image otherwise) but the two policies should be stated
together somewhere a reader will find them.

**`8a2b715` — auto-restart after a successful install.**

Upstream's intent is sound: after VIIPER becomes ready the app needs to re-probe,
and a restart is the blunt way to get there. The implementation has two defects,
both described in §3.

---

## 2. Merge result

`git merge --no-ff upstream-track` produced **zero textual conflicts**. Every
file the task brief flagged as high-risk — `ScpUtil.cs`, `ProductInfo.cs`,
`App.xaml.cs`, `StartupMethods.cs`, `Directory.Build.props`,
`.github/workflows/*`, `Translations/*.resx`, the rest of `DS4Control/Viiper/` —
was untouched upstream. In `ViiperSetupManager.cs` our 2.4b additions and
upstream's edits landed in disjoint regions; in `install-viiper-backend.ps1` our
changes are branding-only and upstream's are functional, so git combined them
cleanly.

A clean textual merge is not the same as a correct one. The real conflicts in
this cycle were **semantic**, and are listed next.

Divergence budget (ADR-0002 §5) after the merge: **11,987** added lines outside
`docs/` and `*.md`, against an alarm threshold of ~15,000. No review triggered.

---

## 3. Semantic conflicts and how each was resolved

### 3.1 `RestartDs4Windows` hardcodes `DS4Windows.exe` — **ours wins**

Upstream wanted: `Path.Combine(Global.exedirpath, "DS4Windows.exe")`, plus four
user-visible strings naming DS4Windows.

We want: every identity string derived from `ProductInfo` (plan task 1.1, ADR
none — it is the whole point of Phase 1). Our executable is `Thrum.exe`, so
upstream's literal makes `File.Exists` false, the restart silently degrades to a
log line, and the log line names a product that does not exist here.

**Resolution:** kept upstream's shape, replaced the composed path with
`Global.exelocation`. That is strictly better than composing
`ProductInfo.ExeBaseName + ".exe"`: it is the executable *actually running*, so it
also survives a portable copy under a different filename and the junction/Scoop
case `exelocation` already resolves — a case upstream's own code comment says it
cares about. The four strings now interpolate `ProductInfo.ProductName`. The
private method is renamed `RestartApplication`.

Eight `DS4Windows` occurrences entered the tree with the merge; the resolution
removes exactly those eight. Sweep total is back to the pre-merge figure (§6).

### 3.2 `{logPath}` in a non-interpolated string fragment — **upstream defect, fixed**

`3937d26` split the failure message into three concatenated fragments and put the
`$` prefix only on the first. The third contains `{logPath}` and is shown to the
user literally. One character; fixed in this merge because it is broken text on a
path we display, not because it is in scope for 2.4.

Reported upstream-worthy. **[EXT]**

### 3.3 The auto-restart cannot work as written — **taken as-is, defect recorded**

Not resolved here, deliberately: this PR is a merge plus analysis, and changing
the restart's ordering would be a behaviour change to upstream's new feature. It
is recorded so it cannot be lost.

`RestartApplication` starts the replacement process **before** shutting the
current one down:

```
Thread.Sleep(2000) → Process.Start(exe) → Dispatcher.BeginInvoke(Shutdown)
```

The new instance reaches `App.OnStartup` in a few hundred milliseconds. The old
instance is still alive: `CleanShutdown` waits up to 8 s for
`rootHub.Stop`/`ShutDown`. So the new instance finds the single-instance event
still open, signals the old one, and exits (`App.xaml.cs`, the
`EventWaitHandleAcl.TryOpenExisting` block). The old instance then finishes
shutting down. Net effect in the common case: **the user is left with no
application running** after a successful VIIPER install, where before the merge
they got a success dialog.

It is worse than that in our tree specifically. The restart is reached from
`InstallerProcess_Exited`, immediately after `GetStatus(tryStartServer: true)` —
which may have *started the backend and recorded ownership*. `CleanShutdown` then
calls `StopOwnedBackendOnExit`, which correctly stops a backend we own and that
is hosting nothing. So the sequence can end with no app **and** no backend, right
after the install whose whole purpose was to make the backend available.

Two things make this survivable in the meantime: the path only runs when the
bundled installer is launched from inside the app and exits 0 with a ready
status, and the user can simply start the app again. It must be fixed before any
release. The fix is ordering (shut down first, have a detached helper or the
installer script start the replacement), and it belongs to whoever picks up the
installer work — see §5.

### 3.4 `Stop-ViiperProcesses` vs. our ownership policy — **both kept, documented**

Covered in §1. No code change; §4 and task 2.4 own the reconciliation. Worth
recording that our ownership record is safe against it: `ViiperOwnedBackend`
matches on (process id, start time), so after the script kills a backend we own,
`TryResolve()` returns null and the exit path logs "left running" and does
nothing. No stale-PID kill is possible.

---

## 4. Task 2.4, requirement by requirement

Requirements as written in the plan. "Upstream" below means the merged state of
`extras/install-viiper-backend.ps1` at `8a2b715`.

| # | 2.4 requirement | Verdict | Detail |
|---|---|---|---|
| 1 | Pinned exact usbip-win2 release URL | **Already satisfied upstream** | Line 396: `.../releases/download/v.0.9.7.7/USBip-0.9.7.7-x64.exe`. Hardcoded, no "latest" resolution. |
| 2 | + SHA-256 of that download, checked before execution | **Still ours** | There is no `Get-FileHash` anywhere in `extras/`. `Invoke-Download` checks only that the file is non-empty. The pinned digest `51620FA5…F185FEA` exists in our notes and in `driver-packages/`; nothing consumes it. |
| 3 | Authenticode subject verification **before** execution | **Still ours** | No `Get-AuthenticodeSignature`, no `WinVerifyTrust`. The installer is downloaded and run with `/S` on the next line. Expected subject: Cloudyne Systems (Scheibling Consulting AB). |
| 4 | Post-install validation of the **actually-installed package pair** | **Still ours** | `Get-UsbipInstalledVersion` reads the FileVersion of `usbip2_ude.sys` (or an uninstall-registry `DisplayVersion`). The Extension filter `usbip2_filter.sys` is never looked at, so "the pair" is never validated — and a version number is not a validation anyway (Part 3 rule 2: floors are never sufficient; the manifest decides admission). `db21d7e` hardened the *parsing* of this check, which is a genuine improvement to a check that answers the wrong question. Our `ViiperDriverGate` already answers the right one; 2.4 wires it in. |
| 5 | No silent acceptance of a newer unlisted release | **Not satisfied — and it is two problems, not one** | **usbip-win2:** the download URL is pinned, but the *acceptance* test is `$usbipVersion -ge [Version]"0.9.7.7"`, so an already-installed 0.9.7.8 (our known-risk baseline) or anything later is accepted silently and the install is skipped. **VIIPER:** `Get-ViiperAssetUrl` walks the 20 most recent releases and takes the first matching asset from the newest non-draft one. Whatever hbashton publishes next gets installed, unpinned and unverified. `fac5467` fixed the *repository* but not the *version*. |
| 6 | Atomic install with `.previous` rollback retained | **Partially satisfied** | `Install-ViiperAtomically` (pre-existing at our import base) uses `[IO.File]::Replace(new, current, backup)` with a copy-back on failure — genuinely atomic. But on success the verification block **deletes** `viiper.exe.previous` (lines 449–452), so rollback exists only inside the install window. "Retained" in the plan means retained afterwards; that part is still ours. `3937d26` strengthened the surrounding fail-closed behaviour (see §1) and we keep it. |
| 7 | Log every decision to `install.log` | **Largely satisfied for what it does; extends with the rest** | `Write-SetupLog` appends to `%LOCALAPPDATA%\VIIPER\install.log` with a timestamp, and upstream added several new lines this window (per-PID stop, stop-attempt exhaustion, task-registration fallback and failure, version-probe failure). The gaps are the decisions that do not exist yet: no digest, signature, package-pair or version-pin decision is logged because none is made. Whoever adds 2–5 adds their log lines. |

**Net:** of the seven, one is fully satisfied upstream, two are partially
satisfied (and upstream improved both this window), and four are still entirely
ours. Upstream's four commits do not reduce 2.4's scope much — but they do change
its *shape*, because 2.4 now has to be written on top of `Stop-ViiperProcesses`,
`Register-ViiperRunTask` and `ConvertTo-VersionFromObject` rather than the code
the task was drafted against.

---

## 5. The `RunVIIPER` question

**Yes — upstream's script still registers the `RunVIIPER` logon task, and this
window made it register harder.**

Precisely, the merged script creates **two** independent autostart mechanisms:

1. **Line 428–436, `viiper.exe install`** — writes the `HKCU\…\Run` value
   `VIIPER`. `3937d26` made this step *more* mandatory: it now stops every
   running backend first, and if it could not and the registration then fails, it
   throws with a message telling the user to close `viiper.exe` and run
   Install/Repair again. Previously a non-zero exit just threw a generic error.
2. **Line 438–444, `Register-ViiperRunTask $viiperPath "RunVIIPER"`** — an
   at-logon, `RunLevel Highest`, interactive task running `viiper.exe server`.
   `db21d7e` added a `schtasks.exe /Create /F /TN RunVIIPER /SC ONLOGON /RL
   HIGHEST /IT` fallback for when the `ScheduledTasks` module path fails, so the
   registration now succeeds in cases where it previously would have aborted the
   whole install.

Both are registered unconditionally, with no opt-out, on every install and every
repair.

Consequences for our plan, stated exactly:

- **Task 2.4's removal is bigger than deleting one block.** It is not "drop the
  `Register-ViiperRunTask` call": the `viiper.exe install` invocation must go or
  be replaced too, and that invocation is currently load-bearing for upstream's
  new error handling. Removing it means deciding what, if anything, "Registering
  VIIPER" still does. If the answer is "nothing", the step and its
  `$registrationSafeToRun` logic disappear together, and `Stop-ViiperProcesses`
  is then only needed by `Install-ViiperAtomically`.
- **Neither entry starts the backend with `--update-notify none`.** The task
  action is `viiper.exe server`, and `viiper.exe install` writes a Run value with
  no such flag. A backend started by either mechanism therefore has the issue #8
  updater fully enabled — it polls `Alia5/VIIPER`, and its "Update Now" pipes a
  remote script into an elevated PowerShell. Our 2.4b fix covers backends *we*
  spawn and nothing else; that is exactly why 2.4b surfaces these two entries in
  Settings with one-click removal. Removing the registrations closes the hole at
  the source.
- **`Start-AndVerifyViiper` (line 321) has the same gap** and is not an autostart
  entry at all: the script's own verification step starts `viiper.exe server`
  without the flag. Pre-existing at our import base, untouched by upstream,
  untouched by 2.4b — but it means running the bundled installer today leaves an
  update-nagging backend running until it is restarted. 2.4 should add the flag
  here regardless of what it decides about autostart.
- **Mergeability cost.** Our 5.3/2.4 removal will delete or rewrite lines that
  upstream has just actively worked on, which makes it the most likely place for
  a future merge conflict. ADR-0002 §4's advice applies: prefer a shape that
  keeps upstream's functions intact and changes only whether they are *called*
  (for example, a parameter defaulting to no-autorun), so the next upstream
  change to `Register-ViiperRunTask` still merges.

Nothing about this is implemented in this PR. It is reported so 2.4 can be
re-scoped before it is started.

---

## 6. Effect on the rest of Phase 2

| Task | Affected? | Why |
|---|---|---|
| **2.1** Wire `ViiperDriverGate` into readiness | **No change to the design; one new call site.** | Upstream did not touch `GetStatus` or the readiness checks. But `8a2b715` added a second consumer of readiness — `InstallerProcess_Exited` now branches on `refreshed.Ready` to decide whether to restart the app — so the four-state enum has to keep a usable boolean "ready" projection, and the restart decision should key off the new states rather than the old boolean. Worth a line in 2.1's design; not a redesign. |
| **2.2** Settings driver-status card | **No change.** | Nothing upstream touched the UI. |
| **2.3** Experimental gating + disclosure | **No change.** | `PlayStationFeatureOutputPolicy` and `ViiperOutDevice` untouched. |
| **2.4** Harden the installer | **Materially affected.** | See §4 and §5. Re-scope before starting. |
| **2.4b** Backend lifecycle ownership | **Conclusions hold; two are reinforced.** | Nothing upstream weakened the ownership record, the census policy, the console-break stop, or the `--update-notify none` spawn (§7 verifies each). The open follow-up recorded in 2.4b — "our own script still registers `RunVIIPER`" — is now confirmed as still true *and* strengthened upstream, so the Settings autostart detection we shipped is not a transitional nicety; it will keep firing for anyone who runs the bundled installer until 2.4 lands. `fac5467` is the installer-side complement to our issue #8 fix. |
| **2.5** Runtime guardrails | **No change.** | The `usbip.exe` Program Files resolution upstream has is untouched. |

Nothing upstream did in this window changes Phase 1, 3, 4, 6 or 7. Phase **5.3**
gains the §5 finding: the installer constraint is now "remove two mechanisms,
one of which upstream made mandatory", not one.

---

## 7. Verification

**Invariants (all re-checked after the merge).**

| Invariant | Result |
|---|---|
| Backend spawned with `--update-notify none` (issue #8) | **Intact.** `ViiperBackendLifecycle.cs` untouched by the merge; `ViiperSetupManager.StartServer` still delegates to `ViiperBackendSpawn.BuildServerStartInfo`; the three argument/environment assertions still pass. |
| Stop-on-exit ownership logic | **Intact.** `StopOwnedBackendOnExit`, the (pid, start-time) ownership record, the census policy and `App.CleanShutdown`'s call site are all in the merge's untouched region. |
| `StopViiperBackendOnExit` round-trips | **Intact.** String-proxy DTO property and its four round-trip tests pass. |
| Satellite resolver registered from the module initializer | **Intact.** `[ModuleInitializer] internal static void Install()` in `SatelliteAssemblyResolver.cs`; `SatelliteAssemblyResolutionTests` pass. |
| `ProductInfo` values still Thrum | **Intact.** `ProductName`/`ExeBaseName` = `Thrum`, `StartupTaskName` = `RunThrum`, `ReleaseOwnerRepo` = `potpiemuncher/Thrum`. |
| No reintroduced `DS4Windows` identity strings | **Restored.** The merge added 8 occurrences (7 in `ViiperSetupManager.cs`, 1 in the installer script); §3.1 removed exactly those 8. `git grep -ic ds4windows` totals **1715** on both `main` and this branch. A targeted anchor sweep (`DS4Windows.exe`, `.lnk`, `RunDS4Windows`, `ds4windows_log`, `.resources.dll`, both pack-URI forms, `DS4Windows_IPC`, `%APPDATA%\DS4Windows`, `DS4Windows.app`, `DS4Updater`) returns only the pre-existing, documented residue: historical-context comments, and the `DS4Updater` mention in the custom-exe-name help string across 22 translations, which the 1.8 sweep left on purpose because it names a different product. |
| Import wizard + decline marker | **Intact.** `ImportPlanner.DeclineMarkerFileName = "import-declined.txt"` and its write/read sites unchanged; `SettingsImportTests` pass. |

**Build:** `dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64` — **0
errors**, 17 warnings, all pre-existing (unused fields, the three duplicate
`Strings.ru.resx` keys).

**Tests:** full suite with the CI filter — **626 passed / 626 total, 0 failed**.
No delta against the 2.4b baseline: upstream added no tests and removed none, and
its two changed files have no test coverage upstream or here. The known stale
snapshot fixture `AppSettingsTests.CheckSettingsSave` remains excluded by the
filter and remains stale for the reason recorded in the 2.4b entry.

**Not verified, and cannot be here:** the installer script itself. Running it
installs the usbip-win2 kernel driver, which Part 3 rule 1 puts behind a TESTENV
checkpoint. Everything in §4 and §5 is source-level reading of the merged script,
not observed behaviour. When 2.4 runs the [VM] pass, the §3.3 restart defect and
the two autostart registrations should be observed directly rather than inferred.

---

## 8. Worth reporting upstream **[EXT]**

Three items, all in `8a2b715`/`3937d26`, none of them Thrum-specific:

1. **The auto-restart cannot restart** (§3.3) — the replacement process is
   started while the single-instance event is still held, so it exits
   immediately and the user is left with nothing running. Affects upstream
   identically; their single-instance guard is the same code.
2. **`{logPath}` rendered literally** in the setup-failure dialog (§3.2) — a
   missing `$` on the third string fragment.
3. **`Start-AndVerifyViiper` and both autostart entries start the backend
   without `--update-notify none`** (§5), which leaves the `Alia5/VIIPER` updater
   live. This is the installer-side half of the report already drafted for issue
   #8, and `fac5467` shows upstream already agrees with the premise.

Per the plan's contribution sequence these are offered, not depended on.

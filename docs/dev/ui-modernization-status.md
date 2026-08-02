# UI modernization close-out

Phase 4 acceptance sweep, measured from `phase4/acceptance-sweep` after the Settings card
conversion (`be4ceb9`). This replaces the task 4.1 inventory measured at `main @ 1be04bd`.
Every main page and every non-theme XAML surface under `DS4Windows/DS4Forms` now has one of the
two required dispositions: **modernized**, with the landing PR or pre-4.1 groundwork named, or
**logged with a reason**.

This is a source-and-verification close-out. It does not convert the separate rendered-surface
pass into a result: the VM pass is still in progress, as recorded under [Rendered verification](#rendered-verification).

## Counting method

- **Bridge refs** counts references matching `Bridge*Style` in the relevant XAML subtree or
  backing control. It is an adoption signal, not a claim that every reference is a card.
- **GroupBox** counts raw opening `<GroupBox` elements. This intentionally still counts the 21
  Profile Editor containers that PR #46 preserved and restyled through
  `ProfileEditorCardGroupBoxStyle`.
- The surface inventory contains all 37 non-theme `.xaml` files under `DS4Windows/DS4Forms`.
  `MainWindow.xaml` is represented by its ten navigation rows; its other modal/control XAML files
  are listed separately below.

The shared shell is now 851 lines with 21 keyed styles/templates, 113 `DynamicResource`
references, 11 `StaticResource` references, and no literal hex colours. Theme brush parity is
auto-discovered by `ThemeResourceTests` from the shell's `DynamicResource` bindings.

## Main navigation pages

For an extracted control, the counts come from that control. For an inline page, they come from
the page's `TabItem` subtree in `MainWindow.xaml`.

| page | backing | Bridge refs | GroupBox | final disposition |
|---|---|---:|---:|---|
| Overview | `ControllerOverviewControl.xaml` (493 lines) | 8 | 0 | **Modernized** — pre-4.1 card shell, completed by PR #41. |
| Controllers | inline in `MainWindow` | 3 | 0 | **Modernized** — pre-4.1 controller cards, with action parity completed by PRs #41 and #45. |
| Audio Haptics | `AudioHapticsControl.xaml` (512) | 2 | 0 | **Modernized** — PR #44 polished capture choice, meter and card presentation. |
| Trigger Lab | `TriggerLabControl.xaml` (133) | 10 | 0 | **Modernized** — PR #44 added the preset workflow and completed the page presentation. |
| Profiles | inline + `DupBox` | 9 | 0 | **Modernized before 4.1 (no Phase 4 PR)** — the card toolbar/list layout remains appropriate; no Phase 4 wrapper was needed. |
| Auto Profiles | `AutoProfiles.xaml` (423) | 2 | 0 | **Modernized before 4.1 (no Phase 4 PR)** — its rule list and editor already occupy the two useful card regions. |
| Output Slots | `OutputSlotManagerControl.xaml` (107) | 3 | 0 | **Modernized before 4.1 (no Phase 4 PR)** — already a complete three-card status/action surface. |
| Diagnostics | `DiagnosticsControl.xaml` (97) | 9 | 0 | **Modernized** — PR #39 added the six-card diagnostics page. |
| Settings | inline in `MainWindow` | 60 | 0 | **Modernized** — this acceptance sweep converted the five surviving sections; OSC and UDP remain flat because the whole set is already behind the collapsed Advanced disclosure. |
| Log | inline in `MainWindow` | 8 | 0 | **Modernized** — PR #42 added the card toolbar, filtering, category tags and copy action. |

## Remaining controls and dialogs

The zero-ref rows are not omissions. Each is either a deliberately retained compatibility
surface or a focused control/dialog where adding card padding and another visual boundary would
not clarify the task.

| file | lines | Bridge refs | GroupBox | final disposition |
|---|---:|---:|---:|---|
| `About.xaml` | 890 | 0 | 0 | **Logged** — still a simple Hotkeys/License about box; most of its line count is static GPL text, so card work remains low value. |
| `AxialStickUserControl.xaml` | 62 | 0 | 0 | **Logged** — embedded single-axis editor; a card would duplicate its parent's section boundary. |
| `BindingWindow.xaml` | 608 | 0 | 1 | **Logged** — the deliberately dense mapping chooser needs its canvas width; the one Extras group isolates optional rumble/macro controls and card padding would make the primary task worse. |
| `ChangelogWindow.xaml` | 21 | 0 | 0 | **Logged** — read-only content host; no additional section exists to card. |
| `ColorPickerWindow.xaml` | 14 | 0 | 0 | **Logged** — single picker host; the card idiom adds no hierarchy. |
| `ControllerReadingsControl.xaml` | 261 | 0 | 0 | **Logged** — retained only as the Profile Editor compatibility surface; PR #45 supplied the modern user-facing tester instead of rewriting it in place. |
| `ControllerRegisterOptionsWindow.xaml` | 123 | 0 | 1 | **Logged** — focused device-registration dialog; its one conditional Joy-Con group is the useful boundary for type-specific options. |
| `ControllerTesterControl.xaml` | 446 | 4 | 0 | **Modernized** — new live tester in PR #45. |
| `ControllerTesterWindow.xaml` | 11 | 0 | 0 | **Modernized** — intentionally thin host for the PR #45 tester. |
| `DupBox.xaml` | 39 | 0 | 0 | **Logged** — compact duplicate-profile prompt; no second content region. |
| `FirstLaunchUtilWindow.xaml` | 79 | 0 | 0 | **Logged** — PR #43 removed its startup call site; restyling the superseded window would not affect the first-run experience. |
| `FirstRunWizard.xaml` | 242 | 45 | 0 | **Modernized** — new seven-stage card-shell flow in PR #43. |
| `ImportSettingsDialog.xaml` | 51 | 0 | 0 | **Logged** — compact import offer still used from Settings; a card would wrap the only prompt. |
| `LanguagePackControl.xaml` | 24 | 0 | 0 | **Logged** — one embedded selector already sits inside the Settings Advanced card. |
| `LightbarMacroCreator.xaml` | 42 | 0 | 0 | **Logged** — focused colour/macro editor; no additional grouping to express. |
| `LogMessageDisplay.xaml` | 20 | 0 | 0 | **Logged** — single message-detail host. |
| `PluginOutDevWindow.xaml` | 34 | 0 | 0 | **Logged** — single output-device choice. |
| `PresetOptionWindow.xaml` | 58 | 0 | 0 | **Logged** — compact preset confirmation/choice. |
| `ProfileEditor.xaml` | 2,821 | 17 | 21 | **Modernized** — all four slices landed in PR #46; the 21 raw GroupBoxes intentionally retain their children and names while a local template supplies card chrome. |
| `RecordBox.xaml` | 101 | 0 | 0 | **Logged** — specialized input-capture control whose key grid is already the hierarchy. |
| `RecordBoxWindow.xaml` | 17 | 0 | 0 | **Logged** — thin host for `RecordBox`. |
| `RenameProfileWindow.xaml` | 28 | 0 | 0 | **Logged** — one-field rename prompt. |
| `SaveWhere.xaml` | 35 | 0 | 0 | **Logged** — PR #43 removed its startup call site and replaced the decision in the wizard. |
| `SpecialActionEditor.xaml` | 551 | 0 | 0 | **Logged** — dense split trigger/action editor with no obsolete GroupBox chrome; cards would fragment one modal workflow without reducing complexity. |
| `StickCalibrationWindow.xaml` | 33 | 0 | 0 | **Logged** — focused calibrator linked from PR #45; one instruction/action surface needs no card. |
| `SwipeProfilesEditor.xaml` | 38 | 0 | 0 | **Logged** — compact allowed-profile list. |
| `TouchButtonUserControl.xaml` | 14 | 0 | 0 | **Logged** — single embedded touch binding. |
| `UpdaterWindow.xaml` | 26 | 0 | 0 | **Logged** — single progress/status surface. |
| `ViiperDebuggerWindow.xaml` | 64 | 0 | 0 | **Logged** — developer-only diagnostic control grid behind verbose logging; card chrome adds no task hierarchy. |
| `WelcomeDialog.xaml` | 72 | 0 | 0 | **Logged** — PR #43 replaced its onboarding role; the retained Settings/`-driverinstall` route is a small one-purpose installer dialog. |

## GroupBox close-out

The raw census before this sweep was **28**: Settings 5, Profile Editor 21, Binding Window 1 and
controller registration 1. It is now **23**:

| file | count | disposition |
|---|---:|---|
| `ProfileEditor.xaml` | 21 | Modernized by PR #46; the elements remain to preserve names, children and behavior while the local template renders them as cards. |
| `BindingWindow.xaml` | 1 | Untouched dialog exception: optional Extras rail in a width-sensitive mapping chooser. |
| `ControllerRegisterOptionsWindow.xaml` | 1 | Untouched dialog exception: conditional Joy-Con-only options in a focused registration dialog. |

No main navigation page contains a `<GroupBox>`. The two rows above are the only untouched
GroupBox exceptions; the other 21 grep hits are the deliberately preserved, already-modernized
Profile Editor containers.

## Verification posture

The per-PR unit and negative-control record is retained here so close-out does not imply that a
green final suite is the only evidence Phase 4 produced.

| landing | surface | automated result | negative-control evidence |
|---|---|---|---|
| PR #39 | Diagnostics | 909/909, 10 new tests; x64 build clean | Trap guards were removed one at a time; reviewer also reintroduced the MAC-bearing read. The paired tests failed and each mutation was restored. |
| PR #41 | controller cards | 918/918, 9 new tests; x64 build clean | Three failures observed and restored: missing dark-theme brush, missing Identify visibility gate, and removed `NoOutputData` capability exclusion. |
| PR #42 | Log | 929/929, 11 new tests; x64 build clean | Three failures observed and restored: wrong VIIPER classification, inverted severity predicate and case-sensitive search. |
| PR #43 | first run | 946/946, 17 new tests; no-incremental x64 build 0 errors | Moving the pristine-state sample and bypassing the non-pristine import gate each failed its intended assertion, then were restored. |
| PR #44 | Trigger Lab + Audio Haptics | 957/957, 11 new tests; no-incremental x64 build 0 errors | Removing schema-version rejection and accepting a vanished endpoint each failed its intended assertion, then were restored. |
| PR #45 | live input tester | 974/974, 17 new tests; no-incremental x64 build 0 errors | Zeroing anti-deadzone geometry and reversing the drift comparison each failed its intended assertion, then were restored. |
| PR #46 | Profile Editor | 983/983 after four slices; x64 Debug XAML and x64 Release builds 0 errors | Reversing default-section disclosure and offsetting a reset default each failed its intended assertion, then were restored. |
| acceptance sweep | Settings + close-out | untouched baseline and final suite 983/983; theme parity 2/2; x64 Debug XAML and no-incremental x64 Release builds 0 errors | No brush key was introduced, so the auto-discovered parity test was run without fabricating a missing-theme mutation. |

The Settings `x:Name` audit is also clean. Baseline and current are 107 declaration occurrences,
106 distinct names (the extra occurrence is the commented legacy `updPortNum`), and 52 distinct
names referenced by `MainWindow.xaml.cs`; 0 referenced baseline names are missing, 0 names were
removed or renamed, and 0 were added. The final builds retain the same 17 known warnings.

## Rendered verification

Every implementation PR above explicitly recorded that its changed surface was not rendered in
that PR. The separate VM UI pass is **in progress**, with evidence assigned to
`vm-validation-reports/phase4-ui-pass-20260802/`. No completed report from that folder exists in
this branch at close-out, so this document does not claim theme, layout, keyboard, live hardware,
audio, haptics or controller results from it.

Phase 4's source acceptance criterion is closed: every page/dialog is either modernized or
logged above with a current reason. The VM folder remains the honest evidence boundary for the
rendered-surface debt until that independent pass finishes.

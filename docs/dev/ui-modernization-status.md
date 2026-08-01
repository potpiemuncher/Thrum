# UI modernization status

Phase 4, task 4.1. An inventory of every page, control and dialog against the card-based shell
(`Themes/BridgeShellStyles.xaml`), so tasks 4.2–4.8 are scoped from what the tree actually
contains rather than from the plan's estimate.

Measured against `main` @ `1be04bd`. Counts are from the current XAML, not from upstream's
starting point.

## Method, and what the numbers mean

"Adoption" is counted as references to a `Bridge*Style` key, cross-checked against the number of
`<GroupBox>` elements — upstream's idiom, and what the card styles replace. Neither number alone
is a verdict: a 14-line colour-picker host needs no card, and a page can reference one style and
still be substantially unmodernized. Every row below carries a disposition, not just a count.

## What the shell provides

`BridgeShellStyles.xaml` (838 lines) defines **20 keys**: card and card-list items, controller
card and sidebar items, section title/description, described checkbox, primary/secondary buttons,
status badge and badge text, inline banner and headline, profile combo box, the main/profile tab
controls and their items, the editor navigation item, and the trigger-pair icon.

It follows the theme properly — **110 `DynamicResource` references against 11 `StaticResource`**,
and exactly one hardcoded colour (see findings).

## The nine navigation pages

All nine exist and are wired into the modern nav shell. Adoption behind them varies.

| page | backing | inline lines | cards | GroupBox | status |
|---|---|---|---|---|---|
| Overview | `ControllerOverviewControl` (474) | 8 | 5 refs | 0 | **adopted** |
| Controllers | inline in `MainWindow` | 119 | 0 | 0 | plain layout, no card idiom |
| Audio Haptics | `AudioHapticsControl` (493) | 4 | 2 refs | 0 | **partial** |
| Trigger Lab | `TriggerLabControl` (84) | 4 | 1 ref | 0 | **partial**, but the page is thin |
| Profiles | inline + `DupBox` | 260 | 2 | 0 | **partial** |
| Auto Profiles | `AutoProfiles` (423) | 5 | 2 refs | 0 | **partial** |
| Output Slots | `OutputSlotManagerControl` (107) | 5 | 3 refs | 0 | **adopted** |
| Settings | inline + `LanguagePackControl` | 548 | 5 | **5** | **mixed** — see below |
| Log | inline in `MainWindow` | 31 | 0 | 0 | plain list, no filters yet (4.8) |

**Settings is the mixed one.** Five cards coexist with five surviving `GroupBox` sections —
`RunAs`, the OSC server, the UDP server, and `Utils`. That is the page where upstream's
"nothing removed, only relocated under Advanced" rule has the most left to apply, and it is
548 lines inline in `MainWindow.xaml`.

## Controls and dialogs

Grouped by what should happen to them, not by size alone.

**Substantial and unmodernized — real work:**

| file | LOC | GroupBox | note |
|---|---|---|---|
| `ProfileEditor.xaml` | **2681** | **21** | The single largest surface in the app and the least modernized. Task 4.5. |
| `About.xaml` | 890 | 0 | Large but it is an about box; low user value, cheap to leave. |
| `BindingWindow.xaml` | 608 | 1 | Dense mapping dialog, reached constantly from the editor. |
| `SpecialActionEditor.xaml` | 551 | 0 | Dense form. |
| `ControllerReadingsControl.xaml` | 261 | 0 | **Task 4.4 rewrites this anyway** — do not modernize separately. |
| `ControllerRegisterOptionsWindow.xaml` | 123 | 1 | Also the second-largest cluster of device-type branching (12 hits). |

**Small enough that the card idiom would add nothing** — a single control, a prompt, or a
confirmation: `RecordBox` (101), `FirstLaunchUtilWindow` (79), `WelcomeDialog` (72),
`ViiperDebuggerWindow` (64), `AxialStickUserControl` (62), `PresetOptionWindow` (58),
`ImportSettingsDialog` (51), `LightbarMacroCreator` (42), `DupBox` (39), `SwipeProfilesEditor`
(38), `SaveWhere` (35), `PluginOutDevWindow` (34), `StickCalibrationWindow` (33),
`RenameProfileWindow` (28), `UpdaterWindow` (26), `LanguagePackControl` (24), `ChangelogWindow`
(21), `LogMessageDisplay` (20), `RecordBoxWindow` (17), `TouchButtonUserControl` (14),
`ColorPickerWindow` (14).

Two of these are **replaced rather than restyled** by task 4.7: `WelcomeDialog` and
`FirstLaunchUtilWindow`. `StickCalibrationWindow` gets folded into 4.4 as an entry point.

## Groundwork already in place

Three things the plan treats as work to be done are already built, which is the main scoping
result of this audit.

**The capability policy exists and is used.** `ControllerUiCapabilityPolicy.cs` defines
`ControllerUiCapabilities.For(InputDeviceType)`, returning artwork name, feedback label, audio
header, microphone toggle label, and flags for controller-audio settings, DualSense hardware
controls, adaptive triggers and the mute button. It is consumed by **four** view-models —
`ControllerListViewModel`, `MainWindowsViewModel`, `MappingListViewModel`,
`ProfileSettingsViewModel` — and covered by **8 tests**.

Task 4.2 says to "extend it rather than scattering `if (deviceType…)` in views". That premise
already holds: of the device-type branching in the UI layer, 14 hits are *inside the policy
itself*, where they belong. Only three sites branch outside it —
`ControllerRegDeviceOptsViewModel` (12), `ProfileEditor.xaml.cs` (5) and `MainWindowsViewModel`
(2) — and the first of those is a single specialised dialog.

**Theme parity is already enforced by a test.**
`ThemeResourceTests.ThemeDefinesEveryBrushTheShellStylesBindTo(string theme)` is parameterised
over the themes and asserts that every brush the shell styles bind to exists in each one, and
`DefaultThemeLoadsBridgeShellStylesOnFreshConfiguration` checks the styles resolve on a clean
config. The Phase 4 acceptance criterion "theme parity checked by extended `ThemeResourceTests`
(light + dark keys for all new styles)" is therefore satisfied *structurally* — new styles are
covered automatically as long as they bind brushes by key rather than hardcoding.

**Task 4.3's data layer exists.** `ViiperDriverStatusViewModel` and
`ViiperBackendStatusViewModel` were built in Phase 2 and already back the Settings driver card.
The diagnostics page needs to aggregate and present them, not derive them.

## Findings

**1. One hardcoded colour breaks theme-following, in the selected state.**
`BridgeShellStyles.xaml:389` sets the controller selector card's selected background to a literal
`#253881D8` (14 %-alpha blue), while every sibling setter in the same trigger block — including
the `IsMouseOver` background and both border brushes — uses `DynamicResource`. So the *selected*
state, the one that matters most for telling the user which controller they are configuring, is
the one state that will not adapt between light and dark. The existing theme test cannot catch it
because it verifies that referenced brush keys exist, and a literal references no key. One-line
fix: add a themed `SelectedCardBackground` brush to both themes and bind it.

**2. `ProfileEditor.xaml` is the whole of task 4.5 and then some.** 2,681 lines with 21
`GroupBox` sections and zero cards — more unmodernized surface than every other dialog combined.
The plan's risk note about god code-behinds applies here, and **both code-behinds it names have
grown since the plan was written**: `ProfileEditor.xaml.cs` is now **3,003** lines (plan: 2,997)
and `MainWindow.xaml.cs` is **2,957** (plan: 2,649, so +308). The plan's advice — prefer additive
view-models, do not attempt an MVVM rewrite mid-phase — therefore applies more strongly than when
it was written, not less. Worth treating 4.5 as its own multi-session task rather than part of a
sweep.

**3. `ControllerReadingsControl` should not be modernized in place.** At 261 lines with no card
adoption it looks like a candidate, but task 4.4 replaces it with the live input tester. Styling
it first would be thrown away.

## Re-scoped estimate for 4.2–4.8

The plan budgets 6–10 sessions for Phase 4. That still looks right in total, but the distribution
is uneven and two tasks are cheaper than they appear:

| task | revised view |
|---|---|
| 4.2 controller cards | **Cheaper than scoped.** The capability policy and its tests exist; this is extending a tested seam plus card XAML, not building the policy. |
| 4.3 diagnostics page | **Medium.** Both status view-models exist; the work is aggregation, the redacted "Copy full report", and plain-language remedies. This is also where Phase 3's leftovers surface (orphaned-backend affordance, audio guard state). |
| 4.4 live input tester | **Largest greenfield item.** Replaces `ControllerReadingsControl` outright. |
| 4.5 profile editor | **Largest overall.** 2,681 lines, 21 GroupBoxes, 2,997-line code-behind. Its own task. |
| 4.6 trigger lab + audio haptics | Medium; `TriggerLabControl` is thin (84 lines) so the preset store is most of it. |
| 4.7 first-run flow | Medium; replaces two small dialogs rather than restyling them. |
| 4.8 log tab polish | **Small.** 31 inline lines today; filters, search and category tags. |

Suggested order: **4.3 → 4.2 → 4.8 → 4.7 → 4.6 → 4.4 → 4.5**, i.e. diagnostics first because it
has the most existing scaffolding and the highest support value, and the profile editor last
because it is the biggest and benefits from the card patterns the earlier tasks settle.

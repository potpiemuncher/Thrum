# Rebrand smoke checklist

The manual pass for Phase 1. Everything here needs the running application, a
real Windows session, or a second product installed side by side, which is why
none of it is a unit test. Run it before calling the rebrand accepted, and again
before the first release build.

**Conventions.** `<install>` is the folder the application runs from.
`%APPDATA%\Thrum` and `%LOCALAPPDATA%\Thrum` are the data folders in the normal
(non-portable) mode. "Fresh state" means those two folders do not exist. No
absolute paths appear in this document on purpose: substitute your own.

**Before you start:** back up `%APPDATA%\DS4Windows` if a real DS4Windows
install is present. Nothing here is supposed to modify it — item 2 exists
specifically to prove that — but a checklist that tells you to point a new build
at your only copy of your profiles should also tell you to copy it first.

---

## 1. First run: the import offer, accepted

**Preconditions:** fresh state. A real DS4Windows configuration exists in
`%APPDATA%\DS4Windows` with at least: `Profiles.xml`, `Auto Profiles.xml`, and
two or more files in `Profiles\`. Note the file count.

**Steps**

1. Launch the application.
2. At the save-location question, choose the AppData option.
3. The import dialog appears. Read the summary list.
4. Press **Import**.

**Expected**

- The dialog's title carries the product name; the summary names the profile
  count and one line per other kind of file found, and the counts match what is
  actually in `%APPDATA%\DS4Windows`.
- After Import, startup continues to the main window without a second
  save-location or first-launch prompt.
- `%APPDATA%\Thrum` now contains the same file names as the source, including
  the `Profiles\` folder.
- The Profiles tab lists the imported profiles by name; the profile that was
  selected in DS4Windows is selected here.
- `thrum_log.txt` contains a line reporting the copied / already-present /
  failed counts, and the failed count is zero.
- Keyboard only: Tab reaches both buttons, Enter activates Import, Alt+I and
  Alt+F work. Verify this on a second fresh run rather than backing out of this
  one.

## 2. The import source is untouched

**Steps**

1. Before item 1, record the source: file names, sizes, and last-write times of
   everything under `%APPDATA%\DS4Windows`.
2. Repeat the comparison after item 1.

**Expected** Identical in every respect. No file added, removed, renamed, or
rewritten — the last-write times must not move either. If a real DS4Windows
install is present, launch it afterwards and confirm it still starts with its
profiles intact.

## 3. First run: the import offer, declined and remembered

**Preconditions:** fresh state, same source as item 1.

**Steps**

1. Launch, choose AppData, and press **Start fresh**.
2. Let the application finish starting, then exit it.
3. Launch again.

**Expected**

- Run 1 continues into the ordinary first-run path (device selection, a Default
  profile is created). Nothing from the source appears.
- `%APPDATA%\Thrum\import-declined.txt` exists.
- Run 2 does **not** show the import dialog, and never will again while that
  marker file exists.
- Repeat step 1 pressing Escape instead, and again closing the window with the
  title-bar X, on fresh state each time: both count as declining and both write
  the marker.

## 4. Portable mode never offers

**Preconditions:** fresh state; a source configuration present in
`%APPDATA%\DS4Windows`.

**Steps** Launch and choose the **program folder** option at the save-location
question.

**Expected** No import dialog, in this run or any later one. Configuration is
written next to the executable and `%APPDATA%\Thrum` is not created for
settings. Confirm the application otherwise works normally: create a profile,
restart, and see it come back.

## 5. Imported profiles load, with legacy output types normalized

**Preconditions:** item 1 completed, using a source that contains at least one
profile saved by an older DS4Windows version (an output controller type of
`X360` or `DS4` rather than a VIIPER one is the interesting case).

**Steps**

1. Open each imported profile in the profile editor.
2. Check the output controller selection on each.
3. Save one profile and reopen it.

**Expected** Every profile opens without an error dialog and without a log
entry about invalid XML. Legacy output types show as their current equivalents
rather than as blank or as an unknown value. A saved-and-reopened profile keeps
its settings. Nothing in the log mentions a failed migration.

## 6. Command-line IPC on the new names

The `-command` client finds the running instance through a memory-mapped file
holding the window class name, then `FindWindow` on the window **title**. All
four object names and the title moved with the rebrand, so this item proves the
two halves still agree with each other.

**Steps**

1. With the application running, from `<install>` run
   `Thrum.exe -command query.1.apprunning`.
   (The query verb takes a controller index: `query.<device#>.<property>`.
   `apprunning` ignores the index but still requires one in range.)
2. Run `Thrum.exe -command cycle` and watch the main window's start/stop state.
3. With the application still running, launch `Thrum.exe` again with no
   arguments.
4. Finish with `Thrum.exe -command shutdown`.

**Expected**

- Step 1 returns the running instance's value rather than hanging for its
  ten-second timeout. A timeout means the result event or the result MMF name
  does not match between the two halves.
- Step 2 toggles the service in the already-running window.
- Step 3 does not start a second instance: the existing window comes to the
  front, and only one process remains in Task Manager.
- Step 4 closes the running instance.

## 7. Side-by-side with a real DS4Windows install

**Preconditions:** a real DS4Windows install, working, with its own
configuration. Do not run both with a controller connected unless you intend to;
one exclusive-access holder is enough to confuse the other. The point of this
item is process identity, not input.

**Steps**

1. Start DS4Windows. Confirm it is running.
2. Start Thrum.
3. From `<install>`, run `Thrum.exe -command query.1.profilename`.
4. Run the same command against the DS4Windows executable, from its own folder.
5. Exit one; then the other.

**Expected**

- Both start. Neither reports "already running", and neither steals the other's
  window.
- Two separate tray icons, each with its own tooltip naming its own product.
- Each `-command` call is answered by its own product, returning that product's
  own active profile name. Neither call reaches the other process, and neither
  call times out.
- Logs stay separate: `thrum_log.txt` under this product's folder,
  `ds4windows_log.txt` under theirs.
- Configuration stays separate: editing a profile in one changes nothing in the
  other's folder.
- Exiting one leaves the other running and responsive.

## 8. Language switch loads the satellite assembly

**Steps**

1. Settings, change the UI language to a non-English one that is installed.
2. Observe the interface.
3. Restart and confirm the choice persisted.
4. While in that language, read three strings the 1.8 sweep edited: the
   Settings "check for updates at startup" checkbox, the anti-deadzone tooltip
   in the profile editor, and the auto-profile "turn off temporarily" label.

**Expected** Interface strings change to the selected language. The
corresponding `Lang\<culture>\Thrum.resources.dll` exists under `<install>`.
No log entry about a missing resource assembly, and no page that stays English
while the rest translates — that is the failure mode of a missed
`DefaultAssembly` attribute and it shows up one page at a time.

The three strings from step 4 read normally and name **Thrum** in the middle of
otherwise translated text. No mojibake, no doubled or missing characters around
the product name — the sweep rewrote 24 non-English files, and a broken
encoding shows up exactly there and nowhere else. Arabic and Hebrew are worth
one look each, for right-to-left rendering around the Latin word.

## 8a. The import dialog renders its resource-driven text

**Steps** With a populated `%APPDATA%\DS4Windows` and no `%APPDATA%\Thrum`,
launch and read the import dialog carefully before answering.

**Expected** Title, heading, source path, the found-items list, the footer and
both buttons are all filled in — no blank line, no literal `{0}`. The window
is a fixed 520x360 and does not resize, so check the footer's last line is
visible rather than clipped. **In a non-English UI language this dialog is
still English**, by design: its keys are neutral-only until translators pick
them up.

## 9. Startup entries create and remove under our own names

**Steps**

1. Settings, enable "run at startup" with the **shortcut** option.
2. Check the user's Startup folder.
3. Switch to the **scheduled task** option (requires elevation).
4. Check Task Scheduler.
5. Turn the option off.
6. Check both places again.

**Expected**

- Step 2: a `Thrum.lnk` appears, and it targets this build's executable.
- Step 4: a task named `RunThrum` appears; the shortcut is gone.
- Step 6: both are gone.
- **At no point does anything named `DS4Windows.lnk` or `RunDS4Windows` change.**
  If a real DS4Windows install is present with its own startup entry enabled,
  confirm its entry is still there and still enabled after step 6. This is the
  item that catches a startup-cleanup path that was never scoped to our names.
- `RunVIIPER`, if present, is untouched throughout. It belongs to the backend
  and is shared with other installs.

## 10. HidHide registration uses this executable

**Preconditions:** HidHide installed.

**Steps**

1. Launch the application and connect a controller.
2. Open the HidHide configuration client and look at the application
   whitelist.

**Expected** An entry for this build's `Thrum.exe`, with the path of the folder
it actually runs from. Move the install to a different folder and relaunch: a
new entry appears for the new path. No entry is invented for a name the
application does not have.

## 11. Log file name and location

**Steps** Launch, use the application briefly, exit.

**Expected** `thrum_log.txt` is written, under the data folder in use — the
`Logs` folder in appdata mode, next to the executable in portable mode. No
`ds4windows_log.txt` is created by this build. Rolled archives, if any, are
named `thrum_log_<n>.txt`.

## 12. Tray, theme and window identity

**Steps** Hover the tray icon; open its menu; switch the theme in Settings;
look at the window title bar and the shell header.

**Expected** Tray tooltip, balloon captions, window title and shell header all
read the product name. The theme switch repaints without an unstyled window or
a missing-resource exception. Message boxes raised during the session carry the
product name as their caption.

---

## Recording a run

Note the build (version and commit), the date, the Windows build, and for each
item: pass, fail, or not applicable with the reason. File failures as issues and
link them here rather than annotating this checklist — it is the procedure, not
the results.

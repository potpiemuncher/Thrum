# Third-party licence audit — 2026-07-31

The audit that closes the `TO AUDIT` section opened in `NOTICE.txt` on 2026-07-25 (Phase 0,
deviation 1). This file records **how** each entry was established, so the next person can
re-check a claim without repeating the whole exercise. `NOTICE.txt` is the deliverable; this is
the working.

## Method

The rule was: **the distributed artifact is the authority.** A licence claimed by a README, a
badge, or a third-party aggregator is not evidence about the thing we ship.

1. **What actually ships** was taken from a framework-dependent publish of
   `DS4WinWPF.csproj` (the shape CI and `release.yml` produce), not from the csproj's
   `PackageReference` list — the two differ, because transitive dependencies ship too.
2. **NuGet licences** were read from each package's own `.nuspec` in the local package cache:
   the SPDX `licenseExpression` where present, otherwise the bundled licence file, read in full.
3. **Vendored source** was read from the in-tree files and their headers.
4. **Bundled binaries** have no metadata to consult, so their upstream repositories were queried
   directly through the GitHub API for the licence GitHub itself detects, and the licence file
   was then read to confirm the detection.
5. **GPL compatibility** was checked against the FSF's published licence list rather than from
   memory.

## What the beta.1 publish output actually contained

Third-party assemblies in the package (excluding Thrum's own and the .NET runtime):

```
Concentus.dll                            NAudio.Wasapi.dll
DotNetProjects.Wpf.Extended.Toolkit.dll  NAudio.WinForms.dll
FakerInputDll.dll                        NAudio.WinMM.dll
FakerInputWrapper.dll                    NLog.dll
H.NotifyIcon.dll                         Ookii.Dialogs.Wpf.dll
H.NotifyIcon.Wpf.dll                     rnnoise.dll
HttpProgress.dll                         SharpOSC.dll
ICSharpCode.AvalonEdit.dll               WPFLocalizeExtension.dll
MdXaml.dll                               WpfScreenHelper.dll
MdXaml.Plugins.dll                       XAMLMarkupExtensions.dll
Microsoft.Win32.TaskScheduler.dll        NAudio.Asio.dll
NAudio.Core.dll                          NAudio.dll
NAudio.Midi.dll
```

plus `Microsoft.Windows.SDK.NET.dll` and `System.Management.dll` (Microsoft, MIT), the
`BezierCurveEditor/`, `Resources/`, `ThirdParty/` and `extras/` content directories, and 23
satellite culture folders.

**Two traps worth recording.** `Microsoft.Win32.TaskScheduler.dll` is *not* a Microsoft product —
its `CompanyName` resource reads "GitHub Community" and it is dahall/TaskScheduler. Filtering an
inventory by `Microsoft.*` hides it. And `ICSharpCode.AvalonEdit.dll` and `XAMLMarkupExtensions.dll`
appear in no `PackageReference`; they are transitive, so a csproj-only inventory misses them.
The inventory above is retained as the beta.1 audit baseline.

### Beta 2 delta — issue #72 (2026-08-03)

Issue #72 removed the `WPFLocalizeExtension` package and replaced its load-time resource lookup
with Thrum's clean-room GPL-3.0-or-later `LocExtension`. That also removes the transitive
`XAMLMarkupExtensions` package. A fresh self-contained win-x64 publish from the issue branch
contains **0** `WPFLocalizeExtension.dll`, **0** `XAMLMarkupExtensions.dll`, **1**
`DotNetProjects.Wpf.Extended.Toolkit.dll`, and all **23** `Thrum.resources.dll` satellites. The
restored `dotnet list package --include-transitive` graph likewise names only the remaining
toolkit when filtered for these three package families.

This is a current artifact check, not an inference from the edited project file. The two removed
rows remain in the evidence table below because they document what beta.1 redistributed and why
the legal cleanup was required.

## Findings that changed the notice

### The inherited notice was wrong in both directions

It credited **Font Awesome**, which is not in the product. A full-tree search for `font awesome`,
`fontawesome` and for any `.ttf`/`.otf`/`.woff` file returned exactly one hit: the notice itself.
The entry was removed.

It omitted, among others, every NuGet dependency, both bundled binaries, the transitive
assemblies above, and a 431 KB vendored JavaScript bundle that the 2026-07-25 `TO AUDIT` list did
not mention at all.

### `BezierCurveEditor/build.js` is a webpack bundle nobody had catalogued

441,650 bytes of minified JavaScript shipped in the package. It is a build of
`gre/bezier-easing-editor` (MIT) and embeds that project's npm dependency tree, including React.
Searching the bundle finds 28 occurrences of `MIT` and one `license: "ISC"` field, but no
per-package manifest, so the constituent notices are not recoverable from the artifact. Recorded
as unresolved item 3(b).

## Resolved entries and their evidence

| Component | Licence | How it was established |
|---|---|---|
| bloomtom.HttpProgress 2.3.2 | MIT | nuspec `licenseExpression` |
| Concentus 2.2.2 | BSD-3-Clause | bundled `LICENSE`, read in full — IETF/Opus three-clause text |
| DotNetProjects.Extended.Wpf.Toolkit 5.0.106 | Ms-PL | nuspec `licenseExpression` |
| H.NotifyIcon(.Wpf) 2.0.74 | MIT | nuspec `licenseExpression` |
| MdXaml 1.27.0, MdXaml.Plugins | MIT | nuspec `licenseExpression` |
| AvalonEdit (transitive) | MIT | nuspec `licenseExpression` |
| NAudio 2.2.1 (7 assemblies) | MIT | bundled `license.txt` — verbatim MIT, "Copyright 2020 Mark Heath" |
| NLog 5.1.1 | BSD-3-Clause | nuspec `licenseExpression` |
| Ookii.Dialogs.Wpf 5.0.1 | BSD-3-Clause | nuspec `licenseExpression` |
| System.Management 7.0.2 | MIT | nuspec `licenseExpression` |
| System.Memory 4.5.5 | MIT | nuspec `licenseUrl` → dotnet/corefx LICENSE.TXT |
| TaskScheduler 2.10.1 | MIT | nuspec `licenseExpression`, author David Hall |
| WPFLocalizeExtension 3.9.4 | Ms-PL | beta.1 evidence: bundled `LICENSE` — Ms-PL text; removed by issue #72 |
| XAMLMarkupExtensions 2.1.3 (transitive) | Ms-PL | beta.1 evidence: bundled `LICENSE`; shipped DLL `ProductVersion` confirms 2.1.3; removed by issue #72 |
| WpfScreenHelper 2.1.0 | MIT | nuspec `licenseExpression` |
| YellowDogMan.RRNoise.NET 0.1.9 | MIT | nuspec `licenseExpression` |
| rnnoise (native, in rnnoise.dll) | BSD-3-Clause | xiph/rnnoise via GitHub API |
| Microsoft.Windows.CsWin32 0.3.106 | MIT | nuspec; build-time only, `PrivateAssets=all` |
| SbcSharp (vendored) | Apache-2.0 | in-tree `LICENSE.txt` + `NOTICE.md`; Ylianst/SbcSharp confirms |
| Crc32.cs (vendored) | Apache-2.0 | file header states the grant; dariogriffo/Crc32 confirms |
| vJoyFeeder.cs (vendored) | public-domain dedication | file header quotes the author's statement and links it |
| SharpOSC.dll (bundled) | MIT | upstream `License.txt` read in full — verbatim MIT, © 2012 Valdemar Örn Erlingsson. GitHub classifies it "NOASSERTION" only because of the filename/header shape |
| FakerInputDll.dll (bundled native) | MIT | Ryochan7/FakerInput via GitHub API |
| VIIPER (not redistributed) | GPL-3.0 | hbashton/VIIPER via GitHub API |
| usbip-win2 (not redistributed) | BSD-2-Clause | vadimgrn/usbip-win2 via GitHub API — confirms the 2026-07-25 "understood to be" |

## Unresolved, and why each is release-blocking

### 1. `FakerInputWrapper.dll` has no licence grant at all

`Ryochan7/FakerInputWrapper` was checked exhaustively on 2026-07-31: repository root contains
only `.gitignore`, `FakerInputWrapper.sln`, the source directory and `README.md`; the README is
a single heading with no body; `FakerInput.cs` and the other sources carry no licence header;
`FakerInputWrapper.csproj` sets `Authors` to "Ryodigi Solutions LLC" and no licence property.
GitHub's licence API returns `NONE`.

We redistribute the compiled assembly in every release. Without a grant, default copyright
reserves all rights. The native `FakerInputDll.dll`, from the separate `Ryochan7/FakerInput`
repository, **is** MIT — only the managed wrapper is affected.

Options: ask the author for a licence (cheapest, and the author is the original DS4Windows
author, so the ask is natural); drop the dependency; or reimplement the wrapper, which is a thin
`DllImport` surface over `FakerInputDll.dll` and looks small.

### 2. One Ms-PL assembly remains inside a GPL-3.0 program

`DotNetProjects.Extended.Wpf.Toolkit` is Microsoft Public License. The FSF's licence list says
of Ms-PL:
*"This is a free software license; it has a copyleft that is not strong, but incompatible with
the GNU GPL."*

Thrum is GPL-3.0-or-later and still links the toolkit, so this remains a compatibility question,
not a paperwork gap. It is **inherited from upstream DS4Windows** and tracked for replacement by
issue #71.

Issue #72 resolved two thirds of the original finding: `WPFLocalizeExtension` and its transitive
`XAMLMarkupExtensions` no longer appear in the project, restored package graph, or fresh
self-contained publish. The replacement is in-house GPL-3.0-or-later code, so those two
assemblies are no longer part of the distributed-work analysis. The summary above is the FSF's
published position, not legal advice.

For contrast, the Apache-2.0 components are fine: the FSF confirms Apache-2.0 is compatible with
GPLv3 (though not GPLv2, which does not affect us).

### 3. Two vendored items that cannot be cleanly licensed as they stand

**(a) `DS4Windows/OneEuroFilter.cs`** — 105 lines, namespace `Sensorit.Base`, no licence header
anywhere in the file. The 1€ filter authors' own page lists the C# implementation under "other
implementations not verified yet" with no licence stated, while their Python, C++, Java,
JavaScript, TypeScript and Arduino versions are BSD or MIT. The algorithm is published research
(Casiez, Roussel & Vogel, CHI 2012). Cheapest resolution: reimplement from the paper, or port
from one of the BSD-licensed reference versions, and put a header on it.

**(b) `DS4Windows/BezierCurveEditor/build.js`** — see above. Either rebuild from source so a
dependency licence manifest can be produced, or delete it if the editor is no longer wanted.

## Guard tests

`DS4WindowsTests/ThirdPartyNoticeTests.cs` (4 tests) checks that every `PackageReference` and
every DLL under `DS4Windows/libs` is named somewhere in `NOTICE.txt`, that the three
authoritative notice files still exist and are still cross-referenced, and that the UNRESOLVED
section keeps saying it is release-blocking for as long as it exists.

They check **presence of an entry, not correctness of a licence** — a licence can only be
verified against the artifact, which is what this document is for. What they prevent is the
silent failure mode: a dependency is added, it ships, and nobody notices the notice was never
updated. That is exactly how Font Awesome came to be credited for something absent while a
431 KB bundle shipped uncredited.

Negative control: removing every occurrence of `WpfScreenHelper` and `SharpOSC` from
`NOTICE.txt` fails both list tests with their intended messages; restored and re-verified green.
Suite: **805 passed / 0 failed**.

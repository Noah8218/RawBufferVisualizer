# Release Qualification: 1.0.50

> Current-state note (2026-07-31): the pre-collection and pre-announcement packages below remain historical. The current 2,011,595-byte package (`E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93`) passed Release build/self-tests, release-communication validation, ordinary scripted reinstall, installed release-announcement automation, `AutomaticCollections`, and `MultiLibraryHybrid`. The external Windows 10 in-place update gate remains open.

## Scope

This candidate addresses four current release risks:

1. debugger handoff files disappearing before the docked window actually acknowledges the image;
2. duplicate/stale Raw Buffer Visualizer View-menu registrations;
3. initialized exact OpenCvSharp/Emgu Mats not opening through Automatic Inspector;
4. inconsistent image aspect/zoom state when images are repeatedly appended or selected.

Public Marketplace `1.0.49` became healthy on the affected Windows 10 PC only after uninstall/reinstall. That is a clean-reinstall result, not proof of an in-place update. The exact `1.0.50` candidate must pass both local installed-VSIX and external update gates.

## Previously installed baseline

```text
Version: 1.0.50.0
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 2,001,513 bytes
Modified UTC: 2026-07-29T03:19:16.5836519Z
SHA-256: 2014AA8D679AF3D01F0B16CC304E77064ABCF0B0725BDC6BD543B7C08CDA397E
Source: b5aa43e plus the current uncommitted working tree
```

These properties were read from the artifact that was installed and exercised in the local qualification below. They are retained as historical baseline evidence and must not be attributed to the newer collection package.

## Historical Automatic Mat collection candidate

```text
Version: 1.0.50.0
Path: C:\Git\RawBufferVisualizer\.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Release\net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 2,007,042 bytes
Modified UTC: 2026-07-31T08:49:45.6433326Z
SHA-256: 01D4B19276625F4F73883C5113C8DA2044D3F5B7EAC49D6D1D3994CA727ECE3A
Source: b5aa43e plus the current uncommitted working tree
Installed-VSIX status: Pass on VS 17.14.37314.3 instance 2c8402d8; seven rows, five opens, two isolated failures, duplicate-free rescan, settings restored, exact menu counts, zero protocol errors
```

## Current local release candidate

```text
Version: 1.0.50.0
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 2,011,595 bytes
Modified UTC: 2026-07-31T11:11:26.4934885Z
SHA-256: E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93
Source: b5aa43e plus the current uncommitted working tree
Installed-VSIX status: Pass on VS 17.14.37314.3 instance 2c8402d8; one-time non-modal release highlights, persisted Dismiss, What's New reopen, zero image-list side effects, exact menu counts, AutomaticCollections 5 opened/2 isolated failures, MultiLibraryHybrid 9 documents/0 errors, zero protocol errors
```

## Intended changes

### Atomic handoff and explicit completion

- producer publishes a completed temporary file through an atomic Ready move;
- consumer exclusively claims Ready as Processing;
- ACK is written only after the document opens;
- NACK retains a rejection reason;
- request read and terminal marker move retry `10 x 50 ms`;
- Created and Renamed watcher handlers are registered before events are enabled;
- missing Ready alone is never treated as success.

Modern timeout/cancel and Classic fire-and-forget share `ScheduleTerminalArtifactCleanup`. It polls terminal state every 100 ms for up to two minutes, deletes ACK/NACK/conflict terminal artifacts, and never deletes Ready/Processing on timeout. Locked terminal cleanup is retried, and a request must be observed Missing consecutively before it leaves the reaper's pending set. Once any request was published, cancellation/exception no longer treats Missing as proof that the snapshot payload is safe to delete.

Current boundary: terminal markers created after the two-minute window and Processing files stranded by process crash are not immediately reclaimed. The existing 24-hour stale snapshot sweep also has no active-document lease, so unusually long-lived file-backed documents remain a separate lifetime-hardening item.

### Package/menu identity

- extension ID remains `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f`;
- VSPackage GUID remains `{1977574b-f107-465f-bfd1-5fc022907039}`;
- generated registration uses `Menus.ctmenu, 2`;
- the View menu contains exactly one `Raw Buffer Visualizer` and one `Raw Buffer Visualizer: Scan Current Frame`;
- future `.vsct` command-table changes must increase the resource version.

### Automatic Inspector

- exact initialized OpenCvSharp `Mat`: `Data`, `Cols`, `Rows`, `Step()`, `Depth()`, `Channels()`;
- exact initialized Emgu CV `Mat`: `DataPointer`, `Cols`, `Rows`, `Step`, `Depth`, `NumberOfChannels`;
- Bitmap remains a registered glyph path because automatic debugger-side `LockBits` is outside the safe contract;
- test breakpoints are placed after assignment has completed;
- repeated Scan Now is duplicate-free and a failed candidate does not block successful rows.

### Fit/Manual

- new image, changed selection, Fit, and double-click enter Fit;
- Fit preserves aspect ratio and shows the full image with the intended margin;
- wheel, pan, and 1:1 enter Manual;
- Manual preserves user zoom/center across unrelated selection/layout refresh;
- Fit recomputes after viewport resize.

## Current-source evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass | `dotnet build RawBufferVisualizer.sln -c Release --no-restore /nodeReuse:false`; 0 errors, 18 existing `VSTHRD010` warnings |
| Core/self-test suite | Pass | `RawBufferVisualizer self-tests passed`; includes release-announcement persistence/version tests |
| Atomic publish | Pass | `VisualizerHandoffInboxPublishesRequestsAtomically`; observer start gate and observed count `> 0` |
| Exactly-once claim | Pass | `VisualizerHandoffInboxClaimsRequestExactlyOnce` |
| Explicit completion | Pass | `VisualizerHandoffInboxTracksExplicitCompletion` |
| NACK retry | Pass | Processing path held with an exclusive lock for 150 ms, then terminal move succeeded through retry |
| Locked terminal cleanup | Pass | A locked ACK survives the first cleanup attempt and is removed after the lock is released; NACK terminal publication has a separate locked-processing retry test |
| Stable Missing detection | Pass | Reaper requires consecutive Missing observations before dropping a request |
| Terminal cleanup | Pass | ACK becomes Missing after cleanup |
| In-flight preservation | Pass | Ready still exists after a 200 ms reaper interval |
| Preview-first handoff smoke | Pass | `SmokePreviewFirstHandoff.ps1 -NoBuild`; preview 1/full 1 |
| Smart Type Mapper smoke | Pass | focused smoke passed |
| Current-source Fit/Manual matrix | Pass | 540/900/1160 px; aspect errors 0, Fit margin 1.05, Manual zoom delta 0, Manual center delta 0 |

The Fit/Manual matrix instantiates the current-source view and is not an installed-VSIX behavioral run. The installed screenshot is retained only as visual evidence that no obvious aspect distortion was present.

## Final package/static checks

| Criterion | Result | Evidence |
| --- | --- | --- |
| Exact VSIX version `1.0.50.0` | Pass | packaged manifest and installed extension |
| VSIX size/SHA-256/modified time recorded | Pass | candidate block above |
| Full Release solution build | Pass | 0 errors; 18 existing `VSTHRD010` warnings |
| Final candidate incremental/package build | Pass | run after the last handoff hardening; 0 errors; 0 warnings |
| Package/publish guard | Pass | final candidate produced and accepted by the package pipeline |
| Exactly one current package registration | Pass | one `.pkgdef`; package key `{1977574b-f107-465f-bfd1-5fc022907039}` |
| Retired Package GUID absent | Pass | archive inspection found zero retired GUID occurrences |
| `Menus.ctmenu, 2` present | Pass | exactly one registration in the packaged `.pkgdef` |
| Legacy split `.pkgdef` absent | Pass | archive contains no VSSDK split-project `.pkgdef` |
| Main manifest count | Pass | exactly one `extension.vsixmanifest` |
| Changed PowerShell parser checks | Pass | 7/7 scripts parsed without errors |

## Local installed-VSIX acceptance

Use an ordinary Visual Studio 2022 profile without registration repair.

### Environment

```text
Visual Studio version: 17.14.37314.3
Instance ID: 2c8402d8
Operating system: Windows 10 Pro 10.0.19045, build 19045
Extension version shown: 1.0.50.0
Install/update route: ordinary Reinstall; no repair and no /ResetSkipPkgs
ActivityLog path: C:\Users\user\AppData\Roaming\Microsoft\VisualStudio\17.0_2c8402d8\ActivityLog.xml
package.log path: C:\Users\user\AppData\Local\Temp\RawBufferVisualizer\VisualStudio\package.log
```

### Results

| Criterion | Result | Evidence / fill-in |
| --- | --- | --- |
| Install exact final SHA-256 | Pass | ordinary Reinstall of the candidate above; four installed core assemblies hash-match the publish directory |
| Fresh Visual Studio session after reinstall | Pass | both final scenarios launched fresh VS processes |
| `View > Raw Buffer Visualizer` count = 1 | Pass | both JSON results report `openCommandCount: 1` |
| `View > Raw Buffer Visualizer: Scan Current Frame` count = 1 | Pass | both JSON results report `scanCommandCount: 1` |
| ToolWindow opens | Pass | both installed scenarios completed |
| Breakpoint is after image assignment | Pass | final `--multi-library-debug` scenario |
| Exact OpenCvSharp Mat automatic open | Pass | `MultiLibraryHybrid`; automatic live row |
| Exact Emgu CV Mat automatic open | Pass | `MultiLibraryHybrid`; automatic live row |
| Bitmap registered glyph open | Pass | `registeredVisualizerTypes: System.Drawing.Bitmap` |
| Safe wrapper automatic rows | Pass | final status `8 detected: 8 opened, 0 need mapping, 0 failed` |
| Multi-library final documents = 9, errors = 0 | Pass | fresh result JSON |
| Repeated Scan Now is duplicate-free | Pass | MultiLibrary and Automatic scenarios |
| One failed candidate does not block valid images | Pass | Automatic scenario: 6 opened, 1 map, 1 failed |
| Automatic preference remains enabled through the scenario | Pass | initial/final enabled |
| Registered handoff protocol | Pass | Bitmap opened and `packageProtocolErrorCount: 0` |
| Package-load/runtime protocol errors | Pass | both result JSON files report 0 protocol errors |
| Installed visual aspect evidence | Pass, visual only | screenshot shows no obvious distortion; behavioral Fit assertions come from the current-source matrix |
| `1.0.50` release highlights shown in Tool Window | Pass | `ReleaseAnnouncement-installed-vsix.json`; installed screenshot |
| Dismiss saved and What's New reopens | Pass | saved `lastSeenVersion: 1.0.50`, banner collapsed, then reopened through UI Automation |
| Release highlights do not start inspection | Pass | image rows remained 0 before/after Dismiss and What's New |
| Automatic Mat collections on exact final VSIX | Pass | 7 rows, 5 opens, 2 isolated failures, duplicate-free rescan |
| Registered/automatic hybrid on exact final VSIX | Pass | 9 documents, 0 errors; automatic status 8/8 opened |

Installed debuggee:

```text
--multi-library-debug
```

Evidence folder:

```text
artifacts\ui\release-qualification-1.0.50\
```

Recorded evidence:

```text
01-before-external-win10-menu-handoff-fit.jpg
02-before-external-win10-handoff-error.jpg
03-after-local-win10-single-view-menu.png
04-after-local-win10-automatic-mats-bitmap.png
05-after-local-win10-partial-failure-isolation.png
06-after-current-source-fit-1160.png
07-after-current-source-fit-540.png
AutomaticVisionInspector-installed-vsix.json
MultiLibraryHybrid-installed-vsix.json
fit-layout-widths.json
```

Files `01` and `02` are the reported external-PC failure baseline. Files `03` through `05` and both installed result JSON files are local installed-VSIX evidence. Files `06`, `07`, and `fit-layout-widths.json` are current-source Fit evidence, not installed-VSIX Fit proof.

## External Windows 10 in-place update gate

Prerequisite: the affected external Windows 10 / VS2022 PC with public Marketplace `1.0.49` installed.

Rules:

1. Record the pre-update extension version and Visual Studio instance.
2. Do not uninstall the extension.
3. Do not run registration repair or `/ResetSkipPkgs`.
4. Update to the exact final `1.0.50` SHA-256.
5. Close every Visual Studio process and restart.
6. Repeat the complete menu, ToolWindow, registered Bitmap/Mat, Automatic Inspector, ACK, and Fit/Manual checks.
7. Record Windows display scale and dock dimensions.

```text
PC/profile:
Windows build:
Display scale:
Visual Studio version/instance:
Pre-update version:
Post-update version:
Exact VSIX SHA-256:
View command counts:
ToolWindow open:
Registered Bitmap:
Automatic OpenCvSharp:
Automatic Emgu:
Explicit ACK:
Fit/Manual:
Errors/log paths:
Screenshots:
Result: Pending
```

If the extension works only after uninstall/reinstall, this gate fails. Record that recovery separately as clean-install evidence.

## Publication decision

Do not publish `1.0.50` until:

- the affected Windows 10 public-1.0.49 in-place update gate passes;
- Marketplace overview and release notes match the qualified behavior.

The current artifact/static, current-source, scripted reinstall, installed metadata, and `AutomaticCollections` checks have passed. Publication remains gated by the affected external PC's no-uninstall update result.

The maintainer has chosen to upload `1.0.50` before that external gate and perform the separate-PC update check afterward. This does not convert the qualification state to Complete; a failed public `1.0.49` to `1.0.50` update must be treated as a release defect, not recovered evidence.

## Completion record

```text
Status: Incomplete
Scope: 1.0.50 source/package including optional Automatic Mat collections, in-product release highlights, unified Marketplace/GitHub/VSIX release communication, installed-VSIX handoff/menu/automatic-image behavior, and current-source Fit/Manual reliability
Acceptance criteria: Current source build/self-tests/package/layout, release-communication coherence, ordinary scripted reinstall, installed announcement behavior, AutomaticCollections, and MultiLibraryHybrid passed; external affected-PC in-place update remains Pending
Verification: Release build 0 errors/18 existing warnings; final package build 0/0; self-tests passed; communication guard and Marketplace dry run passed; exact final VSIX reinstalled; release highlights passed Dismiss persistence/What's New reopen/zero image side effects; AutomaticCollections passed 5 opens/2 isolated failures; MultiLibraryHybrid passed 9 documents/0 errors; all installed runs reported exact menu counts and zero protocol errors
Evidence: SHA-256 E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93; artifacts/ui/installed-vsix-new-features/ReleaseAnnouncement-installed-vsix.json; artifacts/ui/installed-vsix-new-features/release-announcement-installed.png; AutomaticCollections-installed-vsix.json; MultiLibraryHybrid-installed-vsix.json; CHANGELOG.md; embedded Resources/ReleaseNotes.txt
Boundary / next dependency: Update the affected external Windows 10 PC from public 1.0.49 to the exact current package without uninstall, repair, or /ResetSkipPkgs
```

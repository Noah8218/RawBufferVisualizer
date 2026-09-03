# Raw Buffer Visualizer 2.0.4 Release Qualification

## Status

`Complete` for the approved local 2.0.4 feature, package, current-media, documentation, and available-host verification scope. Owner review is the next gate. Commit, push, CI, Marketplace upload, public readback, tag, and GitHub Release were not performed.

## Qualified scope

- Product and VSIX version `2.0.4` / `2.0.4.0`; public `2.0.3.0` remains immutable.
- The existing complete reset is presented as a labelled **Clear all** action directly above the accumulated image list instead of in the top toolbar.
- **Clear all** is disabled for an empty list, enabled when images exist, clears every loaded document and dependent viewer state in one action, and does not change paused-debuggee data.
- Direct 20-image collection transfer and full-list reset regression.
- Narrow, medium, and wide docked layout verification at 540, 900, and 1160 DIP, including full labels, icon, tooltip, accessible name, hover, focus, pressed, mouse click, keyboard activation, reset, and reopen.
- Existing 2.0.3 ConcurrentDictionary and first-use ImagePtr implementation retained unchanged.
- English Marketplace Overview, Korean owner-review copy, release notes, changelog, README, release documentation, and all six current-UI Marketplace media files aligned to 2.0.4.

Excluded: camera acquisition/control, proprietary SDK redistribution, 3D/depth/point-cloud visualization, Visual Studio 2019, 32-bit Visual Studio, Preview/Insiders builds, commit/push, CI, publication, and public readback.

## Source state

```text
Repository: C:\Git\RawBufferVisualizer_vs17.9_compat
Branch: agent/vs2022-17.9-compat
Baseline commit: e236278f642ea061e7a49e32256ad0e953544084
State: local uncommitted 2.0.4 review candidate
Public Marketplace baseline checked on 2026-08-25: 2.0.3.0
```

## Exact review VSIX

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\candidate-final-2\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Length: 2,493,763 bytes
SHA-256: 1707B216FE5488E9910238EEEA596D8BDCD8B0C7DB236D9BF1942DD16D746AF7
Manifest version: 2.0.4.0
Extension identity: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
Installation range: [17.9,18.0)
Entries: 72
```

The package guard verified the same isolated hybrid ownership as public 2.0.3: the `net472` VSSDK assembly owns the in-process package, commands, menus, and Tool Window; the out-of-process Extensibility assembly owns debugger visualizer providers. Do not rebuild or substitute this VSIX after owner review. Any product source, manifest, embedded release note, or package-content change creates new bytes and requires a new hash plus installed qualification.

## Verification

| Check | Result |
| --- | --- |
| Public baseline readback | Pass; official Gallery API reported `2.0.3.0`, updated 2026-08-24 12:52:32 UTC |
| Release solution build | Pass; 0 errors and 18 pre-existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` during candidate packaging |
| Aggregate `RawBufferVisualizer.Tests` | Pass, including exact 20-entry collection transfer/count checks |
| Focused WPF UI smoke | Pass at 540/900/1160 DIP: 20 rows before Clear, complete reset, disabled empty state, reopen, labels/bounds, icon, tooltip, accessibility, hover/focus/pressed/mouse/keyboard paths |
| Version surfaces | Pass; five established product/manifest surfaces agree on 2.0.4 / 2.0.4.0 |
| Release communication contract | Pass for `2.0.4` |
| VSIX publish/package guard | Pass; exact identity/version/range and 72 entries |
| Installed payload validator | Pass on VS17.14 and VS18.8 after in-place update over public 2.0.3; no uninstall, repair, or registration reset |
| Installed changed-path scenarios | Pass on available hosts as recorded below; package-protocol error count 0 |
| Current media | Pass; exact installed-2.0.4 DataTip and industrial workflow captures visually reviewed; all six assets replaced and hashed |
| Marketplace publisher dry run | Pass with exact VSIX, English 2.0.4 Overview, and exactly six current assets; no upload executed |

The focused WPF smoke used `\\.\DISPLAY2`, bounds `Left=-1920, Top=365, Width=1920, Height=1080`, at 125% DPI. The changed control passed dark-theme runtime verification. Other supported themes and 100/150/175/200% scales were not rerun for this local presentation-only change.

## Installed-host results

The exact unchanged VSIX recorded above was installed in-place and validated on both available stable hosts:

| Host | Scenario | Result |
| --- | --- | --- |
| VS2022 Community `17.14.37516.0` (`419f0858`) | `ConcurrentDictionary` | Pass; one named snapshot, 0 errors |
| VS2022 Community `17.14.37516.0` | `AutomaticCollections` | Pass; 7 rows, 5 opened, 2 isolated failures, full Clear/reset/reopen contract, no duplicates |
| VS2022 Community `17.14.37516.0` | `MultiLibraryHybrid` | Pass; 9 documents, 8 detected/8 opened, 0 errors |
| VS2022 Community `17.14.37516.0` | `ReleaseAnnouncement` | Pass; 2.0.4 title, repeat open/close, no inspection side effect |
| VS2022 Community `17.14.37516.0` | `IndustrialDataTip` | Pass; code DataTip glyph opened 1280 x 720 OpenCvSharp `BGR24` image |
| VS2022 Community `17.14.37516.0` | `IndustrialMarketplace` | Pass; 1280 x 960 color PCB, Automatic Inspector 5/5, color stride 7344 -> 7424 recovery |
| VS2026 Community `18.8.12105.206` (`19923728`) | `ConcurrentDictionary` | Pass; one named snapshot, 0 errors |
| VS2026 Community `18.8.12105.206` | `AutomaticCollections` | Pass; same 7-row Clear/reset/reopen contract |
| VS2026 Community `18.8.12105.206` | `ReleaseAnnouncement` | Pass; 2.0.4 title and side-effect-free repeat toggle |

The installed desktop runs dynamically selected and verified `\\.\DISPLAY2`; the Visual Studio window rectangle was `Left=-1900, Top=385, Width=1880, Height=1040` and intersected that monitor.

One first `IndustrialMarketplace` attempt ended in a transient UI Automation `ElementNotAvailable` condition; an immediate run against the same package passed and the final evidence uses that successful run. This is retained as harness evidence, not counted as a product defect.

## Compatibility and cold-start boundaries

The package keeps the same isolated 17.9 SDK boundary, manifest floor, provider ownership, and extension identity used by public 2.0.3. The temporary exact Visual Studio 2022 17.9 host is not installed, so exact 2.0.4-on-17.9 runtime was not rerun. The user separately confirmed normal 17.9 operation for 2.0.3; that is user evidence rather than an automated exact-2.0.4 host record.

The ImagePtr product path did not change in 2.0.4. A fresh attempted exact-2.0.4 cold-start run did not meet the scenario precondition because the Tool Window had persisted open from prior installed scenarios. Therefore no new exact-2.0.4 true-cold result is claimed; the exact 2.0.3 true-cold evidence on VS17.14 and VS18.8 remains the applicable unchanged-path evidence.

## Marketplace media

All six Overview assets were replaced with exact installed-2.0.4 captures showing the current **Clear all** UI. The separate code-DataTip, Locals, and complete workflow GIFs are 3.0, 3.25, and 6.0 seconds. The complete GIF uses eight 0.75-second states and ends with color `BGR24` stride recovery. Source licenses, dimensions, frame counts, sizes, hashes, and contact-sheet evidence are recorded in [Industrial Image Debug Testing](industrial-image-testing.md).

The English [Marketplace Overview](marketplace-overview-2.0.4.md) points to the eventual GitHub `main` asset URLs. The [Korean owner-review copy](marketplace-overview-2.0.4.ko.md) uses local relative paths so it renders the unpushed 2.0.4 files for review.

## Evidence

```text
Public 2.0.3 baseline:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\public-baseline-20260825\gallery-query.json

Focused 20-image/Clear UI matrix:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\ui-final-2\layout-widths.json

VS2022 installed scenarios:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-ConcurrentDictionary\ConcurrentDictionary-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-AutomaticCollections\AutomaticCollections-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-MultiLibraryHybrid\MultiLibraryHybrid-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-ReleaseAnnouncement\ReleaseAnnouncement-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-industrial-datatip-final3\IndustrialDataTip-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-industrial-marketplace-final2\IndustrialMarketplace-installed-vsix.json

VS2026 installed scenarios:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2026-ConcurrentDictionary\ConcurrentDictionary-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2026-AutomaticCollections-final\AutomaticCollections-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2026-ReleaseAnnouncement-final\ReleaseAnnouncement-installed-vsix.json

Current media and contact sheets:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-media-final

Marketplace dry run:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-dry-run-final\vs-publish.json
```

## Closure record

```text
Status: Complete
Scope: Local Raw Buffer Visualizer 2.0.4 candidate with labelled full-list Clear all placement, 20-image reset regression, aligned English/Korean Marketplace copy, current installed-UI media, exact VSIX generation, and available-host validation.
Acceptance criteria: public 2.0.3 version not reused -> pass; version surfaces/VSIX/copy agree on 2.0.4 -> pass; 20 entries transfer and one Clear all removes every row/viewer state -> pass; labels and action states remain usable at 540/900/1160 DIP -> pass; exact package passes changed installed scenarios on VS17.14/VS18.8 -> pass; all six current media assets and Marketplace dry run -> pass.
Verification: Release build/self-tests, focused WPF UI matrix, package/update guards, installed scenarios, visual media review, measured media metadata/hashes, and Marketplace dry run passed as recorded above.
Evidence: Exact VSIX path/hash plus D-drive JSON, screenshots, contact sheets, and dry-run manifest listed above.
Boundary / next dependency: Owner review of the exact VSIX and English/Korean Overview is required before commit, push, CI, or Marketplace upload. Exact 2.0.4 runtime on VS17.9 and a fresh exact-2.0.4 true-cold ImagePtr run were not recorded for the reasons stated above.
```

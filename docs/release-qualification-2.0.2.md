# Raw Buffer Visualizer 2.0.2 Release Qualification

## Status

`Complete` for the approved local 2.0.2 UI, collection-policy, packaging, and installed-host verification scope. Commit, push, CI, Marketplace upload, and public readback were not performed.

## Qualified scope

- Visual Studio 2022 compatibility remains declared from `17.9` through the isolated `net472` VSSDK 17.9 in-process package.
- The `net8.0-windows8.0` debugger visualizer providers remain out of process, and stable Visual Studio 2026 `18.x` support is retained.
- Supported exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays are included in every manual and Break Mode scan. The retired `Mat collections` setting is no longer shown or persisted.
- Primary Tool Window commands use Visual Studio-native icons. `Open`, `Clear`, and `Save` may become icon-only below 760 px; their accessible names and explanatory tooltips remain mandatory. `Fit`, `1:1`, and panel commands keep visible text because their icons alone are ambiguous.
- The Tool Window supports content widths from 320 px, wraps the toolbar without clipping, preserves central viewer and compact-main minimum sizes while splitters move, and wraps empty-state guidance inside the viewer.
- `Clear` now removes the active image and every row, shows the empty-viewer guidance without a stale renderer frame, blanks and disables both Wide and Compact Interpret controls, clears diagnosis candidates/status, pixel/marker state, and comparison A/B, and preserves whether the Inspector is open or closed. A later Open or Scan Now repopulates and re-enables Interpret controls.
- PL-0001 long temporary-path handoff claim repair and the existing package ownership remain unchanged.

Excluded: camera acquisition/control, proprietary SDK redistribution, 3D/depth/point-cloud visualization, Visual Studio 2019, 32-bit Visual Studio, commit/push, CI, and publication.

## Current VSIX

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\release-candidate-final\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Length: 2,493,359 bytes
SHA-256: 46A84EFC5AB685139B3C16B0DA0C35B6277E359D05E3E6198CA9925D737E5D57
Manifest version: 2.0.2.0
Installation range: [17.9,18.0)
CoreEditor prerequisite: [17.9,)
Entries: 72
```

The publish guard verified the hybrid ownership and payload:

- `RawBufferVisualizer.VisualStudio.Vssdk.dll` and its `.pkgdef` own the in-process package, menus, and Tool Window.
- `OutOfProc/RawBufferVisualizer.VisualStudio.Extensibility.dll` owns the debugger visualizer providers.
- The package contains `.vsextension/extension.json` and embedded release notes.
- Registration contains one current package/menu owner and rejects the retired package GUID.

Any product, manifest, embedded-note, or package-content change requires rebuilding and replacing this hash. Documentation-only evidence updates do not change these VSIX bytes.

## Verification

Earlier UI-modernization evidence remains below `D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809`. The final Clear-reset implementation, exact-candidate package, current layout results, and installed-host evidence were written below `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809`; test `TEMP` and `TMP` used its `temp` directory.

| Check | Result |
| --- | --- |
| Release solution build | Pass; 0 errors and 18 existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` |
| Aggregate `RawBufferVisualizer.Tests` | Pass |
| Release communication contract | Pass for `2.0.2` |
| Environment Check source/UI contract | Pass |
| Automatic Inspector layout | Pass at 540 and 1160 px |
| Full docked viewer workflow | Pass at 540, 900, and 1160 px, including Fit/manual zoom, recovery, hover/pin, and malformed handoff paths |
| Clear presentation reset | Pass at 540, 900, and 1160 px: empty list/guidance, hidden renderer, blank disabled Interpret controls, cleared diagnosis/pixel/marker/comparison state, preserved Inspector visibility, and repopulation after reopen |
| Responsive layout contract | Pass at 320, 350, 400, 420, 480, 540, 619, 620, 759, 760, 900, 1039, 1040, and 1160 px |
| Icon-only discoverability | Pass; standard native monikers, non-empty accessible names, and non-empty explanatory tooltips |
| Shared UI visual states | Pass; normal, keyboard focus, pointer hover, disabled, popup, and selection paths in the Smart Type Mapper smoke |
| VSIX publish/package guard | Pass |

Responsive splitter coverage:

- Images splitter: minimum, middle where available, and maximum list widths at all 14 content widths; central viewer stayed at or above 140 px.
- Compact Inspector splitter: requested 120, 168, and 300 px at every width below 1040; the request was clamped where necessary and main content stayed at or above 100 px.
- Right Inspector splitter: 180, 300, and 420 px at 1040 and 1160; central viewer stayed at or above 140 px.
- Toolbar children and empty guidance were checked numerically against their parent bounds; both remained inside at all 14 widths.

The original 420 px minimum prevented a 350 px host width from settling, and the first 320 px empty-state rendering allowed its text to exceed the viewer. The fixes lowered the product minimum to 320 px, used a 160 px image-list default below 420 px, and constrained empty guidance with a 12 px wrapping margin. The failing pre-fix case and passing after matrix are preserved separately.

## Installed-host results

The current product build passed `AutomaticCollections` on both installed stable hosts:

| Host | Instance | Result |
| --- | --- | --- |
| Visual Studio 2022 Community `17.14.37516.0` | `419f0858` | Pass on the exact frozen candidate: 7 rows, 5 opened, 2 isolated failures, diagnosis populated before Clear, complete Clear reset, Inspector visibility preserved, Interpret controls repopulated after rescan, duplicate-free rescan, one Open/Scan command, 0 package-protocol errors |
| Visual Studio 2026 Community `18.8.12023.21` | `19923728` | Same exact-candidate behavior and result; pass |

Both desktop runs used and verified the dynamically selected leftmost monitor `\\.\DISPLAY2`, bounds `Left=-1920, Top=365, Width=1920, Height=1080`.

The actual installed dark-theme captures show native icon pixels, retained `Fit`/`1:1` text at the narrow docked width, retained text for Inspector/What's New/Environment, the removed Mat checkbox, and wrapped empty guidance. The product does not currently claim separately qualified adaptive Light or Blue Tool Window themes.

The temporary Visual Studio 2022 Professional 17.9 instance used by the earlier architecture qualification was removed. The current UI candidate therefore has current-source VSSDK 17.9 compile/package proof, plus earlier 17.9 runtime proof for the unchanged package architecture and handoff repair, but it does not claim an installed run of these exact UI bytes on 17.9. Reinstalling 17.9 was explicitly excluded in favor of proportional verification.

## Marketplace media refresh

The 2026-08-09 media correction uses captures from the installed `2.0.2.0` extension on Visual Studio 2022 `17.14.37516.0`. A new 3.0-second code-DataTip GIF shows the cursor on an OpenCvSharp `Mat`, its visualizer magnifying-glass icon, the click, and a different 1280 x 720 `BGR24` industrial-machine photograph in the docked viewer. The retained Locals workflow is 3.25 seconds. The complete GIF is 960 x 531, eight frames, and 6.0 seconds; it proceeds through live pixels, automatic color representations, diagnostics, the deliberately wrong color `BGR24` stride, the top `7424`-stride correction, and the restored color scene. Source, license, capture paths, dimensions, durations, and tracked hashes are recorded in [Industrial Image Debug Testing](industrial-image-testing.md).

The pre-publish copy review rebuilt the English [Marketplace Overview](marketplace-overview-2.0.2.md) with the code-DataTip GIF first, followed by the Locals GIF and complete workflow. The separate [Korean owner-review copy](marketplace-overview-2.0.2.ko.md) uses local relative media paths so it renders these unpushed files instead of stale public `main` bytes. The current README and Overview no longer reference the older pre-icon automatic-inspector, overlay, or error-row screenshots. `Publish-VisualStudioMarketplace.ps1` includes only the six media files referenced by the selected Overview.

The debuggee fixture, its focused regression, capture automation, and documentation media do not change the frozen VSIX bytes or the installed-product qualification result above.

The Marketplace publisher dry run passed release-communication validation and produced exactly the six Overview media assets without publishing.

## Evidence

```text
Responsive matrix:
D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\responsive-matrix-final3\layout-widths.json

Full viewer workflow:
D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\docked-full-final\layout-widths.json

Automatic Inspector layouts:
D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\automatic-layout-final

Button/theme-state smoke:
D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\smart-type-mapper-theme-final

Pre-Clear UI candidate historical VS17.14 result:
D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\installed-vs17.14-final\AutomaticCollections\AutomaticCollections-installed-vsix.json

VS18.8 installed result:
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\AutomaticCollections-installed-vsix.json

Clear/reset responsive workflow:
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\docked-layout-release-final\layout-widths.json

VS17.14 exact-candidate installed result:
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2022\AutomaticCollections-installed-vsix.json

VS18.8 before/after/reopen captures:
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\automatic-collections-before-clear.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\automatic-collections-after-clear.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\automatic-collections-after-reopen.png

Current Locals/color-Doctor Marketplace capture:
D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-marketplace\IndustrialMarketplace-installed-vsix.json

Current code-DataTip Marketplace capture:
D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-datatip-run1\IndustrialDataTip-installed-vsix.json

Current six-asset Marketplace dry run:
D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-readiness-2.0.2\20260809-color-datatip\vs-publish-six-assets.json
```

## Closure record

```text
Status: Complete
Scope: Approved 2.0.2 UI modernization, always-on bounded exact-Mat collection discovery, responsive/splitter hardening, complete Clear presentation reset, exact package generation, and installed verification on the two currently installed stable IDEs.
Acceptance criteria: retired collection option absent and policy always on -> pass; legacy settings compatibility -> pass; Clear empties every document/presentation state while preserving Inspector visibility -> pass; Interpret controls blank/disable on Clear and repopulate after reopen -> pass; 320-1160 px toolbar/guidance/splitter bounds -> pass; icon discoverability and shared visual states -> pass; Release build/self-tests/contracts/package guard -> pass; exact-candidate installed VS17.14/VS18.8 scenarios -> pass.
Verification: Commands, exact results, host versions, monitor bounds, screenshots, result JSON, VSIX length, and SHA-256 are recorded above.
Evidence: D-drive paths listed above and the current VSIX at the recorded path.
Boundary / next dependency: No commit, push, CI, Marketplace upload, or public readback was performed. Exact-current VS17.9 installed runtime was not repeated because that temporary host was removed; current 17.9 support evidence is compile/package plus the earlier unchanged-architecture runtime qualification.
```

# Raw Buffer Visualizer 2.0.2 UI modernization work contract

Status: Complete

## Outcome

Remove the user-facing Mat collection toggle, make bounded exact-Mat collection discovery part of every Automatic Vision Inspector scan, and modernize the Tool Window controls with Visual Studio-native icons, clear empty-state guidance, responsive labels, command availability, and Visual Studio theme colors.

## Included scope

- Production Automatic Vision Inspector scans always include supported exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays.
- The persisted preference contains only `Auto Inspect on Break`.
- The `Mat collections` checkbox and its saved-state/status coupling are removed.
- Primary and repeated inspector commands use the Visual Studio 17.9 image catalog without adding a package or bitmap asset.
- Empty viewer guidance and image-dependent enabled/disabled states are explicit.
- `Clear` resets every document-owned presentation surface while preserving responsive layout and the current Inspector open/closed state; a later Open or Scan Now restores Interpret values.
- Wide and Compact layouts retain the existing workflow and use the current Visual Studio theme.
- Current documentation and 2.0.2 release evidence describe the new behavior.

## Excluded scope

- Camera acquisition or control.
- 3D, depth-container, or point-cloud visualization.
- Vendor SDK redistribution or direct vendor-named adapters.
- New collection types, broader `IEnumerable` traversal, or relaxed scan limits.
- Commit, push, or Marketplace publication.

## Acceptance criteria

1. No production UI or persisted setting exposes `Mat collections`.
2. Every production manual and Break Mode scan enables exact-Mat collection discovery.
3. Existing limits remain 8 elements per collection, 16 elements and 8 collection roots per scan, with isolated element failures.
4. Old settings that contain `includeImageCollections` still load the Auto Inspect choice and omit the retired field on the next save.
5. Empty/loaded command states and Wide/Compact toolbar presentation pass deterministic UI smoke checks.
6. Visual Studio-native icons and themed resources compile against the 17.9 SDK and render in a currently installed Visual Studio instance.
7. The rebuilt 2.0.2 VSIX passes aggregate tests, package/manifest checks, and installed smoke verification.
8. Clear leaves zero rows, empty guidance, no renderer frame, blank/disabled Wide and Compact Interpret controls, no diagnosis/pixel/marker/comparison state, unchanged Inspector visibility, and repopulated Interpret controls after reopen.

## Structural proof

- Previous owner: `AutomaticInspectionPreferences.IncludeImageCollections` and `IncludeImageCollectionsBox` jointly controlled scanner policy.
- Intended owner: `RawBufferToolWindowControl.ScanLocals` owns the product policy that supported Mat collection discovery is always on; `AutomaticInspectionPreferences` owns only the durable Auto Inspect trigger choice.
- Observable contract preserved: scan caps, exact-type eligibility, element isolation, debugger visualizer paths, and explicit Scan Now action.
- Removal proof: search for the retired setting/control/event identifiers in current source, tests, scripts, and current documentation.

## Required evidence

- Fresh before and after screenshots stored under `D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809`.
- Focused preferences/scanner tests and aggregate self-tests.
- 17.9 SDK Release build and VSIX package validation.
- Deterministic Wide/Compact layout smoke and one installed Visual Studio smoke.

## Completion evidence

- Release build: passed with 0 errors and 18 existing `VSTHRD010` warnings.
- Aggregate self-tests, release communication, and Environment Check contracts: passed.
- Responsive matrix: 14 content widths from 320 through 1160 px; toolbar and empty guidance stayed inside bounds.
- Splitters: image-list min/mid/max, compact Inspector 120/168/300 requests, and right Inspector 180/300/420 requests preserved the 140 px viewer and 100 px main-content limits.
- Discoverability: `Open`/`Clear`/`Save` use standard native monikers with accessible names and explanatory tooltips; `Fit`/`1:1` retain text at every width.
- Clear/reset layout: 540, 900, and 1160 px passed the empty renderer/guidance, disabled blank Interpret, cleared diagnosis/pixel/marker/comparison, Inspector-preservation, and reopen-repopulation contract.
- Exact-candidate installed scenarios: VS17.14 and VS18.8 passed with seven collection rows, five opens, two isolated failures, diagnosis-before-Clear, complete Clear reset, Interpret repopulation after rescan, duplicate-free rescan, retired option absent, always-on discovery, and zero package-protocol errors.
- Candidate: 2,493,359-byte VSIX, SHA-256 `46A84EFC5AB685139B3C16B0DA0C35B6277E359D05E3E6198CA9925D737E5D57`.
- Current evidence root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809`; earlier UI-modernization evidence remains under its original root.

```text
Status: Complete
Scope: Approved collection-policy, Tool Window UI modernization, and complete Clear presentation reset only.
Acceptance criteria: criteria 1-8 above -> pass.
Verification: Release build, aggregate tests, contracts, deterministic responsive/full-layout smokes, current package guard, and installed VS17.14/VS18.8 smokes passed.
Evidence: D-drive evidence root, release-qualification-2.0.2.md, and current VSIX/hash above.
Boundary / next dependency: The completed UI source is included in the approved 2.0.2 commit/push and CI gate. Marketplace upload/readback remains owner-controlled. Exact-current VS17.9 installed UI was not rerun because the temporary host was removed.
```

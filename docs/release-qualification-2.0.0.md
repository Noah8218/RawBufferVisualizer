# Release Qualification: 2.0.0

Last verified: 2026-08-05 KST.

## Release State

This record qualifies the exact local `2.0.0.0` VSIX as an unchanged installable candidate. It does not claim that Visual Studio Marketplace serves version 2.0.0; the public item remains `1.0.53.0` until a separately approved upload and readback.

## Frozen Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Manifest version: 2.0.0.0
Size: 1,914,538 bytes
SHA-256: 2A6D94016B03430BDF2EF5ECCF6282D32896C02AEFB3A9EB8F5519AFE4B13512
Marketplace extension ID: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
```

`candidate-identity.json` beside the VSIX records the same identity. The candidate was frozen before installation and its SHA-256 was rechecked after both IDE runs. Do not rebuild into or overwrite this directory.

## Installed Package Equality

| IDE | Host | Installed extension root | Result |
| --- | --- | --- | --- |
| Visual Studio 2022 Community | `17.14.37516.0`, x64 | `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_f2675563\Extensions\skfxg4bz.abo` | Manifest `2.0.0.0`; 93 package-owned files compared; 0 missing or mismatched |
| Visual Studio 2026 Community | `18.8.12023.21`, x64 | `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_19923728\Extensions\mkmw2iuo.qcs` | Manifest `2.0.0.0`; 93 package-owned files compared; 0 missing or mismatched |

The install logs contain a harmless exit-code `1002` warning while attempting to remove the absent legacy split ToolWindow extension. The current hybrid package installed successfully in both profiles without registry repair or `/ResetSkipPkgs`.

## Installed Runtime Matrix

| Check | VS2022 | VS2026 | Evidence |
| --- | --- | --- | --- |
| Release announcement | Pass | Pass | `New in Raw Buffer Visualizer 2.0.0` with the three 2.0 compatibility highlights |
| Environment Check | Pass | Pass | Host version, extension `2.0.0.0`, and writable temp storage all `[Ready]` |
| Repeated panel buttons | Pass | Pass | **Environment** and **What's New** each open and close when selected again; no scan/image open side effect |
| Persisted automatic setting | Pass | Pass | `Auto Inspect on Break` restored visibly; reopening alone left `0 images` until debugging |
| Multi-library automatic inspection | Pass | Pass | `8 detected: 8 opened, 0 need mapping, 0 failed.` |
| Registered `RawBufferView` | Pass | Pass | Locals shows `rawBufferView` as `RawBufferVisualizer.Sdk.RawBufferView`; visualizer result reports `640x484 Mono8 mem 1 tiles` |
| Monitor placement | Pass | Pass | Main window origin `(-1895, 385)`, size `1870 x 1035`, intersecting leftmost `\\.\DISPLAY2` bounds `(-1920, 365, 1920, 1080)` |

The eight automatic rows were:

1. registered-shape `PinnedRawBufferView` Mono8, 100%;
2. registered-shape `PinnedRawBufferView` BGR24, 100%;
3. OpenCvSharp `Mat`, 100%;
4. Emgu CV `Mat`, 100%;
5. padding-aware neutral frame, 90%;
6. stride-aware neutral frame, 94%;
7. offset-aware neutral frame, 90%;
8. sized-buffer neutral frame, 90%.

These are repository-owned neutral fixtures. They do not certify a proprietary SDK, driver, camera, frame grabber, or board.

The legacy `SmokeMultiLibraryDebug.ps1` harness was not executed during this qualification. Its stale vendor-labelled row expectations were aligned with the same eight neutral fixtures above and the updated script passed PowerShell parsing; the equivalent installed-extension workflow was exercised directly in both IDEs and is the UI evidence reported here.

## Evidence Paths

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\candidate-identity.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\install\vs2022-install.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\install\vs2026-install.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\install\vs2022-installed-package-audit.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\install\vs2026-installed-package-audit.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\before\vs2022-release-announcement-1.0.53-before.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2022-release-announcement-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2022-registered-and-mapped-buffers-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2022-rawbufferview-registered-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2026-release-announcement-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2026-environment-open-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2026-registered-and-mapped-buffers-2.0.0-after.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\ui\after\vs2026-rawbufferview-registered-2.0.0-after.png
```

## Durable Closure

Status: Complete

Scope: Exact local `2.0.0.0` package identity, dual-IDE installation/package equality, 2.0 release/environment UI, persisted automatic setting, neutral eight-buffer automatic inspection, and registered `RawBufferView` visualization.

Acceptance criteria: frozen version/size/hash recorded -> pass; installed files match candidate in VS2022 and VS2026 -> pass; extension loads as 2.0.0.0 -> pass; required UI toggles reopen/reclose -> pass; saved settings restore without unintended scan -> pass; eight neutral registered/mapped/library buffers open with zero mapping/failure -> pass; registered RawBufferView visualizer opens -> pass.

Verification: aggregate Release self-tests and Release solution build passed before candidate freezing; release-communication guard passed for `2.0.0`; the exact unchanged VSIX passed the installed matrix above in both IDEs; final no-write release checks are recorded with this working tree.

Evidence: Frozen candidate identity, install logs, package audits, screenshots, and source contract documents listed above.

Boundary / next dependency: Marketplace publication, public-package update from `1.0.53.0`, public asset/readback equality, Git commit, and Git push were not performed by this qualification. They require separate owner action or request. This result does not authorize a proprietary SDK adapter or hardware compatibility claim.

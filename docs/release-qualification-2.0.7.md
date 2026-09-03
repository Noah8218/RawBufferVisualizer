# Raw Buffer Visualizer 2.0.7 Installed Review Candidate

Date: 2026-09-03 KST

## State

`2.0.7.0` is a local stable-channel review candidate. The official Visual Studio Marketplace Gallery API was read back on 2026-09-03 and returned public `2.0.6.0`. Version 2.0.7 therefore uses a new immutable package identity for the signed 32-bit single-channel feature instead of changing the already-public package.

This candidate adds `Int32` 2D image support for OpenCvSharp `CV_32SC1`, Emgu CV `Cv32S` C1, mapped `int[]`, `RawBufferView`, and `RawBufferSnapshot`. It preserves signed pixel values and four source bytes, renders a visible grayscale preview by signed min/max normalization, and supports direct visualizers, Automatic Inspector, supported Mat collections, byte order, sampled/tiled reads, and padded stride.

`CV_32SC1` is one channel of signed 32-bit integers. It is not 32 channels. `CV_32SC2`, `CV_32SC3`, `CV_32SC4`, arbitrary 32-channel data, and 3D containers remain outside this candidate.

The working source is based on commit `27b6d3ce75b44ab2c747428744e9f859138fed91` on `agent/vs2022-17.9-compat` with uncommitted 2.0.7 changes. No commit, push, tag, GitHub Release, Marketplace upload, publication, or deployment was performed for 2.0.7.

## Exact Review VSIX

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,513,312` bytes |
| SHA-256 | `DC0648E72859A9019701A0ED075B9D4D757F85FAE2DC7F33591CBD448B33C9FF` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Manifest version | `2.0.7.0` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72` |

## Installed Industrial Runtime Result

The exact candidate was installed in Visual Studio 2022 Community `17.14.37516.0`. The test selected the smaller left monitor dynamically as `\\.\DISPLAY2`, bounds `-1920,365,1920,1080`; the verified Visual Studio rectangle was `-1900,385,1880,1040`.

The source was the recorded CC0 1280 x 960 PCB photograph with SHA-256 `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46`. The debug fixture converts that image to one signed intensity value per pixel; grayscale output is therefore the correct display for this single-channel format.

Direct code-DataTip visualization passed without the fallback visualizer menu. `labelMat` opened as OpenCvSharp `CV_32SC1` / `Int32`, 1280 x 960, stride 5120. At pixel `(815, 386)`, the viewer reported signed value `-12208` and stored little-endian bytes `80 208 255 255` (`50 D0 FF FF`). The object name, native `Ptr`, pixel `Data` address, PID, captured state, format, size, and stride were visible in the row.

After clearing that direct result, **Scan Now** reported:

```text
5 detected: 5 opened, 0 need mapping, 0 failed.
Collections: labelMatList: 1 inspected, 1 opened.
```

The five successful rows covered OpenCvSharp `labelMat`, Emgu CV `emguLabelMat`, pointer-backed `paddedOwner`, application wrapper `paddedInt32Frame`, and `labelMatList[0]`. The padded frame retained stride `5184`, exactly `1280 * 4 + 64` bytes per row.

## Checks Run

| Check | Result |
| --- | --- |
| Routed Release solution build | Passed; 0 errors and 18 pre-existing `VSTHRD010` warnings |
| Aggregate Release self-tests | Passed |
| Signed Int32 focused tests | Passed for signed extrema/zero rendering, exact signed inspection, four raw bytes, little-/big-endian reads, padded stride, sampled/tiled/file-backed sources, mapped arrays, Buffer Doctor, and registered/automatic paths |
| Debug industrial fixture, `--int32-industrial-debug ... --no-break` | Passed |
| Legacy library matrix | Passed five OpenCvSharp and five Emgu package versions, including `CV_32SC1` / `Cv32S` C1 |
| Release communication | Passed for `2.0.7` |
| Package build and guards | Passed; exact `2.0.7.0` VSIX recorded above |
| Exact VSIX installation | Passed on VS2022 Community `17.14.37516.0` |
| Installed `Int32Industrial` scenario | Passed; direct DataTip, signed pixel/raw bytes, five automatic results, collection row, padded stride, zero package-protocol errors |
| Independent `Int32IndustrialAutomatic` scenario | Passed; five documents, zero errors, padded frame selection |
| Full docked layout smoke | Passed at 540, 900, and 1160 px; Int32 selector, toolbar bounds, addresses, same-address semantics, invalidation provenance, Clear all pointer/keyboard paths, error/recovery, and split layout checks passed |

The earlier address-provenance smoke failure was a Windows PowerShell 5 test-literal issue: the product state contained the correct `UNAVAILABLE`, last PID, and last address, but a BOM-less script compared a non-ASCII middle-dot literal incorrectly. The test now checks the ASCII state prefix and PID suffix independently. Synthetic mouse/keyboard input was also bounded and retried after explicitly restoring the test window foreground; product logic was not changed for either harness correction.

## Evidence

- Candidate/build/test root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903`
- Installed direct/runtime result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\installed-int32-direct-vs2022\Int32Industrial-installed-vsix.json`
- Independent automatic result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\installed-int32-automatic-vs2022\Int32IndustrialAutomatic-installed-vsix.json`
- Layout result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\final-layout-widths\layout-widths.json`
- Marketplace Gallery readback: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\marketplace-gallery-readback.json`
- English Overview: [marketplace-overview-2.0.7.md](marketplace-overview-2.0.7.md)
- Korean review copy: [marketplace-overview-2.0.7.ko.md](marketplace-overview-2.0.7.ko.md)
- Marketplace release notes: [marketplace-release-notes-2.0.7.md](marketplace-release-notes-2.0.7.md)

## Remaining Release Gates

1. Review the source diff and exact 2.0.7 Overview/candidate as one change set.
2. Commit and push only after explicit authorization, then run or review CI for that exact commit.
3. Run the changed installed path on stable Visual Studio 2026 before treating 2.0.7 as publication-ready. Exact VS2022 17.9 runtime remains unavailable; retain the manifest compatibility contract but do not describe the 17.14 run as exact 17.9 proof.
4. Marketplace upload, public readback, tag, GitHub Release, and deployment remain separate approvals. Do not rebuild or substitute the recorded VSIX after owner approval without producing a new hash and repeating proportionate qualification.

## Feature Qualification Closure

```text
Status: Complete
Scope: Signed 32-bit single-channel 2D visualization on direct, automatic, collection, pointer, snapshot, mapped-array, sampled/tiled, byte-order, and padded-stride paths, with an exact local 2.0.7 candidate and available-host industrial runtime evidence.
Acceptance criteria: CV_32SC1/Cv32S C1 map to Int32 -> pass; real industrial scalar image renders -> pass; exact signed value and four bytes remain inspectable -> pass; automatic OpenCvSharp/Emgu/application-wrapper/collection paths open -> pass; padded stride 5184 remains intact -> pass; 540/900/1160 layout and address lifetime checks pass -> pass.
Verification: Release build/self-tests, ten-version library matrix, no-break fixture, release communication, package guard, exact VSIX install, installed Int32Industrial and Int32IndustrialAutomatic scenarios, and full three-width UI smoke passed.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903 and the exact files listed above.
Boundary / next dependency: This is local feature qualification on VS2022 17.14, not release authorization. Stable VS2026 2.0.7 runtime, exact VS2022 17.9 runtime, source commit/push/CI, Marketplace publication/readback, tag, GitHub Release, physical camera, multi-channel signed matrices, and 3D remain unproven or out of scope.
```

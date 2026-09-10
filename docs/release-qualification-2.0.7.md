# Raw Buffer Visualizer 2.0.7 Installed Review Candidate

Date: 2026-09-03 KST

## Post-Publication Invalidation - 2026-09-04

The official Gallery API now reports public `2.0.7.0`. This qualification is no longer valid for release use. A real Visual Studio 2022 `17.9.34902.65` report (`RBV-ERROR-20260904005014-8DA38D99`) failed the direct OpenCvSharp path in `ClrCustomVisualizerVSHost` with `Unsupported Mat type: CV_32SC1`.

Forensic inspection of the exact recorded VSIX below found that `netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.dll` has SHA-256 `E23CC8782B6C87B2A81DBAB9CD830ACF355DB764E5CE2685FDFF96C8E0148954`, byte-identical to the ObjectSource packaged in 2.0.6. The current 2.0.7 source contained the `CV_32SC1 -> Int32` mapping, but the Extensibility package read this target from a stale repository-local `.build` directory while the qualified release build had been routed to `D:`. Existing package checks proved only that the entry existed, not that it matched the fresh build.

Consequences:

- Reinstalling the same 2.0.7 VSIX cannot fix this defect.
- The 17.14 installed result below remains evidence for the path exercised on that host, but it does not prove the `netstandard2.0` payload loaded by Visual Studio 17.9.
- 2.0.7 must not be reused, rebuilt under the same public version, or presented as a complete `CV_32SC1` release.
- The corrected package is 2.0.8 and must pass fresh-output/VSIX hash equality plus exact Visual Studio 17.9 direct `CV_32SC1` runtime validation before publication.

## State

`2.0.7.0` was prepared as a local stable-channel review candidate and was subsequently published. The historical text below records the pre-publication evidence; the post-publication invalidation above supersedes its release verdict.

This candidate adds `Int32` 2D image support for OpenCvSharp `CV_32SC1`, Emgu CV `Cv32S` C1, mapped `int[]`, `RawBufferView`, and `RawBufferSnapshot`. It preserves signed pixel values and four source bytes, renders a visible grayscale preview by signed min/max normalization, and supports direct visualizers, Automatic Inspector, supported Mat collections, byte order, sampled/tiled reads, and padded stride.

`CV_32SC1` is one channel of signed 32-bit integers. It is not 32 channels. `CV_32SC2`, `CV_32SC3`, `CV_32SC4`, arbitrary 32-channel data, and 3D containers remain outside this candidate.

The feature, tests, English/Korean Overview, release notes, and reviewed Int32 images are committed as `33b0a0b4855ce02aeed05f0726bf0071418611fa` on `agent/vs2022-17.9-compat`. The owner authorized that commit plus this handoff closure as one branch-push batch. Tagging, GitHub Release, Marketplace upload/publication, and deployment are not included.

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
| Release-ready short rebuild and repeated self-test | Passed; focused Release build produced 0 warnings and 0 errors, followed by two consecutive aggregate self-test passes |
| Marketplace dry run | Passed for version `2.0.7.0`, English Overview, and seven referenced media assets; no Marketplace publication was executed |

The earlier address-provenance smoke failure was a Windows PowerShell 5 test-literal issue: the product state contained the correct `UNAVAILABLE`, last PID, and last address, but a BOM-less script compared a non-ASCII middle-dot literal incorrectly. The test now checks the ASCII state prefix and PID suffix independently. Synthetic mouse/keyboard input was also bounded and retried after explicitly restoring the test window foreground; product logic was not changed for either harness correction.

## Evidence

- Candidate/build/test root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903`
- Installed direct/runtime result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\installed-int32-direct-vs2022\Int32Industrial-installed-vsix.json`
- Independent automatic result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\installed-int32-automatic-vs2022\Int32IndustrialAutomatic-installed-vsix.json`
- Layout result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\final-layout-widths\layout-widths.json`
- Marketplace Gallery readback: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\marketplace-gallery-readback.json`
- Release-ready short checks: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\release-ready-test-build-final.log` and `release-ready-short-self-tests-final.log`
- Marketplace dry run: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\marketplace-ready\vs-publish.json`
- English Overview: [marketplace-overview-2.0.7.md](marketplace-overview-2.0.7.md)
- Korean review copy: [marketplace-overview-2.0.7.ko.md](marketplace-overview-2.0.7.ko.md)
- Marketplace release notes: [marketplace-release-notes-2.0.7.md](marketplace-release-notes-2.0.7.md)

## Superseding Release Gates

1. Do not reinstall, republish, or otherwise reuse 2.0.7 as remediation.
2. Build 2.0.8 from the active routed Release output and require SHA-256 equality for every debugger-side VSIX payload.
3. Exercise the extracted packaged ObjectSource and then the same unchanged VSIX through direct `CV_32SC1` visualization on exact Visual Studio 2022 17.9.
4. Treat source commit/push, CI, Marketplace upload/readback, tag, GitHub Release, and deployment as separate approvals.

## Feature Qualification Closure

```text
Status: Incomplete
Scope: Historical 2.0.7 signed Int32 feature and available-host evidence; release qualification is invalidated for the Visual Studio 2022 registered debugger-host payload.
Acceptance criteria: source CV_32SC1/Cv32S C1 mapping -> pass; recorded VS17.14 paths -> pass; fresh ObjectSource/VSIX byte equality -> fail; exact VS17.9 direct CV_32SC1 runtime -> fail with RBV-ERROR-20260904005014-8DA38D99.
Verification: Exact 2.0.7 VSIX inspection found the netstandard2.0 ObjectSource SHA-256 equal to 2.0.6; the user-provided VS17.9 support report records the corresponding unsupported-type failure.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903; D:\OpenVisionLab-TestData\RawBufferVisualizer\RawBufferVisualizer-2.0.7-Marketplace-Upload\RawBufferVisualizer-2.0.7.vsix; error RBV-ERROR-20260904005014-8DA38D99.
Boundary / next dependency: Supersede with a newly versioned 2.0.8 package whose fresh debugger payload matches the VSIX and passes exact Visual Studio 2022 17.9 direct CV_32SC1 runtime validation.
```

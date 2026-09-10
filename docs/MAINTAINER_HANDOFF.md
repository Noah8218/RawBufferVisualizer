# Raw Buffer Visualizer Maintainer Handoff

This is the canonical continuation document for the next conversation. Read it after `AGENTS.md` and `docs/README.md`.

## Snapshot

| Item | Verified state |
| --- | --- |
| Last verified | 2026-09-10 KST: the unchanged final local 2.0.8 candidate passed seven installed scenario groups on exact VS2022 Community `17.9.34902.65`, including direct/automatic/padded signed Int32, 50-object Automatic Collections, mixed libraries, ImagePtr, ConcurrentDictionary, Release Announcement, and Environment Check. Installed payload equality was 17/17 and every scenario recorded zero package-protocol errors. The existing serviced VS2022 `17.14.37516.0` and stable VS2026 `18.9.12128.139` results remain valid. Final 320/348/540/900/1160 px layout smoke passed on `DISPLAY2` at 96 DPI; 125%-200% DPI remain external release gates. |
| Active repository | `C:\Git\RawBufferVisualizer_VSIX\RawBufferVisualizer_vs17.9_compat` |
| Branch / remote | `main`; the reviewed 2.0.8 integration was fast-forwarded and pushed to `origin/main` through `b11448b`. Push-triggered CI run `34481599292` completed successfully. The complete source/media batch is `720ceec`, immutable Marketplace media links are `9ae28f3`, and the feature-branch handoff is `fec2c87`. No tag, Marketplace mutation, GitHub Release, or deployment was performed. Repository: `https://github.com/Noah8218/RawBufferVisualizer.git`. |
| Implementation baseline | Public 2.0.7 contains the signed `Int32` source feature but a stale VS2022 debugger payload. Current local source keeps the 2.0.8 payload correction and adds bounded 50+-object Automatic Inspector workflow: stable in-place rows, 8-image/2-second initial batch, progress/Stop/load-more UI, newest-Break gating, cache-safe per-session type analysis, and readable narrow image cards. |
| Source and VSIX version | Local source metadata is `2.0.8` / `2.0.8.0`; the last recorded Gallery readback reports public `2.0.7.0`. Exact final local VSIX: 2,521,139 bytes, SHA-256 `612517FA64805853A072D19773709B6DD9D09AE9B095342028EB261086BBD9A2`; 17 critical archive/installed files matched on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026. Every older 2.0.8 path/hash in historical sections is superseded. |
| Visual Studio support | Current source retains `[17.9,18.0)` and the 17.9 SDK/package boundary. The exact final candidate passed exact VS2022 `17.9.34902.65`, serviced VS2022 `17.14.37516.0`, and stable VS2026 `18.9.12128.139`; the preceding same-feature VS2022 development build also passed actual Stop/resume. The exact 17.9 runtime gate is complete; 125%-200% DPI remain release gates. |
| Public Marketplace version | Official Gallery API returned `2.0.7.0` on 2026-09-04 KST. It is immutable but superseded; reinstalling it does not correct its packaged ObjectSource. |
| Git tags / GitHub Releases | Local annotated tag `v1.0.45` on `a23d8ad` created 2026-07-26; not pushed yet; no GitHub Release yet |
| Product stage | Last recorded Gallery baseline is public 2.0.7 and affected by the stale payload. Main-source 2.0.8 is source-tested and exact-final-candidate installed-runtime-tested on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026, with new current-UI Marketplace media. The exact 17.9 gate is complete; the supported DPI sweep remains the recorded publication gate. |
| Commit scope | Commit `720ceec` contains the routed ObjectSource/hash-gate correction, Automatic Inspector incremental refresh, cache-safety correction, responsive image-card layout, tests, release copy, current README, and current media. Commit `9ae28f3` pins all English/Korean Marketplace media to the immutable `720ceec` blobs. Integration record `b11448b` and the reviewed history are on `origin/main`; Marketplace upload, tag, and release remain separate authorization boundaries. |

Public links:

- Marketplace: https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer
- Repository: https://github.com/Noah8218/RawBufferVisualizer
- 2.0.1 CI evidence: https://github.com/Noah8218/RawBufferVisualizer/actions/runs/31074717609

## Automatic Inspector Incremental Batching Development

Status: Blocked

Scope: Current-source automatic discovery keeps matching image rows, refreshes current pointer/pixels in place, discovers up to 128 candidates, loads an initial maximum of eight with a soft two-second budget, and presents progress, Stop, Load next 8, and Load all this Break in the docked window. One current-Break executor owns the batch cursor; Run/Design/Clear/disable/dispose and newer Break generations invalidate older work. Per-runtime-type analysis is reused only as a debug-session discovery optimization and cannot bypass `Inference.CanAutoOpen` or saved-mapping authorization.

Acceptance criteria: final Release build -> pass with zero errors and 18 pre-existing `ImageTypeRecognizer` VSTHRD010 warnings; aggregate self-tests -> pass; final VSIX/install equality -> 17/17 on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026; final stable-VS2026 Automatic Inspector cache safety -> `[Map] incompleteAutomaticFrame` retained across repeated scans; final 50-object workflow -> 48 valid/2 isolated intentional failures, no duplicates, overlap rejection, Clear/reopen, next-Break 50 -> 42, pointer replacement in place, 44.17 ms scan; final Multi-Library Hybrid -> 9/0; final direct/automatic/padded `CV_32SC1`, ImagePtr, ConcurrentDictionary, release, and environment scenarios -> pass; exact-final VS2022 industrial Bitmap/Automatic Inspector/Doctor, code DataTip, and direct/automatic/padded `CV_32SC1` -> pass; exact VS2022 17.9 seven-scenario regression -> 7/7 pass with zero package-protocol errors; final 320/348/540/900/1160 px layout -> pass and visually reviewed at 96 DPI; earlier VS2022 17.14 actual Stop/resume -> pass at 17 -> 24 loaded rows; 125%-200% DPI -> unavailable.

Verification: Final routed package and aggregate tests; exact final VSIX installs in exact VS2022 17.9 instance `3b79a6ac`, serviced VS2022 instance `419f0858`, and VS2026 instance `19923728`; 17/17 SHA-256 archive/install comparison on each host; exact 17.9 seven-scenario JSON, ActivityLog, and fresh screenshot review; final installed scenario JSON and fresh screenshot review on `\\.\DISPLAY2`; final layout-width JSON; Marketplace dry run with no publication. Earlier VS2022 17.14 heavy Stop/resume evidence remains applicable to the same feature behavior.

Evidence: final VSIX and hash in the Snapshot above; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\installed-vs2026-final`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-final`; exact DataTip expression-name retry under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-object-name`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\restart-verification-20260909-235208`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\layout-widths-final`; prior actual Stop/resume under `D:\OpenVisionLab-TestData\RawBufferVisualizer\automatic-inspector\installed-vs2022-17.14\heavy-stop`; detailed owner/call path and cache invariant in [automatic-vision-inspector.md](automatic-vision-inspector.md).

Boundary / next dependency: Local implementation, exact VS2022 17.9 and stable-VS2026 validation, release copy, and dry run are complete. Publication approval remains blocked on 125%, 150%, 175%, and 200% DPI. The user's original Auto Inspect preference was restored. Source/media commit, feature-branch push, main integration, main push, and integration-commit CI are complete; upload, publication, tag, and deployment remain separate actions.

## 2.0.8 Debugger Payload Correction Candidate (Historical Pre-Change Artifact)

Historical candidate: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,515,206 bytes, SHA-256 `5E6BB8F5D4259FF7BF34359B2CD53F817B0EBAC3A3B2C8143339C6029081C04D`. Do not upload it as the current-source candidate.

The package reads the `netstandard2.0` ObjectSource from the sibling output under the active `BaseOutputPath` instead of repository `.build`. Core, SDK, ObjectSource, and deps build/VSIX hashes match 4/4; VSIX-extracted OpenCvSharp `CV_32SC1` and Emgu `Cv32S` C1 execution passes; intentionally changing the built ObjectSource makes packaging fail. An in-place update from installed 2.0.7 to this exact candidate passed on VS2022 `17.14.37516.0`; five critical VSIX/installed hashes matched, and installed direct DataTip, automatic 5/5, collection 1/1, Emgu `Cv32S`, padded stride 5184, signed pixel inspection, and package protocol checks passed. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\installed-vs2022-17.14`. See [release-qualification-2.0.8.md](release-qualification-2.0.8.md). Exact VS2022 17.9 installed runtime, serviced-VS2022 clean installation, and stable-VS2026 candidate regression are still required, so this candidate is not approved for publication.

## Invalidated Public 2.0.7 Signed Int32 Package

The exact local `2.0.7.0` review VSIX is `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,513,312 bytes, SHA-256 `DC0648E72859A9019701A0ED075B9D4D757F85FAE2DC7F33591CBD448B33C9FF`. It must not be reused: its `netstandard2.0` ObjectSource hash `E23CC8782B6C87B2A81DBAB9CD830ACF355DB764E5CE2685FDFF96C8E0148954` is byte-identical to 2.0.6, and error `RBV-ERROR-20260904005014-8DA38D99` confirms the corresponding direct `CV_32SC1` failure on VS2022 17.9.

The exact package passed installed code-DataTip and Automatic Inspector checks on VS2022 `17.14.37516.0` with a real 1280 x 960 PCB source: direct OpenCvSharp stride 5120, five automatic results with zero failures, one collection item, and padded stride 5184. Full 540/900/1160 px layout smoke also passed after fixing test-only PowerShell encoding and foreground-input timing assumptions. See [release-qualification-2.0.7.md](release-qualification-2.0.7.md) and `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903`.

## Public 2.0.6 Baseline And Preserved Candidate

The preserved local `2.0.6.0` review VSIX is `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,510,240 bytes, SHA-256 `2ED03932358F45E6B9981DE830558CACD03E7677780BAB3DFA5F8593A0E77AEA`. Its local qualification remains recorded in [release-qualification-2.0.6.md](release-qualification-2.0.6.md). The Gallery now reports public `2.0.6.0`; this metadata readback alone is not a byte-equality claim for the preserved local container.

The English/Korean 2.0.6 Overview retains the complete product explanation and states that 2.0.6 advances the immutable package version without a functional or compatibility change. All six Overview media assets were regenerated from exact installed 2.0.6 runs on VS2022 `17.14.37516.0`; the DataTip and Locals entry paths plus the complete color Buffer Doctor workflow were visually reviewed and passed the Marketplace dry run.

## Public 2.0.5 Pointer Provenance And Memory-Safety Baseline

Official Gallery API readback confirms public `2.0.5.0`. The preserved local `2.0.5.0` review VSIX is `D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\final-publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,510,595 bytes, SHA-256 `9A3A9C73AE61A83D00E5BA5BFCAC3D7F26EE2D354F080AC4F988CB6D2E6340C5`. Installed VS2022 `17.14.37516.0` runtime evidence covers direct Bitmap/OpenCvSharp/Emgu/RawBufferView/RawBufferSnapshot/ImagePtr, supported collections and arrays, expression names, pointer labels, and narrow/default/wide list widths. Public Gallery metadata does not prove that its downloadable payload has the same hash as this preserved local artifact; no such byte-equality claim is made. See [release-qualification-2.0.5.md](release-qualification-2.0.5.md) and `D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\REPORT.md`.

The former six Overview assets captured from exact installed `2.0.4.0` were superseded on 2026-09-03 by exact installed-2.0.6 media. The current static images now show object-name and pointer/pixel provenance rows; the preserved 2.0.5 technical evidence remains under its recorded evidence root.

## Historical Visual Studio 2022 17.9 Compatibility And 2.0.4 Release Candidate

The `2.0.4.0` candidate keeps the public 2.0.3 `AsyncPackage`, VSCT, generated `.pkgdef`, out-of-process provider boundary, installation range `[17.9,18.0)`, and CoreEditor prerequisite `[17.9,)`. Only the docked location and presentation of the existing full reset change. The frozen review VSIX is `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\candidate-final-2\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,493,763 bytes, SHA-256 `1707B216FE5488E9910238EEEA596D8BDCD8B0C7DB236D9BF1942DD16D746AF7`. Exact checks and installed evidence are in [release-qualification-2.0.4.md](release-qualification-2.0.4.md).

Public 2.0.3 retains its recorded installed `ConcurrentDictionary` and true-cold `ImagePtr` evidence on VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12105.206`. The user separately reported normal 17.9 operation for 2.0.3. This is user evidence, not an automated exact-host record. Local 2.0.4 keeps the ImagePtr transfer path unchanged; exact 2.0.4 ConcurrentDictionary, collection/Clear, hybrid, announcement, and industrial demonstration paths passed on the available hosts. A fresh exact-2.0.4 true-cold ImagePtr result was not recorded because the Tool Window had persisted open from prior scenarios, so the 2.0.3 cold-start evidence remains the current proof for that unchanged path.

The test-only Professional 17.9 instance remains removed. Community `17.14.37516.0` and Community `18.8.12105.206` are the available exact-package targets. Public 2.0.3 evidence remains historical in [release-qualification-2.0.3.md](release-qualification-2.0.3.md); no 2.0.3 candidate may be substituted for 2.0.4.

The earlier Gallery `2.0.0.0` metadata versus `1.0.53.0` downloadable-payload mismatch is retained as historical recovery evidence under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\prepublish-public-readback-20260806`. Gallery recovery advanced through `2.0.1.0` and the last verified public package is now `2.0.3.0`; the exact recovery candidate remains recorded in [release-qualification-2.0.1.md](release-qualification-2.0.1.md). The original failed `1.0.51` and all preserved `2.0.0` candidates remain historical evidence and must not be substituted.

The historical 2.0.4 media under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-media-final` used exact installed-`2.0.4.0` captures and showed the labelled **Clear all** UI. Those files were superseded by the exact installed-2.0.6 media recorded in [industrial-image-testing.md](industrial-image-testing.md).

## Current Change Set And Ownership

The pre-remediation technical `1.0.53` change set is pushed at `c35074b` on `origin/agent/basler-environment-1.0.53`. Do not publish that commit because it still contains the removed vendor experiment.

The `1.0.53` remediation baseline contains no direct Basler or other proprietary adapter. The remediation following `c35074b` removed the direct experiment and classifies all proprietary camera and board adapters as blocked by default:

- `VisualizerEnvironmentCheck` now reports only Visual Studio host, loaded extension registration, and temporary storage; the Environment ToggleButton opens and closes the panel, and development/media utilities remain documentation-only;
- What's New is now a ToggleButton: repeated selection closes the banner without changing the release-seen preference, while Dismiss closes it, clears the checked state, and persists the preference; the narrow-layout Inspector control was already a toggle;
- large pointer-preview page budgeting and phase/cache-state performance evidence without relaxing the five-second threshold;
- current-workspace Automatic Inspector diagnostic seam and newest-restored VSSDK discovery for Smart Type Mapper;
- current-source-verified Connect Your Buffer dialog with editable/restored roles, explicit preview, suggestion reset, dependency-free mapping save, optional pointer-template copy, and dark IDE control states;
- removed Basler provider/ObjectSource/exact registration/dedicated tests and the multi-vendor SDK assembly-audit script; retained only neutral padding/stride/offset/size safety tests and historical evidence;
- version `1.0.53`, release announcement, changelog, README, Marketplace Overview/notes, embedded notes, utility recovery documentation, and safety-contract tests;
- package guards that invalidate version-bump outputs and reject stale generated/packaged VSIX manifests.

Do not stage, commit, delete, or treat these pre-existing untracked paths as part of the release without separate inspection and user authorization:

```text
.tmp/
add-break-mode-scan.ps1
add-command-id.ps1
add-scan-locals-command.ps1
```

The `2.0.0.0` P0 VSIX was installed into both IDEs. In the same smart-type debug scenario each host detected eight candidates, opened six, exposed one mapping candidate and one isolated failure, and rendered a `640x484 Mono8 live` image. Continue/process exit marked five process-backed rows `Unavailable`, kept the copied `companyArrayFrame` available, and logged `invalidated 5 live source(s)`. Seven package-owned assemblies match the candidate, the corresponding Release build, and both installations by SHA-256. VS2022 also passed the Environment open/second-click-close regression. The test sessions were terminated after evidence capture; only transient `/debugexe` solution metadata was discarded and no repository file was saved from Visual Studio. See [release-qualification-2.0.0.md](release-qualification-2.0.0.md).

The exact `2.0.0` consolidated candidate is `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-connect-doctor-docs-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 1,923,731 bytes, SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`. The same bytes passed the P0 Break-to-Continue matrix, five Emgu and five OpenCvSharp versions, package/build/install equality, Connect Doctor, Buffer Doctor, Automatic Collections, Multi-Library Hybrid, and Environment Check on both IDEs. Canonical evidence is `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\consolidated-local-release-verdict.json`. Matching source commit `0d3ac20` is pushed and CI run `31059894280` succeeded; this paragraph is historical evidence, not the current upload state.

## Windows Reinstall Checkpoint

The source, release documents, and reusable validation scripts in the intended change set were committed and pushed to `origin/main` at `0d3ac20` before Windows reinstallation. The exact candidate and runtime evidence remain deliberately outside Git:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52
```

If the reinstall preserves `D:`, keep this directory unchanged. If the operation repartitions, formats, or otherwise replaces `D:`, copy the entire directory to external storage first and verify that the candidate is still 1,902,513 bytes with SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. A Git clone restores the source and documentation but does not restore this immutable VSIX or its installed-runtime evidence.

After reinstall, do not rebuild the qualified/public candidate for the remaining migration check. On a separate serviced VS2022 `17.14+` or stable VS2026 profile that retains exact public Marketplace `1.0.50.0`, update directly to the unchanged `1.0.52.0` VSIX without uninstall, repair, `/ResetSkipPkgs`, or manual registration repair. Restart Visual Studio and repeat ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, registration, and protocol checks. Publication already occurred; this remains post-public validation debt rather than a pending upload step.

### Post-reinstall audit - 2026-08-03

Windows and the repository were restored successfully. Git, Windows PowerShell 5.1, .NET 8/9/10 SDKs, VS2022 Community `17.14.37516.0`, VS2026 Community `18.8.12023.21`, the .NET desktop workload, `vswhere`, and the NuGet-restored `VsixPublisher.exe` are available. FFmpeg is absent and remains optional/demo-only. The verified role matrix and recovery commands are in [development-prerequisites.md](development-prerequisites.md).

The full evidence and boundaries are in [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md). Restore/build/core tests, package/registration checks, compatibility checks, preview replacement, Buffer Doctor, docked layouts, warm preview, and memory soak passed. The approved correction passes Automatic Inspector seam validation, VSSDK `17.14.2094` discovery, Environment Check unit/static safety contracts, and final-regression first benchmark-process sampled-preview access (`992 ms` for 100k; `692 ms` for 200k). Exact-public `1.0.52` and the historical pre-feedback `1.0.53` candidate passed their recorded installed checks; the newest panel-consistency current-source development package also passed the changed UI workflow in VS2022 and VS2026.

The diagnostic VSIX produced after reinstall is `1,902,521` bytes with SHA-256 `95395489AF7427B8CE6C15BE8E9236C21B87153C1C340BF3B960D100523E79C8`. It proves fresh-machine packaging only. It does not replace the immutable release candidate above and must not be used for the public-update gate.

## Product Identity

Raw Buffer Visualizer is an Image Watch-style debugger visualizer for C# machine-vision developers. Its primary product surface is one docked Visual Studio 2022 or Visual Studio 2026 window where inspected images accumulate and can be examined without adding temporary save or conversion code to the debuggee.

Primary workflow:

1. Stop at a breakpoint with an image variable alive.
2. Let **Auto Inspect on Break** discover safe image-like locals and function arguments, or click the Raw Buffer Visualizer icon in DataTip, Watch, Locals, or Autos for a registered type.
3. Append the discovered image or supported collection entries to the existing docked `Images` list.
4. Select a thumbnail, zoom/pan, and inspect X/Y, GV or channels, raw bytes, descriptor, and diagnostics.
5. Compare images or export the visible view/raw snapshot when needed.

The standalone WPF app is a sample, saved-snapshot viewer, and validation surface. It is not the primary user product.

## Scope Contract

Keep these in scope:

- `RawBufferSnapshot`, `RawBufferView`, and deliberately registered pointer-object adapters;
- `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`;
- safe current-frame discovery of debugger-visible pointer/managed-array image shapes;
- supported lists, dictionaries, arrays, and mixed collections;
- large raw image display, pixel/format/stride diagnosis, comparison, and export;
- Visual Studio docking, installation/update reliability, and actionable error reporting.

Keep these out of scope unless the user explicitly starts a separate project:

- Vision Replay Debugger;
- camera acquisition/control, lighting, PLC, I/O, or recipe execution;
- vendor SDK control surfaces;
- generic media editing or labeling;
- a user-facing dependency on a rendering implementation name.

No proprietary camera or board SDK is an active direct-adapter target. Prefer Connect Your Buffer, `RawBufferView`, or `RawBufferSnapshot`; start a vendor-named adapter only after the written license gate, exact runtime/lifetime evidence, and explicit owner approval all pass.

## Completed Work

### Visual Studio integration

- A single Marketplace VSIX contains the debugger visualizer providers and docked ToolWindow required for normal operation.
- Supported invocations append to one docked image list instead of opening one window per image.
- Individual image providers exist for raw snapshot/view, Bitmap, OpenCvSharp, Emgu, and one exact ImagePtr compatibility target.
- Collection providers cover open generic `List<>`, `Dictionary<,>`, and `ConcurrentDictionary<,>`, plus `ArrayList`, `Hashtable`, `object[]`, and registered image arrays.
- Multiple Visual Studio processes use per-process handoff routing.
- Failed values remain visible as error rows with stable error IDs, reason, `Copy Report`, and `Open Logs`.
- Package repair and reinstall scripts cover stale VSIX/VSSDK registration paths.

### Viewer and UX

- Thumbnail list, selected image viewer, responsive narrow/medium/wide dock layouts, and read-only metadata.
- Mouse-wheel zoom, drag pan, Fit, 1:1, linked views, and explicit Fit/Manual interaction state. New/selected images, Fit, and double-click enter Fit; wheel/pan/1:1 enter Manual.
- X/Y, GV or RGB/channel values, swatches, raw bytes, 5x5 neighborhood/statistics, and high-zoom pixel overlay.
- Selection overlay and pinned marker behavior; live hover remains active when no marker is pinned.
- Inspector, diagnostics, line profile, histogram, and Try interpretation controls.
- Connect Your Buffer reuses Smart Type Mapper for editable role selection, explicit preview, saved reuse, suggestion reset, and optional neutral RawBufferView starter-code copy.
- A/B selection, linked pan/zoom, split, absolute difference, and blink comparison MVP.
- Save visible PNG, raw snapshot export, selected-row Delete, and Clear.
- User-facing text avoids exposing the rendering implementation.

### Input and format compatibility

- OpenCvSharp is read through reflection across legacy/current API shapes rather than binding the extension to the debuggee package version.
- Emgu is read through reflection and registered for legacy/current assembly names.
- Bitmap uses the stable .NET Framework drawing APIs.
- The ImagePtr object-source converter is shape-based (`Ptr`, `Length`, `Width`, `Height`, `Step`, `Bpp`) after invocation. The current individual provider registration is nevertheless tied to `Cressem.ImageModel.ImagePtr, Cressem.ImageModel`; arbitrary classes with the same shape do not automatically receive an icon.
- Tested OpenCvSharp points: `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, `4.13.0.20260627`.
- Tested Emgu points: `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, `4.13.0.5924`.
- Pixel formats: `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and four 8-bit Bayer patterns.

### Large-image and runtime work

- File-backed random-access image sources and tiled viewport rendering avoid materializing a full display bitmap.
- Buffers at or above 64 MiB use a sampled preview first; eligible pointer-backed sources then use direct process-memory reads.
- Preview dimensions are bounded to 512 x 512 by default.
- Large-image render requests are progressive and scoped to the visible viewport.
- Blank/single-color frame regressions during zoom were fixed before `1.0.45`.
- Delete/Clear dispose image sources and remove owned temporary directories.
- Partial handoffs are cleaned up and stale temp sessions older than 24 hours are removed.
- A docked accumulation/cleanup soak covers 240 repeated 2048 x 2048 opens.

### Buffer Doctor

- **Diagnose Buffer** panel in the Interpret section generates ranked interpretation candidates (pixel format, stride/row padding, width/height, endianness, valid bits) when an image looks broken.
- Candidate generation and scoring are pure managed and sample-bounded (at most 64 rows / 4 MiB); full buffer scans are never required.
- Candidates are previewed and applied immediately via `RawImageSource.WithDescriptor` (no debugger round-trip).
- Phase 1 covers `Mono8`, `Mono16`, `Mono10/12PackedLsb`, `RGB24/BGR24`, `BGRA32`, `Float32`; RGB/BGR and Bayer variants are surfaced as ambiguous groups with an explicit "cannot be distinguished from content" notice.
- Eight deterministic self-tests cover padded-stride detection, diagonal shear, endianness, valid-bits fit, trailing-row fit, sampling caps, and ambiguity grouping.

### Smart Type Mapper

- Users can map unsupported image classes (e.g. a company SDK `CompanyFrame`) by selecting member roles in a dialog; mappings are saved per type in `%APPDATA%\RawBufferVisualizer\type-mappings.json` and, if present, a solution-local `.rawbuffervisualizer.json`.
- Mapped types open automatically inside already-registered collections (`List<>`, `Dictionary<,>`, `ConcurrentDictionary<,>`, `object[]`, arrays) without code changes or extension rebuilds.
- The collection fallback now tries the mapping file before heuristic shape detection; failures still appear as visible error rows with a `Connect Your Buffer` action and a member inventory payload.
- **Open Variable** entry in the docked window opens individual mapped variables via EnvDTE expression evaluation while in break mode (pointer-backed data only in v1; array-backed mapped types still route through collections).
- Mapping extraction reads only fields and property getters; methods are never invoked.
- Six self-tests cover mapping file round-trip, solution-local priority, IntPtr/byte[]/ushort[] extraction, enum pixel-format mapping, failure inventory, and missing-member visible failure.

### Automatic Vision Inspector

- **Auto Inspect on Break** merges the current stack frame's Locals and Arguments after each Break Mode transition; **Scan Now** refreshes it on demand.
- The option defaults enabled after the user opens the Tool Window, persists in `%APPDATA%\RawBufferVisualizer\automatic-inspector-settings.json`, and survives a full Visual Studio restart. Breakpoints never force the Tool Window to open or steal focus.
- The recognition order is saved mapping -> known shape -> runtime type hint -> member structure -> current-value/memory validation -> Smart Type Mapper fallback.
- Direct fields/property getters and one nested member level are considered. Supported inferred data members are `IntPtr`, `UIntPtr`, `byte[]`, `ushort[]`, and `float[]`.
- Complete validated shapes at 90% or higher open automatically; results from 40% through 89% remain visible as mapping/review candidates; lower scores are hidden.
- Rows are explicit: `[Auto]` opened, `[Map]` needs metadata confirmation, and `[Failed]` was recognized but could not read its current value/memory. Each candidate has an exception boundary, and a successful row is selected before an error row.
- Automatic scanning always expands exact OpenCvSharp/Emgu `Mat` `List<T>` and one-dimensional arrays; there is no separate collection setting. Work remains capped at 8 items per collection/16 items and 8 roots per scan, while Bitmap, dictionaries, mixed lists, jagged/multidimensional arrays, and arbitrary `IEnumerable` stay on the registered collection path.
- Deterministic self-tests cover direct pointer inference, one-level nesting, ambiguous pixel format, low-confidence hiding, nested mapping extraction, `SizeX`/buffer-length role separation, and preference persistence/fallback.
- Full behavior, ownership, limits, and evidence are recorded in `docs/automatic-vision-inspector.md`.
- In `1.0.50`, initialized exact OpenCvSharp/Emgu `Mat` values have a dedicated metadata path and can open automatically; Bitmap remains on its registered debugger-visualizer glyph path because safe automatic `LockBits` evaluation is not available.
- Test breakpoints must be after the image assignment has completed. A breakpoint on the assignment line can expose the pre-assignment/null value.

### Handoff reliability

- Requests publish through a temporary `.publishing` file and atomic Ready move; the consumer exclusively claims Ready as Processing.
- Success is explicit: the consumer writes ACK only after opening the document, or NACK with a reason on rejection. A missing Ready file is not treated as success.
- Modern timeout/cancel and Classic fire-and-forget use the shared `ScheduleTerminalArtifactCleanup`: it polls every 100 ms for up to two minutes and removes ACK/NACK/conflict terminal artifacts only.
- The cleanup path never removes Ready or Processing on timeout. Cancellation/exception preserves the snapshot payload directory whenever any producer-owned request remains non-Missing.
- Locked ACK/NACK cleanup is retried, and Missing must be observed consecutively before the background reaper drops the request from its pending set.
- Outer non-cancellation exception catches also schedule terminal cleanup, closing the previous `GetRequestState` exception gap.
- The watcher registers Created and Renamed handlers before events are enabled. Request reads and terminal-marker moves retry `10 x 50 ms`.
- `ClaimedHandoffOpenCoordinator` now owns claimed-request read retry plus ACK/NACK terminal policy; the ToolWindow supplies only the UI/document opener and visible error presentation.
- `RawBufferDocumentWorkspace` owns the document collection, active-document state, removal, clear, and disposal. The ToolWindow remains the presentation/event layer.
- Every owned file-backed document holds a unique `.rbuf-active-*.lease`. The stale sweep skips a directory while the lease file is locked. Preview-to-full replacement acquires the next lease before releasing the previous one, and document/ToolWindow disposal releases and deletes owned snapshot directories.
- Current boundary: a marker that terminalizes after two minutes and a Processing item stranded by process crash are not immediately auto-reclaimed.

### Release and documentation

- Public README, Marketplace checklist/Overview source, install/update/repair guidance, release runbook, release notes, demo media, license, and third-party notices exist.
- CI, Marketplace CD, and GitHub Release workflows exist.
- `1.0.48.0` moved package/VSCT ownership into the public hybrid project and removed normal-install registry writes that had masked the `1.0.47` defect.
- `1.0.49.0` kept the Marketplace extension ID and moved the VSPackage to `{1977574b-f107-465f-bfd1-5fc022907039}`. The affected Windows 10 PC passed only after uninstall/reinstall, which remains clean-install rather than update evidence.
- Source `1.0.50.0` keeps that VSPackage GUID, uses `Menus.ctmenu, 2`, requires one copy of each View command, and adds the handoff/Fit/exact-Mat hardening described above.
- ToolWindow command exceptions now produce a visible diagnostic message rather than only a log entry.
- `CHANGELOG.md`, version-specific Marketplace Overview/release notes, VSIX-embedded release notes, and the in-product announcement are guarded by `Test-ReleaseCommunication.ps1`; the Marketplace publishing script selects the Overview by VSIX version instead of uploading README.
- The Tool Window shows the current release highlights once, only after the user opens it. Dismiss persists per user, **What's New** reopens the summary, and neither action starts inspection or opens an image.
- GitHub tag releases use the same curated version-specific release notes and attach only the standalone viewer; Marketplace remains the single user-facing VSIX distribution path.
- `1.0.51` is preserved as a failed VS2026 candidate. `1.0.52` updates the Extensibility SDK/runtime contract to `17.14`, raises the VS2022 support floor to `17.14`, and uses the unchanged Marketplace extension identity so it remains an update from public `1.0.50` on eligible IDEs.

## Verified Evidence

These are recorded regression results, not performance promises for every PC.

| Case | Evidence |
| --- | --- |
| Core/adapter tests | Release self-test passed for the `1.0.47` source on 2026-07-28. |
| Legacy libraries | Five OpenCvSharp versions, five Emgu versions, and .NET Framework Bitmap passed the 2026-07-28 compatibility matrix. |
| Installed VSIX large Mats | VS2022 17.14 opened real 8192 x 8192 OpenCvSharp and Emgu Mats; expected GV values were read; preview files stayed bounded; source-unavailable state after process exit was controlled. |
| Dense file-backed 100k | 100000 x 100000 Mono8, 10 GB non-sparse payload, first visible in 1.73 s, about 88.0 MB working set. |
| Dense file-backed 200k | 200000 x 200000 Mono8, 40 GB non-sparse payload, first visible in 1.94 s, about 87.5 MB working set. |
| Real docked mouse input | 24000 x 24000 dense Mono8 in installed VSIX: 87 wheel and 269 drag events; 6.515 ms average frame, 13.642 ms maximum frame. |
| Memory soak | 240 repeated 2048 x 2048 opens with Delete/Clear; no positive managed/private/working-set or GDI/USER growth and no owned temp directories left. |
| Current CI | `CI #71` completed successfully for `a23d8ad`. |
| Handoff recheck | On 2026-07-17, restore + Release build passed with the four recorded VSTHRD warnings and zero errors; `RawBufferVisualizer.Tests` passed. |
| 1.0.47 release qualification | On 2026-07-28, the full Release solution build passed with 18 known `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and zero errors; `RawBufferVisualizer.Tests`, packaging, reinstall, and the final hybrid installed-VSIX smoke passed. Full criteria: `docs/release-qualification-1.0.47.md`. |
| 1.0.48 registration hotfix | Release build/self-tests/package guard passed. After removing the stale developer CodeBase, normal VSIX reinstall plus restart passed Automatic Inspector, Buffer Doctor, Smart Type Mapper, and registered/automatic hybrid smokes. Artifact: 1,990,304 bytes, SHA256 `AABBD3A36780AE070C3FBBDE384CB5CD9A1977607EA929D15DAEBF75899F717D`. Full record: `docs/release-qualification-1.0.48.md`. |
| 1.0.49 package-identity recovery | Historical local Release build, self-tests, package guard, local update, Automatic Inspector, and MultiLibraryHybrid passed. Public 1.0.49 became healthy on the affected Windows 10 PC only after uninstall/reinstall; therefore the external in-place update criterion was not proven. Full record: `docs/release-qualification-1.0.49.md`. |
| 1.0.50 pre-collection local release qualification | A full Release solution build passed with 0 errors and the existing 18 `VSTHRD010` warnings; self-tests, atomic handoff/reaper tests, preview-first, and the current-source Fit matrix passed. Ordinary local reinstall of the 2,001,513-byte package on Windows 10 Pro / VS 17.14.37314.3 passed exact menu counts, automatic OpenCvSharp/Emgu, Bitmap glyph, MultiLibrary 9 documents/0 errors, Automatic partial-failure isolation, duplicate-free refresh, and 0 protocol errors. This is retained as baseline evidence but does not qualify the later Automatic Mat collection package. Full record: `docs/release-qualification-1.0.50.md`. |
| Automatic Mat collection current qualification | Exact OpenCvSharp/Emgu Mat List/one-dimensional array scanning is always included in manual and Break Mode scans; the retired checkbox and persisted field are absent. The 8-per-collection, 16-element, and 8-root bounds plus per-element failure isolation remain. Current 2.0.2 installed results on VS17.14 and VS18.8 each reported seven rows, five opens, two isolated failures, duplicate-free rescan, one Open/Scan command, and zero protocol errors. |
| Final 1.0.50 release-communication package | Exact 2,011,595-byte VSIX, SHA256 `E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93`. Release build 0 errors/18 existing warnings, self-tests, communication guard, Marketplace dry run, package inspection, and ordinary reinstall passed. Installed ReleaseAnnouncement passed persisted Dismiss, What's New reopen, and 0 image-row side effects; AutomaticCollections passed 5 opens/2 isolated failures; MultiLibraryHybrid passed 9 documents/0 errors. Each run reported one Open/Scan View command and 0 protocol errors. |
| Exact 1.0.51 final qualification | Exact 2,011,587-byte VSIX, SHA256 `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`. VS2022 `17.14.33` passed. A user-approved full VS2026 reinstall removed the old per-machine conflict and preserved the 55-component configuration, but unchanged `1.0.51` then failed registered Bitmap-provider activation on VS2026 `18.8.2` because `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` could not load. The artifact is preserved and superseded, not publishable. Evidence: `docs/release-qualification-1.0.51.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-runtime`. |
| Exact 1.0.52 local qualification | Exact 1,902,513-byte VSIX, SHA256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. Manifest `1.0.52.0` with `[17.14,18.0)` and no obsolete Classic DLL. Aggregate self-tests cover workspace lifecycle, snapshot lease/replacement/disposal, and claimed-handoff ACK/NACK. The same package passed ReleaseAnnouncement, AutomaticCollections (7 rows/5 opens/2 isolated failures), and MultiLibraryHybrid (9 documents/0 errors, Bitmap registered provider active) on VS2022 `17.14.33` and VS2026 `18.8.2`; one Open/Scan command and zero protocol errors per run. Registration audit and Marketplace dry run passed. The Marketplace script now requires an explicit VSIX path: the exact `1.0.52` dry run passed, while the preserved `1.0.51` was rejected before manifest generation. Evidence: `docs/release-qualification-1.0.52.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52`. |
| Buffer Doctor tests | 8 deterministic self-tests passed on the Core candidate generator/scorer (padded stride, diagonal shear, endianness, valid-bits, trailing-row fit, sampling cap, ambiguity group). |
| Smart Type Mapper tests | 6 deterministic self-tests passed on mapping store, ObjectSource extraction, enum mapping, and failure inventory. |
| Vendor-neutral 2D buffer validation | Public neutral pointer plus `byte[]`/`ushort[]`/`float[]` fixtures verify descriptors, bytes, valid bits, byte order, and process ownership. Registered `RawBufferView` and mapped carriers now reject invalid dimensions, stride, length, and valid-bits at the common metadata boundary. Aggregate Release self-tests and the Release solution build passed on 2026-08-05. Contract: `docs/vendor-neutral-buffer-compatibility-matrix.md`. |
| Producer-side unmanaged read safety | All registered pointer producers share a full-read-only `ReadProcessMemory` boundary. Current bytes at a reused address are read on a new transfer; freed and protected/partial ranges fail with a controlled `IOException`; readable cross-page Float32 and negative-stride Bitmap normalization pass. On 2026-08-31, ObjectSource built for net472/netstandard2.0/net8.0 with 0 warnings/errors, aggregate Release self-tests passed, and the full solution built with 0 errors plus the 18 existing `VSTHRD010` warnings. |
| Buffer Doctor UI smoke | `SmokeBufferDoctorPanel.ps1` passed: top candidate for a 2448x2048 padded Mono8 buffer is the correct `stride 2560` descriptor; applying it restores the image. Captures: `artifacts/ui/buffer-doctor/2026-07-26/`. |
| Smart Type Mapper UI smoke | `SmokeSmartTypeMapper.ps1` passed: error row shows `Map This Type`, dialog preselects members, live-memory preview renders, enum mapping saves to `%APPDATA%\RawBufferVisualizer\type-mappings.json`. Captures: `artifacts/ui/smart-type-mapper/2026-07-26/`. |
| Docked layout width smoke | `SmokeDockedLayoutWidths.ps1` passed at 540/900/1160 px after UI changes. |
| Installed VSIX Buffer Doctor | VS2022 17.14 installed-VSIX automation passed on 2026-07-27: five candidates appeared, the corrected interpretation was applied, and pixel inspection reported `GV 204`. Evidence: `artifacts/ui/installed-vsix-new-features/BufferDoctor-installed-vsix.json`. |
| Automatic Vision Inspector tests (historical base) | Five deterministic self-tests passed for confidence gates, direct/nested inference, ambiguous format handling, and nested mapping extraction before the `1.0.50` exact-Mat additions. The current aggregate self-test executable also covers Automatic Mat collections and release-announcement version/persistence behavior. |
| Automatic Vision Inspector layout | `SmokeAutomaticVisionInspectorLayout.ps1` and the focused inspector-panel smoke passed at 540/900/1160 px. Evidence: `artifacts/ui/automatic-vision-inspector/2026-07-27/`. |
| Installed VSIX Automatic Vision Inspector | VS2022 17.14 automation passed on 2026-07-28: a function argument plus five locals opened, one incomplete shape remained `[Map]`, and one null-pointer shape remained `[Failed]`; the six successful images stayed usable and repeated **Scan Now** did not duplicate rows. A second VS session restored the disabled preference, manual **Scan Now** still passed, and the pre-test user setting was restored. Evidence: `artifacts/ui/automatic-inspector-workflow/2026-07-28-final/`. |
| Installed VSIX Smart Type Mapper fallback | VS2022 17.14 automation passed on 2026-07-28: an unregistered `UnmappedCompanyFrame` remained an 88% `MappingRequired` candidate, `Mono12PackedLsb` rendered from live debuggee memory, Save wrote the inferred roles/value mapping, and automatic rescan reopened it as 640 x 484, stride 960, live source with zero final errors. The pre-existing user mapping was restored. Evidence: `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json` and the three `smart-type-mapper-*.png` captures. |
| Installed VSIX registered/automatic hybrid (historical) | Real OpenCvSharp, Emgu CV, and Bitmap values opened through registered visualizers; `RawBufferSnapshot`/`RawBufferView` and all registered types were absent from automatic rows; six camera-shape fixtures opened automatically. Final state: nine images, zero errors. The exact `1.0.50` rerun is recorded in the local release-qualification row above and replaced this file with fresh evidence. |
| Industrial SDK contract hardening | Official contracts for PFNC, Basler, Spinnaker, Vimba X, IDS peak, Euresys, HIKROBOT, Sapera, Zebra, and Zivid were reviewed. Deterministic padding/payload/offset/PFNC tests passed. IDS peak ICV 1.4.0 assembly metadata passed. Exact Basler pylon `8.1.0.16743` and `26.07.2.18500` test points each passed installed assembly audit, 11-format official camera emulation, and a real installed-VSIX Mono12 direct debugger open. General vendor-runtime, untested pylon releases, and physical hardware are not proven. Evidence: `docs/industrial-camera-compatibility-validation.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md`. |
| Responsive Inspector affordance | Fresh 540/900/1160 px captures passed. The top `Inspector` button appears only below 760 px; medium layout exposes the bottom Inspector and wide layout exposes the right Inspector. Evidence: `artifacts/ui/inspector-button-visibility/2026-07-28/before/`. |

Same-machine before/current comparison for dense 5000 x 5000 Mono8:

| Metric | Before | 1.0.45 | Change |
| --- | ---: | ---: | ---: |
| Initial open path | 179.818 ms | 115.369 ms | 35.8% lower |
| Zoom average frame | 16.684 ms | 13.765 ms | 17.5% lower |
| Zoom maximum frame | 49.397 ms | 36.527 ms | 26.1% lower |
| Pan maximum frame | 21.951 ms | 17.056 ms | 22.3% lower |
| Pan average tile upload | 20.410 ms | 15.211 ms | 25.5% lower |

## Remaining External Or Release Work

The 2026-08-03 restored-PC audit is complete. The harness seam, VSSDK discovery, sampled-preview cold-page budget, exact-public runtime, and final `1.0.53` installed checks all passed. See [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md).

1. Marketplace serves the exact qualified `1.0.53.0` asset and the rendered 1.0.53 Overview. The historical separate-profile `1.0.50 -> 1.0.52` migration was never recorded without uninstall, repair, or `/ResetSkipPkgs`; the later publication does not retroactively prove that old migration.
2. Local installed screenshots provide visual aspect evidence only. The full 540/900/1160 Fit/Manual assertions are current-source view evidence, not an installed-VSIX behavioral matrix.
3. Local tag `v1.0.45` exists on `a23d8ad` but is not pushed; there are no GitHub Releases. Release bookkeeping should follow the actual next publication decision instead of presenting the historical draft as current.
4. Marketplace CD exists, but PAT/publisher/environment approval and an actual automated publish run are not proven. Manual upload remains the known working release path.
5. The exact vendor-safe `1.0.53` candidate is locally qualified and publicly verified: 1,914,615 bytes, SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`. Matching product source/evidence commit `7ab84b7` is pushed. The historical Basler candidate remains non-publishable.
6. No active direct proprietary camera or frame-grabber/board adapter remains. `RawBufferView` and safe structural discovery are the vendor-neutral paths.
7. Current Vimba X and IDS peak terms exclude consumers; current Spinnaker terms require owned qualifying hardware/images; current Basler terms introduce purpose, marketing, derivative-distribution, indemnity, record, and audit conditions. Other proprietary camera/board SDKs are blocked by default. Do not download, test, implement, publish, or advertise a direct path without the clearance defined in [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md).
8. Stable Visual Studio 2026 `18.x` is a supported compatibility target under Microsoft's VSIX API-version model. Exact `1.0.53` runtime qualification passed on Community `18.8.12023.21`; VS2022 qualification passed on Community `17.14.37516.0`. Preview/Insiders and explicit standalone .NET 9/10 matrices are not current support claims.
9. Large 100k/200k evidence is file-backed raw-image evidence, not proof that a debuggee can safely allocate a fully decoded 100k/200k `Mat`.
10. Smart Type Mapper **Open Variable** passed on both IDE generations from a supported collection error through variable selection/open. The VS2026 follow-up routed `companyFrame` to the inferred `Connect Your Buffer` screen on the now-superseded pre-P0 candidate; no mapping was saved. This remains useful unchanged-surface regression evidence, while the preserved P0 candidate owns that historical release identity. Separate Connect Your Buffer save/restart/restore qualification already passed on both IDE generations.
11. The top `Inspector` button is intentionally visible only in narrow layout. Medium and wide layouts expose the Inspector panel directly; a consistent always-present toggle would be a separate approved UX change, not a release-fix requirement.

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy/arbitrary `IEnumerable` sequences are intentionally not enumerated while the debugger is paused.
- `.raw` or `.bin` without a descriptor cannot be interpreted safely; use `.rbuf.json` metadata.
- Live process-memory sources can be read only while the debuggee is paused and the source memory is valid. The P0 candidate disposes and marks them `Unavailable` on Continue; already-rendered pixels may remain visible but cannot trigger another source read.
- A producer read that succeeds returns the bytes available during that read. Address reuse cannot be distinguished from the original allocation, and concurrent mutation cannot be detected or made atomic; keep the source buffer alive and unchanged until the transfer finishes.
- Planar, YUV, compressed, signed multi-channel, and unsupported packed camera formats fail visibly instead of being guessed; signed single-channel `Int32` is supported.
- Current working source and the exact P0 candidate have strict shared metadata/enum validation and installed dual-IDE Continue invalidation evidence. The immutable public `1.0.53` package predates those changes, so do not attribute the P0 behavior to the public Marketplace version.
- Compatibility points do not guarantee every intermediate OpenCvSharp/Emgu package build.
- Error reports exclude image payloads but may contain local paths and variable names; review before sharing.
- Timings vary with storage, GPU driver, format, debugger state, and dock size.
- Buffer Doctor suggests ranked candidates; it does **not** automatically detect the correct format. RGB vs BGR and Bayer phase can be mathematically ambiguous for some scenes.
- Buffer Doctor scoring reads at most 64 rows / 4 MiB; very unusual corruption patterns may not score well with sampled data.
- Smart Type Mapper does **not** make the visualizer icon appear for arbitrary individual types. Unregistered current-frame values can enter through Automatic Vision Inspector or **Open Variable**; registered collections remain another supported entry.
- Smart Type Mapper v1 supports pointer-backed mapped variables for individual Open Variable; array-backed mapped variables must use the collection path.
- Smart Type Mapper mappings are per type name + assembly simple name; renaming either requires a new mapping.
- Automatic Vision Inspector scans the selected stack frame's Locals and Arguments only. It does not scan other frames/threads, fields outside the bounded root/one-level inventory, or arbitrary collection contents. Optional collection expansion is limited to exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays under the documented 8/16/8 caps.
- Managed-array extraction prefers VSSDK child enumeration. Its EnvDTE fallback is capped at 256 elements, so a debugger engine that does not expose array children may require the existing collection path or a pointer-backed view.
- Automatic confidence is structural evidence, not semantic proof. A plausible but wrong shape can still require **Edit Mapping**, and format ambiguities remain user decisions.
- The automatic path uses vendor-neutral structural fixtures, not live proprietary SDK objects. Padded rows, extra payload, and `Buffer`/`ImageData` offsets fail closed.
- OpenCvSharp/Emgu exact automatic capture requires an initialized value and a breakpoint after assignment. Bitmap remains a registered-glyph path because automatic `LockBits` evaluation is not part of the safe contract.
- Shared terminal cleanup polls for at most two minutes and removes terminal artifacts only. It intentionally leaves Ready/Processing untouched; late terminal markers and Processing items stranded by process crash are not immediately reclaimed.
- Owned file-backed documents now hold active snapshot-directory leases, but a process crash can leave unlocked lease markers and payload directories until the later stale sweep.
- Same-machine update history exists for public `1.0.50` to `1.0.51`, but `1.0.51` failed VS2026 and was superseded. The required separate-PC public `1.0.50` to exact `1.0.52` update and a full installed-VSIX 540/900/1160 Fit/Manual matrix remain unverified.

## Incident Lessons To Preserve

- A compressed TIFF file size is not the decoded `Mat` size. For example, a roughly 61 MB TIFF can decode to several GB when its dimensions are about 31800 x 96800 Mono8. The debuggee must still own that decoded memory even when the viewer uses preview-first/direct reads.
- Do not diagnose every large-image failure as a viewer memory leak. Check decoded dimensions, stride, debuggee bitness, OpenCV pixel limit, process lifetime, and whether repeated source Mats are still alive.
- Direct process-memory viewing is intentionally live. After Continue or process exit, show a controlled unavailable state; do not attempt to keep reading an invalid pointer.
- Standalone viewer speed does not prove docked Visual Studio speed. Performance acceptance must use the installed VSIX, the docked window, and real wheel/drag input.
- Provider registration controls whether the visualizer icon appears. Reflection conversion code alone is not enough; exact/legacy type and assembly registrations must remain in the generated extension metadata.
- Typed collections require open generic `List<>`/`Dictionary<,>`/`ConcurrentDictionary<,>` registration. Visual Studio's built-in `IEnumerable Visualizer` may also appear and is not this product.
- A local registry repair can make a broken VSIX look healthy. Normal install must never write registration, and a clean-PC release gate must pass without `Repair-VisualStudioExtensionRegistration.ps1`.
- Marketplace rejects an already published version. Every uploaded binary change needs a higher VSIX version, but documentation-only public copy can be edited separately when the portal permits it.
- A passing source test or newer-host smoke does not prove which TFM-specific ObjectSource the oldest debugger host loads. Compare the fresh routed build and VSIX payload by hash, then exercise the extracted payload and exact affected host.

## Do-Not-Regress Checklist

- One installable VSIX, not two user-installed extensions.
- Core, SDK, ObjectSource, and deps under `netstandard2.0` match the same fresh routed Release output by SHA-256; a mismatch aborts packaging.
- One package-registration owner: `RawBufferVisualizer.VisualStudio.Extensibility` generates `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef`; the VSSDK support library must not generate the Marketplace `.pkgdef`.
- Normal install performs no manual VSSDK registry write; repair-only results are never release evidence.
- One docked viewer session, not one window per image.
- Bitmap, OpenCvSharp, Emgu, raw, registered pointer target, list, dictionary, and array icons/opens.
- Error rows remain visible and selecting a valid row recovers normal viewing.
- Mouse hover updates continuously when unpinned; pin freezes marker/current inspection as designed.
- First image and newly selected images start with a correct Fit view.
- Wheel zoom and drag pan remain responsive in the docked window.
- Pixel/GV/channels/raw bytes, selection, Save, Delete, and Clear remain functional.
- Delete/Clear dispose sources and temporary storage.
- Active owned snapshots remain leased while their document is open; preview/full replacement and ToolWindow disposal release the old lease and directory.
- Document collection and active-state mutation remain behind `RawBufferDocumentWorkspace`; claimed handoff terminal policy remains behind `ClaimedHandoffOpenCoordinator`.
- Narrow docking retains image list, viewer, status strip, Save, and Inspector access.
- Rendering technology names stay out of Marketplace/README/UI copy.
- README/Marketplace images pass the visual review gate in `AGENTS.md`.
- Buffer Doctor candidate panel remains usable in narrow/medium/wide layouts and selecting a candidate applies the descriptor without a debugger round-trip.
- Connect Your Buffer remains available only for error rows that carry a member inventory; saving a mapping writes the expected JSON and does not corrupt existing mappings.
- Open Variable remains disabled or shows a clear message when there is no active debug session in break mode.
- Automatic scanning remains deferred until the debugger reaches stable Break Mode and does not block the debugger transition event.
- Repeated Break/Scan Now refreshes automatic rows without duplicating them or deleting manually opened rows.
- Low-confidence objects stay hidden, ambiguous candidates remain editable, and automatic rows expose confidence/member/validation details.
- Exact initialized OpenCvSharp/Emgu Mats remain eligible for automatic opening; Bitmap remains available through its registered debugger-visualizer glyph.
- Ready must be claimed exactly once as Processing, and success requires explicit ACK after document open. NACK retains a reason.
- Delayed cleanup removes only ACK/NACK/conflict terminal artifacts and never deletes Ready/Processing on timeout.
- `Menus.ctmenu, 2` and exactly one instance of each Raw Buffer Visualizer View command remain in the generated package.
- Fit preserves aspect ratio with the whole image visible; wheel/pan/1:1 switch to Manual and preserve the user's zoom/center.
- No proprietary vendor-named adapter or compatibility statement enters a release until [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) passes. Technical tests and absence of bundled SDK DLLs are not legal clearance.

## Historical Removed Basler Experiment (2026-08-04)

**Removed from active source:** The following is technical history only. It does not authorize distribution, Marketplace publication, or a Basler/pylon support claim. See [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md).

The experiment targeted exact `Basler.Pylon.IGrabResult, Basler.Pylon`. The active product direction is vendor-neutral 2D buffer inspection; 3D point clouds, depth/coordinate containers, camera acquisition/control, and PLC/I/O remain out of scope.

Historically implemented and now removed:

- dependency-free Extensibility provider and debuggee-side ObjectSource;
- exact `Image`/`TopDown`/live-pointer gates and official nullable `ComputeStride(IImage)` reflection;
- Mono8, unpacked Mono10/12/16, PFNC Mono10p/12p, Bayer 8-bit phases, RGB8/BGR8/BGRA8 mappings;
- explicit rejection of legacy packed, signed, planar, YUV, compressed, bottom-up, failed/disposed, GenDC/3D, and undersized payload shapes;
- deterministic padded-stride, payload, format, chunk/preview, and no-clone/no-dispose tests;
- SDK audit repair for inherited interface properties and Basler `ComputeStride` signature verification.

Evidence:

- Release self-tests: passed;
- Release solution build: passed; the final restored-runtime rerun reported the same 18 existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and 0 errors;
- packaged `.vsextension/extension.json`: full assembly-qualified `IGrabResult` and concrete `GrabResult` targets plus ObjectSource present;
- official Basler-owned NuGet package `10.3.2.636`: package SHA-256 `2FCDFFFCA338EE3393962398A25D33F9B8A79E7D2C4C76F9548C44CC9BC83D38`;
- contained `Basler.Pylon.dll`: assembly `1.2.0.0`, SHA-256 `C20A5F8D8140FE6C920850431EF07127C80DD5ED2B4C991A6858A102D8451F89`, metadata contract passed;
- installed pylon Software Suite `26.07.2.18500`: x64 assembly file version `9.1.0.1300`, SHA-256 `588FDD275EE2F9BE3FD6EF57A5BC99FB70793C3407806E9D18D9B86D1DA2D8DC`, metadata contract passed;
- official `BaslerCamEmu`: 11 supported 2D formats passed official-stride, lifetime, direct-memory, preview, and pointer-chunk checks;
- installed candidate VSIX SHA-256 `608A9327F0261079A95F3B7F6FB864965135BFA00105D2272D5D487A23F1EFAD`: real concrete Mono12 `GrabResult` opened at 128 x 96 in VS2022 `17.14.37516.0`;
- pylon `8.1.0.16743`: x64 assembly file version `8.1.0.471`, SHA-256 `9499FD0EE33C1156262B2BDDF1512616F7E33CE5EE3E341E4E443B9A543D69F7`; contract, 11/11 emulator frames, and a real direct Mono12 `GrabResult` open passed in VS2026 `18.8.12023.21`;
- restored pylon `26.07.2.18500`: contract, 11/11 emulator frames, and the same direct Mono12 scenario passed again in VS2026 after 8.1 removal; combined report `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md`;
- long `D:` TEMP path: actual handoff log passed `Published -> Queue -> Open start -> Open end` after short request IDs and final-name watcher filtering;
- physical evidence root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2`.

Boundary: the removed direct debugger visualizer passed a technical experiment only. It is not active or license-cleared. `Mono10p`/`Mono12p` were unavailable from the emulator profile, and no physical camera, transport driver, requeue/disposal across resume, or 3D claim passed.

## Next Priorities

The exact 2.0.8 correction VSIX, English/Korean Marketplace copy, refreshed exact-installed media, package-integrity checks, extracted-payload tests, and exact-candidate runtime regressions on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026 are committed and pushed to `origin/main`. All seven immutable Overview media URLs returned HTTP 200 with the expected content type and exact local byte length. Integration-commit CI run `34481599292` passed; Marketplace publication has not occurred. Exact Visual Studio 2022 17.9 runtime proof is complete; the supported DPI sweep remains the current gate.

1. Exact VS2022 17.9 direct `CV_32SC1` validation — Complete | Recommended model: `gpt-5.6-luna` | Reasoning effort: `low`

   The unchanged SHA-256-recorded 2.0.8 VSIX passed on Community `17.9.34902.65`: 17/17 installed files matched, seven scenario groups passed, and package-protocol errors remained zero. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final`.

2. Run the 125%, 150%, 175%, and 200% DPI sweep | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

   Reuse the unchanged exact candidate, exercise the changed docked viewer at each supported scale, and record normal, hover, pressed, focused, selected, disabled, progress/Stop, narrow-width, resize, and scrolling states. A source-only or 96-DPI result is not sufficient.

3. Review the 2.0.8 English/Korean copy, refreshed media, and exact VSIX | Recommended model: `gpt-5.6-luna` | Reasoning effort: `low`

   Review [English Overview](marketplace-overview-2.0.8.md), [Korean copy](marketplace-overview-2.0.8.ko.md), [release notes](marketplace-release-notes-2.0.8.md), [qualification](release-qualification-2.0.8.md), and the exact media listed in [industrial-image-testing.md](industrial-image-testing.md).

4. Commit, feature-branch push, main integration, and main push — Complete | Recommended model: `gpt-5.6-luna` | Reasoning effort: `low`

   Commits `720ceec` and `9ae28f3` are present on `origin/main`; integration record `b11448b` was pushed and CI run `34481599292` passed.

5. Upload only after CI and separate publication approval | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

   Use the 2.0.8 files linked above. After separately authorized publication propagates, download the public VSIX and require exact size/hash/manifest/payload equality. Tag and GitHub Release remain later approvals.

Do not describe extracted-payload execution or a newer-host run as exact Visual Studio 17.9 runtime proof.

Blocked future work: any direct proprietary camera/frame-grabber/board adapter requires applicable written vendor rights, required legal review, and explicit owner implementation approval first. Do not spend model tokens on implementation until those prerequisites exist.

Owner decision recorded 2026-07-26: the ImagePtr provider/public-contract mismatch (former priority 1) is intentionally left unchanged. The exact `Cressem.ImageModel.ImagePtr` registration is company-specific support and stays as is; the broader public wording question is deferred.

## Vendor-Neutral 2D Buffer Validation Closure

Status: Complete
Scope: Vendor-neutral 2D carrier/layout compatibility contract, common metadata validation, and public repository fixtures for supported and fail-closed paths
Acceptance criteria: pointer and managed carriers covered; dimension/stride/length/offset/format/valid-bits/byte-order/lifetime decisions recorded; registered and mapped paths reject invalid descriptor/length/valid-bits; valid values preserve metadata and bytes; no proprietary SDK, UI, 3D, package, or version change
Verification: aggregate Release self-tests passed; Release solution build passed with 0 errors and the 18 pre-existing `VSTHRD010` warnings; changed-document links and `git diff --check` passed
Evidence: [vendor-neutral-buffer-compatibility-matrix.md](vendor-neutral-buffer-compatibility-matrix.md); `src\RawBufferVisualizer.Core\RawBufferDiagnostics.cs`; `src\RawBufferVisualizer.VisualStudio.ObjectSource\VisualizerChunkedTransfer.cs`; `tests\RawBufferVisualizer.Tests\IndustrialCameraContractTests.cs`; TEMP/TMP root `D:\OpenVisionLab-TestData\RawBufferVisualizer\2.0-buffer-validation\final`
Boundary / next dependency: This contract later passed exact installed `2.0.0` qualification and was integrated to `origin/main` at `3d88894`; see [release-qualification-2.0.0.md](release-qualification-2.0.0.md). Publication, public readback, and a real public update remain separate owner-approved work.

## Exact Start For The Next Conversation

Run the repository orientation commands, verify that the public Gallery state has not changed, confirm the local-main and remote-main boundary, and verify the exact local artifact without rebuilding it:

```powershell
Set-Location C:\Git\RawBufferVisualizer_VSIX\RawBufferVisualizer_vs17.9_compat
git status --short
git log --oneline -5
git branch --show-current
git rev-parse HEAD
git rev-parse origin/main
git rev-parse origin/agent/vs2022-17.9-compat

$candidate208 = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\candidate-final\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
Get-Item -LiteralPath $candidate208 | Select-Object FullName, Length
Get-FileHash -LiteralPath $candidate208 -Algorithm SHA256
```

Expected before review: a clean `main` synchronized with `origin/main` and containing integration record `b11448b` plus the following documentation-only push record; manifest `2.0.8.0`; 2,521,139 bytes; and SHA-256 `612517FA64805853A072D19773709B6DD9D09AE9B095342028EB261086BBD9A2`. The exact candidate is installed on VS2022 profile `17.0_419f0858` and VS2026 profile `18.0_19923728`; each installation matched 17/17 critical candidate files. Read [release-qualification-2.0.8.md](release-qualification-2.0.8.md) before any release action. Do not rebuild, reinstall, manually dispatch CI, upload, tag, publish, or deploy without the corresponding explicit authorization.

## Historical Exact-Start Record

Run the repository orientation commands, then verify the local 2.0 candidate, public 1.0.53 identity, and preserved historical artifacts:

```powershell
Set-Location C:\Git\RawBufferVisualizer
git status --short
git log --oneline -5
git branch --show-current

$failed = 'C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$previousPublic = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$public = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018\RawBufferVisualizer-public-1.0.53.0.vsix'
$candidate = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$qualified = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate-vendor-safe\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$two = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$p0 = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-p0-20260805\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$focused = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-connect-doctor-docs-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$currentDev = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\current-source-vsix\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
Get-Item -LiteralPath $failed, $previousPublic, $public, $candidate, $qualified, $two, $p0, $focused, $currentDev | Select-Object FullName, Length
Get-FileHash -LiteralPath $failed, $previousPublic, $public, $candidate, $qualified, $two, $p0, $focused, $currentDev -Algorithm SHA256
```

Expected:

- failed `1.0.51`: 2,011,587 bytes, `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`;
- previous public `1.0.52`: 1,902,513 bytes, `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`;
- public and qualified vendor-safe `1.0.53`: 1,914,615 bytes, `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`, manifest `1.0.53.0`;
- superseded pre-P0 `2.0.0`: 1,914,538 bytes, `2A6D94016B03430BDF2EF5ECCF6282D32896C02AEFB3A9EB8F5519AFE4B13512`, manifest `2.0.0.0`; historical baseline only, do not publish;
- preserved full-safety P0 baseline `2.0.0`: 1,917,791 bytes, `3C2DCC1E9E38990D1C17547331E15C5EE344ABEA07D3936B722747B0670AE7EE`, manifest `2.0.0.0`;
- current qualified `2.0.0`: 1,923,731 bytes, `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`, manifest `2.0.0.0`; complete matrix, source push, CI, clean install, and in-place update passed, held for owner approval;
- historical pre-feedback `1.0.53`: 1,911,715 bytes, `EB94CCE2144E1325FDFDB2DF8A63C383F9B4E82DFD2F8EF504CAEECB99220534`, manifest `1.0.53.0`; do not publish it;
- intermediate essential-only development `1.0.53`: 1,907,942 bytes, `297A01104993CB7C524EE418759FDF0EAB41D07A0766A018B1A475F793219CF9`, manifest `1.0.53.0`; superseded by the panel-consistency follow-up;
- newest panel-consistency development `1.0.53`: 1,908,045 bytes, `5EC07758A9592F9F47C7479B005E205AA255A60D6F1400586A37F5B3B8024C23`, manifest `1.0.53.0`; installed UI evidence only, not yet canonical.

Do not rebuild into or overwrite preserved artifacts. Read `docs/release-qualification-2.0.1.md` before current release work, then `docs/release-qualification-2.0.0.md` for the complete feature qualification. Gallery metadata is `2.0.0.0`, but its downloadable payload remains the exact `1.0.53.0` package. Only the recorded `2.0.1.0` recovery candidate is eligible for the next upload after its remaining gates pass.

## Current Release Artifacts

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\candidate-marketplace-recovery-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Current exact 2.0.1 recovery candidate:

```text
Version: 2.0.1.0
Size: 1,923,778 bytes
SHA256: 5E1C112089C7BE39067BC2B17C7E3D75659575356F1E8EF9FA06A7BDA955A20B
Source: commit 0d57e10, pushed to origin/main
CI: run 31074717609, successful
```

This artifact passed the Release build, aggregate tests, release/environment contracts, package guards, exact manifest/identity inspection, Marketplace dry run, source push/CI, in-place installation on VS2022 and VS2026, 14/14 package-to-install assembly comparisons, and the required installed runtime scenarios. Rebuilding changes the VSIX hash and invalidates this exact record. Gallery metadata currently reports `2.0.0.0`, while its downloadable payload is the separate `1.0.53.0` binary above. Publication to the existing item and public readback remain owner-controlled. See [release-qualification-2.0.1.md](release-qualification-2.0.1.md).

The prior 1,923,731-byte `2.0.0.0` candidate remains the qualified feature baseline and must not be uploaded to repair this higher-version Marketplace state.

## Qualified Vendor-Safe 1.0.53 Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate-vendor-safe\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 1.0.53.0
Size: 1,914,615 bytes
SHA256: E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3
Source: commit 7ab84b7
```

The exact package passed ReleaseAnnouncement, EnvironmentCheck, AutomaticCollections, MultiLibraryHybrid, SmartTypeMapper, and SmartTypeMapperPersisted on VS2022 `17.14.37516.0` and VS2026 `18.8.12023.21`. BufferDoctor, AutomaticVisionInspector, and OpenVariable also passed on VS2022. Five package-owned files matched the installed payload by length and SHA-256 in both IDE profiles; registration audit, release/environment contracts, layout, memory soak, Marketplace Dry Run, parser, and diff checks passed. Full evidence is [release-qualification-1.0.53.md](release-qualification-1.0.53.md). Do not rebuild it. Commit `7ab84b7` is pushed and Marketplace publication/readback is complete.

## Historical Local 1.0.53 Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 1.0.53.0
Size: 1,911,715 bytes
SHA256: EB94CCE2144E1325FDFDB2DF8A63C383F9B4E82DFD2F8EF504CAEECB99220534
Source baseline: acc467f plus the qualified 1.0.53 release change set; use git log for the pushed commit
```

Internal package inspection found 94 entries, manifest `1.0.53.0`, and embedded `Raw Buffer Visualizer 1.0.53` notes. The first incremental package attempt exposed a stale generated `1.0.52.0` manifest; that artifact was rejected. `Bump-VisualStudioExtensionVersion.ps1` now invalidates generated manifests/VSIX outputs, and `Publish-VisualStudioExtension.ps1` rejects generated or packaged versions that differ from the source manifest. Actual VS2022 review then found and corrected build-suffixed host-version parsing and a compact-width Close action clipping defect. This package subsequently became historical when owner feedback removed optional utility rows/actions and the separate Close button. Preserve it as evidence, but do not publish it.

## Intermediate Essential-Only 1.0.53 Development Package

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\current-source-vsix\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 1.0.53.0
Size: 1,907,942 bytes
SHA256: 297A01104993CB7C524EE418759FDF0EAB41D07A0766A018B1A475F793219CF9
Source baseline: bb23756 plus the uncommitted essential-only Environment follow-up
```

VS2022 installed it under `17.0_f2675563\Extensions\i00kqfbx.m2v`; VS2026 installed it under `18.0_19923728\Extensions\d4t0qvg1.cnd`. Both manifests report `1.0.53.0`, and both product assembly hashes match the VSIX payload. VS2022 Wide and VS2026 Compact showed only host/extension/temp, Refresh, and Copy; no .NET/VSSDK/FFmpeg or installer surface remained; repeated Environment selection closed the panel and reported `Environment check closed`. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle`. The later panel-consistency package below supersedes this intermediate development package.

## Panel-Consistency 1.0.53 Development Package

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\current-source-vsix\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 1.0.53.0
Size: 1,908,045 bytes
SHA256: 5EC07758A9592F9F47C7479B005E205AA255A60D6F1400586A37F5B3B8024C23
Source baseline: bb23756 plus the uncommitted essential-only Environment and panel-toggle consistency follow-ups
```

VS2022 installed it under `17.0_f2675563\Extensions\vxty5daa.2d4`; VS2026 installed it under `18.0_19923728\Extensions\nmzdwcrf.z0i`. Both manifests report `1.0.53.0`. The VSIX and both installations contain `RawBufferVisualizer.VisualStudio.dll` SHA-256 `8AB612A6...6FD636` and `RawBufferVisualizer.VisualStudio.Vssdk.dll` SHA-256 `95B424C4...0FB65` at matching lengths. Actual VS2022 Wide and VS2026 Compact interactions proved What's New first-open, repeated-button close, reopen, and Dismiss close with an unchecked final state. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency`. This is current-source validation evidence, not the canonical Marketplace candidate.

### Automatic Mat collection locally qualified candidate (2026-07-31)

- Added a persisted, default-off **Mat collections** option.
- Added exact bounded automatic expansion for `List<OpenCvSharp.Mat>`, `List<Emgu.CV.Mat>`, `OpenCvSharp.Mat[]`, and `Emgu.CV.Mat[]`.
- Added per-element `[Auto]`/`[Failed]` isolation, collection summaries, duplicate-free indexed stable keys, exact type policy tests, legacy-setting migration tests, and direct `Bitmap[]`/Emgu `Mat[]` registered-transfer regressions.
- Release build passed with 0 errors and the same 18 existing `VSTHRD010` warnings; current-source self-tests and the Automatic Inspector layout smoke passed.
- Historical pre-announcement package: 2,007,042 bytes, SHA256 `01D4B19276625F4F73883C5113C8DA2044D3F5B7EAC49D6D1D3994CA727ECE3A`.
- Before/after UI evidence: `artifacts/ui/automatic-collection-inspector-20260731/before/automatic-inspector-1160.png`, `artifacts/ui/automatic-collection-inspector-20260731/after-final-v3/automatic-inspector-1160.png`, and narrow `artifacts/ui/automatic-collection-inspector-20260731/narrow-after-v3/automatic-inspector-540.png`.
- Ordinary scripted reinstall completed in 63.7 seconds and validated installed metadata. `AutomaticCollections` then passed with seven rows, five opens, two isolated failures, duplicate-free rescan, restored collection preference, one View/Open and one Scan command, and zero protocol errors. Evidence: `artifacts/ui/installed-vsix-new-features/AutomaticCollections-installed-vsix.json` and `automatic-collections.png`.
- Historical final 1.0.50 development package: 2,011,595 bytes. Its installed `ReleaseAnnouncement` proved the 1.0.50 title, saved Dismiss, What's New reopen, and no image-list side effects; the later failed `1.0.51` evidence is recorded in `docs/release-qualification-1.0.51.md`.
- The reinstall script now waits only for the direct VSIXInstaller process and stops idle `Microsoft.ServiceHub.Controller` processes between operations. This prevents quiet reinstall from hanging on a descendant ServiceHub process; the parser and a real uninstall/legacy-uninstall/install cycle passed.

## Build Environment Note (2026-07-26)

The Kimi/embedded Git Bash environment on this machine starts without standard Windows variables (`ProgramFiles`, `ProgramFiles(x86)`, `ProgramData`, `ComSpec`, `SystemRoot`, ...). Symptoms: NuGet restore fails with `Value cannot be null. (Parameter 'path1')`, `ProcessStartInfo.EnvironmentVariables` returns null in smoke scripts, and `VisualStudioPublicAssemblies` auto-detection fails. Plain `dotnet build/restore/test` works when the variables are prefixed via `env "VAR=..."`; PowerShell smoke scripts should be launched through `.tmp\Invoke-WithFullEnvironment.ps1`, which rebuilds a complete process environment from the registry before invoking the target script.

## Historical Basler Technical Task Closure

Status: Complete
Scope: First direct Basler pylon .NET 2D adapter through source/package implementation; exact pylon `8.1.0.16743` and `26.07.2.18500` installed assembly, 11-format emulator, and actual registered debugger visualizer qualification; long `D:` TEMP handoff repair; and reusable documentation.
Acceptance criteria: deterministic Basler and aggregate tests -> pass; Release solution build -> pass with 18 existing `VSTHRD010` warnings and 0 errors; full assembly-qualified interface/concrete provider registration -> pass; pylon 8.1 and 26.07 installed assembly contracts -> pass; emulator supported formats -> 11/11 on each; installed VSIX concrete `GrabResult` direct open -> Mono12 128 x 96 on each; restored 26.07 regression -> pass; long TEMP publish/claim/open -> pass; Auto Inspect user preference -> restored enabled.
Verification: self-tests under intentionally long `D:` TEMP, full Release solution build, SDK contract script, Environment contract script, release communication script, pylon runtime harness, generated extension registration inspection, actual VS2022 baseline interaction plus VS2026 pylon 8.1/restored-26.07 interactions, package-log inspection, and fresh version-swap screenshots on active leftmost `\\.\DISPLAY2` bounds `-1920,365,1920,1080`.
Evidence: [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md), [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md), installed candidate VSIX SHA-256 `608A9327...EFAD`, and `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md`.
Boundary / next dependency: This completion covers the registered Basler emulator path on two exact pylon suites only. Untested pylon releases, physical cameras/drivers, packed `Mono10p`/`Mono12p` emulator coverage, generic Automatic Inspector integration, and other vendor SDKs remain separate. At the time of this historical closure Marketplace served `1.0.52`; the experiment was removed before public `1.0.53` and never became a support claim.

## Current Vendor SDK Source Remediation Closure

Status: Complete
Scope: Remove the uncleared Basler direct provider/ObjectSource/registration/tests and proprietary multi-SDK audit path; keep vendor-neutral buffer inspection and safety tests; apply the same default license gate to camera and frame-grabber/board SDKs.
Acceptance criteria: direct Basler source and dedicated test path absent -> pass; proprietary SDK audit script absent -> pass; active source/test/sample/script vendor coupling -> 0 matches; vendor-neutral padding/stride/offset/length/PFNC safety coverage retained -> self-tests pass; generated VSIX proprietary registration/entry -> 0 matches; release and maintainer documentation aligned -> pass.
Verification: `dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows` -> pass; `dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore` -> 0 errors and 18 pre-existing `VSTHRD010` warnings; built VSIX inspection -> 94 entries and no proprietary vendor registration; `Test-ReleaseCommunication.ps1` -> pass; changed Markdown relative-link validation -> pass for 16 files; smoke-script PowerShell parse -> pass; `git diff --check` -> pass.
Evidence: current working-tree diff; [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md); [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md); built VSIX at `.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Release\net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`; test TEMP/TMP root `D:\OpenVisionLab-TestData\RawBufferVisualizer\vendor-sdk-removal`.
Boundary / next dependency: This proves source/build/package removal, not an installed-VSIX runtime matrix. The remediation supersedes pre-remediation commit `c35074b`; later exact `1.0.53` installed qualification and Marketplace readback prove the remediated package became public.

## Connect Your Buffer Installed Validation Closure

Status: Complete
Scope: Current 1.0.53.0 development VSIX installation and Connect Your Buffer save/restart/restore testing on VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12023.21`.
Acceptance criteria: package-to-install equality -> pass; initial mapping preview/save/reopen -> pass on both IDEs; fresh-process persisted auto-open and all nine restored selections -> pass on both IDEs; neutral 630-character RawBufferView template copy and no proprietary SDK text -> pass; Copy/Use Suggested Roles/Cancel keep mapping unchanged -> pass; normal Compact layout exposes a visible Edit Mapping action and reopens an already mapped row -> pass on both IDEs.
Verification: `dotnet build .\RawBufferVisualizer.sln --configuration Release` -> zero errors and 18 pre-existing warnings; self-tests -> pass; current-source UI smoke -> pass; five package-owned SHA-256 comparisons per IDE -> pass; installed `SmartTypeMapper` and `SmartTypeMapperPersisted` scenarios on both IDEs -> pass without a Wide-layout transition; mapping backup/restore guard -> pass.
Evidence: development VSIX size `1,914,617` bytes, SHA-256 `ADDC416CDA4F5DB68410BFA352628217D2B1D737BF3449B20D48A47DA751B6C1`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-compact\current-source`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-compact\installed-final`; detailed record in [smart-type-mapper-design.md](smart-type-mapper-design.md).
Boundary / next dependency: Synthetic `UnmappedCompanyFrame` evidence does not prove proprietary SDKs or physical camera/board hardware. Candidate freezing, product-source commit/push, publication, and readback were completed later for exact public `1.0.53`.

## 2.0.0 Release Source And Candidate Closure

Status: Complete

Scope: Vendor-neutral 2D contract and fixtures, checked fail-closed descriptor/enum validation, live process-memory lifetime disposal, Continue/Run Mode document invalidation, delayed-handoff Break-generation admission, Connect Doctor, version/release communication, exact current VSIX identity, current build/install equality, ten-version compatibility, and installed regressions on VS2022 and VS2026.

Acceptance criteria: source/package version `2.0.0.0` -> pass; checked descriptor arithmetic and undefined-enum rejection -> pass; disposed live source cannot read again -> pass; delayed pre-Continue handoff cannot open in Run or a later Break -> pass; active candidate size/hash recorded -> pass; seven product assemblies equal candidate/current-build/both installations -> pass; automatic smart-type scenario detects 8, opens 6, exposes 1 mapping and 1 isolated failure -> pass in both IDEs; Continue invalidates 5 live rows and retains copied `companyArrayFrame` -> pass in both IDEs; Environment second-click close regression -> pass on VS2022; Release build/self-tests/ten-version legacy matrix/package guards -> pass.

Verification: see [release-qualification-2.0.0.md](release-qualification-2.0.0.md) for exact commands, host builds, paths, install logs, package audits, equality reports, runtime logs, and installed UI screenshots.

Evidence: exact VSIX and hash above; evidence root `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806`; canonical verdict `consolidated-local-release-verdict.json`; candidate source commit `0d3ac2011c1bfd5104645e1d16cdf72f9eae9b26`; successful CI run `31059894280`.

Boundary / next dependency: Public Marketplace remains `1.0.53.0`. Candidate source commit `0d3ac20`, CI, clean installation, and exact local public-baseline `1.0.53.0 -> 2.0.0.0` in-place update passed. Publication/readback and a real Marketplace-served `1.0.53 -> 2.0.0` update were not performed. Preserved older candidates are non-publishable. Direct proprietary SDK work remains blocked below.

## Connect Doctor Feature Closure

Status: Complete

Scope: Existing Connect Your Buffer dialog gains a repeated-click diagnosis toggle, bounded ranked Buffer Doctor candidates, current-geometry format retention, accessible/themed rows, draft-only selection and preview, exact-representability Save gate, reset/cancel/unavailable behavior, exact VSIX packaging, and installed VS2022/VS2026 verification.

Acceptance criteria: result toggle opens/closes -> pass; selected candidate changes draft/preview without persistence -> pass; unrepresentable candidate Save is blocked -> pass; Use Suggested Roles clears candidate state without saving -> pass; missing paused source fails visibly -> pass; packed Mono12 stays selectable -> pass; save/reopen -> pass on both IDEs; package assemblies equal both installs -> seven of seven pass.

Verification: aggregate self-tests; Release solution build; current-source Smart Type Mapper, Buffer Doctor, and 540/900/1160 layout smokes; exact installed SmartTypeMapper scenario on VS2022 `17.14.37516.0` and VS2026 `18.8.12023.21`; one Open/Scan command each; zero final errors and zero package protocol errors.

Evidence: exact VSIX and hash above; consolidated verdict under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806`; detailed contract in [smart-type-mapper-design.md](smart-type-mapper-design.md) and [release-qualification-2.0.0.md](release-qualification-2.0.0.md).

Boundary / next dependency: Connect Doctor is included in the completed release closure. Source push, CI, and pre-publish clean/update gate are complete. The exact package remains blocked from upload until the owner explicitly authorizes publication.

## 2.0.0 Pre-Publish Clean/Update Closure

Status: Complete

Scope: Exact candidate clean install and exact public `1.0.53.0 -> 2.0.0.0` in-place update on serviced VS2022 Community `17.14.37516.0`, plus portfolio capture selection.

Acceptance criteria: clean install -> pass; public baseline package/install equality -> 7/7; update without uninstall, repair, or `/ResetSkipPkgs` -> pass; candidate/install equality -> 7/7; Automatic Inspector -> pass; Automatic Collections -> pass; Multi-Library Hybrid -> 9 documents/0 errors after direct user-equivalent Visualizer selection; menu counts -> 1/1; package-protocol errors -> 0; Fit/1:1/wheel/pan/Manual resize -> pass; portfolio images -> six reviewed captures.

Verification: `Test-VisualStudioMarketplaceUpdate.ps1`; installed `AutomaticVisionInspector`, `AutomaticCollections`, and `MultiLibraryHybrid` scenarios; `SmokeDockedLayoutWidths.ps1` at 540/900/1160; package/install SHA-256 comparison; visual review of all selected images.

Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\portfolio-captures-20260806\selected`.

Boundary / next dependency: This is local pre-publish evidence, not Marketplace publication or a Marketplace-served update. The exact candidate now waits for explicit owner publication approval. Two automated Bitmap Visualizer clicks timed out before one manual-assisted pass; evidence identifies this as a harness-control selection issue rather than a product handoff failure.

## Industrial Photograph Debug Validation Closure

Status: Complete

Scope: Add an opt-in real-photograph debuggee route using one reviewed CC0 PCB image; exercise registered Bitmap/OpenCvSharp/Emgu CV paths, Automatic Inspector, pixel inspection, and Buffer Doctor stride recovery; track three reviewed screenshots and use them in the English/Korean 2.0 Marketplace Overview while retaining the existing workflow GIF.

Acceptance criteria: reusable industrial-image debug route -> pass; source/license/hash recorded -> pass; Debug build and no-Break conversion smoke -> pass; aggregate self-tests -> pass; Automatic Inspector opens four supported live representations -> 4/4; registered Bitmap visualizer opens 1280 x 960 `BGR24` -> pass; incorrect 2448 x 2048 `Mono8` stride is visually broken -> pass; first Buffer Doctor row is `Mono8` stride 2560 and restores the PCB scene -> pass; three current screenshots visually reviewed and tracked -> pass; English/Korean Overview references all three screenshots and retains the workflow GIF -> pass.

Verification: Debug debuggee build -> 0 warnings and 0 errors; `--industrial-image-debug <CC0 PCB> --no-break` -> exit 0; Release `RawBufferVisualizer.Tests` -> pass; actual Visual Studio 2022 Community `17.14.37516.0` interaction on leftmost `\\.\DISPLAY2` -> Automatic Inspector 4 opened/0 failed and Buffer Doctor recovery pass.

Evidence: [industrial-image-testing.md](industrial-image-testing.md); tracked screenshots under `docs/images/industrial-pcb-*.png`; [marketplace-overview-2.0.0.md](marketplace-overview-2.0.0.md); [marketplace-overview-2.0.0.ko.md](marketplace-overview-2.0.0.ko.md); source SHA-256 `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\portfolio-captures-industrial-20260806`; detailed record `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260806\industrial-image-validation-20260806.md`.

Boundary / next dependency: This proves real CC0 photographic content through supported in-memory representations. It does not prove a physical camera, proprietary vendor SDK, transport, acquisition timing, exposure control, or sensor-native Bayer/packed data. The original source photograph remains outside Git; only the three reviewed Visual Studio captures are tracked. Public Marketplace remains `1.0.53.0` until the owner uploads 2.0.

## Superseded 2.0.2 Current-UI Marketplace Media Refresh Closure

Historical record only. The later color-Doctor and code-DataTip closure below replaces its public-media files, hashes, asset count, and copy.

Status: Complete

Scope: Recapture the current installed 2.0.2 UI with the licensed industrial PCB source, add a real Break Mode `industrialBitmap` hover/menu/visualizer-open sequence, replace the three tracked industrial PNGs and complete workflow GIF, add the short breakpoint GIF, and update the English Overview plus a locally rendered Korean owner-review copy.

Acceptance criteria: actual Locals row and cursor visible -> pass; Raw Buffer Visualizer popup entry visible and selected -> pass; registered Bitmap opens as 1280 x 960 `BGR24` -> pass; Automatic Inspector opens four industrial color representations -> pass; live B/G/R/raw-byte evidence visible -> pass; Buffer Doctor first candidate `Mono8 2448 x 2048 / stride 2560` and grayscale recovery -> pass; no procedural stripe/gradient fixture in public GIFs -> pass; main GIF 5-8 seconds and no state over 1.5 seconds -> pass; Korean review uses current local assets -> pass; exact five-asset Marketplace dry run -> pass.

Verification: installed `IndustrialMarketplace` scenario on Visual Studio 2022 Community `17.14.37516.0` -> pass; source and encoded-frame contact-sheet review -> pass; breakpoint GIF 960 x 531, four frames, 3.9 seconds; main GIF 960 x 531, ten frames, 7.45 seconds; media hash/dimension/link/order validation -> pass; `Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.2` -> pass; `Publish-VisualStudioMarketplace.ps1 ... -DryRun` -> pass with five exact assets; PowerShell parse and `git diff --check` -> pass.

Evidence: [industrial-image-testing.md](industrial-image-testing.md); [2.0.2 English Overview](marketplace-overview-2.0.2.md); [Korean owner-review copy](marketplace-overview-2.0.2.ko.md); `D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-current-ui-breakpoint-run7\IndustrialMarketplace-installed-vsix.json`; dry-run manifest `D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-readiness-2.0.2\20260809\vs-publish-current-breakpoint-assets.json`; frozen VSIX SHA-256 `46A84EFC5AB685139B3C16B0DA0C35B6277E359D05E3E6198CA9925D737E5D57`.

Boundary / next dependency: This prepares and verifies local media/copy only. Nothing was committed, pushed, published, or read back from Marketplace. The English Overview intentionally points to GitHub `main`, so the reviewed media commit must reach `main` before the owner uploads that Overview. Owner approval remains required.

## 2.0.2 Color Doctor And Code DataTip Media Correction Closure

Status: Complete

Scope: Replace the public Doctor demonstration with real color `BGR24` stride recovery, retain the Locals visualizer GIF, add a code-editor DataTip magnifying-glass GIF using a second CC0 industrial photograph, regenerate the complete workflow, and synchronize the English Marketplace Overview with a locally rendered Korean owner-review copy.

Acceptance criteria: BGR24 fixture remains color before/after and top stride 7424 restores alignment -> pass; actual code object/DataTip/magnifying-glass/click/docked viewer visible -> pass; different industrial image source/license/hash recorded -> pass; Locals path retained -> pass; all GIFs use current installed UI and hold no frame longer than 1.25 seconds -> pass; English/Korean copy uses the same six media files -> pass.

Verification: `RawBufferVisualizer.Tests` Release run -> pass; debuggee Debug build -> 0 warnings/0 errors; installed `IndustrialMarketplace` and `IndustrialDataTip` scenarios on VS2022 `17.14.37516.0` -> pass with dynamically verified `\\.\DISPLAY2` placement and zero package-protocol errors; all source and encoded-frame contact sheets visually reviewed; media dimensions, frame durations, lengths, and SHA-256 verified; Marketplace dry run -> exactly six referenced assets and no publish.

Evidence: [industrial-image-testing.md](industrial-image-testing.md); [2.0.2 English Overview](marketplace-overview-2.0.2.md); [Korean owner-review copy](marketplace-overview-2.0.2.ko.md); `D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip`; dry-run manifest `D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-readiness-2.0.2\20260809-color-datatip\vs-publish-six-assets.json`.

Boundary / next dependency: The owner approved this final media/copy set for commit/push, and it is included in the 2.0.2 source/media commit and CI gate. Marketplace upload and public readback remain separate owner-controlled steps.

## Producer Pointer Read Safety Closure

Status: Complete

Scope: Replace direct producer-side unmanaged dereferences in all registered raw/ImagePtr/OpenCvSharp/Emgu/Bitmap chunk and sampled-preview paths with one fail-closed current-process memory reader; preserve Bitmap row order for negative stride; document same-address reuse and concurrent-mutation limits. No UI, version, package, release, commit, push, tag, or publication change is included.

Acceptance criteria: all six external pointer-read sites use the shared reader -> pass; complete reads preserve valid bytes -> pass; same address is reread instead of treated as object identity -> pass; readable page-spanning Float32 -> pass; freed address -> controlled failure; protected partial range -> controlled failure; negative-stride Bitmap chunk and preview -> top-to-bottom pass.

Verification: `dotnet build .\src\RawBufferVisualizer.VisualStudio.ObjectSource\RawBufferVisualizer.VisualStudio.ObjectSource.csproj --configuration Release --no-restore` -> net472/netstandard2.0/net8.0, 0 warnings/errors; `dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows --no-restore` -> passed; `dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore` -> 0 errors and 18 pre-existing `VSTHRD010` warnings; 8192 x 8192 unmanaged Mono8 sampled-preview probe -> 512 x 512 in 26 ms under the 5000 ms gate.

Evidence: `src\RawBufferVisualizer.VisualStudio.ObjectSource\CurrentProcessMemoryReader.cs`; registered ObjectSource transfer files; `tests\RawBufferVisualizer.Tests\Program.cs`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\producer-pointer-safety\sampled-preview-page-cache.json`; [vendor-neutral-buffer-compatibility-matrix.md](vendor-neutral-buffer-compatibility-matrix.md).

Boundary / next dependency: This is source/build/self-test evidence, not installed-Visual-Studio runtime evidence. The unchanged VS 17.9 targeting contract still builds, but exact runtime verification requires an installed VS 17.9 host. Readable memory being changed concurrently can yield a mixed-time image and address reuse cannot prove allocation identity.

## Future Direct Proprietary SDK Gate

Status: Blocked
Scope: Any new direct Basler pylon, Allied Vision Vimba X, IDS peak, Teledyne FLIR Spinnaker, or other proprietary camera/frame-grabber/transport-board/imaging-board adapter.
Acceptance criteria: current official terms for the exact proposal -> required; applicable written vendor authorization -> missing; qualified legal review of material conditions -> missing; explicit owner implementation approval -> missing.
Verification: official terms and decision checklist are recorded in [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md).
Evidence: [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) and [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md).
Boundary / next dependency: Do not download, test, implement, publish, or advertise a direct integration until those prerequisites exist. Vendor-neutral `RawBufferView`/`RawBufferSnapshot` use remains supported.

## 2.0.8 Automatic Inspector Final Local Qualification

Status: Blocked

Scope: Final local 2.0.8 candidate with corrected Visual Studio 2022 signed-Int32 debugger payload, bounded 128-candidate discovery, 8-image/2-second initial batching, non-modal load/Stop feedback, stable-row repeated-Break refresh, cache-safe mapping decisions, and narrow image-card metadata layout.

Acceptance criteria: routed Release build/package -> pass with zero errors and 18 pre-existing `VSTHRD010` warnings; exact candidate/install critical equality -> 17/17 on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026; aggregate self-tests -> pass; exact installed stable-VS2026 Automatic Inspector, 50-item Automatic Collections, Multi-Library Hybrid, direct/automatic/padded `CV_32SC1`, ImagePtr cold start, ConcurrentDictionary, Release Announcement, and Environment Check -> pass with zero package-protocol errors; exact installed VS2022 industrial Bitmap/Automatic Inspector/Doctor, code DataTip, and direct/automatic/padded `CV_32SC1` -> pass with zero package-protocol errors; exact VS2022 17.9 seven-scenario matrix -> 7/7 pass with zero package-protocol errors; refreshed exact-installed media -> pass; 320/348/540/900/1160 px layout -> pass and visually reviewed at 96 DPI; 125%-200% DPI -> unavailable on this workstation.

Verification: final `Publish-VisualStudioExtension.ps1` run; `RawBufferVisualizer.Tests` on `net8.0-windows`; installed `SmokeInstalledVsixNewFeatures.ps1` scenario matrices on exact VS2022 `17.9.34902.65`, serviced VS2022 `17.14.37516.0`, and VS2026 `18.9.12128.139`; `SmokeDockedLayoutWidths.ps1`; archive/install SHA-256 comparison on all three hosts; exact 17.9 ActivityLog and screenshot review; decoded-GIF and screenshot review. Cache regression retained `[Map] incompleteAutomaticFrame` before and after repeated scans. The 50-object run produced 48 valid rows, 2 isolated intentional failures, no duplicates, one rejected overlapping command, successful Clear/reopen, 50-to-42 next-Break reconciliation, pointer replacement in place, and a recorded 44.17 ms scan.

Evidence: exact VSIX `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\candidate-final\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`, 2,521,139 bytes, SHA-256 `612517FA64805853A072D19773709B6DD9D09AE9B095342028EB261086BBD9A2`; exact VS2022 17.9 results `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final`; VS2026 results `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\installed-vs2026-final`; VS2022 17.14 and media results `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-final`; three-host 17/17 equality roots `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\restart-verification-20260909-235208`; layout evidence `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\layout-widths-final`; detailed record [release-qualification-2.0.8.md](release-qualification-2.0.8.md); behavior record [automatic-vision-inspector.md](automatic-vision-inspector.md).

Boundary / next dependency: Local implementation and exact-host evidence are complete, including the corrected direct `CV_32SC1` path on Visual Studio 2022 `17.9.34902.65`. Marketplace approval remains blocked until the UI is checked at 125%, 150%, 175%, and 200% DPI. The source/media history is pushed to `origin/main`, and integration-commit CI passed; no Marketplace upload, tag, release, or deployment was performed.

## 2.0.8 Marketplace Media Refresh

Status: Complete

Scope: English/Korean Overview media now uses fresh screenshots and short GIFs captured from the exact installed 2.0.8 candidate on VS2022 `17.14.37516.0`, covering code DataTip, Locals, Automatic Inspector, Buffer Doctor color recovery, and signed Int32.

Acceptance criteria: current responsive UI visible -> pass; actual DataTip magnifier interaction and exact `dataTipIndustrialMat` card name visible -> pass; Locals entry retained -> pass; industrial color diagnosis/recovery visible -> pass; Int32 one-channel result visible -> pass; final GIFs free of the rejected white-frame palette defect -> pass; English/Korean asset paths aligned -> pass.

Verification: installed `IndustrialMarketplace`, `IndustrialDataTip`, and `Int32Industrial-retry2` JSON passed; GIF dimensions/frame counts/rates/durations/sizes and media hashes measured; decoded frames and full-context screenshots visually reviewed.

Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-final`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-object-name`; tracked files under `docs\images` and `docs\video`; exact metadata and hashes in [industrial-image-testing.md](industrial-image-testing.md).

Boundary / next dependency: Media preparation, feature-branch publication, main integration, main push, and integration-commit CI are complete. All seven English/Korean Overview URLs are pinned to immutable commit `720ceec`; each returned HTTP 200 with the expected content type and exact local byte length. No Marketplace upload, tag, release, or deployment was performed.

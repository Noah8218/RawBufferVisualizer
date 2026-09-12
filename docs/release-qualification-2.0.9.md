# Raw Buffer Visualizer 2.0.9 Release Qualification

Opened and last updated: 2026-09-12 KST

## Exact Local Candidate

Only the following package is the qualified 2.0.9 candidate:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\candidate-final-r2\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,520,780` bytes |
| SHA-256 | `EB0F862EDA94FBDCC938A5AA26BF812C8A1BB1B25567FE8D7D1992A90799DDC0` |
| Manifest version | `2.0.9.0` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72`, of which `71` are installer-deployed files |
| Source baseline | The 2.0.9 release-source commit containing this record; parent baseline `368299b1a774e6e3cceeb10efe54b87f2283909a` |

Rebuilding produces different bytes and invalidates this identity. The frozen package has not been tagged, uploaded, or published. Verify the matching source commit, remote state, and CI result independently.

## Field Incident And Correction

The public 2.0.8 report came from Visual Studio 2022 `17.9.34902.65`. A `6768 x 3225 Mono8` image contains `21,826,800` bytes. Version 2.0.8 attempted to retrieve that pointer-backed payload through six debugger-RPC snapshot requests of up to 4 MiB and failed inside `WriteRawChunksCoreAsync` with `StreamJsonRpc.RemoteInvocationException`.

Version 2.0.9 makes five focused corrections:

- Pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads at or above 8 MiB use checked live process-memory reads. Smaller payloads keep the durable snapshot path and 4 MiB chunks.
- Inferred pointer spans use `stride * (height - 1) + minimumRowBytes`. Explicit caller-supplied lengths remain authoritative. This avoids reading padding that does not exist after the final row of an ROI or submatrix.
- Visible debugger-RPC failures use stable technical fields while the full localized exception remains in the local support report.
- Reopening the same manual expression with the same normalized failure type and message refreshes its existing error row, timestamp, error ID, and diagnostics instead of appending duplicates. Different expressions or causes remain separate rows.
- Registered visualizer handoffs no longer search for and close a same-caption Visual Studio frame. The permanent VSSDK Tool Window remains the owner of the docked UI; disposing `VisualizerTarget` releases only the temporary out-of-process handoff host.

Live reads remain valid only while the debuggee is paused and the range is completely readable. Continue, process exit, inaccessible pages, partial reads, invalid descriptors, and arithmetic overflow fail closed.

## Error-Wording Audit

Production source under `src` contains no Korean UI literal. The reported `평가 시간 초과` text was Visual Studio's localized `RemoteInvocationException.Message`, not a product-authored message.

All exception exits in the registered out-of-process single-object and collection launch paths route remote failures through `CreateTechnicalFailureMessage`. A visible metadata failure now reports:

```text
Debugger RPC request failed: operation=metadata; source=<type>; exception=<exception type>; hresult=<HRESULT>; image payload=not received.
```

A chunk failure reports `operation=snapshot-chunk`, chunk index/count, byte offset, requested bytes, total bytes, exception type, and HRESULT. The original localized exception and complete stack are retained in `Error details` in **Copy Report**, not used as the visible row message. Image-format, pointer, stride, and descriptor validation messages remain specific to their rejected value. Local PNG, snapshot, mapping, clipboard, and file operations retain their operation name plus the operating-system detail because that detail is actionable and is not a debugger-RPC status.

The exact candidate's `RawBufferVisualizer.VisualStudio.Extensibility.dll` contains both technical RPC formats and does not contain `평가 시간 초과`. `Test-ReleaseCommunication.ps1` rejects removal of the technical fields, loss of `ex.ToString()` report detail, or addition of that localized literal.

## Build And Automated Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Routed Release package | Passed with 0 errors and 18 pre-existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\candidate-final-r2` |
| Aggregate self-tests | Passed, including inaccessible-page final-row span tests for RawBufferView, ImagePtr, legacy OpenCvSharp Mat, and Emgu CV Mat | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\final-selftest-build-r2` |
| Legacy image-library matrix | Passed 5 OpenCvSharp and 5 Emgu CV package versions | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\legacy-matrix-build` |
| Release communication | Passed for `2.0.9`, including debugger-RPC wording guards | `scripts\Test-ReleaseCommunication.ps1` |
| Registration audit | Four installed profiles report `2.0.9.0`; no stale codebase or per-machine conflict | `scripts\Test-VisualStudioMarketplaceUpdate.ps1` |
| Repeated-error layout smoke | Passed at 540, 900, and 1160 px: a second identical failure kept the document count at 2 and replaced the error ID | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\ui-host-fix\after\layout-widths.json` |
| Exact package/install equality | Passed: every one of 71 installer-deployed files matched by SHA-256 on all three installed hosts | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\candidate-install-equality-r2.json` |
| Marketplace preparation | Exact VSIX metadata, English Overview, and generated publish manifest passed dry validation; no Marketplace mutation occurred | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\marketplace\vs-publish-r2.json` |

## Public 2.0.8 Readback And Update Boundary

The official Visual Studio Gallery package endpoint was read back on 2026-09-12 without changing the Marketplace item:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\public-2.0.8-readback-20260912\RawBufferVisualizer-public-2.0.8.0.vsix
```

The package is `2,521,139` bytes, has SHA-256 `612517FA64805853A072D19773709B6DD9D09AE9B095342028EB261086BBD9A2`, manifest version `2.0.8.0`, and the expected extension ID. It is byte-identical to the preserved final 2.0.8 candidate, so the exact public baseline is available for a later update test.

The prerequisite attempt to stage that public baseline on VS2022 Community 17.9 did not replace the already-installed 2.0.9 package. Both the default and explicitly targeted 17.9 VSIX installer returned exit code `-2146233079`; the post-attempt registration audit still reported valid `2.0.9.0` payloads on all four profiles. Therefore the actual `2.0.8 -> 2.0.9` in-place step was not executed. No registration repair, `/ResetSkipPkgs`, manual deletion, or forced recovery was used.

## Installed Runtime Matrix

The same exact VSIX was installed on all three hosts. Each successful runtime run used the smaller left monitor, `\\.\DISPLAY2`, with bounds `-1920,365`, `1920 x 1080`; the verified Visual Studio window intersected that monitor.

| Host | Reported-size result |
| --- | --- |
| Visual Studio Community 2022 `17.9.34902.65` | OpenCvSharp `12443 ms`, Emgu CV `8846 ms`, ImagePtr `9684 ms`; 3 documents, 0 errors; expected pixels `37`, `173`, `37`; all sources `live`, then `UNAVAILABLE` after process exit. Evidence: `installed-vs2022-17.9-reported-size-r2-retry` |
| Visual Studio Community 2022 `17.14.37516.0` | OpenCvSharp `8421 ms`, Emgu CV `7860 ms`, ImagePtr `8011 ms`; 3 documents, 0 errors; expected pixels and pointer provenance passed; all sources became `UNAVAILABLE` after exit. Evidence: `installed-vs2022-17.14-reported-size-r2` |
| Stable Visual Studio Community 2026 `18.9.12128.139` | Not executed on the final `r2` bytes. Repeated attempts remained at the empty Visual Studio start window and did not register DTE before the debuggee or extension ran, so they are environment-start failures rather than product-runtime results. The exact `r2` package did pass the registered-open dock-retention scenario below on this host. A later attempt after the instance updated to `18.10.12201.205` stopped at the same pre-debuggee boundary and is recorded under `installed-vs2026-18.10-reported-size-r2`; it is also not a product-runtime result. |

The earlier pre-UI-fix 2.0.9 candidate passed the same reported-size three-source workflow on stable Visual Studio 2026, but that result is not presented as execution proof for the final `r2` bytes. The final candidate's 71 deployed files do match its VSIX on the 18.9 profile.

### Pinned dock and repeated-open regression

The dedicated installed test disables Automatic Inspector, pins the real pane containing `ImageList`, then opens registered `System.Drawing.Bitmap` and `RawBufferVisualizer.Sdk.RawBufferView` variables from Locals. It requires two successful documents, zero error rows, the same UI Automation runtime ID before and after each open, and no visible Visual Studio invalid-state helper page.

| Host | Result | Evidence |
| --- | --- | --- |
| VS2022 `17.9.34902.65` | Passed; 2 documents, 0 errors, runtime ID `7.30716.66906707` retained | `installed-vs2022-17.9-dock-retention-r2-pass\DockRetention-installed-vsix.json` |
| VS2022 `17.14.37516.0` | Passed; 2 documents, 0 errors, one pane runtime ID retained | `installed-vs2022-17.14-dock-retention-r2\DockRetention-installed-vsix.json` |
| VS2026 `18.9.12128.139` | Passed; 2 documents, 0 errors, one pane runtime ID retained | `installed-vs2026-18.9-dock-retention-r2\DockRetention-installed-vsix.json` |

Additional pre-UI-fix 2.0.9 transfer-boundary evidence remains applicable to the unchanged threshold and transfer implementation, but it is not execution proof for the final `r2` bytes:

| Boundary | Result | Evidence |
| --- | --- | --- |
| Exactly 4 MiB (`4096 x 1024 Mono8`) | OpenCvSharp, Emgu CV, and ImagePtr opened as `CAPTURED` snapshots and remained viewable after process exit | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\installed-vs2022-17.9-snapshot-4mib-final` |
| Exactly 8 MiB (`4096 x 2048 Mono8`) | All three opened as checked `live` sources and became `UNAVAILABLE` after exit; no complete-image temporary file was created | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\installed-vs2022-17.9-live-8mib-final` |
| Release Announcement | Title `New in Raw Buffer Visualizer 2.0.9`, Dismiss persistence, What's New open/second-click close, 0 image-list changes, 0 package-protocol errors | `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\installed-vs2022-17.9-release-announcement-r2` |

The package/install equality record compares every installer-deployed entry, not a selected subset: `71/71` files matched by SHA-256 on all three hosts. Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\candidate-install-equality-r2.json`.

## Release Copy And Media

- English Marketplace Overview: [marketplace-overview-2.0.9.md](marketplace-overview-2.0.9.md)
- Korean review copy: [marketplace-overview-2.0.9.ko.md](marketplace-overview-2.0.9.ko.md)
- Marketplace release notes: [marketplace-release-notes-2.0.9.md](marketplace-release-notes-2.0.9.md)

Version 2.0.9 does not change the viewer layout or demonstrated controls, so both Overviews deliberately reuse the exact-installed 2.0.8 visual set pinned to commit `720ceec0ec72b603a08987f0e0436b5fa0adbbb9`. The images and GIFs remain current for the visible workflow; no media is presented as evidence of the new transfer internals.

## Closure

```text
Status: Incomplete
Scope: Exact local 2.0.9 candidate for the medium native-image RPC correction, inferred ROI span correction, technical debugger-error wording, repeated-error coalescing, and pinned Tool Window retention.
Acceptance criteria: source/build/aggregate/legacy checks -> pass; reported 6768 x 3225 OpenCvSharp/Emgu/ImagePtr workflow -> pass on final bytes for VS2022 17.9 and VS2022 17.14, but unexecuted on final bytes for stable VS2026 18.9; repeated identical error row -> pass at three widths; sequential registered opens retain one pinned pane -> pass on VS2022 17.9, VS2022 17.14, and stable VS2026 18.9; package/install equality -> 71/71 on all three hosts; release announcement -> pass.
Verification: Commands and installed-runtime evidence listed above were checked individually; the VS2026 18.9 attempts and post-update 18.10 attempt stopped before debuggee launch and are not counted as passes; exact candidate length and SHA-256 were re-read after verification; official 2.0.8 readback matched the preserved candidate byte-for-byte, but prerequisite baseline staging failed and did not exercise the in-place update.
Evidence: This document and D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9, including `public-2.0.8-readback-20260912`.
Boundary / next dependency: The final stable-VS2026 reported-size run and an in-place update from a profile already running the exact public 2.0.8 package remain unexecuted. On 2026-09-12 the owner accepted those two evidence gaps for the source commit/push decision; this is risk acceptance, not runtime-test evidence. CI must still pass for the pushed release-source commit before the owner-controlled Marketplace upload. No tag, GitHub Release, Marketplace upload, or deployment was performed by this qualification work.
```

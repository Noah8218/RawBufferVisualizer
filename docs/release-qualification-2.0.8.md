# Raw Buffer Visualizer 2.0.8 Release Qualification

Opened: 2026-09-04 KST
Last updated: 2026-09-10 KST

## 2026-09-09 Final Local Candidate

This record supersedes every earlier 2.0.8 package path and hash below. The exact current local candidate is:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\candidate-final\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,521,139` bytes |
| SHA-256 | `612517FA64805853A072D19773709B6DD9D09AE9B095342028EB261086BBD9A2` |
| Manifest version | `2.0.8.0` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72` |

The routed Release build completed with zero errors and 18 pre-existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs`. The publish gate accepted the package only after checking the debugger-side Core, SDK, ObjectSource, and dependency payload against the fresh routed output. After reinstalling this exact VSIX, 17 critical archive/installed files matched by SHA-256 on each tested host: exact VS2022 Community `17.9.34902.65`, serviced VS2022 Community `17.14.37516.0`, and stable VS2026 Community `18.9.12128.139`.

Installed final-candidate results on VS2026:

| Scenario | Result |
| --- | --- |
| Automatic Vision Inspector | Passed; 6 automatic rows, 1 mapping row, 1 isolated failure, duplicate-free repeated scan, and `[Map] incompleteAutomaticFrame` remained mapping-required after cached analysis. |
| Automatic Collections | Passed; 50 detected, 48 successful, 2 intentionally invalid rows isolated, overlapping command rejected, clear/reopen passed, 50-to-42 next-Break reconciliation passed, and the replaced pointer refreshed in place. Recorded full-scan time: `44.17 ms`. |
| Multi-Library Hybrid | Passed; 9 documents, 0 errors, bounded discovery converged in 2 scans. |
| Signed Int32 industrial matrix | Passed direct OpenCvSharp `CV_32SC1`, automatic matrix, padded stride `5184`, exact `Value 17604`, and bytes `196 68 0 0`. |
| ImagePtr cold start | Passed; `Cressem.ImageModel.ImagePtr`, `640 x 484`, `BGR24`, 1 document, 0 errors. |
| ConcurrentDictionary | Passed; key-preserving `concurrentImageDictionary[concurrent-snapshot]`, 1 document, 0 errors. |
| Release Announcement | Passed; 2.0.8 copy, persisted dismissal, What's New reopen and second-click close, no inspection side effect. |
| Environment Check | Passed; 2.0.8 loaded/registered, open and second-click close. |
| Package protocol | Zero protocol errors in every final installed scenario above. |

Installed final-candidate results on VS2022 17.14:

| Scenario | Result |
| --- | --- |
| Industrial Marketplace workflow | Passed; registered 1280 x 960 `BGR24` Bitmap, 5 Automatic Inspector results, bad-stride diagnosis `7344 -> 7424`, color recovery, RGB pixel inspection, one View/Open command, and one Scan command. |
| Code DataTip visualizer | Passed; UI Automation hovered the actual `dataTipIndustrialMat` token, clicked the magnifying-glass visualizer affordance, and opened the 1280 x 720 OpenCvSharp `BGR24` image with card name `dataTipIndustrialMat` and without using the fallback menu. |
| Signed Int32 industrial matrix | Passed direct OpenCvSharp `CV_32SC1`, 5 automatic results, padded stride `5184`, and signed pixel value `-15035` with bytes `69 197 255 255`. |
| Package protocol | Zero protocol errors in all three VS2022 final-candidate scenarios. |

## 2026-09-10 Exact Visual Studio 2022 17.9 Qualification

The unchanged final candidate was installed into Visual Studio Community 2022 `17.9.34902.65` (`17.9.7`, instance `3b79a6ac`) at `C:\Program Files\Microsoft Visual Studio\2022\Community17.9`. Visual Studio Setup reported the instance complete, launchable, and not awaiting a reboot. The installed extension root was `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_3b79a6ac\Extensions\kcqkjdar.e0x`; all 17 critical VSIX/installed files matched by SHA-256. A separate Professional 17.9 trial instance was not used for runtime qualification because its local trial license had expired.

| Scenario | Exact VS2022 17.9 result |
| --- | --- |
| Signed Int32 industrial matrix | Passed direct OpenCvSharp `CV_32SC1`, automatic discovery, padded stride `5184`, signed value `-19147`, and bytes `53 181 255 255`. |
| ImagePtr cold start | Passed; `Cressem.ImageModel.ImagePtr`, `640 x 484`, `BGR24`, 1 document, 0 errors. |
| ConcurrentDictionary | Passed; key-preserving `concurrentImageDictionary[concurrent-snapshot]`, 1 document, 0 errors. |
| Automatic Collections | Passed; 50 detected, first 8 then next 8, 48 valid and 2 intentionally invalid rows isolated, no duplicates, overlap coalesced, clear/reopen, next-Break 42, and pointer replacement refreshed in place. |
| Multi-Library Hybrid | Passed; 9 documents, 0 errors, bounded discovery converged in 2 scans. |
| Release Announcement | Passed; 2.0.8 copy, dismissal persistence, reopen/second-click close, and no inspection side effect. |
| Environment Check | Passed; exact 17.9 host and 2.0.8 extension reported loaded/registered, with second-click close. |
| Package protocol | Zero protocol errors across all seven scenarios. Eight captured ActivityLogs each recorded one package-load begin/end pair and zero Raw Buffer Visualizer error entries. |

The first Automatic Collections attempt reached the expected visible `16 refreshed / 34 deferred / 0 failed` state but the harness timed out while waiting for its JSON file. A clean retry passed the complete assertions above, so the retained first-attempt capture is classified as harness/session-file timing evidence rather than a product failure.

```text
Status: Complete
Scope: Unchanged Raw Buffer Visualizer 2.0.8 candidate installed and exercised on exact Visual Studio Community 2022 17.9.34902.65.
Acceptance criteria: exact host and version -> pass; candidate/install critical equality -> 17/17 pass; affected direct CV_32SC1 path -> pass; ImagePtr/ConcurrentDictionary/Automatic Collections/Multi-Library/release/environment regressions -> 7/7 pass; package protocol errors -> 0.
Verification: installed-VSIX runtime scenarios, SHA-256 archive/install comparison, package registration check, ActivityLog review, and fresh screenshot review.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final\exact-vs17.9-qualification-summary.json and its referenced scenario/equality records.
Boundary / next dependency: This is exact-host local qualification of the unchanged candidate. No commit, push, Marketplace upload, tag, release, or deployment was performed.
```

The final VS2022 run used dynamically selected `\\.\DISPLAY2` with bounds `-1920,365`, `1920 x 1080`; the verified Visual Studio rectangle was `-1900,385`, `1880 x 1040`. The first `Int32Industrial` launch used a stale `-NoBuild` debuggee and did not reach the requested scenario; it is retained as harness evidence only. `Int32Industrial-retry2` used the correct Debug output and passed, so the initial launch is not counted as a product failure. The first DataTip media fixture exposed one Mat under two local aliases, so the 17.9-compatible resolver correctly kept its safe type-name fallback. The final DataTip fixture uses a distinct object, asserts `openedObjectName == dataTipIndustrialMat`, and passed; the generic industrial workflow was rerun afterward and retained its five-object result.

The exact final build also passed the aggregate `net8.0-windows` self-test executable and the 320/348/540/900/1160 px layout smoke. Fresh 96-DPI captures were visually reviewed on dynamically selected `\\.\DISPLAY2`; object names and pointer provenance remain above the thumbnail, while image summary text uses the full card width below it at narrow sizes.

The English and Korean 2.0.8 Overviews now reference fresh exact-installed-2.0.8 media. The three GIFs are 960 x 532 at 4 fps: DataTip 3.0 seconds/195,057 bytes, Locals 3.25 seconds/456,412 bytes, and complete workflow 6.0 seconds/486,571 bytes. The DataTip sequence shows the exact `dataTipIndustrialMat` card name. All decoded final frames were visually reviewed; an earlier palette encode that produced white later scenes was rejected and is not tracked. The five static screenshots are 1880 x 1040. The English Overview, Marketplace release notes, embedded release notes, version sources, and changelog passed `Test-ReleaseCommunication.ps1`. `Test-VisualStudioMarketplaceUpdate.ps1` confirmed one valid 2.0.8 installation in each available Visual Studio profile and no legacy configuration key; its not-applicable CodeBase field is now blank instead of the misleading value `False`. A final Marketplace dry run produced `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-dry-run-20260910-final\vs-publish.json`; it did not publish or modify Marketplace state.

```text
Status: Blocked
Scope: Final local 2.0.8 VSIX with corrected CV_32SC1 payload, bounded incremental Automatic Inspector, stable-row refresh, cache-safe mapping decisions, responsive image cards, release copy, and available-host installed regression.
Acceptance criteria: routed Release package -> pass; fresh payload/hash gates -> pass; exact VSIX/install equality -> 17/17 on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026; aggregate self-tests -> pass; final installed Automatic Inspector/50-item collections/mixed libraries/Int32/ImagePtr/ConcurrentDictionary/release/environment scenarios -> pass on exact VS2022 17.9 and stable VS2026; final installed industrial Bitmap/Automatic Inspector/Doctor, code DataTip, and direct/automatic/padded Int32 -> pass on serviced VS2022; current 2.0.8 media refresh -> pass; 320/348/540/900/1160 layout -> pass at 96 DPI; exact VS2022 17.9 -> pass; 125%-200% DPI -> blocked by unavailable local scales.
Verification: Publish-VisualStudioExtension.ps1; RawBufferVisualizer.Tests net8.0-windows; SmokeInstalledVsixNewFeatures.ps1 final scenario matrices on exact VS2022 17.9, serviced VS2022 17.14, and stable VS2026; SmokeDockedLayoutWidths.ps1; Test-VisualStudioMarketplaceUpdate.ps1; Test-ReleaseCommunication.ps1; exact-package Marketplace dry run; SHA-256 archive/install comparisons; ActivityLog review; media hash/dimension/frame/duration checks; decoded-GIF and fresh-screenshot visual review.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\candidate-final, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\vs2022-17.9-final, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\installed-vs2026-final, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-final, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-media-vs2022-object-name, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\layout-widths-final, D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\restart-verification-20260909-235208, and D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\marketplace-dry-run-20260910-final.
Boundary / next dependency: The exact Visual Studio 2022 17.9 runtime gate is complete. Marketplace publication approval still requires the recorded 125%, 150%, 175%, and 200% DPI checks. The completed source/media batch and immutable-link correction are pushed to `origin/agent/vs2022-17.9-compat` and integrated into local `main`; no main push, CI dispatch, Marketplace upload, tag, release, or deployment was performed.
```

## 2026-09-10 Feature-Branch And Marketplace Media Handoff

```text
Status: Complete
Scope: Commit the complete 2.0.8 source/media batch, pin all seven English/Korean Marketplace media references to immutable repository blobs, and push the approved feature branch.
Acceptance criteria: source/media commit -> 720ceec; immutable-link commit -> 9ae28f3; remote feature branch -> contains both commits after push; English/Korean media sets -> 7/7 aligned; remote media readback -> 7/7 HTTP 200 with expected image content type and exact local byte length; release communication check -> pass.
Verification: git remote/branch/workflow inspection before mutation; Test-ReleaseCommunication.ps1; curl.exe readback of all seven immutable raw-media URLs; local/remote blob and byte-length comparison; local/remote feature-branch head comparison.
Evidence: commits 720ceec0ec72b603a08987f0e0436b5fa0adbbb9 and 9ae28f3eb2efa5fde922d598803ea3fb89e21118 on origin/agent/vs2022-17.9-compat; docs/marketplace-overview-2.0.8.md; docs/marketplace-overview-2.0.8.ko.md; tracked media under docs/images and docs/video.
Boundary / next dependency: This records only the approved feature-branch commit/push and immutable media readback. Main integration, CI, Marketplace upload/publication, tag creation/push, GitHub Release, and deployment were not performed.
```

## 2026-09-10 Local Main Integration Handoff

```text
Status: Complete
Scope: Fast-forward local main from the remote 2.0.2 baseline through the reviewed 2.0.8 feature-branch head, including the current 2.0.8 README and repository media.
Acceptance criteria: origin/main is an ancestor of the reviewed feature head -> pass; origin/main update into local main -> fast-forward pass; feature head into local main -> fast-forward pass with no conflict; README version -> 2.0.8; README media targets -> 7/7 present locally.
Verification: git fetch; git merge --ff-only origin/main; git merge --ff-only agent/vs2022-17.9-compat; README diff and local media-target inspection; release communication check and final clean-worktree/head review.
Evidence: local main contains feature head fec2c87227d24e9bdd4f3904bb6231f36b501b91; README.md; docs/images; docs/video; this release qualification record.
Boundary / next dependency: The approved local main integration is complete. origin/main remains e236278f642ea061e7a49e32256ad0e953544084 until a separate main-push authorization; CI, Marketplace upload/publication, tag creation/push, GitHub Release, and deployment were not performed.
```

> The sections below are preserved historical 2.0.8 evidence. Their package paths and hashes are superseded by the final local candidate above and must not be uploaded as the current candidate.

## 2026-09-09 Current-Source Development-Test Supplement

The current-source development package was rebuilt and reinstalled into VS2022 Community `17.14.37516.0` instance `419f0858` after the Automatic Inspector batching/UI change:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\automatic-inspector\install-candidate\RawBufferVisualizer-2.0.8-automatic-inspector-devtest.vsix
```

It is 2,520,237 bytes with SHA-256 `62233686012228A898362E92B7CCF273D91CE2C4A9F5BCDDD08FCBB542D1FA93` and manifest version `2.0.8.0`. The installed root is `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_419f0858\Extensions\gwnd3t0j.d4f`. The VSIX and installed manifest, VSSDK, ObjectSource, viewer, Core, and SDK files matched 6/6 by SHA-256.

The installed F5/Break workflow detected 50 image objects. Its soft two-second initial batch opened 6 and deferred 44; **Load next 8** advanced the total to 13 without replacing the first six rows; **Load all this Break** completed 46 valid OpenCvSharp Mats, 2 valid Emgu Mats, and 2 intentionally invalid OpenCvSharp elements. Three same-Break **Scan Now** operations retained all 50 `handoffId -> instanceId` mappings with no duplicate rows. At the next Break, eight removed collection elements disappeared, all 42 surviving row identities stayed stable, and item 0 refreshed both source `Ptr` and pixel address while changing from `640 x 480 Mono8` to `96 x 72 BGR24`. Completing that Break produced 40 valid OpenCvSharp and 2 valid Emgu rows with zero failures.

An isolated test-only build enlarged the same 50-object fixture to `2048 x 1536` so cancellation could be reached through real UI input. **Stop** paused at 17 refreshed / 33 deferred with existing rows still usable; **Load next 8** resumed to 24 refreshed / 26 deferred while all first-17 identities stayed stable. The sample source was restored to its exact pre-test SHA-256 after the heavy build. Fresh dark-theme UI states for initial, progress/Stop, paused, resumed, complete, next-Break replacement, and unavailable-after-session were displayed and visually reviewed on dynamically selected `\\.\DISPLAY2` (`1920 x 1080`, 96 DPI); those task captures were not added to repository media.

Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\automatic-inspector\installed-vs2022-17.14`. The main session records are `01-initial-break.json` through `06-second-break-full-42.json`; cancellation records are `heavy-stop\07-stop-paused.json` and `heavy-stop\08-resume-next.json`. The 54 new package-log lines contained no error/exception/failure/RPC pattern. Both ActivityLogs recorded successful `RawBufferVisualizerPackage` begin/end and zero RawBufferVisualizer error entries. Unrelated Visual Studio Extension Manager `PkgDefMgmt.dll` and shutdown MEF errors remain in the global logs and are not attributed to this extension. The user's pre-test Auto Inspect preference was restored byte-for-byte.

This supplement qualifies the changed behavior on the available serviced VS2022 host only. It does not convert the development package into a publication-approved release.

## State

`2.0.8.0` is a local correction candidate. Public Marketplace `2.0.7.0` is superseded because its `netstandard2.0` ObjectSource was stale and a real Visual Studio 2022 `17.9.34902.65` direct OpenCvSharp visualizer request failed with `Unsupported Mat type: CV_32SC1` (`RBV-ERROR-20260904005014-8DA38D99`).

The correction does not change the `Int32` image contract or UI. It makes the Extensibility package consume the ObjectSource built beside the active routed `BaseOutputPath`, and makes packaging fail unless all four debugger-side payloads are byte-identical to that fresh build. The exact candidate was installed as an in-place update from `2.0.7.0` and exercised on the available Visual Studio 2022 `17.14.37516.0` host. Commit, push, CI, clean installation, VS2026 installation, Marketplace upload, public readback, tag, GitHub Release, and deployment remain separate actions.

## Exact Local VSIX

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,515,206` bytes |
| SHA-256 | `5E6BB8F5D4259FF7BF34359B2CD53F817B0EBAC3A3B2C8143339C6029081C04D` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Manifest version | `2.0.8.0` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72` |

## Debugger Payload Equality

The following values were read independently from the routed Release output and the exact VSIX. All four comparisons passed.

| VSIX entry | Fresh build SHA-256 | VSIX SHA-256 |
| --- | --- | --- |
| `netstandard2.0/RawBufferVisualizer.Core.dll` | `9A485BA12515F29F734FF1D4EB659164C01ABCFD48F4CBA6FDC2E8B352BB420C` | `9A485BA12515F29F734FF1D4EB659164C01ABCFD48F4CBA6FDC2E8B352BB420C` |
| `netstandard2.0/RawBufferVisualizer.Sdk.dll` | `68FFCAA7DA037D7048770D7A88272D47CC23D21F38BB2EEDB05BBEF9992D8FAC` | `68FFCAA7DA037D7048770D7A88272D47CC23D21F38BB2EEDB05BBEF9992D8FAC` |
| `netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.dll` | `5A61F829FF01020D193133B6E84EFC59FEE69643F04A04D391F4D587580EDC19` | `5A61F829FF01020D193133B6E84EFC59FEE69643F04A04D391F4D587580EDC19` |
| `netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.deps.json` | `3E1477B9BA566FD2A3657B3C494A489B8E7D044F8958F7E7DBED76AED20EC15C` | `3E1477B9BA566FD2A3657B3C494A489B8E7D044F8958F7E7DBED76AED20EC15C` |

The corrected ObjectSource hash differs from the stale 2.0.7 value `E23CC8782B6C87B2A81DBAB9CD830ACF355DB764E5CE2685FDFF96C8E0148954`, which was byte-identical to 2.0.6.

## Installed Visual Studio 2022 17.14 Regression

The exact VSIX above replaced installed `2.0.7.0` with `2.0.8.0` on Visual Studio 2022 Community `17.14.37516.0` (`419f0858`) without uninstall, registration repair, or `/ResetSkipPkgs`. The installed extension root is `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_419f0858\Extensions\zq51j0xk.srq`. SHA-256 comparisons between the exact VSIX and the installed manifest, ObjectSource, out-of-process provider, VSSDK package, and `.pkgdef` passed 5/5.

The installed `Int32Industrial` scenario opened an initialized OpenCvSharp `CV_32SC1` from the code DataTip visualizer glyph, then exercised Automatic Inspector, an Emgu `Cv32S` C1 Mat, a one-item OpenCvSharp Mat collection, neutral raw-buffer owners, and a padded-stride frame. The direct result was `1280 x 960 Int32` with stride `5120`; the automatic scan opened 5/5 candidates plus 1/1 collection item with zero failures; the padded frame used stride `5184`; and the inspector returned signed value `-19918` with bytes `50 178 255 255`. The package-protocol error count was zero.

The smoke ran on dynamically selected `\\.\DISPLAY2` at `1920 x 1080`, with the verified Visual Studio window rectangle fully intersecting that monitor. Fresh screenshots were visually reviewed: the industrial PCB structure is visible in the direct, automatic, and padded-stride views, with no product error card or corrupted image. This is exact installed-candidate evidence for VS2022 17.14, not VS2022 17.9 evidence.

The copied Visual Studio ActivityLog records successful begin/end package load for `RawBufferVisualizerPackage`. It also contains an independent Visual Studio Extension Manager update-check failure for missing `PkgDefMgmt.dll`; that stack is outside the Raw Buffer Visualizer handoff and did not affect this scenario, but the global ActivityLog is therefore not described as error-free.

## Checks Run

| Check | Result |
| --- | --- |
| Routed `Publish-VisualStudioExtension.ps1` Release build/package | Passed; 0 errors and 18 pre-existing `VSTHRD010` warnings |
| Exact VSIX version/content/registration guards | Passed; `2.0.8.0`, 72 entries |
| Four fresh-build/VSIX debugger payload SHA-256 comparisons | Passed; 4/4 exact matches |
| Extracted packaged-payload runner | Passed OpenCvSharp `CV_32SC1`, Emgu CV `Cv32S` C1, Bitmap, and existing BGR paths using the DLLs extracted from the VSIX |
| Negative stale-payload guard | Passed; an intentionally changed ObjectSource in an isolated copied build was rejected with `VSIX contains a stale netstandard2.0 debugger payload` |
| Release solution restore/build | Passed; 0 errors and 18 pre-existing `VSTHRD010` warnings |
| Aggregate Release self-tests | Passed |
| Release communication | Passed for `2.0.8` |
| Environment Check contracts | Passed |
| Publish script PowerShell parse | Passed |
| Marketplace dry run | Passed with the exact VSIX, English Overview, and generated `vs-publish.json`; no publication was executed |
| In-place update on VS2022 Community `17.14.37516.0` | Passed from installed `2.0.7.0` to exact candidate `2.0.8.0`; no uninstall or repair |
| Exact VSIX-to-installed critical payload equality | Passed; manifest, ObjectSource, provider, VSSDK package, and `.pkgdef` matched 5/5 by SHA-256 |
| Installed `Int32Industrial` scenario on VS2022 17.14 | Passed direct DataTip `CV_32SC1`, automatic 5/5, collection 1/1, Emgu `Cv32S`, padded stride 5184, signed pixel inspection, and zero package-protocol errors |
| Exact Visual Studio 2022 17.9 direct `CV_32SC1` runtime | Not run on this workstation; required before publication |
| Clean install on current VS2022 and exact-candidate run on stable VS2026 | Not run in this step; required by the release runbook before publication |

## Evidence

- Build/package/equality root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904`
- Package-extracted runtime probe: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\packaged-payload-probe-20260904-1`
- Negative guard build: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\negative-guard-build-1`
- Marketplace dry-run manifest: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\marketplace\vs-publish.json`
- Installed VS2022 17.14 result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\installed-vs2022-17.14\Int32Industrial\Int32Industrial-installed-vsix.json`
- Installed VS2022 17.14 screenshots: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\installed-vs2022-17.14\Int32Industrial\int32-industrial-visualizer-glyph.png`, `int32-industrial-opencv-direct.png`, `int32-industrial-automatic-matrix.png`, and `int32-industrial-padded-stride.png`
- Installed package log: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\installed-vs2022-17.14\Int32Industrial\temp\RawBufferVisualizer\VisualStudio\package.log`
- Captured Visual Studio ActivityLog: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904\installed-vs2022-17.14\Int32Industrial\Int32Industrial-activity-log.xml`
- Invalidated predecessor: [release-qualification-2.0.7.md](release-qualification-2.0.7.md)
- English Overview: [marketplace-overview-2.0.8.md](marketplace-overview-2.0.8.md)
- Korean review copy: [marketplace-overview-2.0.8.ko.md](marketplace-overview-2.0.8.ko.md)
- Marketplace release notes: [marketplace-release-notes-2.0.8.md](marketplace-release-notes-2.0.8.md)

## Required Next Gate

Do not advance the historical VSIX below. The current-source Automatic Inspector development package has now passed installed runtime on the available serviced VS2022 host, but publication still requires the affected Visual Studio 2022 `17.9` `CV_32SC1` path, a clean/update test from public 2.0.7 on a serviced VS2022 profile, stable-VS2026 regression, supported 125%-200% DPI checks, refreshed Marketplace media/copy, and a final frozen artifact/readback.

```text
Status: Incomplete
Scope: 2.0.8 debugger-payload correction plus current-source Automatic Inspector incremental batching, stable-row refresh, progress/Stop/load-more UI, and installed VS2022 17.14 runtime qualification.
Acceptance criteria: routed package and fresh debugger-payload equality -> pass; packaged CV_32SC1/Cv32S and stale-payload rejection -> pass; historical installed Int32 regression -> pass; current development VSIX-to-installed critical equality -> 6/6 pass; current installed 50-object/partial/full/isolated-failure/three-rescan/50-to-42/pointer-replacement/large-frame Stop-resume matrix -> pass on VS2022 17.14; exact VS2022 17.9, serviced-VS2022 clean update, stable VS2026, 125%-200% DPI, and refreshed release media -> not run.
Verification: Historical routed build/package probes plus current exact reinstall, installed hash comparison, live F5/Break UI operations, eight JSON state/identity/address assertions, package-log delta review, ActivityLog package-load review, and source restoration hash check passed.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.8\20260904 and D:\OpenVisionLab-TestData\RawBufferVisualizer\automatic-inspector\installed-vs2022-17.14; exact historical and current development VSIX hashes are recorded above.
Boundary / next dependency: Current-source behavior is verified on one serviced VS2022 host but the development VSIX is not approved for Marketplace publication. Exact VS2022 17.9, clean/update VS2022, stable VS2026, supported DPI, refreshed media/copy, and final artifact qualification remain. No commit, push, CI, Marketplace mutation, tag, release, or deployment was performed.
```

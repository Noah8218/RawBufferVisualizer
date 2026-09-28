# Changelog

This file records user-visible Raw Buffer Visualizer changes. The Tool Window shows a concise one-time summary for each installed version; the complete history remains available here.

## [2.2.0] - 2026-09-28

- Opens supported image variables directly in the docked viewer without displaying an auxiliary Raw Buffer visualizer dialog.
- Gives Raw Buffer priority for recognized image collections while retaining the usual visualizer for non-image collections such as List<int>. The menu uses the product name without Direct or test labels.
- Adds two-point horizontal and vertical pixel measurement. Sampled previews disable measurement; full-resolution images enable it. Selecting another image clears the measurement.
- Keeps selected image rows visible, wraps long error titles, and prevents a late sampled preview from replacing an already loaded full-resolution image.
- Preserves checked live reads for large pointer-backed images and correct debugger byte forwarding. Live sources become unavailable when execution continues or the process exits; captured snapshots remain viewable.

## [2.1.0] - 2026-09-15

Source changes for inspection, pixel correctness, mapping/save and histograms, including the 2.0.9 fixes below. This entry does not establish Marketplace availability or installed-IDE qualification.

### Fixed

- Updates paused/running/session-ended status independently of the Auto Inspect preference, clears stale session information and enables manual scanning only while paused and idle.
- Clears transient panel/action notices on state changes without clearing real image errors; closing What's New no longer leaves its old notice covering subsequent debugger status.
- Corrects Bitmap signed stride, indexed palettes and alpha semantics across import/debugger paths; corrects sampled Bayer colors, Float32 source-consistent scaling, numeric readout precision and stable raw-value statistics.
- Processes cancellation and debugger transitions during automatic discovery, bounds failed member attempts and prevents cancelled discovery from poisoning the cache or overwriting newer results.
- Follows the current solution for mapping lookup, respects assembly identity and protects against corrupt, unsupported or concurrently changed mapping files.

### Added and improved

- Reclaims Images space by moving persistent frame/help paragraphs into the question-mark help beside Auto Inspect while retaining visible status and scan errors.
- Adds raw-value histograms for supported scalar/Bayer/file/live sources, color-channel selection, explicit full/sample coverage, non-finite counts and cancellable background calculation.
- Adds visible mapping scope/destination and bounded asynchronous snapshot saving with progress, cancellation and failure-safe preservation of existing snapshots.
- Removes the Float32 renderer's temporary per-pixel byte-array allocation. No universal timing guarantee is implied.

### Standalone application

- Adds explicit RAW setup/validation, an empty-state example, accessible document selection/closing, duplicate activation, readable diagnostics, export outcomes and histogram explanations.
- Replaces stale normal diagnostics with current file-read errors and supports Refresh recovery after an open RAW file is restored.
- These standalone changes belong to the separate application and are not an additional application installed by the VSIX.

### Included 2.0.9 corrections

- Checked live reads for pointer-backed images at or above 8 MiB; final-row ROI spans; stable technical RPC diagnostics; refreshed matching error rows; and preserved pinned registered opens. The detailed 2.0.9 entry follows.

## [2.0.9] - 2026-09-12

Raw Buffer Visualizer `2.0.9` removes the debugger-RPC bottleneck reported for medium native images, corrects inferred ROI memory spans, and stabilizes repeated visualizer use in the docked Tool Window.

### Fixed

- Routes pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads of 8 MiB or more through the existing checked live-process-memory path instead of serializing the complete image through repeated debugger RPC snapshot requests.
- Computes an inferred 2D buffer span as `stride * (height - 1) + minimum row bytes`, so a submatrix or ROI does not read nonexistent padding after its final row.
- Applies the same final-row rule to registered Mat transfers, inferred ImagePtr/RawBufferView values, and Automatic Inspector's known registered-image capture.
- Preserves explicit caller-supplied buffer lengths and continues to reject unreadable, released, partial, or overflowed memory ranges.
- Refreshes an existing manual error row when the same expression fails again for the same technical reason, while generating a new report ID and retaining the latest diagnostic details.
- Leaves the permanent pinned Tool Window open across consecutive registered visualizer invocations; temporary debugger-host lifetime remains owned by Visual Studio's `VisualizerTarget` contract.

### Diagnostics

- Keeps the original debugger exception in the local support report while replacing localized remote-exception text in the visible error with a stable technical RPC failure description.
- Snapshot chunk failures now identify the RPC operation, chunk number, byte offset, requested byte count, total byte count, exception type, and HRESULT.

### Compatibility

- Keeps 4 MiB chunks for pointer-backed images below 8 MiB and for non-pointer snapshot sources.
- Retains the existing Visual Studio 2022 `17.9+` x64 and stable Visual Studio 2026 `18.x` installation targets.

## [2.0.8] - 2026-09-04

Raw Buffer Visualizer `2.0.8` corrects the Visual Studio 2022 debugger payload packaged for the signed 32-bit matrix support introduced in 2.0.7.

### Fixed

- Packages the `netstandard2.0` debugger ObjectSource, Core, SDK, and dependency manifest from the current routed Release build instead of a stale repository-local build directory.
- Adds a fail-closed SHA-256 gate that compares every debugger-side payload in the VSIX with the corresponding fresh build output before a candidate can be produced.
- Restores direct OpenCvSharp `CV_32SC1` and Emgu CV `Cv32S` C1 visualization on the Visual Studio 2022 debugger-host path while retaining the existing `Int32` rendering and inspection behavior.

### Improved

- Bounds Automatic Inspector discovery to 128 current-frame image candidates and opens an initial batch of at most eight images or a soft two-second budget.
- Adds non-modal candidate/refreshed/deferred/failed counts plus **Load next 8**, **Load all this Break**, and **Stop** for large results.
- Refreshes matching automatic rows in place across repeated Break/F10/Scan Now operations, coalesces overlapping scans, and prevents cached inference from bypassing the mapping-required safety gate.

### Release safety

- Public `2.0.7.0` is superseded because its packaged `netstandard2.0` ObjectSource was byte-identical to the 2.0.6 payload. Reinstalling 2.0.7 cannot correct that package content; use 2.0.8 or later.
- The Marketplace extension identity and Visual Studio 2022 `17.9+` / stable Visual Studio 2026 `18.x` targets are unchanged.

## [2.0.7] - 2026-09-03

Raw Buffer Visualizer `2.0.7` adds signed 32-bit, one-channel matrix visualization for segmentation labels, integer result maps, and other `CV_32SC1`-style buffers.

### Added

- Maps OpenCvSharp `CV_32SC1` and Emgu CV `Cv32S` C1 to the new `Int32` raw pixel format on direct visualizer, Automatic Inspector, and supported collection paths.
- Accepts mapped `int[]` data and `RawBufferSnapshot.FromInt32Array`, including explicit byte order.
- Displays signed values through min/max grayscale autoscaling while preserving the exact signed integer and four raw bytes in pixel inspection.
- Supports padded stride, tiled/file-backed rendering, sampled pointer previews, and Buffer Doctor candidates for `Int32`.

### Compatibility

- Retains the Visual Studio 2022 `17.9+` x64 installation floor and stable Visual Studio 2026 `18.x` contract.
- Retains prior enum numeric values by appending `Int32`; `CV_32SC2`, `CV_32SC3`, `CV_32SC4`, and 3D data remain unsupported.

## [2.0.6] - 2026-09-03

Raw Buffer Visualizer `2.0.6` advances the immutable Marketplace package version after `2.0.5.0` became public. It contains no functional or compatibility change from the validated 2.0.5 behavior.

### Changed

- Advances the extension, assembly, and package identity to `2.0.6`/`2.0.6.0` instead of attempting to overwrite public `2.0.5.0`.
- Retains exact debugger expression names, native `Ptr` versus `Pixels` provenance, complete image-array registration, fail-closed pointer reads, negative-stride Bitmap correction, and the existing Visual Studio support contract.

## [2.0.5] - 2026-09-03

Raw Buffer Visualizer `2.0.5` distinguishes an image object's native pointer from the address used to read its pixels, keeps the debugger expression name visible above each thumbnail, and rejects incomplete native-memory reads instead of presenting partial image data.

### Added

- Shows the exact debugger object/expression name above each thumbnail, including collection roots such as `imageList[0]` and `imageDictionary[key]`.
- Shows OpenCvSharp/Emgu `Ptr` separately from `Pixels` (`Data`/`DataPointer`), uses `Ptr / Pixels` for ImagePtr, `Buffer / Pixels` for `RawBufferView`, and records Bitmap `Scan0 / Pixels` as captured provenance.
- Adds separate **Copy pointer address** and **Copy pixel address** actions, with the same values in Descriptor details and structured workspace export.
- Shows the process ID and `LIVE`, `CAPTURED`, `PREVIEW`, or `UNAVAILABLE` state when source-location information is available.

### Fixed

- Reopening the same address observes its current bytes instead of treating the address as an object identity.
- Released, inaccessible, or partially readable pointer ranges fail with a controlled error before incomplete image data is accepted.
- Corrects top-to-bottom transfer for `System.Drawing.Bitmap` images whose native stride is negative.
- Registers Bitmap, OpenCvSharp Mat, and Emgu Mat arrays with complete assembly-qualified identities so Visual Studio does not reject their visualizer metadata with a version-string error.

### Compatibility

- Retains the same Marketplace extension identity, Visual Studio 2022 `17.9+` support floor, stable Visual Studio 2026 `18.x` target, and existing 2.0 workflows.

## [2.0.4] - 2026-08-25

Raw Buffer Visualizer `2.0.4` puts the full-viewer reset where accumulated images are managed and keeps the action explicit at narrow and wide docked sizes.

### Changed

- Moved the existing full reset from the top toolbar to a clearly labelled **Clear all** button directly above the image list.
- Keeps **Clear all** disabled when the image list is empty and enables it as soon as an image is loaded.
- Clarifies in the tooltip that clearing removes every loaded image and resets the viewer without changing data in the paused debuggee.

### Verified

- A direct 20-image collection transfers all 20 entries, and one **Clear all** action removes the complete accumulated list and document-dependent viewer state.
- Narrow, medium, and wide docked layouts retain the full **Images** and **Clear all** labels without clipping.

### Compatibility

- Retains Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` support through the existing Marketplace extension identity.

## [2.0.3] - 2026-08-24

Raw Buffer Visualizer `2.0.3` adds a direct debugger-visualizer path for concurrent image dictionaries and repairs the first registered `ImagePtr` handoff when the docked viewer has not been opened yet.

### Added

- Registered open generic `ConcurrentDictionary<TKey,TValue>` targets for .NET Framework and current .NET so the collection can be opened directly from DataTip, Locals, Autos, or Watch.
- Preserved dictionary keys as image-row names, isolated unsupported entries as visible failures, and retained the existing 256-entry bound.

### Fixed

- Preloads the lightweight VSSDK package when debugging begins and passively arms its handoff listener before the first visualizer click, preventing the first registered `ImagePtr` handoff from re-entering the package while it is loading.
- Corrected installed-package validation to inspect `RawBufferVisualizer.VisualStudio.Vssdk.dll` and its matching `.pkgdef` after the 17.9 compatibility split.

### Compatibility

- Retains Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` support through the existing Marketplace extension identity.

## [2.0.2] - 2026-08-09

Raw Buffer Visualizer `2.0.2` restores the Visual Studio 2022 `17.9+` support floor while retaining stable Visual Studio 2026 `18.x` compatibility.

### Improved

- Split the in-process VSSDK package from the out-of-process debugger visualizer providers so the installed extension uses the Visual Studio 17.9 SDK line without changing the docked inspection workflow.
- Kept Community, Professional, and Enterprise x64 installation targets on one unchanged Marketplace extension identity.
- Added Visual Studio-native toolbar and inspector icons, compact icon-only primary commands, actionable empty-viewer guidance, and image-aware command states.
- Removed the separate Mat collection setting; every manual or Break Mode scan now applies the existing bounded exact-Mat collection policy automatically.

### Fixed

- Shortened claimed debugger-handoff filenames so a long writable temporary-storage path no longer prevents a registered image from reaching the docked viewer.
- Updated installation, packaging, and installed-smoke tooling for the Visual Studio 2022 `17.9+` compatibility contract.

## [2.0.1] - 2026-08-06

Raw Buffer Visualizer `2.0.1` republishes the complete 2.0 feature set at a higher extension version so Visual Studio can recognize it as a Marketplace update.

### Fixed

- Raised the VSIX, assembly, product-registration, and release-communication versions from `2.0.0` to `2.0.1` without changing the Marketplace extension identity.
- Prepared a newly built `2.0.1.0` package so installations offered the earlier Marketplace entry can receive the complete 2.0 payload through the normal update path.

## [2.0.0] - 2026-08-06

Raw Buffer Visualizer `2.0.0` adds safer 2D buffer inspection, Connect Doctor, and debugger-lifetime protection for Visual Studio 2022 and Visual Studio 2026.

### Added

- Added **Diagnose interpretation** inside Connect Your Buffer. It ranks bounded Buffer Doctor candidates without opening another ToolWindow or starting a new debugger transfer.
- Candidate selection updates only the visible mapping draft and preview; only **Save Mapping** persists. Repeated selection closes the result panel, and **Use Suggested Roles** resets the draft.
- Added a documented compatibility matrix for registered `RawBufferView`/`RawBufferSnapshot`, mapped pointer buffers, and mapped `byte[]`, `ushort[]`, and `float[]` carriers.

### Improved

- Preserved up to three format alternatives that match the current width, height, and stride so packed Mono10/Mono12 candidates are not crowded out by higher-scoring but unrelated dimension factorizations.
- Added explicit accessible names and themed selected/focus/hover/disabled states to ranked interpretation rows.
- Registered and mapped metadata now use the same checked dimension, stride, buffer-length, enum, and valid-bits validation boundary before transfer.
- Valid `Mono16` values from 1 through 16 remain accepted; fixtures explicitly preserve 10, 12, 14, and 16.

### Fixed

- Invalid `Mono16` valid-bit counts now fail instead of remaining advisory warnings.
- `Mono10PackedLsb` and `Mono12PackedLsb` now reject valid-bit values that contradict their fixed 10-bit and 12-bit layouts.
- Undefined pixel-format/byte-order values and overflowing descriptor arithmetic now fail before allocation, transfer, or rendering.
- Continue/process exit now disposes live process-memory sources, marks their rows `Unavailable`, and prevents progressive or pixel reads from touching invalid memory; copied managed buffers remain available.
- Delayed handoffs captured before Continue are rejected in Run Mode and cannot revive in a later Break session.

## [1.0.53] - 2026-08-03

### Added

- Added an **Environment** panel that checks only the supported Visual Studio host, loaded extension version, and temporary-storage writability required by the extension.
- Added explicit **Refresh** and **Copy diagnostic report** actions. The report excludes credentials, environment-variable values, and image payloads, and warns that local paths must be reviewed before sharing.

### Improved

- Large pointer-backed sampled previews use a bounded estimate of storage page reads.
- Contributor and demo-media utilities remain documented in the development prerequisites instead of being presented as product runtime requirements.
- Selecting **Environment** again closes the panel; the redundant in-panel **Close** action was removed.
- Selecting **What's New** again closes the release highlights. **Dismiss** still records the version as seen, while a simple toggle close does not change that saved preference.

### Fixed

- Updated the Automatic Vision Inspector layout check to use a narrow current-workspace diagnostic seam instead of reflecting a removed `_activeDocument` field.
- Updated the Smart Type Mapper layout check to discover the newest restored `Microsoft.VSSDK.BuildTools` package instead of requiring historical package `17.9.3168`.
- Environment Check now accepts the build-suffixed file-version text reported by installed Visual Studio hosts, so supported `17.14+` and `18.x` sessions are not mislabeled as unknown.
- Environment Check now labels unavailable required state as **Attention** rather than suggesting an unrelated install action.

### Safety contract

- Removed the uncleared proprietary vendor adapter, its exact-type registration, SDK audit script, and vendor-named test fixtures. Vendor-neutral buffer inspection remains available.
- Opening, refreshing, copying, or toggling Environment Check does not scan the current frame, open an image, change the active document, install software, or modify VSPackage registration.
- The existing explicit debugger visualizer, **Scan Now**, Fit/Manual, snapshot ownership, and preview-to-full handoff contracts remain unchanged.

## [1.0.52] - 2026-08-02

### Fixed

- Updated the stable Microsoft Visual Studio Extensibility SDK from `17.9.2092` to the `17.14` line so registered debugger visualizers activate on stable Visual Studio 2026 `18.x` hosts.
- Raised the Visual Studio 2022 support floor to the serviced `17.14` baseline so the declared range matches the extension runtime dependencies.
- Added per-document snapshot leases so the 24-hour stale cleanup cannot delete a file-backed payload while its document is still open.
- Preview-to-full handoff replacement now releases the previous snapshot directory; removing, clearing, or disposing the Tool Window releases the current directory.

### Improved

- Moved document activation/removal/disposal into `RawBufferDocumentWorkspace` and claimed handoff ACK/NACK policy into `ClaimedHandoffOpenCoordinator`, with focused tests for both boundaries.
- Hardened installed-VSIX UI automation for VS 2026 Locals virtualization by reacquiring a row after selection before clicking its debugger visualizer.
- Carries forward the automatic Mat collection inspection and one-time release highlights prepared in the unpublished `1.0.51` candidate.


## [1.0.51] - 2026-08-01

Not released: Visual Studio 2026 activation failed; use the later source line.

### Added

- Added optional Automatic Vision Inspector expansion for exact OpenCvSharp and Emgu CV `Mat` lists and one-dimensional arrays.
- Added one-time, non-modal release highlights in the Tool Window. Dismissal is saved per user across Visual Studio restarts, and **What's New** reopens the current highlights.

### Improved

- Each automatic collection element now succeeds or fails independently, so null or disposed Mats do not hide valid images from the same collection.
- Automatic collection work is bounded to 8 items per collection, 16 items and 8 roots per scan.

### Release communication

- Refreshed the Marketplace Overview and release notes so the public listing describes Automatic Vision Inspector, Vision Buffer Doctor, Smart Type Mapper, automatic Mat collections, and current safety limits.
- Carried forward the `1.0.50` handoff, menu-registration, automatic direct-Mat, and Fit/Manual reliability improvements in the higher version required for Marketplace updates.

### Compatibility

- Documented the then-intended Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` range. Subsequent VS2026 runtime failure superseded this candidate.
- `System.Drawing.Bitmap`, mixed collections, dictionaries, jagged arrays, multidimensional arrays, and arbitrary enumerables continue to use their registered debugger-visualizer path.
- Automatic Inspector does not invoke arbitrary vendor methods or decode private native layouts.

## [1.0.50] - 2026-07-29

### Improved

- Automatic Inspector can open initialized exact OpenCvSharp and Emgu CV `Mat` values from the selected stack frame after assignment.
- Debugger handoff uses atomic claiming plus explicit ACK/NACK completion.
- New and selected images remain in aspect-correct Fit mode; manual zoom and pan are preserved.
- Visual Studio package and View-menu registration checks reject duplicate or retired registrations.

Automatic Mat collection expansion and the release-highlights banner are included in the later 1.0.52 source line.

## [1.0.49] - 2026-07-29

### Fixed

- Assigned a new VSPackage identity so Visual Studio profiles affected by the failed `1.0.47`/`1.0.48` package state do not reuse it after update.
- Kept the Marketplace extension identity unchanged so installation remains a normal update.
- Added a visible diagnostic when the View command reaches the package but the Tool Window cannot be created.
- Added packaging guards for the retired Package GUID.

## [1.0.48] - 2026-07-28

### Fixed

- Moved VSSDK package and command registration into the hybrid Marketplace project and removed developer-registry repair from the normal install path.
- Added build guards for the former split-project registration layout.

This release was superseded by `1.0.49` after an external upgraded Visual Studio profile still failed to load the reused VSPackage identity.

## [1.0.47] - 2026-07-28

### Added

- Added Automatic Vision Inspector for bounded Locals/Arguments discovery with isolated `[Auto]`, `[Map]`, and `[Failed]` outcomes.
- Added Vision Buffer Doctor for ranked stride, format, valid-bit, byte-order, width, and height interpretations.
- Added persisted **Auto Inspect on Break** and independent **Scan Now** workflows.

### Improved

- Smart Type Mapper became the explicit fallback for ambiguous compatible company-specific wrappers.

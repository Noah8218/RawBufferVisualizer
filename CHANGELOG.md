# Changelog

This file records user-visible Raw Buffer Visualizer changes. The Tool Window shows a concise one-time summary for each installed version; the complete history remains available here.

## [Unreleased]

No post-`2.0.8` changes are queued.

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

Release status: public but superseded by `2.0.8`. The published VSIX contains a stale `netstandard2.0` debugger ObjectSource, so direct `CV_32SC1` visualization can fail with `Unsupported Mat type: CV_32SC1` on the Visual Studio 2022 debugger-host path even though the feature exists in source.

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

Release status: published Marketplace release.

### Added

- Added an **Environment** panel that checks only the supported Visual Studio host, loaded extension version, and temporary-storage writability required by the extension.
- Added explicit **Refresh** and **Copy diagnostic report** actions. The report excludes credentials, environment-variable values, and image payloads, and warns that local paths must be reviewed before sharing.

### Improved

- Large pointer-backed sampled previews now use a bounded estimate of cold storage page reads. On the restored workstation, the final regression run's first benchmark-process access completed in `0.992 s` for the dense 100k fixture and `0.692 s` for the dense 200k fixture without changing the existing five-second gate.
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

Release status: locally qualified development candidate. Marketplace publication, propagation, and the corresponding GitHub tag/release are not complete.

### Fixed

- Updated the stable Microsoft Visual Studio Extensibility SDK from `17.9.2092` to the `17.14` line so registered debugger visualizers activate on stable Visual Studio 2026 `18.x` hosts.
- Raised the Visual Studio 2022 support floor to the serviced `17.14` baseline so the declared range matches the extension runtime dependencies.
- Added per-document snapshot leases so the 24-hour stale cleanup cannot delete a file-backed payload while its document is still open.
- Preview-to-full handoff replacement now releases the previous snapshot directory; removing, clearing, or disposing the Tool Window releases the current directory.

### Improved

- Moved document activation/removal/disposal into `RawBufferDocumentWorkspace` and claimed handoff ACK/NACK policy into `ClaimedHandoffOpenCoordinator`, with focused tests for both boundaries.
- Hardened installed-VSIX UI automation for VS 2026 Locals virtualization by reacquiring a row after selection before clicking its debugger visualizer.
- Carries forward the automatic Mat collection inspection and one-time release highlights prepared in the unpublished `1.0.51` candidate.

### Compatibility evidence

- Stable Visual Studio 2026 Community `18.8.2` (`18.8.12023.21`) passed the installed `ReleaseAnnouncement`, `AutomaticCollections`, and `MultiLibraryHybrid` core matrix: Bitmap registered handoff plus eight automatic opens produced nine documents, zero errors, one Open command, one Scan command, and zero protocol errors.
- The original `1.0.51` candidate remains unchanged as failed evidence: its SHA-256 is `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`, and its registered debugger visualizer cannot activate on VS 2026 `18.8.2` because its older framework requests `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` while that host supplies `18.0.0.0`.

## [1.0.51] - 2026-08-01

Release status: superseded local candidate. It was never published because stable Visual Studio 2026 runtime qualification failed.

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

The public Marketplace `1.0.50.0` package was built before automatic Mat collection expansion and the in-product release-highlights banner. Those changes were first prepared in unpublished `1.0.51` and are carried into candidate `1.0.52`.

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

[Unreleased]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.8...HEAD
[2.0.8]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.7...v2.0.8
[2.0.7]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.6...v2.0.7
[2.0.6]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.5...v2.0.6
[2.0.5]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.4...v2.0.5
[2.0.4]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.3...v2.0.4
[2.0.3]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/Noah8218/RawBufferVisualizer/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/Noah8218/RawBufferVisualizer/compare/v1.0.53...v2.0.0
[1.0.53]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.53
[1.0.52]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.52
[1.0.51]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.51
[1.0.50]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.50
[1.0.49]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.49
[1.0.48]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.48
[1.0.47]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.47

The version links above are release targets used by the tag workflow. A link is expected to remain unavailable until that version's GitHub tag and Release have actually been created.

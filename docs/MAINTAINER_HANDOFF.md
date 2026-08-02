# Raw Buffer Visualizer Maintainer Handoff

This is the canonical continuation document for the next conversation. Read it after `AGENTS.md` and `docs/README.md`.

## Snapshot

| Item | Verified state |
| --- | --- |
| Last verified | 2026-08-02 KST |
| Canonical repository | `C:\Git\RawBufferVisualizer` |
| Branch / remote | `main` / `https://github.com/Noah8218/RawBufferVisualizer.git` |
| Implementation baseline | `854cb67` (`Qualify Raw Buffer Visualizer 1.0.52`) plus the immediately following documentation checkpoint on `origin/main` |
| Source and VSIX version | `1.0.52` / `1.0.52.0`; public Marketplace line is `1.0.50.0`; preserved `1.0.51.0` is a failed unpublished candidate |
| Visual Studio support | VS2022 `17.14+` and stable VS2026 `18.x`, Community/Professional/Enterprise x64. Exact `1.0.52` installed runtime passed on Community `17.14.33` and `18.8.2`; manifest range is `[17.14,18.0)` |
| Public Marketplace version | `1.0.50.0`; published 2026-07-29 13:09:54 KST, 2,001,513 bytes, SHA-256 `2014AA8D679AF3D01F0B16CC304E77064ABCF0B0725BDC6BD543B7C08CDA397E` |
| Git tags / GitHub Releases | Local annotated tag `v1.0.45` on `a23d8ad` created 2026-07-26; not pushed yet; no GitHub Release yet |
| Product stage | Public Marketplace Preview; exact `1.0.52` local packaging and VS2022/VS2026 runtime qualification passed. Marketplace publication is blocked by the separate-PC public `1.0.50 -> 1.0.52` update gate |
| Working tree | The intended `1.0.52` implementation, version, release-document, and test-infrastructure changes are committed; only the explicitly excluded pre-existing untracked paths below remain local |

Public links:

- Marketplace: https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer
- Repository: https://github.com/Noah8218/RawBufferVisualizer
- CI evidence: https://github.com/Noah8218/RawBufferVisualizer/actions/runs/29512606667

The public Marketplace package is `1.0.50.0`. The Gallery API was rechecked on 2026-08-02 and still reports only `1.0.50.0`, last updated `2026-07-29T04:09:54.877Z`; the rendered public overview is still titled `Raw Buffer Visualizer 1.0.47 Marketplace Overview`. The original `1.0.51` candidate remains at the repository publish path, 2,011,587 bytes, SHA-256 `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`; it failed stable VS2026 registered-provider activation and must not be published or overwritten. The exact current `1.0.52` candidate is on `D:`, 1,902,513 bytes, SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. It passed the core installed runtime matrix on VS2022 `17.14.33` and VS2026 `18.8.2`. Full current evidence: `docs/release-qualification-1.0.52.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52`.

## Committed Change Set And Ownership

The `1.0.52` source, tests, scripts, and release documents below are committed at `854cb67`. Continue from `origin/main`; do not replace them with the older `43e347c` tree.

Current intended changes:

- release/version alignment: `CHANGELOG.md`, `README.md`, both Visual Studio project versions, VSIX manifest/package version and `17.14` floor, embedded release notes, `ReleaseAnnouncement.cs`, and its tests;
- current public copy: `docs/marketplace-overview-1.0.52.md`, `docs/marketplace-release-notes-1.0.52.md`, `docs/release-qualification-1.0.52.md`, plus the preserved failed `1.0.51` record;
- maintainer state: `AGENTS.md`, `docs/README.md`, this handoff, architecture, product direction, Marketplace checklist, and release runbook;
- release/test infrastructure: mandatory exact Marketplace `-VsixPath` plus source/manifest version rejection, custom D-drive publish root, D-drive output routing, leftmost-monitor evidence, VS2026 UI Automation handling, VS2022/VS2026 instance discovery, and per-machine extension-conflict detection in the modified PowerShell scripts;
- ToolWindow lifetime/ownership: document workspace, claimed-handoff coordinator, snapshot-directory leases, ToolWindow disposal, and focused aggregate self-tests.

Do not stage, commit, delete, or treat these pre-existing untracked paths as part of the release without separate inspection and user authorization:

```text
.tmp/
add-break-mode-scan.ps1
add-command-id.ps1
add-scan-locals-command.ps1
```

No current product-feature implementation file is awaiting a hidden partial edit. The remaining release blocker is external: a separate eligible PC must update from exact public `1.0.50` to the unchanged `1.0.52` candidate without repair or reinstall.

## Windows Reinstall Checkpoint

The source, release documents, and reusable validation scripts in the intended change set must be committed and pushed to `origin/main` before Windows is reinstalled. The exact candidate and runtime evidence are deliberately outside Git:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52
```

If the reinstall preserves `D:`, keep this directory unchanged. If the operation repartitions, formats, or otherwise replaces `D:`, copy the entire directory to external storage first and verify that the candidate is still 1,902,513 bytes with SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. A Git clone restores the source and documentation but does not restore this immutable VSIX or its installed-runtime evidence.

After reinstall, do not rebuild the qualified candidate for the remaining gate. On a separate serviced VS2022 `17.14+` or stable VS2026 profile, install or retain exact public Marketplace `1.0.50.0`, then update directly to the unchanged `1.0.52.0` VSIX without uninstall, repair, `/ResetSkipPkgs`, or manual registration repair. Restart Visual Studio and repeat ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, registration, and protocol checks. Marketplace publication is allowed only after that update passes.

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

For Basler, HIKROBOT, Spinnaker, eGrabber, Sapera, and MIL/Aurora, prefer `RawBufferView` first. Direct SDK adapters are later, optional work that must not add vendor dependencies to the core package.

## Completed Work

### Visual Studio integration

- A single Marketplace VSIX contains the debugger visualizer providers and docked ToolWindow required for normal operation.
- Supported invocations append to one docked image list instead of opening one window per image.
- Individual image providers exist for raw snapshot/view, Bitmap, OpenCvSharp, Emgu, and one exact ImagePtr compatibility target.
- Collection providers cover open generic `List<>` and `Dictionary<,>`, `ArrayList`, `Hashtable`, `object[]`, and registered image arrays.
- Multiple Visual Studio processes use per-process handoff routing.
- Failed values remain visible as error rows with stable error IDs, reason, `Copy Report`, and `Open Logs`.
- Package repair and reinstall scripts cover stale VSIX/VSSDK registration paths.

### Viewer and UX

- Thumbnail list, selected image viewer, responsive narrow/medium/wide dock layouts, and read-only metadata.
- Mouse-wheel zoom, drag pan, Fit, 1:1, linked views, and explicit Fit/Manual interaction state. New/selected images, Fit, and double-click enter Fit; wheel/pan/1:1 enter Manual.
- X/Y, GV or RGB/channel values, swatches, raw bytes, 5x5 neighborhood/statistics, and high-zoom pixel overlay.
- Selection overlay and pinned marker behavior; live hover remains active when no marker is pinned.
- Inspector, diagnostics, line profile, histogram, and Try interpretation controls.
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
- Mapped types open automatically inside already-registered collections (`List<>`, `Dictionary<,>`, `object[]`, arrays) without code changes or extension rebuilds.
- The collection fallback now tries the mapping file before heuristic shape detection; failures still appear as visible error rows with a `Map This Type` action and a member inventory payload.
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
- Optional **Mat collections** expands only exact OpenCvSharp/Emgu `Mat` `List<T>` and one-dimensional arrays. It defaults off, persists independently, caps work at 8 items per collection/16 items and 8 roots per scan, and leaves Bitmap, dictionaries, mixed lists, jagged/multidimensional arrays, and arbitrary `IEnumerable` on the registered collection path.
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
| Automatic Mat collection local qualification | Exact OpenCvSharp/Emgu Mat List/one-dimensional array scanning, persisted default-off option, bounded expansion, and per-element failure isolation are implemented. Release build, self-tests, no-break debuggee sample, script parsing, 540/1160 layout, ordinary scripted reinstall, installed metadata validation, and installed `AutomaticCollections` passed. Package: 2,007,042 bytes, SHA256 `01D4B19276625F4F73883C5113C8DA2044D3F5B7EAC49D6D1D3994CA727ECE3A`. Final scenario: seven rows, five opens, two isolated failures, duplicate-free rescan, settings restored, one Open/Scan command each, zero protocol errors. |
| Final 1.0.50 release-communication package | Exact 2,011,595-byte VSIX, SHA256 `E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93`. Release build 0 errors/18 existing warnings, self-tests, communication guard, Marketplace dry run, package inspection, and ordinary reinstall passed. Installed ReleaseAnnouncement passed persisted Dismiss, What's New reopen, and 0 image-row side effects; AutomaticCollections passed 5 opens/2 isolated failures; MultiLibraryHybrid passed 9 documents/0 errors. Each run reported one Open/Scan View command and 0 protocol errors. |
| Exact 1.0.51 final qualification | Exact 2,011,587-byte VSIX, SHA256 `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`. VS2022 `17.14.33` passed. A user-approved full VS2026 reinstall removed the old per-machine conflict and preserved the 55-component configuration, but unchanged `1.0.51` then failed registered Bitmap-provider activation on VS2026 `18.8.2` because `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` could not load. The artifact is preserved and superseded, not publishable. Evidence: `docs/release-qualification-1.0.51.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-runtime`. |
| Exact 1.0.52 local qualification | Exact 1,902,513-byte VSIX, SHA256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. Manifest `1.0.52.0` with `[17.14,18.0)` and no obsolete Classic DLL. Aggregate self-tests cover workspace lifecycle, snapshot lease/replacement/disposal, and claimed-handoff ACK/NACK. The same package passed ReleaseAnnouncement, AutomaticCollections (7 rows/5 opens/2 isolated failures), and MultiLibraryHybrid (9 documents/0 errors, Bitmap registered provider active) on VS2022 `17.14.33` and VS2026 `18.8.2`; one Open/Scan command and zero protocol errors per run. Registration audit and Marketplace dry run passed. Evidence: `docs/release-qualification-1.0.52.md` and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52`. |
| Buffer Doctor tests | 8 deterministic self-tests passed on the Core candidate generator/scorer (padded stride, diagonal shear, endianness, valid-bits, trailing-row fit, sampling cap, ambiguity group). |
| Smart Type Mapper tests | 6 deterministic self-tests passed on mapping store, ObjectSource extraction, enum mapping, and failure inventory. |
| Buffer Doctor UI smoke | `SmokeBufferDoctorPanel.ps1` passed: top candidate for a 2448x2048 padded Mono8 buffer is the correct `stride 2560` descriptor; applying it restores the image. Captures: `artifacts/ui/buffer-doctor/2026-07-26/`. |
| Smart Type Mapper UI smoke | `SmokeSmartTypeMapper.ps1` passed: error row shows `Map This Type`, dialog preselects members, live-memory preview renders, enum mapping saves to `%APPDATA%\RawBufferVisualizer\type-mappings.json`. Captures: `artifacts/ui/smart-type-mapper/2026-07-26/`. |
| Docked layout width smoke | `SmokeDockedLayoutWidths.ps1` passed at 540/900/1160 px after UI changes. |
| Installed VSIX Buffer Doctor | VS2022 17.14 installed-VSIX automation passed on 2026-07-27: five candidates appeared, the corrected interpretation was applied, and pixel inspection reported `GV 204`. Evidence: `artifacts/ui/installed-vsix-new-features/BufferDoctor-installed-vsix.json`. |
| Automatic Vision Inspector tests (historical base) | Five deterministic self-tests passed for confidence gates, direct/nested inference, ambiguous format handling, and nested mapping extraction before the `1.0.50` exact-Mat additions. The current aggregate self-test executable also covers Automatic Mat collections and release-announcement version/persistence behavior. |
| Automatic Vision Inspector layout | `SmokeAutomaticVisionInspectorLayout.ps1` and the focused inspector-panel smoke passed at 540/900/1160 px. Evidence: `artifacts/ui/automatic-vision-inspector/2026-07-27/`. |
| Installed VSIX Automatic Vision Inspector | VS2022 17.14 automation passed on 2026-07-28: a function argument plus five locals opened, one incomplete shape remained `[Map]`, and one null-pointer shape remained `[Failed]`; the six successful images stayed usable and repeated **Scan Now** did not duplicate rows. A second VS session restored the disabled preference, manual **Scan Now** still passed, and the pre-test user setting was restored. Evidence: `artifacts/ui/automatic-inspector-workflow/2026-07-28-final/`. |
| Installed VSIX Smart Type Mapper fallback | VS2022 17.14 automation passed on 2026-07-28: an unregistered `UnmappedCompanyFrame` remained an 88% `MappingRequired` candidate, `Mono12PackedLsb` rendered from live debuggee memory, Save wrote the inferred roles/value mapping, and automatic rescan reopened it as 640 x 484, stride 960, live source with zero final errors. The pre-existing user mapping was restored. Evidence: `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json` and the three `smart-type-mapper-*.png` captures. |
| Installed VSIX registered/automatic hybrid (historical) | Real OpenCvSharp, Emgu CV, and Bitmap values opened through registered visualizers; `RawBufferSnapshot`/`RawBufferView` and all registered types were absent from automatic rows; six camera-shape fixtures opened automatically. Final state: nine images, zero errors. The exact `1.0.50` rerun is recorded in the local release-qualification row above and replaced this file with fresh evidence. |
| Industrial SDK contract hardening | Official contracts for PFNC, Basler, Spinnaker, Vimba X, IDS peak, Euresys, HIKROBOT, Sapera, Zebra, and Zivid were reviewed. Deterministic padding/payload/offset/PFNC tests passed. IDS peak ICV 1.4.0 assembly metadata passed. Basler pylon, Spinnaker, and Vimba X were not installed. General vendor-runtime/hardware support is not proven. Evidence: `docs/industrial-camera-compatibility-validation.md` and `artifacts/validation/industrial-camera-sdk-contracts-20260728.json`. |
| Responsive Inspector affordance | Fresh 540/900/1160 px captures passed. The top `Inspector` button appears only below 760 px; medium layout exposes the bottom Inspector and wide layout exposes the right Inspector. Evidence: `artifacts/ui/inspector-button-visibility/2026-07-28/before/`. |

Same-machine before/current comparison for dense 5000 x 5000 Mono8:

| Metric | Before | 1.0.45 | Change |
| --- | ---: | ---: | ---: |
| Initial open path | 179.818 ms | 115.369 ms | 35.8% lower |
| Zoom average frame | 16.684 ms | 13.765 ms | 17.5% lower |
| Zoom maximum frame | 49.397 ms | 36.527 ms | 26.1% lower |
| Pan maximum frame | 21.951 ms | 17.056 ms | 22.3% lower |
| Pan average tile upload | 20.410 ms | 15.211 ms | 25.5% lower |

## Incomplete Or Unverified

1. Marketplace serves `1.0.50.0`; exact local `1.0.52.0` passed VS2022 and VS2026 qualification but has not been uploaded or observed after propagation.
2. A separate serviced VS2022 `17.14+` or stable VS2026 PC has not yet updated from exact public `1.0.50` to exact candidate `1.0.52` without uninstall, repair, or `/ResetSkipPkgs`. Local reinstall compatibility does not replace this external-PC update gate.
3. Local installed screenshots provide visual aspect evidence only. The full 540/900/1160 Fit/Manual assertions are current-source view evidence, not an installed-VSIX behavioral matrix.
4. Local tag `v1.0.45` exists on `a23d8ad` but is not pushed; there are no GitHub Releases. Release bookkeeping should follow the actual next publication decision instead of presenting the historical draft as current.
5. Marketplace CD exists, but PAT/publisher/environment approval and an actual automated publish run are not proven. Manual upload remains the known working release path.
6. The Release build still reports 18 `VSTHRD010` warnings in `ImageTypeRecognizer.cs` for EnvDTE access. They remain technical debt and must not be described as zero-warning output.
7. Vendor-specific SDK adapters are not implemented. `RawBufferView` and safe structural discovery are the generic supported answers; the exact ImagePtr registration remains a compatibility exception.
8. Basler pylon, Spinnaker, and Vimba X assemblies/live objects, drivers, emulators, cameras, and representative lifetime cases are missing. Fixture results are not vendor certification.
9. Stable Visual Studio 2026 `18.x` is a supported compatibility target under Microsoft's VSIX API-version model. Exact `1.0.52` runtime qualification passed on Community `18.8.2`; VS2022 qualification passed on Community `17.14.33`. Preview/Insiders and explicit standalone .NET 9/10 matrices are not current support claims.
10. Large 100k/200k evidence is file-backed raw-image evidence, not proof that a debuggee can safely allocate a fully decoded 100k/200k `Mat`.
11. Smart Type Mapper **Open Variable** handles the no-debug-session case, but its individual pointer-backed live-open path does not have the same installed-VSIX automation depth as the automatic fallback path.
12. The top `Inspector` button is intentionally visible only in narrow layout. Medium and wide layouts expose the Inspector panel directly; a consistent always-present toggle would be a separate approved UX change, not a release-fix requirement.

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy/arbitrary `IEnumerable` sequences are intentionally not enumerated while the debugger is paused.
- `.raw` or `.bin` without a descriptor cannot be interpreted safely; use `.rbuf.json` metadata.
- Live process-memory sources can be read only while the debuggee is paused and the source memory is valid. Continue, disposal, or process exit can make them unavailable.
- Planar, YUV, compressed, signed, and unsupported packed camera formats fail visibly instead of being guessed.
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
- The new automatic path is proven with simulated company frames and IDS peak assembly metadata only, not live industrial-camera SDK objects. Padded rows, extra payload, and `Buffer`/`ImageData` offsets now fail closed.
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
- Typed collections require open generic `List<>`/`Dictionary<,>` registration. Visual Studio's built-in `IEnumerable Visualizer` may also appear and is not this product.
- A local registry repair can make a broken VSIX look healthy. Normal install must never write registration, and a clean-PC release gate must pass without `Repair-VisualStudioExtensionRegistration.ps1`.
- Marketplace rejects an already published version. Every uploaded binary change needs a higher VSIX version, but documentation-only public copy can be edited separately when the portal permits it.

## Do-Not-Regress Checklist

- One installable VSIX, not two user-installed extensions.
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
- Smart Type Mapper `Map This Type` remains available only for error rows that carry a member inventory; saving a mapping writes the expected JSON and does not corrupt existing mappings.
- Open Variable remains disabled or shows a clear message when there is no active debug session in break mode.
- Automatic scanning remains deferred until the debugger reaches stable Break Mode and does not block the debugger transition event.
- Repeated Break/Scan Now refreshes automatic rows without duplicating them or deleting manually opened rows.
- Low-confidence objects stay hidden, ambiguous candidates remain editable, and automatic rows expose confidence/member/validation details.
- Exact initialized OpenCvSharp/Emgu Mats remain eligible for automatic opening; Bitmap remains available through its registered debugger-visualizer glyph.
- Ready must be claimed exactly once as Processing, and success requires explicit ACK after document open. NACK retains a reason.
- Delayed cleanup removes only ACK/NACK/conflict terminal artifacts and never deletes Ready/Processing on timeout.
- `Menus.ctmenu, 2` and exactly one instance of each Raw Buffer Visualizer View command remain in the generated package.
- Fit preserves aspect ratio with the whole image visible; wheel/pan/1:1 switch to Manual and preserve the user's zoom/center.

## Next Priorities

1. Validate public `1.0.50 -> 1.0.52` on a separate PC | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`

   Prerequisite: a separate serviced VS2022 `17.14+` or stable VS2026 PC that currently runs the exact public `1.0.50.0` package. Install the unchanged candidate SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F` as an update without uninstall, repair, `/ResetSkipPkgs`, or manual registration changes; restart and repeat ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, and protocol checks. Do not spend model tokens on repeated local reinstall testing when this prerequisite is unavailable.

2. Publish exact `1.0.52` to Marketplace | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`

   Prerequisite: priority 1 passes and Marketplace publisher credentials/environment approval are available. Upload the unchanged D-drive candidate, use `docs/marketplace-overview-1.0.52.md` plus `docs/marketplace-release-notes-1.0.52.md`, and confirm the Gallery API and rendered Overview after propagation. Never upload preserved `1.0.51`.

3. Complete the industrial camera release-qualification matrix | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

   Prerequisite: current Basler pylon, Spinnaker, and Vimba X SDK installations or legal qualification machines, representative camera/emulator objects, and lifetime rules. Follow `docs/industrial-camera-compatibility-validation.md`; do not make vendor support claims from fixtures alone.

4. Decide whether to keep an Inspector toggle visible at every width | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

   Current evidence proves the button/panel switch is responsive behavior, not intermittent registration. Any change requires a written UI description, a text mockup, and explicit owner approval before implementation.

Owner decision recorded 2026-07-26: the ImagePtr provider/public-contract mismatch (former priority 1) is intentionally left unchanged. The exact `Cressem.ImageModel.ImagePtr` registration is company-specific support and stays as is; the broader public wording question is deferred.

## Exact Start For The Next Conversation

Run the repository orientation commands, then verify both immutable artifact identities:

```powershell
Set-Location C:\Git\RawBufferVisualizer
git status --short
git log --oneline -5
git branch --show-current

$failed = 'C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
$candidate = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
Get-Item -LiteralPath $failed, $candidate | Select-Object FullName, Length
Get-FileHash -LiteralPath $failed, $candidate -Algorithm SHA256
```

Expected:

- failed `1.0.51`: 2,011,587 bytes, `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`;
- current `1.0.52`: 1,902,513 bytes, `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`.

Do not rebuild or overwrite either file during exact-package qualification. Read `docs/release-qualification-1.0.52.md`, then obtain the external prerequisite: a separate serviced VS2022 `17.14+` or stable VS2026 PC currently running exact public `1.0.50.0`. Update that profile to the unchanged candidate without uninstall, repair, `/ResetSkipPkgs`, or manual registration repair. Restart and repeat ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, and protocol checks. Store evidence on that machine's D-drive test root where available.

Only after the external update passes should Marketplace publication proceed. Use the prepared `1.0.52` Overview/release notes and the existing dry-run manifest. Confirm the public Gallery API reports `1.0.52.0` and the rendered Overview has updated after propagation. The source checkpoint is commit `854cb67`; tag, GitHub Release, and Marketplace publication still require explicit user instruction/credentials.

## Current Release Artifact

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Current exact local candidate (VS2022/VS2026 qualified; external update gate pending):

```text
Version: 1.0.52.0
Size: 1,902,513 bytes
SHA256: 3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F
Source: commit 854cb67
```

This artifact passed installed `ReleaseAnnouncement`, `AutomaticCollections`, and `MultiLibraryHybrid` on VS2022 `17.14.33` and VS2026 `18.8.2`. Rebuilding changes the VSIX hash and invalidates the exact record. Do not upload until the separate-PC public `1.0.50 -> 1.0.52` update gate passes. The repository publish path intentionally remains occupied by the byte-preserved failed `1.0.51`; do not overwrite or publish it. The last Marketplace-published binary is `1.0.50.0`.

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

## Current Task Closure

Status: Blocked
Scope: Local `1.0.52` implementation, packaging, ToolWindow ownership/lease hardening, VS2022/VS2026 installed-runtime qualification, and Marketplace metadata preparation
Acceptance criteria: All local build, test, package, registration, and runtime criteria passed; the required separate-PC public `1.0.50 -> 1.0.52` update criterion is waiting on an external eligible machine
Verification: Exact artifact/hash and payload inspection; Release build; aggregate self-tests; release communication; VS2022 `17.14.33` and VS2026 `18.8.2` three-scenario runtime matrix; registration audit; Marketplace dry run; public Gallery query; `git diff --check`; source checkpoint commit `854cb67` and `origin/main` push verification
Evidence: `docs/release-qualification-1.0.52.md`, exact VSIX SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`, and `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52`
Boundary / next dependency: A separate serviced VS2022 `17.14+` or stable VS2026 PC currently running exact public `1.0.50.0`, followed by an unchanged-candidate update and runtime matrix without uninstall, repair, or `/ResetSkipPkgs`; Marketplace publisher credentials/approval are needed only after that gate passes

# Raw Buffer Visualizer Maintainer Handoff

This is the canonical continuation document for the next conversation. Read it after `AGENTS.md` and `docs/README.md`.

## Snapshot

| Item | Verified state |
| --- | --- |
| Last verified | 2026-07-28 KST |
| Canonical repository | `C:\Git\RawBufferVisualizer` |
| Branch / remote | `main` / `https://github.com/Noah8218/RawBufferVisualizer.git` |
| Implementation baseline | `5b68cb3` (`Improve automatic inspector breakpoint workflow`, pushed) plus the current `1.0.47` release-qualification change on `main` |
| Source and VSIX version | `1.0.47` / `1.0.47.0`; published Marketplace line remains `1.0.45.0` |
| Public Marketplace version | `1.0.45.0`; the public Overview was re-fetched on 2026-07-26 KST and now matches the local `1.0.45` copy, including the Large Image Performance section |
| Git tags / GitHub Releases | Local annotated tag `v1.0.45` on `a23d8ad` created 2026-07-26; not pushed yet; no GitHub Release yet |
| Product stage | Public Marketplace Preview; published line 1.0.45, locally qualified line 1.0.47. Automatic Vision Inspector, Smart Type Mapper fallback, and Buffer Doctor are release-qualified on the local VS2022 matrix; general vendor/hardware compatibility is not certified |

Public links:

- Marketplace: https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer
- Repository: https://github.com/Noah8218/RawBufferVisualizer
- CI evidence: https://github.com/Noah8218/RawBufferVisualizer/actions/runs/29512606667

The public Marketplace package is `1.0.45.0`. Its Overview was re-fetched on 2026-07-26 and matches the local 1.0.45 copy, including the performance comparison section. Treat Marketplace binary publication and Marketplace copy synchronization as separate checks.

## Product Identity

Raw Buffer Visualizer is an Image Watch-style debugger visualizer for C# machine-vision developers. Its primary product surface is one docked Visual Studio 2022 window where inspected images accumulate and can be examined without adding temporary save or conversion code to the debuggee.

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
- Mouse-wheel zoom, drag pan, Fit, 1:1, linked views, and stable first-load Fit behavior.
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
- Root arrays/collections stay with the existing registered collection visualizers, and arbitrary SDK methods, dynamic vendor DLL loading, private native-layout decoding, and unbounded graph traversal remain excluded.
- Deterministic self-tests cover direct pointer inference, one-level nesting, ambiguous pixel format, low-confidence hiding, nested mapping extraction, `SizeX`/buffer-length role separation, and preference persistence/fallback.
- Full behavior, ownership, limits, and evidence are recorded in `docs/automatic-vision-inspector.md`.

### Release and documentation

- Public README, Marketplace checklist/Overview source, install/update/repair guidance, release runbook, release notes, demo media, license, and third-party notices exist.
- CI, Marketplace CD, and GitHub Release workflows exist.
- `1.0.47.0` Release VSIX was generated, passed the packaging compatibility guard, reinstalled, and passed the final hybrid registered/automatic smoke.
- Public Marketplace remains `1.0.45.0`; current source and locally installed qualification VSIX are `1.0.47.0`.

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
| Buffer Doctor tests | 8 deterministic self-tests passed on the Core candidate generator/scorer (padded stride, diagonal shear, endianness, valid-bits, trailing-row fit, sampling cap, ambiguity group). |
| Smart Type Mapper tests | 6 deterministic self-tests passed on mapping store, ObjectSource extraction, enum mapping, and failure inventory. |
| Buffer Doctor UI smoke | `SmokeBufferDoctorPanel.ps1` passed: top candidate for a 2448x2048 padded Mono8 buffer is the correct `stride 2560` descriptor; applying it restores the image. Captures: `artifacts/ui/buffer-doctor/2026-07-26/`. |
| Smart Type Mapper UI smoke | `SmokeSmartTypeMapper.ps1` passed: error row shows `Map This Type`, dialog preselects members, live-memory preview renders, enum mapping saves to `%APPDATA%\RawBufferVisualizer\type-mappings.json`. Captures: `artifacts/ui/smart-type-mapper/2026-07-26/`. |
| Docked layout width smoke | `SmokeDockedLayoutWidths.ps1` passed at 540/900/1160 px after UI changes. |
| Installed VSIX Buffer Doctor | VS2022 17.14 installed-VSIX automation passed on 2026-07-27: five candidates appeared, the corrected interpretation was applied, and pixel inspection reported `GV 204`. Evidence: `artifacts/ui/installed-vsix-new-features/BufferDoctor-installed-vsix.json`. |
| Automatic Vision Inspector tests | Five deterministic self-tests passed for confidence gates, direct/nested inference, ambiguous format handling, and nested mapping extraction. |
| Automatic Vision Inspector layout | `SmokeAutomaticVisionInspectorLayout.ps1` and the focused inspector-panel smoke passed at 540/900/1160 px. Evidence: `artifacts/ui/automatic-vision-inspector/2026-07-27/`. |
| Installed VSIX Automatic Vision Inspector | VS2022 17.14 automation passed on 2026-07-28: a function argument plus five locals opened, one incomplete shape remained `[Map]`, and one null-pointer shape remained `[Failed]`; the six successful images stayed usable and repeated **Scan Now** did not duplicate rows. A second VS session restored the disabled preference, manual **Scan Now** still passed, and the pre-test user setting was restored. Evidence: `artifacts/ui/automatic-inspector-workflow/2026-07-28-final/`. |
| Installed VSIX Smart Type Mapper fallback | VS2022 17.14 automation passed on 2026-07-28: an unregistered `UnmappedCompanyFrame` remained an 88% `MappingRequired` candidate, `Mono12PackedLsb` rendered from live debuggee memory, Save wrote the inferred roles/value mapping, and automatic rescan reopened it as 640 x 484, stride 960, live source with zero final errors. The pre-existing user mapping was restored. Evidence: `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json` and the three `smart-type-mapper-*.png` captures. |
| Installed VSIX registered/automatic hybrid | Real OpenCvSharp, Emgu CV, and Bitmap values opened through registered visualizers; `RawBufferSnapshot`/`RawBufferView` and all registered types were absent from automatic rows; six camera-shape fixtures opened automatically. Final state: nine images, zero errors. Evidence: `artifacts/ui/installed-vsix-new-features/MultiLibraryHybrid-installed-vsix.json`. |
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

1. Marketplace still serves `1.0.45.0`; the locally qualified `1.0.47.0` VSIX and reviewed Overview/release notes have not been published.
2. A manual Marketplace update/restart smoke on a separate PC that previously had `1.0.45.0` is not recorded. Local reinstall and CI do not replace this check.
3. Local tag `v1.0.45` exists on `a23d8ad` but is not pushed; there are no GitHub Releases. Release bookkeeping should follow the actual `1.0.47` publication decision instead of presenting the historical draft as current.
4. Marketplace CD exists, but PAT/publisher/environment approval and an actual automated publish run are not proven. Manual upload remains the known working release path.
5. The Release build still reports 18 `VSTHRD010` warnings in `ImageTypeRecognizer.cs` for EnvDTE access. Installed-VSIX behavior is proven, but the warnings remain technical debt and must not be described as zero-warning output.
6. Vendor-specific SDK adapters are not implemented. `RawBufferView` and safe structural discovery are the generic supported answers; the exact ImagePtr registration remains a compatibility exception.
7. Basler pylon, Spinnaker, and Vimba X assemblies/live objects, drivers, emulators, cameras, and representative lifetime cases are missing. Fixture results are not vendor certification.
8. Visual Studio 18/2026 and explicit .NET 9/10 runtime matrices are not current support claims. The Marketplace manifest targets Visual Studio 2022 `[17.9,18.0)` and the modern standalone target is .NET 8.
9. Large 100k/200k evidence is file-backed raw-image evidence, not proof that a debuggee can safely allocate a fully decoded 100k/200k `Mat`.
10. Smart Type Mapper **Open Variable** handles the no-debug-session case, but its individual pointer-backed live-open path does not have the same installed-VSIX automation depth as the automatic fallback path.
11. The top `Inspector` button is intentionally visible only in narrow layout. Medium and wide layouts expose the Inspector panel directly; a consistent always-present toggle would be a separate approved UX change, not a release-fix requirement.

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
- Automatic Vision Inspector scans the selected stack frame's Locals and Arguments only. It does not scan other frames/threads, fields outside the bounded root/one-level inventory, or arbitrary collection contents.
- Managed-array extraction prefers VSSDK child enumeration. Its EnvDTE fallback is capped at 256 elements, so a debugger engine that does not expose array children may require the existing collection path or a pointer-backed view.
- Automatic confidence is structural evidence, not semantic proof. A plausible but wrong shape can still require **Edit Mapping**, and format ambiguities remain user decisions.
- The new automatic path is proven with simulated company frames and IDS peak assembly metadata only, not live industrial-camera SDK objects. Padded rows, extra payload, and `Buffer`/`ImageData` offsets now fail closed.

## Incident Lessons To Preserve

- A compressed TIFF file size is not the decoded `Mat` size. For example, a roughly 61 MB TIFF can decode to several GB when its dimensions are about 31800 x 96800 Mono8. The debuggee must still own that decoded memory even when the viewer uses preview-first/direct reads.
- Do not diagnose every large-image failure as a viewer memory leak. Check decoded dimensions, stride, debuggee bitness, OpenCV pixel limit, process lifetime, and whether repeated source Mats are still alive.
- Direct process-memory viewing is intentionally live. After Continue or process exit, show a controlled unavailable state; do not attempt to keep reading an invalid pointer.
- Standalone viewer speed does not prove docked Visual Studio speed. Performance acceptance must use the installed VSIX, the docked window, and real wheel/drag input.
- Provider registration controls whether the visualizer icon appears. Reflection conversion code alone is not enough; exact/legacy type and assembly registrations must remain in the generated extension metadata.
- Typed collections require open generic `List<>`/`Dictionary<,>` registration. Visual Studio's built-in `IEnumerable Visualizer` may also appear and is not this product.
- VSIX update problems often come from stale Visual Studio package registration or a Visual Studio process that was still running. Close all instances, reinstall, restart, and use the repair script before changing working code.
- Marketplace rejects an already published version. Every uploaded binary change needs a higher VSIX version, but documentation-only public copy can be edited separately when the portal permits it.

## Do-Not-Regress Checklist

- One installable VSIX, not two user-installed extensions.
- One docked viewer session, not one window per image.
- Bitmap, OpenCvSharp, Emgu, raw, registered pointer target, list, dictionary, and array icons/opens.
- Error rows remain visible and selecting a valid row recovers normal viewing.
- Mouse hover updates continuously when unpinned; pin freezes marker/current inspection as designed.
- First image and newly selected images start with a correct Fit view.
- Wheel zoom and drag pan remain responsive in the docked window.
- Pixel/GV/channels/raw bytes, selection, Save, Delete, and Clear remain functional.
- Delete/Clear dispose sources and temporary storage.
- Narrow docking retains image list, viewer, status strip, Save, and Inspector access.
- Rendering technology names stay out of Marketplace/README/UI copy.
- README/Marketplace images pass the visual review gate in `AGENTS.md`.
- Buffer Doctor candidate panel remains usable in narrow/medium/wide layouts and selecting a candidate applies the descriptor without a debugger round-trip.
- Smart Type Mapper `Map This Type` remains available only for error rows that carry a member inventory; saving a mapping writes the expected JSON and does not corrupt existing mappings.
- Open Variable remains disabled or shows a clear message when there is no active debug session in break mode.
- Automatic scanning remains deferred until the debugger reaches stable Break Mode and does not block the debugger transition event.
- Repeated Break/Scan Now refreshes automatic rows without duplicating them or deleting manually opened rows.
- Low-confidence objects stay hidden, ambiguous candidates remain editable, and automatic rows expose confidence/member/validation details.

## Next Priorities

1. Publish the qualified 1.0.47 package and listing | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`

   Prerequisite: Marketplace publisher access and the owner's explicit publication action. Upload the artifact recorded below, paste `docs/marketplace-overview-1.0.47.md` and `docs/marketplace-release-notes-1.0.47.md`, then verify the public version and copy.

2. Post-publication smoke on a separate PC | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

   Prerequisite: a VS2022 machine with the prior Marketplace version installed. Test update, restart, registered Bitmap/Mat open, Automatic Inspector discovery, and absence of package-load popups.

3. Complete the industrial camera release-qualification matrix | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

   Prerequisite: current Basler pylon, Spinnaker, and Vimba X SDK installations or legal qualification machines, representative camera/emulator objects, and lifetime rules. Follow `docs/industrial-camera-compatibility-validation.md`; do not make vendor support claims from fixtures alone.

4. Decide whether to keep an Inspector toggle visible at every width | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

   Current evidence proves the button/panel switch is responsive behavior, not intermittent registration. Any change requires a written UI description, a text mockup, and explicit owner approval before implementation.

Owner decision recorded 2026-07-26: the ImagePtr provider/public-contract mismatch (former priority 1) is intentionally left unchanged. The exact `Cressem.ImageModel.ImagePtr` registration is company-specific support and stays as is; the broader public wording question is deferred.

## Exact Start For The Next Conversation

Run:

```powershell
Set-Location C:\Git\RawBufferVisualizer
git status --short
git log --oneline -5
git branch --show-current
```

Then verify source version:

```powershell
Select-String -Path .\src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest -Pattern 'Identity Id='
```

Before source edits, read:

```text
AGENTS.md
docs/README.md
docs/MAINTAINER_HANDOFF.md
docs/PRODUCT_DIRECTION_AND_ROADMAP.md
docs/ARCHITECTURE_AND_VALIDATION.md
docs/automatic-vision-inspector.md
```

Start by checking whether Marketplace now serves `1.0.47.0`. If not, the next external gate is publication using the qualified artifact and copy. An industrial SDK claim requires a real object, version, lifetime rule, and legal test sample. Do not reopen the completed Automatic Vision Inspector, Smart Type Mapper fallback, Buffer Doctor, or local 1.0.47 qualification scope unless its recorded acceptance criteria no longer pass. Any provider/source fix requires a new VSIX version and the installed-extension matrix.

## Current Release Artifact

```text
C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Recorded properties (1.0.47 source, rebuilt, packaged, reinstalled, and hybrid-smoked locally 2026-07-28):

```text
Version: 1.0.47.0
Size: 1,924,125 bytes
SHA256: DAB2CE62007F77F11CFF828AF02EF2F2DAE26A3BB3238CF251679E4A69505174
```

This artifact is local/generated. Rebuild it after any source, packaging, dependency, or version change; do not assume the old artifact matches a new commit. The last Marketplace-published binary remains `1.0.45.0`.

## Build Environment Note (2026-07-26)

The Kimi/embedded Git Bash environment on this machine starts without standard Windows variables (`ProgramFiles`, `ProgramFiles(x86)`, `ProgramData`, `ComSpec`, `SystemRoot`, ...). Symptoms: NuGet restore fails with `Value cannot be null. (Parameter 'path1')`, `ProcessStartInfo.EnvironmentVariables` returns null in smoke scripts, and `VisualStudioPublicAssemblies` auto-detection fails. Plain `dotnet build/restore/test` works when the variables are prefixed via `env "VAR=..."`; PowerShell smoke scripts should be launched through `.tmp\Invoke-WithFullEnvironment.ps1`, which rebuilds a complete process environment from the registry before invoking the target script.

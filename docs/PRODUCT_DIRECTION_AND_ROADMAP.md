# Product Direction And Roadmap

## Product Definition

Raw Buffer Visualizer is a Visual Studio debugger tool for C# machine-vision development. It lets a developer inspect image variables at a breakpoint in the same way Image Watch supports native vision workflows, while adding raw-memory and industrial-image diagnostics that a basic Mat viewer usually does not provide.

The product promise is:

> Inspect Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, supported image collections, and user-mapped application buffer objects directly inside one docked Visual Studio window.

Registered debugger visualizers remain available. Automatic Vision Inspector also opens initialized exact OpenCvSharp and Emgu CV Mats through a validated live-memory path, while safe unregistered company-frame shapes use structural discovery and Smart Type Mapper only when inference is ambiguous. Bitmap remains on its registered visualizer path. The workflow should remove debug-only code such as temporary image saves, `ImShow`, conversion helpers, and ad hoc pointer dumps.

## Target Users And Problems

Primary users:

- C# machine-vision and inspection-software developers;
- OpenCvSharp or Emgu users who need to inspect intermediate Mats;
- developers integrating industrial cameras and frame grabbers through unmanaged buffers;
- developers debugging stride, channel order, valid bits, byte order, packed mono, or Bayer interpretation;
- developers working with images too large for a full decoded display bitmap.

Primary problems:

- an image variable is alive only while stopped at a breakpoint;
- the developer cannot tell whether the defect is in content, stride, format, channel order, or buffer length;
- temporary save/conversion code changes timing and pollutes production code;
- multiple processing stages need to remain visible and comparable;
- large images make naive full-copy/full-frame rendering slow or unstable.

## Core Workflow

1. Break in Visual Studio.
2. Invoke Raw Buffer Visualizer from the variable.
3. Append the value to the existing docked `Images` list.
4. Identify the image from its thumbnail, variable/title, dimensions, format, stride, and source type.
5. Zoom/pan and inspect pixel coordinate, GV/channels, raw bytes, and neighborhood.
6. Use diagnostics/Try interpretation when descriptor metadata is wrong or incomplete.
7. Compare before/after images through linked views, split, diff, or blink.
8. Export only when evidence must be shared or retained.

When an unregistered camera or board object is structurally image-like but ambiguous, **Connect Your Buffer** is the supported recovery workflow: review suggested member roles, explicitly Preview, Save Mapping for reuse, or optionally copy a vendor-neutral `RawBufferView` starter template. Opening, resetting, or copying from the dialog must not scan the frame, append an image, or persist a mapping.

The image list and viewer are the primary UI. Diagnostics are supporting UI and must not crowd out the image.

## Product Principles

### Visual Studio first

The docked Visual Studio workflow is the product. The standalone WPF viewer supports saved snapshots, sample generation, screenshots, and isolated viewer tests.

### One session, one window

Every invocation appends to one docked session. Do not reopen independent viewer windows for each variable.

### Raw-buffer diagnosis is the differentiator

Keep width/height/stride/buffer-length validation, valid bits, byte order, channel order, packed formats, raw bytes, and Try interpretation first-class. A plain image preview is not enough.

### Fail visibly

Unsupported, null, disposed, malformed, or unavailable values remain visible as error rows with a reason and shareable report. Never silently drop a failed entry.

### Large-image work must be viewport-bounded

Do not decode, upload, or allocate a complete display frame when the viewport needs only a subset. Preserve preview-first handoff, random access, tiling, progressive requests, and bounded caches.

### Compatibility without dependency collisions

Use reflection and debugger-side shape extraction for OpenCvSharp/Emgu and pointer shapes. Do not force the debuggee to load the extension's library version.

### Compact IDE UX

Keep the top toolbar small. Put advanced controls in Inspector, tabs, context menus, or responsive regions. Narrow docking must retain the essential workflow.

### Evidence before claims

Performance, compatibility, and large-image claims must name the test shape and environment. Do not turn single-PC regression measurements into universal guarantees.

## UX Contract

Essential visible controls:

- `Open`, `Clear`, `Save`, `Fit`, `1:1`, `Inspector`;
- `Link Views` only when comparison context makes it useful;
- image list, viewer, zoom/status, and pixel value status.

Responsive behavior:

| Width | Required behavior |
| --- | --- |
| Narrow | Image list + viewer remain dominant; Inspector is collapsed behind a clear toggle; Save and status remain reachable. |
| Medium | Bottom/tab Inspector is available without reducing the viewer to an unusable area. |
| Wide | Descriptor and fuller Inspector can be shown beside the viewer. |

Interaction rules:

- wheel zoom is centered predictably and drag pan follows the pointer;
- a newly loaded/selected image, the Fit command, and viewer double-click enter sticky Fit mode;
- wheel zoom, pan, and 1:1 enter Manual mode;
- Fit remains aspect-correct after docking or resize, while Manual resize preserves zoom and image center;
- hover updates coordinate/value/selection while unpinned;
- pinning freezes the intended inspection point and its corresponding detail panels;
- high-zoom overlay values remain readable;
- descriptor data is read-only;
- selected-row Delete and Clear release owned resources;
- no ROI feature is prioritized unless it is usable in the docked workflow and visibly identifies its region.

## Current Product Stage

The product is a public Marketplace extension with a mature core workflow and broad regression coverage. It is not an experimental prototype, but it is still in stability-hardening rather than a final compatibility guarantee.

Current strengths:

- supported individual and collection inputs are integrated into one docked workflow;
- large-image transfer/rendering no longer depends on a full display bitmap;
- compatibility matrices cover old and current OpenCvSharp/Emgu points;
- pixel/raw diagnostics, comparison, export, error reporting, and cleanup are implemented;
- repeatable local/CI/release scripts exist.

Current maturity gaps:

- the public ImagePtr-style claim is broader than the current exact `Cressem.ImageModel.ImagePtr` provider registration;
- public-update smoke evidence and Marketplace copy must be kept synchronized per release;
- GitHub tags/releases are not yet used;
- direct vendor adapters are release-blocked until the vendor SDK license, developer eligibility, hardware restrictions, distribution conditions, and compatibility wording are cleared in writing;
- real long-running team usage is still the best source for remaining leaks, package-load edge cases, and unsupported type reports.

## Scope

### In scope now

- debugger visualizers for current supported inputs and collections;
- docked image-list workflow;
- raw descriptor/pixel diagnosis;
- large-image responsiveness and bounded memory/storage;
- compatibility, packaging, update, repair, diagnostics, and support evidence;
- comparison features that help before/after inspection.
- vendor-neutral `RawBufferView`/`RawBufferSnapshot` integration for buffers supplied by the user's application.
- user-controlled Connect Your Buffer mappings for debugger-visible pointer or managed-array objects, with explicit preview and reusable per-user or solution-local storage.

### Later, only with evidence

- focused 2D vendor adapters only after the [vendor SDK license policy](vendor-sdk-license-policy.md) passes and the exact type, memory layout, and lifetime are tested without adding a vendor runtime dependency to the VSIX;
- additional industrial formats such as signed, planar, YUV, or packed Bayer variants;
- broader Visual Studio/runtime support matrices;
- richer comparison operators when a real workflow exposes a gap;
- configurable cache/storage limits when measurements show the current defaults are insufficient.

### Out of scope

- 3D point clouds, depth/coordinate containers, and 3D camera visualization;
- Vision Replay Debugger;
- camera discovery, grabbing, trigger, exposure, lighting, PLC, and I/O control;
- production acquisition pipelines;
- recipe authoring/execution;
- labeling/annotation software;
- a general-purpose image editor;
- requiring users to install a second viewer extension or a rendering runtime separately.

## Roadmap

### Current checkpoint: 2.0.6 immutable package successor

Official Gallery API readback on 2026-09-03 confirms public `2.0.5.0` as the Marketplace baseline. It includes the **Clear all** placement, direct bounded `ConcurrentDictionary<TKey,TValue>` visualization, first exact `Cressem.ImageModel.ImagePtr` cold handoff, exact debugger expression names, native `Ptr` versus pixel-address provenance, complete image-array registration, fail-closed pointer reads, and negative-stride Bitmap correction. Local `2.0.6.0` advances only the immutable package/release identity and does not change that product contract.

The frozen 2.0.6 container passed in-place update and installed runtime checks on VS2022 `17.14.37516.0` and VS2026 `18.8.12105.206`, plus a clean reinstall on VS2022. Installed payload equality was 71/71 files on both hosts and the runtime matrix passed 15/15 records. Exact runtime on VS2022 17.9 remains unverified because that host is not installed. Owner final review of the exact VSIX and English/Korean Overview is the next priority. Commit, push, Marketplace upload, public readback, tag, and GitHub Release remain separate approvals.

### Version transition: public 1.0.53 to separate 2.0

`1.0.53` is the published 1.x stabilization bridge, not a substitute for the 2.0 product line. It delivers the qualified Environment, panel-toggle, bounded-preview, and vendor-neutral Connect Your Buffer foundation without claiming a breaking platform transition.

The exact public `1.0.53` VSIX was not relabeled or overwritten. After explicit owner approval, a separately versioned `2.0.0.0` candidate was built and qualified on both supported IDE generations. Matching pre-P0 2.0 source/evidence is on `origin/main` at `3d88894`, and CI run `30975139392` succeeded. A later P0 safety review superseded those candidate bytes: the current local candidate adds checked descriptor validation, live-source invalidation on Continue, and delayed-handoff session gating. Public 1.0.53 source/evidence commit `7ab84b7` and Marketplace readback remain the immutable 1.x baseline. See [Vendor-Neutral 2D Buffer Compatibility Matrix](vendor-neutral-buffer-compatibility-matrix.md) and [Release Qualification 2.0.0](release-qualification-2.0.0.md).

### 2.0 foundation already delivered: Connect Your Buffer

The 2.0 direction is a vendor-neutral 2D industrial buffer debugger, not a bundle of proprietary SDK adapters. The first vertical slice was delivered in public `1.0.53` by reusing Smart Type Mapper instead of adding a new pane or acquisition layer. Do not reimplement it for the version transition.

Required workflow:

1. An ambiguous application object enters through Automatic Inspector, Open Variable, or a mapped collection error row.
2. The user sees persisted or inferred roles and may edit every selection.
3. Preview remains explicit and reads only the current stopped-process buffer.
4. Save Mapping persists the selection; reopening restores it visibly and editably.
5. Use Suggested Roles changes the visible draft only.
6. Copy RawBufferView Template copies neutral starter code only for pointer-backed data and does not modify the project or mapping store.

Acceptance gate:

- mapping save/reload/reopen round-trips;
- Preview, reset, copy, and dialog-open paths have no unintended scan, image-open, registration, or save side effect;
- generated code contains selected member roles and no proprietary SDK dependency or claim;
- the dialog follows the existing dark IDE visual system for normal, hover, focus, selected/open, disabled, error, and scroll states;
- direct camera/board SDKs, acquisition, device control, 3D, PLC, and I/O remain outside the slice.

### Completed: lock the 2.0 vendor-neutral buffer contract

The [Vendor-Neutral 2D Buffer Compatibility Matrix](vendor-neutral-buffer-compatibility-matrix.md) now records supported and fail-closed decisions across managed arrays and pointers, dimensions, stride/length, image offset, pixel format, valid bits, byte order, and lifetime. Repository-owned fixtures exercise Connect Your Buffer plus existing `RawBufferView`/`RawBufferSnapshot` contracts without a vendor SDK, new UI, or new abstraction.

### Completed: close the proven shared-validation gaps

1. Public Marketplace `1.0.53.0`, its package, and its installed evidence remain immutable.
2. The common transfer metadata boundary applies complete descriptor/length diagnostics to registered `RawBufferView`, mapped carriers, snapshots, and registered library adapters.
3. Incompatible `ValidBits` values fail consistently; valid `Mono16` values such as 10, 12, 14, and 16 and the fixed packed layouts remain accepted.
4. Focused neutral tests cover both entry paths without changing Preview, mapping persistence, or no-side-effect behavior.
5. No pane, SDK adapter, offset abstraction, camera acquisition/control, PLC/I/O, or 3D path was added.

### Completed release checkpoint: separate 2.0 candidate

1. Explicit owner approval was obtained before the version/package checkpoint.
2. Separately identifiable `2.0.0` candidates were frozen without overwriting preserved 1.x or superseded 2.0 artifacts.
3. The preserved P0 candidate passed package identity, installed VS2022/VS2026 Break-to-Continue invalidation, copied-array retention, shared descriptor/lifetime tests, and the ten-version OpenCvSharp/Emgu compatibility matrix.
4. The active package hash, manifest, installed assembly hashes, Overview, and release notes identify the same 2.0 product line. Publication and public readback remain a separate explicit owner action.
5. Complete P0 and Connect Doctor source/evidence was integrated to `origin/main` at `0d3ac20`; GitHub Actions run `31059894280` passed, and the VS2026 Open Variable follow-up reached `Connect Your Buffer` without saving a user mapping.

Public Marketplace `1.0.53.0` is the immutable published baseline and contains only vendor-neutral buffer paths. New direct proprietary integrations remain blocked by default.

Exit criteria:

- no package-load popup after update/restart;
- exactly one View open command and one current-frame scan command;
- debugger handoff success requires an explicit ACK and rejection surfaces its reason;
- primary individual/collection providers appear and open;
- release-facing code and copy contain no uncleared proprietary vendor adapter or support claim;
- initialized OpenCvSharp/Emgu Mats open automatically without creating duplicate rows, while Bitmap stays glyph-owned;
- Fit and Manual mode behavior remains stable across resize;
- pointer support claims match exact provider registrations and samples;
- docked real-mouse zoom/pan remains responsive;
- error/report/recovery, Save, Delete, Clear, and cleanup work;
- every supported and rejected matrix row has a reproducible neutral fixture and expected outcome;
- save/reload/reopen preserves visible editable roles without executing Preview or opening an image;
- public version, Overview, README, release notes, and artifact version agree before any future release.

### Current 2.0 release checkpoint

The vendor-neutral carrier/layout matrix, shared registered/mapped transfer validation, neutral executable fixtures, separately versioned `2.0.0.0` VSIX, and Connect Doctor feature are implemented. The current exact candidate is 1,923,731 bytes with SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`. It passed the consolidated local matrix on the same bytes: current-source build/self-tests, five Emgu and five OpenCvSharp versions, candidate/build/install equality, Break-to-Continue invalidation and copied-array retention, Connect Doctor, and installed regressions on VS2022 `17.14.37516.0` and stable VS2026 `18.8.12023.21`. Marketplace still serves `1.0.53.0`; the worktree is not committed or pushed, so CI, owner upload approval, publication, and readback remain separate actions. See [release-qualification-2.0.0.md](release-qualification-2.0.0.md).

### Completed priority: Connect Doctor

Connect Doctor joins the already delivered **Connect Your Buffer** mapping workflow with the existing bounded **Buffer Doctor** scorer so a developer can repair a wrong raw-buffer interpretation without leaving the mapping workflow or starting a new debugger transfer.

Shortest safe workflow:

1. The user previews an inferred or persisted mapping in Connect Your Buffer.
2. If the interpretation is wrong, **Diagnose interpretation** runs Buffer Doctor against the same bounded source and current descriptor draft.
3. Ranked candidates show stride, pixel format, valid bits, byte order, score, and ambiguity. Selecting one changes only the visible draft and preview.
4. The mapping is persisted only when the user explicitly selects **Save Mapping**. Cancel, reset, reopen, and restored settings do not preview, scan, open an image, or save.

Implementation constraints:

- reuse the current mapping dialog, Buffer Doctor result model, bounded sampling caps, and semantic theme roles; do not add another tool window or diagnosis service abstraction;
- preserve ties for RGB/BGR and Bayer phase rather than claiming semantic detection;
- keep source lifetime explicit and never rerun a live read after Continue;
- persist only the selected interpretation fields at the current mapping scope, keep them visible/editable on reopen, and provide an explicit reset;
- add no vendor SDK dependency, vendor-named mapping, acquisition, device control, board control, or 3D path.

Acceptance gate:

- packed Mono12 and current-geometry format alternatives remain selectable in the visible ranked set -> pass;
- ambiguous ties remain explicit and require user choice -> pass through existing Core ambiguity tests and visible row/status contracts;
- apply/reset/cancel/save/reload/reopen round trips have no unintended save or image-list side effect -> pass;
- a missing/unavailable paused source produces a controlled unavailable state -> pass;
- the existing dark theme passes normal, checked, selected, hover/focus, disabled, popup, scroll, and open-result checks with fresh before/after evidence -> pass;
- focused tests prove Core owns candidate generation/scoring while the mapping dialog owns draft/persistence decisions -> pass.

Installed result: both supported IDE generations selected `Mono12PackedLsb 640 x 484 stride 960`, rendered candidate preview without saving, closed the result on the second toggle selection, saved explicitly, and automatically reopened the mapped live row with zero final errors. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-doctor-20260806`.

### Completed priority: consolidate and requalify the exact 2.0 package

Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

The consolidated candidate passed the P0 release matrix against one exact byte set: full Release build/self-tests, release communication/package guards, ten-version OpenCvSharp/Emgu compatibility, candidate/build/install equality, installed Break-to-Continue invalidation and copied-array retention on both IDEs, and the Connect Doctor scenario. Matching source commit `0d3ac20` is pushed, and CI run `31059894280` succeeded.

### Completed priority: pre-publish clean and in-place update gate

Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

The exact qualified candidate passed a clean install and an exact public `1.0.53.0 -> 2.0.0.0` in-place update on serviced VS2022 `17.14.37516.0` without uninstall, repair, or skipped-package reset. Core registered/mapped, Automatic Inspector, collection, mixed-library, menu-count, package-protocol, and viewer interaction checks passed. Publication remains a separate explicit owner action.

### Next priority: owner review of the 2.0.6 candidate

Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`

Review only the installed-qualified VSIX recorded in [Release Qualification 2.0.6](release-qualification-2.0.6.md) together with the [English Overview](marketplace-overview-2.0.6.md) and [Korean review copy](marketplace-overview-2.0.6.ko.md). After explicit approval, commit/push and CI come before Marketplace upload. Do not rebuild or substitute another candidate after review; public readback, real Marketplace update, tag, and GitHub Release remain later explicit steps.

### Next: supportability and compatibility growth

1. Convert real user failures into reproducible samples in `VisualizerDebuggee` or focused smoke scripts.
2. Convert vendor SDK/runtime evidence into a compatibility claim only after both the technical gate and [vendor SDK license gate](vendor-sdk-license-policy.md) pass.
3. Add only requested formats/types with written authorization, an exact source type, assembly version, descriptor mapping, and lifetime rule.
4. Extend long-session and multi-instance regression coverage when a real failure reveals a missing assertion.
5. Evaluate Visual Studio 18 and newer .NET debuggee matrices after stable tooling is available.

Exit criteria for a new adapter/type:

- visualizer icon registration is proven;
- descriptor and byte mapping are tested;
- source lifetime is explicit;
- individual and relevant collection paths work;
- unsupported shapes fail visibly;
- no core dependency on a vendor package is introduced without approval.
- written vendor authorization and required legal review are recorded for the exact developer, purpose, SDK version, distribution model, and compatibility wording.

### Later: direct proprietary SDK integrations

There is no active vendor order or implementation target. All direct camera and frame-grabber/board work is blocked by [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md). A vendor becomes a candidate only after applicable written rights, required legal review, and explicit owner approval exist; only then is technical evidence planning justified. Hardware or an available installer never overrides license eligibility. `RawBufferView` remains the public vendor-neutral path.

## Prioritization Rule

Use this order when choosing work:

1. Public release breakage: package load, missing icon, crash, corrupted view, update failure.
2. Data correctness: wrong pixel/channel/stride/byte-order interpretation.
3. Resource safety: leaks, runaway temp storage, stale process reads.
4. Docked interaction performance: real wheel/drag latency and blank-frame regressions.
5. Core debugger UX: image accumulation, selection, pinning, errors, export, comparison.
6. Compatibility requested by a real project.
7. New features and vendor adapters.

Do not trade correctness or release stability for a broader feature list.

## Release Decision

An update is ready only when all changed surfaces have evidence:

- proprietary vendor code and compatibility wording have passed the license gate or are absent from the candidate;
- source and package build;
- focused tests for the change;
- relevant docked/installed VSIX smoke;
- version and package metadata validation;
- README/Marketplace/release notes updated if behavior or claims changed;
- current-build screenshots visually reviewed if UI or public images changed;
- known limitations stated instead of hidden.

See [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md) for the current release state and [ARCHITECTURE_AND_VALIDATION.md](ARCHITECTURE_AND_VALIDATION.md) for exact commands.

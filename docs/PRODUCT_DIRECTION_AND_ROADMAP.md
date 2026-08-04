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

The product is a public Marketplace Preview with a mature core workflow and broad regression coverage. It is not an experimental prototype, but it is still in stability-hardening rather than a final compatibility guarantee.

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

### Version transition: 1.0.53 before 2.0

`1.0.53` is the planned 1.x stabilization bridge, not a substitute for the 2.0 product line. It delivers the already-qualified Environment, panel-toggle, bounded-preview, and vendor-neutral Connect Your Buffer foundation to users of public `1.0.52` without claiming a breaking platform transition.

Do not relabel the exact qualified `1.0.53` VSIX as `2.0.0`; changing its version changes the package bytes and invalidates its installed qualification. Publish the unchanged `1.0.53` candidate first after source commit and explicit Marketplace approval. Then start the 2.0 development line with the vendor-neutral camera/frame-grabber buffer-shape matrix, corresponding onboarding/support contract, and a separately built and qualified `2.0.0` candidate.

### 2.0 first vertical slice: Connect Your Buffer

The 2.0 direction is a vendor-neutral 2D industrial buffer debugger, not a bundle of proprietary SDK adapters. Its first vertical slice reuses Smart Type Mapper instead of adding a new pane or acquisition layer.

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

### Now: stabilize 1.0.53 and resolve the vendor SDK license hold

1. Keep public Marketplace `1.0.52.0` immutable while `1.0.53.0` remains a local development candidate.
2. Keep the removed Basler experiment's emulator results classified as historical engineering evidence only; no active provider, registration, or transfer path remains.
3. Apply the same license gate to camera, frame-grabber, transport-board, and imaging-board SDKs before any direct adapter is implemented, distributed, or advertised.
4. Do not download or test Vimba X or IDS peak as an individual consumer under the currently reviewed terms, and do not use Spinnaker without owned qualifying hardware or written authorization.
5. Keep `RawBufferView` as the vendor-neutral supported path while the direct-adapter gate is blocked.
6. Continue watching team usage for repeated-open memory, temp storage, package-load, menu duplication, automatic-scan false positives, and live-source-unavailable issues.

Public Marketplace `1.0.52.0` remains the published baseline and never contained the direct Basler adapter. Source `1.0.53.0` removes the engineering experiment and retains only vendor-neutral buffer paths. New direct proprietary integrations remain blocked by default.

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
- public version, Overview, README, release notes, and artifact version agree.

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

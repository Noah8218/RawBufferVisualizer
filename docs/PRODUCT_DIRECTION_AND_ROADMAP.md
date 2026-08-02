# Product Direction And Roadmap

## Product Definition

Raw Buffer Visualizer is a Visual Studio debugger tool for C# machine-vision development. It lets a developer inspect image variables at a breakpoint in the same way Image Watch supports native vision workflows, while adding raw-memory and industrial-image diagnostics that a basic Mat viewer usually does not provide.

The product promise is:

> Inspect Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside one docked Visual Studio window.

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
- VSSDK threading warnings remain;
- GitHub tags/releases are not yet used;
- direct vendor SDK adapters and Visual Studio 18/.NET 9/10 matrices are not current commitments;
- real long-running team usage is still the best source for remaining leaks, package-load edge cases, and unsupported type reports.

## Scope

### In scope now

- debugger visualizers for current supported inputs and collections;
- docked image-list workflow;
- raw descriptor/pixel diagnosis;
- large-image responsiveness and bounded memory/storage;
- compatibility, packaging, update, repair, diagnostics, and support evidence;
- comparison features that help before/after inspection.

### Later, only with evidence

- direct adapters for a specific vendor SDK;
- additional industrial formats such as signed, planar, YUV, or packed Bayer variants;
- broader Visual Studio/runtime support matrices;
- richer comparison operators when a real workflow exposes a gap;
- configurable cache/storage limits when measurements show the current defaults are insufficient.

### Out of scope

- Vision Replay Debugger;
- camera discovery, grabbing, trigger, exposure, lighting, PLC, and I/O control;
- production acquisition pipelines;
- recipe authoring/execution;
- labeling/annotation software;
- a general-purpose image editor;
- requiring users to install a second viewer extension or a rendering runtime separately.

## Roadmap

### Now: publish and externally verify the 1.0.51 update

1. Publish the exact qualified `1.0.51` SHA without rebuilding it.
2. Update another PC from public `1.0.50` to Marketplace `1.0.51` without uninstall, repair, or `/ResetSkipPkgs`.
3. Repeat menu, ToolWindow, Automatic Inspector, automatic Mat collections, registered Bitmap, explicit handoff completion, and Fit checks on that external profile.
4. Continue watching team usage for repeated-open memory, temp storage, package-load, menu duplication, automatic-scan false positives, and live-source-unavailable issues.

Public Marketplace `1.0.50.0` is the early 2,001,513-byte baseline. The exact local `1.0.51.0` package passed an in-place update from that downloaded baseline plus installed runtime checks on Windows 10 Pro / VS2022. Marketplace publication, post-propagation verification on another PC, and live vendor hardware remain separate qualification scopes.

Exit criteria:

- no package-load popup after update/restart;
- exactly one View open command and one current-frame scan command;
- debugger handoff success requires an explicit ACK and rejection surfaces its reason;
- primary individual/collection providers appear and open;
- initialized OpenCvSharp/Emgu Mats open automatically without creating duplicate rows, while Bitmap stays glyph-owned;
- Fit and Manual mode behavior remains stable across resize;
- pointer support claims match exact provider registrations and samples;
- docked real-mouse zoom/pan remains responsive;
- error/report/recovery, Save, Delete, Clear, and cleanup work;
- public version, Overview, README, release notes, and artifact version agree.

### Next: supportability and compatibility growth

1. Convert real user failures into reproducible samples in `VisualizerDebuggee` or focused smoke scripts.
2. Convert only real vendor SDK/runtime evidence into new compatibility claims; the qualified `1.0.51` OpenCvSharp/Emgu automatic capture and Bitmap registered capture do not certify unrelated vendor SDK objects.
3. Add only requested formats/types with an exact source type, assembly version, descriptor mapping, and lifetime rule.
4. Extend long-session and multi-instance regression coverage when a real failure reveals a missing assertion.
5. Evaluate Visual Studio 18 and newer .NET debuggee matrices after stable tooling is available.

Exit criteria for a new adapter/type:

- visualizer icon registration is proven;
- descriptor and byte mapping are tested;
- source lifetime is explicit;
- individual and relevant collection paths work;
- unsupported shapes fail visibly;
- no core dependency on a vendor package is introduced without approval.

### Later: vendor-specific adapters

Candidate order remains driven by actual users and available SDK samples, not a speculative checklist:

- Euresys eGrabber;
- Teledyne DALSA Sapera LT;
- Basler pylon .NET;
- HIKROBOT MVS;
- Teledyne FLIR Spinnaker;
- Zebra Aurora/MIL.

The default solution remains a small user-side adapter that exposes `RawBufferView`. A first-party direct adapter is justified only when that path is insufficient and the SDK can be tested legally and repeatably.

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

- source and package build;
- focused tests for the change;
- relevant docked/installed VSIX smoke;
- version and package metadata validation;
- README/Marketplace/release notes updated if behavior or claims changed;
- current-build screenshots visually reviewed if UI or public images changed;
- known limitations stated instead of hidden.

See [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md) for the current release state and [ARCHITECTURE_AND_VALIDATION.md](ARCHITECTURE_AND_VALIDATION.md) for exact commands.

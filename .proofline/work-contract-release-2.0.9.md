# Raw Buffer Visualizer 2.0.9 field-reliability work contract

## User goal

Repair the failures reported against the already-public 2.0.8 release, verify the real field-sized workflows on supported Visual Studio hosts, and prepare a coherent 2.0.9 VSIX plus English/Korean release documentation for owner review.

## Current product boundary

- Product identity: an Image Watch-style C# machine-vision debugger visualizer that opens supported 2D image objects and raw buffers in one docked Visual Studio window.
- Implement now: safe ROI/native spans, reliable medium-size registered-object transfer, approved duplicate-error and dock-host behavior, focused regressions, exact installed-host verification, 2.0.9 version/package/docs.
- Review later: broader performance tuning that is not required to eliminate the reproduced failures.
- Out of scope: camera acquisition/control, PLC/I/O, lighting, 3D/depth-container features, new vendor SDK integrations, and unrelated UI redesign.

## User workflow and failure cases

1. A developer pauses on an initialized `OpenCvSharp.Mat`, Emgu CV `Mat`, `ImagePtr`, or ROI-backed image.
2. The registered debugger visualizer retrieves metadata and image bytes without exceeding the debugger evaluation window.
3. The permanent docked Raw Buffer Visualizer remains available while sequential objects are opened.
4. Valid pixels are read only from the declared image span; final-row padding after an ROI is not treated as readable image data.
5. If the same object fails repeatedly, the approved UI behavior updates one matching error row instead of flooding the list.

Failure coverage includes a source that is resumed/disposed during transfer, an unreadable or shortened native range, repeated invocation, multiple registered objects, handoff timeout, cancellation, and deterministic temporary-artifact cleanup.

## Acceptance criteria

- `OpenCvSharp.Mat`, Emgu CV `Mat`, inferred `ImagePtr`, and inferred `RawBufferView` use `stride * (height - 1) + minimumRowBytes`; explicit trustworthy buffer lengths remain authoritative and are validated.
- Exact-size right/bottom ROI regressions prove that no read is attempted after the final pixel row.
- A 6768 x 3225 Mono8 OpenCvSharp Mat, Emgu CV Mat, and ImagePtr open on exact Visual Studio 2022 17.9 without evaluation timeout.
- Sequential and repeated registered opens preserve the permanent docked tool window and complete handoff cleanup.
- After owner approval of the UI mockup, repeated identical failures update one row with repeat/recency information.
- Focused automated tests, legacy compatibility, release package checks, and the supported installed-host matrix pass for the same 2.0.9 source candidate.
- Version sources, README, changelog, architecture/support notes, Marketplace Overview, Korean review copy, and VSIX payload agree on 2.0.9.

## Verification plan

- Run focused ObjectSource/Core tests including guarded exact-span ROI cases and medium-size transfer policy tests.
- Reproduce the 2.0.8 field dimensions before the fix where practical, then run the repaired installed-VSIX flow on exact VS 17.9, VS 17.14, and stable VS 2026.
- Exercise OpenCvSharp Mat, Emgu CV Mat, ImagePtr, repeated opens, debug resume/stop, source-unavailable handling, and cleanup.
- For approved UI changes, capture fresh contextual runtime screenshots on the dynamically selected smaller left monitor and exercise supported themes and layout widths.
- Run release communication, VSIX payload/registration/hash-equality, and package dry-run checks without publishing.
- Store generated evidence under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9`.

## Approval and publication boundary

- Core memory and transport repair is authorized.
- The error-row deduplication and tool-window retention mockup has been shown; visible UI/workflow code remains gated until the owner explicitly approves that mockup.
- Local version edits, builds, tests, documentation, and review VSIX preparation are authorized.
- Commit, push, tag, GitHub release, Marketplace upload, and deployment are separate actions and are not authorized by this contract.

## Durable issue

Track claims, evidence, milestones, and closure in `.proofline/issues/PL-0002.json`.

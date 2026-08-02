# Product Concept

## Current Product State

Raw Buffer Visualizer is an Image Watch-style Visual Studio debugger extension for C# machine-vision developers.

The immediate goal is to make image variables easy to inspect regardless of whether they start as `byte[]`, `IntPtr`, `ushort[]`, `float[]`, `Bitmap`, OpenCvSharp `Mat`, or camera SDK buffers.

The current delivery order is:

1. Keep one docked Visual Studio image list reliable across registered visualizers and Automatic Vision Inspector.
2. Diagnose raw-buffer layout problems without hiding ambiguity or unsafe memory assumptions.
3. Preserve viewport-bounded behavior for large images and explicit failure rows for unsupported values.
4. Qualify each Marketplace update on real installed Visual Studio instances before expanding compatibility claims.

The standalone WPF viewer remains a support, snapshot, and test host. It is not the primary product surface.

## Product Direction

The final target is Visual Studio debugger integration, similar in workflow to Image Watch:

- Stop at a breakpoint.
- Select or invoke an image variable.
- Inspect pixels, metadata, zoom, histogram, stride, format, and diagnostics.
- Open raw buffers, `Bitmap`, `Mat`, and adapter-provided camera SDK image objects.
- Keep the standalone viewer available for snapshots and focused rendering tests without creating a second primary workflow.

## Problem

Machine-vision developers often debug image data before it has a convenient viewer type. The useful details are usually raw memory layout details:

- width and height
- stride and byte order
- packed mono formats
- Bayer layouts
- valid bit depth
- channel order
- buffer lifetime and pointer ownership

Standard debugger views do not make these details visible enough, and converting every buffer to an image type just for debugging adds friction.

## Differentiation

Existing debugger image tools mostly inspect already-known image objects.

- Microsoft Image Watch focuses on in-memory image inspection while stopped in the Visual Studio debugger.
- Visual Studio debugger visualizers can display individual managed objects.
- Vendor tools are powerful but tied to their ecosystem.

This product stays small and vendor-neutral: a lightweight C# SDK, optional adapters, one Visual Studio docked inspection surface, and a supporting standalone viewer.

## Product Shape

Main components:

- `RawBufferVisualizer.Core`: descriptors, pixel formats, validation, tile decode, diagnostics.
- `RawBufferVisualizer.Sdk`: snapshot helpers for buffers and pointers.
- `RawBufferVisualizer.VisualStudio.Extensibility`: public hybrid VSIX, registered debugger visualizers, package, and menu ownership.
- `RawBufferVisualizer.VisualStudio.Vssdk`: docked Tool Window and debugger integration.
- `RawBufferVisualizer.VisualStudio.ObjectSource`: safe object extraction, inference, and mapping.
- `RawBufferVisualizer.OpenGlCanvas`: viewport-bounded tiled display.
- `RawBufferVisualizer.Wpf`: standalone support and test viewer.
- Optional adapter packages: OpenCvSharp `Mat` and `Bitmap` without making them core dependencies.

## Current Core Contract

The product must continue to:

1. Open `.rbuf.json` plus `.raw` payload files.
2. Inspect supported mono, packed mono, color, float, and Bayer formats.
3. Open snapshots produced from `byte[]`, `IntPtr`, `ushort[]`, `float[]`, `Bitmap`, and `Mat`.
4. Show pixel values, histogram, diagnostics, zoom, and export options.
5. Handle large images without requiring one full-frame WPF bitmap.
6. Route registered and automatically recognized values into the same docked image list.
7. Keep automatic inspection bounded, non-modal, duplicate-free, and isolated per candidate.
8. Require real installed-runtime evidence before claiming a Visual Studio or vendor compatibility target.

## Visual Studio Workflow Contract

The Visual Studio integration follows these rules:

1. Registered image types use their debugger-visualizer glyph.
2. Safe unregistered current-frame shapes use Automatic Vision Inspector or Smart Type Mapper.
3. A breakpoint never opens the Tool Window or steals focus merely because automatic inspection is enabled.
4. One failed value does not hide successful values, and failures remain visible with a reason.
5. Raw pointers open only when descriptor metadata and paused-process memory access are safe enough to validate.
6. Adapters remain optional so user projects do not inherit unused dependencies.

The current implementation direction is fixed in [docs/visual-studio-integration.md](docs/visual-studio-integration.md).

## Non-Goals For Now

- Replacing HALCON, VisionPro, or OpenCV.
- Building a recipe editor.
- Running live camera acquisition.
- Controlling lighting, PLC, I/O, or industrial execution equipment.
- Adding a database server.
- Managing full inspection-run timelines.
- Claiming vendor SDK or hardware compatibility from documentation-only research.

## Technical Principles

- Vendor-neutral core: raw buffer descriptor is the boundary.
- Adapters are optional: Mat, Bitmap, HALCON, Basler, and Cognex support must not become core dependencies.
- Large images use tiled display paths.
- Metadata is JSON so it is simple to inspect, diff, and version.
- Raw payloads stay as binary files; they are not base64 in JSON.

## Source Notes

- Visual Studio Image Watch and debugger visualizers confirm that image-variable viewing is a known developer workflow.
- Basler pylon and GenICam PFNC documentation confirm that raw buffer lifetime, pixel format, stride, and packed formats are first-class concerns.
- Large-image viewers commonly use chunked or tiled display patterns to avoid full-frame UI memory pressure.

# Product Concept

## Current Priority

Raw Buffer Visualizer is an Image Watch style utility for C# machine-vision developers.

The immediate goal is to make image variables easy to inspect regardless of whether they start as `byte[]`, `IntPtr`, `ushort[]`, `float[]`, `Bitmap`, OpenCvSharp `Mat`, or camera SDK buffers.

The delivery order is:

1. Complete the standalone Windows Image Watch program.
2. Publish and package the project on GitHub.
3. Add Visual Studio integration so developers can open supported image variables while debugging.

## Final Product Direction

The final target is Visual Studio debugger integration, similar in workflow to Image Watch:

- Stop at a breakpoint.
- Select or invoke an image variable.
- Inspect pixels, metadata, zoom, histogram, stride, format, and diagnostics.
- Open raw buffers, `Bitmap`, `Mat`, and adapter-provided camera SDK image objects.
- Keep the standalone viewer as the same inspection surface used by the Visual Studio integration.

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

This product should stay small and vendor-neutral: a lightweight C# SDK, optional adapters, a standalone viewer, and later a Visual Studio entry point.

## Product Shape

Main components:

- `RawBufferVisualizer.Core`: descriptors, pixel formats, validation, tile decode, diagnostics.
- `RawBufferVisualizer.Sdk`: snapshot helpers for buffers and pointers.
- `RawBufferVisualizer.Wpf`: standalone inspection viewer.
- Adapter packages: OpenCvSharp `Mat`, `Bitmap`, and later vendor-specific objects.
- Visual Studio integration: debugger-side entry point that sends supported variables to the viewer.

## MVP

The MVP should be able to:

1. Open `.rbuf.json` plus `.raw` payload files.
2. Inspect supported mono, packed mono, color, float, and Bayer formats.
3. Open snapshots produced from `byte[]`, `IntPtr`, `ushort[]`, `float[]`, `Bitmap`, and `Mat`.
4. Show pixel values, histogram, diagnostics, zoom, and export options.
5. Handle large images without requiring one full-frame WPF bitmap.
6. Package a Windows executable and sample files through GitHub.

## Visual Studio Integration Goals

The Visual Studio integration should start small:

1. Debugger visualizer or extension entry for managed image-like objects.
2. Support `RawBufferSnapshot`, `Bitmap`, and OpenCvSharp `Mat` first.
3. Add raw pointer support only when descriptor metadata can be supplied safely.
4. Reuse the standalone viewer surface instead of building a second UI.
5. Keep adapters optional so user projects do not inherit dependencies they do not use.

## Non-Goals For Now

- Replacing HALCON, VisionPro, or OpenCV.
- Building a recipe editor.
- Running live camera acquisition.
- Adding a database server.
- Managing full inspection-run timelines.

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

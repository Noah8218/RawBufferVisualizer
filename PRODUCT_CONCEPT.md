# Product Concept

## Position

Raw Buffer Visualizer is the image engine for a larger product:

Vision Replay Debugger records one machine-vision inspection run and replays it on a developer PC with the same images, intermediate stages, parameters, ROIs, overlays, measurements, timing, and result metadata.

## Problem

Machine-vision defects are often hard to reproduce because the saved image alone is not enough. The useful context is usually spread across camera buffers, recipe values, ROI edits, PLC/camera timing, intermediate images, and result overlays.

The product should answer these questions first:

- What image entered the inspection?
- Which processing stages changed it?
- Which parameters and ROIs were active?
- Which measurement or decision failed?
- Was the issue image quality, recipe, timing, calibration, or algorithm logic?

## Differentiation

Existing debugger image tools mostly inspect one in-memory image during debugging.

- Microsoft Image Watch focuses on in-memory bitmap/image inspection while stopped in the Visual Studio debugger.
- Visual Studio debugger visualizers can display individual managed objects.
- HALCON and VisionPro provide full machine-vision environments, but they are heavy platform products.

This product should stay smaller and vendor-neutral: a lightweight C# SDK plus a standalone WPF viewer that can be attached to existing equipment applications.

## Product Shape

The first useful product is not a vision algorithm suite. It is a black box and replay viewer for C# machine-vision development.

Main components:

- `RawBufferVisualizer.Core`: descriptors, pixel formats, validation, tile decode, diagnostics.
- `RawBufferVisualizer.OpenGlCanvas`: WPF/OpenGL large-image canvas with tile and LOD rendering.
- `VisionRecorder.Sdk`: small API for recording one inspection shot.
- `VisionReplayViewer`: WPF application for browsing `.vrec` files.
- Adapter packages: OpenCvSharp Mat, Bitmap, and later vendor-specific objects.

## Recording API Sketch

```csharp
using (var shot = VisionRecorder.Begin("Cam1", triggerId, recipeName))
{
    shot.AddImage("01_Raw", buffer, width, height, stride, VisionPixelFormat.Mono8);
    shot.AddParam("ExposureUs", exposureUs);
    shot.AddParam("Threshold", threshold);
    shot.AddRoi("SearchROI", searchRect);
    shot.AddImage("02_Binary", binaryMat);
    shot.AddOverlay("03_Result", contours);
    shot.AddMeasure("Width", width);
    shot.Result(isOk);
}
```

## MVP

The MVP should be able to:

1. Record one inspection run to a `.vrec` file.
2. Store raw images using the existing `.rbuf.json` descriptor model.
3. Store params, ROIs, overlays, measurements, timing, and final OK/NG.
4. Open `.vrec` in the viewer.
5. Browse stage images in order.
6. Show ROI and overlay on top of each image.
7. Compare two `.vrec` files side by side.

## Non-Goals For Now

- Replacing HALCON, VisionPro, or OpenCV.
- Building a Visual Studio extension first.
- Supporting every camera SDK directly.
- Building a recipe editor.
- Running live camera acquisition.
- Adding a database server.

## Technical Principles

- File first: a `.vrec` should be inspectable and portable without a server.
- Vendor-neutral core: raw buffer descriptor is the boundary.
- Adapters are optional: Mat, Bitmap, HALCON, Basler, and Cognex support must not become core dependencies.
- Large images are tiled: no full-frame `BitmapSource` for large buffers.
- Metadata is JSON: simple to inspect, diff, and version.
- Image payloads are separate files inside the package: raw payloads must not be base64 in JSON.

## Source Notes

- Visual Studio Image Watch and debugger visualizers confirm that single-variable image viewing is already a known category.
- Cognex VisionPro QuickBuild and image database verification confirm demand for image replay/verification workflows.
- HALCON Variable Inspect confirms vendor tools already cover debugger-side variable inspection.
- Basler pylon and GenICam PFNC documentation confirm that raw buffer lifetime, pixel format, stride, and packed formats are first-class concerns.
- libvips and OME-Zarr show the same large-image design pattern: chunked/tiled, multiscale, on-demand access.

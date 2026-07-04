# Visual Studio Integration Plan

This document fixes the first Visual Studio integration direction for Raw Buffer Visualizer.

## Decision

Use the modern `VisualStudio.Extensibility` debugger visualizer model as the primary path.

Reasoning:

- Microsoft documents debugger visualizers as the Visual Studio feature for showing .NET variables or objects during a debug session.
- The modern model can run out of the main Visual Studio process and targets .NET, which fits the existing standalone viewer and SDK direction.
- The older VSSDK visualizer model remains a fallback only when older Visual Studio support is explicitly required.

## First Supported Workflow

The first Visual Studio prototype should support this flow:

1. The developer stops at a breakpoint.
2. The developer opens the Raw Buffer Visualizer entry from DataTip, Watch, Locals, or Autos.
3. The visualizer receives a supported image-like object from the debuggee process.
4. The visualizer writes a temporary `.rbuf.json` plus `.raw` snapshot.
5. The existing standalone viewer opens that snapshot.

This keeps the Visual Studio extension thin and reuses the viewer already built for zoom, pixel inspection, histogram, diagnostics, and export.

## Type Priority

1. `RawBufferSnapshot`
   - It is our own type and already carries both bytes and `RawImageDescriptor`.
   - It should be the first debugger visualizer target.
2. `System.Drawing.Bitmap`
   - Convert through `RawBufferVisualizer.BitmapAdapter.BitmapSnapshot.FromBitmap`.
   - Keep it optional so projects without `System.Drawing` are not forced into that dependency.
3. OpenCvSharp `Mat`
   - Convert through `RawBufferVisualizer.OpenCvSharpAdapter.MatSnapshot.FromMat`.
   - Keep it optional so projects without OpenCvSharp are not forced into that dependency.
4. Pointer and camera SDK buffers
   - Support only through a descriptor wrapper or adapter object.
   - A raw pointer alone is not enough because width, height, stride, pixel format, byte order, and ownership are required.

## Project Shape

Add these projects when implementation starts:

```text
src\RawBufferVisualizer.VisualStudio\
src\RawBufferVisualizer.VisualStudio.ObjectSource\
```

`RawBufferVisualizer.VisualStudio`:

- VS 2022 debugger visualizer entry point.
- Targets the modern Visual Studio extension model.
- Resolves the packaged standalone viewer path.
- Launches the viewer with the temporary snapshot path.

`RawBufferVisualizer.VisualStudio.ObjectSource`:

- Debuggee-side conversion layer.
- Prefer `netstandard2.0` where practical so .NET Framework and modern .NET debuggee apps can use the same object source.
- Converts supported objects into a serializable snapshot transfer shape.

## Current Prototype Status

- `RawBufferVisualizer.VisualStudio.ObjectSource` converts `RawBufferSnapshot` into `VisualizerSnapshotTransfer`.
- `RawBufferVisualizer.VisualStudio.ObjectSource` includes the Visual Studio custom object source for `RawBufferSnapshot`.
- `RawBufferVisualizer.VisualStudio` writes that transfer to a temporary `.rbuf.json` plus `.raw` snapshot.
- `RawBufferVisualizer.VisualStudio` prepares a standalone viewer launch request.
- `RawBufferVisualizer.VisualStudio.Extensibility` registers a `RawBufferSnapshot` debugger visualizer provider.
- The standalone viewer path is resolved from `RAW_BUFFER_VISUALIZER_VIEWER` or a side-by-side `RawBufferVisualizer.Wpf.exe`.
- Manual Visual Studio installation and DataTip/Watch verification are the next steps.

## Prototype Build

Build the current extension prototype:

```powershell
dotnet build .\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj -c Release
```

Create the extension prototype zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1
```

The output is:

```text
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net8.0-windows\
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net8.0-windows.zip
```

Before manual Visual Studio testing, point the extension at a built viewer:

```powershell
$env:RAW_BUFFER_VISUALIZER_VIEWER = "C:\Tools\RawBufferVisualizer\RawBufferVisualizer.Wpf.exe"
```

Manual Visual Studio testing still requires Visual Studio 2022 with the extension development workload. Open this solution in Visual Studio, set `RawBufferVisualizer.VisualStudio.Extensibility` as the startup project, press `F5`, then inspect a `RawBufferSnapshot` variable from DataTip, Watch, Locals, or Autos.

## Data Contract

The transfer shape should be intentionally small and stable:

```csharp
public sealed class VisualizerSnapshotTransfer
{
    public RawImageDescriptor Descriptor { get; set; }
    public byte[] Buffer { get; set; }
    public string? SourceType { get; set; }
    public string? DisplayName { get; set; }
}
```

The object source converts the target object into this transfer shape. The visualizer side writes it to the current `.rbuf.json` plus `.raw` format and opens the viewer.

For large images, do not rely on one oversized serialization call. Use the visualizer object source message pattern to request metadata first and buffer chunks after that.

## Version Policy

- Primary: Visual Studio 2022 17.9 or newer, using `VisualStudio.Extensibility`.
- Fallback: older VSSDK visualizer only if support for older Visual Studio versions becomes a product requirement.
- Runtime: keep the standalone viewer on `net472` plus modern .NET targets. The VS extension can use a modern target where the extension model allows it.

## Implementation Order

1. Add a small object-source project that can convert `RawBufferSnapshot` to the transfer shape.
2. Add a VS 2022 visualizer project targeting `RawBufferSnapshot`.
3. Launch the existing viewer with a temp snapshot file.
4. Add install/build notes for the extension.
5. Add `Bitmap` support.
6. Add OpenCvSharp `Mat` support.
7. Add chunked transfer for large buffers.
8. Add vendor or camera SDK adapters only after the generic descriptor-wrapper path is stable.

## Validation Checklist

- Visualizer appears for `RawBufferSnapshot` in Watch, Locals, Autos, and DataTip.
- Viewer opens from the visualizer with a generated temp snapshot.
- Pixel format, width, height, stride, valid bits, and byte order match the debuggee object.
- Mono, color, packed mono, float, and Bayer samples still render correctly after the VS path.
- Large images do not freeze Visual Studio while transferring data.
- Missing viewer executable shows a clear actionable error.
- Unsupported target types fail with a clear message, not a silent no-op.

## References Checked

- Microsoft Learn, "Create Visual Studio debugger visualizers":
  https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/debugger-visualizer/debugger-visualizers?view=visualstudio
- Microsoft Learn, "Choose the right Visual Studio extensibility model for you":
  https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/extensibility-models?view=visualstudio
- Microsoft Learn, "Custom data visualizers for the Visual Studio debugger":
  https://learn.microsoft.com/en-us/visualstudio/debugger/create-custom-visualizers-of-data?view=visualstudio
- Microsoft Learn, "Advanced visualizer scenarios":
  https://learn.microsoft.com/en-us/visualstudio/debugger/visualizer-advanced-scenarios?view=visualstudio

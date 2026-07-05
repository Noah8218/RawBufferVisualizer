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
2. `RawBufferView`
   - It is the SDK wrapper for the common industrial-camera shape: `IntPtr Buffer`, width, height, stride, pixel format, channels, bit depth, and byte order.
   - It lets camera or frame-grabber code expose unmanaged buffers without forcing an immediate full managed copy.
3. `System.Drawing.Bitmap`
   - Convert through `RawBufferVisualizer.BitmapAdapter.BitmapSnapshot.FromBitmap`.
   - Keep it optional so projects without `System.Drawing` are not forced into that dependency.
4. OpenCvSharp `Mat`
   - Convert in the Visual Studio object source from the `Mat` width, height, stride, data pointer, depth, and channel count.
   - Keep it optional so projects without OpenCvSharp are not forced into that dependency.
5. Emgu CV `Mat`
   - Convert by reflection from `Emgu.CV.Mat` properties: `Rows`, `Cols`, `Step`, `Depth`, `NumberOfChannels`, `DataPointer`, and `Dims`.
   - Keep it optional so projects without Emgu do not inherit that dependency.
6. Pointer and camera SDK buffers
   - Support only through a descriptor wrapper or adapter object.
   - A raw pointer alone is not enough because width, height, stride, pixel format, byte order, and ownership are required.

## Project Shape

The Visual Studio prototype is split into these projects:

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

- `RawBufferVisualizer.VisualStudio.ObjectSource` converts `RawBufferSnapshot`, `RawBufferView`, `System.Drawing.Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat`.
- `RawBufferVisualizer.VisualStudio.ObjectSource` includes Visual Studio custom object sources for `RawBufferSnapshot`, `RawBufferView`, `Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat`.
- `RawBufferVisualizer.VisualStudio.ObjectSource` sends snapshot metadata first, then serves raw buffer chunks on request.
- `RawBufferVisualizer.VisualStudio` writes that transfer to a temporary `.rbuf.json` plus `.raw` snapshot.
- `RawBufferVisualizer.VisualStudio` prepares a standalone viewer launch request. The request can carry multiple metadata paths so the standalone viewer opens them as separate images/tabs.
- `RawBufferVisualizer.VisualStudio.Extensibility` registers debugger visualizer providers for `RawBufferSnapshot`, `RawBufferView`, `Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat`.
- The Visual Studio launch status panel now reports success/failure, source type, dimensions, pixel format, byte length, and the generated metadata path.
- The standalone viewer path is resolved from `RAW_BUFFER_VISUALIZER_VIEWER` or a side-by-side `RawBufferVisualizer.Wpf.exe`.
- Manual Visual Studio installation and DataTip/Watch verification are documented in [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md).

## Viewer Session Strategy

The current session handoff is process-per-launch:

- The extension writes one `.rbuf.json` plus `.raw` pair per inspected image.
- The extension launches the standalone viewer with one or more metadata paths as command-line arguments.
- The viewer opens those paths into the `Images` list and tabs.

This is intentionally simpler than IPC. Reusing an already-running viewer through named pipes, local RPC, or a localhost endpoint should be added only when there is evidence that repeated process startup is hurting the debugger workflow.

Trigger criteria for process reuse:

- repeated visualizer opens feel slow in real Visual Studio debugging sessions
- users need to append new debug images into an already-arranged compare session
- large shared context should survive between inspections

## Prototype Build

Recommended local install/update command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Reinstall
```

This builds the `net472` viewer, publishes the `net8.0-windows` VSIX, sets the user-level `RAW_BUFFER_VISUALIZER_VIEWER` environment variable, uninstalls the previous local VSIX when `-Reinstall` is passed, and installs the new VSIX. Close Visual Studio before running it, then restart Visual Studio before debugger testing.

The debugger visualizer providers are registered as `ToolWindow` visualizers. The Visual Studio status surface may remain visible after launching the standalone viewer, but it must not block `Continue`, `Step Over`, or moving to the next debuggee breakpoint.

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
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net8.0-windows\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Before manual Visual Studio testing, point the extension at a built viewer:

```powershell
dotnet build .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj -c Debug -f net472
setx RAW_BUFFER_VISUALIZER_VIEWER "C:\Git\RawBufferVisualizer\.build\bin\RawBufferVisualizer.Wpf\Debug\net472\RawBufferVisualizer.Wpf.exe"
```

Close and reopen Visual Studio after `setx`; existing Visual Studio processes do not inherit the new user environment variable.

If `Raw Buffer Visualizer` is not listed under `Extensions > Manage Extensions > Installed`, install the VSIX first:

```powershell
dotnet build .\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj -c Debug -f net8.0-windows
start .\.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Debug\net8.0-windows\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Close Visual Studio before running the VSIX installer. Select Visual Studio 2022 Community when the installer asks for a target instance.

Manual Visual Studio testing still requires Visual Studio 2022 with the extension development workload. Open this solution in Visual Studio, set `RawBufferVisualizer.VisualStudio.Extensibility` as the startup project, select the `RawBufferVisualizer.VisualStudio.Extensibility` debug profile, press `F5`, then inspect `RawBufferSnapshot`, `RawBufferView`, `Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat` variables from DataTip, Watch, Locals, or Autos.

If Visual Studio shows `A project with an Output Type of Class Library cannot be started directly`, the extension debug profile is not selected. Use the Start button dropdown and choose `RawBufferVisualizer.VisualStudio.Extensibility`, or open the project's Debug properties and verify:

```text
Profile: RawBufferVisualizer.VisualStudio.Extensibility
Executable: $(DevEnvDir)devenv.exe
Arguments: /RootSuffix Exp
Working directory: $(DevEnvDir)
```

The expected result is a second Visual Studio window using the Experimental Instance. Open the target application in that second window, stop at a breakpoint, and use the visualizer icon next to a supported variable.

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

The object source converts the target object into this transfer shape internally. The extension requests `VisualizerSnapshotMetadata` first, then requests `VisualizerSnapshotChunk` blocks until the raw payload is written to disk. The current chunk size is 4 MiB.

This avoids one oversized buffer serialization call. `Bitmap` and `Mat` currently snapshot their source object once inside the object source, then return chunks from that snapshot.

## Version Policy

- Primary: Visual Studio 2022 17.9 or newer, using `VisualStudio.Extensibility`.
- Fallback: older VSSDK visualizer only if support for older Visual Studio versions becomes a product requirement.
- Runtime: keep the standalone viewer on `net472` plus modern .NET targets. The VS extension can use a modern target where the extension model allows it.

## Implementation Order

1. Done: add a small object-source project that can convert `RawBufferSnapshot` to the transfer shape.
2. Done: add a VS 2022 visualizer project targeting `RawBufferSnapshot`.
3. Done: launch the existing viewer with a temp snapshot file.
4. Done: add install/build notes for the extension.
5. Done: add `Bitmap` support.
6. Done: add OpenCvSharp `Mat` support.
7. Done: add chunked transfer for large buffers.
8. Done: add `RawBufferView` support for generic unmanaged camera/frame-grabber buffers.
9. Done: add Emgu CV `Mat` support through reflection.
10. Add vendor or camera SDK adapters only after the generic descriptor-wrapper path is stable.

## Validation Checklist

- Visualizer appears for `RawBufferSnapshot`, `RawBufferView`, `Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat` in Watch, Locals, Autos, and DataTip.
- Viewer opens from the visualizer with a generated temp snapshot.
- Pixel format, width, height, stride, valid bits, and byte order match the debuggee object.
- Mono, color, packed mono, float, and Bayer samples still render correctly after the VS path.
- Large images are transferred through metadata plus repeated chunk requests instead of one full-buffer response.
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

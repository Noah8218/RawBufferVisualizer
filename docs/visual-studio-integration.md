# Visual Studio Integration Plan

This document fixes the first Visual Studio integration direction for Raw Buffer Visualizer.

## Decision

Use the modern `VisualStudio.Extensibility` debugger visualizer model as the primary path.

Reasoning:

- Microsoft documents debugger visualizers as the Visual Studio feature for showing .NET variables or objects during a debug session.
- The modern model can run out of the main Visual Studio process and targets .NET, which fits the existing standalone viewer and SDK direction.
- The older VSSDK visualizer model remains a fallback only when older Visual Studio support is explicitly required.

## First Supported Workflow

The first Visual Studio workflow supports this flow:

1. The developer stops at a breakpoint.
2. The developer opens the Raw Buffer Visualizer entry from DataTip, Watch, Locals, or Autos.
3. The visualizer receives a supported image-like object from the debuggee process.
4. The visualizer writes a temporary `.rbuf.json` plus `.raw` snapshot.
5. The Visual Studio docked visualizer adds the image to the shared `Images` session and renders it in the docked viewer.

This keeps the normal debugging path inside the IDE. The standalone viewer remains available as a separate executable for opening saved `.rbuf.json` snapshots.

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

The Visual Studio integration is split into these projects:

```text
src\RawBufferVisualizer.VisualStudio\
src\RawBufferVisualizer.VisualStudio.ObjectSource\
src\RawBufferVisualizer.VisualStudio.Extensibility\
```

`RawBufferVisualizer.VisualStudio`:

- Shared Visual Studio helper types that are not tied to a specific object source.

`RawBufferVisualizer.VisualStudio.ObjectSource`:

- Debuggee-side conversion layer.
- Prefer `netstandard2.0` where practical so .NET Framework and modern .NET debuggee apps can use the same object source.
- Converts supported objects into a serializable snapshot transfer shape.

`RawBufferVisualizer.VisualStudio.Extensibility`:

- VS 2022 debugger visualizer entry point.
- Targets the modern Visual Studio extension model.
- Receives chunked debugger data and writes temporary snapshot files.
- Hands debugger snapshots to the docked Visual Studio image inspector.

## Current Prototype Status

- `RawBufferVisualizer.VisualStudio.ObjectSource` converts individual supported images and entries from typed or mixed lists, dictionaries, and supported image arrays.
- `RawBufferVisualizer.VisualStudio.ObjectSource` includes Visual Studio custom object sources for individual images and image collections.
- `RawBufferVisualizer.VisualStudio.ObjectSource` sends snapshot metadata first, then serves raw buffer chunks on request.
- `RawBufferVisualizer.VisualStudio.Extensibility` writes that transfer to a temporary `.rbuf.json` plus `.raw` snapshot and hands it to the docked viewer.
- The Modern debugger visualizer providers register individual supported images, open generic `List<>` and `Dictionary<,>` targets, non-generic `ArrayList` and `Hashtable`, and supported image arrays.
- Classic debugger visualizer assemblies are not packaged in the Marketplace VSIX. This avoids duplicate Classic/Modern registrations while allowing Visual Studio's required open generic registration model for typed lists and dictionaries.
- The Visual Studio debugger visualizer is hosted as a docked tool window and appends inspected images into one shared `Images` session.
- Manual Visual Studio installation and DataTip/Watch verification are documented in [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md).

## Viewer Session Strategy

The current Visual Studio session handoff is:

- The extension writes one `.rbuf.json` plus `.raw` pair per inspected image.
- The debugger visualizer sends the snapshot path to the docked Visual Studio image inspector.
- Repeated inspections add rows to the same `Images` list instead of opening a separate window per image.
- The list item keeps the variable/title, thumbnail, dimensions, pixel format, stride, and source type.
- Failed opens remain visible in the list as error rows with the reason.

The docked viewer owns mouse wheel zoom, drag pan, descriptor display, diagnostics, Try interpretation, pixel inspection, and A/B comparison.

## Temporary Snapshot Storage

Debugger inspection writes temporary files under:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\<session-id>\
```

Each inspected debugger image creates:

- one small `.rbuf.json` descriptor file
- one `.raw` payload file with the same byte length as the inspected buffer

The extension does not create persistent preview PNG files during normal debugger inspection. PNG files are created only when the user explicitly chooses save/export.

Large images can therefore consume real temp-disk space while they remain in the `Images` list. For example, a `36000 x 96000 Mono8` image is about `3.46 GB`.

Cleanup policy:

- Selecting an image row and pressing `Delete` removes that row and deletes its owned debugger temp snapshot directory.
- `Clear` disposes all current rows and deletes their owned debugger temp snapshot directories.
- Failed debugger handoffs delete partial temp snapshot directories.
- On each new debugger snapshot, stale Raw Buffer Visualizer temp snapshot directories older than 24 hours are cleaned up.
- User-opened external `.rbuf.json` files are not deleted.

VSSDK tool window acceptance criteria:

- Visual Studio docked preview stays inside the IDE and does not block stepping.
- Debugger-time pixel hover is calculated from mouse position inside the VS tool window.
- Image list remains the primary navigation surface.
- Viewer operations stay responsive under mouse wheel zoom and drag pan.

## Prototype Build

Recommended local install/update command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Reinstall
```

This publishes the single `net472` hybrid VSIX, uninstalls the previous local VSIX when `-Reinstall` is passed, and installs the new VSIX. Close Visual Studio before running it, then restart Visual Studio before debugger testing.

The debugger visualizer providers are registered as `ToolWindow` visualizers. The docked viewer should stay inside Visual Studio and must not block `Continue`, `Step Over`, or moving to the next debuggee breakpoint.

Build the current extension:

```powershell
dotnet build .\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj -c Release
```

Create the extension zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1
```

The output is:

```text
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472.zip
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

The VS debugger visualizer no longer requires `RAW_BUFFER_VISUALIZER_VIEWER`. Close and reopen Visual Studio after installing or updating the VSIX.

If `Raw Buffer Visualizer` is not listed under `Extensions > Manage Extensions > Installed`, install the VSIX first:

```powershell
dotnet build .\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj -c Debug -f net472
start .\.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Debug\net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Close Visual Studio before running the VSIX installer. Select Visual Studio 2022 Community when the installer asks for a target instance.

Manual Visual Studio testing still requires Visual Studio 2022 with the extension development workload. Open this solution in Visual Studio, set `RawBufferVisualizer.VisualStudio.Extensibility` as the startup project, select the `RawBufferVisualizer.VisualStudio.Extensibility` debug profile, press `F5`, then inspect individual image variables and the sample `imageList`, `imageDictionary`, and `imageArray` collections from DataTip, Watch, Locals, or Autos.

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

- Visualizer appears for individual supported images and registered collection types in Watch, Locals, Autos, and DataTip.
- Viewer opens docked inside Visual Studio with a generated temp snapshot.
- Pixel format, width, height, stride, valid bits, and byte order match the debuggee object.
- Mono, color, packed mono, float, and Bayer samples still render correctly after the VS path.
- Large images are transferred through metadata plus repeated chunk requests instead of one full-buffer response.
- Repeated visualizer opens append images to the same `Images` list.
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

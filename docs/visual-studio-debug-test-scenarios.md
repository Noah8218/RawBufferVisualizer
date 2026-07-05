# Visual Studio Debug Test Scenarios

Use this checklist after changing debugger visualizer, viewer launch, pixel format mapping, or image comparison UI.

## Prerequisites

1. Close all Visual Studio instances.
2. Install or update the local VSIX:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Reinstall
```

3. Reopen Visual Studio 2022.
4. Open `RawBufferVisualizer.sln`.
5. Set `RawBufferVisualizer.VisualizerDebuggee` as the startup project.
6. Run the project under the debugger without `--no-break`.

The debuggee prints the variable name before each `Debugger.Break()` call.

## Required Cases

| Step | Watch Expression | Expected Visualizer Result |
| ---: | --- | --- |
| 1 | `rawMono8Snapshot` | Opens as `Mono8`, 640 x 484, pixel hover reports grayscale values. |
| 2 | `rawMono16Snapshot` | Opens as `Mono16`, autoscaled grayscale preview. |
| 3 | `rawMono10PackedSnapshot` | Opens as `Mono10PackedLsb`. |
| 4 | `rawMono12PackedSnapshot` | Opens as `Mono12PackedLsb`. |
| 5 | `rawBinarySnapshot` | Opens as `Binary`. |
| 6 | `rawRgb24Snapshot` | Opens as `RGB24`, color channels are visible. |
| 7 | `rawBgr24Snapshot` | Opens as `BGR24`, color channels are visible. |
| 8 | `rawBgra32Snapshot` | Opens as `BGRA32`. |
| 9 | `rawFloat32Snapshot` | Opens as `Float32`. |
| 10 | `rawBayerRggb8Snapshot` | Opens as `BayerRGGB8`. |
| 11 | `rawBayerGrbg8Snapshot` | Opens as `BayerGRBG8`. |
| 12 | `rawBayerGbrg8Snapshot` | Opens as `BayerGBRG8`. |
| 13 | `rawBayerBggr8Snapshot` | Opens as `BayerBGGR8`. |
| 14 | `rawViewMono8` | Opens through unmanaged pointer metadata as `Mono8`. |
| 15 | `rawViewBgr24` | Opens through unmanaged pointer metadata as `BGR24`. |
| 16 | `rawViewMono16` | Opens through unmanaged pointer metadata as `Mono16`. |
| 17 | `rawViewBgra32` | Opens through unmanaged pointer metadata as `BGRA32`. |
| 18 | `baslerPylonLikeFrame.View` | Opens through the SDK-style object wrapper as `Mono8`. |
| 19 | `hikrobotMvsLikeFrame.View` | Opens through the SDK-style object wrapper as `BGR24`. |
| 20 | `spinnakerLikeFrame.View` | Opens through the SDK-style object wrapper as `BayerRGGB8`. |
| 21 | `frameGrabberLikeBuffer.View` | Opens through the SDK-style object wrapper as `Mono16`. |
| 22 | `bitmapMono8` | Opens as `System.Drawing.Bitmap` mapped to `Mono8`. |
| 23 | `bitmapBgr24` | Opens as `System.Drawing.Bitmap` mapped to `BGR24`. |
| 24 | `bitmapBgra32` | Opens as `System.Drawing.Bitmap` mapped to `BGRA32`. |
| 25 | `matMono8` | Opens as OpenCvSharp `Mat` mapped to `Mono8`. |
| 26 | `matBgr24` | Opens as OpenCvSharp `Mat` mapped to `BGR24`. |
| 27 | `matBgra32` | Opens as OpenCvSharp `Mat` mapped to `BGRA32`. |
| 28 | `matMono16` | Opens as OpenCvSharp `Mat` mapped to `Mono16`. |
| 29 | `matFloat32` | Opens as OpenCvSharp `Mat` mapped to `Float32`. |
| 30 | `emguMono8` | Opens as Emgu CV `Mat` mapped to `Mono8`. |
| 31 | `emguBgr24` | Opens as Emgu CV `Mat` mapped to `BGR24`. |
| 32 | `emguBgra32` | Opens as Emgu CV `Mat` mapped to `BGRA32`. |
| 33 | `emguMono16` | Opens as Emgu CV `Mat` mapped to `Mono16`. |
| 34 | `emguFloat32` | Opens as Emgu CV `Mat` mapped to `Float32`. |

For SDK-style objects, the visualizer target is the `RawBufferView` property, so add the exact `.View` expression to Watch when the visualizer icon is not shown directly on the parent object.

## UI Checks

- The Visual Studio docked visualizer shows the image list, source type, dimensions, pixel format, byte count, diagnostics, and generated metadata path.
- The visualizer is docked inside Visual Studio. `Continue` and `Step Over` must still move to the next breakpoint while the docked viewer stays open.
- Repeated inspections append rows to the same docked `Images` list instead of opening separate standalone windows.
- `Images` rows show thumbnails and can be clicked comfortably.
- The descriptor fields are read-only display text.
- Mouse wheel zoom, drag pan, `Fit`, `1:1`, descriptor, and diagnostics work in the docked Visual Studio view.
- Pixel hover shows coordinate, decoded value, raw bytes, 5x5 neighborhood, and line profile.
- `Interpret` can change format, stride, valid bits, and byte order without editing source code.
- `Compare` can set A/B, use linked pan/zoom, create a diff view, and blink between A and B.
- A failed open or malformed descriptor remains visible in `Images` as an error row and shows the reason in diagnostics.

## Failure Checks

1. Close Visual Studio.
2. Reinstall the VSIX:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Reinstall
```

3. Reopen Visual Studio and inspect a supported variable.
4. Expected: the docked visualizer opens without requiring `RAW_BUFFER_VISUALIZER_VIEWER`.

## Evidence To Capture

- One screenshot of the Visual Studio docked visualizer after a successful launch.
- One screenshot of the same docked visualizer with at least two images in the `Images` list.
- One screenshot showing pixel hover text and zoom percentage.
- One screenshot showing the Inspector with diagnostics or Try interpretation.
- One note for any unsupported SDK type, including the exact .NET type full name and SDK version.

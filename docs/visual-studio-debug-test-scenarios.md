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

- The Visual Studio visualizer status panel shows a clear success message, source type, dimensions, pixel format, byte count, and metadata path.
- The status panel behaves as a non-modal tool window. `Continue` and `Step Over` must still move to the next breakpoint while the standalone viewer stays open.
- The standalone viewer opens with the selected image in the left `Images` list and the top tab strip.
- `Images` rows show thumbnails and can be clicked comfortably.
- The descriptor fields are read-only display text.
- Mouse wheel zoom, `Fit`, `1:1`, zoom slider, pixel hover, histogram, diagnostics, `Export PNG`, and `Export Snapshot` still work.
- `Link Views` keeps same-size images aligned when switching between tabs.

## Failure Checks

1. Close Visual Studio.
2. Temporarily clear the viewer path:

```powershell
setx RAW_BUFFER_VISUALIZER_VIEWER ""
```

3. Reopen Visual Studio and inspect a supported variable.
4. Expected: the visualizer status panel reports that the viewer path must be set.
5. Restore the viewer path by rerunning:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -NoBuild
```

## Evidence To Capture

- One screenshot of the Visual Studio status panel after a successful launch.
- One screenshot of the standalone viewer with at least two images in the `Images` list.
- One screenshot showing pixel hover text and zoom percentage.
- One note for any unsupported SDK type, including the exact .NET type full name and SDK version.

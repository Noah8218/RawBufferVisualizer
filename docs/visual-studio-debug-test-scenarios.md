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

| Step | Variable | Expected Visualizer Result |
| ---: | --- | --- |
| 1 | `rawMono8Snapshot` | Opens as `Mono8`, 640 x 484, pixel hover reports grayscale values. |
| 2 | `rawBgr24Snapshot` | Opens as `BGR24`, color channels are visible. |
| 3 | `rawMono16Snapshot` | Opens as `Mono16`, autoscaled grayscale preview. |
| 4 | `rawBufferView` | Opens through unmanaged pointer metadata as `BGR24`. |
| 5 | `bitmapMono8` | Opens as `System.Drawing.Bitmap` mapped to `Mono8`. |
| 6 | `bitmapBgr24` | Opens as `System.Drawing.Bitmap` mapped to `BGR24`. |
| 7 | `bitmapBgra32` | Opens as `System.Drawing.Bitmap` mapped to `BGRA32`. |
| 8 | `matMono8` | Opens as OpenCvSharp `Mat` mapped to `Mono8`. |
| 9 | `matBgr24` | Opens as OpenCvSharp `Mat` mapped to `BGR24`. |
| 10 | `matBgra32` | Opens as OpenCvSharp `Mat` mapped to `BGRA32`. |
| 11 | `matMono16` | Opens as OpenCvSharp `Mat` mapped to `Mono16`. |
| 12 | `matFloat32` | Opens as OpenCvSharp `Mat` mapped to `Float32`. |

## UI Checks

- The Visual Studio visualizer status panel shows a clear success message, source type, dimensions, pixel format, byte count, and metadata path.
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

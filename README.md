# Raw Buffer Visualizer

### Image Watch for C# and OpenCvSharp machine-vision debugging

[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/openvisionlab.RawBufferVisualizer?label=Marketplace)](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)
[![Marketplace installs](https://img.shields.io/visual-studio-marketplace/i/openvisionlab.RawBufferVisualizer)](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)
[![CI](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml)

**Stop saving temporary images or writing debug-only conversion code. Inspect C# image variables and diagnose raw-buffer mistakes directly at a breakpoint.**

Inspect `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, `IntPtr`-backed images, raw buffers, supported image collections, and structurally recognizable camera-frame wrappers in one docked Visual Studio window. It combines registered debugger visualizers with safe current-frame discovery for C# machine-vision work.

![Raw Buffer Visualizer debugger workflow in Visual Studio](docs/images/raw-buffer-visualizer-demo.gif)

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## New In 1.0.48

The `1.0.48.0` package fixes the clean-PC ToolWindow registration failure found after the `1.0.47.0` Marketplace upload. Until the Marketplace badge above reports `1.0.48`, the public install link still serves `1.0.47`.

### Clean-install package registration

- The VSSDK package class and command table are now built by the same hybrid project that produces the Marketplace VSIX.
- The generated `.pkgdef` points to `RawBufferVisualizer.VisualStudio.Extensibility.dll` in the installed VSIX folder.
- Local install verification no longer writes a manual package `CodeBase`, so a broken Marketplace package cannot pass by using a developer build output.
- Release packaging fails if the VSIX falls back to the former split-project `.pkgdef` layout.

There is no image-transfer or UI behavior change in this hotfix. Automatic Vision Inspector and Vision Buffer Doctor remain the main feature additions from `1.0.47`.

### Automatic Vision Inspector

Open the docked Tool Window once, leave `Auto Inspect on Break` enabled, and stop at a breakpoint. The inspector scans the selected stack frame's locals and arguments for objects that expose an accessible buffer pointer or managed array together with width, height, stride, and format information.

- `[Auto]` means inference and the current buffer passed validation and the image opened.
- `[Map]` means the object looks image-like but one or more roles are ambiguous. Smart Type Mapper lets you confirm the member or enum mapping once and reuses it later.
- `[Failed]` means the object was recognized but its current pointer, array, or descriptor could not be opened.
- One failed candidate does not prevent the remaining images from opening.
- `Auto Inspect on Break` is a per-user preference that persists across Visual Studio restarts. `Scan Now` still works while automatic scanning is off.

Registered OpenCvSharp, Emgu CV, and Bitmap types continue to use their reliable debugger-visualizer icon path and are excluded from automatic mapping candidates. Automatic discovery is for unregistered pointer/array-backed wrappers whose required members are visible to the debugger; it does not call arbitrary SDK methods or reverse-engineer private native layouts.

![Automatic Vision Inspector finds safe image-like values in the current stack frame](docs/images/automatic-vision-inspector.png)

### Vision Buffer Doctor

When an image looks sheared, too dark, scrambled, or incorrectly packed, select `Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible width, height, stride, pixel format, valid-bit, and byte-order interpretations from bounded samples. Selecting a candidate applies it immediately without another debugger round trip.

Buffer Doctor is a buffer-layout assistant, not a semantic image detector. RGB/BGR and Bayer phase can remain inherently ambiguous, so the UI keeps tied candidates visible for the developer to confirm.

![Vision Buffer Doctor ranks and applies plausible raw-buffer interpretations](docs/images/vision-buffer-doctor.png)

## One-Minute Quick Start

1. Install the extension from [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer) and restart Visual Studio.
2. Open `View > Other Windows > Raw Buffer Visualizer` once.
3. Start debugging and stop where image variables or camera-frame wrappers are alive.
4. Let `Auto Inspect on Break` open safe unregistered wrappers. For Bitmap, OpenCvSharp, Emgu CV, and registered collections, click the `Raw Buffer Visualizer` icon in DataTip, Watch, Locals, or Autos.
5. Select a thumbnail, zoom or pan, and inspect X/Y, GV or RGB values, raw bytes, stride, and pixel format.
6. If a raw image looks wrong, run `Interpret > Diagnose Buffer` and select the most plausible candidate.

The same workflow works for a single image, a typed `List<TImage>` or `Dictionary<TKey, TImage>`, a mixed object collection, or a supported image array.

## Why Raw Buffer Visualizer?

| Capability | Typical basic Mat viewer | Raw Buffer Visualizer |
| --- | --- | --- |
| OpenCvSharp `Mat` | Common | Supported |
| Emgu CV `Mat` and `System.Drawing.Bitmap` | Varies | Supported |
| `IntPtr` and raw image buffers | Limited | Supported through pointer shapes and `RawBufferView` |
| Current-frame discovery for unregistered camera wrappers | Uncommon | Safe pointer/array-backed shapes in locals and arguments |
| Ranked recovery for wrong stride/format/byte order | Uncommon | Vision Buffer Doctor |
| Stride, byte order, valid bits, and raw-byte diagnostics | Limited | Supported |
| `Mono10PackedLsb` and `Mono12PackedLsb` | Uncommon | Supported |
| Multiple inspected images in one docked list | Varies | Supported |
| Split, absolute diff, blink, and linked pan/zoom | Varies | Supported |
| File-backed tiled display for very large payloads | Uncommon | Supported |

Other debugger visualizers have different feature sets. This comparison describes the difference between a basic Mat-only workflow and the capabilities implemented in this project.

## Key Features

- Automatic Vision Inspector for safe image-like locals and arguments, with isolated `[Auto]`, `[Map]`, and `[Failed]` outcomes.
- Smart Type Mapper as the recovery path for ambiguous company-specific image wrappers.
- Vision Buffer Doctor with ranked, immediately applicable raw-buffer interpretations.
- Single docked Visual Studio window where inspected images accumulate in an `Images` list.
- Open a typed or mixed image list, dictionary, or array once to append its image entries to that same list.
- Image rows include variable/title, thumbnail, width x height, pixel format, stride, and source type.
- Failed opens stay visible as error rows with the reason, instead of disappearing silently.
- Pixel status strip with X/Y, GV or RGB channel values, color swatches, and source bytes.
- High-zoom pixel grid overlay for reading values directly on the image.
- Save the current visible view as PNG, or save a raw `.rbuf.json` snapshot from the image list context menu.
- Hover 5x5 values/statistics, selectable pixel marker overlay, pinned marker, line profile, histogram, and diagnostics.
- Try interpretation controls for changing pixel format, stride, valid bits, and byte order while debugging.
- A/B comparison MVP: set A/B, link pan/zoom, split view, diff view, and blink compare.
- File-backed tiled display for very large raw payloads.

![High-zoom pixel value overlay](docs/images/viewer-vs-docked-overlay.png)

## Install

### Visual Studio extension

Install the Marketplace extension from Visual Studio:

1. Open `Extensions > Manage Extensions`.
2. Search for `Raw Buffer Visualizer`.
3. Install the extension.
4. Close all Visual Studio windows when prompted.
5. Reopen Visual Studio before debugging.

The Marketplace package is one VSIX that contains both parts required for normal use:

- debugger visualizers for supported image variables
- the docked Visual Studio image inspector

The `1.0.47.0` feature line added Automatic Vision Inspector and Vision Buffer Doctor. Version `1.0.48.0` keeps those features and fixes clean-install registration of the docked ToolWindow.

For local development builds, close every Visual Studio window and run this from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

The script builds and reinstalls the single VSIX and removes obsolete Raw Buffer Visualizer Classic DLLs from `Documents\Visual Studio 2022\Visualizers`. It intentionally does not write VSSDK registration values; the installed VSIX must register the docked ToolWindow by itself. Restart Visual Studio after it finishes.

### Update

Use `Extensions > Manage Extensions > Updates` in Visual Studio. After the update, close all Visual Studio windows and reopen Visual Studio.

If a lower `Raw Buffer Visualizer` tab from version `1.0.34.0` or earlier is still present in a saved Visual Studio layout, close that tab once. Current Marketplace packages publish the debugger providers and automatically close their temporary handoff host, so new invocations remain in the main docked viewer.

For `1.0.48` and later, a release must not be qualified by manually writing a package `CodeBase`. If an older developer installation left a stale registration, close all Visual Studio windows and use the repair script only as a local migration/recovery step:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

The repair script points the docked tool window back to the currently installed Marketplace extension folder and removes old startup auto-load registrations. After recovery, reinstall the release VSIX without repair and run the installed-VSIX smoke before treating the package as publishable.

If the popup appears only when inspecting an image, check:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\package.log
%APPDATA%\Microsoft\VisualStudio\17.0_...\ActivityLog.xml
```

Older `1.0.24.0` builds could require `Microsoft.VisualStudio.Threading 17.14.0.0` and fail on PCs that had not updated Visual Studio to 17.14. Version `1.0.25.0` and later target Visual Studio 2022 17.9-compatible references.

### Uninstall

To uninstall, use `Extensions > Manage Extensions > Installed` in Visual Studio, or uninstall the VSIX by extension id:

```powershell
VSIXInstaller.exe /quiet /uninstall:RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
```

### Standalone viewer

The standalone viewer is optional. It opens saved `.rbuf.json` snapshots and is useful for saved samples, large-image validation, and screenshots.

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release
dotnet run --project .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj -f net8.0-windows -- .\artifacts\samples\mono8-gradient.rbuf.json
```

## Visual Studio Usage

For unregistered image wrappers:

1. Open `View > Other Windows > Raw Buffer Visualizer`.
2. Keep `Auto Inspect on Break` enabled, or use `Scan Now` on demand.
3. Stop at a breakpoint. The selected frame's locals and arguments are scanned without forcing the Tool Window to open or steal focus.
4. Review automatic image rows. Use `Map` only for an ambiguous type; the saved mapping is reused on later breaks and Visual Studio sessions.

For registered image types and collections:

1. Install `Raw Buffer Visualizer` from Visual Studio Marketplace.
2. Start debugging a C# project in Visual Studio.
3. Stop at a breakpoint where a supported image variable is alive.
4. In DataTip, Watch, Locals, or Autos, click the visualizer icon.
5. The image is appended to the docked `Raw Buffer Visualizer` window.
6. Use the `Images` list to switch between captured variables.
7. Use mouse wheel for zoom and mouse drag for pan.
8. Use the status strip and Inspector for pixel values, raw bytes, hover 5x5 statistics, marker values, diagnostics, interpretation, and comparison.
9. Use `Save` to export the current visible view as PNG. Right-click an image row to save the raw snapshot.

To inspect several images at once, click the visualizer on the collection variable itself:

```csharp
var stages = new List<OpenCvSharp.Mat> { inputMat, thresholdMat, resultMat };
var named = new Dictionary<string, OpenCvSharp.Mat>
{
    ["input"] = inputMat,
    ["result"] = resultMat
};
var mixed = new object[] { inputBitmap, resultMat };
```

Collection entries appear as `[0]`, `[1]`, or `[key]` in the existing docked `Images` list. Valid entries remain normal image rows. Null, unsupported, and failed entries remain visible as red error rows with the reason. One invocation processes at most 256 entries; a collection above that limit adds an error row explaining that only the first 256 entries were shown. Lazy or arbitrary `IEnumerable` sequences are intentionally not enumerated while the debugger is paused.

When an image cannot be opened, select its red error row. The viewer shows a stable error ID, the failure reason, `Copy Report`, and `Open Logs`. `Copy Report` includes the extension and Visual Studio versions, source type, descriptor/diagnostics, and stack details when available. It does not include the image payload. Review paths and variable names in the report before sharing it in a GitHub issue.

Visual Studio requires generic debugger visualizers to register the open generic `List<>` and `Dictionary<,>` types. Raw Buffer Visualizer therefore appears for typed and mixed lists or dictionaries. It transfers supported image entries only; null, unsupported, and failed entries remain visible as error rows. Visual Studio's built-in `IEnumerable Visualizer` can remain in the visualizer menu, so select `Raw Buffer Visualizer` when more than one visualizer is offered.

The toolbar intentionally stays small: `Open`, `Clear`, `Save`, `Fit`, `1:1`, `Inspector`, and `Link Views` when there is room. Detailed debugging controls stay in the Inspector or compact docked inspector so the Visual Studio workflow remains focused.

The docked layout adapts to the available width:

- Narrow: image list, viewer, Save, status strip, and an `Inspector` toggle.
- Medium: image list, viewer, and bottom tab Inspector.
- Wide: image list, viewer, Descriptor, and full right-side Inspector.

![Failed opens remain visible as error rows](docs/images/viewer-vs-docked-error.png)

## Supported Inputs

| Input | Status | Notes |
| --- | --- | --- |
| `RawBufferSnapshot` | Supported | SDK snapshot from `byte[]`, `ushort[]`, `float[]`, or `IntPtr`. |
| `RawBufferView` | Supported | Pointer-backed wrapper for common camera/frame-grabber image shapes. |
| Unregistered camera/frame wrappers | Conditional | Automatic Inspector supports debugger-visible pointer/array, width, height, stride, and pixel-format shapes. Ambiguous enum/member roles require one saved mapping. |
| Exact ImagePtr compatibility target | Limited | The debugger icon is registered for the existing `Cressem.ImageModel.ImagePtr` contract. Other types should use `RawBufferView` or Automatic Inspector. |
| `System.Drawing.Bitmap` | Supported | 8bpp indexed, 24bpp RGB, and 32bpp RGB/ARGB/PARGB mappings. |
| OpenCvSharp `Mat` | Supported | Common 8-bit, 16-bit, and 32-bit float Mat formats. Uses reflection over both legacy and current `Mat` APIs instead of requiring the debuggee's OpenCvSharp package version. |
| Emgu CV `Mat` | Supported | Extracted by reflection, so the extension does not require a direct Emgu dependency. |
| Image collections | Supported | Typed or mixed `List<T>`, `Dictionary<TKey, TValue>`, `ArrayList`, `Hashtable`, `object[]`, and supported image arrays. Up to 256 entries are processed per invocation. |
| `.rbuf.json` + `.raw` | Supported | Snapshot metadata plus raw payload. |
| `.raw` / `.bin` only | Limited | Create a matching `.rbuf.json` descriptor first. |

OpenCvSharp compatibility was exercised with real `Mat` instances from `OpenCvSharp4` packages `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627`. Emgu CV compatibility was exercised with real `Mat` instances from packages `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924`. These are tested compatibility points, not a guarantee for every package version between them. `System.Drawing.Bitmap` is handled through the stable .NET Framework drawing API and is tested in the `net472` debugger sample.

Industrial camera and frame-grabber SDK objects are best supported through a common shape adapter first. If your object exposes buffer pointer, width, height, stride, channels, bit depth, and pixel format, inspect it through `RawBufferView`. Automatic Inspector can also recognize many existing wrappers when those members are debugger-visible.

```csharp
var view = new RawBufferView
{
    Buffer = imagePointer,
    BufferLength = stride * height,
    Width = width,
    Height = height,
    Stride = stride,
    PixelFormat = RawPixelFormat.BGR24,
    Channels = 3,
    BitDepth = 8,
    ByteOrder = RawByteOrder.LittleEndian,
    Name = "camera0"
};
```

Inspect `view` directly from Visual Studio after the VSIX is installed. For existing pointer classes, the visualizer can also read this minimal shape:

```csharp
public sealed class ImagePtr
{
    public IntPtr Ptr { get; set; }
    public long Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Step { get; set; }
    public int Bpp { get; set; } // 1=Mono8, 3=BGR24, 4=BGRA32
}
```

## Supported Pixel Formats

| Format | Storage | Valid Bits | Display |
| --- | ---: | ---: | --- |
| `Mono8` | 1 byte / pixel | 8 | Grayscale |
| `Mono16` | 2 bytes / pixel | 1-16 | Grayscale |
| `Mono10PackedLsb` | 10-bit packed | 10 | Grayscale |
| `Mono12PackedLsb` | 12-bit packed | 12 | Grayscale |
| `Binary` | 1 byte / pixel | 1 | 0 black, non-zero white |
| `RGB24` | 3 bytes / pixel | 8 | Color, RGB byte order |
| `BGR24` | 3 bytes / pixel | 8 | Color, BGR byte order |
| `BGRA32` | 4 bytes / pixel | 8 | Color with alpha |
| `Float32` | 4 bytes / pixel | 32 | Grayscale |
| `BayerRGGB8` | 1 byte / pixel | 8 | Simple Bayer preview |
| `BayerGRBG8` | 1 byte / pixel | 8 | Simple Bayer preview |
| `BayerGBRG8` | 1 byte / pixel | 8 | Simple Bayer preview |
| `BayerBGGR8` | 1 byte / pixel | 8 | Simple Bayer preview |

Unsupported or malformed formats should fail with a visible error row and diagnostics.

## Bitmap And Mat Mappings

| Source format | Visualizer format |
| --- | --- |
| `System.Drawing.Imaging.PixelFormat.Format8bppIndexed` | `Mono8` |
| `System.Drawing.Imaging.PixelFormat.Format24bppRgb` | `BGR24` |
| `System.Drawing.Imaging.PixelFormat.Format32bppArgb` | `BGRA32` |
| `System.Drawing.Imaging.PixelFormat.Format32bppPArgb` | `BGRA32` |
| `System.Drawing.Imaging.PixelFormat.Format32bppRgb` | `BGRA32` |
| OpenCvSharp `CV_8UC1` | `Mono8` |
| OpenCvSharp `CV_8UC3` | `BGR24` |
| OpenCvSharp `CV_8UC4` | `BGRA32` |
| OpenCvSharp `CV_16UC1` | `Mono16` |
| OpenCvSharp `CV_32FC1` | `Float32` |
| Emgu CV `Cv8U`, 1 channel | `Mono8` |
| Emgu CV `Cv8U`, 3 channels | `BGR24` |
| Emgu CV `Cv8U`, 4 channels | `BGRA32` |
| Emgu CV `Cv16U`, 1 channel | `Mono16` |
| Emgu CV `Cv32F`, 1 channel | `Float32` |

## Snapshot Files

A saved snapshot uses two files:

```text
image.raw
image.rbuf.json
```

Example metadata:

```json
{
  "rawFile": "image.raw",
  "width": 2448,
  "height": 2048,
  "stride": 2448,
  "pixelFormat": "Mono8",
  "validBits": 8,
  "byteOrder": "LittleEndian"
}
```

Create a snapshot from code:

```csharp
var descriptor = new RawImageDescriptor
{
    Width = 2448,
    Height = 2048,
    Stride = 2448,
    PixelFormat = RawPixelFormat.Mono8,
    ValidBits = 8,
    ByteOrder = RawByteOrder.LittleEndian
};

RawBufferSnapshot.Save("cam1.rbuf.json", buffer, descriptor);
```

## Large Image Validation

The viewer avoids allocating one full-frame bitmap for large raw payloads. It uses file-backed tiled display and skips CPU-heavy full-frame preview work when needed.

| Case | Current result |
| --- | --- |
| `100000 x 100000` `Mono8` file-backed smoke | Passed with dense 10 GB payload and non-sparse file check. |
| `200000 x 200000` `Mono8` file-backed smoke | Passed with dense 40 GB payload and non-sparse file check. |
| 100k packed `Mono10PackedLsb` and `Mono12PackedLsb` | Passed as file-backed smoke captures. |
| Core tests | Cover descriptor planning, file-backed tile reads, diff rendering, diagnostics, and raw-byte pixel inspection. |

To generate portable `100000 x 100000` and `200000 x 200000` sample files on another PC, see [large image samples](docs/large-image-samples.md).

For OpenCV/Emgu smoke tests with images above OpenCV's default pixel safety limit, set `OPENCV_IO_MAX_IMAGE_PIXELS` before loading the file:

```powershell
$env:OPENCV_IO_MAX_IMAGE_PIXELS = "4000000000"
dotnet run --project .\samples\RawBufferVisualizer.VisualizerDebuggee\RawBufferVisualizer.VisualizerDebuggee.csproj --configuration Release -- --emgu-tiff-smoke "C:\path\large.tif"
```

The large-image dimensions above are validation evidence, not sample payloads committed to Git. Generate the dense files locally with [large image samples](docs/large-image-samples.md).

## Viewer Performance

The table below compares the viewer before and after the progressive viewport update using the same machine, automated smoke, and dense `5000 x 5000 Mono8` input (`25,000,000` raw bytes). Lower values are better.

| Metric | Before update | Current (`1.0.45`) | Improvement |
| --- | ---: | ---: | ---: |
| Initial open path | `179.818 ms` | `115.369 ms` | `35.8%` lower |
| Zoom average frame | `16.684 ms` | `13.765 ms` | `17.5%` lower |
| Zoom maximum frame | `49.397 ms` | `36.527 ms` | `26.1%` lower |
| Pan maximum frame | `21.951 ms` | `17.056 ms` | `22.3%` lower |
| Pan average tile upload | `20.410 ms` | `15.211 ms` | `25.5%` lower |

An installed-VSIX check in Visual Studio 2022 `17.14` also exercised a dense file-backed `24000 x 24000 Mono8` image with real mouse input. Across `87` wheel and `269` drag events, input handling averaged `0.095 ms` and `0.033 ms`; rendered frames averaged `6.515 ms` with a `13.642 ms` maximum. Pixel status, selection, and pinned-marker state remained active during the run.

These measurements are regression evidence from one test machine, not guaranteed timings for every PC. Image format, storage, GPU driver, debugger state, and docked-window size can change the result.

## Runtime Stability

Raw Buffer Visualizer writes temporary snapshot files while Visual Studio transfers debugger data into the docked viewer. The extension keeps these files under:

```text
%TEMP%\RawBufferVisualizer\VisualStudio
```

Version `1.0.27.0` and later include the following runtime safeguards:

- The docked window status bar shows current temp usage as `Temp ...` so long debug sessions can be monitored.
- The active image status shows whether the payload is memory-backed or file-backed.
- `Clear` and image-row `Delete` dispose loaded sources and remove owned temporary snapshot folders.
- Startup inbox polling now uses file-system events plus backoff polling instead of a fixed 500 ms background loop.
- Payloads above 512 MB are opened as file-backed tiled sources instead of one large `byte[]`.
- Stale owned snapshot folders are cleaned on later visualizer runs when they are older than 24 hours.
- The package log is capped so repeated package-load diagnostics do not grow without bound.

If disk usage looks high after a crashed debug session, close Visual Studio and delete:

```text
%TEMP%\RawBufferVisualizer\VisualStudio
```

Latest recorded release-gate smoke for the current runtime line:

| Check | Result |
| --- | --- |
| Full solution and unit-style self-tests | Passed for the declared `net472`, `netstandard2.0`, and .NET 8 targets. |
| Legacy image libraries | Passed with five OpenCvSharp and five Emgu CV package versions plus .NET Framework Bitmap. |
| Standalone viewer interactions | Passed open, pixel/GV read, Fit, 1:1, slider and wheel zoom, PNG/snapshot export, tabs, and linked views. |
| VS2022 docked `5000 x 5000 Mono8` | Passed with `115.3 ms` open path, `1.24 ms` max wheel command, `0.77 ms` max drag command, and `33.94 ms` max frame. |
| Installed VSIX, real `8192 x 8192` Mats | Passed in VS2022 17.14 with OpenCvSharp and Emgu CV, correct GV values, at most `1 MiB` per new preview file, and controlled `Unavailable` state after debuggee exit. |
| Installed VSIX, hybrid current-frame session | Passed in VS2022 17.14 with real OpenCvSharp `4.13.0.20260627`, Emgu CV `4.13.0.5924`, and Bitmap values through registered visualizers plus six automatically opened pointer-backed camera-shape fixtures; 9 images, 0 errors, and no duplicate mapping rows for registered types. |
| Dense file-backed `100000 x 100000 Mono8` | Passed with a non-sparse `10,000,000,000` byte payload, `1.73 s` first visible time, and `88.0 MB` working set. |
| Dense file-backed `200000 x 200000 Mono8` | Passed with a non-sparse `40,000,000,000` byte payload, `1.94 s` first visible time, and `87.5 MB` working set. |
| Docked accumulation and cleanup soak | Passed 240 repeated `2048 x 2048 Mono8` opens using both selected-item Delete and Clear, with no positive managed/private/working-set growth, no GDI/USER growth, and no owned temporary directories left behind. |

## Build And Test

Build prerequisites:

- Visual Studio 2022 17.9 or newer with .NET desktop development.
- .NET 8 SDK or newer. The solution does not require the .NET 9 SDK.

From a fresh clone, build the solution once before inspecting the generated VSIX payload:

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln -c Release
```

The hybrid `RawBufferVisualizer.VisualStudio.Extensibility` project owns debugger providers, the `RawBufferVisualizerPackage` registration shell, command table compilation, and the public VSIX. Its `PkgdefProjectOutputGroup` generates `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef` with a `CodeBase` under `$PackageFolder$`. The `RawBufferVisualizer.VisualStudio.Vssdk` project is a referenced ToolWindow/UI support library and must not produce the Marketplace package registration.

Build:

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release
```

Run core tests:

```powershell
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
```

Run the .NET Framework Bitmap, OpenCvSharp, and Emgu CV version matrix:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLegacyImageCompatibility.ps1
```

Run Visual Studio docked smoke checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild
```

Run the large-image preview and installed-VSIX regression checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokePreviewFirstHandoff.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeSampledPreviewPerformance.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixLargeMats.ps1 -Configuration Release -VisualStudioInstanceId <VS2022-instance-id> -NoBuild
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedMemorySoak.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
```

The installed-VSIX smoke opens real OpenCvSharp and Emgu CV `8192 x 8192` Mats in Visual Studio 2022, checks pixel values and bounded temporary storage, then terminates the debuggee and verifies that the last image remains visible without an unhandled dialog.

Create sample snapshots:

```powershell
dotnet run --project .\samples\RawBufferVisualizer.Samples\RawBufferVisualizer.Samples.csproj --framework net8.0
```

Run the debugger visualizer sample:

```powershell
dotnet run --project .\samples\RawBufferVisualizer.VisualizerDebuggee\RawBufferVisualizer.VisualizerDebuggee.csproj -- --no-break
```

For manual Visual Studio validation, set `RawBufferVisualizer.VisualizerDebuggee` as the startup project and run under the debugger without `--no-break`. The sample creates individual image variables, typed OpenCvSharp/Emgu CV/Bitmap lists and dictionaries, and mixed object collections and arrays so each visualizer path can be checked from Watch, Locals, Autos, or DataTip. Pass `--collection-only` to stop only at collection cases.

README and Marketplace screenshots must be reviewed before commit. Do not publish screenshots that include unrelated applications, private desktop content, stale UI, or a feature state that does not match the text.

## Release And Marketplace

The Marketplace extension is currently distributed as a preview. Before publishing an update, validate:

- Clean install, update, uninstall, and reinstall of the VSIX.
- Multi-instance isolation: with two separate `devenv.exe` processes running, each debugger visualizer invocation must reach only that Visual Studio instance's docked viewer.
- Docked Visual Studio workflow with narrow and wide tool-window layouts.
- Save PNG, raw snapshot export, pixel status, hover 5x5 statistics, marker values, pan, zoom, high-zoom overlay, error rows, and support-report actions.
- `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, and supported collections.
- Automatic Inspector partial-result behavior, preference persistence, registered-type deduplication, and Smart Type Mapper recovery.
- Buffer Doctor ranked candidates and immediate descriptor application.
- Large file-backed snapshots and the standalone viewer.
- Package-load smoke after update: Visual Studio must not show `RawBufferVisualizerPackage did not load correctly` on startup.
- VSSDK package ownership: the generated `.pkgdef` must reference `RawBufferVisualizer.VisualStudio.Extensibility.dll`; the former split-project `.pkgdef` is prohibited.
- VSSDK package compatibility: `RawBufferVisualizer.VisualStudio.Extensibility.dll` must not reference `Microsoft.VisualStudio.Threading` newer than `17.9.0.0`.

See [docs/marketplace-checklist.md](docs/marketplace-checklist.md) for the release checklist.
For repeatable Marketplace updates, use [docs/release-runbook.md](docs/release-runbook.md). The `Marketplace CD` GitHub Actions workflow builds and validates by default, and publishes only when `publish=true` is selected with the Marketplace environment approval.
Marketplace feature Overview: [1.0.47 Overview](docs/marketplace-overview-1.0.47.md).
Marketplace release text for this hotfix: [1.0.48 release notes](docs/marketplace-release-notes-1.0.48.md).
GitHub Release body for this version: [1.0.45 GitHub Release draft](docs/github-release-1.0.45.md).
For the short product video, follow the [20-second demo recording guide](docs/demo-recording-guide.md).

## License

Copyright (c) 2026 Noah Choi.

This project is licensed under the MIT License. You may use, modify, and redistribute the source code, but the copyright and license notice must remain included. See [LICENSE](LICENSE).

External libraries keep their own licenses. Review [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) before publishing a VSIX, release package, or redistributed binary.

# Raw Buffer Visualizer

### Image Watch for C# and OpenCvSharp machine-vision debugging

[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/openvisionlab.RawBufferVisualizer?label=Marketplace)](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)
[![Marketplace installs](https://img.shields.io/visual-studio-marketplace/i/openvisionlab.RawBufferVisualizer)](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)
[![CI](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml)

**Stop saving temporary images or writing debug-only conversion code. Inspect C# image variables and diagnose raw-buffer mistakes directly at a breakpoint.**

Inspect `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, `IntPtr`-backed images, raw buffers, supported image collections, and structurally recognizable camera-frame wrappers in one docked Visual Studio 2022 or Visual Studio 2026 window. It combines registered debugger visualizers with safe current-frame discovery for C# machine-vision work.

![Open an OpenCvSharp Mat from its code DataTip](docs/images/raw-buffer-visualizer-datatip-open.gif)

Hover an initialized `Mat` or `Bitmap` in the code editor and select its visualizer magnifying-glass icon. The example opens a real 1280 x 720 `BGR24` industrial-machine photograph from an OpenCvSharp `Mat` directly into the docked viewer.

![Open a real industrial Bitmap from a Visual Studio breakpoint](docs/images/raw-buffer-visualizer-breakpoint-open.gif)

The breakpoint demo uses an actual Locals row and the registered Raw Buffer Visualizer entry to open a recognizable color PCB photograph in the docked viewer.

![Raw Buffer Visualizer debugger workflow in Visual Studio](docs/images/raw-buffer-visualizer-demo.gif)

The six-second overview continues through live color pixels, automatic inspection, diagnostics, an intentionally wrong `BGR24` stride, and the restored color frame after applying the top Buffer Doctor candidate.

![Raw Buffer Visualizer inspecting a real industrial PCB image in Visual Studio](docs/images/industrial-pcb-auto-inspector-pixel.png)

[Demo image provenance, licenses, and validation](docs/industrial-image-testing.md)

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Raw Buffer Visualizer 2.0.7

Version `2.0.7` adds signed 32-bit, single-channel image support for OpenCvSharp `CV_32SC1`, Emgu CV `Cv32S` C1, mapped `int[]`, and vendor-neutral `RawBufferView`/`RawBufferSnapshot` buffers. It renders these matrices with signed min/max grayscale autoscaling while preserving exact signed pixel values and four source bytes in the inspector. Existing 2.0 features remain included:

![Raw Buffer Visualizer 2.0.7 inspecting signed Int32 industrial images](docs/images/int32-industrial-automatic-matrix.png)

- the exact debugger object/expression name above each thumbnail, including names such as `imageList[0]`, `imageDictionary[key]`, and `bitmapArray[1]`;
- the source object's `Ptr`, `Buffer`, or captured `Scan0` separately from the `Pixels` address used to read image bytes;
- separate **Copy pointer address** and **Copy pixel address** row-menu actions, with the same values in Descriptor details;
- process ID plus `LIVE`, `CAPTURED`, `PREVIEW`, or `UNAVAILABLE` state wherever source-location information is available;
- a fresh read of current bytes whenever the same address is opened again—an address is never treated as object identity;
- controlled failure for released, inaccessible, or partially readable pointer ranges instead of accepting incomplete image data;
- corrected top-to-bottom transfer for `Bitmap` images whose native stride is negative;
- complete assembly-qualified Bitmap/OpenCvSharp/Emgu array registrations, avoiding Visual Studio's invalid version-string failure when an image array is opened directly;
- a clearly labelled **Clear all** action above the image list that removes every loaded image and resets document-dependent viewer state without changing debuggee data;
- direct `ConcurrentDictionary<TKey,TValue>` visualization from DataTip, Locals, Autos, and Watch on .NET Framework and current .NET;
- safe first-use `ImagePtr` handoff without a manual Tool Window warm-up or cyclic package load;
- Visual Studio 2022 `17.9+` Community, Professional, and Enterprise x64 support;
- stable Visual Studio 2026 `18.x` compatibility through the same extension identity;
- shorter debugger-handoff filenames so long writable temporary-storage paths do not block registered images;
- one documented contract for registered `RawBufferView`/`RawBufferSnapshot`, mapped pointers, and mapped `byte[]`, `ushort[]`, and `float[]` buffers;
- shared checked fail-closed validation for dimensions, stride, buffer length, format/order enum values, arithmetic overflow, and valid bits before metadata transfer;
- preserved valid `Mono16` values and fixed 10/12-bit packed-layout rules;
- controlled `Unavailable` state after Continue/process exit, with live reads cancelled and copied managed buffers retained;
- debugger-session gating that prevents delayed pre-Continue handoffs from reopening in Run Mode or a later Break;
- **Connect Doctor** inside Connect Your Buffer: ranked bounded interpretations update only the visible draft and preview until **Save Mapping** is selected.
- direct, automatic, and collection inspection of supported signed `CV_32SC1` Mats, including padded row stride.

Address labels deliberately follow each library's public contract:

| Source | Object/source pointer | Pixel-read address | Display behavior |
| --- | --- | --- | --- |
| OpenCvSharp `Mat` | `Ptr` (`CvPtr`) | `Pixels` (`Data`) | Both rows are shown because the addresses normally differ. |
| Emgu CV `Mat` | `Ptr` | `Pixels` (`DataPointer`) | Both rows are shown because the addresses normally differ. |
| Exact ImagePtr target | `Ptr` | the same pointer | One combined `Ptr / Pixels` row is shown. |
| `RawBufferView` | `Buffer` | the same pointer | One combined `Buffer / Pixels` row is shown. |
| `System.Drawing.Bitmap` | `Scan0` while `LockBits` is active | the same captured address | One combined `Scan0 / Pixels` row is shown as `CAPTURED`; it is never presented as a live Bitmap pointer. |

`Ptr` identifies the native wrapper/source object when that library exposes one. `Pixels` is the address from which image bytes are actually read. The two values are not interchangeable for OpenCvSharp or Emgu CV.

When the Raw Buffer Visualizer Tool Window is first opened after installing `2.0.7`, it shows a non-modal release summary. **What's New** opens or closes it from the same button without starting a scan or opening an image. **Dismiss** also closes it and saves the version as seen across Visual Studio restarts. See the complete [changelog](CHANGELOG.md).

### Environment Check

Use **Environment** in the Tool Window toolbar after a Windows reinstall or when the extension/host setup is uncertain. It lists only the supported Visual Studio host, loaded extension version, and writable temporary storage required by the extension.

- **Refresh** rechecks environment state only. It does not scan the debugger frame, open an image, or modify registration.
- **Copy diagnostic report** excludes credentials, environment-variable values, and image payloads. Review local paths before sharing.
- Select **Environment** again to close the panel without changing registration, starting a scan, or opening an image.
- Marketplace users only need a supported x64 Visual Studio installation and the extension.

Contributor and recovery details are in [Development Prerequisites And Utility Recovery](docs/development-prerequisites.md).

### Automatic Vision Inspector

Open the docked Tool Window once, leave `Auto Inspect on Break` enabled, and stop at a breakpoint. The inspector scans the selected stack frame's locals and arguments for objects that expose an accessible buffer pointer or managed array together with width, height, stride, and format information.

- `[Auto]` means inference and the current buffer passed validation and the image opened.
- `[Map]` means the object looks image-like but one or more roles are ambiguous. Smart Type Mapper lets you confirm the member or enum mapping once and reuses it later.
- `[Failed]` means the object was recognized but its current pointer, array, or descriptor could not be opened.
- One failed candidate does not prevent the remaining images from opening.
- `Auto Inspect on Break` is a per-user preference that persists across Visual Studio restarts. `Scan Now` still works while automatic scanning is off.

Initialized exact OpenCvSharp `Mat` and Emgu CV `Mat` values can use Automatic Inspector's validated live-memory path and still retain their debugger-visualizer icons. Exact Mat `List<T>` and one-dimensional arrays are included automatically with bounded per-element success/failure rows; no separate collection option is required. `System.Drawing.Bitmap`, `RawBufferSnapshot`, `RawBufferView`, and other registered collections remain on their registered visualizer paths. Bitmap automatic extraction would require a `LockBits`/`UnlockBits` lifecycle inside the debuggee, which Automatic Inspector deliberately does not inject or invoke.

### Connect Your Buffer

When Automatic Inspector finds an image-like application object but cannot safely decide which members hold its pointer, dimensions, stride, or pixel format, open **Connect Your Buffer** from the `[Map]` result or error-row menu.

1. Review the suggested Data, Width, Height, Stride, Buffer Length, Pixel Format, Valid Bits, and Byte Order roles.
2. Map vendor or application enum values to Raw Buffer Visualizer formats.
3. Select **Preview** to read the current stopped-process buffer. Opening the dialog alone never reads it.
4. If the interpretation still looks wrong, select **Diagnose interpretation**. It opens or closes on repeated selection and lists ranked format/layout candidates. Selecting a row updates only the visible draft and preview.
5. Select **Save Mapping** to persist the mapping for the next equivalent break. A diagnosis that the visible members cannot reproduce is blocked with a reason.
6. **Use Suggested Roles** resets only the visible choices; it does not save, scan, or open an image.

To review a saved automatic mapping later, select its image and open **Inspector > Interpret > Edit Mapping**. The same action is available in Compact and Wide layouts.
7. Optionally select **Copy RawBufferView Template** when the project already references `RawBufferVisualizer.Sdk`. The generated code is a starting point; the application remains responsible for buffer lifetime and runtime pixel-format changes.

The normal no-code route is **Save Mapping**. It invokes no object methods and stores per-user mappings in `%APPDATA%\RawBufferVisualizer\type-mappings.json`. A solution may provide a source-controlled `.rawbuffervisualizer.json`, which takes precedence.

Visual Studio stops before executing the highlighted breakpoint statement. If the breakpoint is on `Bitmap bitmap = new Bitmap(...)`, `bitmap` is not initialized yet. Stop on the next executable line, or use **Scan Now** only after the image object exists in the selected stack frame.

### Vision Buffer Doctor

When an image looks sheared, too dark, scrambled, or incorrectly packed, select `Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible width, height, stride, pixel format, valid-bit, and byte-order interpretations from bounded samples. Selecting a candidate applies it immediately without another debugger round trip.

Buffer Doctor is a buffer-layout assistant, not a semantic image detector. RGB/BGR and Bayer phase can remain inherently ambiguous, so the UI keeps tied candidates visible for the developer to confirm.

Incorrect stride metadata shears a dedicated color `BGR24` derivative of the industrial image. Its descriptor reports stride `7344`, while the stored rows use stride `7424` with 80 padding bytes:

![Buffer Doctor ranks interpretations for an industrial PCB image with incorrect stride metadata](docs/images/industrial-pcb-buffer-doctor-before.png)

Selecting the top `BGR24`, stride `7424` interpretation restores both row alignment and the original color scene. Buffer Doctor changes the active interpretation metadata without modifying the paused application's source bytes:

![Buffer Doctor restores the color BGR24 PCB image after applying the correct stride](docs/images/industrial-pcb-buffer-doctor-recovered.png)

## One-Minute Quick Start

1. Install the extension from [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer) and restart Visual Studio.
2. Start debugging and stop where image variables or camera-frame wrappers are alive.
3. Click the `Raw Buffer Visualizer` icon for a registered image or collection in DataTip, Watch, Locals, or Autos. The first visualization opens the docked window automatically. To use automatic inspection before any registered handoff, open `View > Other Windows > Raw Buffer Visualizer`.
4. With the docked window open, let `Auto Inspect on Break` find initialized OpenCvSharp/Emgu Mats, supported exact Mat lists/arrays, and safe unregistered wrappers.
5. Select a thumbnail, zoom or pan, and inspect X/Y, GV or RGB values, raw bytes, stride, and pixel format.
6. If a raw image looks wrong, run `Interpret > Diagnose Buffer` and select the most plausible candidate.

The same workflow works for a single image, a typed `List<TImage>`, `Dictionary<TKey, TImage>`, `ConcurrentDictionary<TKey, TImage>`, a mixed object collection, or a supported image array.

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
- Connect Your Buffer (Smart Type Mapper) as the one-time recovery path for ambiguous company-specific image wrappers.
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

## Install

### Visual Studio compatibility

| Product | Supported versions | Current qualification |
| --- | --- | --- |
| Visual Studio 2022 | `17.9` or newer, x64 | Exact 2.0.6 installed runtime checks passed on `17.14.37516.0`; local 2.0.7 qualification is recorded separately. Exact 17.9 runtime remains unverified because that host is not installed. |
| Visual Studio 2026 | Stable `18.x`, x64 | Exact 2.0.6 installed runtime checks passed on `18.8.12105.206`; 2.0.7 retains the same host/API contract. |

Community, Professional, and Enterprise editions are installation targets. Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

The manifest uses the API range `[17.9,18.0)`. This is compatible with Visual Studio 2026 because VS2026 supports Visual Studio API version 17.x and evaluates the lower bound while ignoring the old product-version upper bound. See [Microsoft's extension compatibility model](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/extension-compatibility?view=visualstudio).

### Required software and optional utilities

Normal extension users need only a supported x64 Visual Studio installation and the Raw Buffer Visualizer extension. Libraries used by the application being debugged remain that application's responsibility.

The in-product **Environment** panel therefore shows only the Visual Studio host, loaded extension version, and writable temporary storage. Select **Environment** again to close it. Contributor and demo-media utilities stay in the development documentation below and are not presented as runtime requirements.

Contributors and release maintainers use additional tools:

| Role | Required tools |
| --- | --- |
| Build and test | Git, Windows PowerShell 5.1, .NET 8 SDK or newer, and Visual Studio 2022 17.9+ with the .NET desktop development workload |
| VS2026 compatibility check | Stable Visual Studio 2026 18.x in addition to the VS2022 baseline |
| Marketplace publish | `VsixPublisher.exe`, normally restored through `Microsoft.VSSDK.BuildTools`, plus Marketplace publisher credentials and approval |
| Demo GIF/MP4 creation | FFmpeg; optional and not used by the product at runtime |

`dotnet restore` supplies the pinned NuGet dependencies, including .NET Framework 4.7.2 reference assemblies, Visual Studio SDK/build packages, OpenCvSharp/Emgu test packages, and SharpGL. Do not install a historical `Microsoft.VSSDK.BuildTools` version to satisfy a hard-coded test path.

See the [development prerequisites and recovery checklist](docs/development-prerequisites.md) for detection commands, installation links, role boundaries, and the current no-silent-install policy.

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

The `1.0.47.0` feature line added Automatic Vision Inspector and Vision Buffer Doctor. Version `1.0.50.0` introduced the current VSPackage identity and the direct-Mat, handoff, menu, and Fit reliability baseline. Version `1.0.52.0` added automatic Mat collection inspection, release highlights, VS2026 activation compatibility, and leased snapshot ownership. Version `1.0.53.0` added Environment Check, bounded cold-page preview sampling, and the mapping foundation. Version `2.0.0.0` added the shared 2D buffer contract, checked fail-closed transfer validation, Continue-time live-source invalidation, and debugger-session handoff gating. Version `2.0.2.0` restored Visual Studio 2022 `17.9+` support and repaired long temporary-path handoff claims. Version `2.0.3.0` added direct concurrent dictionary visualization and fixed the first cold `ImagePtr` handoff. Version `2.0.4.0` moved the full viewer reset directly above the image list as **Clear all**. Version `2.0.5.0` distinguishes source pointers from pixel addresses, preserves debugger expression names, fixes direct image-array registration, enforces complete pointer reads, and corrects negative-stride Bitmap transfer. Version `2.0.6.0` advanced the immutable public package version. Version `2.0.7.0` adds signed 32-bit single-channel matrices without changing the Visual Studio support floor.

For local development builds, close every Visual Studio window and run this from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

The script builds and reinstalls the single VSIX and removes obsolete Raw Buffer Visualizer Classic DLLs from the Visual Studio 2022/2026 `Visualizers` folders. It intentionally does not write VSSDK registration values; the installed VSIX must register the docked ToolWindow by itself. Restart Visual Studio after it finishes.

### Update

Use `Extensions > Manage Extensions > Updates` in Visual Studio. After the update, close all Visual Studio windows and reopen Visual Studio.

If a lower `Raw Buffer Visualizer` tab from version `1.0.34.0` or earlier is still present in a saved Visual Studio layout, close that tab once. Current Marketplace packages publish the debugger providers and automatically close their temporary handoff host, so new invocations remain in the main docked viewer.

Version `2.0.0` passed ordinary installed-VSIX qualification and an in-place update from `1.0.53` without registration repair or `/ResetSkipPkgs`. If an older developer installation left a stale registration, close all Visual Studio windows and use the repair script only as a local migration/recovery step:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

The repair script points the docked tool window back to the currently installed Marketplace extension folder and removes old startup auto-load registrations. After recovery, reinstall the release VSIX without repair and run the installed-VSIX smoke before treating the package as publishable.

If the popup appears only when inspecting an image, check:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\package.log
%APPDATA%\Microsoft\VisualStudio\17.0_...\ActivityLog.xml
```

Versions `1.0.52` through `2.0.1` used the Visual Studio 2022 17.14 floor. Version `2.0.2` restores the Visual Studio 2022 17.9 API floor by separating the in-process VSSDK package from the out-of-process provider while retaining stable Visual Studio 2026 compatibility.

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
var concurrent = new ConcurrentDictionary<string, OpenCvSharp.Mat>();
concurrent["latest"] = resultMat;
var mixed = new object[] { inputBitmap, resultMat };
```

Collection entries retain their debugger root expression and append the item selector, for example `imageList[0]`, `imageDictionary[key]`, `concurrentImageDictionary[key]`, or `bitmapArray[1]`, in the existing docked `Images` list. Valid entries remain normal image rows. Null, unsupported, and failed entries remain visible as red error rows with the reason. One invocation processes at most 256 entries; a collection above that limit adds an error row explaining that only the first 256 entries were shown. Lazy or arbitrary `IEnumerable` sequences are intentionally not enumerated while the debugger is paused.

When an image cannot be opened, select its red error row. The viewer shows a stable error ID, the failure reason, `Copy Report`, and `Open Logs`. `Copy Report` includes the extension and Visual Studio versions, source type, descriptor/diagnostics, and stack details when available. It does not include the image payload. Review paths and variable names in the report before sharing it in a GitHub issue.

Visual Studio requires generic debugger visualizers to register open generic collection types. Raw Buffer Visualizer registers `List<>`, `Dictionary<,>`, and `ConcurrentDictionary<,>` so it appears for typed and mixed supported collections. It transfers supported image entries only; null, unsupported, and failed entries remain visible as error rows. Visual Studio's built-in `IEnumerable Visualizer` can remain in the visualizer menu, so select `Raw Buffer Visualizer` when more than one visualizer is offered.

The toolbar intentionally stays small and uses Visual Studio-native command icons. At narrow widths, Open and Save become icon-only controls with tooltips and accessible names, while Fit and 1:1 retain their labels. **Clear all** remains labelled directly above the image list and is disabled until an image is available. The empty viewer explains how to begin, and detailed debugging controls stay in the Inspector or compact docked inspector.

The docked layout adapts to the available width:

- Narrow: image list, viewer, Save, status strip, and an `Inspector` toggle.
- Medium: image list, viewer, and bottom tab Inspector.
- Wide: image list, viewer, Descriptor, and full right-side Inspector.

## Supported Inputs

| Input | Status | Notes |
| --- | --- | --- |
| `RawBufferSnapshot` | Supported | SDK snapshot from `byte[]`, `ushort[]`, `float[]`, `int[]`, or `IntPtr`. |
| `RawBufferView` | Supported | Pointer-backed wrapper for common camera/frame-grabber image shapes. |
| Unregistered camera/frame wrappers | Conditional | Automatic Inspector supports debugger-visible pointer/array, width, height, stride, and pixel-format shapes. Ambiguous enum/member roles require one saved mapping. |
| Exact ImagePtr compatibility target | Limited | The debugger icon is registered for the existing `Cressem.ImageModel.ImagePtr` contract. Other types should use `RawBufferView` or Automatic Inspector. |
| `System.Drawing.Bitmap` | Supported | 8bpp indexed, 24bpp RGB, and 32bpp RGB/ARGB/PARGB mappings. Its `Scan0 / Pixels` address is captured only while `LockBits` is active. |
| OpenCvSharp `Mat` | Supported | Common 8-bit, 16-bit, 32-bit float, and signed `CV_32SC1` Mat formats. Uses reflection over both legacy and current `Mat` APIs and reports `Ptr` separately from pixel `Data`. |
| Emgu CV `Mat` | Supported | Supports the corresponding common formats, including signed `Cv32S` C1. Extracted by reflection, so the extension does not require a direct Emgu dependency. Reports `Ptr` separately from `DataPointer`. |
| Image collections | Supported | Typed or mixed `List<T>`, `Dictionary<TKey, TValue>`, `ConcurrentDictionary<TKey, TValue>`, `ArrayList`, `Hashtable`, `object[]`, and registered Bitmap/OpenCvSharp/Emgu/raw image arrays. Up to 256 entries are processed per invocation. |
| Automatic Mat collections | Supported | Exact OpenCvSharp/Emgu `Mat` lists and one-dimensional arrays are included automatically; 8 items per collection, 16 items and 8 roots per scan. Bitmap and broad collections remain glyph-owned. |
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
| `Int32` | 4 bytes / pixel | 32 | Signed min/max autoscaled grayscale; inspector retains exact signed value and four raw bytes |
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
| OpenCvSharp `CV_32SC1` | `Int32` |
| Emgu CV `Cv8U`, 1 channel | `Mono8` |
| Emgu CV `Cv8U`, 3 channels | `BGR24` |
| Emgu CV `Cv8U`, 4 channels | `BGRA32` |
| Emgu CV `Cv16U`, 1 channel | `Mono16` |
| Emgu CV `Cv32F`, 1 channel | `Float32` |
| Emgu CV `Cv32S`, 1 channel | `Int32` |

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
- Every open owned snapshot holds a lease, so stale cleanup skips its payload until preview replacement, document removal, Clear, or Tool Window disposal releases it.
- The package log is capped so repeated package-load diagnostics do not grow without bound.

Preview-to-full replacement acquires the new snapshot lease before releasing the previous directory. Closing or removing a document releases and deletes its current owned snapshot; a crashed Visual Studio process leaves an unlocked marker that a later stale sweep can remove.

If disk usage looks high after a crashed debug session, close Visual Studio and delete:

```text
%TEMP%\RawBufferVisualizer\VisualStudio
```

Recorded evidence covers `2.0.0`, previous releases, and historical stress/compatibility runs:

| Check | Result |
| --- | --- |
| Raw Buffer Visualizer `2.0.0` | 1,923,731 bytes; SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`. The complete matrix passed on the same bytes: Release build/self-tests, five Emgu plus five OpenCvSharp versions, build/package/install equality, Break-to-Continue safety, Connect Doctor, installed regressions on VS2022 and VS2026, clean installation, and the `1.0.53.0 -> 2.0.0.0` in-place update. |
| Preserved P0 `2.0.0` safety baseline | 1,917,791 bytes; SHA-256 `3C2DCC1E9E38990D1C17547331E15C5EE344ABEA07D3936B722747B0670AE7EE`. Aggregate tests, the ten-version legacy matrix, and installed Continue invalidation passed on VS2022 `17.14.37516.0` and VS2026 `18.8.12023.21`; see [release-qualification-2.0.0.md](docs/release-qualification-2.0.0.md). Source has advanced, so these bytes are a baseline rather than the current upload asset. |
| Superseded pre-P0 `2.0.0` baseline | 1,914,538 bytes; SHA-256 `2A6D94016B03430BDF2EF5ECCF6282D32896C02AEFB3A9EB8F5519AFE4B13512`. Preserved only as a defect baseline because live rows remained marked `live` after Continue/process exit. |
| Previous `1.0.53` package | 1,914,615 bytes; SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`. Gallery metadata, the downloadable VSIX, manifest `1.0.53.0`, and rendered Overview were verified on 2026-08-05 KST. |
| Previous `1.0.53` installed runtime | The same package passed ReleaseAnnouncement, Environment Check, AutomaticCollections, MultiLibraryHybrid, Smart Type Mapper, persisted mapping, exact menu counts, registration, and protocol diagnostics on VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12023.21`. The durable result is recorded in [release-qualification-1.0.53.md](docs/release-qualification-1.0.53.md). |
| Previous public `1.0.52` package | 1,902,513 bytes; SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`. Its installed-runtime evidence remains in [release-qualification-1.0.52.md](docs/release-qualification-1.0.52.md). |
| Preserved failed `1.0.51` package | 2,011,587 bytes; SHA-256 `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`. VS2022 passed, but stable VS2026 `18.8.2` could not activate the registered provider because the older Extensibility framework requested the unavailable host-contract assembly version `17.0.0.0`. |
| Previous public Marketplace `1.0.50` baseline | Exact downloaded package: 2,001,513 bytes; SHA-256 `2014AA8D679AF3D01F0B16CC304E77064ABCF0B0725BDC6BD543B7C08CDA397E`. It predates automatic Mat collection expansion and the in-product release-highlights banner. |
| Current-source Fit/Manual matrix | 540/900/1160 px passed with aspect errors 0, Fit margin 1.05, and Manual zoom/center delta 0. This is current-source view evidence, not an installed-VSIX Fit behavioral run. |
| Full solution and unit-style self-tests (historical runtime line) | Passed for the declared `net472`, `netstandard2.0`, and .NET 8 targets. |
| Legacy image libraries | Passed with five OpenCvSharp and five Emgu CV package versions plus .NET Framework Bitmap. |
| Standalone viewer interactions | Passed open, pixel/GV read, Fit, 1:1, slider and wheel zoom, PNG/snapshot export, tabs, and linked views. |
| VS2022 docked `5000 x 5000 Mono8` | Passed with `115.3 ms` open path, `1.24 ms` max wheel command, `0.77 ms` max drag command, and `33.94 ms` max frame. |
| Installed VSIX, real `8192 x 8192` Mats (historical) | Passed in VS2022 17.14 with OpenCvSharp and Emgu CV, correct GV values, at most `1 MiB` per new preview file, and controlled `Unavailable` state after debuggee exit. This is not exact `1.0.51` evidence. |
| Installed VSIX, hybrid current-frame session | Exact `1.0.53` passed in VS2022 17.14 and VS2026 18.8 with OpenCvSharp, Emgu CV, Bitmap, and five pointer-backed camera-shape fixtures; 9 images, 0 errors, 8/8 automatic opens, and no duplicate registered-type rows. |
| Dense file-backed `100000 x 100000 Mono8` | Passed with a non-sparse `10,000,000,000` byte payload, `1.73 s` first visible time, and `88.0 MB` working set. |
| Dense file-backed `200000 x 200000 Mono8` | Passed with a non-sparse `40,000,000,000` byte payload, `1.94 s` first visible time, and `87.5 MB` working set. |
| Docked accumulation and cleanup soak | Passed 240 repeated `2048 x 2048 Mono8` opens using both selected-item Delete and Clear, with no positive managed/private/working-set growth, no GDI/USER growth, and no owned temporary directories left behind. |

## Build And Test

Build prerequisites:

- Windows x64, Git, and Windows PowerShell 5.1.
- Visual Studio 2022 17.9 or newer with the .NET desktop development workload for IDE and installed-VSIX validation.
- .NET 8 SDK or newer. The solution does not require the .NET 9 or .NET 10 SDK.
- Internet access for the first NuGet restore, or a previously populated NuGet package cache.

The Visual Studio extension development workload is recommended for IDE-based extension development, but the command-line build restores its required VSSDK build assets through NuGet. Confirm the exact machine state with the checklist in [docs/development-prerequisites.md](docs/development-prerequisites.md) instead of installing an old package version manually.

From a fresh clone, build the solution once before inspecting the generated VSIX payload:

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln -c Release
```

`RawBufferVisualizer.VisualStudio.Vssdk` owns the in-process package, command table, generated `.pkgdef`, docked Tool Window, and composite VSIX. `RawBufferVisualizer.VisualStudio.Extensibility` contributes only the out-of-process debugger visualizer providers under `OutOfProc`.

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

For manual Visual Studio validation, set `RawBufferVisualizer.VisualizerDebuggee` as the startup project and run under the debugger without `--no-break`. The sample creates individual image variables, typed OpenCvSharp/Emgu CV/Bitmap lists, arrays and dictionaries, and mixed object collections so each visualizer path can be checked from Watch, Locals, Autos, or DataTip. Pass `--collection-only` to stop only at collection cases. Pass `--automatic-collections-debug` for one breakpoint containing a five-item OpenCvSharp list with three valid/two failed elements plus a two-item Emgu array.

README and Marketplace screenshots must be reviewed before commit. Do not publish screenshots that include unrelated applications, private desktop content, stale UI, or a feature state that does not match the text.

## Release And Marketplace

The Marketplace extension is distributed through Visual Studio Marketplace. Release validation covers:

- Clean install, update, uninstall, and reinstall of the VSIX.
- Multi-instance isolation: with two separate `devenv.exe` processes running, each debugger visualizer invocation must reach only that Visual Studio instance's docked viewer.
- Docked Visual Studio workflow with narrow and wide tool-window layouts.
- Save PNG, raw snapshot export, pixel status, hover 5x5 statistics, marker values, pan, zoom, high-zoom overlay, error rows, and support-report actions.
- `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, and supported collections.
- Automatic Inspector partial-result behavior, preference persistence, automatic OpenCvSharp/Emgu live opens, Bitmap registered-path ownership, duplicate-free refresh, and Smart Type Mapper recovery.
- Buffer Doctor ranked candidates and immediate descriptor application.
- Large file-backed snapshots and the standalone viewer.
- Package-load smoke after update: Visual Studio must not show `RawBufferVisualizerPackage did not load correctly` on startup.
- Upgrade recovery: update a `1.0.53` profile to `2.0.0`, then prove the View command, Environment Check, registered buffer handoff, automatic Mat, and mapped-buffer workflows without repair or `/ResetSkipPkgs`.
- VSSDK package ownership: the generated `.pkgdef` must reference `RawBufferVisualizer.VisualStudio.Vssdk.dll`; the out-of-process provider must not own a second package registration.
- VSPackage/menu identity: the `2.0.0` `.pkgdef` must contain `{1977574b-f107-465f-bfd1-5fc022907039}`, exactly one `Menus.ctmenu, 2` entry, and no retired `1.0.47`/`1.0.48` GUID.
- View menu: exactly one open command and one current-frame scan command.
- Fit/Manual: Fit remains aspect-correct after resize; wheel, pan, and 1:1 remain Manual and preserve center/scale.
- Hybrid package compatibility: the in-process `RawBufferVisualizer.VisualStudio.Vssdk.dll` must remain on the declared Visual Studio 2022 `17.9` SDK floor; debugger providers remain out of process.

See [docs/marketplace-checklist.md](docs/marketplace-checklist.md) for the release checklist.
For repeatable Marketplace updates, use [docs/release-runbook.md](docs/release-runbook.md). The `Marketplace CD` GitHub Actions workflow builds and validates by default, and publishes only when `publish=true` is selected with the Marketplace environment approval.
Marketplace feature Overview: [2.0.7 Overview](docs/marketplace-overview-2.0.7.md).
Korean review copy: [2.0.7 Overview (Korean)](docs/marketplace-overview-2.0.7.ko.md).
Marketplace release text: [2.0.7 release notes](docs/marketplace-release-notes-2.0.7.md).
Complete user-visible history: [CHANGELOG](CHANGELOG.md).
For the short product video, follow the [fast demo recording guide](docs/demo-recording-guide.md).

## License

Copyright (c) 2026 Noah Choi.

This project is licensed under the MIT License. You may use, modify, and redistribute the source code, but the copyright and license notice must remain included. See [LICENSE](LICENSE).

External libraries keep their own licenses. Review [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) before publishing a VSIX, release package, or redistributed binary.

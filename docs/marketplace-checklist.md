# Visual Studio Marketplace Checklist

Use this before publishing each Marketplace update.

## Package

Build a Release VSIX:

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472
```

Expected output:

```text
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472.zip
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

## Listing Metadata

Suggested Marketplace fields:

| Field | Value |
| --- | --- |
| Display name | Raw Buffer Visualizer |
| Publisher/author | Noah Choi |
| Short description | Inspect Bitmap, OpenCvSharp Mat, Emgu CV Mat, image collections, IntPtr, and raw buffers inside Visual Studio. |
| Type | Tools |
| Categories | Debugging, Other Tools |
| Tags | image-watch, csharp, opencvsharp, machine-vision, raw-buffer, emgucv |
| License | MIT |
| Release stage | Preview for the first Marketplace upload |

Keep the product name stable and put the search-oriented positioning in the short description and first Overview paragraph. Keep the extension ID unchanged.

## GitHub Discovery

Recommended repository description:

```text
Image Watch for C# machine vision: inspect OpenCvSharp Mat, Emgu CV Mat, Bitmap, IntPtr, and raw image buffers inside Visual Studio.
```

Recommended repository homepage:

```text
https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer
```

Recommended topics:

```text
csharp, visual-studio, visual-studio-extension, opencvsharp, emgucv,
machine-vision, computer-vision, image-debugger, debugger-visualizer,
raw-buffer, intptr, industrial-camera, bitmap
```

Create the GitHub Release from tag `v1.0.40` using [github-release-1.0.40.md](github-release-1.0.40.md). Point installation to Marketplace rather than attaching a second user-facing VSIX distribution path.

Record the first product demo with [demo-recording-guide.md](demo-recording-guide.md). Do not publish a simulated animation; the capture must show the real Visual Studio debugger workflow.

## Launch Post Copy

Use the demo GIF with this title:

```text
Raw Buffer Visualizer: inspect OpenCvSharp Mat, Bitmap, and IntPtr buffers inside Visual Studio
```

Suggested post:

```text
I built Raw Buffer Visualizer for C# machine-vision debugging.

It lets you inspect OpenCvSharp Mat, Emgu CV Mat, System.Drawing.Bitmap,
IntPtr-backed images, and raw buffers at a Visual Studio breakpoint without
saving temporary files or adding debug-only conversion code.

The docked viewer includes pixel values, raw bytes, stride/format diagnostics,
multiple-image comparison, and file-backed display for very large payloads.

Marketplace: https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer
Source: https://github.com/Noah8218/RawBufferVisualizer
```

Lead with the debugger workflow, not the large-image benchmark. Publish only in C#, OpenCvSharp, computer-vision, or machine-vision communities whose self-promotion rules allow project posts.

Overview copy:

```markdown
Stop saving temporary images or writing debug-only conversion code. Inspect C# image variables directly while stopped at a breakpoint.

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision developers. It combines a C# image debugger, OpenCvSharp and Emgu CV visualizer, typed image collection visualizer, IntPtr image viewer, and raw image buffer inspector directly inside Visual Studio.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## One-Minute Quick Start

1. Install the extension and restart Visual Studio.
2. Start debugging and stop where an image variable is alive.
3. Click the visualizer icon for an image, typed list, dictionary, or supported array in DataTip, Watch, Locals, or Autos.
4. Select a thumbnail added to the docked Raw Buffer Visualizer window.
5. Zoom, pan, and inspect X/Y, GV or RGB values, raw bytes, stride, and pixel format.

## Why Raw Buffer Visualizer?

| Capability | Typical basic Mat viewer | Raw Buffer Visualizer |
| --- | --- | --- |
| OpenCvSharp `Mat` | Common | Supported |
| Emgu CV `Mat` and `System.Drawing.Bitmap` | Varies | Supported |
| Typed `List<TImage>` and `Dictionary<TKey,TImage>` | Varies | Supported |
| `IntPtr` and raw image buffers | Limited | Supported |
| Stride, byte order, valid bits, and raw-byte diagnostics | Limited | Supported |
| `Mono10PackedLsb` and `Mono12PackedLsb` | Uncommon | Supported |
| Multiple inspected images in one docked list | Varies | Supported |
| Split, absolute diff, blink, and linked pan/zoom | Varies | Supported |
| File-backed tiled display for very large payloads | Uncommon | Supported |

The docked image list keeps inspected values in one place. Select a thumbnail to compare variables without opening a separate viewer window for each image. Other visualizers vary; this table describes the difference between a basic Mat-only workflow and the features implemented here.

## Key Features

- Single docked Visual Studio window where inspected images accumulate in an image list
- Typed and mixed image lists, dictionaries, and supported arrays append their entries to that same docked list
- Thumbnail preview for each inspected variable
- Width, height, stride, pixel format, source type, and diagnostics
- Pixel inspection with X/Y position, GV/RGB values, channel swatches, and raw bytes
- Zoom, pan, fit, 1:1 view, and high-zoom pixel value overlay
- Raw buffer diagnostics for stride, buffer size, valid bits, byte order, and format interpretation
- PNG export and raw snapshot export
- File-backed tiled viewer for very large raw payloads

## Supported Inputs

- `RawBufferSnapshot`
- `RawBufferView`
- ImagePtr-style pointer objects
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`
- Emgu CV `Mat`
- Typed or mixed `List<T>`, `Dictionary<TKey,TValue>`, `ArrayList`, `Hashtable`, and supported image arrays
- `.rbuf.json` + `.raw` snapshot files

OpenCvSharp `Mat` transfers were validated with OpenCvSharp4 `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627`.

Emgu CV `Mat` transfers were validated with Emgu CV `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924`.

## Supported Pixel Formats

- `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`
- `Binary`
- `RGB24`, `BGR24`, `BGRA32`
- `Float32`
- `BayerRGGB8`, `BayerGRBG8`, `BayerGBRG8`, `BayerBGGR8`

## Typical Workflow

1. Start debugging in Visual Studio.
2. Stop at a breakpoint where an image variable is alive.
3. Click the debugger visualizer icon for a supported image or collection from DataTip, Watch, Locals, or Autos.
4. The image, or each supported collection entry, is appended to the docked Raw Buffer Visualizer window.
5. Inspect pixels, raw bytes, stride, format, and diagnostics.

Failed values remain visible as error rows, so unsupported formats or invalid buffers do not disappear silently.

![Failed image values remain visible with diagnostics](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/viewer-vs-docked-error.png)

## Large Image Support

The viewer uses file-backed tiled rendering for large raw payloads. Dense Mono8 payloads at `100000 x 100000` and `200000 x 200000` were exercised without loading the complete payload into managed memory.

## Known Limits

- A collection visualization processes the first 256 entries.
- Open generic registration makes Raw Buffer Visualizer available for typed lists and dictionaries. Only supported image entries are transferred; null, unsupported, and failed entries remain visible as error rows.
- Visual Studio's built-in `IEnumerable Visualizer` may remain in the visualizer menu. Select `Raw Buffer Visualizer` for the docked image-list workflow.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Tested library versions are compatibility points, not a guarantee for every intermediate package build.

## License

Raw Buffer Visualizer is licensed under the MIT License. External libraries retain their own licenses; see `THIRD-PARTY-NOTICES.md` in the source repository.
```

## Required Screenshots

Keep these files current for the listing:

```text
docs\images\marketplace-icon.png
docs\images\viewer-vs-docked.png
docs\images\viewer-vs-docked-overlay.png
docs\images\viewer-vs-docked-error.png
docs\images\raw-buffer-visualizer-demo.gif
```

The first Marketplace media item must show the reviewed Visual Studio debugger workflow GIF, not the standalone viewer.

Screenshot gate:

- Open every README and Marketplace screenshot before commit.
- Reject screenshots that include unrelated applications, browser tabs, private desktop content, stale UI, or a feature state that does not match the listing text.
- Prefer cropped ToolWindow-focused screenshots for the Visual Studio workflow.
- Store review evidence under `artifacts\ui\...` when screenshots are replaced.

## Required Validation

Run before uploading:

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --no-build
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLegacyImageCompatibility.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 100000 -Height 100000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 200000 -Height 200000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -PixelFormat Mono16 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat BGR24 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat Float32 -Width 320 -Height 240
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild
```

The docked smoke must validate:

- One Visual Studio docked ToolWindow.
- Single image list accumulation.
- Single images and supported collections append only to the main docked image list; no debugger invocation opens a second lower ToolWindow.
- Error rows remain visible with a red error thumbnail, `Open failed` title, reason, and Diagnostics entry.
- Narrow docked layout keeps the Inspector collapsed by default and preserves image list, viewer, Save, and status strip access.
- Medium and wide layouts expose compact tab Inspector and full Inspector respectively.
- Save visible PNG, raw snapshot export path, pixel status, raw bytes, hover 5x5 statistics, selected/pinned marker, pan, zoom, drag, and wheel interaction.
- Non-blank framebuffer capture.
- `Publish-VisualStudioExtension.ps1` must pass its VSSDK compatibility guard: `RawBufferVisualizer.VisualStudio.Vssdk.dll` must not reference `Microsoft.VisualStudio.Threading` newer than `17.9.0.0`.

## Install, Update, Uninstall, Reinstall

Use the install script for developer verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

Manual smoke checklist:

- Install from the generated VSIX.
- Restart Visual Studio.
- Confirm `Raw Buffer Visualizer` appears in `Extensions > Manage Extensions > Installed`.
- Debug `RawBufferVisualizer.VisualizerDebuggee`.
- Inspect `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, typed OpenCvSharp/Emgu CV/Bitmap lists and dictionaries, `imageList`, `imageDictionary`, and `imageArray`; confirm every result uses the same upper docked viewer.
- Inspect a mixed `object[]` containing a valid image, `null`, and an unsupported object; confirm the valid image row and per-item error rows appear together in the upper `Images` list.
- Start two separate Visual Studio `devenv.exe` processes, invoke the visualizer in each process, and confirm each snapshot appears only in the docked viewer belonging to the process that invoked it.
- Close Visual Studio.
- Install the same VSIX again and confirm update/reinstall path does not leave a broken package.
- Uninstall from Manage Extensions.
- Restart Visual Studio and confirm the visualizer is gone.
- Reinstall and repeat one debugger inspection.
- For scripted developer smoke, verify uninstall removes the extension manifest from `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<instance>\Extensions`, then reinstall with `Install-VisualStudioExtension.ps1 -Reinstall`.
- After update, restart Visual Studio once with no solution open and confirm no `RawBufferVisualizerPackage did not load correctly` popup appears.
- If a PC already has stale VSSDK registration, close Visual Studio and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

This rewrites the VSSDK package `CodeBase` to the current installed VSIX folder and removes old startup autoload registration.

## Marketplace CD

Use [release-runbook.md](release-runbook.md) for repeatable updates.

Version bump:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 1.0.40
```

GitHub setup:

| Setting | Kind | Notes |
| --- | --- | --- |
| `VS_MARKETPLACE_TOKEN` | Secret | Azure DevOps PAT with Marketplace manage permission. |
| `VS_MARKETPLACE_PUBLISHER` | Repository variable | Marketplace publisher ID, not display name. |
| `visual-studio-marketplace` | Environment | Add required reviewer approval before publish. |

Workflow:

1. Run `Actions > Marketplace CD` with `publish=false`.
2. Verify the generated VSIX artifact and `vs-publish.json`.
3. Run again with `publish=true`.
4. Approve the `visual-studio-marketplace` environment.
5. Wait for Marketplace propagation.
6. Test Visual Studio update from `Extensions > Manage Extensions > Updates`.
7. Verify the installed version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.40.0
```

## Release Notes Template

For the current update, paste [marketplace-release-notes-1.0.40.md](marketplace-release-notes-1.0.40.md) into the Marketplace release notes field.

## Evidence Artifacts

Keep these artifacts with the release validation notes:

```text
artifacts\perf\vs-docked\visual-studio-docked-performance.json
artifacts\perf\vs-docked\visual-studio-docked-session.json
artifacts\perf\vs-docked\visual-studio-docked-session.png
artifacts\perf\vs-docked\visual-studio-docked-framebuffer.png
artifacts\ui\docked-layout-widths\layout-widths.json
```

## Do Not Ship If

- User-facing text still mentions rendering implementation details.
- Narrow Visual Studio docking hides Save, image list, viewer, status strip, or Inspector access.
- Debugger inspections open multiple independent viewer windows instead of one docked image list.
- The generated `.vsextension\extension.json` is missing `IDebuggerVisualizerProvider`, a required Modern debugger visualizer provider, or the open generic `List<>`/`Dictionary<,>` collection targets.
- A debugger snapshot invoked from one Visual Studio process appears in another Visual Studio process's docked viewer.
- Install/update/uninstall/reinstall has not been checked.
- The README or listing does not include the MIT license and third-party notice requirement.
- Visual Studio shows `RawBufferVisualizerPackage did not load correctly` after updating and restarting.
- Visual Studio shows `RawBufferVisualizerPackage did not load correctly` when inspecting an image on a VS 2022 17.9-17.13 machine.

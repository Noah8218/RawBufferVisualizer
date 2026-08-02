# Visual Studio Marketplace Checklist

Use this before publishing each Marketplace update.

## Package

Build a Release VSIX:

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.52
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -PublishRoot D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate
```

Expected output:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472.zip
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

## Listing Metadata

Suggested Marketplace fields:

| Field | Value |
| --- | --- |
| Display name | Raw Buffer Visualizer |
| Publisher/author | Noah Choi |
| Short description | Image Watch-style C# debugging with Bitmap/Mat inspection, automatic camera-frame discovery, and raw-buffer diagnosis. |
| Type | Tools |
| Categories | Debugging, Other Tools |
| Tags | image-watch, csharp, opencvsharp, machine-vision, raw-buffer, emgucv, image-debugger |
| License | MIT |
| Release stage | Preview for the first Marketplace upload |
| Supported Visual Studio | Visual Studio 2022 `17.14+` and stable Visual Studio 2026 `18.x`; Community/Professional/Enterprise x64 |

Keep the product name stable and put the search-oriented positioning in the short description and first Overview paragraph. Keep the extension ID unchanged.

Every Overview must distinguish the technical support range from exact runtime evidence:

- technical/API floor: Visual Studio 2022 `17.14`;
- stable VS2026 target: `18.x`, using Microsoft's VSIX API-version compatibility model;
- exact installed IDE builds: list only versions that passed the current installed-VSIX matrix;
- excluded release targets: Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32-bit Visual Studio, and Preview/Insiders builds.

The `1.0.52` manifest range `[17.14,18.0)` is valid for VS2026 because VS2026 supports API version 17.x and ignores the upper bound. The `17.14` lower bound is intentional and matches the Extensibility runtime dependency; any later manifest edit produces a new exact package and requires full requalification.

For every update, the binary version, `CHANGELOG.md`, dedicated Marketplace Overview, Marketplace/GitHub release notes, embedded VSIX `ReleaseNotes.txt`, and in-product `ReleaseAnnouncement.cs` version must agree. The communication test above enforces that contract. Confirm the first Tool Window open shows the current non-modal highlights, **Dismiss** survives a Visual Studio restart, and **What's New** can reopen them without triggering a scan.

## GitHub Discovery

Recommended repository description:

```text
Image Watch-style debugging for C# machine vision with Mat/Bitmap inspection, automatic camera-frame discovery, and raw-buffer diagnosis.
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

For the `1.0.52` update, use [marketplace-release-notes-1.0.52.md](marketplace-release-notes-1.0.52.md). Point installation to Marketplace rather than attaching a second user-facing VSIX distribution path.

The GitHub tag workflow uses that same file as its curated Release body. It attaches only the standalone viewer; do not attach the Visual Studio VSIX to GitHub Releases.

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

Current Overview copy:

[Raw Buffer Visualizer 1.0.52 Marketplace Overview](marketplace-overview-1.0.52.md)

The block below is the published 1.0.45 baseline and is retained only for historical comparison. Do not paste it for `1.0.52`.

Historical 1.0.45 Overview copy:

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

## Large Image Performance

The same-machine automated viewer benchmark below used the same dense `5000 x 5000 Mono8` input before and after the progressive viewport update. Lower values are better.

| Metric | Before update | Current (`1.0.45`) | Improvement |
| --- | ---: | ---: | ---: |
| Initial open path | `179.818 ms` | `115.369 ms` | `35.8%` lower |
| Zoom average frame | `16.684 ms` | `13.765 ms` | `17.5%` lower |
| Zoom maximum frame | `49.397 ms` | `36.527 ms` | `26.1%` lower |
| Pan maximum frame | `21.951 ms` | `17.056 ms` | `22.3%` lower |
| Pan average tile upload | `20.410 ms` | `15.211 ms` | `25.5%` lower |

An installed-VSIX check in Visual Studio 2022 `17.14` also exercised a dense file-backed `24000 x 24000 Mono8` image with `87` wheel and `269` drag events. Input handling averaged `0.095 ms` for wheel and `0.033 ms` for drag; rendered frames averaged `6.515 ms` with a `13.642 ms` maximum. Results vary with image format, storage, GPU driver, debugger state, and docked-window size.

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
docs\images\automatic-vision-inspector.png
docs\images\vision-buffer-doctor.png
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
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario BufferDoctor -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 100000 -Height 100000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 200000 -Height 200000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -PixelFormat Mono16 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat BGR24 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat Float32 -Width 320 -Height 240
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild -OutputDir D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\fit-stability
```

The docked smoke must validate:

- One Visual Studio docked ToolWindow.
- Single image list accumulation.
- Single images and supported collections append only to the main docked image list; no debugger invocation opens a second lower ToolWindow.
- Error rows remain visible with a red error thumbnail, `Open failed` title, stable error ID, reason, `Copy Report`, and `Open Logs`.
- Support reports contain actionable version/source/error context and explicitly exclude image payloads; selecting a valid row after an error restores the normal viewer.
- Narrow docked layout keeps the Inspector collapsed by default and preserves image list, viewer, Save, and status strip access.
- Medium and wide layouts expose compact tab Inspector and full Inspector respectively.
- Save visible PNG, raw snapshot export path, pixel status, raw bytes, hover 5x5 statistics, selected/pinned marker, pan, zoom, drag, and wheel interaction.
- Non-blank framebuffer capture.
- Explicit Fit/Manual behavior: new image/Fit/double-click enter Fit; wheel/pan/1:1 enter Manual; resize keeps Fit aspect/margin or Manual center/scale.
- `Publish-VisualStudioExtension.ps1` must prove that `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef` was generated from the current hybrid project and points its `CodeBase` to `RawBufferVisualizer.VisualStudio.Extensibility.dll`.
- The VSIX must not contain the former `RawBufferVisualizer.VisualStudio.Vssdk.pkgdef`.
- The current registration must use Package GUID `{1977574b-f107-465f-bfd1-5fc022907039}`, contain exactly one `Menus.ctmenu, 2` registration, and reject retired `{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}`.
- Package, menu, and ToolWindow registrations must each occur exactly once; each VS instance must have one main extension manifest and no legacy split VSSDK extension.
- `RawBufferVisualizer.VisualStudio.Extensibility.dll` must not reference `Microsoft.VisualStudio.Threading` newer than the declared Visual Studio 2022 `17.14` floor.
- The current release record must include installed-VSIX results for a serviced VS2022 `17.14` instance and stable VS2026 `18.x` when both environments are available. If one is unavailable, Marketplace copy must say which exact runtime qualification remains pending.
- `Test-VisualStudioMarketplaceUpdate.ps1` must report no per-machine historical Raw Buffer Visualizer under the selected VS2026 `Common7\IDE\VSExtensions` root. If it finds one, remove/update it through Visual Studio tooling with administrator rights and rerun; never qualify by manually deleting `Program Files` content.

## Install, Update, Uninstall, Reinstall

Use the install script for developer verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

The script closes no active Visual Studio window: all `devenv.exe` processes must already be closed. It stops idle `Microsoft.ServiceHub.Controller` processes between quiet VSIXInstaller operations so uninstall descendants cannot block the following install. Exit code `2004` requires checking the newest `%TEMP%\dd_VSIXInstaller_*.log` for the named blocker.

Manual smoke checklist:

- Install from the generated VSIX.
- Restart Visual Studio.
- Confirm `Raw Buffer Visualizer` appears in `Extensions > Manage Extensions > Installed`.
- Open the View menu and confirm exactly one `Raw Buffer Visualizer` and one `Raw Buffer Visualizer: Scan Current Frame` command.
- Debug `RawBufferVisualizer.VisualizerDebuggee`.
- Place the breakpoint after image construction. Confirm initialized OpenCvSharp/Emgu Mats open automatically on Break and **Scan Now**, Bitmap stays on its registered visualizer glyph, and repeated scans do not duplicate rows.
- Inspect `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, typed OpenCvSharp/Emgu CV/Bitmap lists and dictionaries, `imageList`, `imageDictionary`, and `imageArray`; confirm every result uses the same upper docked viewer.
- Inspect a mixed `object[]` containing a valid image, `null`, and an unsupported object; confirm the valid image row and per-item error rows appear together in the upper `Images` list.
- Start two separate Visual Studio `devenv.exe` processes, invoke the visualizer in each process, and confirm each snapshot appears only in the docked viewer belonging to the process that invoked it.
- Close Visual Studio.
- Install the same VSIX again and confirm update/reinstall path does not leave a broken package.
- Uninstall from Manage Extensions.
- Restart Visual Studio and confirm the visualizer is gone.
- Reinstall and repeat one debugger inspection.
- For scripted developer smoke, verify uninstall removes the extension manifest from `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<instance>\Extensions`, then reinstall with `Install-VisualStudioExtension.ps1 -Reinstall`.
- After update, restart Visual Studio once with no solution open and confirm no `RawBufferVisualizerPackage did not load correctly` popup appears.
- Mandatory update gate: on a separate eligible PC, begin with the exact public `1.0.50` package, update to the exact `1.0.52` candidate without uninstall, repair, or `/ResetSkipPkgs`, and prove the View/menu, Bitmap handoff, automatic Mat, automatic Mat collection, and Fit/Manual workflows.
- Run `Test-VisualStudioMarketplaceUpdate.ps1`, then run the installed-VSIX `AutomaticVisionInspector`, `AutomaticCollections`, and `MultiLibraryHybrid` scenarios. A successful real handoff is the registration acceptance test.
- Confirm the normal install output contains no manual package-registration step.
- If a developer PC already has stale VSSDK registration, close Visual Studio and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

This rewrites the VSSDK package `CodeBase` to the current installed VSIX folder and removes old startup autoload registration. It is a recovery tool only. If a clean PC needs it, the release fails. See [vsix-package-registration.md](vsix-package-registration.md).

## Marketplace CD

Use [release-runbook.md](release-runbook.md) for repeatable updates.

Version bump:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 1.0.52
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
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.52.0
```

## Release Notes Template

For the current update, paste [marketplace-release-notes-1.0.52.md](marketplace-release-notes-1.0.52.md) into the Marketplace release notes field.

## Evidence Artifacts

Keep these artifacts with the release validation notes:

```text
artifacts\perf\vs-docked\visual-studio-docked-performance.json
artifacts\perf\vs-docked\visual-studio-docked-session.json
artifacts\perf\vs-docked\visual-studio-docked-session.png
artifacts\perf\vs-docked\visual-studio-docked-framebuffer.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\fit-stability\layout-widths.json
artifacts\ui\installed-vsix-new-features\AutomaticVisionInspector-installed-vsix.json
artifacts\ui\installed-vsix-new-features\AutomaticCollections-installed-vsix.json
artifacts\ui\installed-vsix-new-features\BufferDoctor-installed-vsix.json
artifacts\ui\installed-vsix-new-features\SmartTypeMapper-installed-vsix.json
artifacts\ui\installed-vsix-new-features\MultiLibraryHybrid-installed-vsix.json
```

## Do Not Ship If

- `Publish-VisualStudioMarketplace.ps1` was invoked without the exact explicit `-VsixPath`, or its manifest version does not match the current source release.
- User-facing text still mentions rendering implementation details.
- Narrow Visual Studio docking hides Save, image list, viewer, status strip, or Inspector access.
- Debugger inspections open multiple independent viewer windows instead of one docked image list.
- The generated `.vsextension\extension.json` is missing `IDebuggerVisualizerProvider`, a required Modern debugger visualizer provider, or the open generic `List<>`/`Dictionary<,>` collection targets.
- A debugger snapshot invoked from one Visual Studio process appears in another Visual Studio process's docked viewer.
- Install/update/uninstall/reinstall has not been checked.
- The README or listing does not include the MIT license and third-party notice requirement.
- Visual Studio shows `RawBufferVisualizerPackage did not load correctly` after updating and restarting.
- The listing or manifest offers `1.0.52` to a VS2022 `17.9`-`17.13` machine even though the candidate depends on the `17.14` Extensibility runtime.
- The installed debugger host reports `did not acknowledge the image handoff`.
- Handoff success can be reported without an explicit ACK, or a NACK reason is unavailable.
- The generated `.pkgdef` is owned by the VSSDK support project, points to `RawBufferVisualizer.VisualStudio.Vssdk.dll`, or is absent from the VSIX.
- Package, `Menus.ctmenu, 2`, or ToolWindow registration is missing or duplicated.
- The View menu contains anything other than one open command and one current-frame scan command.
- A local install passes only after `Repair-VisualStudioExtensionRegistration.ps1` or another manual registry write.
- README and the 1.0.52 Marketplace Overview disagree about Automatic Inspector's automatic Mat/registered Bitmap boundary.
- Initialized OpenCvSharp/Emgu Mats do not each create one live automatic row, Bitmap/RawBufferSnapshot/RawBufferView creates an automatic row, or **Scan Now** duplicates rows.
- Fit changes image aspect after resize or Manual resize resets zoom/center.
- The update from public `1.0.50` to candidate `1.0.52` requires uninstall, repair, `/ResetSkipPkgs`, or another recovery action.
- The unchanged candidate has not passed the stable VS2026 installed-runtime core matrix.

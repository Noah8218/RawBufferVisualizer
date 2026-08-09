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

Visual Studio stops before executing the highlighted breakpoint statement. Do not place an automatic-inspection breakpoint on the image construction/assignment line; stop on the next executable line so the variable is initialized.

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
| 18 | `contiguousMonoFrame.View` | Opens through the vendor-neutral object wrapper as `Mono8`. |
| 19 | `bgrPointerFrame.View` | Opens through the vendor-neutral object wrapper as `BGR24`. |
| 20 | `bayerPointerFrame.View` | Opens through the vendor-neutral object wrapper as `BayerRGGB8`. |
| 21 | `mono16BoardBuffer.View` | Opens through the vendor-neutral object wrapper as `Mono16`. |
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
| 35 | `imageList` | Appends eight mixed image entries with `[index]` names. |
| 36 | `imageDictionary` | Appends four mixed image entries with `[key]` names. |
| 37 | `imageArray` | Appends four mixed entries from `object[]`. |

For SDK-style objects, the visualizer target is the `RawBufferView` property, so add the exact `.View` expression to Watch when the visualizer icon is not shown directly on the parent object.

## Automatic Inspector 1.0.50 Scenario

Run the installed debuggee with `--multi-library-debug`, or use the installed-VSIX automation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 `
  -Scenario MultiLibraryHybrid `
  -Configuration Release `
  -NoBuild `
  -NoInstall
```

Expected at the initialized breakpoint:

1. **Auto Inspect on Break** opens exact OpenCvSharp `Mat` and Emgu CV `Mat` values through live process memory.
2. The camera-wrapper fixtures also open automatically; status is `8 detected: 8 opened, 0 need mapping, 0 failed.`
3. **Scan Now** refreshes the same eight automatic rows without duplication.
4. `System.Drawing.Bitmap` does not create an automatic row. Click its registered visualizer glyph.
5. Final state is nine documents and zero errors.
6. `RawBufferSnapshot` and `RawBufferView` remain registered-path owned.

OpenCvSharp/Emgu automatic capture supports fixed metadata for `8U C1/C3/C4`, `16U C1`, and `32F C1`. Bitmap remains glyph-only because Automatic Inspector does not inject a `LockBits`/`UnlockBits` lifecycle into the debuggee.

## Automatic Mat Collections Scenario

Run only after unlocking the interactive Windows desktop:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 `
  -Scenario AutomaticCollections `
  -Configuration Release
```

The script backs up and restores the current Automatic Inspector preference file, enables collection inspection for the isolated session, installs the current VSIX, and stops at `--automatic-collections-debug`.

Expected:

1. `partialOpenCvMatList` produces five indexed rows: three `[Auto]` and two `[Failed]` for the null/disposed elements.
2. `emguMatArray` produces two `[Auto]` rows.
3. Overall status is seven detected, five opened, zero mapping candidates, and two failed.
4. The per-collection status reports `5 inspected, 3 opened, 2 failed` and `2 inspected, 2 opened`.
5. A second **Scan Now** leaves seven rows and two errors without duplication.
6. The persisted collection option is restored enabled inside the test session, then the pre-test user settings file is restored during cleanup.

## Real Industrial Image Scenario

Use a user-owned or license-cleared photograph and follow [Industrial Image Debug Testing](industrial-image-testing.md). Run the debuggee with:

```text
--industrial-image-debug "D:\path\industrial-image.jpg"
```

Expected at the initialized breakpoint:

1. `industrialOpenCvMat`, `industrialEmguMat`, and `industrialFrame` preserve the same recognizable color scene through three supported memory representations.
2. `industrialBitmap`, `industrialBgrSnapshot`, and `industrialMonoSnapshot` open through their registered debugger visualizers.
3. `industrialBadStrideSnapshot` initially shows the real color scene with a stride defect; Buffer Doctor ranks `BGR24`, 2448 x 2048, stride 7424 first, and applying it restores the color image.
4. Pixel hover, Fit, 1:1, wheel zoom, and pan remain usable on the real photograph.
5. Capture evidence records the source page, license, downloaded-file SHA-256, Visual Studio version, and exact installed VSIX.

## UI Checks

- The Visual Studio docked visualizer shows the image list, source type, dimensions, pixel format, byte count, diagnostics, and generated metadata path.
- The visualizer is docked inside Visual Studio. `Continue` and `Step Over` must still move to the next breakpoint while the docked viewer stays open.
- Repeated inspections append rows to the same docked `Images` list instead of opening separate standalone windows.
- `Images` rows show thumbnails and can be clicked comfortably.
- The descriptor fields are read-only display text.
- A new/selected image, `Fit`, and viewer double-click enter Fit mode. Resize remains aspect-correct with the Fit margin intact.
- Mouse wheel zoom, drag pan, and `1:1` enter Manual mode; resize preserves the manual zoom and image center.
- Pixel hover shows coordinate, decoded value, raw bytes, 5x5 neighborhood, and line profile.
- `Interpret` can change format, stride, valid bits, and byte order without editing source code.
- `Compare` can set A/B, use linked pan/zoom, create a diff view, and blink between A and B.
- A failed open or malformed descriptor remains visible in `Images` as an error row and shows the reason in diagnostics.
- The View menu contains exactly one `Raw Buffer Visualizer` and one `Raw Buffer Visualizer: Scan Current Frame` command.

## Failure Checks

1. Close Visual Studio.
2. Reinstall the VSIX:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Reinstall
```

3. Reopen Visual Studio and inspect a supported variable.
4. Expected: the docked visualizer opens without requiring `RAW_BUFFER_VISUALIZER_VIEWER`; the producer receives explicit ACK only after the document opens.
5. Force or inspect an open failure when practical. Expected: the producer receives NACK with a readable rejection reason rather than treating request-file disappearance as success.

## Evidence To Capture

- One screenshot of the Visual Studio docked visualizer after a successful launch.
- One screenshot of the same docked visualizer with at least two images in the `Images` list.
- One screenshot showing pixel hover text and zoom percentage.
- One screenshot showing the Inspector with diagnostics or Try interpretation.
- `MultiLibraryHybrid-installed-vsix.json` showing View-menu counts 1/1, automatic status 8/8, nine documents, and zero errors.
- The exact VSIX SHA-256, Visual Studio/Windows versions, `package.log`, and relevant `ActivityLog.xml`.
- One note for any unsupported SDK type, including the exact .NET type full name and SDK version.

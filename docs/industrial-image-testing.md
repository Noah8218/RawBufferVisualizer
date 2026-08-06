# Industrial Image Debug Testing

Use this scenario when a human-readable debugger demonstration is more useful than the deterministic stripe and gradient fixtures. The existing procedural fixtures remain the source of truth for exact pixel-format, packing, and stride regression checks.

## Approved Portfolio Asset

| Item | Value |
| --- | --- |
| Image | `Printed circuit boards 20240831 083726.jpg` |
| Author | IMG 4512 |
| Source page | https://commons.wikimedia.org/wiki/File:Printed_circuit_boards_20240831_083726.jpg |
| License | CC0 1.0 Universal public-domain dedication |
| Test derivative | Wikimedia Commons 1280 x 960 preview |
| Local SHA-256 | `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46` |
| Local test path | `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260806\source\Printed_circuit_boards_20240831_083726-1280.jpg` |

The image is not committed to the repository. Keep the source page, author, license, exact downloaded-file hash, and capture date with any published screenshot. CC0 does not require attribution, but retain it for traceability. Product screenshots do not imply affiliation with or endorsement by any product marks incidentally visible in the source photograph.

Do not use MVTec AD or MVTec AD 2 images in Marketplace or portfolio material. Their current dataset terms include a non-commercial restriction.

## Fast Conversion Check

```powershell
dotnet build .\samples\RawBufferVisualizer.VisualizerDebuggee\RawBufferVisualizer.VisualizerDebuggee.csproj -c Debug

.\.build\bin\RawBufferVisualizer.VisualizerDebuggee\Debug\net472\RawBufferVisualizer.VisualizerDebuggee.exe `
  --industrial-image-debug "D:\path\industrial-image.jpg" `
  --no-break
```

Expected output names the decoded dimensions, `BGR24/Mono8`, and the Buffer Doctor diagnostic geometry, then exits with code `0`.

## Visual Studio Check

Run the same executable under the Visual Studio debugger without `--no-break`:

```text
--industrial-image-debug "D:\path\industrial-image.jpg"
```

At the initialized break, inspect:

| Variable | Purpose |
| --- | --- |
| `industrialBitmap` | Registered `System.Drawing.Bitmap` path |
| `industrialOpenCvMat` | OpenCvSharp `CV_8UC3` path |
| `industrialEmguMat` | Emgu CV 8-bit, three-channel path |
| `industrialFrame` | Vendor-neutral pointer-backed `BGR24` wrapper |
| `industrialBgrSnapshot` | Owned `BGR24` snapshot |
| `industrialMonoSnapshot` | Grayscale `Mono8` snapshot |
| `industrialBadStrideSnapshot` | Same grayscale scene resized to the existing 2448 x 2048 Buffer Doctor regression geometry, with 112 bytes of row padding and an intentionally wrong descriptor stride |

Verify that the real scene remains recognizable, the normal variables match the decoded image dimensions, pixel hover changes with position, and Fit and 1:1 work. For `industrialBadStrideSnapshot`, verify that Buffer Doctor ranks `Mono8`, 2448 x 2048, stride 2560 as the top interpretation and that applying it restores the grayscale scene.

The resized Buffer Doctor variable intentionally reuses the geometry already covered by the deterministic padded-`Mono8` regression test. Its purpose is a visually meaningful demonstration; the procedural fixture remains the exact algorithm regression oracle.

## Recorded Validation - 2026-08-06

- Host: Visual Studio 2022 Community `17.14.37516.0` on the active leftmost `\\.\DISPLAY2` (`-1920,365`, `1920 x 1080`).
- Automatic Inspector: `4 detected: 4 opened, 0 need mapping, 0 failed` for OpenCvSharp, Emgu CV, pointer-backed owner, and vendor-neutral frame representations of the same 1280 x 960 `BGR24` photograph.
- Registered Bitmap visualizer: `industrialBitmap` opened as `System.Drawing.Bitmap`, 1280 x 960 `BGR24`, stride 3840, with the same recognizable PCB scene.
- Pixel evidence: live `B`, `G`, `R`, grayscale, raw-byte, 5 x 5 neighborhood, and line values changed at the inspected PCB position.
- Buffer Doctor: the incorrect 2448 x 2048 `Mono8` descriptor showed the expected row shear; the first ranked row was 2448 x 2048 `Mono8`, stride 2560, score 67; applying it restored the grayscale PCB scene.
- Build/smoke: Debug debuggee build passed with 0 warnings and 0 errors; `--industrial-image-debug ... --no-break` exited 0.
- Regression: the Release `RawBufferVisualizer.Tests` executable reported `RawBufferVisualizer self-tests passed.`
- Reviewed captures: `D:\OpenVisionLab-TestData\RawBufferVisualizer\portfolio-captures-industrial-20260806`.
- Tracked documentation copies: `docs\images\industrial-pcb-auto-inspector-pixel.png`, `docs\images\industrial-pcb-buffer-doctor-before.png`, and `docs\images\industrial-pcb-buffer-doctor-recovered.png` with unchanged SHA-256 values.
- Detailed evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260806\industrial-image-validation-20260806.md`.

## Boundary

This proves real photographic content through the supported in-memory representations. It does not prove a physical camera, vendor SDK, acquisition timing, exposure control, transport stability, or sensor-native Bayer/packed payload.

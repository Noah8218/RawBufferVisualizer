# Industrial Image Debug Testing

Use this scenario when a human-readable debugger demonstration is more useful than the deterministic stripe and gradient fixtures. The existing procedural fixtures remain the source of truth for exact pixel-format, packing, and stride regression checks.

## Approved Portfolio Assets

| Item | Value |
| --- | --- |
| Image | `Printed circuit boards 20240831 083726.jpg` |
| Author | IMG 4512 |
| Source page | https://commons.wikimedia.org/wiki/File:Printed_circuit_boards_20240831_083726.jpg |
| License | CC0 1.0 Universal public-domain dedication |
| Test derivative | Wikimedia Commons 1280 x 960 preview |
| Local SHA-256 | `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46` |
| Local test path | `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260806\source\Printed_circuit_boards_20240831_083726-1280.jpg` |

| Item | Value |
| --- | --- |
| Image | `Mesin CNC.jpg` |
| Author | Naufal Shiddiq Fadhilah |
| Source page | https://commons.wikimedia.org/wiki/File:Mesin_CNC.jpg |
| License | CC0 1.0 Universal public-domain dedication |
| Test source | Wikimedia Commons original, 1280 x 720 |
| Local SHA-256 | `64901B8C11E37287060B997C891D219EA6DD47C53F1B5EF0758286AC722F25EF` |
| Local test path | `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260809\source\Mesin_CNC-1280x720.jpg` |

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
| `industrialBadStrideSnapshot` | Same color scene resized to 2448 x 2048 `BGR24`, with 80 bytes of row padding and an intentionally wrong descriptor stride of 7344 instead of 7424 |
| `dataTipIndustrialMat` | OpenCvSharp `CV_8UC3` alias placed immediately above the break for repeatable code-editor DataTip capture |

Verify that the real scene remains recognizable, the normal variables match the decoded image dimensions, pixel hover changes with position, and Fit and 1:1 work. For `industrialBadStrideSnapshot`, verify that Buffer Doctor ranks `BGR24`, 2448 x 2048, stride 7424 as the top interpretation and that applying it restores the aligned color scene.

The Doctor fixture retains three color bytes per pixel. The descriptor deliberately reports the tight stride of 7344 even though each stored row uses 7424 bytes. Buffer Doctor changes only the active interpretation metadata; it does not modify the source bytes.

The exact BGR24 geometry is covered by `BufferDoctorFindsPaddedBgr24Descriptor`. The older procedural padded-`Mono8` regression remains independently covered and is not used as the public color-recovery claim.

## Recorded Validation - 2026-08-06

- Host: Visual Studio 2022 Community `17.14.37516.0` on the active leftmost `\\.\DISPLAY2` (`-1920,365`, `1920 x 1080`).
- Automatic Inspector: `4 detected: 4 opened, 0 need mapping, 0 failed` for OpenCvSharp, Emgu CV, pointer-backed owner, and vendor-neutral frame representations of the same 1280 x 960 `BGR24` photograph.
- Registered Bitmap visualizer: `industrialBitmap` opened as `System.Drawing.Bitmap`, 1280 x 960 `BGR24`, stride 3840, with the same recognizable PCB scene.
- Pixel evidence: live `B`, `G`, `R`, grayscale, raw-byte, 5 x 5 neighborhood, and line values changed at the inspected PCB position.
- Buffer Doctor: the incorrect 2448 x 2048 `Mono8` descriptor showed the expected row shear; the first ranked row was 2448 x 2048 `Mono8`, stride 2560, score 67; applying it restored the grayscale PCB scene.
- Build/smoke: Debug debuggee build passed with 0 warnings and 0 errors; `--industrial-image-debug ... --no-break` exited 0.
- Regression: the Release `RawBufferVisualizer.Tests` executable reported `RawBufferVisualizer self-tests passed.`
- Reviewed captures: `D:\OpenVisionLab-TestData\RawBufferVisualizer\portfolio-captures-industrial-20260806`.
- Tracked documentation copies at the time of this validation: `docs\images\industrial-pcb-auto-inspector-pixel.png`, `docs\images\industrial-pcb-buffer-doctor-before.png`, and `docs\images\industrial-pcb-buffer-doctor-recovered.png`. They were refreshed and re-hashed on 2026-08-09 below.
- Detailed evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\industrial-image-tests-20260806\industrial-image-validation-20260806.md`.

## Documentation Refresh - 2026-08-09

- Host: installed `2.0.2.0` on Visual Studio 2022 Community `17.14.37516.0`; capture window on the dynamically selected leftmost `\\.\DISPLAY2` (`-1920,365`, `1920 x 1080`) at `-1900,385`, `1880 x 1040`.
- Registered breakpoint workflow: the actual cursor hovered the initialized `industrialBitmap` Locals row, opened the visible `Raw Buffer Visualizer` menu entry, and handed a 1280 x 960 `System.Drawing.Bitmap` to the docked viewer as `BGR24`, stride 3840.
- Code DataTip workflow: the actual cursor hovered `dataTipIndustrialMat` in the code editor, exposed `Mat [720 x 1280 x CV_8UC3]`, selected its magnifying-glass visualizer icon, and opened the different 1280 x 720 `BGR24` CC0 industrial-machine photograph in the docked viewer.
- Automatic Inspector: `5 detected: 5 opened, 0 need mapping, 0 failed` for supported representations of the same 1280 x 960 `BGR24` PCB photograph.
- Pixel/diagnostic evidence: the captured cursor position reported `X=881, Y=333, B=152, G=173, R=111, GV=145, Raw=98 AD 6F`; 5 x 5 statistics and BGR24 stride diagnostics were visible in the installed extension.
- Buffer Doctor: the color `BGR24` fixture reports stride 7344 while its stored rows use stride 7424; the first candidate was `BGR24`, 2448 x 2048, stride 7424, score 68; selecting it restored the aligned green PCB color scene.
- DataTip GIF: 960 x 531, 4 frames, 3.0 seconds total, 846,521 bytes; frame holds are 0.45-1.25 seconds.
- Locals GIF: 960 x 531, 4 frames, 3.25 seconds total, 913,045 bytes; frame holds are 0.55-1.15 seconds.
- Complete GIF: 960 x 531, 8 frames, 6.0 seconds total, 2,028,020 bytes; frame holds are 0.60-1.20 seconds.
- Installed scenarios: `IndustrialMarketplace` and `IndustrialDataTip` both passed with one Marketplace Open command, one Scan command, zero package-protocol errors, and verified monitor/window placement; Marketplace dry run resolved exactly six Overview assets and did not publish.
- Reviewed captures and GIF contact sheets: `D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip`.
- Tracked SHA-256: DataTip GIF `5645B03A8CFC8DF8B4B04990AA5AF2FF3BDF28D170BA0AFD93DB6E01A5F73DFC`; Locals GIF `B11744D4FC74E7B89C66999DDADC9A3DBC8B04E642C10EC7FB560540561C7BC1`; complete GIF `32E18864B8F685BB96A151B63F5BE0CC6824908FFC8D42FC593AFA99F0725E56`; Automatic Inspector `B7ABBCC19E7BC9DDCD1ECF46A9BFE2F83036FF50B62978701A5E1F61D12000D1`; Doctor before `F18F07DC358C45BE52D80705123861F7173996C60EEEB297780CF1F842486FAD`; Doctor recovered `1E392EA21A0B64F93C40AE2736D63E909AEAF62EE649D51AC75D465E7F90C8FE`.

## Documentation Refresh Closure

```text
Status: Complete
Scope: Add a real code-editor DataTip visualizer demonstration with a second CC0 industrial image, replace the public Doctor fixture with color BGR24 stride recovery, retain the Locals flow, regenerate all current media, and synchronize English/Korean 2.0.2 Overview copy.
Acceptance criteria: DataTip object, tooltip, magnifying-glass icon, click, and docked result visible -> pass; second image source/license/hash recorded -> pass; Locals workflow retained -> pass; Doctor before/after remains BGR24 and top stride 7424 restores color -> pass; main GIF remains 5-8 seconds with no state over 1.5 seconds -> pass; English/Korean Overview references the same current assets -> pass.
Verification: aggregate Release self-tests passed; Debug debuggee build passed with 0 warnings/0 errors; installed `IndustrialMarketplace` and `IndustrialDataTip` scenarios passed; source and encoded contact sheets visually reviewed; GIF dimensions/frame durations/file sizes and SHA-256 verified; Marketplace dry run resolved exactly six assets and did not publish.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip and the tracked hashes above.
Boundary / next dependency: No commit, push, CI, Marketplace upload, or public readback was performed. The sample/test/media/document changes require owner review before repository publication.
```

## Boundary

This proves real photographic content through the supported in-memory representations. It does not prove a physical camera, vendor SDK, acquisition timing, exposure control, transport stability, or sensor-native Bayer/packed payload.

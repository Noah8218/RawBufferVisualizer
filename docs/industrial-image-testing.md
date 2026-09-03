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

## Documentation Refresh - 2026-08-25 (2.0.4)

- Host: exact installed `2.0.4.0` review VSIX on Visual Studio 2022 Community `17.14.37516.0`; capture window on the dynamically selected left monitor `\\.\DISPLAY2` (`-1920,365`, `1920 x 1080`) at `-1900,385`, `1880 x 1040`, Windows scale 125%, dark theme.
- Code DataTip: `dataTipIndustrialMat` exposed the actual OpenCvSharp visualizer glyph and opened the separate 1280 x 720 `BGR24` CC0 industrial-machine image. Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-industrial-datatip-final3`.
- Locals and complete workflow: the registered Bitmap path opened the 1280 x 960 PCB image, Automatic Inspector reported `5 detected: 5 opened, 0 need mapping, 0 failed`, and Buffer Doctor restored the color image from declared stride 7344 to actual stride 7424. Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\installed-vs2022-industrial-marketplace-final2`.
- DataTip GIF: 960 x 532, 12 frames at 4 fps, 3.0 seconds, 226,577 bytes.
- Locals GIF: 960 x 532, 13 frames at 4 fps, 3.25 seconds, 1,915,939 bytes.
- Complete GIF: 960 x 532, 24 frames at 4 fps, 6.0 seconds, 4,110,265 bytes. Its eight states are held for 0.75 seconds each.
- Static screenshots: all three are 1880 x 1040 and show the current UI, including the labelled **Clear all** action above the image list.
- Reviewed source frames, final GIF contact sheets, and staged media: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-media-final`.
- Tracked SHA-256: DataTip GIF `F284D65BBDE9BFB1466BFA756E2282A91FD4929F92E581A4D9D6B306D5AD862A`; Locals GIF `45BA65AF255C9D15D910A87982AB490710614C8DBE4DCACD74C86713C8701A11`; complete GIF `6AEC2449C25DEE827845E670EE8A7A0C650038DF141041E6C0672CD304F9715D`; Automatic Inspector `521F74B81B0ADB6D67ADB2D8F582B19EF37C719BE47D0CD5F4C6531989CBC489`; Doctor before `C9A576853DF46D3207E0CE26728655EC76D9DF0CB288878EA10B0285B22A27F4`; Doctor recovered `A98F443BE2BE4634A20F7AC488360EF7ED87CD77E542E7A6B28EC0EC8D9E667E`.
- Marketplace dry run resolved exactly these six assets from the 2.0.4 English Overview and did not publish. Manifest: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-dry-run-final\vs-publish.json`.

## 2.0.4 Documentation Refresh Closure

```text
Status: Complete
Scope: Replace all six Marketplace media files with exact installed-2.0.4 current-UI captures while retaining separate DataTip and Locals entry GIFs and the complete color Buffer Doctor sequence.
Acceptance criteria: DataTip object/glyph/click/result visible -> pass; Locals visualizer flow retained -> pass; industrial images remain color -> pass; Doctor top stride 7424 restores the color scene -> pass; complete GIF is 5-8 seconds with no state over 1.5 seconds -> pass; English/Korean Overview uses the same six files -> pass.
Verification: exact installed IndustrialDataTip and IndustrialMarketplace scenarios passed; source and encoded contact sheets were visually reviewed; GIF duration/frame/dimension/size and all six SHA-256 values were measured; Marketplace dry run resolved exactly six assets without publication.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.4\20260825\marketplace-media-final and the tracked hashes above.
Boundary / next dependency: Media and copy are local review inputs. No commit, push, Marketplace upload, or public readback was performed.
```

## Superseded 2.0.2 Documentation Refresh Closure

```text
Status: Complete
Scope: Add a real code-editor DataTip visualizer demonstration with a second CC0 industrial image, replace the public Doctor fixture with color BGR24 stride recovery, retain the Locals flow, regenerate all current media, and synchronize English/Korean 2.0.2 Overview copy.
Acceptance criteria: DataTip object, tooltip, magnifying-glass icon, click, and docked result visible -> pass; second image source/license/hash recorded -> pass; Locals workflow retained -> pass; Doctor before/after remains BGR24 and top stride 7424 restores color -> pass; main GIF remains 5-8 seconds with no state over 1.5 seconds -> pass; English/Korean Overview references the same current assets -> pass.
Verification: aggregate Release self-tests passed; Debug debuggee build passed with 0 warnings/0 errors; installed `IndustrialMarketplace` and `IndustrialDataTip` scenarios passed; source and encoded contact sheets visually reviewed; GIF dimensions/frame durations/file sizes and SHA-256 verified; Marketplace dry run resolved exactly six assets and did not publish.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip and the tracked hashes above.
Boundary / next dependency: No commit, push, CI, Marketplace upload, or public readback was performed. The sample/test/media/document changes require owner review before repository publication.
```

## Documentation Refresh - 2026-09-03 (2.0.6)

- Host: exact installed `2.0.6.0` review VSIX on Visual Studio 2022 Community `17.14.37516.0`; the monitor was selected dynamically as `\\.\DISPLAY1` (`0,0`, `1920 x 1080`) and the verified Visual Studio rectangle was `20,20`, `1880 x 1040`.
- Code DataTip: UI Automation located the visible `dataTipIndustrialMat` token instead of relying on a source-line coordinate. Hover exposed the OpenCvSharp `Mat` DataTip, the actual magnifying-glass affordance was clicked, and the separate 1280 x 720 `BGR24` industrial-machine image opened in the docked viewer.
- Locals and complete workflow: the registered Bitmap path opened the 1280 x 960 PCB image, Automatic Inspector reported `5 detected: 5 opened, 0 need mapping, 0 failed`, and Buffer Doctor restored the color image from declared stride 7344 to actual stride 7424.
- DataTip GIF: 960 x 532, 12 frames at 4 fps, 3.0 seconds, 356,082 bytes.
- Locals GIF: 960 x 532, 13 frames at 4 fps, 3.25 seconds, 2,499,942 bytes. The transient Visual Studio loading surface is held for one 0.25-second frame.
- Complete GIF: 960 x 532, 24 frames at 4 fps, 6.0 seconds, 4,254,049 bytes; no state is held longer than 0.75 seconds.
- Static screenshots: all three are 1880 x 1040 and show the installed 2.0.6 UI, object names, pointer/pixel provenance, the labelled **Clear all** action, color `BGR24` diagnosis, and the aligned recovery result.
- Reviewed source frames, final GIF contact sheets, and staged media: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\marketplace-media-2.0.6`.
- Tracked SHA-256: DataTip GIF `7BD5E2B3BE933AF0ACDD91302792CACE0DE5042C9174585E05F72361E1672630`; Locals GIF `E49A575C8EBC016AE8A53B387C8FEBD3E4F975367A6781FA9676C1A4A5A6A9BE`; complete GIF `63DE414FDD424438B30064401FCC0F8C0F87F2F5AA4B093C2EC6E2DBAAF7BBBA`; Automatic Inspector `7546A8A272FA3DA53EAF39F0148E83390CD0609F839ED4E00738477315DABC6A`; Doctor before `2FA321961FCA01CA7636057FF8ED76B511DB540BA04EE33ABD6E3E1129BA5451`; Doctor recovered `F9B852827F64B7059375B966F577A39D8A3B3BEC9F8DFA0BE4F00F1135AC7246`.
- Marketplace dry run resolved the English 2.0.6 Overview, the exact frozen VSIX, and all six current assets without publishing. Manifest: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\marketplace-media-2.0.6\dry-run\vs-publish.json`.

## 2.0.6 Documentation Refresh Closure

```text
Status: Complete
Scope: Replace all six Overview media files with exact installed-2.0.6 captures, retain separate code-DataTip and Locals entry GIFs, and keep the complete color Buffer Doctor workflow within six seconds.
Acceptance criteria: DataTip object/tooltip/glyph/click/result visible -> pass; Locals visualizer path retained -> pass; current object-name and pointer/pixel rows visible -> pass; Doctor remains BGR24 and stride 7424 restores color -> pass; all GIFs use the installed 2.0.6 UI and no state exceeds 1.25 seconds -> pass; English/Korean Overview references the same six paths -> pass.
Verification: installed IndustrialMarketplace and IndustrialDataTip scenarios passed on VS2022 17.14.37516.0; source and encoded contact sheets were visually reviewed; GIF frame count/rate/duration/dimensions/size and all six SHA-256 values were measured; Marketplace dry run passed without publication.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\marketplace-media-2.0.6 and the tracked hashes above.
Boundary / next dependency: This proves the documented debugger workflows on the available VS2022 host. It does not prove exact VS2022 17.9 runtime, a physical camera, a proprietary SDK, Marketplace publication, or public-package readback.
```

## Boundary

This proves real photographic content through the supported in-memory representations. It does not prove a physical camera, vendor SDK, acquisition timing, exposure control, transport stability, or sensor-native Bayer/packed payload.

## Signed Int32 Industrial Validation - 2026-09-03 (2.0.7)

- Host: exact installed local `2.0.7.0` candidate on Visual Studio 2022 Community `17.14.37516.0`; dynamically selected `\\.\DISPLAY2` (`-1920,365`, `1920 x 1080`) with verified Visual Studio rectangle `-1900,385`, `1880 x 1040`.
- Source: the recorded CC0 1280 x 960 PCB image, SHA-256 `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46`.
- Interpretation: the color photograph is converted into one signed intensity value per pixel to exercise OpenCvSharp `CV_32SC1` and Emgu CV `Cv32S` C1. The viewer intentionally displays this one-channel scalar field in grayscale after signed min/max normalization; it is not a missing-color defect.
- Direct DataTip: `labelMat` opened without a fallback menu as OpenCvSharp `Int32`, 1280 x 960, stride 5120. Pixel `(815, 386)` reported signed value `-12208` and decimal raw bytes `80 208 255 255`, equivalent to little-endian `50 D0 FF FF`.
- Automatic Inspector: `5 detected: 5 opened, 0 need mapping, 0 failed`; `labelMatList` contributed one inspected and opened collection item. Rows covered OpenCvSharp, Emgu CV, a pinned `RawBufferView`, an application frame wrapper, and the Mat list.
- Padded stride: `paddedInt32Frame` remained `Int32`, 1280 x 960, stride 5184 (`1280 * 4 + 64`).
- Runtime result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\installed-int32-direct-vs2022\Int32Industrial-installed-vsix.json`.
- Layout result: `D:\OpenVisionLab-TestData\RawBufferVisualizer\cv32sc1-2.0.7-20260903\final-layout-widths\layout-widths.json`; 540, 900, and 1160 px passed.
- Capture SHA-256: DataTip hover `78BEE900316D5B74ABF7443789DF06BB255E10EA61219531AD890CA5797DA212`; visualizer glyph `16C37E630FE052C36157AAE80A98B4CECE077AA93B71B364FEEAFBE67F4B9AEC`; direct result `7FDF4E76841D75F9CA87D6D5D514CC568CBD432F0DB3A91F260C1C4A81031125`; automatic matrix `898DC75F7159A11FCEA62DCBBFF1221F33CA2F4A88C00F57993826CF251E00D2`; padded stride `95CD70A340E7EEB8F773D747DE71D0A0B43FC9DE2A1B90107683DD0CF6902020`.

The reviewed direct and automatic captures are tracked as `docs/images/int32-industrial-opencv-direct.png` and `docs/images/int32-industrial-automatic-matrix.png`. The owner approved them for the 2.0.7 README and Marketplace Overview on 2026-09-03. They retain the complete Visual Studio context deliberately so the breakpoint, object names, addresses, image list, format, stride, and real image can be evaluated together.

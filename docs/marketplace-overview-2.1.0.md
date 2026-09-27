# Raw Buffer Visualizer

## Image Watch-style debugging for C# machine vision

Inspect image variables and raw 2D buffers directly inside Visual Studio at a breakpoint. Raw Buffer Visualizer opens `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, pointer-backed frames, snapshots, and supported collections in one docked viewer without debug-only image conversion code.

## What's New In 2.1.0

Version `2.1.0` brings together the native-image transfer fixes from `2.0.9` and the following inspection, pixel-accuracy, and workflow improvements.

### Automatic inspection and image space

- **Correct debugger status with Auto Inspect off:** the status changes from running to paused or session ended even when automatic scanning is disabled. Temporary panel and action notices clear on state changes without clearing actual image errors. **Scan Now** is available while paused with no scan in progress; captured images remain available after the debug session ends.
- **More room for Images:** long frame names and guidance move into the **?** help control beside **Auto Inspect on Break**. Current state and scan failures stay visible in the bottom status area, including in narrow layouts.
- **Responsive discovery and cancellation:** **Stop** is checked during candidate discovery as well as image loading. Continuing execution, ending the session, or starting newer work prevents obsolete results from being published. An individual debugger call must still return before cancellation can proceed.

### Pixel correctness and histogram analysis

- **Bitmap color and alpha:** correct handling of signed stride, indexed color palettes, premultiplied alpha, and unused alpha bytes across snapshot and debugger transfer paths.
- **Float32 and Bayer accuracy:** consistent Float32 display scaling across memory, file, and live sources; precise numeric readouts and stable statistics; corrected Bayer preview colors at sampled coordinates. Float32 rendering also avoids allocating a temporary byte array for every pixel.
- **Histograms from source values:** inspect raw mono, packed, signed, floating-point, and Bayer values, including file-backed and live sources. Color images provide RGB mean, R, G, and B choices. The panel identifies full or sampled coverage, numeric range, and excluded non-finite values; generated comparisons are labelled as display values. Calculation runs in the background and rejects stale results when the selection changes.

### Buffer mappings and snapshot saving

- **Visible mapping scope:** choose the current solution or user scope and see the save destination. Solution changes update the mapping context, assembly-specific mappings remain separate, and conflicting or invalid mapping files are not silently overwritten.
- **Cancellable snapshot export:** bounded background copying provides progress and cancellation. Failed or cancelled exports preserve the previous snapshot; a live-source export is cancelled when execution resumes.

### Included fixes from 2.0.9

- Pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads of **8 MiB or larger** use checked live process-memory reads instead of repeatedly transferring the complete image through debugger RPC. Smaller pointer-backed buffers and non-pointer snapshots retain chunked transfer.
- Inferred ROI and submatrix spans end at the final pixel row, avoiding nonexistent trailing stride padding. Explicit supplied lengths remain authoritative, and unreadable, partial, released, or overflowed ranges fail closed.
- Reopening the same expression after the same technical failure refreshes its existing error row with current details and a new report ID instead of adding duplicate rows.
- Consecutive registered visualizer opens keep the pinned docked Tool Window open and in place.
- Transfer errors identify the operation, source or chunk, available byte-range details, exception type, and HRESULT. The original debugger exception remains in the local support report.

Live rows show the process ID, source pointer, pixel address, and `LIVE` state. Continue or process exit changes them to `UNAVAILABLE`; copied snapshots remain `CAPTURED` and viewable.

## Main Workflow

1. Install Raw Buffer Visualizer and restart Visual Studio.
2. Start debugging and stop after the image object has been initialized.
3. From a DataTip, Locals, Autos, or Watch window, choose Raw Buffer Visualizer from the magnifying-glass menu. You can also open `View > Other Windows > Raw Buffer Visualizer` and use Automatic Inspector.
4. Select an image row to inspect its expression or object name, dimensions, format, stride, source state, `Ptr`/`Pixels` addresses, and pixels.
5. Hover the image for X/Y, channel or signed values, and raw bytes. Use Fit, 1:1, pan, zoom, markers, histogram, line profile, A/B comparison, or Save as needed.

Images accumulate in the list. **Clear all** removes the complete viewer list and resets document-specific presentation without changing data in the paused program.

## Supported Inputs

| Input | Support |
| --- | --- |
| `System.Drawing.Bitmap` | 8-bit indexed, 24-bit RGB storage, and common 32-bit RGB/ARGB/PARGB formats |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
| `ImagePtr` and pointer-backed frames | Direct visualization when the pointer, dimensions, stride, format, and available byte length can be validated |
| `RawBufferSnapshot` | Managed snapshots from byte, unsigned-16, float-32, signed-int-32, or pointer data |
| `RawBufferView` | Pointer plus explicit width, height, stride, format, length, and lifetime metadata |
| Existing application frame wrappers | Automatic Inspector recognizes safe debugger-visible layouts; ambiguous layouts can be saved once through Connect Your Buffer |
| Collections | Lists, dictionaries, concurrent dictionaries, arrays, and mixed supported image collections |
| Snapshot files | `.rbuf.json` descriptor plus `.raw` payload |

Supported pixel formats are `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, `Int32`, and 8-bit Bayer `RGGB`, `GRBG`, `GBRG`, and `BGGR` previews.

`CV_32SC1` means one channel containing signed 32-bit integers. It does not mean 32 channels. Multi-channel signed formats such as `CV_32SC2`, `CV_32SC3`, and `CV_32SC4` are not interpreted as images by this release.

## Automatic Inspector And Connect Your Buffer

Leave **Auto Inspect on Break** enabled to scan the selected stack frame for initialized supported Mats and safe image-like objects. **Scan Now** remains available while automatic scanning is off. Large results use bounded discovery and incremental loading, with visible candidate, refreshed, deferred, and failed counts.

- `[Auto]` opened successfully.
- `[Map]` needs a one-time member or enum mapping.
- `[Failed]` was recognized but could not be read safely.

One failed item does not block other images. Matching automatic rows refresh in place across repeated Break, F10, and **Scan Now** operations.

When an application object is image-like but ambiguous, **Connect Your Buffer** lets you review Data, Width, Height, Stride, Buffer Length, Pixel Format, Valid Bits, and Byte Order roles. **Preview** is explicit; only **Save Mapping** persists the mapping for later equivalent breaks.

## Pointer And Lifetime Safety

The viewer validates dimensions, stride, minimum row bytes, required span, format, valid bits, and arithmetic before rendering. OpenCvSharp and Emgu rows distinguish the native object `Ptr` from the `Pixels` address used to read image bytes. ImagePtr and RawBufferView show a combined pointer/pixel address when both are identical.

Each open reads the bytes currently stored at the address. Reusing an address does not reuse stale image content. The debuggee must remain paused and keep live memory valid while the viewer reads it.

## Vision Buffer Doctor

If a raw image appears sheared, too dark, scrambled, or incorrectly packed, open `Inspector > Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible dimensions, stride, format, valid bits, and byte order from bounded samples without changing the paused program's bytes.

## Visual Studio Compatibility

Source 2.1.0 has not been qualified in an installed IDE. The support targets below do not claim that qualification.

- Visual Studio 2022 Community, Professional, or Enterprise `17.9+`, x64.
- Stable Visual Studio 2026 `18.x`, x64.
- Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not release targets.


Install the extension in a supported Visual Studio edition and restart Visual Studio.

## Privacy And Diagnostics

Raw Buffer Visualizer runs locally. Error rows can copy a support report containing versions, source type, technical error details, and local diagnostic paths; image payloads and credentials are excluded. Review local paths before sharing a report.

[Source code and complete documentation](https://github.com/Noah8218/RawBufferVisualizer) · [Complete changelog](https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md)

# Raw Buffer Visualizer

## Image Watch-style debugging for C# machine vision

Inspect image variables and raw 2D buffers directly inside Visual Studio at a breakpoint. Raw Buffer Visualizer opens `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, pointer-backed frames, snapshots, and supported collections in one docked viewer without debug-only image conversion code.

![Open an OpenCvSharp Mat from its code DataTip](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-datatip-open.gif)

The DataTip example opens a real 1280 x 720 industrial-machine image and preserves the exact `dataTipIndustrialMat` expression name on its image card.

![Open a real industrial image from Visual Studio Locals](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer debugger workflow](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-demo.gif)

## What's New In 2.0.9

Version `2.0.9` corrects the native-image transfer path used by Visual Studio debugger visualizers:

- Pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads at or above 8 MiB use checked live process-memory reads instead of repeated full-image debugger RPC snapshot requests.
- Inferred ROI and submatrix spans end at the last pixel in the final row: `stride * (height - 1) + minimum row bytes`. Trailing padding after the final row is not read.
- Explicit caller-supplied buffer lengths remain authoritative.
- Unreadable, released, partially readable, or overflowed native ranges still fail closed.
- Smaller snapshots retain the 4 MiB chunk path and remain available after the debuggee continues or exits.
- A debugger RPC failure identifies its operation, source or chunk, byte offset and sizes when applicable, exception type, and HRESULT. The original localized debugger exception remains in the local support report.
- Reopening the same expression after the same technical failure refreshes its existing error row with a new report ID instead of filling the image list with duplicates.
- Consecutive registered visualizer opens keep the user's pinned docked Raw Buffer Visualizer Tool Window open and in place.
- The Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` targets are unchanged.

For a live source, the image row records the process ID, source pointer, pixel address, and `LIVE` state. Continuing execution or ending the process changes that row to `UNAVAILABLE`; it does not read freed memory. A copied snapshot remains `CAPTURED` and viewable.

## Main Workflow

1. Install Raw Buffer Visualizer and restart Visual Studio.
2. Start debugging and stop after the image object has been initialized.
3. Select the Raw Buffer Visualizer magnifying-glass entry on a supported variable, or open `View > Other Windows > Raw Buffer Visualizer` and use Automatic Inspector.
4. Select an image row to inspect its expression name, dimensions, format, stride, source state, pointer provenance, and pixels.
5. Hover the image for X/Y, channel or signed values, and raw bytes. Use Fit, 1:1, pan, zoom, markers, histogram, line profile, A/B comparison, or Save as needed.

Images accumulate in the list. **Clear all** removes the complete viewer list and resets document-specific presentation without changing data in the paused program.

## Supported Inputs

| Input | Support |
| --- | --- |
| `System.Drawing.Bitmap` | 8-bit indexed, 24-bit RGB storage, and common 32-bit RGB/ARGB/PARGB formats |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
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

![Buffer Doctor candidates for an industrial PCB buffer](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-before.png)

![Recovered color PCB image after applying the correct padded stride](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio Compatibility

- Visual Studio 2022 Community, Professional, or Enterprise `17.9+`, x64.
- Stable Visual Studio 2026 `18.x`, x64.
- Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not release targets.

Visual Studio and this extension are the only separate software required by an extension user. Image libraries used by the application being debugged remain part of that application.

## Privacy And Diagnostics

Raw Buffer Visualizer runs locally. Error rows can copy a support report containing versions, source type, technical error details, and local diagnostic paths; image payloads and credentials are excluded. Review local paths before sharing a report.

[Source code and complete documentation](https://github.com/Noah8218/RawBufferVisualizer) · [Complete changelog](https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md)

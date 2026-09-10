# Raw Buffer Visualizer

## Image Watch-style debugging for C# machine vision

Inspect image variables and raw 2D buffers directly inside Visual Studio at a breakpoint. Raw Buffer Visualizer brings `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, pointer-backed frames, snapshots, and supported collections into one docked viewer without temporary image files or debug-only conversion code.

![Open an OpenCvSharp Mat from its code DataTip](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-datatip-open.gif)

The DataTip example opens a real 1280 x 720 industrial-machine image and retains the exact `dataTipIndustrialMat` expression name on its image card.

![Open a real industrial image from Visual Studio Locals](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer debugger workflow](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## What's New In 2.0.8

Version `2.0.8` makes signed 32-bit, single-channel matrix visualization reliable on the Visual Studio 2022 debugger path, strengthens release packaging, and retains the complete 2.0 workflow:

- OpenCvSharp `CV_32SC1` opens as `Int32` from DataTip, Locals, Autos, or Watch.
- Emgu CV `Cv32S` C1 uses the same signed `Int32` path.
- Automatic Inspector and supported Mat collections recognize the format.
- Signed minimum and maximum values are autoscaled to grayscale while pixel inspection retains the exact signed integer and four raw bytes.
- Pointer-backed images, mapped `int[]`, snapshots, byte order, padded stride, sampled previews, and tiled display remain supported.
- Release packaging now compares the debugger-side VSIX payload with the fresh Release build and fails if the hashes differ.
- Automatic Inspector limits discovery to 128 candidates and opens the first eight images or a soft two-second batch before offering **Load next 8**, **Load all this Break**, and **Stop**.
- Repeated Break, F10, and **Scan Now** operations refresh matching rows in place instead of duplicating them. Cached type analysis improves repeated scans but never turns a mapping-required object into an automatic open.

![Open a signed Int32 industrial OpenCvSharp Mat](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/int32-industrial-opencv-direct.png)

![Automatic inspection of signed Int32 matrices and a padded-stride frame](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/int32-industrial-automatic-matrix.png)

`CV_32SC1` means one channel containing signed 32-bit integers. It does not mean 32 channels. Multi-channel signed formats such as `CV_32SC2`, `CV_32SC3`, and `CV_32SC4` are not interpreted as images by this release.

## Main Workflow

1. Install Raw Buffer Visualizer and restart Visual Studio.
2. Start debugging and stop after the image object has been initialized.
3. Select the Raw Buffer Visualizer magnifying-glass entry on a supported variable, or open `View > Other Windows > Raw Buffer Visualizer` and use Automatic Inspector.
4. Select an image row to inspect its variable name, dimensions, pixel format, stride, source state, pointer provenance, and pixels.
5. Hover the image for X/Y, channels or signed value, and raw bytes. Use Fit, 1:1, pan, zoom, markers, histogram, line profile, A/B comparison, or Save as needed.

Images accumulate in the list. **Clear all** removes the complete viewer list and resets document-dependent presentation without changing data in the paused program.

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

## Automatic Inspector And Connect Your Buffer

Leave **Auto Inspect on Break** enabled to scan the selected stack frame for initialized supported Mats and safe image-like objects. **Scan Now** remains available while automatic scanning is off. Large results use bounded discovery and incremental loading, with visible candidate/refreshed/deferred/failed counts instead of a blocking popup. A failed item remains visible and does not prevent other images from opening.

When an application object is image-like but ambiguous, **Connect Your Buffer** lets you review the inferred Data, Width, Height, Stride, Buffer Length, Pixel Format, Valid Bits, and Byte Order roles. **Preview** is explicit; only **Save Mapping** persists the editable mapping for later equivalent breaks.

## Pointer And Lifetime Safety

The viewer validates dimensions, stride, required byte length, format, valid bits, and arithmetic before rendering. Live pointer-backed rows become unavailable after Continue or process exit, while copied snapshots remain available. Inaccessible, released, or partially readable native ranges fail visibly instead of being shown as complete images.

OpenCvSharp and Emgu rows distinguish the native object `Ptr` from the `Pixels` address used to read image bytes. ImagePtr and `RawBufferView` show one combined address when the source pointer and pixel pointer are identical. Reopening the same address reads its current bytes; an address is not treated as permanent object identity.

## Vision Buffer Doctor

If a raw image appears sheared, too dark, scrambled, or incorrectly packed, open `Inspector > Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible dimensions, stride, format, valid bits, and byte order from bounded samples without changing the paused program's bytes.

![Buffer Doctor candidates for an industrial PCB buffer](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-before.png)

![Recovered color PCB image after applying the correct padded stride](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio Compatibility

- Visual Studio 2022 Community, Professional, or Enterprise `17.9+`, x64.
- Stable Visual Studio 2026 `18.x`, x64.
- Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not release targets.

## Privacy And Diagnostics

Raw Buffer Visualizer runs locally. Error rows can copy a support report containing versions, source type, error details, and local diagnostic paths; image payloads and credentials are excluded. Review local paths before sharing a report.

[Source code and complete documentation](https://github.com/Noah8218/RawBufferVisualizer) · [Complete changelog](https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md)

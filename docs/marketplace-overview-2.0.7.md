# Raw Buffer Visualizer

## Image Watch-style debugging for C# machine vision

Inspect image variables and raw 2D buffers directly inside Visual Studio at a breakpoint. Raw Buffer Visualizer brings `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, pointer-backed frames, snapshots, and supported collections into one docked viewer without temporary image files or debug-only conversion code.

![Open an OpenCvSharp Mat from its code DataTip](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-datatip-open.gif)

![Open a real industrial image from Visual Studio Locals](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer debugger workflow](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## What's New In 2.0.7

Version `2.0.7` adds signed 32-bit, single-channel matrix visualization:

- OpenCvSharp `CV_32SC1` opens as `Int32` from DataTip, Locals, Autos, or Watch.
- Emgu CV `Cv32S` C1 uses the same `Int32` path.
- Automatic Inspector and supported Mat collections recognize the new format.
- Signed minimum and maximum values are autoscaled to grayscale, so label maps and integer result images remain visible.
- Pixel inspection retains the exact signed integer and its four raw bytes.
- Explicit little-/big-endian data, pointer-backed images, mapped `int[]`, snapshots, sampled previews, tiled display, and padded stride are supported.

![Open a real industrial OpenCvSharp CV_32SC1 Mat](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/int32-industrial-opencv-direct.png)

The installed 2.0.7 extension above displays a real industrial PCB source as signed `Int32`; the status bar retains the exact signed pixel value and its four source bytes.

![Automatic inspection of signed Int32 matrices and a padded-stride frame](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/int32-industrial-automatic-matrix.png)

Automatic Inspector opens OpenCvSharp, Emgu CV, mapped pointer, snapshot, collection, and padded-stride forms in the same image list.

`CV_32SC1` means **one channel containing signed 32-bit integers**. It does not mean 32 channels. Multi-channel signed formats such as `CV_32SC2`, `CV_32SC3`, and `CV_32SC4` are not interpreted as images by this release.

## Main Workflow

1. Install Raw Buffer Visualizer and restart Visual Studio.
2. Start debugging and stop at a breakpoint where the image object is initialized.
3. Select the Raw Buffer Visualizer magnifying-glass entry on a supported variable, or open `View > Other Windows > Raw Buffer Visualizer` and use Automatic Inspector.
4. Select an image row to inspect its dimensions, pixel format, stride, source state, pointer provenance, and pixels.
5. Hover the image for X/Y, channel or signed value, and raw bytes. Use Fit, 1:1, pan, zoom, markers, histogram, line profile, A/B comparison, or Save as needed.

Images accumulate in the list. **Clear all** removes the complete viewer list and resets document-dependent presentation without changing data in the paused program.

## Supported Inputs

| Input | Support |
| --- | --- |
| `System.Drawing.Bitmap` | 8-bit indexed, 24-bit RGB storage, and common 32-bit RGB/ARGB/PARGB formats |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
| `RawBufferSnapshot` | Managed snapshots from byte, unsigned-16, float-32, signed-int-32, or pointer data |
| `RawBufferView` | Pointer plus explicit width, height, stride, format, length, and lifetime metadata |
| Existing camera/frame wrappers | Automatic Inspector can recognize safe debugger-visible layouts; ambiguous layouts can be saved once through Connect Your Buffer |
| Collections | Lists, dictionaries, concurrent dictionaries, arrays, and mixed supported image collections |
| Snapshot files | `.rbuf.json` descriptor plus `.raw` payload |

OpenCvSharp compatibility is exercised at package versions `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627`. Emgu CV compatibility is exercised at `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924`.

## Supported Pixel Formats

`Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, `Int32`, and 8-bit Bayer `RGGB`, `GRBG`, `GBRG`, and `BGGR` previews are supported.

For `Int32`, display grayscale is derived from the signed range in the image or bounded preview sample. This visualization does not alter the source. The inspector still reports the original signed value and four stored bytes.

## Automatic Vision Inspector

Leave **Auto Inspect on Break** enabled to scan the current stack frame for initialized supported Mats and safe image-like wrappers. **Scan Now** remains available when automatic scanning is off. Results are isolated:

- `[Auto]` opened successfully.
- `[Map]` needs a one-time member or enum mapping.
- `[Failed]` was recognized but could not be read safely.

A failed item does not prevent other images from opening. Exact OpenCvSharp and Emgu Mat lists and one-dimensional arrays are included automatically; there is no separate collection option.

## Buffer And Pointer Safety

The viewer validates dimensions, stride, required byte length, format, valid bits, and arithmetic before rendering. Live pointer-backed rows are invalidated after Continue or process exit. Copied snapshots remain available. Inaccessible, released, or partially readable native ranges fail visibly instead of being presented as complete images.

OpenCvSharp and Emgu rows distinguish the native object `Ptr` from the `Pixels` address used for image reads. ImagePtr and `RawBufferView` show a combined address when the source and pixel pointer are the same.

## Vision Buffer Doctor

If a raw image appears sheared, too dark, scrambled, or incorrectly packed, open `Inspector > Interpret > Diagnose Buffer`. Buffer Doctor uses bounded samples to rank plausible dimensions, stride, format, valid bits, and byte order. Selecting a candidate updates the active interpretation without modifying the paused program's bytes.

![Buffer Doctor candidates for an industrial PCB buffer](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-before.png)

![Recovered color PCB image after applying the correct padded stride](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio Compatibility

- Visual Studio 2022 `17.9` or newer, x64
- Stable Visual Studio 2026 `18.x`, x64
- Community, Professional, and Enterprise editions

The exact 2.0.7 installed-runtime qualification used Visual Studio 2022 `17.14.37516.0`. The exact Visual Studio 2022 `17.9` and stable Visual Studio 2026 2.0.7 runtime checks were not available for this candidate; the declared compatibility targets and 17.9 SDK boundary are unchanged.

Visual Studio is the only separate software a normal extension user needs. Libraries used by the application being debugged remain part of that application.

## Scope

Raw Buffer Visualizer inspects existing 2D image memory while debugging. Camera acquisition and control, lighting, PLC/I/O, and 3D point-cloud or depth-container visualization are outside its scope.

The extension does not upload source code, image payloads, or debugger values. Diagnostic reports are created locally for review before sharing.

[Source, documentation, and issue tracker](https://github.com/Noah8218/RawBufferVisualizer)

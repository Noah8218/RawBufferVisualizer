# Raw Buffer Visualizer 1.0.51 Marketplace Overview

Stop saving temporary images or adding debug-only conversion code. Inspect C# machine-vision images, discover compatible camera-frame wrappers, and diagnose raw-buffer layout mistakes while stopped at a breakpoint.

Raw Buffer Visualizer is an Image Watch-style debugger tool for C# developers. It combines Bitmap, OpenCvSharp, Emgu CV, raw buffer, pointer, and image-collection visualizers with one docked image list inside Visual Studio 2022 and Visual Studio 2026.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## What's New in 1.0.51

This update completes the collection and release-communication work built after the public `1.0.50` package:

- Optionally expand exact OpenCvSharp and Emgu CV `Mat` lists and one-dimensional arrays from the selected stack frame.
- Keep valid images visible when another collection element is null, disposed, unsupported, or unreadable.
- Bound automatic collection work to 8 items per collection, 16 items and 8 roots per scan.
- Show a concise summary once for this version, with a persistent **Dismiss** and a reusable **What's New** button.

The update also carries forward the reliable debugger handoff, single View-menu registration, automatic direct-Mat inspection, and stable aspect-correct Fit behavior introduced in the public `1.0.50` line.

### Automatic Vision Inspector for individual images and Mat collections

Open the Tool Window once, enable **Auto Inspect on Break**, and stop after your image variables have been assigned. Automatic Vision Inspector scans the selected frame's Locals and Arguments.

- Initialized exact OpenCvSharp and Emgu CV `Mat` values can open automatically.
- Compatible unregistered pointer/array-backed wrappers use bounded structural discovery.
- `[Auto]` means inference and current-buffer validation passed.
- `[Map]` means the object looks image-like but needs member or format confirmation.
- `[Failed]` isolates one unreadable value without blocking images that succeeded.
- **Scan Now** refreshes the current frame without duplicating existing automatic rows.
- The enabled option persists across Visual Studio restarts and does not force the Tool Window open or steal focus.
- Exact OpenCvSharp/Emgu `Mat` lists and one-dimensional arrays can be included with the persisted, default-off **Mat collections** option; each element succeeds or fails independently.

`System.Drawing.Bitmap` remains available through its registered Raw Buffer Visualizer icon. Safe automatic Bitmap capture would require debugger-side `LockBits` evaluation, so it is not presented as an automatic live-open format.

![Automatic Vision Inspector finds safe image-like values in the current stack frame](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/automatic-vision-inspector.png)

### Release highlights without a repeated popup

The first Tool Window open after this update shows a concise, non-modal summary of the `1.0.51` changes. Selecting **Dismiss** marks this version as seen and keeps the summary hidden across Visual Studio restarts. It will not appear automatically again for `1.0.51`, but **What's New** can reopen it at any time. A future release can show its own summary.

Closing the Tool Window or Visual Studio without selecting **Dismiss** does not mark the summary as seen. Dismissing or reopening the summary never starts a scan or opens an image.

### Reliable image handoff

The debugger visualizer and docked window use an atomic request claim plus explicit completion acknowledgement. A transfer is successful only after the docked viewer has opened the image; rejected requests retain an actionable reason. This prevents a disappearing request file from being mistaken for a successful open.

### Stable Fit and manual navigation

- A new image, a changed selection, **Fit**, or double-click fits the full image while preserving its aspect ratio.
- Mouse-wheel zoom, drag pan, and **1:1** enter manual navigation and preserve the chosen zoom and center.
- Fit is recalculated when the docked viewer changes size; manual navigation is not reset by an unrelated layout refresh.

### Visual Studio registration hardening

The package validates that the View menu contains exactly one `Raw Buffer Visualizer` command and one `Raw Buffer Visualizer: Scan Current Frame` command.

## Vision Buffer Doctor

If a raw image looks sheared, scrambled, too dark, or incorrectly packed, use `Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible width, height, stride, pixel-format, valid-bit, and byte-order interpretations from bounded samples. Select a candidate to apply it immediately without another debugger round trip.

Buffer Doctor does not claim semantic image recognition. RGB/BGR order and Bayer phase may remain ambiguous and require developer confirmation.

![Vision Buffer Doctor ranks and applies plausible raw-buffer interpretations](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/vision-buffer-doctor.png)

## Smart Type Mapper

When an unregistered company-specific wrapper is recognizable but incomplete, **Map This Type** lets you confirm the buffer, dimensions, stride, and format members once. The mapping can then be reused on later breaks and Visual Studio sessions.

Smart Type Mapper does not dynamically register a debugger glyph for arbitrary CLR types. Individual registered types use their glyph; current-frame unregistered values enter through Automatic Inspector or **Open Variable**.

## Visual Studio Support

| Product | Supported range | Notes |
| --- | --- | --- |
| Visual Studio 2022 | `17.9` or newer, x64 | Community, Professional, and Enterprise. The current serviced `17.14` baseline is recommended. Exact `1.0.51` installed runtime qualification passed on Community `17.14.33`. |
| Visual Studio 2026 | Stable `18.x`, x64 | Community, Professional, and Enterprise. Supported through Microsoft's VSIX API compatibility model; exact `18.7.1` installed runtime qualification is pending. |

Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets. Visual Studio 2026 supports the stable Visual Studio 17.x extension APIs used by this VSIX and evaluates the manifest's lower API bound. See [Microsoft's Visual Studio extension compatibility model](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/extension-compatibility?view=visualstudio).

## One-Minute Quick Start

1. Install the extension in Visual Studio 2022 17.9+ or stable Visual Studio 2026 and fully restart Visual Studio.
2. Open `View > Raw Buffer Visualizer` once.
3. Start debugging and stop on a line after image assignment has completed.
4. Let **Auto Inspect on Break** open initialized OpenCvSharp/Emgu Mats and safe unregistered wrappers.
5. Enable the default-off **Mat collections** option when exact Mat lists or one-dimensional arrays should also expand automatically.
6. For Bitmap and other registered types, click the `Raw Buffer Visualizer` icon in DataTip, Watch, Locals, or Autos.
7. Select a thumbnail and inspect pixels, raw bytes, stride, format, and diagnostics.
8. If the interpretation looks wrong, run **Diagnose Buffer** and select a candidate.

## Why Raw Buffer Visualizer?

| Capability | Typical basic Mat viewer | Raw Buffer Visualizer |
| --- | --- | --- |
| OpenCvSharp `Mat` | Common | Registered and automatic current-frame paths |
| Emgu CV `Mat` | Varies | Registered and automatic current-frame paths |
| `System.Drawing.Bitmap` | Varies | Registered debugger-visualizer path |
| Typed and mixed image collections | Varies | Supported |
| Optional automatic exact Mat list/array expansion | Uncommon | Bounded and failure-isolated |
| `IntPtr` and raw image buffers | Limited | `RawBufferView`, mappings, and compatible shapes |
| Current-frame discovery for unregistered wrappers | Uncommon | Bounded Locals/Arguments inspection |
| Wrong stride/format/byte-order recovery | Uncommon | Ranked Buffer Doctor candidates |
| Multiple images in one docked list | Varies | Supported |
| Pixel values, channels, and raw bytes | Varies | Supported |
| Split, absolute diff, blink, linked pan/zoom | Varies | Supported |
| File-backed tiled display for very large payloads | Uncommon | Supported |

Other debugger visualizers have different feature sets. This table compares a basic Mat-only workflow with capabilities implemented in Raw Buffer Visualizer.

## Key Features

- One docked Visual Studio image list
- Automatic Inspector with isolated `[Auto]`, `[Map]`, and `[Failed]` outcomes
- Optional bounded automatic inspection of exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays
- Smart Type Mapper for compatible company-specific wrappers
- Vision Buffer Doctor with ranked, immediately applicable interpretations
- Thumbnail, dimensions, stride, format, source type, and diagnostic details
- X/Y, GV/RGB, channel swatches, source bytes, hover statistics, markers, line profile, and histogram
- Aspect-correct Fit, 1:1, wheel zoom, drag pan, and high-zoom pixel overlay
- A/B, linked views, split, difference, and blink comparison
- PNG and raw snapshot export
- File-backed tiled display for very large raw payloads

## Supported Inputs

- `RawBufferSnapshot`
- `RawBufferView`
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`
- Emgu CV `Mat`
- Typed or mixed `List<T>`, `Dictionary<TKey,TValue>`, `ArrayList`, `Hashtable`, and supported image arrays
- Optional Automatic Inspector expansion for exact OpenCvSharp/Emgu `Mat` lists and one-dimensional arrays
- Unregistered pointer/array-backed wrappers whose required members are visible to the debugger
- `.rbuf.json` plus `.raw` snapshot files

Current compatibility points include tested OpenCvSharp `Mat` packages from `4.0.0.20181225` through `4.13.0.20260627`, and Emgu CV `Mat` packages from `3.4.3.3016` through `4.13.0.5924`. These are tested points, not a guarantee for every intermediate build.

## Supported Pixel Formats

- `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`
- `Binary`
- `RGB24`, `BGR24`, `BGRA32`
- `Float32`
- `BayerRGGB8`, `BayerGRBG8`, `BayerGBRG8`, `BayerBGGR8`

## Safety And Known Limits

- Automatic Inspector scans only the selected stack frame's Locals and Arguments.
- Stop after assignment. A breakpoint on the assignment line can expose the old or null value.
- Automatic inspection is bounded to direct and one-level nested members.
- It does not invoke arbitrary vendor methods, dynamically load SDK DLLs, or decode private native layouts.
- Exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays can be expanded automatically when the persisted option is enabled; work is capped at 8 items per collection, 16 items and 8 roots per scan.
- Bitmap, dictionary, mixed, jagged/multidimensional, and arbitrary enumerable collections use their registered collection visualizer path.
- A registered collection visualization processes at most 256 entries.
- A buffer address without valid dimensions, stride/length, format, and lifetime cannot be opened safely.
- Bayer phase and RGB/BGR ordering can be inherently ambiguous.
- Industrial-camera contract fixtures are not real SDK/hardware certification.

## Large Image Support

Large debugger transfers use a bounded preview first and then live process memory or file-backed tiled data when available. Dense `100000 x 100000` and `200000 x 200000` Mono8 payloads have been exercised without loading the complete payload into one managed display bitmap.

## License

Raw Buffer Visualizer is licensed under the MIT License. External libraries retain their own licenses; see `THIRD-PARTY-NOTICES.md` in the source repository.

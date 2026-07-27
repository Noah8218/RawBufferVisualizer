# Raw Buffer Visualizer 1.0.47 Marketplace Overview

Stop saving temporary images or writing debug-only conversion code. Inspect C# image variables, discover accessible camera-frame wrappers, and diagnose raw-buffer layout mistakes directly while stopped at a breakpoint.

Raw Buffer Visualizer is an Image Watch-style debugger tool for C# machine-vision developers. It combines Bitmap, OpenCvSharp, Emgu CV, raw buffer, pointer, and image-collection visualizers with a docked image list inside Visual Studio.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## New In 1.0.47

### Automatic Vision Inspector

Open the Tool Window once and stop at a breakpoint. Automatic Vision Inspector scans the selected stack frame's locals and arguments for unregistered objects that expose a debugger-visible buffer pointer or managed array plus usable image metadata.

- `[Auto]`: inference and current-buffer validation passed; the image opened.
- `[Map]`: the object is image-like but a member or pixel-format mapping needs confirmation.
- `[Failed]`: the object was recognized, but its current data could not be opened.
- Failures are isolated, so valid images still open when another candidate fails.
- Smart Type Mapper saves a confirmed type mapping for later breaks and Visual Studio sessions.
- `Auto Inspect on Break` persists across restarts. `Scan Now` remains available when automatic scanning is off.

Registered Bitmap, OpenCvSharp, and Emgu CV types continue to use the debugger visualizer icon and are excluded from duplicate automatic mapping rows.

![Automatic Vision Inspector finds safe image-like values in the current stack frame](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/automatic-vision-inspector.png)

### Vision Buffer Doctor

If a raw image looks sheared, scrambled, too dark, or incorrectly packed, use `Interpret > Diagnose Buffer`. Buffer Doctor ranks plausible width, height, stride, pixel-format, valid-bit, and byte-order interpretations from bounded samples. Select a candidate to apply it immediately without another debugger round trip.

Buffer Doctor does not claim semantic image recognition. RGB/BGR and Bayer phase may remain ambiguous and require developer confirmation.

![Vision Buffer Doctor ranks and applies plausible raw-buffer interpretations](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/vision-buffer-doctor.png)

## One-Minute Quick Start

1. Install the extension and restart Visual Studio.
2. Open `View > Other Windows > Raw Buffer Visualizer`.
3. Start debugging and stop where images or camera-frame wrappers are alive.
4. Let `Auto Inspect on Break` open safe unregistered wrappers. For Bitmap, OpenCvSharp, Emgu CV, and registered collections, click the `Raw Buffer Visualizer` icon in DataTip, Watch, Locals, or Autos.
5. Select a thumbnail and inspect pixels, raw bytes, stride, format, and diagnostics.
6. If the image interpretation looks wrong, run `Diagnose Buffer` and select a candidate.

## Why Raw Buffer Visualizer?

| Capability | Typical basic Mat viewer | Raw Buffer Visualizer |
| --- | --- | --- |
| OpenCvSharp `Mat` | Common | Supported |
| Emgu CV `Mat` and `System.Drawing.Bitmap` | Varies | Supported |
| Typed and mixed image collections | Varies | Supported |
| `IntPtr` and raw image buffers | Limited | Supported through `RawBufferView` and compatible shapes |
| Current-frame discovery for unregistered wrappers | Uncommon | Safe pointer/array-backed locals and arguments |
| Wrong stride/format/byte-order recovery | Uncommon | Ranked Buffer Doctor candidates |
| Pixel values and raw bytes | Varies | Supported |
| Multiple images in one docked list | Varies | Supported |
| Split, absolute diff, blink, and linked pan/zoom | Varies | Supported |
| File-backed tiled display for very large payloads | Uncommon | Supported |

Other debugger visualizers have different feature sets. This table describes the difference between a basic Mat-only workflow and the capabilities implemented in Raw Buffer Visualizer.

## Key Features

- One docked Visual Studio image list
- Automatic Inspector with `[Auto]`, `[Map]`, and `[Failed]` outcomes
- Smart Type Mapper recovery for ambiguous company-specific wrappers
- Vision Buffer Doctor with ranked, immediately applicable interpretations
- Thumbnail, dimensions, stride, format, source type, and diagnostic details
- X/Y, GV/RGB, channel swatches, source bytes, hover statistics, markers, line profile, and histogram
- Fit, 1:1, wheel zoom, drag pan, and high-zoom pixel overlay
- A/B, linked views, split, diff, and blink comparison
- PNG and raw snapshot export
- File-backed tiled display for very large raw payloads

## Supported Inputs

- `RawBufferSnapshot`
- `RawBufferView`
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`
- Emgu CV `Mat`
- Typed or mixed `List<T>`, `Dictionary<TKey,TValue>`, `ArrayList`, `Hashtable`, and supported image arrays
- Unregistered pointer/array-backed wrappers whose required members are visible to the debugger
- `.rbuf.json` plus `.raw` snapshot files

Current compatibility points include real OpenCvSharp `Mat` packages from `4.0.0.20181225` through `4.13.0.20260627`, and real Emgu CV `Mat` packages from `3.4.3.3016` through `4.13.0.5924`. These are tested points, not a guarantee for every intermediate build.

## Supported Pixel Formats

- `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`
- `Binary`
- `RGB24`, `BGR24`, `BGRA32`
- `Float32`
- `BayerRGGB8`, `BayerGRBG8`, `BayerGBRG8`, `BayerBGGR8`

## Safety And Known Limits

- Automatic Inspector scans only the selected stack frame's locals and arguments. It does not search every stack frame.
- Automatic inspection is bounded to direct and one-level nested members. It does not traverse an arbitrary object graph.
- It reads debugger-visible fields and property getters, but does not call arbitrary vendor methods, load SDK DLLs dynamically, or decode private native layouts.
- Root collections and arrays use their registered collection visualizer path.
- A collection visualization processes at most 256 entries.
- A buffer address without valid dimensions, stride/length, format, and lifetime cannot be opened safely.
- Bayer phase and RGB/BGR ordering can be inherently ambiguous.
- Basler, Spinnaker, Vimba, and similar contract fixtures validate structural inference only. They are not a claim of real SDK/hardware certification.

## Large Image Support

Large debugger transfers use a bounded preview first and then live process memory or file-backed tiled data when available. Dense `100000 x 100000` and `200000 x 200000` Mono8 payloads have been exercised without loading the complete payload into one managed display bitmap.

## License

Raw Buffer Visualizer is licensed under the MIT License. External libraries retain their own licenses; see `THIRD-PARTY-NOTICES.md` in the source repository.

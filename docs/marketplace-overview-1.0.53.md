# Raw Buffer Visualizer

Inspect C# machine-vision images in Visual Studio at a breakpoint. Raw Buffer Visualizer collects Bitmap, OpenCvSharp/Emgu Mat, raw buffers, pointers, and collections in one docked viewer—without temporary save or conversion code.

Automatically discover compatible camera and frame-grabber wrappers, map their debugger-visible members, and diagnose stride, pixel-format, valid-bit, and byte-order mistakes.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## How It Works

1. Install the extension and fully restart Visual Studio.
2. Open `View > Other Windows > Raw Buffer Visualizer` once.
3. Start debugging and stop after image variables have been assigned.
4. Use **Auto Inspect on Break** or **Scan Now** for initialized Mats and compatible wrappers, or the debugger icon for Bitmap and other registered types.
5. Select a thumbnail to inspect pixels, source bytes, dimensions, stride, format, diagnostics, and comparison views.

Inspection remains explicit. Restoring a saved mapping or opening **Environment** does not scan the current frame or open an image.

## Supported Image Sources

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` and Emgu CV `Mat`
- `RawBufferSnapshot` and pointer-backed `RawBufferView`
- Raw managed arrays and pointers with a valid descriptor
- Compatible unregistered camera or frame-grabber wrappers whose required members are visible to the debugger
- Supported typed or mixed image lists, dictionaries, and one-dimensional arrays

Supported pixel formats include `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and four 8-bit Bayer phases.

## Connect Your Buffer And Diagnose Layouts

`[Map]` candidates open **Connect Your Buffer**. Select the buffer, dimensions, stride or length, and pixel-format members, preview the result, then save an editable mapping for that type. A neutral `RawBufferView` starter is available when an explicit wrapper is preferable.

**Diagnose Buffer** ranks plausible layouts for sheared, scrambled, dark, or incorrectly packed images. Apply and compare a candidate. RGB/BGR order and Bayer phase can remain ambiguous and require developer confirmation.

## Viewer And Comparison Tools

- One docked image list for all supported sources
- Aspect-correct Fit, 1:1, wheel zoom, drag pan, and high-zoom pixel overlay
- X/Y, GV/RGB values, channel swatches, source bytes, hover statistics, markers, line profile, histogram, and diagnostics
- A/B, linked views, split, absolute difference, and blink comparison
- PNG and raw snapshot export
- File-backed tiled display for very large raw payloads

## What's New In 1.0.53

- Added **Connect Your Buffer** mapping preview, save, restore, edit, and reset.
- Simplified **Environment Check** to the Visual Studio host, loaded extension version, and temporary-storage state. Select **Environment** again to close it.
- **Refresh** and **Copy diagnostic report** do not scan a frame, open an image, install software, or change extension registration.
- Select **What's New** again to close the release highlights; **Dismiss** also records the version as seen.
- Improved the first preview of extremely large pointer-backed images without changing established Visual Studio 2026, Mat collection, or debugger handoff behavior.

## Visual Studio Support

| Product | Supported range |
| --- | --- |
| Visual Studio 2022 | `17.14` or newer, x64 |
| Visual Studio 2026 | Stable `18.x`, x64 |

Community, Professional, and Enterprise are supported installation targets. Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

## Safety And Limits

- Stop after assignment; a breakpoint on the assignment statement can expose the previous or null value.
- Automatic discovery scans only the selected stack frame's Locals and Arguments and keeps its search bounded.
- A raw pointer cannot be opened safely without valid dimensions, stride or length, pixel format, and lifetime.
- Automatic discovery does not invoke arbitrary vendor methods, load camera SDK DLLs, or decode private native layouts.
- Camera acquisition and device control are outside this extension. Generic buffer inspection is not vendor certification.
- The Environment report excludes credentials, environment-variable values, and image payloads. Because it can contain local paths, review it before sharing.

## License And Support

Raw Buffer Visualizer is licensed under the [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE). External libraries retain their own licenses; see [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md).

Source, documentation, and issue reporting are available in the [Raw Buffer Visualizer repository](https://github.com/Noah8218/RawBufferVisualizer).

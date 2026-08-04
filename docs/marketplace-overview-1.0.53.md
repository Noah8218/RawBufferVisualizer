# Raw Buffer Visualizer 1.0.53 Marketplace Overview

Stop saving temporary images or adding debug-only conversion code. Inspect C# machine-vision images, discover compatible camera-frame wrappers, and diagnose raw-buffer layout mistakes while stopped at a breakpoint.

Raw Buffer Visualizer is an Image Watch-style debugger tool for C# developers. It combines registered Bitmap visualizers, automatic OpenCvSharp/Emgu Mat discovery, raw buffers, pointers, supported collections, and one docked image list inside Visual Studio 2022 and stable Visual Studio 2026.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## What's New in 1.0.53

- **Environment Check** reports only the current Visual Studio host, loaded extension version, and temporary-storage writability required by the extension.
- **Refresh** and **Copy diagnostic report** do not scan a frame, open an image, install software, or change registration.
- Select **Environment** again to close the panel; contributor and demo-media tools stay in the development documentation.
- Select **What's New** again to close the release highlights. **Dismiss** additionally saves the version as seen.
- Large pointer-backed previews use a bounded cold-page-read estimate so first preview remains responsive for dense 100k/200k sources.

The current public predecessor is `1.0.52`. Version `1.0.53` keeps its Visual Studio 2026 activation, automatic Mat collection, snapshot lease, and handoff-reliability baseline. Proprietary vendor-named direct adapters are excluded from release claims until the project's vendor SDK license gate passes.

## Core Workflow

1. Install the extension and fully restart Visual Studio.
2. Open `View > Other Windows > Raw Buffer Visualizer` once.
3. Start debugging and stop after image variables have been assigned.
4. Use **Auto Inspect on Break** or **Scan Now** for initialized OpenCvSharp/Emgu Mats and compatible unregistered wrappers.
5. Click the `Raw Buffer Visualizer` debugger icon for `System.Drawing.Bitmap` and other registered types.
6. Select a thumbnail and inspect pixels, bytes, dimensions, stride, format, diagnostics, and comparison views.
7. If a raw image looks wrong, run `Interpret > Diagnose Buffer` and select a candidate.
8. Open **Environment** when the host, extension registration, or temporary storage needs diagnosis. Select it again to close the panel.

Preview and inspection remain explicit. Opening Environment Check does not inspect the current debugger frame.

## Environment Check

Environment Check contains only the required runtime checks:

- supported x64 Visual Studio host;
- the Raw Buffer Visualizer version loaded in the current session;
- writable extension temporary storage.

Normal extension users do not need .NET, FFmpeg, OpenCV packages, or the VSSDK workload. Those contributor and demo-media tools are documented in the repository instead of appearing in the product panel. The copied report excludes credentials, environment-variable values, and image payloads. Because it can contain local paths, review it before sharing.

## Automatic Vision Inspector

- Scans only the selected stack frame's Locals and Arguments.
- `[Auto]` means inference and current-buffer validation passed.
- `[Map]` means a compatible shape needs member or format confirmation through Smart Type Mapper.
- `[Failed]` isolates one unreadable value without blocking successful images.
- Optional exact OpenCvSharp/Emgu Mat list and one-dimensional-array expansion remains bounded and default-off.
- Does not invoke arbitrary vendor methods, load camera SDK DLLs, or decode private native layouts.

`System.Drawing.Bitmap` remains on its registered debugger-visualizer path because safe extraction requires a debugger-side `LockBits` lifetime.

## Vision Buffer Doctor And Smart Type Mapper

Buffer Doctor ranks plausible width, height, stride, pixel-format, valid-bit, and byte-order interpretations from bounded samples. Smart Type Mapper lets a developer confirm buffer, dimension, stride, and format members for compatible company-specific wrappers and restores that editable mapping for the same type.

RGB/BGR order and Bayer phase can remain inherently ambiguous and require developer confirmation.

## Viewer Features

- One docked image list for Bitmap, Mats, raw buffers, pointers, and supported collections.
- Aspect-correct Fit, 1:1, wheel zoom, drag pan, and high-zoom pixel overlay.
- X/Y, GV/RGB, channel swatches, source bytes, hover statistics, markers, line profile, histogram, and diagnostics.
- A/B, linked views, split, absolute difference, and blink comparison.
- PNG and raw snapshot export.
- File-backed tiled display for very large raw payloads.

## Visual Studio Support

| Product | Supported range |
| --- | --- |
| Visual Studio 2022 | `17.14` or newer, x64 |
| Visual Studio 2026 | Stable `18.x`, x64 |

Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

## Safety And Limits

- Stop after assignment; a breakpoint on the assignment statement can expose the previous or null value.
- Automatic discovery is bounded to direct and one-level nested members.
- A raw pointer without valid dimensions, stride/length, format, and lifetime cannot be opened safely.
- Vendor camera SDKs and drivers remain the user's application responsibility and are not installed by Raw Buffer Visualizer. Generic buffer inspection is not vendor certification.
- No direct proprietary camera-SDK adapter is advertised or approved for this release until written vendor permission and required legal review are recorded.

## License

Raw Buffer Visualizer is licensed under the MIT License. External libraries retain their own licenses; see `THIRD-PARTY-NOTICES.md` and `docs/vendor-sdk-license-policy.md` in the source repository.

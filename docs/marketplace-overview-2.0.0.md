# Raw Buffer Visualizer

Inspect C# machine-vision images and raw 2D buffers directly in Visual Studio at a breakpoint. Raw Buffer Visualizer brings Bitmap, OpenCvSharp/Emgu Mat, pointers, managed buffers, and supported collections into one docked viewer without temporary image files or debug-only conversion code.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## How It Works

1. Install the extension and fully restart Visual Studio.
2. Open `View > Other Windows > Raw Buffer Visualizer` once.
3. Start debugging and stop after image variables have been assigned.
4. Use **Auto Inspect on Break** or **Scan Now** for initialized Mats and compatible wrappers, or use the debugger visualizer icon for registered types.
5. Select a thumbnail to inspect pixels, source bytes, dimensions, stride, format, diagnostics, and comparison views.

Preview and scanning remain explicit actions. Restoring a saved mapping, toggling a panel, or changing visibility does not scan the current frame or open an image.

## Supported Image Sources

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` and Emgu CV `Mat`
- `RawBufferSnapshot` and pointer-backed `RawBufferView`
- Mapped `byte[]`, `ushort[]`, and `float[]` buffers
- Mapped pointer objects with explicit dimensions, stride or exact length, pixel format, and lifetime
- Supported typed or mixed image lists, dictionaries, and one-dimensional arrays

Supported pixel formats include `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and four 8-bit Bayer phases.

## Connect Your Buffer

When a compatible object is not registered directly, **Connect Your Buffer** lets you assign its debugger-visible members to buffer, width, height, stride or length, format, valid bits, and byte order. Preview the result explicitly. If it still looks wrong, **Diagnose interpretation** ranks bounded alternatives in the same dialog; selecting one changes only the visible draft and preview. Only **Save Mapping** persists an editable mapping for that type. A neutral `RawBufferView` starter is available when an explicit wrapper is clearer.

The same checked validation contract is used for registered and mapped sources. Invalid dimensions, undersized stride, short buffer length, undefined format/order values, overflowing descriptor arithmetic, or incompatible valid bits fail before transfer instead of being guessed.

## Diagnose And Compare

- Inspect X/Y, GV/RGB values, channel swatches, raw source bytes, hover statistics, markers, line profile, histogram, and diagnostics.
- Use aspect-correct Fit, 1:1, wheel zoom, drag pan, and the high-zoom pixel overlay.
- Compare A/B images with linked views, split, absolute difference, and blink modes.
- Use **Diagnose Buffer** to rank plausible layouts for sheared, scrambled, dark, or incorrectly packed images.
- Export PNG images and raw snapshots.
- Open very large raw payloads through the file-backed tiled viewer.

## What's New In 2.0.0

- Added Connect Doctor to the mapping dialog for ranked format, stride, valid-bit, and byte-order alternatives with an explicit save boundary.
- Added one documented 2D compatibility contract for registered views, mapped pointers, and mapped managed buffers.
- Registered and mapped metadata now share fail-closed dimension, stride, length, and valid-bits validation.
- Descriptor arithmetic and format/order enum values now fail closed before allocation, transfer, or rendering.
- Valid `Mono16` values from 1 through 16 remain supported; 10, 12, 14, and 16 are covered by executable fixtures.
- `Mono10PackedLsb` and `Mono12PackedLsb` reject valid-bit values that contradict their fixed layouts.
- Neutral fixtures verify descriptor fields, transferred bytes, byte order, and pointer ownership without a proprietary SDK dependency.
- Continue or process exit converts live process-backed rows to `Unavailable` and blocks delayed handoffs; copied managed buffers stay usable.

## Visual Studio Support

| Product | Supported range |
| --- | --- |
| Visual Studio 2022 | `17.14` or newer, x64 |
| Visual Studio 2026 | Stable `18.x`, x64 |

Community, Professional, and Enterprise are supported installation targets. Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

## Safety And Limits

- Stop after assignment; a breakpoint on the assignment statement can expose the previous or null value.
- A pointer requires valid dimensions, stride or length, pixel format, and a lifetime that remains valid while the debuggee is paused.
- After Continue or process exit, a live row keeps only its last rendered pixels as context and cannot read the debuggee source again. Pause at a valid breakpoint and reopen or rescan to obtain a new live source.
- Automatic discovery scans only the selected stack frame's Locals and Arguments and keeps its search bounded.
- Ambiguous, compressed, planar, YUV, unsupported packed, offset, or method-only layouts fail visibly instead of being guessed.
- Camera acquisition/control, lighting, PLC/I/O, 3D point clouds, and depth/coordinate containers are outside this extension.
- Generic buffer inspection is not certification for a camera, frame grabber, driver, SDK, or hardware model.

## License And Support

Raw Buffer Visualizer is licensed under the [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE). External libraries retain their own licenses; see [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md).

Source, documentation, and issue reporting are available in the [Raw Buffer Visualizer repository](https://github.com/Noah8218/RawBufferVisualizer).

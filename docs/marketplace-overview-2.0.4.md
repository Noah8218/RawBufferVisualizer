# Raw Buffer Visualizer

Inspect C# machine-vision images and raw 2D buffers directly at a Visual Studio breakpoint. Raw Buffer Visualizer brings `System.Drawing.Bitmap`, OpenCvSharp/Emgu `Mat`, supported image collections, and managed or pointer-backed application buffers into one docked viewer—without adding debug-only image export or conversion code to your application.

![Open an OpenCvSharp Mat from its code DataTip](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-datatip-open.gif)

Hover an initialized `Mat` or `Bitmap` in the code editor, then select the visualizer magnifying-glass icon in its DataTip. The installed extension opens the live object in the docked viewer; the example above opens a real 1280 x 720 `BGR24` industrial-machine photograph from an OpenCvSharp `Mat`.

The same registered visualizer is available from Locals, Autos, and Watch:

![Open a real industrial Bitmap from Locals](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

This second Break Mode example selects the Raw Buffer Visualizer entry for an initialized `industrialBitmap` in Locals and opens a recognizable 1280 x 960 color PCB photograph. Both demonstrations are real debugger-visualizer handoffs, not procedural test patterns or preloaded viewer images.

## One Docked Image-Debugging Workflow

1. Install the extension and fully restart Visual Studio.
2. Start debugging and stop after the image variables have been assigned.
3. Use the DataTip, Locals, Autos, or Watch visualizer icon for registered images and collections. The first visualization opens the docked window automatically.
4. With the docked window open, let **Auto Inspect on Break** find initialized OpenCvSharp/Emgu Mats and compatible application wrappers, or use **Scan Now** on demand.
5. Select a thumbnail to inspect pixels, source bytes, dimensions, stride, format, diagnostics, and comparison views.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

The six-second overview uses the installed extension and a real CC0 industrial photograph throughout. It moves from registered visualizer handoff to live color pixels, automatic inspection, diagnostics, an intentionally wrong color stride, the top Buffer Doctor correction, and the restored color frame. No state is held longer than 1.2 seconds.

Opening or closing **Inspector**, **What's New**, **Environment**, or an interpretation panel never starts a scan. Automatic inspection runs only when the debugger enters Break Mode with **Auto Inspect on Break** enabled; **Scan Now** remains the explicit manual refresh.

![Inspect a real industrial PCB image in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-auto-inspector-pixel.png)

The same 1280 x 960 `BGR24` frame is open through OpenCvSharp, Emgu CV, a pointer-backed owner, and a neutral frame wrapper. The docked viewer keeps the image list, live B/G/R values, raw bytes, 5 x 5 neighborhood statistics, dimensions, stride, format, and source state visible together.

When several debugger images accumulate, the labelled **Clear all** action sits directly above the image list. It removes every loaded image and resets the viewer in one step without changing data in the paused debuggee; it stays disabled while the list is empty.

## Supported Image Sources

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` and Emgu CV `Mat`
- `RawBufferSnapshot` and pointer-backed `RawBufferView`
- Mapped `byte[]`, `ushort[]`, and `float[]` application buffers
- Mapped pointer objects with debugger-visible dimensions, stride or exact length, and pixel-format information
- Supported typed or mixed lists, ordinary or concurrent dictionaries, and one-dimensional image arrays
- `.rbuf.json` plus `.raw` snapshot files

Supported pixel formats include `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and four 8-bit Bayer phases.

## Connect Your Buffer

When a compatible application object is not registered directly, **Connect Your Buffer** lets you assign debugger-visible members to buffer, width, height, stride or length, pixel format, valid bits, and byte order. **Preview** reads the current paused buffer only when selected. If the interpretation still looks wrong, **Diagnose interpretation** ranks bounded alternatives in the same dialog. Only **Save Mapping** persists an editable per-type mapping for later sessions.

Registered and mapped sources use the same checked validation rules. Invalid dimensions, undersized stride, short buffer length, unsupported format/order values, overflowing descriptor arithmetic, or incompatible valid bits fail visibly instead of being guessed.

## Inspect, Compare, And Diagnose

- Read X/Y, GV or RGB values, channel swatches, raw source bytes, hover statistics, markers, line profile, histogram, and diagnostics.
- Use aspect-correct Fit, 1:1, wheel zoom, drag pan, and the high-zoom pixel overlay.
- Compare A/B images with linked views, split, absolute difference, and blink modes.
- Export the visible image as PNG or preserve the source as a raw snapshot.
- Open very large payloads through the file-backed tiled viewer.
- Use **Diagnose Buffer** to rank plausible interpretations for sheared, scrambled, dark, or incorrectly packed images.

The following fixture retains the original `BGR24` color bytes but deliberately reports the wrong stride: 2448 x 2048, declared stride `7344`, actual stride `7424` with 80 padding bytes per row.

![Buffer Doctor ranks color BGR24 interpretations for incorrect stride metadata](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-before.png)

Selecting the top `BGR24`, 2448 x 2048, stride `7424` interpretation restores row alignment and the original color scene immediately without another debugger round trip. Buffer Doctor changes the active interpretation metadata; it does not modify the paused application's source bytes.

![Buffer Doctor restores the color BGR24 PCB image after applying the correct stride](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## What's New In 2.0.4

- Moves the complete viewer reset directly above the image list as a clearly labelled **Clear all** action.
- Clears a directly opened 20-image collection in one step, including document-dependent viewer, comparison, diagnosis, and selection state.
- Keeps the action disabled when no images are loaded and explains that the paused debuggee data is unchanged.
- Retains the direct concurrent-dictionary and first-use `ImagePtr` fixes introduced in 2.0.3.
- Retains Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` support through the same Marketplace extension identity.

## Visual Studio Support

| Product | Supported range |
| --- | --- |
| Visual Studio 2022 | `17.9` or newer, x64 |
| Visual Studio 2026 | Stable `18.x`, x64 |

Community, Professional, and Enterprise are supported installation targets. Visual Studio 2019, Visual Studio 2022 `17.8` or earlier, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

The `2.0.4` package keeps the Visual Studio `17.9` SDK floor and the same isolated in-process/out-of-process package boundary used by `2.0.3`. Exact installed-host qualification is recorded separately from the supported range; a host is named as verified only after that exact package has passed there.

## Safe Debugging Behavior

- Stop after assignment; a breakpoint on the assignment statement can expose the previous or null value.
- A pointer requires valid dimensions, stride or length, pixel format, and a lifetime that remains valid while the debuggee is paused.
- After Continue or process exit, a live row keeps its last rendered pixels only as visual context and cannot read the old debuggee memory again.
- Automatic discovery is limited to the selected stack frame's Locals and Arguments and keeps collection/member inspection bounded.
- Ambiguous or unsupported layouts fail with a visible mapping or error result instead of silently guessing.

## License And Support

Raw Buffer Visualizer is licensed under the [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE). External libraries retain their own licenses; see [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md).

Both industrial demonstration photographs are CC0. Their sources, authors, exact-file hashes, and capture validation are recorded in the [industrial image test documentation](https://github.com/Noah8218/RawBufferVisualizer/blob/main/docs/industrial-image-testing.md). Incidental product marks in the photographs do not imply affiliation or endorsement.

Source code, documentation, and issue reporting are available in the [Raw Buffer Visualizer repository](https://github.com/Noah8218/RawBufferVisualizer).

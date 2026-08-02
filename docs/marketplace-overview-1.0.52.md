# Raw Buffer Visualizer 1.0.52 Marketplace Overview

Stop saving temporary images or adding debug-only conversion code. Inspect C# machine-vision images, discover compatible camera-frame wrappers, and diagnose raw-buffer layout mistakes while stopped at a breakpoint.

Raw Buffer Visualizer is an Image Watch-style debugger tool for C# developers. It combines registered Bitmap visualizers, automatic OpenCvSharp/Emgu Mat discovery, raw buffers, pointers, supported collections, and one docked image list inside Visual Studio 2022 and stable Visual Studio 2026.

![Raw Buffer Visualizer debugger workflow in Visual Studio](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## What's New in 1.0.52

- Stable Visual Studio 2026 registered debugger-visualizer activation through the current stable Visual Studio 17.14 Extensibility SDK line.
- Optional, bounded expansion of exact OpenCvSharp and Emgu CV `Mat` lists and one-dimensional arrays.
- Per-element failure isolation so null, disposed, unsupported, or unreadable Mats do not hide valid images.
- Snapshot leases that keep open file-backed documents safe from stale cleanup and retire replaced/closed payloads.
- One-time non-modal release highlights with persistent **Dismiss** and reusable **What's New** actions.

The public predecessor is `1.0.50`. The unpublished `1.0.51` candidate was superseded after stable Visual Studio 2026 testing found a host-contract mismatch; `1.0.52` includes that compatibility fix.

## Core Workflow

1. Install the extension and fully restart Visual Studio.
2. Open `View > Raw Buffer Visualizer` once.
3. Start debugging and stop after image variables have been assigned.
4. Use **Auto Inspect on Break** or **Scan Now** for initialized OpenCvSharp/Emgu Mats and compatible unregistered wrappers.
5. Enable the default-off **Mat collections** option when exact Mat lists or one-dimensional arrays should expand automatically.
6. Click the `Raw Buffer Visualizer` debugger icon for `System.Drawing.Bitmap` and other registered types.
7. Select a thumbnail and inspect pixels, bytes, dimensions, stride, format, diagnostics, and comparison views.
8. If a raw image looks wrong, run `Interpret > Diagnose Buffer` and select a candidate.

Preview and inspection remain explicit. Preference restoration does not start a scan, open an image, or change the active input.

## Automatic Vision Inspector

- Scans only the selected stack frame's Locals and Arguments.
- `[Auto]` means inference and current-buffer validation passed.
- `[Map]` means a compatible shape needs member or format confirmation through Smart Type Mapper.
- `[Failed]` isolates one unreadable value without blocking successful images.
- Repeated scans replace automatic rows instead of accumulating duplicates.
- Does not invoke arbitrary vendor methods, load camera SDK DLLs, or decode private native layouts.

`System.Drawing.Bitmap` remains on its registered debugger-visualizer path because safe extraction requires a debugger-side `LockBits` lifetime.

## Vision Buffer Doctor

When a raw image looks sheared, scrambled, too dark, or incorrectly packed, Buffer Doctor ranks plausible width, height, stride, pixel-format, valid-bit, and byte-order interpretations from bounded samples. Applying a candidate does not require another debugger round trip.

RGB/BGR order and Bayer phase can remain inherently ambiguous and require developer confirmation.

## Smart Type Mapper

For compatible company-specific wrappers, **Map This Type** lets the developer confirm buffer, dimension, stride, and format members once. The mapping is restored for the same type and remains visible, editable, and resettable.

## Reliable Handoff and Snapshot Ownership

- A handoff succeeds only after the docked viewer opens it and publishes ACK; failures publish NACK with a reason.
- Atomic claiming prevents two Visual Studio consumers from opening the same request.
- Every open owned snapshot holds a lease, so the 24-hour stale sweep cannot remove its payload.
- Preview-to-full replacement releases the preview directory after the replacement source is ready.
- Delete, **Clear**, and Tool Window disposal release the current owned payload.

## Viewer Features

- One docked image list for Bitmap, Mats, raw buffers, pointers, and supported collections.
- Aspect-correct Fit, 1:1, wheel zoom, drag pan, and high-zoom pixel overlay.
- X/Y, GV/RGB, channel swatches, source bytes, hover statistics, markers, line profile, histogram, and diagnostics.
- A/B, linked views, split, absolute difference, and blink comparison.
- PNG and raw snapshot export.
- File-backed tiled display for very large raw payloads.

## Supported Inputs

- `RawBufferSnapshot` and `RawBufferView`
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` and Emgu CV `Mat`
- Typed or mixed supported image lists, dictionaries, and arrays
- Optional automatic exact Mat lists and one-dimensional arrays
- Unregistered pointer/array-backed wrappers whose required members are visible to the debugger
- `.rbuf.json` plus `.raw` snapshot files

Supported pixel formats include `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and four Bayer 8-bit phases.

## Visual Studio Support

| Product | Supported range | Qualification |
| --- | --- | --- |
| Visual Studio 2022 | `17.14` or newer, x64 | Community, Professional, Enterprise; exact Community `17.14.33` runtime qualification is recorded separately. |
| Visual Studio 2026 | Stable `18.x`, x64 | Community `18.8.2` passed the installed core runtime matrix. |

Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets. The manifest uses `[17.14,18.0)` under Microsoft's VSIX API-version compatibility model.

## Safety and Limits

- Stop after assignment; a breakpoint on the assignment statement can expose the previous or null value.
- Automatic discovery is bounded to direct and one-level nested members.
- Mat collection work is capped at 8 items per collection, 16 items, and 8 roots per scan.
- Registered collection visualization processes at most 256 entries.
- A raw pointer without valid dimensions, stride/length, format, and lifetime cannot be opened safely.
- Industrial-camera fixtures verify data contracts only; they are not real SDK, hardware, lighting, or acquisition certification.

## License

Raw Buffer Visualizer is licensed under the MIT License. External libraries retain their own licenses; see `THIRD-PARTY-NOTICES.md` in the source repository.

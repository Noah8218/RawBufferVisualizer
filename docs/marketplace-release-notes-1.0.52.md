# Raw Buffer Visualizer 1.0.52

Version `1.0.52` is the qualified local successor to the public `1.0.50` package. The unpublished `1.0.51` candidate was superseded after stable Visual Studio 2026 runtime testing exposed an Extensibility host-contract mismatch. Marketplace publication remains gated by the separate-PC public-package update check.

## New since public 1.0.50

- A persisted, default-off **Mat collections** option expands exact OpenCvSharp and Emgu CV `Mat` `List<T>` values and one-dimensional arrays from the selected stack frame.
- Collection elements succeed or fail independently, with work capped at 8 items per collection, 16 items, and 8 roots per scan.
- The Tool Window shows the `1.0.52` highlights once. **Dismiss** persists across Visual Studio restarts, while **What's New** reopens them without starting a scan or opening an image.

## Fixed

- Updated the stable Visual Studio Extensibility SDK so registered `System.Drawing.Bitmap` debugger visualizers activate on stable Visual Studio 2026 `18.x`.
- Open file-backed documents now hold a snapshot lease, preventing the 24-hour stale sweep from deleting data that is still displayed.
- Preview-to-full handoff replacement releases the previous snapshot payload; document removal, **Clear**, and Tool Window disposal release the current payload.

## Reliability

- Atomic request claiming plus explicit ACK/NACK remains the debugger handoff contract.
- Document activation/removal/disposal and claimed-handoff completion now have independently tested owners.
- New and selected images keep an aspect-correct Fit; wheel zoom, drag pan, and 1:1 preserve manual navigation.
- One unreadable automatic candidate remains isolated as `[Failed]` while valid images continue opening.

## Visual Studio compatibility

- Supports Visual Studio 2022 `17.14` or newer and stable Visual Studio 2026 `18.x` on x64 Community, Professional, and Enterprise editions.
- Visual Studio 2022 `17.9`-`17.13` remain supported by the public `1.0.50` line but are outside the `1.0.52` runtime contract.
- Stable Visual Studio 2026 Community `18.8.2` passed the installed core runtime matrix, including registered Bitmap handoff, automatic OpenCvSharp/Emgu Mats, automatic Mat collections, menu registration, and protocol checks.
- Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

After installing or updating, close every Visual Studio window and restart Visual Studio.

# Raw Buffer Visualizer 1.0.51

Version `1.0.51` adds bounded automatic Mat collection inspection and clear in-product release highlights while carrying forward the `1.0.50` handoff, menu, direct-Mat, and Fit reliability improvements.

## New

- A persisted, default-off **Mat collections** option can expand exact OpenCvSharp and Emgu CV `Mat` `List<T>` values and one-dimensional arrays from the selected stack frame.
- Collection elements succeed or fail independently, so a null or disposed Mat does not hide valid images from the same collection.
- Automatic collection work is capped at 8 items per collection, 16 items and 8 roots per scan.
- The Tool Window shows the `1.0.51` highlights once. **Dismiss** persists across Visual Studio restarts, while **What's New** can reopen them without starting a scan or opening an image.

## Included reliability improvements

- Automatic Inspector opens initialized exact OpenCvSharp and Emgu CV `Mat` values after assignment while `System.Drawing.Bitmap` remains on its registered debugger-visualizer path.
- Atomic request claiming plus explicit ACK/NACK prevents a missing handoff file from being mistaken for success.
- The Visual Studio command-table cache and package checks prevent duplicate Raw Buffer Visualizer View commands.
- New and selected images keep an aspect-correct Fit; wheel zoom, drag pan, and 1:1 preserve manual navigation.
- One unreadable automatic candidate remains isolated as `[Failed]` while valid images continue opening.

Vision Buffer Doctor and Smart Type Mapper remain included.

## Visual Studio compatibility

- Supports Visual Studio 2022 `17.9` or newer and stable Visual Studio 2026 `18.x` on x64 Community, Professional, and Enterprise editions.
- Visual Studio 2022 `17.14` is the recommended serviced 2022 baseline; exact `1.0.51` installed runtime qualification passed on Community `17.14.33`.
- Visual Studio 2026 uses Microsoft's API-version compatibility model for VSIX extensions. Exact Community `18.7.1` installed runtime qualification is pending and remains recorded separately from the supported target.
- Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds are not supported release targets.

After installing or updating, close every Visual Studio window and restart Visual Studio.

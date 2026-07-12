# Raw Buffer Visualizer 1.0.30

- Adds a reviewed 20-second GIF and H.264 MP4 showing the real Visual Studio debugger workflow from visualizer invocation through pixel inspection.
- Reworks the Marketplace Overview and GitHub README around C# machine-vision debugging for OpenCvSharp Mat, Emgu CV Mat, Bitmap, IntPtr, and raw image buffers.
- Adds `tools/Create-DemoMedia.ps1` so the Marketplace and README media can be reproduced with FFmpeg `palettegen` and `paletteuse`.
- Keeps the existing Visual Studio 2022 17.9-compatible debugger visualizers, docked viewer, image collections, pixel diagnostics, comparison tools, and file-backed large-image display.

Runtime behavior is unchanged from `1.0.29.0`.

Known limits:

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Tested library versions are compatibility points, not a guarantee for every intermediate package build.

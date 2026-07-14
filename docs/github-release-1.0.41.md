# Raw Buffer Visualizer 1.0.41

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Restores the yellow 5x5 and cyan pixel marker so they follow live mouse movement in the manually rendered docked viewer.
- Keeps the image marker synchronized with hover coordinates, decoded values, raw bytes, and statistics.
- Preserves the fixed marker and inspector values while Pin is active, then resumes live hover after Clear.
- Coalesces hover redraws without re-uploading the image texture.

## Verified

- Three settled hover positions each update the inspector and produce a rendered frame at 540, 900, and 1160 pixel docked widths.
- Actual Visual Studio 2022 docked smoke covers external mouse-wheel zoom, drag pan, pixel inspection, and non-blank rendering.
- Standalone interaction smoke covers open, tabs, zoom, pan, Fit, 1:1, pixel values, PNG export, snapshot export, and linked views.
- The full Release build, self-test suite, and VSIX package validation pass.

## Upgrade

After installing or updating to `1.0.41.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Known Limits

- One collection invocation processes the first 256 entries.
- Visual Studio's built-in `IEnumerable Visualizer` can remain as another menu choice.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

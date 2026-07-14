# Raw Buffer Visualizer 1.0.40

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Stabilizes the initial Fit view after Visual Studio docks or resizes the viewer.
- Keeps the image and viewport aspect ratios aligned during the initial layout pass.
- Makes Pin hold the selected pixel, neighborhood, statistics, marker, and status values instead of allowing hover updates to replace them.
- Makes Clear return the inspector to live hover updates.

## Verified

- Actual Visual Studio 2022 docked smoke covers mouse-wheel zoom, drag pan, pixel inspection, and rendered output.
- Docked layout checks cover 540, 900, and 1160 pixel widths with matching image and viewport aspect ratios.
- Pin and Clear behavior is exercised by an automated inspector-state regression check.
- The full Release build, test suite, and VSIX package validation pass.

## Upgrade

After installing or updating to `1.0.40.0`, close all Visual Studio windows and reopen Visual Studio.

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

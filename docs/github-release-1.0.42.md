# Raw Buffer Visualizer 1.0.42

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Added

- Failed visualizations remain visible as red rows with a stable error ID and failure reason.
- `Copy Report` captures extension, Visual Studio, source, diagnostic, and exception context without including image payloads.
- `Open Logs` writes the latest support report and locates the relevant log folder.
- Malformed debugger handoffs become actionable error rows.

## Preserved

- Selecting a valid image after an error restores the normal viewer and its zoom, pan, pixel inspection, and export behavior.
- The compact top toolbar and single docked image-list workflow remain unchanged.

## Verified

- Actual Visual Studio 2022 docked smoke covers normal images, the error panel, support-report availability, external mouse-wheel zoom, drag pan, and non-blank rendering.
- Layout smoke at 540, 900, and 1160 pixels covers report copy, report-file creation, malformed handoff handling, and recovery to a valid image.
- Standalone interaction smoke covers open, tabs, zoom, pan, Fit, 1:1, pixel values, PNG export, snapshot export, and linked views.
- Legacy compatibility smoke passes for Bitmap plus five OpenCvSharp and five Emgu CV package versions.
- The full Release build, self-test suite, and VSIX package validation pass.

## Upgrade

After installing or updating to `1.0.42.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Known Limits

- One collection invocation processes the first 256 entries.
- Visual Studio's built-in `IEnumerable Visualizer` can remain as another menu choice.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Support reports exclude image payloads but can contain local paths and variable names; review them before sharing.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

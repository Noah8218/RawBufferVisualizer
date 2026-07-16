# Raw Buffer Visualizer 1.0.43

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Added

- Large debugger-backed images now show a bounded sampled preview before the full-resolution source is ready.
- Large Bitmap, OpenCvSharp `Mat`, Emgu CV `Mat`, `RawBufferView`, and ImagePtr-style values use tiled reads from the paused debuggee instead of a full temporary raw-file copy.
- Progressive viewport rendering cancels stale work during zoom, pan, image changes, and interpretation changes.
- When live debugger memory becomes unavailable, the last rendered image remains visible and the image row reports `Unavailable`.

## Preserved

- Bitmap, OpenCvSharp, Emgu CV, raw-buffer, pointer-object, array, list, and dictionary visualizers continue to accumulate in one docked image list.
- Pixel/GV and raw-byte inspection, Fit, 1:1, mouse zoom and pan, selection and marker state, export, comparison, and actionable error reports remain available.

## Verified

- Actual Visual Studio 2022 installed-VSIX smoke opens `8192 x 8192` OpenCvSharp and Emgu CV Mats, verifies pixel values, and confirms that terminating the debuggee does not raise an unhandled process-memory dialog.
- Dense non-sparse `100000 x 100000` and `200000 x 200000` Mono8 payloads display through file-backed sampling while process working set remains below 100 MB in the local release gate.
- Standalone interaction smoke covers pixel/GV read, Fit, 1:1, slider and wheel zoom, PNG and snapshot export, tabs, and linked views.
- Legacy compatibility smoke passes for Bitmap plus five OpenCvSharp and five Emgu CV package versions.
- The full Release build, self-test suite, responsive-layout smoke, and Marketplace package dry-run pass.

## Upgrade

After installing or updating to `1.0.43.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Known Limits

- Live debugger memory can be read only while the debuggee is paused. After Continue or process exit, pause again and reopen the visualizer.
- One collection invocation processes the first 256 entries.
- Visual Studio's built-in `IEnumerable Visualizer` can remain as another menu choice.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Support reports exclude image payloads but can contain local paths and variable names; review them before sharing.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

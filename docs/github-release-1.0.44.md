# Raw Buffer Visualizer 1.0.44

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Large images no longer turn into blank, white, black, or single-color frames when a progressive viewport upload would exceed the active OpenGL texture limit.
- Progressive rendering now chooses a sample step that fits the visible region inside the current texture limit.
- Periodic image structures no longer collapse to one sampled phase because the large-image path no longer rounds every sample step to a power of two.
- Texture upload errors are detected and surfaced instead of leaving an invalid texture visible.

## Preserved

- Bitmap, OpenCvSharp, Emgu CV, raw-buffer, pointer-object, array, list, and dictionary visualizers continue to accumulate in one docked image list.
- Pixel/GV and raw-byte inspection, Fit, 1:1, mouse-wheel zoom, drag pan, selection and marker state, export, comparison, actionable error reports, and preview-first large debugger image loading remain available.

## Verified

- Dense `24000 x 24000` Mono8 periodic data reproduces the former failure and now renders 199 sampled colors at 7.3% zoom with OpenGL error 0.
- A 1272-pixel-wide 1:1 viewport uses sample step 2 and a `1024 x 1024` upload on a context reporting `GL_MAX_TEXTURE_SIZE = 1024`.
- Dense non-sparse `100000 x 100000` and `200000 x 200000` Mono8 payloads pass file-backed zoom and pan regression checks.
- Standalone interaction smoke covers pixel/GV read, Fit, 1:1, mouse-wheel zoom, drag pan, PNG and snapshot export, tabs, and linked views.
- The full Release build, self-test suite, responsive-layout smoke, and 240-iteration docked memory soak pass.

## Upgrade

After installing or updating to `1.0.44.0`, close all Visual Studio windows and reopen Visual Studio.

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

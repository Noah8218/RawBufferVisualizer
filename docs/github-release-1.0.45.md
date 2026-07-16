# Raw Buffer Visualizer 1.0.45

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Large-Image Performance

The current viewer uses preview-first debugger transfer, file-backed tiled sources, and progressive viewport reads. This release updates the Marketplace and repository documentation with the measured improvement.

Same-machine automated viewer benchmark with the same dense `5000 x 5000 Mono8` input:

| Metric | Before update | Current (`1.0.45`) | Improvement |
| --- | ---: | ---: | ---: |
| Initial open path | `179.818 ms` | `115.369 ms` | `35.8%` lower |
| Zoom average frame | `16.684 ms` | `13.765 ms` | `17.5%` lower |
| Zoom maximum frame | `49.397 ms` | `36.527 ms` | `26.1%` lower |
| Pan maximum frame | `21.951 ms` | `17.056 ms` | `22.3%` lower |
| Pan average tile upload | `20.410 ms` | `15.211 ms` | `25.5%` lower |

An installed-VSIX Visual Studio 2022 `17.14` test also exercised a dense file-backed `24000 x 24000 Mono8` image with real mouse input. Across `87` wheel and `269` drag events, rendered frames averaged `6.515 ms` with a `13.642 ms` maximum. Results vary by hardware and image source.

## Preserved

- Stable large-image zoom and pan without blank or single-color frames.
- Bitmap, OpenCvSharp, Emgu CV, raw-buffer, pointer-object, array, list, and dictionary visualizers in one docked image list.
- Pixel/GV and raw-byte inspection, selection and marker state, export, comparison, and actionable error reports.
- Dense file-backed `100000 x 100000` and `200000 x 200000 Mono8` validation paths.

## Upgrade

After installing or updating to `1.0.45.0`, close all Visual Studio windows and reopen Visual Studio before debugging.

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

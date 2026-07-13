# Raw Buffer Visualizer 1.0.37

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and explicitly supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Prevents Visual Studio 2022's expression evaluator from failing after extension installation. The Classic visualizer no longer registers open generic collection targets.
- Keeps collection inspection available for the verified closed types `List<object>`, `Dictionary<string, object>`, and `object[]`.
- Keeps valid entries and null, unsupported, or failed entries visible together in the single docked image list.
- Prevents build-node reuse during packaging and removes only orphaned Visual Studio MSBuild workers before local VSIX replacement.

## Upgrade

After installing or updating to `1.0.37.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Supported Collection Types

- `List<object>`
- `Dictionary<string, object>`
- `object[]`

Typed collections such as `List<Mat>` are not registered directly. Convert them to one of the closed types above before invoking the collection visualizer. This restriction preserves Visual Studio 2022 expression-evaluator stability.

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Unsupported planar, compressed, YUV, signed integer, and additional packed formats fail with visible diagnostics.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

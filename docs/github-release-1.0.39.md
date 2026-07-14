# Raw Buffer Visualizer 1.0.39

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and typed or mixed image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Restores debugger visualizer discovery for typed `List<TImage>` and `Dictionary<TKey,TImage>` values.
- Preserves individual Bitmap, OpenCvSharp `Mat`, Emgu CV `Mat`, raw-buffer, and pointer-backed image visualizers.
- Preserves the single docked workflow: the Marketplace-compatible provider hands images to the main viewer and its temporary host closes automatically.
- Prevents releases with missing Modern provider metadata or missing open generic collection targets.
- Removes stale Classic debugger visualizer assemblies from Marketplace and local reinstall packages.

## Verified

- `List<OpenCvSharp.Mat>` and `Dictionary<string, OpenCvSharp.Mat>` append their entries to one docked image list in an actual Visual Studio 2022 debug session.
- Typed OpenCvSharp, Emgu CV, and Bitmap list and dictionary transfers pass the self-test suite.
- The full Debug solution build and Release VSIX package validation pass.
- The VSIX contains the required Modern providers and does not contain the obsolete Classic visualizer DLL.

## Upgrade

After installing or updating to `1.0.39.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Known Limits

- One collection invocation processes the first 256 entries.
- Open generic registration makes Raw Buffer Visualizer available for typed and mixed lists and dictionaries. Only supported image entries are transferred; other entries become error rows.
- Visual Studio's built-in `IEnumerable Visualizer` can remain as another menu choice.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

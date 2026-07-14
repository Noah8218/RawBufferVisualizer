# Raw Buffer Visualizer 1.0.38

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and explicitly supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Restores debugger visualizer discovery for OpenCvSharp `Mat`, Emgu CV `Mat`, raw buffers, pointer-backed image objects, and supported collections in the Marketplace VSIX.
- Preserves the single docked workflow: the Marketplace-compatible provider hands the image to the main viewer and its temporary host closes automatically.
- Prevents releases with missing provider metadata or missing tested OpenCvSharp/Emgu target identities.
- Removes stale Raw Buffer Visualizer Classic DLL registrations during local reinstalls.

## Verified

- OpenCvSharp and Emgu visualizer icons appear in an actual Visual Studio 2022 debug session.
- OpenCvSharp and Emgu images render and accumulate in one docked image list.
- Typed `List<OpenCvSharp.Mat>` and `Dictionary<string, OpenCvSharp.Mat>` values append their entries to that same docked list in an actual Visual Studio 2022 debug session.
- Typed OpenCvSharp, Emgu CV, and Bitmap collection transfers pass the self-test suite.
- Bitmap transfer and the full self-test suite pass.
- OpenCvSharp `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627` pass compatibility transfer tests.
- Emgu CV `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924` pass compatibility transfer tests.

## Upgrade

After installing or updating to `1.0.38.0`, close all Visual Studio windows and reopen Visual Studio.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Known Limits

- Open generic registration makes Raw Buffer Visualizer available for typed and mixed `List<T>` and `Dictionary<TKey,TValue>` values. Only supported image entries are transferred; other entries become error rows.
- Visual Studio's built-in `IEnumerable Visualizer` can remain as another menu choice.
- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

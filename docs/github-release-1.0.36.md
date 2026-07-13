# Raw Buffer Visualizer 1.0.36

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- All supported single-image and collection visualizers now feed the same docked Raw Buffer Visualizer window.
- The Modern debugger ToolWindow providers that created a second lower window are no longer published.
- A debugger invocation is routed to the Visual Studio process that invoked it, preventing another running Visual Studio instance from receiving the image.
- Collection items use bounded chunk transfer. Valid entries remain image rows, while null, unsupported, and failed entries remain visible as red error rows with their reason.
- Collections above 256 entries show the first 256 entries and append an explanatory limit error row.
- The developer installer registers the Classic visualizers and verifies that the installed extension does not contain Modern debugger visualizer metadata.

## Upgrade

After installing or updating to `1.0.36.0`, close all Visual Studio windows and reopen Visual Studio. If an old lower `Raw Buffer Visualizer` tab remains from version `1.0.34.0` or earlier, close that tab once; new debugger invocations use only the main docked viewer.

For a local repository build, close Visual Studio and run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

## Supported Inputs

- `RawBufferSnapshot` and `RawBufferView`
- ImagePtr-style pointer objects
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`
- Emgu CV `Mat`
- Supported `List<T>`, `Dictionary<TKey,TValue>`, and array inputs
- `.rbuf.json` plus `.raw` snapshot files

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Unsupported planar, compressed, YUV, signed integer, and additional packed formats fail with visible diagnostics.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

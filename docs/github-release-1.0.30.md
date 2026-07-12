# Raw Buffer Visualizer 1.0.30

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Highlights

- Adds a reviewed 20-second GIF and H.264 MP4 showing the real Visual Studio debugger workflow.
- Reworks the README and Marketplace Overview around OpenCvSharp, Emgu CV, Bitmap, IntPtr, and raw-buffer debugging.
- Adds `tools/Create-DemoMedia.ps1` to reproduce the 960 px, 12 fps GIF and web-ready MP4.
- Keeps one docked image list for variables inspected from DataTip, Watch, Locals, or Autos.
- Retains pixel X/Y, GV or RGB values, raw bytes, stride/format diagnostics, comparison tools, and file-backed large-image display.

## Supported Inputs

- `RawBufferSnapshot` and `RawBufferView`
- ImagePtr-style pointer objects
- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`
- Emgu CV `Mat`
- Supported `List<T>`, `Dictionary<TKey,TValue>`, and array inputs
- `.rbuf.json` plus `.raw` snapshot files

## Compatibility Checks

- OpenCvSharp4: `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627`.
- Emgu CV: `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924`.
- Visual Studio 2022 `17.9` or newer.

Runtime behavior is unchanged from `1.0.29.0`. These package versions are tested compatibility points, not a guarantee for every intermediate package build.

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Unsupported planar, compressed, YUV, signed integer, and additional packed formats fail with visible diagnostics.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

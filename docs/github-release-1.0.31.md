# Raw Buffer Visualizer 1.0.31

Raw Buffer Visualizer is an Image Watch style debugger tool for C# machine-vision development. It inspects Bitmap, OpenCvSharp Mat, Emgu CV Mat, pointer-backed images, raw buffers, and supported image collections directly inside Visual Studio.

[Install from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer)

## Fixed

- Debugger snapshots are now routed to the docked Raw Buffer Visualizer that belongs to the Visual Studio instance where the visualizer was invoked.
- Running two or more Visual Studio instances no longer allows a snapshot from a later instance to appear in the first instance's viewer.
- The debugger visualizer now identifies the hosting `devenv` process and targets that process's DTE command instead of selecting a generic running Visual Studio automation object.
- Handoff inboxes are isolated by Visual Studio process ID, with regression tests covering cross-instance separation.
- Package diagnostics include the receiving Visual Studio process ID for easier troubleshooting.

## Upgrade

Install or update to `1.0.31.0`, close all Visual Studio windows when prompted, and reopen Visual Studio before debugging.

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

These package versions are tested compatibility points, not a guarantee for every intermediate package build.

## Known Limits

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Vendor SDK-specific adapters are not bundled. Use `RawBufferView` or an ImagePtr-style shape for camera and frame-grabber buffers.
- Unsupported planar, compressed, YUV, signed integer, and additional packed formats fail with visible diagnostics.

## Source And License

Source: https://github.com/Noah8218/RawBufferVisualizer

Licensed under the MIT License. External dependencies retain their own licenses; see `THIRD-PARTY-NOTICES.md`.

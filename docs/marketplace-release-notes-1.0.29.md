# Raw Buffer Visualizer 1.0.29

- Updates the Marketplace Overview with reviewed screenshots of the docked image viewer, visible error rows, and file-backed large-image rendering.
- Documents real OpenCvSharp `Mat` transfer checks with OpenCvSharp4 `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, and `4.13.0.20260627`.
- Documents real Emgu CV `Mat` transfer checks with Emgu CV `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, and `4.13.0.5924`.
- Retains debugger visualization for supported `List<T>`, `Dictionary<TKey,TValue>`, arrays, raw buffers, pointer-backed images, `System.Drawing.Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat`.

This release uses a new VSIX version so the updated Marketplace listing can be published. Runtime behavior is unchanged from `1.0.28.0`.

Known limits:

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Tested library versions are compatibility points, not a guarantee for every intermediate package build.

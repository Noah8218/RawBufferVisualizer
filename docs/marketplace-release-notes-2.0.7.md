# Raw Buffer Visualizer 2.0.7

Version `2.0.7` adds signed 32-bit, one-channel image matrices.

## Added

- OpenCvSharp `CV_32SC1` and Emgu CV `Cv32S` C1 now open as `Int32` through direct debugger visualizers.
- Automatic Inspector and supported Mat collections use the same signed format mapping.
- Min/max grayscale autoscaling makes label and integer-result maps visible while pixel inspection retains the exact signed value and four raw bytes.
- Pointer, snapshot, mapped `int[]`, sampled preview, tiled rendering, byte-order, and padded-stride paths accept `Int32`.

`CV_32SC1` means one channel with signed 32-bit elements; it does not mean 32 channels. Multi-channel signed matrices and 3D data remain unsupported. Visual Studio 2022 `17.9+` x64 and stable Visual Studio 2026 `18.x` remain the installation targets.

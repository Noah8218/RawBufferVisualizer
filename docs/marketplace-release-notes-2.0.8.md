# Raw Buffer Visualizer 2.0.8

Version `2.0.8` corrects the Visual Studio 2022 debugger payload used to open signed 32-bit matrices.

## Fixed

- OpenCvSharp `CV_32SC1` and Emgu CV `Cv32S` C1 now use the current `Int32` ObjectSource on the Visual Studio 2022 debugger-host path.
- The VSIX now packages Core, SDK, ObjectSource, and its dependency manifest from the same fresh routed Release build.
- Packaging fails when SHA-256 comparison finds that any debugger-side VSIX payload differs from its fresh build output.

## Improved

- Automatic Inspector now bounds each current-frame discovery to 128 candidates and loads an initial batch of at most eight images or a soft two-second budget.
- Large results expose **Load next 8**, **Load all this Break**, and **Stop**, with visible candidate, refreshed, deferred, and failed counts.
- Repeated Break/F10/Scan Now operations refresh stable rows in place, coalesce overlapping scans, and keep mapping-required objects behind the mapping gate even when type analysis is cached.

Existing signed min/max grayscale display, exact signed pixel/raw-byte inspection, collections, pointer/snapshot inputs, mapped `int[]`, byte order, padded stride, sampled previews, and tiled rendering are retained.

Visual Studio 2022 `17.9+` x64 and stable Visual Studio 2026 `18.x` remain the installation targets.

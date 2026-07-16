# Raw Buffer Visualizer 1.0.43

- Shows a bounded sampled preview before opening large debugger-backed images at full resolution.
- Reads large Bitmap, OpenCvSharp `Mat`, Emgu CV `Mat`, `RawBufferView`, and ImagePtr-style payloads as tiled data from the paused debuggee instead of creating a full temporary raw copy.
- Cancels obsolete background tile work while zooming, panning, switching images, or changing interpretation settings.
- Keeps the last rendered image visible and marks it `Unavailable` when the debuggee continues or exits, instead of showing an unhandled process-memory error.
- Preserves the single docked image list, pixel/GV inspection, collections, export, comparison, and actionable error reports.

Validation:

- Actual Visual Studio 2022 installed-VSIX smoke with `8192 x 8192` OpenCvSharp and Emgu CV Mats, pixel checks, bounded temporary storage, and debuggee-exit recovery.
- Non-sparse dense `100000 x 100000` and `200000 x 200000` Mono8 payloads displayed through file-backed sampling without loading the complete payload into managed memory.
- Full Release solution build, self-tests, standalone interaction smoke, responsive docked layouts, legacy Bitmap/OpenCvSharp/Emgu compatibility matrix, and Marketplace package validation.

Large live debugger images are readable only while the debuggee is paused. After Continue or process exit, pause again and reopen the visualizer to refresh the image.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

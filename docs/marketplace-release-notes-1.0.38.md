# Raw Buffer Visualizer 1.0.38

- Restores the Raw Buffer Visualizer icon for OpenCvSharp `Mat`, Emgu CV `Mat`, raw buffers, pointer-backed images, and supported collections after a Marketplace install.
- Keeps every invocation in the single main docked image list by closing the temporary debugger-provider host after the image handoff.
- Restores typed `List<TImage>` and `Dictionary<TKey,TImage>` visualization, while retaining mixed object collections and supported image arrays.
- Strengthens VSIX packaging validation so a release fails when required debugger provider metadata or tested OpenCvSharp and Emgu CV targets are missing.
- Removes obsolete Raw Buffer Visualizer Classic DLLs during local reinstall to prevent stale registrations from masking Marketplace behavior.

Validation:

- Actual Visual Studio 2022 DataTip/Locals invocation with OpenCvSharp `Mat` and Emgu CV `Mat`.
- One docked viewer with both images accumulated in the same list.
- Actual Visual Studio 2022 invocation of `List<OpenCvSharp.Mat>` and `Dictionary<string, OpenCvSharp.Mat>`, with all entries accumulated in that same docked list.
- Typed OpenCvSharp, Emgu CV, and Bitmap collection transfer self-tests.
- Bitmap transfer plus five OpenCvSharp and five Emgu CV compatibility points.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

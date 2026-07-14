# Raw Buffer Visualizer 1.0.39

- Restores Raw Buffer Visualizer discovery for typed `List<TImage>` and `Dictionary<TKey,TImage>` values, including OpenCvSharp `Mat`, Emgu CV `Mat`, and `System.Drawing.Bitmap` collections.
- Retains mixed object collections, supported image arrays, raw buffers, pointer-backed images, and individual image visualizers.
- Keeps every invocation in the single main docked image list by closing the temporary debugger-provider host after the image handoff.
- Strengthens VSIX validation for required Modern debugger providers and open generic collection targets.
- Removes obsolete Classic debugger visualizer assemblies from the Marketplace package and local reinstall path.

Validation:

- Actual Visual Studio 2022 invocation of `List<OpenCvSharp.Mat>` and `Dictionary<string, OpenCvSharp.Mat>` into one docked image list.
- Typed OpenCvSharp, Emgu CV, and Bitmap collection transfer self-tests.
- Full Debug solution build and Release VSIX packaging.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

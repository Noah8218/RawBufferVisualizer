# Raw Buffer Visualizer 1.0.37

- Fixes a Visual Studio 2022 expression-evaluator failure caused by Classic visualizer registrations for open generic collection types.
- Registers the verified closed collection types `List<object>`, `Dictionary<string, object>`, and `object[]` while retaining single-image support.
- Keeps valid collection items and per-item failures together in the single docked image list.
- Keeps the 256-entry collection limit and bounded chunk transfer for predictable debugger-time memory use.
- Improves local reinstall reliability by disabling build-node reuse during packaging and removing only orphaned Visual Studio MSBuild workers before VSIX replacement.

Known limits:

- Typed collections such as `List<Mat>` must be converted to `List<object>`, `Dictionary<string, object>`, or `object[]` before collection visualization.
- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Tested OpenCvSharp and Emgu CV versions are compatibility points, not a guarantee for every intermediate package build.

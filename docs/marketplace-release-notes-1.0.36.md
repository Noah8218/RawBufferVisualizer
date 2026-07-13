# Raw Buffer Visualizer 1.0.36

- Routes `RawBufferSnapshot`, `RawBufferView`, `Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, ImagePtr-style objects, and supported image collections through one docked Raw Buffer Visualizer window.
- Removes the Modern debugger ToolWindow providers that could leave a second lower Raw Buffer Visualizer open.
- Keeps collection failures visible beside valid entries as red error rows with the item name and reason.
- Transfers collection items in bounded chunks and adds a visible limit row when a collection contains more than 256 entries.
- Keeps debugger handoffs isolated by the hosting Visual Studio process when multiple Visual Studio instances are running.
- Updates the developer installer to register the Classic visualizers and reject packages that still publish a Modern debugger visualizer provider.

Known limits:

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- An old lower ToolWindow saved by version `1.0.34.0` or earlier may remain in the Visual Studio layout until it is closed once.
- Tested library versions are compatibility points, not a guarantee for every intermediate package build.

# Raw Buffer Visualizer 1.0.31

- Fixes multi-instance routing so debugger snapshots are delivered only to the Raw Buffer Visualizer in the Visual Studio instance that invoked them.
- Targets the hosting `devenv` process when opening the docked viewer, preventing another running Visual Studio instance from receiving the image.
- Isolates handoff queues per Visual Studio process and adds regression coverage for cross-instance delivery.
- Adds process-tagged package diagnostics to make routing issues easier to identify.
- Retains the Visual Studio 2022 17.9-compatible debugger visualizers, docked viewer, image collections, pixel diagnostics, comparison tools, and file-backed large-image display.

Known limits:

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Tested library versions are compatibility points, not a guarantee for every intermediate package build.

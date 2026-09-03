# Raw Buffer Visualizer 2.0.4

Version `2.0.4` puts the complete viewer reset directly above the accumulated image list as **Clear all**.

## Improved

- Moves the existing full reset from the top toolbar to the image-list header where loaded debugger images are managed.
- Keeps the icon and explicit **Clear all** label readable across narrow, medium, and wide docked layouts.
- Disables the action while the list is empty and explains that clearing does not change data in the paused debuggee.

## Verified

- A directly opened 20-image collection transfers all 20 entries and clears in one action.
- The same action resets image documents, viewer content, comparison state, diagnostics, interpretation, selection, and owned temporary resources without a confirmation dialog.

Direct concurrent-dictionary visualization, the first-use `ImagePtr` handoff repair, Visual Studio 2022 `17.9+`, and stable Visual Studio 2026 `18.x` support remain included. After installing or updating, close every Visual Studio window and restart Visual Studio.

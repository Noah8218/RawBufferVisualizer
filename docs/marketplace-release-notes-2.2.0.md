# Raw Buffer Visualizer 2.2.0

- Opens supported image variables directly in the docked viewer without displaying an auxiliary Raw Buffer visualizer dialog.
- Gives Raw Buffer priority for recognized image collections while retaining the usual visualizer for non-image collections such as List<int>. The menu uses the product name without Direct or test labels.
- Adds two-point horizontal and vertical pixel measurement. Sampled previews disable measurement; full-resolution images enable it. Selecting another image clears the measurement.
- Keeps selected image rows visible, wraps long error titles, and prevents a late sampled preview from replacing an already loaded full-resolution image.
- Preserves checked live reads for large pointer-backed images and correct debugger byte forwarding. Live sources become unavailable when execution continues or the process exits; captured snapshots remain viewable.

Installation targets remain Visual Studio 2022 17.9+ x64 and stable Visual Studio 2026 18.x. Runtime behavior depends on the supported image type and source lifetime. Sparse object-valued collections use bounded inspection; if no image is recognized in the inspected portion, the usual collection visualizer remains available.
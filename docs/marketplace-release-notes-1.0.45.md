# Raw Buffer Visualizer 1.0.45

- Publishes the validated large-image performance update in the Marketplace Overview and release documentation.
- Uses preview-first debugger transfer, file-backed tiled sources, and progressive viewport reads so large-image navigation does not repeatedly process a complete frame.
- Preserves stable large-image zoom, mouse-wheel zoom, drag pan, Fit, 1:1, pixel/GV inspection, selection, export, collections, comparison, and actionable error rows.

Measured on the same test machine with the same dense `5000 x 5000 Mono8` input:

- Initial open path: `179.818 ms` to `115.369 ms` (`35.8%` lower).
- Zoom maximum frame: `49.397 ms` to `36.527 ms` (`26.1%` lower).
- Pan average tile upload: `20.410 ms` to `15.211 ms` (`25.5%` lower).

An installed-VSIX Visual Studio 2022 `17.14` check with a dense file-backed `24000 x 24000 Mono8` image recorded `6.515 ms` average and `13.642 ms` maximum rendered frames during real wheel and drag input. Results vary by PC and image source.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

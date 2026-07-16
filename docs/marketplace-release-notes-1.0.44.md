# Raw Buffer Visualizer 1.0.44

- Fixes blank, white, black, or single-color frames that could appear when zooming large images in the Visual Studio docked viewer.
- Detects the active OpenGL texture-size limit and keeps progressive viewport uploads within that limit.
- Uses a continuous sample step instead of fixed power-of-two sampling, reducing aliasing on periodic machine-vision patterns.
- Rejects failed texture uploads with a diagnostic error instead of presenting an invalid frame.
- Preserves mouse-wheel zoom, drag pan, Fit, 1:1, pixel/GV inspection, selection, export, collections, comparison, and preview-first large debugger image loading.

Validation:

- Dense `24000 x 24000` Mono8 periodic data at the reported 7.3% zoom path rendered 199 sampled colors with OpenGL error 0.
- A 1272-pixel-wide 1:1 viewport stayed within a 1024-pixel texture limit by selecting sample step 2.
- Dense `100000 x 100000` and `200000 x 200000` Mono8 file-backed payloads completed zoom and pan regression checks.
- Full Release solution build, self-tests, standalone interaction smoke, responsive docked layouts, and the 240-iteration docked memory soak passed.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

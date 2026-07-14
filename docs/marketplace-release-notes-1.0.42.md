# Raw Buffer Visualizer 1.0.42

- Keeps failed visualizations visible as red rows in the docked image list.
- Adds a stable error ID and a focused error panel with the failure reason.
- Adds `Copy Report` for sharing extension, Visual Studio, source, and exception context without image payloads.
- Adds `Open Logs` for locating the latest support report and package logs.
- Restores the normal image viewer immediately when a valid row is selected after an error.
- Treats malformed debugger handoffs as visible error rows instead of silently losing the failure.

Validation:

- Actual Visual Studio 2022 docked-view smoke with normal images and a visible error report panel.
- Error report copy, report-file creation, malformed-handoff recovery, and valid-image recovery at 540, 900, and 1160 pixel docked widths.
- Full Release solution build, self-test suite, standalone interaction smoke, legacy Bitmap/OpenCvSharp/Emgu compatibility smoke, and VSIX package validation.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

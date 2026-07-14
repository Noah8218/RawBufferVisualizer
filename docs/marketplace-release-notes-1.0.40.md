# Raw Buffer Visualizer 1.0.40

- Stabilizes the initial Fit view when an image opens in a resized or newly docked Visual Studio ToolWindow.
- Preserves the image aspect ratio while the docked viewer completes its first layout pass.
- Makes Pin freeze the selected pixel, 5x5 neighborhood values, statistics, marker, and status readouts.
- Makes Clear release the pin and resume live hover inspection.
- Adds repeatable narrow, medium, and wide docked-layout regression checks for Fit and Pin behavior.

Validation:

- Actual Visual Studio 2022 docked-view smoke for mouse-wheel zoom, drag pan, pixel inspection, and non-blank rendering.
- Docked layout regression at 540, 900, and 1160 pixel widths.
- Full Release solution build, test suite, and VSIX package validation.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

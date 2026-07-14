# Raw Buffer Visualizer 1.0.41

- Restores live mouse-following updates for the yellow 5x5 and cyan pixel markers in the docked viewer.
- Keeps hover coordinates, decoded values, raw bytes, statistics, and the image marker synchronized.
- Preserves pinned inspection until Clear is selected.
- Avoids image texture uploads during hover-only redraws.
- Adds repeatable continuous-hover regression coverage at narrow, medium, and wide docked widths.

Validation:

- Actual Visual Studio 2022 docked-view smoke with external mouse-wheel and drag input.
- Three-position hover, Pin, Clear, Fit, and non-blank framebuffer checks at 540, 900, and 1160 pixel widths.
- Full Release solution build, test suite, standalone interaction smoke, and VSIX package validation.

After updating, close all Visual Studio windows and reopen Visual Studio before debugging.

# Raw Buffer Visualizer 1.0.47

This feature release adds two new debugger workflows and hardens Smart Type Mapper as their explicit fallback.

## Automatic Vision Inspector

- Scans the selected stack frame's locals and arguments at Break Mode.
- Automatically opens safe pointer/array-backed image wrappers when member inference and current-buffer validation pass.
- Isolates candidate failures and reports `[Auto]`, `[Map]`, and `[Failed]` outcomes instead of dropping partial results.
- Uses Smart Type Mapper only when a company-specific type is ambiguous, then reuses the saved mapping.
- Adds a persisted `Auto Inspect on Break` preference and an independent `Scan Now` action.
- Keeps registered Bitmap, OpenCvSharp, Emgu CV, and snapshot types on their debugger-visualizer path to avoid duplicate or misleading mapping rows.

## Vision Buffer Doctor

- Ranks plausible width, height, stride, pixel format, valid-bit, and byte-order interpretations for a suspicious raw buffer.
- Uses bounded structural and sampled-content scoring.
- Applies the selected candidate immediately without another debugger round trip.
- Keeps inherently ambiguous RGB/BGR and Bayer interpretations visible for developer confirmation.

## Smart Type Mapper fallback

- Keeps ambiguous company-specific wrappers visible instead of guessing or dropping them.
- Lets the developer confirm inferred member roles and pixel format, preview live paused-process memory, and save the mapping for later breaks.
- Remains a fallback for unregistered types; it does not create a Visual Studio visualizer icon dynamically.

## Validation

- Installed-VSIX hybrid smoke passed on Visual Studio 2022 `17.14.37314.3`.
- Real OpenCvSharp `4.13.0.20260627`, Emgu CV `4.13.0.5924`, and `System.Drawing.Bitmap` values opened through registered debugger visualizers.
- Six pointer-backed camera-shape fixtures opened through Automatic Inspector in the same session, producing 9 images and 0 errors.
- Basler, Spinnaker, Vimba, and IDS fixture results validate accessible member contracts only; they do not certify a vendor SDK version or camera hardware.

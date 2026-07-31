# Changelog

This file records user-visible Raw Buffer Visualizer changes. The Tool Window shows a concise one-time summary for each installed version; the complete history remains available here.

## [Unreleased]

No user-visible changes are queued after `1.0.50`.

## [1.0.50] - 2026-07-31

### Added

- Added optional Automatic Vision Inspector expansion for exact OpenCvSharp and Emgu CV `Mat` lists and one-dimensional arrays.
- Added one-time, non-modal release highlights in the Tool Window. Dismissal is saved per user across Visual Studio restarts, and **What's New** reopens the current highlights.

### Improved

- Each automatic collection element now succeeds or fails independently, so null or disposed Mats do not hide valid images from the same collection.
- Automatic collection work is bounded to 8 items per collection, 16 items and 8 roots per scan.
- Debugger handoff now uses atomic claiming plus explicit ACK/NACK completion.
- New and selected images remain in aspect-correct Fit mode; manual zoom and pan are preserved.
- Visual Studio package and View-menu registration checks reject duplicate or retired registrations.

### Compatibility

- `System.Drawing.Bitmap`, mixed collections, dictionaries, jagged arrays, multidimensional arrays, and arbitrary enumerables continue to use their registered debugger-visualizer path.
- Automatic Inspector does not invoke arbitrary vendor methods or decode private native layouts.

## [1.0.49] - 2026-07-29

### Fixed

- Assigned a new VSPackage identity so Visual Studio profiles affected by the failed `1.0.47`/`1.0.48` package state do not reuse it after update.
- Kept the Marketplace extension identity unchanged so installation remains a normal update.
- Added a visible diagnostic when the View command reaches the package but the Tool Window cannot be created.
- Added packaging guards for the retired Package GUID.

## [1.0.48] - 2026-07-28

### Fixed

- Moved VSSDK package and command registration into the hybrid Marketplace project and removed developer-registry repair from the normal install path.
- Added build guards for the former split-project registration layout.

This release was superseded by `1.0.49` after an external upgraded Visual Studio profile still failed to load the reused VSPackage identity.

## [1.0.47] - 2026-07-28

### Added

- Added Automatic Vision Inspector for bounded Locals/Arguments discovery with isolated `[Auto]`, `[Map]`, and `[Failed]` outcomes.
- Added Vision Buffer Doctor for ranked stride, format, valid-bit, byte-order, width, and height interpretations.
- Added persisted **Auto Inspect on Break** and independent **Scan Now** workflows.

### Improved

- Smart Type Mapper became the explicit fallback for ambiguous compatible company-specific wrappers.

[Unreleased]: https://github.com/Noah8218/RawBufferVisualizer/compare/v1.0.50...HEAD
[1.0.50]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.50
[1.0.49]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.49
[1.0.48]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.48
[1.0.47]: https://github.com/Noah8218/RawBufferVisualizer/releases/tag/v1.0.47

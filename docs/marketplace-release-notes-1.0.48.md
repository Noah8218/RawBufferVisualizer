# Raw Buffer Visualizer 1.0.48

Version `1.0.48` is a clean-install reliability hotfix for the docked Visual Studio inspector.

## Fixed

- Fixed a clean-PC installation failure where the debugger visualizer host opened but the docked Raw Buffer Visualizer did not acknowledge the image handoff.
- Moved VSSDK package and command registration into the same hybrid project that builds the Marketplace VSIX.
- Changed the generated package `CodeBase` to the installed `RawBufferVisualizer.VisualStudio.Extensibility.dll`.
- Removed automatic developer-registry repair from the normal install path so packaging defects cannot be hidden during release testing.
- Added build-time guards that reject the former split-project `.pkgdef` layout.

## Included from 1.0.47

- Automatic Vision Inspector with safe breakpoint discovery and isolated `[Auto]`, `[Map]`, and `[Failed]` results.
- Smart Type Mapper recovery for ambiguous company-specific image wrappers.
- Vision Buffer Doctor for ranked stride, format, valid-bit, and byte-order interpretations.

Restart Visual Studio after installing or updating the extension.

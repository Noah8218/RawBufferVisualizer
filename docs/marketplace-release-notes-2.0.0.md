# Raw Buffer Visualizer 2.0.0

Version `2.0.0` establishes one vendor-neutral 2D buffer contract across registered and mapped debugger sources.

## Added

- Added **Diagnose interpretation** to Connect Your Buffer. Ranked candidates update only the visible draft and preview until **Save Mapping** is selected.
- Added a documented compatibility matrix for `RawBufferView`, `RawBufferSnapshot`, mapped pointers, and mapped `byte[]`, `ushort[]`, and `float[]` carriers.
- Added neutral fixtures that verify descriptor metadata, transferred bytes, byte order, valid bits, and pointer ownership without loading a proprietary SDK.

## Improved

- Preserved current width/height/stride format alternatives in the visible candidate set and kept ambiguous interpretations explicit.
- Registered and mapped metadata now share the same checked dimension, stride, buffer-length, enum, and valid-bits validation boundary.
- Valid `Mono16` values from 1 through 16 remain accepted; executable fixtures explicitly preserve 10, 12, 14, and 16.

## Fixed

- Invalid `Mono16` valid-bit counts now fail before transfer.
- `Mono10PackedLsb` and `Mono12PackedLsb` reject values that contradict their fixed 10-bit and 12-bit layouts.
- Undefined pixel-format/byte-order values and overflowing descriptor arithmetic now fail before allocation, transfer, or rendering.
- Continuing or exiting the debuggee now marks live process-backed rows `Unavailable`, cancels later source reads, and keeps only the last rendered pixels as context. Copied managed buffers remain available.
- Delayed debugger handoffs created before Continue can no longer open during Run Mode or revive at a later breakpoint.

## Scope

- Camera acquisition/control, lighting, PLC/I/O, 3D visualization, and proprietary SDK adapters remain outside this release.
- Visual Studio 2022 `17.14+` and stable Visual Studio 2026 `18.x` remain the supported x64 targets.

After installing or updating, close every Visual Studio window and restart Visual Studio.

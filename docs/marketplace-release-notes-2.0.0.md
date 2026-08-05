# Raw Buffer Visualizer 2.0.0

Version `2.0.0` establishes one vendor-neutral 2D buffer contract across registered and mapped debugger sources.

## Added

- Added a documented compatibility matrix for `RawBufferView`, `RawBufferSnapshot`, mapped pointers, and mapped `byte[]`, `ushort[]`, and `float[]` carriers.
- Added neutral fixtures that verify descriptor metadata, transferred bytes, byte order, valid bits, and pointer ownership without loading a proprietary SDK.

## Improved

- Registered and mapped metadata now share the same dimension, stride, buffer-length, and valid-bits validation boundary.
- Valid `Mono16` values from 1 through 16 remain accepted; executable fixtures explicitly preserve 10, 12, 14, and 16.

## Fixed

- Invalid `Mono16` valid-bit counts now fail before transfer.
- `Mono10PackedLsb` and `Mono12PackedLsb` reject values that contradict their fixed 10-bit and 12-bit layouts.

## Scope

- Camera acquisition/control, lighting, PLC/I/O, 3D visualization, and proprietary SDK adapters remain outside this release.
- Visual Studio 2022 `17.14+` and stable Visual Studio 2026 `18.x` remain the supported x64 targets.

After installing or updating, close every Visual Studio window and restart Visual Studio.

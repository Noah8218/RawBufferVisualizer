# Raw Buffer Visualizer 2.0.3

Version `2.0.3` adds direct `ConcurrentDictionary<TKey,TValue>` image-collection visualization and fixes the first registered `ImagePtr` handoff when the docked viewer has not been opened yet.

## Added

- Opens `ConcurrentDictionary<TKey,TValue>` directly from DataTip, Locals, Autos, and Watch on .NET Framework and current .NET.
- Preserves dictionary keys as image-row names, reports unsupported entries independently, and keeps each invocation bounded to 256 entries.

## Fixed

- Preloads the lightweight VSSDK package and passively arms its handoff listener when debugging begins, so the first registered `ImagePtr` can open without a manual Tool Window warm-up or cyclic package load.
- Checks the actual isolated VSSDK package DLL and pkgdef names during installed-update validation.

Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` support, Connect Doctor, checked 2D buffer validation, automatic inspection, registered Bitmap/OpenCvSharp/Emgu paths, and live-source invalidation remain included. After installing or updating, close every Visual Studio window and restart Visual Studio.

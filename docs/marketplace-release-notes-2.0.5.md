# Raw Buffer Visualizer 2.0.5

Version `2.0.5` distinguishes a native object pointer from its pixel buffer, preserves debugger expression names, and rejects incomplete native-memory reads.

## Added

- Shows the exact debugger object/expression name above each thumbnail, including collection-root names.
- Shows OpenCvSharp/Emgu `Ptr` separately from `Pixels`, combines equal ImagePtr/RawBufferView addresses, and records Bitmap `Scan0 / Pixels` as captured provenance.
- Adds separate **Copy pointer address** and **Copy pixel address** actions and includes both values in Descriptor details.
- Shows process ID and `LIVE`, `CAPTURED`, `PREVIEW`, or `UNAVAILABLE` state when provenance is available.

## Safer Pointer Behavior

- Opening the same address again reads its current bytes; an address is not treated as an object identity.
- Released, inaccessible, or partially readable pointer ranges stop with a controlled error instead of displaying incomplete image data.

## Fixed

- Corrects top-to-bottom transfer for `System.Drawing.Bitmap` images whose native stride is negative.
- Fixes direct Bitmap/OpenCvSharp/Emgu array registration by using complete assembly-qualified target identities.

**Clear all**, direct concurrent dictionaries, first-use `ImagePtr` handoff, Visual Studio 2022 `17.9+`, and stable Visual Studio 2026 `18.x` support remain included. After installing or updating, close every Visual Studio window and restart Visual Studio.

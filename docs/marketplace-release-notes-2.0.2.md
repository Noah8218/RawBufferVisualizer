# Raw Buffer Visualizer 2.0.2

Version `2.0.2` restores Visual Studio 2022 `17.9+` support while retaining stable Visual Studio 2026 `18.x` compatibility and the complete Raw Buffer Visualizer 2.0 feature set.

## Improved

- Supports Visual Studio 2022 `17.9` or newer on x64 Community, Professional, and Enterprise.
- Retains stable Visual Studio 2026 `18.x` support through the existing Marketplace extension identity.
- Uses Visual Studio-native toolbar and inspector icons, responsive command labels, an actionable empty-viewer message, and image-aware command states.
- Includes supported exact OpenCvSharp/Emgu Mat lists and one-dimensional arrays automatically, without a separate collection setting.

## Fixed

- Long writable temporary-storage paths no longer block registered debugger images from reaching the docked viewer.
- The in-process package and out-of-process debugger providers now use a compatibility boundary suitable for the Visual Studio 17.9 SDK line.

Connect Doctor, checked 2D buffer validation, automatic inspection, registered Bitmap/OpenCvSharp/Emgu paths, and live-source invalidation remain included. After installing or updating, close every Visual Studio window and restart Visual Studio.

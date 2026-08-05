# Raw Buffer Visualizer 1.0.53

Version `1.0.53` is a qualified update to the public `1.0.52` release. It adds a complete vendor-neutral buffer-mapping workflow, simplifies environment diagnostics, and improves the first preview of extremely large pointer-backed images.

## Added

- Added **Connect Your Buffer** to preview, save, restore, edit, and reset mappings for compatible company-specific camera and frame-grabber wrappers.
- Added an optional neutral `RawBufferView` starter for applications that prefer an explicit pointer-backed wrapper.
- Added **Environment Check** for the current Visual Studio host, loaded extension version, and temporary-storage state.
- Added **Refresh** and **Copy diagnostic report**. The report excludes credentials, environment-variable values, and image payloads and warns when local paths may be present.

## Improved

- Improved the first preview of extremely large pointer-backed images while preserving the existing full-image handoff behavior.
- Selecting **Environment** again now closes the panel.
- Selecting **What's New** again now closes the release highlights; **Dismiss** still records the version as seen.
- Environment actions do not scan the current frame, open an image, install software, or change extension registration.

## Fixed

- Visual Studio file versions that contain a `built by` suffix are now recognized correctly.
- Automatic Vision Inspector and Smart Type Mapper validation now follows the current workspace and installed tooling state.
- Removed the redundant in-panel **Close** action and unrelated utility rows from Environment Check.

## Compatibility

- Retains automatic OpenCvSharp/Emgu Mat discovery, registered Bitmap visualization, supported collections, Buffer Doctor, Smart Type Mapper, Fit/Manual navigation, comparison tools, and snapshot export.
- Supports Visual Studio 2022 `17.14` or newer and stable Visual Studio 2026 `18.x` on x64 Community, Professional, and Enterprise editions.
- Compatible exposed buffers can be connected without a manufacturer-specific extension adapter. Camera acquisition and device control remain outside this extension.

After installing or updating, close every Visual Studio window and restart Visual Studio.

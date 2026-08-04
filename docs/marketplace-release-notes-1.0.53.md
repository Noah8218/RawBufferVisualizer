# Raw Buffer Visualizer 1.0.53

Version `1.0.53` is a local development candidate based on the public `1.0.52` package. Marketplace publication and propagation are not complete.

## Added

- Added a registered 2D debugger visualizer for supported Basler pylon .NET `IGrabResult` values without packaging Basler assemblies in the VSIX. The development candidate passed pylon `26.07.2.18500` camera emulation and an installed-VSIX Mono12 debugger open; physical-camera qualification remains separate.
- Added **Environment Check** to show supported Visual Studio host state, the extension version loaded in the current session, and temporary-storage writability.
- Added **Refresh** and **Copy diagnostic report**. The report contains no credentials, environment-variable values, or image payloads and warns when local paths are included.

## Improved

- Large pointer-backed sampled previews now apply a bounded estimate of cold page reads. The final regression run's first benchmark-process access on the restored workstation completed in `0.992 s` for dense 100k and `0.692 s` for dense 200k inputs, below the unchanged five-second gate.
- Environment Check contains only product runtime requirements. Contributor and demo-media utilities remain in the development documentation.
- Selecting **Environment** again closes the panel.
- Selecting **What's New** again closes the release highlights; **Dismiss** still saves the version as seen.

## Fixed

- Automatic Vision Inspector layout validation now uses the current workspace diagnostic seam rather than a removed private field.
- Smart Type Mapper layout validation now discovers the newest compatible restored `Microsoft.VSSDK.BuildTools` package rather than historical `17.9.3168`.
- Visual Studio file versions that include a `built by` suffix are now recognized correctly instead of being shown as unsupported or unknown.
- The redundant in-panel **Close** action and unrelated utility rows were removed.

## Behavior And Compatibility

- Opening, refreshing, copying, or toggling Environment Check does not scan the current frame, open an image, change the active document, install software, or modify registration.
- Existing automatic Mat discovery, registered Bitmap visualizer, Buffer Doctor, Smart Type Mapper, Fit/Manual navigation, snapshot lease, and preview-to-full handoff contracts remain in place.
- Supports Visual Studio 2022 `17.14` or newer and stable Visual Studio 2026 `18.x` on x64 Community, Professional, and Enterprise editions.

After installing or updating, close every Visual Studio window and restart Visual Studio.

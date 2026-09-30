# Raw Buffer Visualizer 2.2.1

- Registers the Classic visualizer as an extension-owned component so .NET Framework ObjectSource loading uses the extension installation directory.
- Uses the installed Classic DLL's full assembly identity for Visual Studio installation-path lookup for single images and collections.
- Checks compiled registration and debugger payload hashes during packaging to reject missing or stale loader metadata.
- Retains 2.2.0's docked image opening, image-only collection routing and pixel measurement.

Update the extension and restart Visual Studio; no manual DLL copy to the IDE's global Visualizers directory is required.

Installation targets remain Visual Studio 2022 17.9+ x64 and stable Visual Studio 2026 18.x. Runtime behavior depends on the supported image type and source lifetime. Sparse object-valued collections use bounded inspection; if no image is recognized in the inspected portion, the usual collection visualizer remains available.

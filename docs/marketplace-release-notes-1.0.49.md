# Raw Buffer Visualizer 1.0.49

Version `1.0.49` is a Visual Studio package-load recovery update.

## Fixed

- Uses a new VSPackage identity so Visual Studio profiles affected by the failed `1.0.47`/`1.0.48` package do not reuse that package state after update.
- Keeps the existing Marketplace extension identity, so installation remains a normal update.
- Shows a diagnostic error when the View command reaches the package but the docked ToolWindow cannot be created, instead of failing silently.
- Adds packaging guards that reject the retired Package GUID.

Automatic Vision Inspector, Vision Buffer Doctor, Smart Type Mapper, registered Bitmap/OpenCvSharp/Emgu visualizers, and the docked image list remain included.

Close every Visual Studio window after updating, then reopen Visual Studio.

# Product Contract

Raw Buffer Visualizer lets a C# developer inspect already-acquired 2D images and buffers at a Visual Studio breakpoint, without adding temporary saves or display code to the application.

The primary workflow is: pause the application, open an image variable, inspect its descriptor and pixels in the docked viewer, compare retained images, and export a snapshot when a durable copy is needed. RawBufferView and RawBufferSnapshot provide vendor-neutral integration; Connect Your Buffer handles supported application-owned shapes.

The Images list and image viewer are primary. Diagnostics and mapping help support this workflow without hiding the current image or error state. Automatic discovery must remain cancellable and must not publish results from an obsolete debugger session. Continue, process exit or source release must never imply that live memory remains a valid captured image.

The separate WPF application opens snapshots and explicit RAW layouts. Camera acquisition/control, vendor certification, PLC/I/O, recipes, 3D and inspection automation are outside this product.

See [architecture and validation](docs/ARCHITECTURE_AND_VALIDATION.md) for owners and [README](README.md) for the current supported workflow.

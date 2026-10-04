# Raw Buffer Visualizer 2.2.2

- Keeps your chosen docked position and tab group when you open another image object.
- Preserves floating-window position and size and document-tab mode when switching images.
- Uses Visual Studio's saved layout when you reopen the viewer or restart the IDE.

Visual Studio saves editing and debugging layouts separately. Starting or ending debugging can switch to the position saved for that mode.

Includes the 2.2.1 debugger visualizer-loading fixes for single images and image collections. The dialog-free docked viewer, image-only collection routing and horizontal/vertical pixel measurement remain available.

Update the extension and restart Visual Studio. No manual DLL copy to Visual Studio's global Visualizers directory is required.

Installation targets remain Visual Studio 2022 17.9+ x64 and stable Visual Studio 2026 18.x. Runtime behavior depends on the supported image type and source lifetime. Sparse object-valued collections use bounded inspection; if no image is recognized in the inspected portion, the usual collection visualizer remains available.

# Image Watch UX Analysis

This note summarizes the Image Watch-style UX direction for Raw Buffer Visualizer. It is intentionally an analysis document, not an implementation plan for Vision Replay Debugger.

## Sources Checked

- Microsoft Learn, Image Watch: https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/debugger/image-watch/image-watch?view=vs-2015
- Microsoft Learn, Image Watch reference: https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/debugger/image-watch/image-watch-reference?view=vs-2015
- Microsoft C++ Team Blog, Image Watch for Visual Studio 2017: https://devblogs.microsoft.com/cppblog/image-watch-is-now-available-for-visual-studio-2017/
- OpenCV tutorial mirror, Image Watch workflow: https://vovkos.github.io/doxyrest-showcase/opencv/sphinx_rtd_theme/page_tutorial_windows_visual_studio_image_watch.html

## What Image Watch Actually Does

Image Watch is a Visual Studio debugger watch window for native C++ image variables. The official docs describe it as a combined Locals and Watch window: Locals auto-populates image variables from the current stack frame, and Watch lets users pin image expressions manually.

The main UI pattern is not just document tabs. The stronger pattern is:

- image list with thumbnails, expression/type/size/format, and valid/invalid state
- one selected image shown in the main viewer
- quick keyboard selection between images
- linked pan/zoom across images of the same size
- A/B switching to the previously viewed image
- image operators for derived views, including threshold, clamp, channel extraction, file reference, raw memory interpretation, and pixel-wise diff

The OpenCV tutorial highlights the practical comparison workflow: zoom into a region, enable Link Views, switch between images, and verify that corresponding structures align.

## UX Features Worth Copying

1. Image List / Watch List

   Keep multiple images in a session. Each item should show name, thumbnail, dimensions, pixel format, source kind, byte size, and validity. This is more useful than a plain tab bar once there are many intermediate images.

2. Tabs For Active Comparison Set

   Tabs are still useful, but should represent a small active comparison set, not every image ever seen. Recommended: left image list for all captures, top tab strip for pinned/open images.

3. Link Views

   Add a toggle that shares pan and zoom among images with the same width and height. This should be the first comparison feature because it is simple and directly matches vision-debugging workflows.

4. A/B Flip

   Add a command to switch between the current image and the previous image while preserving view state. This is faster than side-by-side for small local changes.

5. Difference View

   Add derived comparison views after Link Views:

   - absolute difference
   - signed difference with neutral midpoint
   - threshold mask
   - optional reference image from file

6. Per-Image Display Settings

   Keep global defaults, but allow per-image overrides for pseudo color, alpha handling, and Bayer/color interpretation. Large raw files should avoid full-file autoscale unless explicitly requested.

## Recommended Raw Buffer Visualizer Order

1. Add a session model: `ImageDocument` list with descriptor, source, thumbnail, status, and display state.
2. Add WPF UI for image list + active tab strip.
3. Add Link Views for same-size images.
4. Add A/B flip between current and previous image.
5. Add side-by-side compare and diff as derived image sources.
6. Integrate Visual Studio variables into the same session model later, so the standalone viewer and debugger extension share behavior.

## Design Constraint

Do not make the first version a heavy multi-document editor. The minimum useful version is a left list, a top tab strip, one viewer, and Link Views. Side-by-side and diff can come after the session model is stable.

## 1.0.47 Positioning Evaluation (2026-07-27)

### Current Sources And Evidence

- Microsoft Learn describes Image Watch as a native C++ debugger window that combines Locals-style automatic current-frame discovery with a manual Watch list, thumbnails, validity state, pixel values, zoom, and pan:
  https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/debugger/image-watch/image-watch?view=vs-2015
- Microsoft Research describes the core value as seeing the image objects that exist at a given point and inspecting pixels without adding temporary save code:
  https://www.microsoft.com/en-us/research/blog/image-debugging-for-visual-studio/
- The current ImageWatchCSharp Marketplace listing emphasizes automatic OpenCvSharp `Mat` discovery, metadata, disposed state, pixel inspection, zoom, and pan:
  https://marketplace.visualstudio.com/items?itemName=LingLuo.ImageWatchSharp
- Raw Buffer Visualizer 1.0.47 installed-VSIX evidence is recorded in:
  `artifacts/ui/installed-vsix-new-features/MultiLibraryHybrid-installed-vsix.json`
  and `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`.

### Position By Capability

| Area | Current position | Evidence-based assessment |
| --- | --- | --- |
| C# Image Watch workflow | Competitive | One docked list, thumbnails, selected viewer, pixel/raw values, zoom/pan, Fit, and persistent break-mode workflow are implemented. |
| Supported managed image families | Strong | Real Bitmap, OpenCvSharp, and Emgu CV paths plus typed/mixed collections cover more than a Mat-only visualizer. |
| Known `Mat` automatic discovery | Gap by design | Registered OpenCvSharp/Emgu values still use the debugger icon. Automatic Inspector targets unregistered safe pointer/array wrappers and does not duplicate registered rows. |
| Company/camera wrapper discovery | Differentiator | Current-frame Locals + Arguments scanning, structural inference, validation, `[Map]` recovery, and saved mappings address types that cannot receive a runtime visualizer registration. |
| Raw-buffer correctness diagnosis | Differentiator | Stride, valid bits, byte order, packed mono, raw bytes, and ranked Buffer Doctor interpretations go beyond a basic decoded Mat preview. |
| Partial failure handling | Strong | One candidate failure does not block other images; recognized mapping and open failures remain visible and successful rows remain usable. |
| Multiple-image comparison | Strong | Linked pan/zoom, A/B, split, absolute difference, and blink are already present. |
| Native C++ Image Watch replacement | Out of scope | The product targets managed C# machine vision and should not claim to replace Microsoft's native C++ Image Watch. |
| Vendor SDK certification | Not ready | Camera-shape fixtures prove inference contracts only. Basler, Spinnaker, Vimba, and other SDK/hardware claims require legal SDKs, real objects, and explicit buffer lifetime evidence. |
| Ecosystem and field maturity | Gap | Public usage history and real industrial-team reports are still limited compared with long-established Image Watch workflows. |

### Recommended Market Position

Use:

> Image Watch-style debugging for C# machine vision, with automatic discovery of accessible camera-frame wrappers and raw-buffer diagnosis.

Do not use:

> A universal Image Watch replacement for every camera SDK.

The defensible niche is not merely “another OpenCvSharp viewer.” It is the combination of broad managed-image support, one docked multi-image session, safe recovery for unregistered wrappers, and raw-buffer interpretation tools. The product is a differentiated Marketplace Preview and a credible specialist tool for C# machine-vision developers, but it is not yet a vendor-certified industrial camera platform or the category leader by adoption.

### Release Implication

The 1.0.47 feature claim is ready when the installed package preserves the hybrid boundary:

1. registered Bitmap/OpenCvSharp/Emgu types open through their debugger visualizers;
2. accessible unregistered wrappers open or fail visibly through Automatic Inspector;
3. Smart Type Mapper remains a recovery path rather than the headline workflow;
4. Buffer Doctor remains explicitly non-semantic and exposes ambiguity;
5. public text does not turn contract fixtures into real SDK certification.

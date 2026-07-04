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

   Keep global defaults, but allow per-image overrides for display range, pseudo color, alpha handling, and Bayer/color interpretation. Large raw files should avoid full-file autoscale unless explicitly requested.

## Recommended Raw Buffer Visualizer Order

1. Add a session model: `ImageDocument` list with descriptor, source, thumbnail, status, and display state.
2. Add WPF UI for image list + active tab strip.
3. Add Link Views for same-size images.
4. Add A/B flip between current and previous image.
5. Add side-by-side compare and diff as derived image sources.
6. Integrate Visual Studio variables into the same session model later, so the standalone viewer and debugger extension share behavior.

## Design Constraint

Do not make the first version a heavy multi-document editor. The minimum useful version is a left list, a top tab strip, one viewer, and Link Views. Side-by-side and diff can come after the session model is stable.

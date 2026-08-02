# Raw Buffer Visualizer 1.0.50

> Historical draft: these notes were not uploaded with the public `1.0.50.0` package. Automatic Mat collections and the in-product release-highlights banner are released as `1.0.51` so existing `1.0.50` users receive a higher-version update.

Version `1.0.50` improves Automatic Vision Inspector, debugger handoff reliability, Visual Studio menu registration, and viewer Fit behavior.

## Improved

- Automatic Inspector can open initialized exact OpenCvSharp and Emgu CV `Mat` values from the current frame after assignment.
- A persisted, default-off **Mat collections** option can expand exact Mat `List<T>` and one-dimensional arrays with bounded indexed rows.
- Collection elements fail independently, so a null or disposed Mat does not hide valid images from the same collection.
- One unreadable automatic candidate remains isolated as `[Failed]` while valid images continue opening.
- `System.Drawing.Bitmap` remains available through its registered debugger-visualizer icon.
- Atomic request claiming plus explicit ACK/NACK prevents a missing handoff file from being mistaken for success.
- Delayed cleanup removes terminal markers only and preserves in-flight Ready/Processing requests.
- The Visual Studio command-table cache is versioned and packaging verifies one copy of each Raw Buffer Visualizer View command.
- New/selected images keep an aspect-correct Fit; wheel zoom, drag pan, and 1:1 preserve manual navigation.
- The Tool Window shows these highlights once for `1.0.50`; **Dismiss** persists across restarts and **What's New** reopens them without starting inspection.

Vision Buffer Doctor and Smart Type Mapper remain included.

After installing or updating, close every Visual Studio window and restart Visual Studio.

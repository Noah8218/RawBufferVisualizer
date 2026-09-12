# Raw Buffer Visualizer 2.0.9

Version `2.0.9` fixes debugger transfer failures for medium native images, corrects inferred ROI memory spans, and stabilizes repeated use of the docked Tool Window.

## Fixed

- Pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads at or above 8 MiB now use checked live process-memory reads instead of repeated full-image debugger RPC snapshot requests.
- Inferred pointer-backed image length now ends at the final pixel row rather than including nonexistent trailing stride padding.
- The final-row rule also applies to Automatic Inspector's known OpenCvSharp, Emgu CV, and ImagePtr capture path.
- Explicit buffer lengths remain authoritative, and unreadable, released, partial, or overflowed memory ranges continue to fail closed.
- Repeated failures for the same expression and technical cause refresh one error row with a new report ID and current details.
- Consecutive registered visualizer opens retain the existing pinned docked Raw Buffer Visualizer Tool Window.

## Diagnostics

- Debugger RPC failures now report the operation, source or failed chunk, byte offset and sizes when applicable, exception type, and HRESULT.
- Localized Visual Studio remote-exception text is retained in the support report while the visible error uses a stable technical description.

## Compatibility

- Pointer-backed images below 8 MiB and non-pointer sources retain 4 MiB snapshot chunks.
- Live rows become `UNAVAILABLE` after Continue or process exit; copied snapshots remain viewable.
- Visual Studio 2022 `17.9+` x64 and stable Visual Studio 2026 `18.x` remain the installation targets.

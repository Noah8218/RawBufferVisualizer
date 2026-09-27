# Raw Buffer Visualizer 2.1.0

Version `2.1.0` includes the `2.0.9` native-image transfer fixes and the subsequent inspection and viewer improvements.

## Inspection and usability

- Corrects the running/paused/session-ended status when Auto Inspect is off, including manual Scan Now availability and retained captured images.
- Expires temporary panel/action notices on state changes while preserving actual image errors, so closing What's New cannot hide the next debugger state.
- Moves long frame/help text to the question-mark help beside Auto Inspect, reclaiming space for Images while keeping status and scan errors visible.
- Processes Stop and debugger state changes during automatic candidate discovery, bounds failed member enumeration, and rejects obsolete scan results. An active debugger call must return before cancellation can continue.

## Pixel accuracy and analysis

- Corrects Bitmap signed-stride, indexed-palette, premultiplied-alpha and unused-alpha handling.
- Makes Float32 scaling consistent across source kinds, preserves numeric readout precision, stabilizes statistics and removes temporary per-pixel byte-array allocations.
- Corrects sampled Bayer preview colors across supported patterns and sample origins.
- Adds raw-value histograms for supported scalar, Bayer, file and live sources; RGB mean/R/G/B selection; explicit sampled/full coverage and non-finite counts; background calculation and stale-result rejection.

## Mappings and saving

- Shows solution/user mapping scope and destination, follows the current solution, separates assembly-specific mappings and protects against invalid or conflicting saves.
- Adds bounded background snapshot export with progress and cancellation, preserving the previous snapshot on failure or cancellation.

## Included from 2.0.9

- Pointer-backed Mat, ImagePtr and RawBufferView images of 8 MiB or larger use checked live process-memory reads instead of repeated full-image debugger RPC transfer.
- Inferred ROI spans end at the final pixel row without nonexistent trailing padding. Explicit lengths and fail-closed memory validation remain authoritative.
- Matching repeated failures refresh one error row with current details and a new report ID.
- Consecutive registered visualizer opens preserve the pinned docked Tool Window.
- RPC diagnostics expose the operation, source/chunk, available byte range, exception type and HRESULT while retaining the original exception in the local report.

## Scope and qualification

The installation targets remain Visual Studio 2022 17.9+ x64 and stable Visual Studio 2026 18.x. Source version 2.1.0 has not been qualified in an installed IDE with these changes. Historical 2.0.9 installed evidence does not qualify these new bytes.

The repository also contains standalone RAW setup, document navigation, export feedback, readable diagnostics and file-read recovery improvements. Those belong to the separate standalone application; this VSIX does not install that application.

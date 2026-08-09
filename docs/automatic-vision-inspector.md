# Automatic Vision Inspector

## Purpose

Automatic Vision Inspector scans the selected stack frame for safe unregistered image-buffer shapes and for initialized exact OpenCvSharp and Emgu CV Mat values. Every scan also expands exact `List<OpenCvSharp.Mat>`, `List<Emgu.CV.Mat>`, `OpenCvSharp.Mat[]`, and `Emgu.CV.Mat[]` roots. It remains the primary recovery path for company-specific debug values that cannot receive a normal debugger-visualizer icon because their runtime type was not registered when the VSIX was built.

It does not dynamically register visualizers. Instead, the docked Raw Buffer Visualizer scans the current stack frame whenever Visual Studio enters Break Mode, recognizes safe image-buffer shapes, validates the current values, and adds successful images to the existing `Images` list. Exact OpenCvSharp `Mat` and Emgu CV `Mat` values use fixed supported metadata contracts and paused-process live memory; Smart Type Mapper remains the correction and persistence step for ambiguous or incomplete company wrappers.

The user workflow is deliberately non-modal:

- opening the Raw Buffer Visualizer Tool Window expresses interest in automatic inspection;
- **Auto Inspect on Break** defaults to enabled and is saved across Visual Studio restarts;
- supported exact Mat collections are included automatically in Break Mode refresh and **Scan Now**, with no separate setting;
- a breakpoint never forces the Tool Window to open or steal focus;
- **Scan Now** remains available while automatic scanning is paused;
- one failed candidate never prevents other recognized images from opening.

## Recognition Pipeline

The pipeline is intentionally ordered:

1. saved type mapping;
2. built-in/known image shape;
3. runtime type-name hints;
4. field/property member inference;
5. current-value and descriptor validation;
6. Smart Type Mapper when the result cannot be opened safely.

The scanner merges the current `StackFrame.Locals` and `StackFrame.Arguments`, deduplicated by root expression. It examines fields and debugger-visible property getters on the root object and one nested member level. It does not walk an unlimited object graph or other stack frames.

Supported inferred data members:

- `IntPtr` and `UIntPtr`;
- `byte[]`;
- `ushort[]` / `UInt16[]`;
- `float[]` / `Single[]`.

Required roles are data, width, and height. Stride, buffer length, valid bits, and pixel format increase confidence when present. Managed array element type can safely imply `Mono16` or `Float32`; other formats require a recognizable current enum/string value or an explicit mapping.

Automatic opening is deliberately stricter for SDK-style pointer buffers:

- `PixelDataPointer`, `ImageData`, and `DataPtr` are preferred over generic `Buffer`/`Data` names;
- without a stride, a reported buffer length must exactly match the contiguous image byte count;
- nonzero/unreadable row padding blocks automatic opening when stride is absent;
- an `ImageData` address that differs from a base `Buffer` address is treated as a chunk/image offset and requires an adapter;
- mapped pointer paths enforce the same rules and do not fall back to the legacy ImagePtr heuristic after a mapping failure.

Safe GenICam PFNC aliases include `Mono10p`, `Mono12p`, Bayer RG/GR/GB/BG 8-bit, RGB8 packed, and BGR8 packed. Legacy `Mono10Packed`/`Mono12Packed`, YUV, planar, compressed, signed, and 3D coordinate formats remain explicit because their layouts cannot be inferred safely from a similar name.

## Confidence And UX

| Confidence | Behavior |
| --- | --- |
| Saved mapping | Evaluate first, validate current data, and open when valid. |
| 90-100 | Open automatically after descriptor/memory validation. |
| 70-89 | Keep as a review/mapping candidate; the user can preview and correct it through **Edit Mapping**. |
| 40-69 | Show only as a candidate that needs mapping. |
| Below 40 | Hide by default. |

The docked window provides:

- **Auto Inspect on Break**: refresh on the next Break Mode event; the choice is stored in `%APPDATA%\RawBufferVisualizer\automatic-inspector-settings.json`;
- exact OpenCvSharp/Emgu `Mat` lists and one-dimensional arrays: included automatically, with at most 8 items per collection, 16 items and 8 collection roots per scan;
- **Scan Now**: rescan the current frame without waiting for another breakpoint;
- confidence, inferred-member summary, and validation reason on the selected row;
- **Edit Mapping** for ambiguous or incorrect inference.

Initialized exact OpenCvSharp `Mat` and Emgu CV `Mat` values are exceptions to the registered-type exclusion. They can open automatically through validated live process memory and still retain their registered debugger-visualizer icons. The same fixed path always applies to supported indexed expressions such as `frames[0]`; no collection object is copied into the extension process. OpenCvSharp uses `Data`, `Cols`, `Rows`, `Step()`, `Depth()`, and `Channels()`; Emgu uses `DataPointer`, `Cols`, `Rows`, `Step`, `Depth`, and `NumberOfChannels`. Only the fixed supported metadata query methods are evaluated; scanned objects cannot supply an arbitrary method name.

Registered `RawBufferSnapshot`, `RawBufferView`, `System.Drawing.Bitmap`, and the exact ImagePtr compatibility target stay on their registered debugger-visualizer paths. Bitmap automatic extraction would require a `LockBits`/`UnlockBits` lifecycle inside the debuggee, which the scanner deliberately does not inject or invoke. Exact normalized runtime-type matching prevents similarly named company wrappers from being suppressed accidentally.

### Breakpoint timing

Visual Studio stops before executing the highlighted breakpoint statement. A breakpoint on:

```csharp
Bitmap bitmap = new Bitmap(width, height);
```

occurs before `bitmap` is initialized. Place the breakpoint on the next executable line. **Scan Now** cannot recover an object that does not yet exist in the selected stack frame, but it can refresh values after execution has reached a later paused point.

The top `Inspector` button is a narrow-layout affordance rather than a permanently visible command. It is visible below 760 px, hidden from 760-1039 px while the compact bottom Inspector is present, and hidden at 1040 px or wider while the full right Inspector is present. The 540/900/1160 px states were rechecked on 2026-07-28. Changing this responsive contract requires the repository's UI mockup-and-approval gate.

Automatic rows use a stable key derived from the root expression. Every scan removes and replaces the prior automatic rows, so repeated Break/Scan Now events do not accumulate duplicates. Manually opened or visualizer-handoff rows are not removed.

### Partial success and failure policy

| Row state | Meaning | User action |
| --- | --- | --- |
| `[Auto]` | Shape inference and current buffer validation passed. | Inspect the image normally; **Edit Mapping** remains available for a semantically wrong inference. |
| `[Map]` | Image-like shape was found, but format/layout metadata is incomplete or ambiguous. | Open **Map** and confirm only the missing roles or format. |
| `[Failed]` | Recognition passed, but the current pointer, array, lifetime, or debugger read failed. | Inspect the reason, restore a valid paused object, then use **Scan Now**. This row does not incorrectly claim that a mapping will fix a lifetime failure. |
| Hidden | Confidence is below 40%. | Use **Open Variable** when the expression is intentionally image-like. |

For example, if eight candidates are detected and six open, one needs mapping, and one has an invalid pointer, the status reads `8 detected: 6 opened, 1 need mapping, 1 failed.` A five-element collection with three valid Mats, one null item, and one disposed Mat reports `frames: 5 inspected, 3 opened, 2 failed`. All successful rows stay usable. The viewer selects a successful automatic row in preference to an error row, while failure rows remain in the list for diagnosis.

Malformed or unsupported preference JSON is non-fatal: the control falls back to automatic scanning enabled and shows a warning. Preference writes use a same-directory temporary file followed by replacement so a terminated write does not normally leave a partial settings file.

## Debugger And Memory Boundaries

- Break-event work is deferred to the WPF dispatcher at `ContextIdle`, allowing the debugger transition event to return before expressions and paused-process memory are read.
- Locals and arguments are enumerated independently. If one debugger collection is temporarily unavailable, the other can still produce results.
- Every candidate is isolated by an exception boundary. An unexpected getter/debugger failure becomes one `[Failed]` row instead of aborting the scan.
- Pointer-backed data reuses the existing paused-process memory path and therefore remains valid only while the debuggee is paused and owns the buffer.
- Managed arrays are read through the VSSDK `IDebugProperty2` child enumerator in batches. If that path is unavailable, the EnvDTE fallback is capped at 256 elements to avoid unbounded debugger calls.
- Exact OpenCvSharp/Emgu `Mat` `List<T>` and one-dimensional array roots are expanded by every scan. Element failures are isolated, and collection/scan/root caps are reported instead of silently traversing beyond the limit.
- Bitmap collections, dictionaries, mixed `List<object>`, multidimensional or jagged arrays, and arbitrary `IEnumerable` values remain on the registered collection-visualizer path.
- A simple mapped property expression may be evaluated by the debugger to obtain an array object. The scanner never constructs or invokes arbitrary SDK methods.
- Exact OpenCvSharp/Emgu automatic capture evaluates only the extension-owned fixed metadata expressions listed above. Unsupported depth/channel combinations fail as one isolated automatic row.
- Null or debugger-unavailable exact Mat roots are skipped rather than creating a phantom image.
- No vendor DLL is loaded dynamically, no private native layout is decoded, and no camera/acquisition API is called.

## Implementation Map

| File | Responsibility |
| --- | --- |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/VisionMemberInference.cs` | Pure member-role inference, pixel-format recognition, confidence scoring, and gates. |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/MappedTypeVisualizerTransfer.cs` | Reads saved/transient mappings, including one-level nested member paths. |
| `src/RawBufferVisualizer.VisualStudio/AutomaticInspectionPreferences.cs` | Versioned per-user Auto Inspect preference with non-fatal load and atomic save behavior. |
| `src/RawBufferVisualizer.VisualStudio/AutomaticImageCollectionPolicy.cs` | Pure exact-type recognition, expression construction, and 8-per-collection/16-per-scan/8-root limits. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/KnownImageType.cs` | Exact runtime-type ownership: automatic OpenCvSharp/Emgu capture versus registered-only Bitmap/raw types. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/KnownRegisteredImageCapture.cs` | Reads fixed OpenCvSharp/Emgu metadata, validates the descriptor, and prepares paused-process live memory. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/AutomaticVisionInspector.cs` | Merges current-frame locals and arguments and builds bounded member inventories. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/VisualStudioDebugFrameContext.cs` | Resolves the selected VSSDK frame and reads managed-array elements. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml(.cs)` | Auto Inspect/Scan Now UX, confidence rows, mapping fallback, validation, and deduplication. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferVisualizerPackage.cs` | Schedules automatic scanning on Break Mode. |
| `samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs` | Pointer, array-backed, one-level nested, and intentionally ambiguous mapping-fallback installed-VSIX scenarios. |
| `scripts/SmokeInstalledVsixNewFeatures.ps1` | Installed-VSIX automation and session-state assertions. |
| `scripts/SmokeAutomaticVisionInspectorLayout.ps1` | Narrow/medium/wide layout assertions. |
| `tests/RawBufferVisualizer.Tests/Program.cs` | Core inference and nested-mapping tests. |
| `tests/RawBufferVisualizer.Tests/IndustrialCameraContractTests.cs` | Vendor-neutral PFNC, method-only, stride, padding, payload, size, and offset safety contracts. |
| `tests/RawBufferVisualizer.Tests/AutomaticImageCollectionPolicyTests.cs` | Exact collection type acceptance/rejection, expression, and scheduling-cap regressions. |
| `docs/industrial-camera-compatibility-validation.md` | Official-source matrix, evidence levels, remaining release gates, and durable result. |

## Verification Record

Status: Complete

Scope: Current-frame locals and arguments, direct and one-level nested pointer/managed-array shapes, exact OpenCvSharp/Emgu live capture, always-on bounded exact-Mat list/array expansion, confidence gating, partial-success isolation, the persisted Auto Inspect trigger preference, validation, mapping fallback, and duplicate-free refresh.

Acceptance criteria:

- direct pointer inference and 90% automatic-open gate -> passed by self-test and installed VSIX;
- a function image argument is merged with locals and opens in installed VSIX -> passed;
- one-level nested member inference -> passed by self-test and installed VSIX;
- ambiguous pixel format remains a mapping candidate -> passed by self-test and installed VSIX;
- mapping candidate -> pixel-format confirmation -> live preview -> save -> automatic reopen -> passed in installed VSIX as a 640 x 484 `Mono12PackedLsb` live source with zero final errors;
- low-confidence object remains hidden -> passed by self-test;
- one-level nested mapping extraction -> passed by self-test;
- 64 x 48 `byte[]` image opens from a real paused VS2022 debug session -> passed;
- repeated **Scan Now** does not create duplicate automatic rows -> passed;
- six successful images remain available while one mapping candidate and one open failure are shown independently -> passed in installed VSIX;
- a `SizeX` dimension is not misclassified as total buffer `Size` -> passed by deterministic inference regression and installed VSIX;
- disabling **Auto Inspect on Break**, closing Visual Studio, and starting a second Visual Studio session restores the disabled state; **Scan Now** still works -> passed;
- re-enabling the option persists, and the pre-test user settings file is restored -> passed;
- responsive Auto Inspect UI, toolbar bounds, empty guidance, and splitter limits at 320/350/400/420/480/540/619/620/759/760/900/1039/1040/1160 px -> passed.
- initialized OpenCvSharp and Emgu CV Mats open automatically on Break Mode and refresh without duplication on **Scan Now** -> passed exact `1.0.50` installed-VSIX `MultiLibraryHybrid`;
- Bitmap, `RawBufferSnapshot`, and `RawBufferView` remain absent from automatic rows while Bitmap opens through its registered glyph -> passed exact `1.0.50` installed-VSIX `MultiLibraryHybrid`;
- an assignment-line breakpoint is documented as pre-initialization and the installed scenario stops after image construction -> passed exact `1.0.50` installed-VSIX evidence;
- top Inspector affordance matches narrow/medium/wide layout ownership at 540/900/1160 px -> passed.
- exact supported collection type policy, 8/16 limits, stable element expressions, and broad collection rejection -> passed by current-source self-test;
- `Bitmap[]` and Emgu `Mat[]` item transfer in the registered collection path -> passed by current-source self-test;
- the retired collection checkbox is absent and every production manual/Break Mode scan enables supported exact-Mat collection discovery -> passed by source search, layout smoke, and installed VSIX;
- a legacy settings file containing `includeImageCollections: false` preserves its Auto Inspect choice and omits the retired field on the next save -> passed by current-source self-test;
- installed 2.0.2 `List<OpenCvSharp.Mat>` partial result (3 open/2 failed), `Emgu.CV.Mat[]` (2 open), and duplicate-free rescan -> passed on VS17.14 and VS18.8; each run reported seven rows, five opens, two isolated failures, always-on discovery, and zero protocol errors.
- after an automatic collection scan, **Clear** removes all rows and diagnosis/pixel/marker/comparison state, shows the empty viewer, blanks/disables both Interpret layouts while preserving Inspector visibility, and a later **Scan Now** restores seven rows and repopulates Interpret controls -> passed on the exact frozen 2.0.2 candidate in VS17.14 and VS18.8.

Verification:

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeAutomaticVisionInspectorLayout.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -Configuration Release
```

Evidence:

- `artifacts/ui/automatic-vision-inspector/2026-07-27/before/`
- `artifacts/ui/automatic-vision-inspector/2026-07-27/after/`
- `artifacts/ui/automatic-vision-inspector/2026-07-27/after-inspector/`
- `artifacts/ui/installed-vsix-new-features/automatic-vision-inspector.png`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-session.json`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- `artifacts/ui/automatic-inspector-workflow/2026-07-27/before.png`
- `artifacts/ui/automatic-inspector-workflow/2026-07-27/after.png`
- `artifacts/ui/automatic-inspector-workflow/2026-07-27/preference-disabled-write-result.json`
- `artifacts/ui/automatic-inspector-workflow/2026-07-27/after-result.json`
- `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-before-map.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-dialog-preview.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-after-reopen.png`
- `artifacts/ui/release-qualification-1.0.50/MultiLibraryHybrid-installed-vsix.json` (fresh exact `1.0.50`: 8/8 automatic opens, nine documents, zero errors, zero protocol errors)
- `artifacts/ui/release-qualification-1.0.50/AutomaticVisionInspector-installed-vsix.json` (fresh exact `1.0.50`: six opened, one mapping candidate, one isolated failure, duplicate-free, zero protocol errors)
- `artifacts/ui/inspector-button-visibility/2026-07-28/before/`
- `artifacts/ui/automatic-collection-inspector-20260731/before/automatic-inspector-1160.png`
- `artifacts/ui/automatic-collection-inspector-20260731/after-final-v3/automatic-inspector-1160.png`
- `artifacts/ui/automatic-collection-inspector-20260731/narrow-after-v3/automatic-inspector-540.png`
- `artifacts/ui/installed-vsix-new-features/automatic-collections.png`
- `artifacts/ui/installed-vsix-new-features/AutomaticCollections-view-menu-after.png`
- `artifacts/ui/installed-vsix-new-features/AutomaticCollections-installed-vsix.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\runtime\AutomaticCollections-installed-vsix.json` (exact `1.0.51` candidate)
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\responsive-matrix-final3\layout-widths.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\installed-vs17.14-final\AutomaticCollections\AutomaticCollections-installed-vsix.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\ui-modernization-2.0.2\20260809\installed-vs18.8-final\AutomaticCollections\AutomaticCollections-installed-vsix.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\docked-layout-release-final\layout-widths.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2022\AutomaticCollections-installed-vsix.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\AutomaticCollections-installed-vsix.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\automatic-collections-before-clear.png`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\exact-candidate-installed-vs2026\automatic-collections-after-clear.png`

Boundary / next dependency: This feature record is complete for the current 2.0.2 source and the installed VS17.14/VS18.8 scenarios. The current exact UI candidate was not run on the removed VS17.9 host; its current evidence is VSSDK 17.9 compile/package compatibility plus the earlier unchanged-architecture runtime qualification. This does not prove Bitmap automatic collection capture, live vendor runtime objects, buffer lifetime, drivers, emulators, or hardware.

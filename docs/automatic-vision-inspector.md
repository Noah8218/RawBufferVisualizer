# Automatic Vision Inspector

## Purpose

Automatic Vision Inspector is the primary recovery path for image-like debug values that cannot receive a normal debugger-visualizer icon because their runtime type was not registered when the VSIX was built.

It does not dynamically register visualizers. Instead, the docked Raw Buffer Visualizer scans the current stack frame whenever Visual Studio enters Break Mode, recognizes safe image-buffer shapes, validates the current values, and adds successful images to the existing `Images` list. Smart Type Mapper remains the correction and persistence step for ambiguous or incomplete shapes.

The user workflow is deliberately non-modal:

- opening the Raw Buffer Visualizer Tool Window expresses interest in automatic inspection;
- **Auto Inspect on Break** defaults to enabled and is saved across Visual Studio restarts;
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
- **Scan Now**: rescan the current frame without waiting for another breakpoint;
- confidence, inferred-member summary, and validation reason on the selected row;
- **Edit Mapping** for ambiguous or incorrect inference.

Registered `RawBufferSnapshot`, `RawBufferView`, OpenCvSharp `Mat`, Emgu CV `Mat`, `System.Drawing.Bitmap`, and the exact ImagePtr compatibility target stay on their registered debugger-visualizer path. Automatic Inspector uses exact normalized runtime-type matching for this exclusion; similarly named company wrappers are not suppressed accidentally.

The top `Inspector` button is a narrow-layout affordance rather than a permanently visible command. It is visible below 760 px, hidden from 760-1039 px while the compact bottom Inspector is present, and hidden at 1040 px or wider while the full right Inspector is present. The 540/900/1160 px states were rechecked on 2026-07-28. Changing this responsive contract requires the repository's UI mockup-and-approval gate.

Automatic rows use a stable key derived from the root expression. Every scan removes and replaces the prior automatic rows, so repeated Break/Scan Now events do not accumulate duplicates. Manually opened or visualizer-handoff rows are not removed.

### Partial success and failure policy

| Row state | Meaning | User action |
| --- | --- | --- |
| `[Auto]` | Shape inference and current buffer validation passed. | Inspect the image normally; **Edit Mapping** remains available for a semantically wrong inference. |
| `[Map]` | Image-like shape was found, but format/layout metadata is incomplete or ambiguous. | Open **Map** and confirm only the missing roles or format. |
| `[Failed]` | Recognition passed, but the current pointer, array, lifetime, or debugger read failed. | Inspect the reason, restore a valid paused object, then use **Scan Now**. This row does not incorrectly claim that a mapping will fix a lifetime failure. |
| Hidden | Confidence is below 40%. | Use **Open Variable** when the expression is intentionally image-like. |

For example, if eight candidates are detected and six open, one needs mapping, and one has an invalid pointer, the status reads `8 detected: 6 opened, 1 need mapping, 1 failed.` All six successful rows stay usable. The viewer selects a successful automatic row in preference to an error row, while both failure rows remain in the list for diagnosis.

Malformed or unsupported preference JSON is non-fatal: the control falls back to automatic scanning enabled and shows a warning. Preference writes use a same-directory temporary file followed by replacement so a terminated write does not normally leave a partial settings file.

## Debugger And Memory Boundaries

- Break-event work is deferred to the WPF dispatcher at `ContextIdle`, allowing the debugger transition event to return before expressions and paused-process memory are read.
- Locals and arguments are enumerated independently. If one debugger collection is temporarily unavailable, the other can still produce results.
- Every candidate is isolated by an exception boundary. An unexpected getter/debugger failure becomes one `[Failed]` row instead of aborting the scan.
- Pointer-backed data reuses the existing paused-process memory path and therefore remains valid only while the debuggee is paused and owns the buffer.
- Managed arrays are read through the VSSDK `IDebugProperty2` child enumerator in batches. If that path is unavailable, the EnvDTE fallback is capped at 256 elements to avoid unbounded debugger calls.
- Root arrays and collection objects are skipped by Automatic Vision Inspector; their elements remain owned by the existing registered collection visualizers.
- A simple mapped property expression may be evaluated by the debugger to obtain an array object. The scanner never constructs or invokes arbitrary SDK methods.
- No vendor DLL is loaded dynamically, no private native layout is decoded, and no camera/acquisition API is called.

## Implementation Map

| File | Responsibility |
| --- | --- |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/VisionMemberInference.cs` | Pure member-role inference, pixel-format recognition, confidence scoring, and gates. |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/MappedTypeVisualizerTransfer.cs` | Reads saved/transient mappings, including one-level nested member paths. |
| `src/RawBufferVisualizer.VisualStudio/AutomaticInspectionPreferences.cs` | Versioned per-user Auto Inspect preference with non-fatal load and atomic save behavior. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/AutomaticVisionInspector.cs` | Merges current-frame locals and arguments and builds bounded member inventories. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/VisualStudioDebugFrameContext.cs` | Resolves the selected VSSDK frame and reads managed-array elements. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml(.cs)` | Auto Inspect/Scan Now UX, confidence rows, mapping fallback, validation, and deduplication. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferVisualizerPackage.cs` | Schedules automatic scanning on Break Mode. |
| `samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs` | Pointer, array-backed, one-level nested, and intentionally ambiguous mapping-fallback installed-VSIX scenarios. |
| `scripts/SmokeInstalledVsixNewFeatures.ps1` | Installed-VSIX automation and session-state assertions. |
| `scripts/SmokeAutomaticVisionInspectorLayout.ps1` | Narrow/medium/wide layout assertions. |
| `tests/RawBufferVisualizer.Tests/Program.cs` | Core inference and nested-mapping tests. |
| `tests/RawBufferVisualizer.Tests/IndustrialCameraContractTests.cs` | Basler, Spinnaker, Vimba X, IDS peak, PFNC, method-only, padding, payload, and offset safety contracts. |
| `scripts/Test-IndustrialCameraSdkContracts.ps1` | Optional reflection audit for installed/provided vendor SDK assemblies. |
| `docs/industrial-camera-compatibility-validation.md` | Official-source matrix, evidence levels, remaining release gates, and durable result. |

## Verification Record

Status: Complete

Scope: Current-frame locals and arguments, direct and one-level nested pointer/managed-array shapes, confidence gating, partial-success isolation, persistent Auto Inspect preference, validation, mapping fallback, and duplicate-free refresh.

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
- responsive Auto Inspect UI at 540/900/1160 px -> passed.
- registered OpenCvSharp, Emgu CV, Bitmap, `RawBufferSnapshot`, and `RawBufferView` values do not create duplicate automatic rows -> passed in the hybrid installed-VSIX scenario;
- top Inspector affordance matches narrow/medium/wide layout ownership at 540/900/1160 px -> passed.

Verification:

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeAutomaticVisionInspectorLayout.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
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
- `artifacts/ui/installed-vsix-new-features/MultiLibraryHybrid-installed-vsix.json`
- `artifacts/ui/inspector-button-visibility/2026-07-28/before/`

Boundary / next dependency: The generic Automatic Vision Inspector scenario still passes in VS2022 17.14 after industrial-layout hardening, and IDS peak ICV 1.4.0 assembly metadata was verified. This does not prove live vendor runtime objects, buffer lifetime, drivers, emulators, or hardware. See `docs/industrial-camera-compatibility-validation.md`.

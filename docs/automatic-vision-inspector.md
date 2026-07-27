# Automatic Vision Inspector

## Purpose

Automatic Vision Inspector is the primary recovery path for image-like debug values that cannot receive a normal debugger-visualizer icon because their runtime type was not registered when the VSIX was built.

It does not dynamically register visualizers. Instead, the docked Raw Buffer Visualizer scans the current stack frame whenever Visual Studio enters Break Mode, recognizes safe image-buffer shapes, validates the current values, and adds successful images to the existing `Images` list. Smart Type Mapper remains the correction and persistence step for ambiguous or incomplete shapes.

## Recognition Pipeline

The pipeline is intentionally ordered:

1. saved type mapping;
2. built-in/known image shape;
3. runtime type-name hints;
4. field/property member inference;
5. current-value and descriptor validation;
6. Smart Type Mapper when the result cannot be opened safely.

The scanner reads the current `StackFrame.Locals`. It examines fields and debugger-visible property getters on the root object and one nested member level. It does not walk an unlimited object graph.

Supported inferred data members:

- `IntPtr` and `UIntPtr`;
- `byte[]`;
- `ushort[]` / `UInt16[]`;
- `float[]` / `Single[]`.

Required roles are data, width, and height. Stride, buffer length, valid bits, and pixel format increase confidence when present. Managed array element type can safely imply `Mono16` or `Float32`; other formats require a recognizable current enum/string value or an explicit mapping.

## Confidence And UX

| Confidence | Behavior |
| --- | --- |
| Saved mapping | Evaluate first, validate current data, and open when valid. |
| 90-100 | Open automatically after descriptor/memory validation. |
| 70-89 | Keep as a review/mapping candidate; the user can preview and correct it through **Edit Mapping**. |
| 40-69 | Show only as a candidate that needs mapping. |
| Below 40 | Hide by default. |

The docked window provides:

- **Auto Inspect**: refresh on the next Break Mode event;
- **Scan Now**: rescan the current frame without waiting for another breakpoint;
- confidence, inferred-member summary, and validation reason on the selected row;
- **Edit Mapping** for ambiguous or incorrect inference.

Automatic rows use a stable key derived from the root expression. Every scan removes and replaces the prior automatic rows, so repeated Break/Scan Now events do not accumulate duplicates. Manually opened or visualizer-handoff rows are not removed.

## Debugger And Memory Boundaries

- Break-event work is deferred to the WPF dispatcher at `ContextIdle`, allowing the debugger transition event to return before expressions and paused-process memory are read.
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
| `src/RawBufferVisualizer.VisualStudio.Vssdk/AutomaticVisionInspector.cs` | Enumerates current-frame locals and builds bounded member inventories. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/VisualStudioDebugFrameContext.cs` | Resolves the selected VSSDK frame and reads managed-array elements. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml(.cs)` | Auto Inspect/Scan Now UX, confidence rows, mapping fallback, validation, and deduplication. |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferVisualizerPackage.cs` | Schedules automatic scanning on Break Mode. |
| `samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs` | Pointer, array-backed, one-level nested, and intentionally ambiguous mapping-fallback installed-VSIX scenarios. |
| `scripts/SmokeInstalledVsixNewFeatures.ps1` | Installed-VSIX automation and session-state assertions. |
| `scripts/SmokeAutomaticVisionInspectorLayout.ps1` | Narrow/medium/wide layout assertions. |
| `tests/RawBufferVisualizer.Tests/Program.cs` | Five deterministic inference and nested-mapping tests. |

## Verification Record

Status: Complete

Scope: Initial Automatic Vision Inspector MVP for current-frame direct and one-level nested pointer/managed-array shapes, confidence gating, validation, mapping fallback, and duplicate-free refresh.

Acceptance criteria:

- direct pointer inference and 90% automatic-open gate -> passed by self-test and installed VSIX;
- one-level nested member inference -> passed by self-test and installed VSIX;
- ambiguous pixel format remains a mapping candidate -> passed by self-test and installed VSIX;
- mapping candidate -> pixel-format confirmation -> live preview -> save -> automatic reopen -> passed in installed VSIX as a 640 x 484 `Mono12PackedLsb` live source with zero final errors;
- low-confidence object remains hidden -> passed by self-test;
- one-level nested mapping extraction -> passed by self-test;
- 64 x 48 `byte[]` image opens from a real paused VS2022 debug session -> passed;
- repeated **Scan Now** does not create duplicate automatic rows -> passed;
- responsive Auto Inspect UI at 540/900/1160 px -> passed.

Verification:

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeAutomaticVisionInspectorLayout.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
```

Evidence:

- `artifacts/ui/automatic-vision-inspector/2026-07-27/before/`
- `artifacts/ui/automatic-vision-inspector/2026-07-27/after/`
- `artifacts/ui/automatic-vision-inspector/2026-07-27/after-inspector/`
- `artifacts/ui/installed-vsix-new-features/automatic-vision-inspector.png`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-session.json`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-before-map.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-dialog-preview.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-after-reopen.png`

Boundary / next dependency: This proves the supplied simulated company-frame shapes in VS2022 17.14. It does not prove real OpenCvSharp/Emgu/Bitmap or industrial-camera SDK objects through the new automatic path. Those require exact runtime objects, versions, lifetime rules, and reproducible samples.

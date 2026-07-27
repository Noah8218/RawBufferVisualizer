# Architecture And Validation

This document describes the published `1.0.45` baseline plus the uncommitted `1.0.46` Buffer Doctor, Smart Type Mapper, and Automatic Vision Inspector worktree. It is the technical source for debugger transfer, viewer behavior, compatibility, tests, packaging, and troubleshooting.

## Supported Environment

| Surface | Current target |
| --- | --- |
| Visual Studio extension | Visual Studio 2022 17.9 or newer, manifest range `[17.9,18.0)` |
| Extension/VSSDK projects | .NET Framework 4.7.2 (`net472`) |
| Core/SDK/object source | `net472`, `netstandard2.0`, and/or `net8.0` depending on project |
| Standalone WPF viewer | `net472` and `net8.0-windows` |
| Build machine | Visual Studio 2022 with .NET desktop development and .NET 8 SDK or newer |

Visual Studio 18/2026 and explicit .NET 9/10 matrices are not current release claims. Do not update public support text until the manifest, build, install, and debugger compatibility have been tested.

## System Shape

```text
Debuggee object
    |\
    | \-- registered type/collection -> debugger visualizer provider
    |          -> VisualStudio.ObjectSource
    |          -> VisualStudio.Extensibility handoff
    |
    \---- unregistered local at Break Mode
               -> VSSDK current-frame scan
               -> saved mapping/member inference
               -> value and memory validation
               -> Smart Type Mapper only when needed
               |
               v
VisualStudio.Vssdk docked ToolWindow
    |
    v
Core RawImageSource -> viewport/tile planner -> internal canvas -> pixel/diagnostics UI
```

The user installs one VSIX. Internally, Modern debugger visualizer providers extract values and the VSSDK package hosts the docked ToolWindow. `RawBufferVisualizer.VisualStudio.Classic` remains a compatibility/build project but Classic visualizer assemblies are not installed as a second user visualizer package.

## Project Map

| Project | Responsibility |
| --- | --- |
| `RawBufferVisualizer.Core` | Descriptors, formats, diagnostics, pixel inspection, memory/file/process sources, tile planning, rendering helpers, split/difference sources. |
| `RawBufferVisualizer.Sdk` | Public `RawBufferSnapshot` and `RawBufferView` contracts used by applications. |
| `RawBufferVisualizer.BitmapAdapter` | Bitmap-to-snapshot conversion used by standalone/sample paths. |
| `RawBufferVisualizer.OpenCvSharpAdapter` | OpenCvSharp adapter used by standalone/sample paths; not the debugger compatibility mechanism. |
| `RawBufferVisualizer.VisualStudio.ObjectSource` | Debuggee-side metadata, preview, chunk, reflection, pointer, collection extraction, saved mappings, and pure image-member inference. |
| `RawBufferVisualizer.VisualStudio.Extensibility` | Debugger visualizer registration and transfer/handoff orchestration. Produces the public VSIX. |
| `RawBufferVisualizer.VisualStudio` | Shared inbox, temp storage, snapshot store, process/instance routing, and support-report helpers. |
| `RawBufferVisualizer.VisualStudio.Vssdk` | Docked package, ToolWindow, responsive IDE UI, Break Mode automatic inspection, VSSDK managed-array extraction, session ownership, and source lifecycle. |
| `RawBufferVisualizer.OpenGlCanvas` | Internal accelerated tiled canvas, progressive viewport scheduling, textures, interaction, and render metrics. The implementation name is not user-facing. |
| `RawBufferVisualizer.Wpf` | Optional standalone snapshot viewer and validation host. |
| `RawBufferVisualizer.VisualStudio.Classic` | Legacy compatibility/build metadata; obsolete Classic DLLs are removed during install. |
| `RawBufferVisualizer.Samples` | Generates supported snapshot samples. |
| `RawBufferVisualizer.VisualizerDebuggee` | Breakpoint-driven manual/installed-VSIX test application for images, collections, and large Mats. |
| `RawBufferVisualizer.Tests` | Executable self-test suite for core, adapters, object sources, contracts, storage, and regressions. |
| `tests/RawBufferVisualizer.LegacyCompatibility` | Parameterized old/current Bitmap/OpenCvSharp/Emgu package compatibility executable, invoked by script. |

## Debugger Provider Registration

The visualizer icon appears only when the generated extension metadata contains a matching target registration. Conversion code alone cannot make an icon appear.

Current registrations include:

- exact product types: `RawBufferSnapshot`, `RawBufferView`;
- `System.Drawing.Bitmap`;
- versioned and unversioned OpenCvSharp `Mat` assembly names;
- legacy/current Emgu `Mat` assembly names;
- one exact ImagePtr compatibility target: `Cressem.ImageModel.ImagePtr, Cressem.ImageModel`;
- open generic `List<>` and `Dictionary<,>`;
- `ArrayList`, `Hashtable`, `object[]`, and registered image arrays.

Always validate the generated `.vsextension/extension.json` inside the VSIX after changing provider targets. `Publish-VisualStudioExtension.ps1` performs structural checks and fails packaging when required targets/providers are missing.

## Compatibility Strategy

### OpenCvSharp

The debugger object source checks the runtime type name and reads Mat metadata through reflection across old and new API shapes. The extension does not require the debuggee to bind to the OpenCvSharp version used by a standalone adapter project.

Mapped shapes:

- 8-bit C1 -> `Mono8`;
- 8-bit C3 -> `BGR24`;
- 8-bit C4 -> `BGRA32`;
- 16-bit unsigned C1 -> `Mono16`;
- 32-bit float C1 -> `Float32`.

### Emgu CV

The object source uses reflection over rows, columns, step, depth, channels, data pointer, and dimensions. Provider registrations include old `Emgu.CV.World`, NetStandard, Platform.NetStandard, and current `Emgu.CV` assembly identities.

Mapped shapes:

- `Cv8U` C1/C3/C4;
- `Cv16U` C1;
- `Cv32F` C1.

### Bitmap

Bitmap uses .NET Framework drawing APIs and supports indexed 8bpp, 24bpp RGB storage mapped to BGR, and common 32bpp RGB/ARGB/PARGB mappings.

### ImagePtr and SDK buffers

The ImagePtr object-source conversion is shape-based after the provider has been invoked. Required fields/properties are equivalent to:

```csharp
IntPtr Ptr;
long Length;
int Width;
int Height;
int Step;
int Bpp;
```

`Bpp` means bytes per pixel in this shape: 1=`Mono8`, 3=`BGR24`, 4=`BGRA32`.

Important registration limit: Visual Studio debugger visualizers are attached to registered target types. The current individual provider is registered only for `Cressem.ImageModel.ImagePtr, Cressem.ImageModel, Version=1.0.0.0`. A class with the same properties but a different namespace or assembly does not automatically get an icon. Collection extraction can still recognize a shape after the registered collection provider is invoked, which explains why an object may work inside a supported collection but not as an individual variable.

The public generic pointer contract is `RawBufferView`. Do not describe arbitrary ImagePtr-style variables as individually supported until provider metadata, sample code, tests, and package validation prove that exact claim.

For a richer industrial descriptor, expose `RawBufferView` with buffer address/length, width, height, stride, format, channels, bit depth, valid bits/byte order as applicable. A pointer without dimensions, stride, format, and lifetime is not safely visualizable.

## Automatic Inspection Flow

Automatic Vision Inspector complements provider registration; it does not change Visual Studio's registration rules.

1. The package receives a Break Mode event and schedules the scan at WPF `DispatcherPriority.ContextIdle`.
2. The scanner reads `Debugger.CurrentStackFrame.Locals`, skipping primitive, root-array, and root-collection values.
3. Each remaining local contributes at most 128 direct members and 64 one-level nested members. Unlimited recursion is prohibited.
4. `VisionMemberInference` assigns data/width/height/stride/format roles and a structural confidence score.
5. A saved type mapping takes precedence. Otherwise only complete, unambiguous inference at 90% or higher is eligible to open automatically.
6. Pointer-backed shapes reuse paused-process memory. Managed-array shapes use the current VSSDK frame/property child enumerator; the EnvDTE element fallback is bounded to 256 items.
7. The constructed descriptor and current buffer are validated before an image row is accepted.
8. Ambiguous/incomplete results at 40% or higher become visible mapping candidates; lower-scoring objects are hidden.
9. Automatic rows are replaced by stable root-expression key on refresh, while manual/provider-handoff rows are preserved.

The scanner reads debugger-visible fields and property getters. It does not call arbitrary vendor methods, load SDK assemblies dynamically, decode private native layouts, or control an acquisition device. See [automatic-vision-inspector.md](automatic-vision-inspector.md) for the detailed confidence/UX contract and current evidence.

## Individual Transfer Flow

1. The provider asks the object source for `VisualizerSnapshotMetadata`.
2. Metadata includes descriptor, source type/name, buffer length, and direct-memory fields when the source supports them.
3. For buffers below 64 MiB, the extension writes descriptor metadata and requests raw chunks into an owned `.raw` file.
4. For buffers at or above 64 MiB, the extension first requests a sampled preview bounded to 512 x 512.
5. If the large source supports direct memory and has a valid process ID/address, the docked viewer receives a live process-memory request after the preview.
6. If direct memory is unavailable, the extension falls back to chunking the full source to an owned temp snapshot after checking disk space.
7. The handoff is written to an inbox scoped to the hosting `devenv.exe` process and acknowledged by that ToolWindow.
8. The temporary Modern host closes after successful handoff; the docked VSSDK window remains.

The 64 MiB threshold is `PreviewFirstThreshold` in `DebuggerVisualizerLaunch.cs`. The preview is an early visible state, not proof that the full/live source is available forever.

## Collection Transfer Flow

1. The provider registers a supported collection shape.
2. The object source enumerates only the explicitly supported collection, not an arbitrary lazy `IEnumerable`.
3. Up to 256 entries are processed per invocation.
4. Each entry is mapped through the same raw/Bitmap/OpenCvSharp/Emgu/ImagePtr logic.
5. Valid entries become image rows; null, unsupported, or failed entries become error rows.
6. Entry names use index or dictionary key labels such as `[0]` or `[key]`.
7. Large entries can use the same preview/direct-memory path.

Do not replace this with broad `IEnumerable` execution while the debugger is paused; user iterators can have side effects, block, or mutate state.

## Viewer Source And Rendering Flow

`RawImageSource` abstracts where bytes live:

- managed memory;
- random-access source;
- file-backed raw payload;
- live process memory;
- split and difference compositions.

The viewer:

1. computes Fit/zoom/pan and the visible source rectangle;
2. plans logical tiles for the viewport;
3. reads only needed source rows/tiles;
4. decodes the requested format into display tiles;
5. uploads/caches bounded textures;
6. renders progressively for large file-backed sources;
7. reads pixels/raw bytes through the same source abstraction.

Current internal limits relevant to performance reviews:

- display tile size: 1024;
- maximum cached textures: 96;
- full in-memory/CPU-preview threshold: 512 MiB;
- histogram sampling dimension: 1024.

Do not increase these limits without measuring working set, frame time, and behavior in the docked Visual Studio host.

## Live Process Memory Rules

Direct memory reduces full-buffer transfer and temp-disk use, but it has a strict lifetime:

- the debuggee must still be alive and paused;
- the backing Mat/pointer object must still own valid memory;
- Continue, object disposal, buffer reuse, or process exit can invalidate reads;
- the viewer converts this into a controlled `Unavailable` state rather than continuing to dereference the pointer.

A compressed image's file size is unrelated to decoded Mat memory. Calculate decoded bytes from rows, stride, channels, and element size. A very large Mat can exhaust or terminate the debuggee even if viewer-side transfer is bounded.

## Temporary Storage And Cleanup

Root:

```text
%TEMP%\RawBufferVisualizer\VisualStudio
```

Possible contents:

- per-session preview/full `.rbuf.json` and `.raw` files;
- per-Visual-Studio inbox/request files;
- `package.log`;
- `latest-error-report.txt` when a failure report is generated;
- performance smoke files when a smoke script redirects metrics.

Lifecycle:

- Delete removes the selected row and disposes/deletes its owned temp directory;
- Clear disposes all rows and owned directories;
- failed/partial handoffs attempt immediate cleanup;
- new sessions remove stale snapshot directories older than 24 hours;
- `package.log` is reset when it exceeds 1 MiB;
- live direct-memory sources avoid a full raw snapshot but may retain the bounded sampled preview until the row/session is disposed.

When investigating disk usage, distinguish user-exported snapshots, smoke artifacts, current owned temp rows, and stale crash leftovers.

## Error And Support Flow

Failures should be forwarded into the main image list, not shown only as transient dialogs. An error row carries a stable ID, title, source type, message, and optional stack/details.

Support report requirements:

- extension and Visual Studio versions;
- source type and descriptor/diagnostic context when available;
- error ID/message/details;
- package/activity log locations;
- explicit statement that image payload is not included.

Reports can include local paths and variable names, so the user must review them before sharing.

Common logs:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\package.log
%TEMP%\RawBufferVisualizer\VisualStudio\latest-error-report.txt
%APPDATA%\Microsoft\VisualStudio\17.0_*\ActivityLog.xml
```

## Build And Test Commands

Run from `C:\Git\RawBufferVisualizer`.

### Restore and Release build

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
```

### Core/object-source self-test

```powershell
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
```

### Legacy compatibility matrix

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLegacyImageCompatibility.ps1
```

### Standalone interaction smoke

```powershell
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeViewerInteractions.ps1 -Configuration Release
```

### Docked layout and interaction/performance smoke

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild
```

### Preview-first and large-Mat smoke

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokePreviewFirstHandoff.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeSampledPreviewPerformance.ps1 -Configuration Release -Framework net472 -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixLargeMats.ps1 -Configuration Release -VisualStudioInstanceId <VS2022-instance-id> -NoBuild
```

### Memory/cleanup soak

```powershell
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedMemorySoak.ps1 -Configuration Release -Framework net472 -NoBuild
```

### Dense file-backed large-image smoke

These commands create real dense files and require about 10 GB and 40 GB plus safety margin. Use a drive with sufficient free space; do not run them casually on the repository drive.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 100000 -Height 100000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 200000 -Height 200000 -Configuration Release -Framework net472 -Dense -NoBuild
```

See [large-image-samples.md](large-image-samples.md) for custom output roots and portable samples.

## Manual Installed-VSIX Matrix

After changes to providers, handoff, packaging, or ToolWindow code:

1. Close every Visual Studio process.
2. Install/reinstall the Release VSIX.
3. Restart Visual Studio 2022.
4. Run `RawBufferVisualizer.VisualizerDebuggee` under the debugger.
5. Inspect all individual raw/registered-pointer/Bitmap/OpenCvSharp/Emgu cases, and confirm any claimed sample pointer type exactly matches a provider target.
6. Inspect typed lists/dictionaries, mixed collections, and arrays.
7. Confirm all rows land in the same docked viewer.
8. Confirm valid/error row recovery, pixel values, Save, Delete, Clear, Fit, wheel zoom, and drag pan.
9. Continue/exit after a live large Mat and confirm controlled source-unavailable behavior.
10. Restart Visual Studio with no solution and confirm no package-load popup.
11. Run two Visual Studio processes and confirm handoffs remain instance-local.

Detailed variable names are in [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md).

## Package And Version Flow

Version bump:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version <major.minor.patch>
```

The script updates:

- `RawBufferVisualizer.VisualStudio.Extensibility.csproj`;
- `RawBufferVisualizer.VisualStudio.Classic.csproj`;
- the public Extensibility `source.extension.vsixmanifest`.

Build/package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoZip
```

Release VSIX:

```text
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Developer install, with Visual Studio closed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

Registration repair, with Visual Studio closed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

Full publication steps and the environment-gated CD flow are in [release-runbook.md](release-runbook.md). An upload must use a version not already published. Do not create a version bump only to edit documentation when the Marketplace portal allows copy changes independently.

## Current Validation Baseline

Published implementation baseline `a23d8ad` / `1.0.45`:

- GitHub CI #71: success;
- local Release build/tests/package: passed in the release-preparation turn;
- generated VSIX: 1,843,094 bytes, SHA256 `45561D2BC170DE7D088DB5C4D03C6FD5D7E97CBC916D00C3A902FCE5076163FF`;
- four VSSDK threading analyzer warnings remain (`VSTHRD001` x3, `VSTHRD110` x1);
- public ImagePtr-style documentation is broader than the exact individual provider registration and must be corrected or implemented before the next support-matrix claim;
- dense 100k/200k file-backed, installed 8192 Mat, docked 24k real-input, and 240-cycle memory soak evidence are recorded in [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md).

Uncommitted 1.0.46 feature worktree, rechecked 2026-07-27:

- full Release solution build passed with 18 `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and zero errors;
- `RawBufferVisualizer.Tests` passed;
- Automatic Inspector layout, Buffer Doctor panel, docked layout widths, Preview-first handoff, and Smart Type Mapper UI smokes passed;
- installed-VSIX Buffer Doctor and Automatic Vision Inspector scenarios passed in VS2022 17.14;
- the installed-VSIX Smart Type Mapper fallback passed from a 92% `MappingRequired` candidate through live-memory preview, save, and automatic 640 x 484 `Mono12PackedLsb` reopen with zero final errors.

Do not reuse this evidence after a relevant source change. Re-run the smallest checks that cover the changed surface and update the baseline only when they pass.

## Troubleshooting Order

### Build reports NETSDK1047 for net472/win-x86

The WPF project has runtime-specific restore assets. If `dotnet build --no-restore` reports that `project.assets.json` has no `net472/win-x86` target, restore the solution first:

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
```

This was rechecked during the 2026-07-17 handoff: the first no-restore attempt found stale/incomplete assets, then the documented restore + build sequence passed with four known VSTHRD warnings and zero errors.

### Visualizer icon missing

1. Confirm installed extension version.
2. Confirm the exact runtime type and assembly full name.
3. Inspect generated provider target metadata in the VSIX.
4. Check legacy/current assembly registrations.
5. For pointer objects, remember that shape reflection does not create an icon for an unregistered type; use `RawBufferView` or add/prove an exact provider registration.
6. Test the same type in `VisualizerDebuggee` or a minimal net472 sample.

### Package did not load correctly

1. Close all Visual Studio processes.
2. Run `Repair-VisualStudioExtensionRegistration.ps1`.
3. Restart and inspect `ActivityLog.xml` plus `package.log`.
4. Check for a stale `CodeBase` path or a `Microsoft.VisualStudio.Threading` version newer than 17.9.

### Image unavailable after opening

1. Confirm the debuggee is paused.
2. Confirm the source Mat/pointer remains alive and undisposed.
3. Confirm the process did not exit and the buffer was not reused.
4. Reopen the visualizer at a new breakpoint rather than retrying an invalid live pointer.

### Large image crashes or takes extreme memory

1. Calculate decoded bytes from dimensions and stride, not compressed file size.
2. Check debuggee bitness/available memory and OpenCV pixel limits.
3. Check whether user code retains several full Mats.
4. Determine whether the source used direct memory, preview+full chunk fallback, or a file-backed snapshot.
5. Inspect temp disk free space and stale directories.
6. Reproduce with the provided large-image smoke before changing cache limits.

### Docked zoom/pan is slow or blank

1. Reproduce in the installed VSIX docked window with real wheel/drag input.
2. Capture render metrics and framebuffer, not only button/slider timings.
3. Check viewport bounds, tile selection, cache eviction, and progressive generation cancellation.
4. Verify pixel/status/selection updates continue during interaction.
5. Compare against the current `1.0.45` baseline before changing rendering structure.

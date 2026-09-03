# Architecture And Validation

This document describes the architecture implemented by public `2.0.5.0` and retained by the local `2.0.6.0` candidate while preserving earlier release baselines for regression history. The 2.0 line locks the vendor-neutral 2D carrier/layout contract, shared fail-closed transfer validation, live-source invalidation, debugger-session handoff admission, and ranked interpretation repair inside the mapping dialog. The current hybrid package keeps the in-process `net472` VSSDK 17.9 package separate from the out-of-process `net8` debugger providers. Version 2.0.4 changed only the docked presentation of the existing complete viewer reset. Version 2.0.5 added expression-name recovery, native-object-versus-pixel pointer provenance, complete native-read enforcement, and complete registered image-array identities. Version 2.0.6 changes only the immutable package/release identity and retains those boundaries. This is the technical source for debugger transfer, viewer behavior, compatibility, tests, packaging, and troubleshooting.

## Supported Environment

| Surface | Current target |
| --- | --- |
| Visual Studio extension | Public `2.0.3.0` and local `2.0.4.0`: VS2022 `17.9+` and stable VS2026 `18.x`, Community/Professional/Enterprise x64 |
| Extension/VSSDK projects | VSSDK package `net472`; out-of-process provider `net8.0-windows8.0` |
| Core/SDK/object source | `net472`, `netstandard2.0`, and/or `net8.0` depending on project |
| Standalone WPF viewer | `net472` and `net8.0-windows` |
| Build machine | Visual Studio 2022 with .NET desktop development and .NET 8 SDK or newer |

The public `2.0.3.0` package and local `2.0.4.0` candidate use VisualStudio.Extensibility `17.9.2092`, Visual Studio SDK `17.9.37000`, VSSDK BuildTools `17.9.3184`, and `[17.9,18.0)`. The 2.0.4 change does not alter the debugger-provider or VSSDK dependency graph. Exact 2.0.4 installed-host evidence belongs in [release-qualification-2.0.4.md](release-qualification-2.0.4.md); historical 2.0.3 evidence remains in [release-qualification-2.0.3.md](release-qualification-2.0.3.md). Visual Studio 2019, 32-bit Visual Studio, Preview/Insiders builds, and explicit .NET 9/10 matrices are not support claims.

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

Connect Doctor does not add another service or pane. `TypeMappingDialog` creates a bounded live `RawImageSource`, calls Core `BufferDoctor.Diagnose`, and presents shared `BufferDiagnosisCandidateItem` rows. Core owns generation, scoring, ambiguity, and current-geometry candidate retention; the dialog owns only draft role selection, candidate preview, the exact-representability Save gate, reset, and cancellation. If the wrapper exposes no explicit length, diagnosis may probe at most `stride * height` derived from the visible draft; an explicit shorter length is rejected rather than overridden. Manual Preview still reads only the selected descriptor's required span.

## Project Map

| Project | Responsibility |
| --- | --- |
| `RawBufferVisualizer.Core` | Descriptors, formats, diagnostics, pixel inspection, memory/file/process sources, tile planning, rendering helpers, split/difference sources. |
| `RawBufferVisualizer.Sdk` | Public `RawBufferSnapshot` and `RawBufferView` contracts used by applications. |
| `RawBufferVisualizer.BitmapAdapter` | Bitmap-to-snapshot conversion used by standalone/sample paths. |
| `RawBufferVisualizer.OpenCvSharpAdapter` | OpenCvSharp adapter used by standalone/sample paths; not the debugger compatibility mechanism. |
| `RawBufferVisualizer.VisualStudio.ObjectSource` | Debuggee-side metadata, preview, chunk, reflection, pointer, collection extraction, saved mappings, and pure image-member inference. |
| `RawBufferVisualizer.VisualStudio.Extensibility` | Owns only the out-of-process debugger visualizer providers and composite-extension metadata; it is packaged under `OutOfProc` and has no `AsyncPackage`, VSCT, or `.pkgdef` ownership. |
| `RawBufferVisualizer.VisualStudio` | Shared inbox, claimed-handoff coordination, debugger-session generation admission, document-workspace ownership, temp/snapshot leases, process/instance routing, and support-report helpers. |
| `RawBufferVisualizer.VisualStudio.Vssdk` | Owns the in-process `AsyncPackage`, VSCT, generated `.pkgdef`, ToolWindow/UI, debugger events, Break Mode automatic inspection, VSSDK extraction, commands, and composite VSIX container. |
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
- open generic `List<>`, `Dictionary<,>`, and `ConcurrentDictionary<,>` for .NET Framework and current .NET;
- `ArrayList`, `Hashtable`, `object[]`, and registered image arrays.

Third-party array registrations must use complete assembly-qualified identities. Visual Studio parses the target type before invoking the provider; incomplete strings such as `System.Drawing.Bitmap[], System.Drawing` can fail inside Visual Studio with `ArgumentException: version string portion is too short or too long`. The current array registrations therefore cover:

- `System.Drawing.Bitmap[]` in .NET Framework `System.Drawing, Version=4.0.0.0` and `System.Drawing.Common` assembly versions 4 through 10;
- `OpenCvSharp.Mat[]` assembly versions `1.0.0.0` and `4.0.0.0`;
- the tested Emgu array identities `Emgu.CV.World 3.4.3.3016`, `Emgu.CV.World.NetStandard 1.0.0.0`, `Emgu.CV.Platform.NetStandard 4.5.5.4823`, `Emgu.CV 4.8.1.5350`, and `Emgu.CV 4.13.0.5924`;
- product-owned `RawBufferSnapshot[]` and `RawBufferView[]` through `typeof(...)`, so their generated identity stays aligned with the built SDK assembly.

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

### Object pointer and pixel-address provenance

`VisualizerSnapshotMetadata` keeps `SourcePointerAddress`/`SourcePointerLabel` separate from `BufferAddress`. The Tool Window calls the latter `Pixels` because it is the address used for byte reads. It never substitutes a pixel pointer for a library object's native pointer.

| Source | `SourcePointerLabel` / source address | `BufferAddress` / pixel address | Lifetime shown |
| --- | --- | --- | --- |
| OpenCvSharp `Mat` | `Ptr` from `CvPtr` | `Data` | `CAPTURED`, `LIVE`, `PREVIEW`, or `UNAVAILABLE`, according to the selected transfer path and current lifetime |
| Emgu CV `Mat` | `Ptr` | `DataPointer` | `CAPTURED`, `LIVE`, `PREVIEW`, or `UNAVAILABLE`, according to the selected transfer path and current lifetime |
| ImagePtr | `Ptr` | the same `Ptr` | `CAPTURED`, `LIVE`, `PREVIEW`, or `UNAVAILABLE`, according to the selected transfer path and current lifetime |
| `RawBufferView` | `Buffer` | the same `Buffer` | `CAPTURED`, `LIVE`, `PREVIEW`, or `UNAVAILABLE`, according to the selected transfer path and current lifetime |
| `System.Drawing.Bitmap` | `Scan0` observed while `LockBits` is active | the same captured `Scan0` | always `CAPTURED`, never live Bitmap memory |

When source and pixel addresses are equal, the row renders one combined label such as `Ptr / Pixels`, `Buffer / Pixels`, or `Scan0 / Pixels`. When they differ, `Ptr` and `Pixels` are rendered on separate wrapped lines. The context menu likewise keeps **Copy pointer address** and **Copy pixel address** separate. The debugger object/expression name is a separate bold line above provenance and thumbnail content.

The pointer fields describe provenance; they do not by themselves make a row live. A small registered-object handoff can copy all bytes immediately and retain the originating addresses as `CAPTURED`, as exercised in the direct-type runtime screenshots. `LIVE` is reserved for a document that still reads the paused debuggee process directly. Large direct-memory transfers, automatic inspection, and mapped buffers can therefore be live while eligible, whereas a completed copied handoff remains captured.

Visual Studio 17.9 does not expose the newer original-expression API used by later debugger hosts. For registered reference objects, the handoff therefore carries `RuntimeHelpers.GetHashCode(target)` plus source type. `DebuggerExpressionDisplayNameResolver` correlates that identity with current-frame Locals/Arguments and falls back to a unique pointer/type match. Collections correlate the root object and append the item selector, producing names such as `imageList[0]`, `imageDictionary[key]`, and `bitmapArray[1]`. If the match is not unique, the provider-supplied display name remains the safe fallback.

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

### Vendor-neutral 2D buffer matrix

[Vendor-Neutral 2D Buffer Compatibility Matrix](vendor-neutral-buffer-compatibility-matrix.md) is the 2.0 source of truth for pointer/managed carriers, dimensions, stride/length, image offsets, formats, valid bits, byte order, and lifetime. `VisualizerChunkedTransfer.CreateMetadataCore` and every `RawImageSource` construction path apply the common `RawBufferDiagnostics` boundary. Minimum-stride and required-length arithmetic is checked, undefined pixel-format/byte-order enum values are rejected, and valid metadata must fit the supported address/stream range before transfer or rendering. Repository fixtures use only project-owned neutral shapes. The current P0 `2.0.0.0` candidate passed dual-IDE installed qualification, but this work does not authorize a vendor SDK adapter or change the public `1.0.53` Marketplace asset.

## Automatic Inspection Flow

Automatic Vision Inspector complements provider registration; it does not change Visual Studio's registration rules.

1. The package receives a Break Mode event and schedules the scan at WPF `DispatcherPriority.ContextIdle`.
2. The scanner merges `Debugger.CurrentStackFrame.Locals` and `.Arguments`, deduplicated by root expression, while skipping primitives and unsupported root arrays/collections.
3. Every scan expands exact `List<OpenCvSharp.Mat>`, `List<Emgu.CV.Mat>`, and corresponding one-dimensional arrays into indexed expressions. The policy caps work at 8 items per collection, 16 items and 8 collection roots per scan.
4. Exact initialized OpenCvSharp `Mat` and Emgu CV `Mat` roots or indexed elements take a fixed known-type branch. OpenCvSharp reads `Data`, `Cols`, `Rows`, `Step()`, `Depth()`, and `Channels()`; Emgu reads `DataPointer`, `Cols`, `Rows`, `Step`, `Depth`, and `NumberOfChannels`.
5. Known Mat metadata is converted to a validated descriptor and opened through paused-process live memory. Null/disposed collection elements become isolated `[Failed]` rows. Bitmap, `RawBufferSnapshot`, `RawBufferView`, the exact ImagePtr target, and unsupported collections remain registered-path owned.
6. Each remaining unregistered root contributes at most 128 direct members and 64 one-level nested members. Unlimited recursion is prohibited.
7. `VisionMemberInference` assigns data/width/height/stride/format roles and a structural confidence score.
8. A saved type mapping takes precedence. Otherwise only complete, unambiguous inference at 90% or higher is eligible to open automatically.
9. Pointer-backed shapes reuse paused-process memory. Managed-array shapes use the current VSSDK frame/property child enumerator; the EnvDTE element fallback is bounded to 256 items.
10. The constructed descriptor and current buffer are validated before an image row is accepted.
11. Ambiguous/incomplete results at 40% or higher become visible `[Map]` candidates; lower-scoring objects are hidden. A recognized shape whose current pointer/array read fails becomes `[Failed]`, not a misleading mapping request.
12. Every root/collection element is processed behind an exception boundary, so a failed candidate cannot block successful images. A successful row is selected in preference to an error row.
13. Automatic rows are replaced by stable root/index-expression key on refresh, while manual/provider-handoff rows are preserved.
14. **Auto Inspect on Break** is the only versioned per-user Automatic Inspector preference. It defaults on and persists across Visual Studio restarts. Restoring it does not itself scan or force the Tool Window open. **Scan Now** is independent of Auto Inspect on Break, and both scan paths always apply the bounded exact-Mat collection policy.

The scanner reads debugger-visible fields and property getters. Exact OpenCvSharp/Emgu capture evaluates only the fixed extension-owned metadata expressions above; exact `List<T>.Count` or array `Length` is the only additional collection metadata evaluation. It does not execute arbitrary `IEnumerable`, inject Bitmap `LockBits`/`UnlockBits`, load SDK assemblies dynamically, decode private native layouts, or control an acquisition device. A breakpoint on an assignment statement stops before that statement executes, so the object must be constructed on an earlier line. See [automatic-vision-inspector.md](automatic-vision-inspector.md) for the detailed confidence/UX contract and current evidence.

## Individual Transfer Flow

When the debugging UI context activates, the VSSDK package preloads asynchronously, registers its commands/events, and arms only the file-system event listener. It does not open the ToolWindow or scan the inbox during package initialization. Polling begins only after a new handoff event or an explicit ToolWindow command.

1. The provider asks the object source for `VisualizerSnapshotMetadata`.
2. Metadata includes descriptor, source type/name, expression identity, buffer length, source-pointer provenance, and direct pixel-memory fields when the source supports them.
3. For buffers below 64 MiB, the extension writes descriptor metadata and requests raw chunks into an owned `.raw` file.
4. For buffers at or above 64 MiB, the extension first requests a sampled preview bounded to 512 x 512.
5. If the large source supports direct memory and has a valid process ID/address, the docked viewer receives a live process-memory request after the preview.
6. If direct memory is unavailable, the extension falls back to chunking the full source to an owned temp snapshot after checking disk space.
7. The producer writes complete request JSON to a unique `.publishing.<guid>` path and atomically moves it to the visible `.rbuf-handoff` path in an inbox scoped to the hosting `devenv.exe` process.
8. The docked package claims a ready request exactly once by using an exclusive claim guard and moving it to a unique `.processing.<guid>` path.
9. `ClaimedHandoffOpenCoordinator` reads the processing file and publishes an explicit ACK only after the ToolWindow-supplied opener adds the image/error document successfully. A failed open atomically publishes its reason before a NACK marker.
10. The producer treats only ACK as success. Disappearance of the ready file means Processing, not acknowledgement; NACK and conflicting terminal markers are failures.
11. The temporary Modern host closes after every request reaches a successful ACK; the docked VSSDK window remains. Modern timeout/cancel and Classic fire-and-forget both schedule the shared terminal-artifact cleanup.

Registered unmanaged producers (`RawBufferView`, ImagePtr, OpenCvSharp Mat, Emgu Mat, and Bitmap) route chunk and sampled-preview reads through `CurrentProcessMemoryReader`. It uses current-process `ReadProcessMemory` and accepts a request only when the complete requested byte count is returned. Freed memory, inaccessible pages, and partial native reads therefore become a controlled `IOException`; a partially filled or zero-filled image is not returned as valid data. Sampled preview uses one bounded page cache per transfer. Bitmap reads remain inside `LockBits`/`UnlockBits`, and a negative native stride is normalized from the lowest row address so the transferred descriptor keeps top-to-bottom row order.

Each new transfer reads the bytes currently stored at the supplied address. Reusing the same address for another allocation is indistinguishable from mutating the original object, because a pointer is not an object identity. A successful read also is not an atomic snapshot of a buffer being modified concurrently; the caller must keep the source paused, alive, and stable for the transfer.

Transient handoff reads and terminal-marker moves retry `IOException` and `UnauthorizedAccessException` up to 10 total attempts at 50 ms intervals. Persistent I/O failure or JSON parse/validation failure remains an error. `ScheduleTerminalArtifactCleanup` polls every 100 ms for up to two minutes and removes ACK/NACK/conflict terminal artifacts only. Locked ACK/NACK cleanup is retried, and a request must be observed Missing consecutively before it leaves the pending set. It never deletes Ready or Processing on timeout because another consumer may still own in-flight work. Outer non-cancellation exception catches also schedule the same cleanup. On producer cancellation/exception, any owned request that is still non-Missing causes its snapshot payload directory to be preserved. A marker that terminalizes after the two-minute window and a Processing file stranded by process crash are not immediately reclaimed; stale-session cleanup remains the eventual recovery boundary.

`RawBufferDocumentWorkspace<TDocument>` is the single owner of the ToolWindow document collection, active-document state, removal, clear, and disposal. The ToolWindow translates user/UI events into workspace operations and owns presentation updates, but does not mutate the collection or active state directly. `ImageDocument` owns its image source and a `VisualStudioSnapshotLeaseOwner`; `RawBufferToolWindow.Dispose` disposes the control, which disposes the workspace and all remaining documents.

The 64 MiB threshold is `PreviewFirstThreshold` in `DebuggerVisualizerLaunch.cs`. The preview is an early visible state, not proof that the full/live source is available forever.

## Collection Transfer Flow

1. The provider registers a supported collection shape.
2. The object source enumerates only the explicitly supported collection, not an arbitrary lazy `IEnumerable`.
3. Up to 256 entries are processed per invocation.
4. Each entry is mapped through the same raw/Bitmap/OpenCvSharp/Emgu/ImagePtr logic.
5. Valid entries become image rows; null, unsupported, or failed entries become error rows.
6. Entry names preserve the resolved collection root and append index or dictionary-key labels such as `imageList[0]` or `imageDictionary[key]`; provider labels remain the fallback when the root cannot be resolved uniquely.
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

1. keeps an explicit `Fit` or `Manual` view mode and computes the visible source rectangle;
2. plans logical tiles for the viewport;
3. reads only needed source rows/tiles;
4. decodes the requested format into display tiles;
5. uploads/caches bounded textures;
6. renders progressively for large file-backed sources;
7. reads pixels/raw bytes through the same source abstraction.

A newly opened/selected image, the Fit command, and viewer double-click enter Fit mode. Fit recomputes an aspect-correct view with a 5% margin when the viewport changes. Wheel zoom, pan, and 1:1 enter Manual mode; a Manual resize preserves zoom and image center while normalizing the view rectangle to the current viewport aspect.

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
- `ProcessMemoryRawImageSource` owns an idempotent cancellation lifetime, and its stream checks that lifetime before and after `ReadProcessMemory`;
- entering Run Mode clears the current debugger thread, disposes every live process-backed document source, cancels progressive rendering, increments render generations, and refreshes those rows as `Unavailable`;
- already-rendered pixels may remain visible as context, but pixel reads, grid sampling, progressive uploads, and later source reads are disabled;
- copied managed-array and file-backed documents remain available because Visual Studio already owns their bytes;
- a handoff captures the current Break generation before opening the ToolWindow; Continue invalidates that generation, so delayed work cannot open during Run Mode or revive in a later Break session.

`DebuggerHandoffSessionGate` owns only session admission and generation comparison. `RawBufferVisualizerPackage` owns DTE event wiring and invokes the gate plus ToolWindow invalidation. `RawBufferToolWindowControl` owns document state and presentation, and `RawOpenGlImageCanvas` owns renderer cancellation/generation state. A `partial` split is not used as an architectural substitute for these boundaries.

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
- Clear disposes all rows and owned directories, clears the active renderer and every document-owned presentation state, then exposes the empty-viewer guidance;
- terminal ACK/NACK/conflict handoff artifacts are polled and cleaned for up to two minutes at 100 ms intervals;
- timed-out Ready/Processing handoffs and their payloads are preserved so an in-flight consumer is not disrupted;
- terminal markers produced after that two-minute cleanup window and Processing items stranded by process crash are not immediately reclaimed;
- new sessions remove stale snapshot directories older than 24 hours;
- every open owned file-backed document locks a unique `.rbuf-active-*.lease`, so stale cleanup skips its directory;
- preview-to-full replacement acquires the next directory lease before releasing the prior one; source disposal is followed by a second old-directory delete attempt;
- `package.log` is reset when it exceeds 1 MiB;
- live direct-memory sources avoid a full raw snapshot but may retain the bounded sampled preview until the row/session is disposed.

After a process crash, an unlocked lease marker and its payload directory can remain until the later stale sweep. This is expected eventual cleanup, not an active-document deletion risk.

When investigating disk usage, distinguish user-exported snapshots, smoke artifacts, current owned temp rows, and stale crash leftovers.

### Tool Window Clear contract

`Clear` is a complete document-presentation reset, not only a collection clear. After it returns:

- the image list is empty, the accelerated image surface is hidden, the empty guidance is visible, and status is `0 images`;
- Wide and Compact Interpret format/endian selections are unset, numeric fields are blank, and Apply/Diagnose are disabled;
- Buffer Doctor status/candidates, pixel/neighborhood values, marker/pin state, histogram/diagnostics, and comparison A/B references are cleared;
- responsive layout and the user's current Inspector open/closed state are preserved;
- opening or scanning a new valid document populates and enables both Interpret layouts again.

The 2.0.2 regression covers this contract at 540, 900, and 1160 px and in exact installed VSIX runs on VS17.14 and VS18.8. Test-only session JSON exposes these presentation fields only when `RAWBUFFERVISUALIZER_DOCKED_SESSION_JSON` is set; normal product behavior and storage are unchanged.

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
5. Inspect all individual raw/registered-pointer/Bitmap/OpenCvSharp/Emgu cases. Confirm the title is the debugger expression name; OpenCvSharp/Emgu show native `Ptr` separately from `Pixels`; ImagePtr/RawBufferView combine equal addresses; Bitmap shows captured `Scan0 / Pixels` and never claims live memory.
6. Inspect typed lists/dictionaries, mixed collections, and arrays. Confirm list/dictionary/array rows retain the root expression name and that direct Bitmap/OpenCvSharp/Emgu arrays open without Visual Studio target-type parse errors.
7. Confirm all rows land in the same docked viewer.
8. Confirm valid/error row recovery, pixel values, Save, Delete, Clear, Fit, wheel zoom, and drag pan. Fit must remain aspect-correct after resize; Manual zoom/pan must preserve scale and center.
9. Continue/exit after a live large Mat and confirm controlled source-unavailable behavior.
10. Restart Visual Studio with no solution and confirm no package-load popup.
11. Run two Visual Studio processes and confirm handoffs remain instance-local.
12. Open the View menu and confirm exactly one `Raw Buffer Visualizer` and one `Raw Buffer Visualizer: Scan Current Frame` entry.
13. Stop after image assignments, not on the construction line. Confirm OpenCvSharp/Emgu Mats open automatically on Break and **Scan Now**, while Bitmap remains glyph-owned.
14. Run `Test-VisualStudioMarketplaceUpdate.ps1` and the installed-VSIX `AutomaticVisionInspector` plus `MultiLibraryHybrid` smokes. A repair-script result is not release evidence.

Detailed variable names are in [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md).

## Package And Version Flow

Version bump:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version <major.minor.patch>
```

The script updates:

- `RawBufferVisualizer.VisualStudio.Extensibility.csproj`;
- `RawBufferVisualizer.VisualStudio.Classic.csproj`;
- the public Extensibility `source.extension.vsixmanifest`;
- `RawBufferVisualizerPackage.cs` installed-product version.

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

Registration repair, with Visual Studio closed, is limited to stale developer/legacy installations:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

Never use the repair command to qualify a clean Marketplace candidate. The release contract and regression checks are in [vsix-package-registration.md](vsix-package-registration.md).

The current package keeps VSPackage GUID `{1977574b-f107-465f-bfd1-5fc022907039}` and registers `Menus.ctmenu` resource version 2. The package, menu, and ToolWindow registrations must each occur exactly once. Every future VSCT command/group change must increment the menu resource version and update the publish, install, repair, and payload-verification checks in the same change.

Full publication steps and the environment-gated CD flow are in [release-runbook.md](release-runbook.md). An upload must use a version not already published. Do not create a version bump only to edit documentation when the Marketplace portal allows copy changes independently.

## Current Validation Baseline

Current unpublished `2.0.0.0` candidate, consolidated local qualification on 2026-08-06 KST:

- artifact size 1,923,731 bytes, SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`;
- path `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-connect-doctor-docs-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`;
- current-source aggregate self-tests, zero-warning Release solution build, release/environment/package guards, and ten-version Emgu/OpenCvSharp compatibility passed;
- seven package assemblies match the candidate and both installations by SHA-256;
- installed VS2022 and VS2026 each invalidated exactly five live sources after Continue while retaining the copied array, exposed and selected `Mono12PackedLsb 640 x 484 stride 960` draft-only until Save, and passed Buffer Doctor, Automatic Collections, Multi-Library Hybrid, and Environment toggle regressions with zero package-protocol errors;
- evidence root `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806`, with canonical verdict `consolidated-local-release-verdict.json`;
- qualification, source push at `0d3ac20`, CI run `31059894280`, clean installation, and exact public `1.0.53.0 -> 2.0.0.0` in-place update are complete. Owner upload approval, publication, and public readback remain separate gates.

Preserved unpublished `2.0.0.0` P0 full-safety qualification baseline, qualified 2026-08-06 KST:

- artifact size 1,917,791 bytes, SHA-256 `3C2DCC1E9E38990D1C17547331E15C5EE344ABEA07D3936B722747B0670AE7EE`;
- built from `origin/main` `3d88894` plus the then-uncommitted P0 worktree change set;
- current-source Release solution build and aggregate self-tests passed; descriptor overflow, undefined enums, disposed live-source reads, and handoff-generation transitions are covered;
- the full legacy matrix passed for five Emgu CV and five OpenCvSharp package versions;
- seven package-owned Raw Buffer Visualizer assemblies match the candidate, current Release build, VS2022 installation, and VS2026 installation by length and SHA-256;
- VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12023.21` each opened six smart-type scenario images, marked five live process-backed rows `Unavailable` on Continue, retained the copied managed-array row, and logged `invalidated 5 live source(s)`;
- full evidence and the preserved pre-P0 defect baseline are in [release-qualification-2.0.0.md](release-qualification-2.0.0.md) and `D:\OpenVisionLab-TestData\RawBufferVisualizer\p0-safety-20260805`;
- Marketplace remains exact public `1.0.53.0`; source has advanced beyond these P0 bytes, and no commit, push, tag, upload, or public readback is implied.

The prior 1,914,538-byte `2.0.0.0` candidate is preserved only as historical regression evidence because it left live rows readable/labelled after Continue. It is not a release candidate.

Exact local `1.0.53.0` vendor-safe release candidate, qualified 2026-08-05:

- artifact size 1,914,615 bytes, SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`;
- matching product source, qualification harness, and evidence are in pushed commit `7ab84b7`;
- Release solution build, aggregate self-tests, communication/environment checks, package guards, layout, memory soak, registration audit, installed-file equality, and Marketplace dry run passed;
- the same exact candidate passed ReleaseAnnouncement, EnvironmentCheck, AutomaticCollections, MultiLibraryHybrid, SmartTypeMapper, SmartTypeMapperPersisted, exact menu counts, installed registration, and protocol diagnostics on VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12023.21`;
- BufferDoctor, AutomaticVisionInspector, and OpenVariable also passed the exact candidate on VS2022;
- public Marketplace serves the immutable exact `1.0.53.0` package; Gallery metadata, public download, manifest, and Overview readback match the qualified candidate, with evidence under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018`;
- the exact artifact, criteria, commands, and D-drive evidence are recorded in [release-qualification-1.0.53.md](release-qualification-1.0.53.md).

The sections below preserve historical validation baselines for regression context. They are not the current release status.

Historical published implementation baseline `a23d8ad` / `1.0.45`:

- GitHub CI #71: success;
- local Release build/tests/package: passed in the release-preparation turn;
- generated VSIX: 1,843,094 bytes, SHA256 `45561D2BC170DE7D088DB5C4D03C6FD5D7E97CBC916D00C3A902FCE5076163FF`;
- four VSSDK threading analyzer warnings remain (`VSTHRD001` x3, `VSTHRD110` x1);
- public ImagePtr-style documentation is broader than the exact individual provider registration and must be corrected or implemented before the next support-matrix claim;
- dense 100k/200k file-backed, installed 8192 Mat, docked 24k real-input, and 240-cycle memory soak evidence are recorded in [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md).

Local 1.0.47 release candidate, rechecked 2026-07-28:

- full Release solution build passed with 18 `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and zero errors;
- `RawBufferVisualizer.Tests` passed;
- Automatic Inspector layout, Buffer Doctor panel, docked layout widths, Preview-first handoff, and Smart Type Mapper UI smokes passed;
- installed-VSIX Buffer Doctor, Automatic Vision Inspector, Smart Type Mapper, and hybrid registered/automatic scenarios passed in VS2022 17.14;
- the Smart Type Mapper fallback passed from an 88% `MappingRequired` candidate through live-memory preview, save, and automatic 640 x 484 `Mono12PackedLsb` reopen with zero final errors;
- the hybrid scenario opened real OpenCvSharp, Emgu CV, and Bitmap values through registered visualizers plus six camera-shape fixtures through Automatic Inspector, for nine images and zero errors;
- final artifact: 1,924,125 bytes, SHA256 `DAB2CE62007F77F11CFF828AF02EF2F2DAE26A3BB3238CF251679E4A69505174`.

The reusable acceptance record and exact evidence paths are in [release-qualification-1.0.47.md](release-qualification-1.0.47.md).

The Marketplace `1.0.47.0` package later failed on a clean PC because its separately generated VSSDK `.pkgdef` was present in the extension folder but did not produce a running docked package. The prior local install script wrote a developer-build `CodeBase`, masking that defect.

Local `1.0.48` registration hotfix, verified 2026-07-28:

- `RawBufferVisualizerPackage` and `RawBufferVisualizerCommands.vsct` are owned by the public hybrid project;
- the generated `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef` points to `$PackageFolder$\RawBufferVisualizer.VisualStudio.Extensibility.dll`;
- normal install no longer writes VSSDK registration;
- Release solution build and self-tests passed;
- installed-VSIX Automatic Inspector, Buffer Doctor, Smart Type Mapper, and MultiLibraryHybrid scenarios passed after removing the stale manual developer registration;
- final artifact: 1,990,304 bytes, SHA256 `AABBD3A36780AE070C3FBBDE384CB5CD9A1977607EA929D15DAEBF75899F717D`.

The reusable record is [release-qualification-1.0.48.md](release-qualification-1.0.48.md). A separate clean-PC installation remains the external pre-upload confirmation.

External follow-up invalidated the public `1.0.48` conclusion: the View command was present but did not open the ToolWindow, and debugger handoffs timed out. Version `1.0.49` changed the VSPackage GUID from `{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}` to `{1977574b-f107-465f-bfd1-5fc022907039}` while retaining the Marketplace extension ID. Public `1.0.49.0` is now reported working on that Windows 10 PC only after uninstall and clean reinstall. This does not prove an in-place update. Historical evidence is [release-qualification-1.0.49.md](release-qualification-1.0.49.md).

Public `1.0.50.0` established atomic Ready/Processing/ACK/NACK handoff, CTMENU resource version 2 and exact single-registration checks, explicit Fit/Manual state, and automatic OpenCvSharp/Emgu live capture. Failed candidate `1.0.51.0` added opt-in bounded Automatic Mat collection scanning and one-time in-product release highlights but could not activate the registered provider on VS2026. Candidate `1.0.52.0` corrects that runtime dependency and adds explicit workspace/coordinator/lease ownership. The exact current candidate identity is recorded at the start of this section.

A full Release solution build passed with 0 errors and 18 existing `VSTHRD010` warnings; the final candidate incremental/package build after the last handoff hardening passed 0/0 and self-test passed 20/20. Current-source Preview-first and 540/900/1160 Fit/Manual checks passed; the Fit matrix reported zero aspect error, 1.05 margin, and zero Manual zoom/center delta.

An ordinary local reinstall of the pre-collection 2,001,513-byte baseline without repair or `/ResetSkipPkgs` passed on Windows 10 Pro build 19045, VS 17.14.37314.3 instance `2c8402d8`. Fresh `MultiLibraryHybrid` evidence reports one Open command, one Scan command, automatic OpenCvSharp/Emgu, Bitmap glyph ownership, 8/8 automatic opens, nine documents, zero errors, duplicate-free refresh, and zero protocol errors. Fresh `AutomaticVisionInspector` reports six opened, one mapping candidate, one isolated failure, duplicate-free refresh, enabled preference before/after, and zero protocol errors. The installed screenshots are visual aspect evidence only; the full Fit assertions are current-source view evidence.

That baseline evidence is recorded in [release-qualification-1.0.50.md](release-qualification-1.0.50.md) and `artifacts/ui/release-qualification-1.0.50`. The final 2,011,595-byte package passed source build/self-tests, release-communication validation, Marketplace dry run, ordinary scripted reinstall, installed release-announcement automation, `AutomaticCollections`, and `MultiLibraryHybrid`. Results were: persisted Dismiss and What's New reopen with no image-list side effects; seven indexed collection rows with five opens/two isolated failures and duplicate-free rescan; nine hybrid documents/zero errors; exact one-command menu counts; zero protocol errors. Evidence is under `artifacts/ui/installed-vsix-new-features`. The affected external Windows 10 public-`1.0.49` in-place update remains Pending.

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
2. Verify the installed version and run `Test-VisualStudioMarketplaceUpdate.ps1`.
3. Inspect `ActivityLog.xml`, `package.log`, and the installed `.pkgdef`.
4. Confirm the `.pkgdef` points to `RawBufferVisualizer.VisualStudio.Extensibility.dll` and run an installed-VSIX handoff smoke.
5. Use `Repair-VisualStudioExtensionRegistration.ps1` only for a stale developer/legacy profile; never convert that recovery into release evidence.

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

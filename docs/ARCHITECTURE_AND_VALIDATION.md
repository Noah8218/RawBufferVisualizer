# Architecture And Validation

This owner map describes the 2.1.0 source line. Source changes affecting ownership must update this map; exact test execution belongs in the result for the tested source snapshot.

## Runtime Routes

Registered variable -> VisualStudio.Debugger result provider routes supported image values/collections -> VisualStudio.Classic transfer entry -> VisualStudio.ObjectSource metadata/chunks or live-memory descriptor -> VisualStudio handoff/session admission -> Vssdk docked ToolWindow -> Core image source and canvas. The Extensibility providers remain registered; the result provider chooses the docked entry for supported images and preserves other visualizers for non-image values.

Automatic inspection -> package debugger-mode events -> AutomaticVisionInspector discovery/extraction -> the same docked image/source path. Mode notification reaches the status model even when automatic inspection is disabled.

Standalone file -> ViewerFiles/ViewerViewModel -> ViewerDocument -> Core source -> ViewerCanvas. Shared.Wpf provides linked presentation components, including HistogramViewModel; it is not a separate executable project.

## Owners And Reading Order

| Responsibility | Owner / shortest reading route | State and lifetime |
| --- | --- | --- |
| Extension composition | `VisualStudio.Vssdk/RawBufferVisualizerPackage.cs` -> `RawBufferToolWindow.cs` -> `RawBufferToolWindowControl.xaml.cs` | Package owns debugger subscriptions; window/control own presentation lifecycle |
| Registered providers | `VisualStudio.Extensibility/DebuggerVisualizerLaunch.cs` -> `VisualStudio.ObjectSource` | Out-of-process provider hands data to the in-process VSSDK package |
| Image-only debugger routing | `VisualStudio.Debugger/ImageVisualizerResultProvider.cs` -> `ImageVisualizerTypePolicy.cs` -> `VisualStudio.Classic/RawBufferClassicDebuggerVisualizer.cs` or `ImageCollectionClassicDebuggerVisualizer.cs` | Concord preserves unrelated candidates; Classic owns request publication and never opens a helper dialog; Vssdk owns handoff admission and document lifetime |
| Transfer validation | `VisualStudio.ObjectSource/VisualizerChunkedTransfer.cs`, `CurrentProcessMemoryReader.cs`; `Core/RawBufferDiagnostics.cs` | Layout validation precedes transfer; native reads must complete fully |
| Session and document admission | `VisualStudio/ClaimedHandoffOpenCoordinator.cs`, `RawBufferDocumentWorkspace.cs` | Own admission and workspace lifetime; delayed work cannot reopen an obsolete session |
| Automatic scan | `VisualStudio.Vssdk/AutomaticVisionInspector.cs` | Owns bounded discovery/cache behavior; caller coordinates cancellation and debugger state |
| Mode/status presentation | `VisualStudio.Vssdk/AutomaticInspectionStatusViewModel.cs` | Owns frame/mode/detail/busy/image status; control wires scoped XAML bindings and its lifetime |
| Pixel semantics | `Core/BitmapPixelConverter.cs`, `RawBufferRenderer.cs`, `RawPixelInspector.cs`, `RawPixelStatistics.cs`, `RawPixelHistogram.cs` | Core calculations; no IDE ownership |
| Docked pixel measurement | `Shared.Wpf/PixelMeasurementViewModel.cs` -> scoped toolbar/status bindings in `VisualStudio.Vssdk/RawBufferToolWindowControl.xaml` -> `OpenGlCanvas/RawOpenGlImageCanvas.cs` overlay | ViewModel owns two pixel endpoints, mode, bounds and commands; control supplies image lifecycle and translates pixel events to commands; canvas owns display coordinates and line rendering |
| Histogram presentation | `Shared.Wpf/HistogramViewModel.cs` | Owns accepted generation, background cancellation and source leases |
| Type mappings | `VisualStudio.ObjectSource/TypeMappingStore.cs`, `TypeMappingEditSession.cs`; `VisualStudio.Vssdk/TypeMappingSaveViewModel.cs` | Store owns durable mapping writes; edit/save models retain draft and conflict state |
| Snapshot saving | `Sdk/RawBufferSnapshot.cs`, `AtomicFileSave.cs`, `SnapshotExportOperation.cs` | Export owns progress/cancellation; atomic save preserves the prior destination on failure |
| Standalone UI | `Wpf/MainWindow.xaml`, `ViewerViewModel.cs`, `ViewerDocument.cs`, `ViewerFiles.cs`, `ViewerDialogHost.cs` | ViewModel owns document/command state; host owns dialogs and view lifetime |

Paths in this table are relative to `src/` and use the `RawBufferVisualizer.` prefix on project folders (except `Shared.Wpf`). Existing XAML binding names and command contracts are compatibility surfaces, not an invitation to restructure these owners.

## Project And Startup Map

Open the solution through the [Start Here route](README.md#start-here). Extensibility's existing launchSettings profile starts the experimental IDE. VisualizerDebuggee and Samples are console applications; Wpf is the standalone GUI. Tests and LegacyCompatibility are console test runners. Core, Sdk, adapters, ObjectSource and VisualStudio are libraries. Classic is the dialog-free in-process transfer bridge selected by VisualStudio.Debugger; it is not a second viewer window.

The `.sln`, each `.csproj` and their `ProjectReference` entries are authoritative for the project graph and target frameworks. Vssdk packages the Classic/Debugger routing assemblies and Concord registration, and references the out-of-process Extensibility project with `ReferenceOutputAssembly=false` for its deployment output. The debugger payload has a separate netstandard2.0 build whose bytes must match the VSIX.

## Focused Verification

Build once using [development prerequisites](development-prerequisites.md), then invoke the resulting `RawBufferVisualizer.Tests.dll` with the matching switch:

| Switch | Scope |
| --- | --- |
| `--usability` | Status/mode, standalone state and error/recovery contracts |
| `--histogram` | Histogram plus pixel-correctness tests |
| `--automatic-scan` | Automatic preference/collection and inference regressions |
| `--mapping-save` | Mapping/save and related snapshot/transfer regressions |
| `--pixel-correctness` | Pixel conversion and renderer/source regressions |
| `--pixel-measurement` | Two-point coordinate distances, image bounds and measurement command/reset transitions |

Use the group required by the change. With no argument, the runner executes the aggregate suite; CI uses that route. `scripts/SmokeAutomaticScanResponsiveness.ps1 -SkipWindow` exercises scanner scheduling without its desktop window. UI smoke scripts require matching built binaries and host prerequisites; they do not replace interaction with the installed extension.

Run `scripts/Test-DockedPreviewOrder.ps1` in Windows PowerShell with `-STA`, supplying `-BuildRoot`, `-VisualStudioRoot` and `-OutputRoot`. It checks preview/full admission, late-preview acknowledgement and cleanup, and independent image identities through the built docked control without launching a desktop window. Use a D-drive output root on this workstation; this component check does not qualify an installed IDE.

`Test-ReleaseCommunication.ps1` checks source communication coherence. `Publish-VisualStudioExtension.ps1` builds/checks the local package and compares payload hashes; it does not publish to Marketplace. Installation and external publishing are separate operations.

Installed-IDE qualification must identify the installed payload hashes and host. Component tests and historical screenshots do not qualify different binaries.

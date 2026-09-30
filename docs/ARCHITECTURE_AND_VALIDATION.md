# Architecture And Validation

This owner map describes the 2.2.1 source line. Source changes affecting ownership must update this map; exact test execution belongs in the result for the tested source snapshot.

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

## Classic ObjectSource Loading Contract

The 2.2.0 loading defect registered the extension-owned Classic candidate as `DkmClrCustomVisualizerAssemblyLocation.Debuggee`. On the inspected .NET Framework route, Visual Studio then omitted the extension's ObjectSource probe base and searched its global `Common7/Packages/Debugger/Visualizers` directory. Having ObjectSource inside the VSIX did not make that folder discoverable. UI-side `ResolveAssembly` is a separate callback and does not fix the debuggee-side probe base.

The installation-path lookup also requires the full Classic assembly identity. VS registers keys as `UI type + "-" + Assembly.FullName`, including version, culture and public key token. A candidate using only `RawBufferVisualizer.VisualStudio.Classic` throws `KeyNotFoundException` in `GetInstallPathForDotnetCustomVisualizer` and displays the generic custom-viewer loading error. On 2026-09-30, source 2.2.0's failure was reproduced through the inspected VS2022 17.14.37614.0 registration cache: both full IDs resolved the installed extension, and both short IDs failed. Do not qualify other host versions from this evidence.

Keep these parts together when changing debugger routing, Classic registration, SDK versions or the VSIX:

1. `ImageVisualizerResultProvider` creates both Classic candidates with `Extension` location and reads `AssemblyName.GetAssemblyName(...).FullName` from the adjacent installed Classic DLL, caching it for the process. UI type and full assembly identity must exactly match VS's registered IDs. Do not use a short name, infer the Classic version from another DLL, or hard-code a release version.
2. Source, generated and packaged manifests include exactly one `Microsoft.VisualStudio.DotnetCustomVisualizer` asset pointing to `RawBufferVisualizer.VisualStudio.Classic.dll`, in addition to the Concord `DebuggerEngineExtension` asset.
3. `ClassicVisualizerRegistration.cs` registers both UI classes with `DebuggerVisualizerAttribute`, targeting each class itself. These attributes provide installation-path registration; supported image/collection menus remain owned by the result provider. Do not remove the attributes to achieve zero static registrations or retarget them to `object`, broad collections or open generics. Unrelated candidates must retain their IDs and extension metadata.
4. Core, Sdk, ObjectSource and ObjectSource.deps.json under `netstandard2.0`, plus Classic/Debugger DLLs and `.vsdconfig`, must match the freshly routed build by SHA-256. `Publish-VisualStudioExtension.ps1` checks package freshness, candidate wiring, the compiled candidate's full assembly identity, manifest registration and both attributes in the compiled Classic DLL. CI and release workflows already invoke this script, so short candidate IDs or missing path registration fail their package check. Do not copy payloads into the IDE's global Visualizers folder or hard-code one PC's extension directory.

SDK XML can describe internal interfaces. Check accessibility with the project's actual package references and compiler before treating an API as an available extension point. In particular, this fix does not implement the internal `IDkmDotnetCustomVisualizerPathProvider` interface.

Run the loader regression on Windows PowerShell after the focused extension build and package check, using fresh output directories:

```powershell
dotnet build src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferVisualizer.VisualStudio.Vssdk.csproj -c Release -p:RawBufferVisualizerBuildRoot=D:/RbvChecks/build /nodeReuse:false
powershell.exe -NoProfile -File scripts/Publish-VisualStudioExtension.ps1 -NoBuild -NoZip -BuildRoot D:/RbvChecks/build -PublishRoot D:/RbvChecks/package
powershell.exe -NoProfile -File scripts/Test-VisualizerAssemblyPath.ps1 -VsixPath D:/RbvChecks/package/RawBufferVisualizer-VisualStudioExtensibility-net472/RawBufferVisualizer.VisualStudio.Extensibility.vsix -VisualStudioRoot 'C:/Program Files/Microsoft Visual Studio/2022/Professional' -OutputRoot D:/RbvChecks/loader
powershell.exe -NoProfile -File scripts/Test-HeadlessSnapshotVisualizer.ps1 -VisualStudioRoot 'C:/Program Files/Microsoft Visual Studio/2022/Professional' -OutputRoot D:/RbvChecks/classic
```

The loader regression extracts the checked VSIX, uses the selected VS installation's real `AttributeDecoder` and metadata resolver to decode both registration IDs, and compares them with the compiled candidate identity and location. Its isolated directory classification avoids initializing native Concord; it does not claim to test native extension discovery. Separate .NET Framework x64 processes use the real UI `AssemblyResolver` to load Classic by full identity and construct both entries with the JSON formatter, then call the real debuggee loader. It covers the old global-only failure, both Classic entries with short/qualified ObjectSource names, and missing payloads. Classic is not preloaded in UI loader cases; ObjectSource is not preloaded in debuggee loader cases. The test records host/payload hashes, metadata and assertion counts; it does not install a VSIX, operate the IDE UI or qualify a different host version. A hand-built map of short names cannot validate VS registration. The Classic tests exercise actual ObjectSource/Show code through fake provider transport. Full installed-IDE qualification still requires opening initialized single-image and collection variables with the tested VSIX installed.

After an explicitly authorized installation, pass `-InstalledExtensionPath '<actual extension directory>'` to the same loader script. It compares eight installed payloads against the checked VSIX and probes the real installed directory. To check the registry used by the IDE after debugging starts, also pass `-VisualizerAttributeCachePath '<Documents>/Visual Studio 2022/Visualizers/attribcache140.bin'`. The script copies that cache to its isolated output, calls VS's real `LocalAttributeManager.LoadFromStream` and `GetInstallPathForDotnetCustomVisualizer`, requires both full IDs to resolve the selected installation, and requires both old short IDs to reproduce `KeyNotFoundException`. A missing or stale cache must be reported; do not modify or delete it to obtain a pass. Only the isolated output is written. These checks still do not exercise Concord dispatch or UI opening inside the running IDE.

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

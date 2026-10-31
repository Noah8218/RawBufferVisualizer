# Architecture And Validation

This owner map describes the current source tree. Source changes affecting ownership must update this map; exact test execution belongs in the result for the tested source snapshot.

The cache owner and Inspector focus check below were updated for source 2.2.3.0 on 2026-10-31; installed-IDE qualification remains specific to the tested binary and host.

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
| ObjectSource view reuse | `VisualStudio.ObjectSource/VisualizerViewCache.cs` and the existing image ObjectSources | Reference identity selects the cached target/view pair; a replacement is published only after its factory succeeds |
| Session and document admission | `VisualStudio/ClaimedHandoffOpenCoordinator.cs`, `RawBufferDocumentWorkspace.cs` | Own admission and workspace lifetime; delayed work cannot reopen an obsolete session |
| Automatic scan | `VisualStudio.Vssdk/AutomaticVisionInspector.cs` | Owns bounded discovery/cache behavior; caller coordinates cancellation and debugger state |
| Mode/status presentation | `VisualStudio.Vssdk/AutomaticInspectionStatusViewModel.cs` | Owns frame/mode/detail/busy/image status; control wires scoped XAML bindings and its lifetime |
| Pixel semantics | `Core/BitmapPixelConverter.cs`, `RawBufferRenderer.cs`, `RawPixelInspector.cs`, `RawPixelStatistics.cs`, `RawPixelHistogram.cs` | Core calculations; no IDE ownership |
| Docked pixel measurement | `Shared.Wpf/PixelMeasurementViewModel.cs` -> scoped viewer-toolbar/pixel-bar bindings in `VisualStudio.Vssdk/RawBufferToolWindowControl.xaml` -> `OpenGlCanvas/RawOpenGlImageCanvas.cs` overlay | ViewModel owns two pixel endpoints, mode, bounds and commands; control supplies image lifecycle and translates pixel events to commands; canvas owns display coordinates and line rendering |
| Docked inspector navigation | `VisualStudio.Vssdk/RawBufferToolWindowControl.xaml` -> `InspectorNavigationViewModel.cs` -> control `ApplyResponsiveLayout` | Model owns section, separate docked/overlay open state and image-strip choice; the control persists only open-state changes through `AutomaticInspectionPreferencesStore`. Missing fields retain the docked-open/overlay-closed defaults. Section and image-strip choices remain session-local; the control owns placement and closes popups on unload/disposal |
| Docked pixel presentation | control pixel-event adapters -> `VisualStudio.Vssdk/PixelInspectorViewModel.cs` -> Pixel-section bindings | Model owns channel selection and bounded neighborhood/profile presentation snapshots; Core remains the pixel/statistics owner, canvas retains selection/pin coordinates, and the control releases its source/document lifetime |
| Docked Buffer Doctor preview | `VisualStudio.Vssdk/BufferDiagnosisViewModel.cs` -> `BindDiagnosis` / `ApplyDiagnosisDescriptor` in control -> existing interpretation/render adapter | Per-document model owns candidate/result/selection and committed/preview descriptors; fresh declared-source identity clears stale state. Scoped DiagnosisPanel bindings route commands and selection. Control retains Core worker/cancellation and active subscriptions; workspace disposes documents and model state. Read model -> binding/result adapter -> `InspectorActionTests` / `Test-DockedLayout` |
| Docked image-card actions | `VisualStudio.Vssdk/ImageCardActionsViewModel.cs` -> scoped inline popup -> existing document/clipboard/save result adapters | Model owns borrowed target, open state and parameter-aware command availability. Opening and A/B assignment preserve active selection; save routes through existing activation/export handlers. Control releases subscription/target on shutdown; workspace owns document/source lifetime. Read `ImageCardActionsViewModel`, `ImageCardAction_Requested`, then `InspectorActionTests` |
| Docked layout actions | `VisualStudio.Vssdk/BufferLayoutViewModel.cs` -> existing interpretation result adapter in the control; `CompareSelectionViewModel.cs` -> Compare bindings | Form owns edit/reset/validation state and descriptor results; compare model owns borrowed A/B references and Swap. Control supplies scoped DataContexts and releases subscriptions; workspace retains source/document lifetime |
| Histogram presentation | `Shared.Wpf/HistogramViewModel.cs` | Owns accepted generation, background cancellation and source leases |
| Type mappings | `VisualStudio.ObjectSource/TypeMappingStore.cs`, `TypeMappingEditSession.cs`; `VisualStudio.Vssdk/TypeMappingSaveViewModel.cs` | Store owns durable mapping writes; edit/save models retain draft and conflict state |
| Snapshot saving | `Sdk/RawBufferSnapshot.cs`, `AtomicFileSave.cs`, `SnapshotExportOperation.cs` | Export owns progress/cancellation; atomic save preserves the prior destination on failure |
| Standalone UI | `Wpf/MainWindow.xaml`, `ViewerViewModel.cs`, `ViewerDocument.cs`, `ViewerFiles.cs`, `ViewerDialogHost.cs` | ViewModel owns document/command state; host owns dialogs and view lifetime |

Paths in this table are relative to `src/` and use the `RawBufferVisualizer.` prefix on project folders (except `Shared.Wpf`). Existing XAML binding names and command contracts are compatibility surfaces, not an invitation to restructure these owners.

## Docked Presentation Contracts

The control constructor supplies navigation DataContexts to the rail, inspector container and image-list surfaces, and supplies the pixel presentation model to `PixelVisualization`. The existing automatic-status, measurement and histogram contexts remain scoped to their controls. Read the XAML, the two presentation models, and then the control pixel-event/layout methods; `docs/README.md` remains the Start Here route.

Rail selection is a one-way display binding; the rail toggle command and collapse command change navigation state. Display binding teardown, initial restore and responsive placement must not become persisted user choices. Channel selection writes only the pixel presentation model; its source input is a bounded snapshot through existing Core APIs and is not retained. Pixel hover, selection and pin coordinates still originate in the canvas. Source leases, document admission, saving and measurement commands keep their existing owners. View-specific size, scrolling, shortcut, theme, popup and reparenting handlers remain in the control; its `Dispose` releases subscriptions and closes popups.

`ImageObjectCaptionConverter` is a stateless, View-owned resource used by the header, image cards and comparison slots. Its one-way `ObjectName`/`IsAutomaticInspection` bindings remove the automatic status prefix only from displayed captions. They never write the debugger expression or document identity. Follow the XAML resource to the converter and `Test-DockedLayout.ps1` for rendered caption and identity checks.

`InspectorNavigationTests` and `PixelInspectorTests` exercise these presentation owners through `--docked-layout`. From the repository root, `--inspector-navigation-intent` runs only the production rail bindings' passive-reset, command and teardown/recreation checks on unshown WPF controls. `Test-DockedLayout.ps1` arranges the built control and its popup content without showing a window, checks channel bounds and all five sections, and records bitmap/JSON evidence. These checks do not verify native popup interaction, pointer states, Visual Studio image services or Windows DPI. Installed-IDE and native UI checks remain separate.

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
| `--docked-layout` | Inspector section/resize state and pixel neighborhood, channel, statistics and profile presentation |
| `--pixel-measurement` | Two-point coordinate distances, image bounds and measurement command/reset transitions |

Use the group required by the change. With no argument, the runner executes the aggregate suite; CI uses that route. `scripts/SmokeAutomaticScanResponsiveness.ps1 -SkipWindow` exercises scanner scheduling without its desktop window. UI smoke scripts require matching built binaries and host prerequisites; they do not replace interaction with the installed extension.

`scripts/Test-InspectorFocusObservation.ps1 -OutputPath <isolated-results.json>` runs in PowerShell Core and checks the actual Inspector popup-open handler with a task-backed queue double. It covers deferred focus, closed/disposed state, and observed failure/cancellation without creating a window. It does not qualify WPF focus behavior or an installed IDE.

Run `scripts/Test-DockedPreviewOrder.ps1` in Windows PowerShell with `-STA`, supplying `-BuildRoot`, `-VisualStudioRoot` and `-OutputRoot`. It checks preview/full admission, late-preview acknowledgement and cleanup, and independent image identities through the built docked control without launching a desktop window. Use a D-drive output root on this workstation; this component check does not qualify an installed IDE.

Run `scripts/Test-DockedLayout.ps1` in Windows PowerShell with `-STA`, using the same `-BuildRoot`, `-VisualStudioRoot` and a separate D-drive `-OutputRoot`. Its default matrix covers 320/640/759/760/800/1000/1440 by 440/720, including the overlay threshold and color/scalar values. Desktop scripts such as `SmokeDockedLayoutWidths.ps1` require an explicitly allowed interactive run and matching binaries.

`Test-ReleaseCommunication.ps1` checks source communication coherence. `Publish-VisualStudioExtension.ps1` builds/checks the local package and compares payload hashes; it does not publish to Marketplace. Installation and external publishing are separate operations.

Installed-IDE qualification must identify the installed payload hashes and host. Component tests and historical screenshots do not qualify different binaries.

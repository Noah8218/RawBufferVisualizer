# Raw Buffer Visualizer 1.0.47 Local Release Qualification

Status: Complete

Scope: Source, Release build, package metadata, local VSIX reinstall, registered debugger visualizers, Automatic Vision Inspector, Smart Type Mapper fallback, Vision Buffer Doctor, preference persistence, compatibility fixtures, and public release copy for `1.0.47.0`.

Qualified environment:

- Date: 2026-07-28 KST
- Visual Studio: Community 2022 `17.14.37314.3`
- Instance: `2c8402d8`
- Repository: `C:\Git\RawBufferVisualizer`
- Branch: `main`

## Acceptance Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Source and package versions agree at `1.0.47.0` | Pass | Classic/Extensibility projects, source manifest, installed manifest |
| Release solution builds | Pass | 0 errors; 18 known `VSTHRD010` warnings in `ImageTypeRecognizer.cs` |
| Deterministic self-tests pass | Pass | `RawBufferVisualizer.Tests` Release run |
| Current and legacy Bitmap/Mat adapters remain compatible | Pass | Five OpenCvSharp versions, five Emgu CV versions, and `System.Drawing.Bitmap` |
| Registered types do not become duplicate Automatic Inspector candidates | Pass | Real OpenCvSharp, Emgu CV, Bitmap, `RawBufferSnapshot`, and `RawBufferView` were excluded from automatic rows |
| Unregistered current-frame image shapes open independently | Pass | Six automatic images opened; one mapping candidate and one invalid-pointer failure remained isolated in the focused scenario |
| Smart Type Mapper fallback completes a correction cycle | Pass | 88% candidate -> `Mono12PackedLsb` preview -> Save -> automatic 640 x 484, stride 960 reopen |
| Buffer Doctor repairs the prepared wrong-stride image | Pass | Five candidates; selected correction produced expected `GV 204` |
| Auto Inspect preference survives a Visual Studio restart | Pass | Disabled state restored in a second VS session; manual Scan Now remained available; original user file restored |
| Final generated VSIX reinstalls and loads in a new VS session | Pass | Final package reinstall plus `MultiLibraryHybrid` installed-VSIX smoke |
| README, Marketplace Overview, release notes, and public images cover the two 1.0.47 workflows | Pass | Root README and `docs/marketplace-*.md`; both current-build feature images visually reviewed |

## Verification

The following checks were run during the 1.0.47 qualification:

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
dotnet build .\samples\RawBufferVisualizer.VisualizerDebuggee\RawBufferVisualizer.VisualizerDebuggee.csproj -c Debug
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLegacyImageCompatibility.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Test-IndustrialCameraSdkContracts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario BufferDoctor -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -VisualStudioInstanceId 2c8402d8 -NoBuild -Reinstall
```

The full feature scenarios ran against the same `1.0.47` binaries before the final Marketplace-description-only package rebuild. The final artifact was then reinstalled and the combined registered/automatic `MultiLibraryHybrid` scenario passed again in a new Visual Studio session.

## Evidence

- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/BufferDoctor-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/MultiLibraryHybrid-installed-vsix.json`
- `artifacts/ui/automatic-inspector-workflow/2026-07-28-final/`
- `artifacts/ui/inspector-button-visibility/2026-07-28/before/`
- `artifacts/validation/legacy-image-compatibility-20260728.stdout.log`
- `artifacts/validation/industrial-camera-sdk-contracts-20260728.json`

Final artifact:

```text
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 1.0.47.0
Size: 1,924,125 bytes
SHA256: DAB2CE62007F77F11CFF828AF02EF2F2DAE26A3BB3238CF251679E4A69505174
```

## Responsive Inspector Finding

The top `Inspector` button is intentionally conditional:

- below 760 px: the top button is visible and toggles the collapsed Inspector;
- 760-1039 px: the top button is hidden because the compact bottom Inspector is visible;
- 1040 px and wider: the top button is hidden because the full right-side Inspector is visible.

Fresh 540/900/1160 px captures passed. This explains the reported “button appears and disappears” behavior; it is responsive layout switching, not intermittent command registration. A future change to keep a consistent affordance across widths is a UI/UX decision and still requires the repository's mockup-and-approval gate.

Boundary / next dependency: This qualification does not publish `1.0.47` to Marketplace, prove update behavior on a separate PC, or certify live Basler pylon, FLIR Spinnaker, Allied Vision Vimba X, or camera hardware. IDS peak ICV `1.4.0` assembly metadata passed, but acquisition, buffer lifetime, driver, and hardware behavior remain unverified.

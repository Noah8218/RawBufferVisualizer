# Release Qualification: 1.0.48

## Scope

This hotfix qualifies the hybrid VSIX packaging and runtime handoff path changed after the `1.0.47.0` clean-PC failure. Image interpretation and UI behavior were not changed.

## Candidate

```text
Version: 1.0.48.0
Path: C:\Git\RawBufferVisualizer\.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Release\net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,990,304 bytes
SHA-256: AABBD3A36780AE070C3FBBDE384CB5CD9A1977607EA929D15DAEBF75899F717D
Built: 2026-07-28 KST
```

The copy under `artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472` has the same SHA-256.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Hybrid project owns package registration | Pass | Generated `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef` |
| Package CodeBase is installed hybrid DLL | Pass | `$PackageFolder$\RawBufferVisualizer.VisualStudio.Extensibility.dll` |
| Former split-project pkgdef excluded | Pass | `Publish-VisualStudioExtension.ps1` guard and VSIX entry inspection |
| Normal install does not write registry | Pass | Manual `Register-VssdkToolWindow` path removed from `Install-VisualStudioExtension.ps1` |
| Release solution build | Pass | 0 errors; 18 existing `VSTHRD010` warnings |
| Core/self-test suite | Pass | `RawBufferVisualizer self-tests passed.` |
| Installed Automatic Vision Inspector | Pass | 6 opened, 1 mapping candidate, 1 isolated failure, no duplicates |
| Installed Buffer Doctor | Pass | 5 candidates; corrected result reported `GV 204` |
| Installed Smart Type Mapper | Pass | live preview, saved mapping, reopened 640 x 484 Mono12PackedLsb, 0 final errors |
| Installed registered/automatic hybrid | Pass | 9 documents, OpenCvSharp/Emgu/Bitmap plus automatic wrappers, 0 errors |
| Runtime handoff acknowledgement | Pass | `package.log` records `Open start` and `Open end` for installed VSIX requests |

Commands run:

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore /nodeReuse:false
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoZip
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -VisualStudioInstanceId 2c8402d8 -NoBuild -Reinstall
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.48.0
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario BufferDoctor -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
```

Result JSON:

```text
artifacts\ui\installed-vsix-new-features\AutomaticVisionInspector-installed-vsix.json
artifacts\ui\installed-vsix-new-features\BufferDoctor-installed-vsix.json
artifacts\ui\installed-vsix-new-features\SmartTypeMapper-installed-vsix.json
artifacts\ui\installed-vsix-new-features\MultiLibraryHybrid-installed-vsix.json
```

## Boundary

This proves the rebuilt `1.0.48.0` VSIX on the maintainer's Visual Studio 2022 `17.14.37314.3` profile after removal of the old manual developer registration. It does not replace a clean-PC Marketplace install/update check.

The Marketplace upload gate remains:

1. install the exact SHA-256 candidate on the separate PC that exposed the `1.0.47` failure;
2. restart Visual Studio;
3. run the payload check and one real debugger handoff;
4. confirm no handoff acknowledgement error;
5. only then upload or keep `1.0.48` public.

```text
Status: Complete
Scope: Local 1.0.48 packaging, install, restart, and installed-VSIX runtime qualification
Acceptance criteria: All local criteria above passed
Verification: Release build, self-tests, packaging guard, reinstall, restart, four installed-VSIX scenarios
Evidence: Candidate SHA-256, generated pkgdef, result JSON files, package.log
Boundary / next dependency: Separate clean VS2022 PC validation and Marketplace upload remain external
```

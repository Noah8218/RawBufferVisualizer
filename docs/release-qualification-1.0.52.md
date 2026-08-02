# Raw Buffer Visualizer 1.0.52 Release Qualification

> Current-state record: 2026-08-02 KST. This record covers the exact local `1.0.52.0` candidate below. Local packaging and installed runtime qualification passed on serviced VS2022 and stable VS2026. Marketplace publication is blocked until a separate PC proves an in-place update from the exact public `1.0.50.0` package to this unchanged candidate.

## Scope

Included:

- stable VS2026 registered debugger-visualizer activation through the `17.14` Extensibility SDK line;
- an honest Visual Studio 2022 `17.14+` support floor and manifest range `[17.14,18.0)`;
- optional bounded OpenCvSharp/Emgu Mat collection inspection and `1.0.52` release highlights;
- per-document snapshot leases and preview-to-full snapshot-directory cleanup;
- `RawBufferDocumentWorkspace` ownership of document activation/removal/disposal;
- `ClaimedHandoffOpenCoordinator` ownership of claimed-request read retry and ACK/NACK terminal policy;
- exact candidate packaging, VS2022/VS2026 installation, registration, and three-scenario runtime qualification;
- Marketplace overview/release-note preparation and a no-write dry run.

Excluded:

- Marketplace upload or propagation;
- a separate-PC update from the public `1.0.50.0` package;
- tag, GitHub Release, or Marketplace publication;
- vendor camera SDK or hardware certification.

## Exact artifacts

### Public Marketplace baseline

```text
Version: 1.0.50.0
Published KST: 2026-07-29 13:09:54 +09:00
Size: 2,001,513 bytes
SHA-256: 2014AA8D679AF3D01F0B16CC304E77064ABCF0B0725BDC6BD543B7C08CDA397E
```

The Gallery API was rechecked on 2026-08-02 and still returned only `1.0.50.0`. The rendered public Overview was still titled `1.0.47`.

### Preserved failed candidate

```text
Version: 1.0.51.0
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 2,011,587 bytes
SHA-256: 7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F
```

This file remains byte-for-byte unchanged. On a freshly reinstalled VS2026 Community `18.8.2`, the package passed ReleaseAnnouncement and AutomaticCollections but its registered Bitmap provider failed activation because `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` was unavailable. It must not be published.

### Current candidate

```text
Version: 1.0.52.0
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,902,513 bytes
SHA-256: 3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F
Manifest target: [17.14,18.0) for Community/Professional/Enterprise x64
Source: commit 854cb67
```

Rebuilding or changing any packaged source invalidates this exact record.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Original `1.0.51` remains unchanged | Pass | exact length and SHA-256 above |
| VSIX/package/announcement/docs agree on `1.0.52` | Pass | `Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.52` |
| Package manifest and payload guard | Pass | version `1.0.52.0`; three `[17.14,18.0)` targets; extension metadata and VSSDK DLL present; obsolete Classic DLL absent |
| Release solution build | Pass | `dotnet build RawBufferVisualizer.sln -c Release -p:Platform="Any CPU" --no-restore`; 0 errors |
| Aggregate self-tests | Pass | workspace activation/removal/dispose, snapshot lease/replacement/dispose, handoff ACK/NACK/exception, and existing suites; `RawBufferVisualizer self-tests passed.` |
| VS2026 installed runtime | Pass | Community `18.8.2` (`18.8.12023.21`), instance `0adc3897`; ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid |
| VS2022 installed runtime | Pass | Community `17.14.33` (`17.14.37314.3`), instance `2c8402d8`; same three scenarios |
| Release announcement contract | Pass | title `1.0.52`, persisted Dismiss, What's New reopen, image rows `0 -> 0` on both IDE generations |
| Automatic Mat collections | Pass | seven rows, five opens, two isolated failures, duplicate-free rescan, preference restored on both IDE generations |
| Mixed registered/automatic runtime | Pass | nine documents, zero errors, eight detected/eight opened on both IDE generations; Bitmap registered provider activated |
| Menu and protocol contract | Pass | one Open and one Scan command per run; zero package protocol errors |
| Monitor placement | Pass | leftmost `\\.\DISPLAY2`, bounds `-1920,360,1920,1080`; verified window rectangle `-1900,380,-20,1420` |
| Installed registration audit | Pass | both profiles report `1.0.52.0`, valid registration payload, and no visible legacy config key |
| Marketplace dry run | Pass | no external write; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\marketplace\vs-publish.json` |
| Separate-PC public `1.0.50 -> 1.0.52` update | Blocked | requires another serviced VS2022 `17.14+` or stable VS2026 PC that still runs exact public `1.0.50`; no uninstall, repair, or `/ResetSkipPkgs` is allowed between versions |

## Commands run

```powershell
dotnet clean .\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj -c Release -f net472 /nodeReuse:false
powershell -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -PublishRoot D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate -NoZip
dotnet build .\RawBufferVisualizer.sln -c Release -p:Platform="Any CPU" --no-restore /nodeReuse:false
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.52
powershell -File .\scripts\Install-VisualStudioExtension.ps1 -VisualStudioInstanceId <instance> -VsixPath <exact-1.0.52-vsix> -NoBuild -NoViewerEnv -Reinstall
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario ReleaseAnnouncement -NoBuild -NoInstall -VisualStudioInstanceId <instance> -ExpectedReleaseVersion 1.0.52 -OutputRoot <D-drive-output>
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -NoBuild -NoInstall -VisualStudioInstanceId <instance> -ExpectedReleaseVersion 1.0.52 -OutputRoot <D-drive-output>
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -NoBuild -NoInstall -VisualStudioInstanceId <instance> -ExpectedReleaseVersion 1.0.52 -OutputRoot <D-drive-output>
powershell -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.52.0
powershell -File .\scripts\Publish-VisualStudioMarketplace.ps1 -Publisher openvisionlab -VsixPath <exact-1.0.52-vsix> -OverviewPath .\docs\marketplace-overview-1.0.52.md -PublishManifestPath <D-drive-output> -DryRun
git diff --check
```

Test-process `TEMP` and `TMP` were routed under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52` where practical.

## Windows reinstall preservation

The intended `1.0.52` source, documentation, and validation scripts are preserved from source checkpoint commit `854cb67` through the immediately following handoff checkpoint on `origin/main`. The exact candidate and runtime evidence under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52` are not Git content. Preserve that directory when reinstalling Windows, or copy it to external storage before any operation that may format or repartition `D:`. After copying, verify the candidate length and SHA-256 recorded above. Rebuilding a new VSIX is not a substitute for the unchanged package required by the remaining public-update gate.

## Evidence locations

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\candidate
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\vs2026-runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\vs2022-clean-runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\marketplace
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52\final-validation
```

## Durable closure

Status: Blocked
Scope: Local `1.0.52` implementation, packaging, structure/lease verification, VS2022/VS2026 installed-runtime qualification, and Marketplace metadata preparation
Acceptance criteria: Every local criterion passed; the required separate-PC public `1.0.50 -> 1.0.52` update criterion has no eligible external machine in this workspace
Verification: Exact artifact/hash inspection; Release build; aggregate self-tests; release communication; VS2022 and VS2026 installation plus three runtime scenarios; registration audit; Marketplace dry run; public Gallery query; `git diff --check`; source checkpoint commit `854cb67` and `origin/main` push verification
Evidence: Exact candidate SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F` and the D-drive evidence paths above
Boundary / next dependency: On a separate serviced VS2022 `17.14+` or stable VS2026 PC that currently has exact public `1.0.50.0`, update to this unchanged VSIX without uninstall, repair, or `/ResetSkipPkgs`, restart, and repeat the core runtime matrix. Only after that passes may Marketplace publication proceed with publisher credentials/approval.

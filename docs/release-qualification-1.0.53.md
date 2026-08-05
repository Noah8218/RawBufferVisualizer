# Raw Buffer Visualizer 1.0.53 Release Qualification

> Current-state record: 2026-08-05 KST. This document qualified the exact vendor-safe `1.0.53.0` candidate below before publication. Marketplace now serves the same 1,914,615-byte asset with SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`; Gallery metadata, manifest, download, and Overview readback evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018`.

## Scope

Included:

- essential-only Environment Check and consistent panel toggles;
- bounded sampled-preview, vendor-neutral Connect Your Buffer, Buffer Doctor, Automatic Vision Inspector, collections, Open Variable, and registered debugger-visualizer paths;
- absence of an active proprietary camera/frame-grabber adapter or support claim;
- exact packaging, VS2022/VS2026 installation, runtime behavior, registration, package equality, and Marketplace no-write preparation.

Excluded:

- Marketplace upload, Git commit/push, tag, or GitHub Release;
- direct proprietary SDK integration, camera acquisition/control, 3D, PLC, or I/O;
- physical camera/board certification;
- the separate-PC public `1.0.50 -> 1.0.52` migration debt.

## Exact candidate

```text
Version: 1.0.53.0
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate-vendor-safe\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,914,615 bytes
SHA-256: E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3
Manifest target: [17.14,18.0) for Community/Professional/Enterprise x64
Source: commit 7ab84b7
```

The candidate was frozen on 2026-08-04. Installed-runtime test-harness and documentation corrections made after the freeze are not packaged product files. Rebuilding or modifying the VSIX invalidates this exact record. Commit `7ab84b7` contains the matching packaged product source plus those non-package qualification corrections and is pushed to `origin/agent/basler-environment-1.0.53`. Owner-approved Marketplace publication and exact-asset readback completed on 2026-08-05 KST.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Release build and aggregate self-tests | Pass | full Release build: 0 errors and 18 existing `VSTHRD010` warnings; aggregate self-tests passed before candidate freeze |
| Official package guard | Pass | `Publish-VisualStudioExtension.ps1`; source/generated/package version checks and registration guards passed |
| Candidate identity remained immutable | Pass | 1,914,615 bytes and SHA-256 above before, during, and after installed qualification |
| Vendor-safe package boundary | Pass | no direct proprietary provider/ObjectSource/registration path; vendor-neutral padding/stride/offset/length safety coverage retained |
| Layout and memory regression | Pass | 540/900/1160 layout smoke; 240-cycle docked memory soak with 0 violations and 0 remaining temp directories |
| VS2022 installed runtime | Pass | Community `17.14.37516.0`, instance `f2675563`; nine scenarios below |
| VS2026 installed runtime | Pass | Community `18.8.12023.21`, instance `19923728`; six cross-generation scenarios below |
| Release announcement | Pass | `1.0.53` title, persisted Dismiss, repeated-button close/reopen, image rows `0 -> 0` on both IDEs |
| Environment Check | Pass | host/extension/temp `[Ready]`, version `1.0.53.0`, report copy, no FFmpeg/.NET SDK/VS workload rows, repeated-button close on both IDEs |
| Automatic Mat collections | Pass | seven rows, five opens, two isolated failures, duplicate-free rescan, preference restored on both IDEs |
| Registered/automatic hybrid | Pass | nine documents, zero errors, 8/8 automatic opens, Bitmap registered provider active on both IDEs |
| Connect Your Buffer save and restore | Pass | 88% candidate -> explicit Mono12 mapping -> live preview/save/reopen; fresh-process nine-role restore and neutral 630-character template on both IDEs |
| Buffer Doctor | Pass | five candidates, corrected descriptor apply, pixel read, VS2022 |
| Automatic Vision Inspector | Pass | six opens, one mapping candidate, one isolated failure, duplicate-free refresh and preference restore, VS2022 |
| Open Variable | Pass | unsupported collection error -> context menu -> variable selection/open workflow, VS2022 |
| Menu and protocol contract | Pass | one Open and one Scan command per installed run; zero package protocol errors |
| Monitor placement | Pass | leftmost `\\.\DISPLAY2`, bounds `-1920,365,1920,1080`; verified VS rectangle `-1900,385,-20,1425` |
| Installed package equality | Pass | five package-owned files × two IDEs; length and SHA-256 all equal to candidate payload |
| Installed registration audit | Pass | both profiles report `1.0.53.0`, valid registration payload, no visible legacy config key |
| Release communication and Environment contracts | Pass | `Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.53`; `Test-EnvironmentCheckContracts.ps1` |
| Marketplace dry run | Pass | exact candidate path; manifest generated on `D:`; no external write |
| Script/static checks | Pass | installed smoke parser and `git diff --check` |

## Installed runtime matrix

| Scenario | VS2022 | VS2026 |
| --- | --- | --- |
| ReleaseAnnouncement | Pass | Pass |
| EnvironmentCheck | Pass | Pass |
| AutomaticCollections | Pass | Pass |
| MultiLibraryHybrid | Pass | Pass |
| SmartTypeMapper | Pass | Pass |
| SmartTypeMapperPersisted | Pass | Pass |
| BufferDoctor | Pass | Not repeated; exact candidate passed on VS2022 |
| AutomaticVisionInspector | Pass | Not repeated; the cross-generation automatic/registered paths passed through collection and hybrid scenarios |
| OpenVariable | Pass | Not repeated; exact candidate passed on VS2022 |

Every run used `-NoBuild -NoInstall` after installation of the exact candidate. The test Debuggee was rebuilt separately in Debug configuration after a stale test executable was detected; this did not rebuild or alter the Release VSIX. User mapping and automatic-inspection preferences were restored after the scenarios.

## Commands and checks

```powershell
powershell -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -PublishRoot <unique-D-drive-candidate-root> -NoZip
powershell -File .\scripts\Install-VisualStudioExtension.ps1 -VisualStudioInstanceId <instance> -VsixPath <exact-1.0.53-vsix> -NoBuild -Reinstall
powershell -STA -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario <scenario> -NoBuild -NoInstall -VisualStudioInstanceId <instance> -ExpectedReleaseVersion 1.0.53 -OutputRoot <D-drive-output>
powershell -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.53.0
powershell -File .\scripts\Publish-VisualStudioMarketplace.ps1 -Publisher openvisionlab -VsixPath <exact-1.0.53-vsix> -OverviewPath .\docs\marketplace-overview-1.0.53.md -PublishManifestPath <D-drive-output> -DryRun
powershell -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.53
powershell -File .\scripts\Test-EnvironmentCheckContracts.ps1
git diff --check
```

## Evidence locations

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate-vendor-safe
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\vs2022-runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\vs2026-runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\package-audit-20260805\installed-package-equality.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace\vs-publish.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\final-validation\fit-stability\layout-widths.json
C:\Git\RawBufferVisualizer\.build\validation\release-1.0.53\memory-soak\docked-memory-soak.json
```

The `.build` path is a repository junction whose physical storage is under `D:\OpenVisionLab-TestData\RawBufferVisualizer`.

## Durable closure

Status: Complete
Scope: Exact local vendor-safe `1.0.53.0` candidate packaging, VS2022/VS2026 installed-runtime qualification, package equality, registration audit, and Marketplace no-write preparation
Acceptance criteria: All local criteria in this document passed for SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`
Verification: Release build/self-tests and package guards before freeze; installed scenarios above; ten installed-file comparisons; registration audit; release/environment contracts; Marketplace Dry Run; parser and diff checks
Evidence: Exact candidate and D-drive paths above
Boundary / next dependency: Matching source/evidence commit `7ab84b7` was pushed, and Marketplace publication/readback now proves that the public asset is this exact VSIX. This does not prove a physical camera/board path, authorize a proprietary SDK adapter, or qualify a future 2.0 package.

## Marketplace publication readback closure

Status: Complete
Scope: Existing Marketplace item `openvisionlab.RawBufferVisualizer` updated to exact public `1.0.53.0` with the approved English Overview
Acceptance criteria: Gallery version `1.0.53.0` -> pass; public VSIX size and SHA-256 equal the qualified candidate -> pass; downloaded manifest `1.0.53.0` -> pass; rendered Overview equals the local English source after the Gallery's normal H1-marker removal -> pass
Verification: Gallery query; public VSIX download and SHA-256; downloaded manifest inspection; Overview asset comparison; fresh cache-busted public-page check
Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018`
Boundary / next dependency: This proves publication identity and copy propagation. It does not qualify a new package, a physical camera/board, or a proprietary SDK integration.

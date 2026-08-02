# Raw Buffer Visualizer 1.0.51 Release Qualification

> Final-state record: 2026-08-02 KST. This document records the exact local `1.0.51.0` candidate described below. A user-approved full VS2026 uninstall/reinstall removed the orphaned `1.0.6.0` registration and allowed the unchanged package to install on stable VS2026 `18.8.2`. Runtime qualification then failed because the registered Bitmap provider could not load the VS2026 host-contract assembly. `1.0.51` is preserved as failed evidence and is superseded by `1.0.52`; it must not be published.

## Scope

`1.0.51` moves the automatic Mat collection and in-product release-highlights work built after the public `1.0.50.0` package into a higher version that existing Marketplace users can receive as an update. It carries forward the public `1.0.50` direct-Mat, atomic handoff, menu-registration, and Fit/Manual reliability baseline.

Included:

- VSIX/project/package/announcement version alignment at `1.0.51` / `1.0.51.0`;
- optional bounded exact OpenCvSharp/Emgu Mat `List<T>` and one-dimensional array inspection;
- per-element success/failure isolation and duplicate-free rescan;
- one-time `1.0.51` release highlights with persisted Dismiss and manual What's New reopen;
- Marketplace Overview, Marketplace/GitHub notes, embedded VSIX notes, README, CHANGELOG, checklist, and runbook alignment;
- Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x` support declaration plus exact installed-VSIX qualification on the available stable instances;
- exact public `1.0.50.0` to exact candidate `1.0.51.0` local in-place update without repair or `/ResetSkipPkgs`;
- current-source Fit/Manual width matrix.

Excluded:

- actual Marketplace publish/propagation;
- a second external Windows 10/11 PC update;
- real vendor camera SDK or hardware certification.

## Exact artifacts

### Public Marketplace baseline

```text
Version: 1.0.50.0
Published KST: 2026-07-29 13:09:54 +09:00
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-audit-20260801\RawBufferVisualizer-marketplace-1.0.50.0.vsix
Size: 2,001,513 bytes
SHA-256: 2014AA8D679AF3D01F0B16CC304E77064ABCF0B0725BDC6BD543B7C08CDA397E
```

### Candidate

```text
Version: 1.0.51.0
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 2,011,587 bytes
SHA-256: 7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F
Source: commit 43e347c plus the current 1.0.51 version/document/test-infrastructure working tree
```

Rebuilding changes the candidate hash and invalidates this exact record.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Binary and user-facing version sources agree on `1.0.51` | Pass | `Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.51` |
| Full Release solution build | Pass | 0 errors; existing 18 `VSTHRD010` warnings |
| Aggregate self-tests | Pass | `RawBufferVisualizer self-tests passed.` |
| Upload package build and registration guard | Pass | final package build 0 warnings/0 errors; one `.pkgdef`; expected package GUID and `Menus.ctmenu, 2` checked by publish script |
| Marketplace publish metadata without external write | Pass | dry run manifest at `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\marketplace\vs-publish.json` |
| Exact public `1.0.50` installed as update baseline | Pass | VS2022 instance `2c8402d8`; payload verification at `1.0.50.0` |
| Public `1.0.50` -> candidate `1.0.51` without uninstall/repair between versions | Pass | installed manifest `1.0.51.0`; registration payload valid |
| First Tool Window open exposes `New in Raw Buffer Visualizer 1.0.51` | Pass | `first-start-repro2\release-announcement-installed.png` |
| Dismiss persists and What's New reopens without inspection | Pass | image rows `0 -> 0`; persisted `lastSeenVersion: 1.0.51` |
| View menu contract | Pass | exactly one Open and one Scan command on the first process in reproduction 2 |
| Automatic Mat collections | Pass | seven rows, five opened, two isolated failures, duplicate-free repeated scan, preference restored, 141.37 ms |
| Mixed registered/automatic runtime | Pass | nine documents, zero errors, eight detected/eight opened |
| Package protocol diagnostics | Pass | zero protocol/settings errors in passing installed runs |
| Leftmost monitor placement | Pass | `\\.\DISPLAY2`, bounds `-1920,360,1920,1080`; verified VS rectangle `-1900,380,1880,1040` |
| Fit/Manual layout matrix | Pass | widths 540/900/1160; `fit-stability\layout-widths.json` |
| Exact stable VS2026 installed runtime | Fail | After a full VS2026 reinstall, unchanged `1.0.51` installed on Community `18.8.2` (`18.8.12023.21`). ReleaseAnnouncement and AutomaticCollections passed, but MultiLibraryHybrid failed when `BitmapDebuggerVisualizerProvider` could not load `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` |

## Visual Studio support declaration

- Supported target: Visual Studio 2022 `17.9+` and stable Visual Studio 2026 `18.x`, Community/Professional/Enterprise x64.
- Recommended VS2022 baseline: current serviced `17.14`; exact candidate evidence is Community `17.14.33` (`17.14.37314.3`).
- VS2026 compatibility basis: Microsoft documents that VS2026 supports Visual Studio API version 17.x and evaluates the lower bound while ignoring the existing VSIX upper bound. The manifest therefore remains `[17.9,18.0)` and the exact candidate VSIX is not rebuilt.
- Final VS2026 qualification target: Community `18.8.2` (`18.8.12023.21`), stable channel, instance `0adc3897`. A user-approved full uninstall/reinstall cleared the historical per-machine extension while preserving the same 55-component workload configuration. The unchanged candidate then installed, but its registered provider activation failed on the host-contract mismatch above.
- Not supported as release targets: Visual Studio 2019, 32-bit Visual Studio, and Preview/Insiders builds.

## Commands run

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows --no-build
powershell -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.51
powershell -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoZip
powershell -File .\scripts\Publish-VisualStudioMarketplace.ps1 -Publisher openvisionlab -DryRun -OverviewPath .\docs\marketplace-overview-1.0.51.md
powershell -File .\scripts\Install-VisualStudioExtension.ps1 -VsixPath <public-1.0.50.vsix> -NoBuild -Reinstall -VisualStudioInstanceId 2c8402d8
powershell -File .\scripts\Install-VisualStudioExtension.ps1 -VsixPath <candidate-1.0.51.vsix> -NoBuild -VisualStudioInstanceId 2c8402d8
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario ReleaseAnnouncement -NoBuild -NoInstall -ExpectedReleaseVersion 1.0.51 -OutputRoot <D-drive-output>
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -NoBuild -NoInstall -OutputRoot <D-drive-output>
powershell -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -NoBuild -NoInstall -OutputRoot <D-drive-output>
powershell -STA -File .\scripts\SmokeDockedLayoutWidths.ps1 -NoBuild -OutputDir <D-drive-output>
powershell -File .\scripts\Install-VisualStudioExtension.ps1 -NoBuild -VsixPath <candidate-1.0.51.vsix> -VisualStudioInstanceId 1fe952ea -VsixInstallerPath <VS2026-VSIXInstaller.exe> -AllowRunningVisualStudio
powershell -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.51.0
setup.exe repair --installPath "C:\Program Files\Microsoft Visual Studio\18\Community" --passive --norestart
VSIXInstaller.exe /admin /instanceIds:1fe952ea /uninstall:RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
```

## Evidence locations

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\before-1.0.50
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\first-start-repro2
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\runtime
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\fit-stability
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\marketplace
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-compatibility
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-repair
```

The first update attempt loaded the 1.0.51 Tool Window and banner but the View-menu UI Automation check timed out while Visual Studio regenerated its pkgdef cache. The smoke originally expanded the menu only once for 15 seconds. The test was corrected to re-expand a menu closed by startup activity and wait up to 45 seconds. A complete second public-1.0.50 -> candidate-1.0.51 update then passed the first Visual Studio process with one Open command, one Scan command, the 1.0.51 banner, persisted Dismiss, and zero protocol errors. No product registration repair was used.

The VS2026 installer attempt did not modify or delete the administrator-owned historical extension. Logs show the 1.0.51 manifest and `[17.9,18.0)` range were accepted for VS2026 `18.7.1`; failure began when VSIXInstaller tried to uninstall migrated per-machine `1.0.6.0`, could not find its setup component in the current product catalog, and required administrator rights. The install and qualification scripts now detect this state before qualification instead of reporting an opaque installer exit code.

### Administrator removal follow-up, 2026-08-01 KST

All Visual Studio processes were closed before the following supported removal paths were exercised:

- elevated VS2026 `VSIXInstaller.exe /admin /instanceIds:1fe952ea /uninstall:<extension-id>` redirected to Visual Studio Installer, but the installer could not resolve the migrated component;
- elevated `setup.exe modify --installPath <VS2026> --remove <component-id>` was tried with both the unversioned component ID and the exact versioned component/package IDs recorded in `plan.xml`;
- the installer returned exit code 0 for the correctly quoted modify commands but logged `Cannot find package ... in product graph`, performed no removal, and left the per-machine manifest unchanged;
- VS2026 **Extensions > Manage Extensions > Installed** did not enumerate Raw Buffer Visualizer, so the documented IDE uninstall route could not schedule removal.

Evidence logs:

```text
%TEMP%\dd_VSIXInstaller_20260801222520_6a44.log
%TEMP%\dd_installer_20260801222735.log
%TEMP%\dd_installer_20260801222816.log
```

No `Program Files`, installer-state, registry, or package-cache file was manually deleted or edited.

### VS2026 Repair and post-reboot retry, 2026-08-01 to 2026-08-02 KST

The user approved a full VS2026 Repair after the narrower supported paths failed. Before Repair, the installed workload/component configuration was exported to `before-repair.vsconfig`. Repair completed with installer exit code `0`, `ManifestVerifier Result: Success`, and a zero-byte setup error log. It required a reboot. After reboot, `vswhere` reported instance `1fe952ea` as complete, launchable, and not requiring another reboot.

The post-Repair configuration export is byte-for-byte identical to the pre-Repair export: version `1.0`, 55 components, 3,069 bytes, SHA-256 `47034F9D146025E71F5A09256151D2120350BB67A26A7F465676AB507F160569`. This proves the Repair did not silently add or remove workloads/components.

Repair did not remove the orphaned extension manifest. A post-reboot elevated VSIXInstaller removal retry again discovered `1.0.6.0`, redirected to Visual Studio Installer, and produced `Cannot find package: Component.RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f in product graph`. The installer exited `0` without removal, and the manifest remained at `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSExtensions\pe0nm1ib.afo\extension.vsixmanifest`.

Repair evidence:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-repair\before-repair.vsconfig
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-repair\after-repair.vsconfig
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-repair\repair-final.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-repair\logs
```

The user subsequently approved a full VS2026 uninstall/reinstall. The replacement instance is Community `18.8.2` (`18.8.12023.21`), instance `0adc3897`, complete and launchable with no reboot pending. Its 55-component `.vsconfig` remained byte-identical to the pre-reinstall export, SHA-256 `47034F9D146025E71F5A09256151D2120350BB67A26A7F465676AB507F160569`.

The unchanged `1.0.51` candidate then installed successfully. ReleaseAnnouncement and AutomaticCollections passed, proving that the previous installer conflict was gone. MultiLibraryHybrid failed at the registered Bitmap visualizer. `ActivityLog.xml` records `BitmapDebuggerVisualizerProvider` activation failure because `ServiceHub.Host.Extensibility.Contracts, Version=17.0.0.0` could not be loaded; VS2026 `18.8.2` provides assembly version `18.0.0.0`. Evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-runtime`.

A separate `17.14` Extensibility SDK probe passed MultiLibraryHybrid on the same VS2026 instance, which established the corrective direction without altering the preserved `1.0.51` artifact. The corrected higher-version candidate is recorded in `release-qualification-1.0.52.md`.

## Durable closure

Status: Incomplete
Scope: Exact `1.0.51` package qualification on VS2022 and stable VS2026
Acceptance criteria: VS2022 build/update/runtime/Fit criteria passed; VS2026 installation, ReleaseAnnouncement, and AutomaticCollections passed; the required registered Bitmap visualizer runtime criterion failed
Verification: Exact hash check; Release build/self-tests/communication/package guards; VS2022 public-package update and runtime; VS2026 Repair/reboot/removal audit; user-approved full uninstall/reinstall; unchanged candidate installation; VS2026 core runtime scenarios and ActivityLog inspection
Evidence: Exact VSIX SHA-256 `7219386F9B8C452EE6AB06AED73B7BB13AC4581547D0B47DC8E731B6797B015F`; `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.51\vs2026-runtime`; configuration SHA-256 `47034F9D146025E71F5A09256151D2120350BB67A26A7F465676AB507F160569`
Boundary / next dependency: None for `1.0.51`; it is a definitively failed and superseded candidate. Do not rebuild, publish, or reopen its qualification unless the evidence or requirements change. Continue with `1.0.52`.

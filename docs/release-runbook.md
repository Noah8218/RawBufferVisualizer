# Release Runbook

Use this for a Marketplace update after the normal CI build is green.

## One-time GitHub setup

Create these repository settings:

| Setting | Kind | Value |
| --- | --- | --- |
| `VS_MARKETPLACE_TOKEN` | Secret | Azure DevOps PAT with Marketplace manage permission. |
| `VS_MARKETPLACE_PUBLISHER` | Variable | Marketplace publisher ID, not the display name. |
| `visual-studio-marketplace` | Environment | Add a required reviewer before publishing. |

Microsoft's command-line publishing flow uses `VsixPublisher.exe publish` with a VSIX payload, a publish manifest, and a PAT.

## Version bump

Use the bump script:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 1.0.53
```

This updates all four version sources:

- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj`
- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Classic\RawBufferVisualizer.VisualStudio.Classic.csproj`
- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest`
- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizerPackage.cs`

Example:

```xml
<AssemblyVersion>1.0.53.0</AssemblyVersion>
<FileVersion>1.0.53.0</FileVersion>
<Version>1.0.53</Version>
```

```xml
<Identity Id="RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f" Version="1.0.53.0" Language="en-US" Publisher="Noah Choi" />
```

## Release communication gate

The bump script changes binary version sources only. For every release, intentionally update these user-facing sources before building:

- `CHANGELOG.md`: complete user-visible history.
- `docs\marketplace-overview-<version>.md`: Marketplace Overview uploaded by the publishing script.
- `docs\marketplace-release-notes-<version>.md`: Marketplace and GitHub Release notes.
- `src\RawBufferVisualizer.VisualStudio.Extensibility\Resources\ReleaseNotes.txt`: release notes embedded in the VSIX manifest.
- `src\RawBufferVisualizer.VisualStudio\ReleaseAnnouncement.cs`: short in-product version and highlights.

Run the coherence guard:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.53
```

The guard fails when any version, Overview product heading/current-version section, path, manifest metadata, or GitHub Release notes flow is stale. `Publish-VisualStudioMarketplace.ps1` requires an explicit `-VsixPath`, rejects a VSIX whose manifest version differs from the current extension project version, and defaults only the Overview to `docs\marketplace-overview-<VSIX version>.md`; it no longer uploads the root README as the Overview. Never depend on a repository publish folder containing the current qualified candidate.

The Tool Window announcement is intentionally non-modal. It appears only after the user opens Raw Buffer Visualizer, never opens the Tool Window itself, never starts inspection, and stores **Dismiss** per user in `%APPDATA%\RawBufferVisualizer\release-announcement-settings.json`. **What's New** opens or closes the current summary without changing the saved Dismiss preference.

## Local release check

Run this before pushing the version bump:

```powershell
dotnet restore C:\Git\RawBufferVisualizer\RawBufferVisualizer.sln
dotnet build C:\Git\RawBufferVisualizer\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project C:\Git\RawBufferVisualizer\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.53
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-EnvironmentCheckContracts.ps1
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -PublishRoot D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate -NoZip
powershell -STA -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild -OutputDir D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\fit-stability
powershell -STA -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeDockedMemorySoak.ps1 -Configuration Release -Framework net472 -NoBuild
```

`Publish-VisualStudioExtension.ps1` is also the hybrid package-registration guard. It must produce `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef` with `CodeBase="$PackageFolder$\RawBufferVisualizer.VisualStudio.Extensibility.dll"`, Package GUID `{1977574b-f107-465f-bfd1-5fc022907039}`, and exactly one `"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"` registration. It must reject duplicate package/menu/ToolWindow registrations, the former split-project `.pkgdef`, and the retired Package GUID.

The VSIX to upload or smoke-test is:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Validate the exact package and generated Marketplace manifest without publishing:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Publish-VisualStudioMarketplace.ps1 -VsixPath D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix -Publisher openvisionlab -PublishManifestPath D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace\vs-publish.json -DryRun
```

Omitting `-VsixPath` is an error. Passing the preserved repository `1.0.51` or public `1.0.52` VSIX is also an error because its manifest version does not match the current `1.0.53` source release.

## Pre-publish clean/update gate

Before uploading, perform both a clean install and an in-place update from the exact previous public Marketplace version on the current serviced Visual Studio 2022 `17.14` profile. Do not uninstall the public baseline, run registration repair, or clear skipped-package state before the update test.

1. Install the exact candidate VSIX.
2. Restart Visual Studio.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.53.0
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
```

`Install-VisualStudioExtension.ps1` requires all `devenv.exe` processes to be closed. During quiet reinstall it waits for the direct VSIXInstaller process rather than long-lived descendants and removes idle `Microsoft.ServiceHub.Controller` processes between uninstall/install operations. If a manual install returns exit code `2004`, inspect the newest `%TEMP%\dd_VSIXInstaller_*.log` for the blocking process before retrying.

4. Confirm the View menu contains exactly one open command and one current-frame scan command.
5. Confirm the registered Bitmap handoff reaches explicit ACK, initialized OpenCvSharp/Emgu Mats open automatically on Break and **Scan Now**, the bounded Mat collection scenario reports 5 opened/2 isolated failures without duplicate rows, and the final hybrid result is 9 documents/0 errors.
6. Confirm Fit remains aspect-correct after resize and Manual zoom/pan remains stable.
7. Record the candidate SHA-256, Windows/Visual Studio versions, result JSON, screenshots, `package.log`, and relevant `ActivityLog.xml`.

Then install the same unchanged VSIX on a stable Visual Studio 2026 `18.x` instance and repeat at least `ReleaseAnnouncement`, `AutomaticCollections`, `MultiLibraryHybrid`, and `EnvironmentCheck`. Pass `-VisualStudioInstanceId` explicitly so installation and smoke evidence name the intended product generation. The `1.0.53` VSIX manifest uses `[17.14,18.0)` because the candidate depends on the stable `17.14` Extensibility runtime; Visual Studio 2026 supports API version 17.x and evaluates only the lower bound for compatibility. Record the exact VS2026 build; do not substitute a Preview/Insiders build for stable-release evidence.

Before the VS2026 install, run `Test-VisualStudioMarketplaceUpdate.ps1` or inspect both `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_<instance>\Extensions` and `<VS2026>\Common7\IDE\VSExtensions`. A historical extension migrated into the per-machine `VSExtensions` root owns the same extension ID and cannot be replaced by an ordinary per-user developer install. Remove or update it through Visual Studio **Manage Extensions** or Visual Studio Installer with administrator rights. Never delete the `Program Files` extension directory manually. Preserve the newest `dd_VSIXInstaller_*.log` if the setup catalog no longer contains that historical component.

Do not uninstall the previous public version, run `Repair-VisualStudioExtensionRegistration.ps1`, or use `/ResetSkipPkgs` before the VS2022 update smoke. If any is required, the candidate fails. Full rationale: [vsix-package-registration.md](vsix-package-registration.md).

## GitHub Marketplace CD

1. Push the version bump to `main`.
2. Open `Actions > Marketplace CD`.
3. Run once with `publish=false`.
4. Confirm the generated VSIX artifact has the expected version.
5. Run again with `publish=true`.
6. Approve the `visual-studio-marketplace` environment gate.

Recommended inputs:

| Input | Value |
| --- | --- |
| `publisher` | Leave empty if `VS_MARKETPLACE_PUBLISHER` is set. |
| `internal_name` | `RawBufferVisualizer` |
| `categories` | `other` |
| `expected_version` | Exact VSIX version, for example `1.0.53.0`. |

The workflow publishes only when `publish=true`; the default path is a dry validation build.

The tag workflow creates the GitHub Release from the same curated `docs\marketplace-release-notes-<version>.md` file and attaches only the standalone Windows viewer. Visual Studio users install and update the VSIX through Marketplace, keeping one public VSIX distribution path.

## Visual Studio update smoke

Use a real serviced Visual Studio 2022 `17.14` machine that already has the previous Marketplace version installed. After that update passes, perform a clean install or ordinary update of the identical VSIX on stable Visual Studio 2026 `18.x` and repeat the core runtime matrix.

1. Wait for Marketplace propagation after publishing.
2. Open Visual Studio.
3. Go to `Extensions > Manage Extensions > Updates`.
4. Update `Raw Buffer Visualizer`.
5. Close all Visual Studio windows when prompted.
6. Reopen Visual Studio.
7. Verify the installed version:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.53.0
```

8. Debug `RawBufferVisualizer.VisualizerDebuggee`.
9. Place the breakpoint after image assignments. Confirm initialized OpenCvSharp and Emgu CV Mats open automatically on Break and **Scan Now**, then inspect `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, typed OpenCvSharp/Emgu CV/Bitmap lists and dictionaries, `imageList`, `imageDictionary`, and `imageArray` through their registered paths.
10. Confirm single images and collections append only to the main docked `Raw Buffer Visualizer`; no second lower debugger ToolWindow opens.
11. Inspect a mixed `object[]` containing a valid image, `null`, and an unsupported object; confirm normal and red error rows appear together in the upper `Images` list.
12. Select an error row and confirm the viewer clears while the error overlay, Descriptor, and Diagnostics show the error ID and failure reason.
13. Use `Copy Report`; confirm the report includes extension/Visual Studio versions, source type, error details, and `Image payload included: No`. Use `Open Logs`; confirm `latest-error-report.txt` and `package.log` are discoverable.
14. Select a valid image row after the error and confirm the overlay closes and pan, mouse-wheel zoom, Save PNG, pixel status, selection marker, and diagnostics still work.
15. Resize the docked viewer in Fit mode and confirm aspect/margin remain stable; use wheel zoom/pan/1:1 and confirm Manual state preserves center and scale.
16. Open the View menu and confirm exactly one open command and one current-frame scan command.
17. Start two separate Visual Studio `devenv.exe` processes, invoke the visualizer in each process, and confirm each snapshot appears only in the docked viewer belonging to the process that invoked it.
18. Restart Visual Studio with no solution open and confirm there is no `RawBufferVisualizerPackage did not load correctly` popup.

If an upgraded developer profile reports `RawBufferVisualizerPackage did not load correctly`, preserve `ActivityLog.xml` and `package.log`. The repair script may be used only to confirm a stale legacy registration:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

Then restart Visual Studio, reinstall the candidate without repair, and repeat the smoke. A repair-only success is not release evidence.

Root cause to check in `ActivityLog.xml`: Visual Studio may keep a stale VSSDK `CodeBase` pointing to a deleted folder under `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<id>\Extensions`. Version `1.0.24.0` and later no longer auto-loads the package on startup; the docked window loads when the visualizer command is invoked.

If the popup appears only when inspecting an image, also check whether `ActivityLog.xml` reports a missing or mismatched `Microsoft.VisualStudio.Threading` or `ServiceHub.Host.Extensibility.Contracts` assembly. Versions `1.0.25` through `1.0.51` used the Visual Studio 2022 17.9 Extensibility line. Version `1.0.52` intentionally uses `17.14` and declares that matching VS2022 minimum. The release package step fails if the hybrid assembly references a newer `Microsoft.VisualStudio.Threading` version than the declared floor.

The package also writes a small diagnostic log here:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\package.log
```

## Do not publish if

- The version in the VSIX does not match the Marketplace update version.
- `ImagePtr`, OpenCvSharp `Mat`, Emgu CV `Mat`, a typed image list or dictionary, a mixed object collection, or a supported image array fails to show the Raw Buffer Visualizer option.
- Visual Studio opens multiple viewer windows instead of one docked image list.
- The generated `.vsextension\extension.json` is missing `IDebuggerVisualizerProvider`, a required Modern debugger visualizer provider, or the open generic `List<>`/`Dictionary<,>` collection targets.
- A debugger snapshot invoked from one Visual Studio process appears in another Visual Studio process's docked viewer.
- Mouse-wheel zoom or drag pan is slow in the docked window.
- README or Marketplace screenshots show stale UI or unrelated private applications.
- `RawBufferVisualizer.VisualStudio.Extensibility.dll` references `Microsoft.VisualStudio.Threading` newer than the declared Visual Studio 2022 support floor.
- The generated `.pkgdef` is missing, is named `RawBufferVisualizer.VisualStudio.Vssdk.pkgdef`, or points its package `CodeBase` to the VSSDK support library.
- Package, `Menus.ctmenu, 2`, or ToolWindow registration is missing or duplicated.
- The View menu does not show exactly one open command and one current-frame scan command.
- A clean PC reports `did not acknowledge the image handoff`.
- A request-file disappearance is treated as success without an explicit ACK, or a NACK reason is lost.
- Initialized OpenCvSharp/Emgu Mats do not open automatically, Bitmap appears as an automatic row, or repeated **Scan Now** duplicates rows.
- Fit changes image aspect after resize, or Manual resize resets zoom/center.
- The clean-PC smoke passes only after a registry repair.
- The update from public `1.0.52` to candidate `1.0.53` requires uninstall, repair, `/ResetSkipPkgs`, or another recovery action.
- The unchanged candidate has not passed the stable VS2026 installed-runtime core matrix.

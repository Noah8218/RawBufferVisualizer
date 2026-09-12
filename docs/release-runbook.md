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
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 2.0.9
```

This updates all five version sources:

- `src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj`
- `src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj`
- `src\RawBufferVisualizer.VisualStudio.Classic\RawBufferVisualizer.VisualStudio.Classic.csproj`
- `src\RawBufferVisualizer.VisualStudio.Vssdk\source.extension.vsixmanifest`
- `src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizerPackage.cs`

Example:

```xml
<AssemblyVersion>2.0.9.0</AssemblyVersion>
<FileVersion>2.0.9.0</FileVersion>
<Version>2.0.9</Version>
```

```xml
<Identity Id="RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f" Version="2.0.9.0" Language="en-US" Publisher="Noah Choi" />
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
powershell -ExecutionPolicy Bypass -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.9
```

The guard fails when any version, Overview product heading/current-version section, path, manifest metadata, or GitHub Release notes flow is stale. `Publish-VisualStudioMarketplace.ps1` requires an explicit `-VsixPath`, rejects a VSIX whose manifest version differs from the current extension project version, and defaults only the Overview to `docs\marketplace-overview-<VSIX version>.md`; it no longer uploads the root README as the Overview. Never depend on a repository publish folder containing the current qualified candidate.

The Tool Window announcement is intentionally non-modal. It appears only after the user opens Raw Buffer Visualizer, never opens the Tool Window itself, never starts inspection, and stores **Dismiss** per user in `%APPDATA%\RawBufferVisualizer\release-announcement-settings.json`. **What's New** opens or closes the current summary without changing the saved Dismiss preference.

## Local release check

Run this before pushing the version bump:

```powershell
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
powershell -ExecutionPolicy Bypass -File .\scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.9
powershell -ExecutionPolicy Bypass -File .\scripts\Test-EnvironmentCheckContracts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -BuildRoot D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\build-final-r2 -PublishRoot D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\candidate-final-r2 -NoZip
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild -OutputDir D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\layout-widths
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedMemorySoak.ps1 -Configuration Release -Framework net472 -NoBuild
```

`Publish-VisualStudioExtension.ps1` is also the hybrid package-registration guard. It must produce `RawBufferVisualizer.VisualStudio.Vssdk.pkgdef` with `CodeBase="$PackageFolder$\RawBufferVisualizer.VisualStudio.Vssdk.dll"`, Package GUID `{1977574b-f107-465f-bfd1-5fc022907039}`, and exactly one `"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"` registration. It must package the Extensibility provider under `OutOfProc`, reject the obsolete Extensibility-owned `.pkgdef`, duplicate package/menu/ToolWindow registrations, and the retired Package GUID.

The same script is the debugger-payload freshness gate. It builds the ObjectSource beside the actual `BaseOutputPath` selected for the release, then compares SHA-256 for these fresh files with their VSIX entries:

- `netstandard2.0/RawBufferVisualizer.Core.dll`;
- `netstandard2.0/RawBufferVisualizer.Sdk.dll`;
- `netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.dll`;
- `netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.deps.json`.

Any missing file or hash mismatch must abort packaging. Do not weaken this to an entry-presence check: public 2.0.7 contained all expected names but its ObjectSource was byte-identical to 2.0.6 because a routed build was packaged from a stale repository-local output directory. Source tests or a newer Visual Studio host do not prove which `netstandard2.0` bytes the oldest supported debugger host loads.

When the exact `.build` VSIX has already passed an explicit current-source Release build and installed-host tests, add `-NoBuild` to run the same package guards and copy those exact bytes without regenerating the ZIP. Never use `-NoBuild` as a substitute for the required Release build; record the build command and compare the source VSIX SHA-256 with the copied candidate.

The VSIX to upload or smoke-test is:

Use only the exact candidate recorded in [release-qualification-2.0.9.md](release-qualification-2.0.9.md). Do not upload it until the owner approves the candidate and copy, the matching source is committed and pushed, CI passes for that commit, and the exact bytes pass installed qualification on the required available hosts. Do not rebuild or substitute the candidate bytes after qualification.

Validate the exact package and generated Marketplace manifest without publishing:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioMarketplace.ps1 -VsixPath <FULLY_QUALIFIED_2_0_9_VSIX> -OverviewPath .\docs\marketplace-overview-2.0.9.md -Publisher openvisionlab -PublishManifestPath D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.9\marketplace\vs-publish.json -DryRun
```

Omitting `-VsixPath` is an error. Passing public `2.0.8` or any earlier/superseded candidate as `2.0.9` is also an error. Before upload, the source must be committed/pushed, CI must pass for that commit, installed qualification must pass, and the exact candidate path, length, and SHA-256 must match [release-qualification-2.0.9.md](release-qualification-2.0.9.md). Marketplace upload remains owner-controlled.

## Pre-publish clean/update gate

Before uploading, perform both a clean install and an in-place update from exact public `2.0.8` on a serviced Visual Studio 2022 profile. Also qualify the same bytes on real VS2022 `17.9` and stable VS2026 `18.x`. Do not uninstall the public baseline, run registration repair, or clear skipped-package state before the update test.

1. Install the exact candidate VSIX.
2. Restart Visual Studio.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.9.0
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticCollections -Configuration Release -NoBuild -NoInstall
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
```

`Install-VisualStudioExtension.ps1` requires all `devenv.exe` processes to be closed. During quiet reinstall it waits for the direct VSIXInstaller process rather than long-lived descendants and removes idle `Microsoft.ServiceHub.Controller` processes between uninstall/install operations. If a manual install returns exit code `2004`, inspect the newest `%TEMP%\dd_VSIXInstaller_*.log` for the blocking process before retrying.

4. Confirm the View menu contains exactly one open command and one current-frame scan command.
5. Confirm the registered Bitmap handoff reaches explicit ACK, initialized OpenCvSharp/Emgu Mats open automatically on Break and **Scan Now**, the bounded Mat collection scenario reports 5 opened/2 isolated failures without duplicate rows, and the final hybrid result is 9 documents/0 errors.
6. Confirm Fit remains aspect-correct after resize and Manual zoom/pan remains stable.
7. Record the candidate SHA-256, Windows/Visual Studio versions, result JSON, screenshots, `package.log`, and relevant `ActivityLog.xml`.

Install the same unchanged VSIX on real Visual Studio 2022 `17.9`, the current serviced Visual Studio 2022 instance, and stable Visual Studio 2026 `18.x`; repeat the affected `6768 x 3225` OpenCvSharp Mat, Emgu CV Mat, and exact registered ImagePtr path on each available host. On exact 17.9 also repeat `Int32Industrial`, `ImagePtrColdStart`, `ConcurrentDictionary`, `ReleaseAnnouncement`, `AutomaticCollections`, `MultiLibraryHybrid`, `EnvironmentCheck`, and pointer provenance/safety where the changed dependency graph requires them. Pass `-VisualStudioInstanceId` explicitly so installation and smoke evidence name the intended product generation. The `2.0.9` VSIX manifest uses `[17.9,18.0)`; Visual Studio 2026 supports API version 17.x and evaluates only the lower bound for compatibility. Record each exact host build and do not substitute a Preview/Insiders build. Because 2.0.9 corrects a debugger-RPC transfer failure reported on 17.9, exact 17.9 execution of all three reported-size pointer-backed source types is a required publication gate.

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
| `expected_version` | Exact VSIX version, for example `2.0.9.0`. |

The workflow publishes only when `publish=true`; the default path is a dry validation build.

The tag workflow creates the GitHub Release from the same curated `docs\marketplace-release-notes-<version>.md` file and attaches only the standalone Windows viewer. Visual Studio users install and update the VSIX through Marketplace, keeping one public VSIX distribution path.

## Visual Studio update smoke

Use a real supported Visual Studio 2022 machine that already has public `2.0.7` installed. After that update passes, test the identical VSIX on real VS2022 `17.9`, a current serviced VS2022 instance, and stable VS2026 `18.x`.

1. Wait for Marketplace propagation after publishing.
2. Open Visual Studio.
3. Go to `Extensions > Manage Extensions > Updates`.
4. Update `Raw Buffer Visualizer`.
5. Close all Visual Studio windows when prompted.
6. Reopen Visual Studio.
7. Verify the installed version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.9.0
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
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

Then restart Visual Studio, reinstall the candidate without repair, and repeat the smoke. A repair-only success is not release evidence.

Root cause to check in `ActivityLog.xml`: Visual Studio may keep a stale VSSDK `CodeBase` pointing to a deleted folder under `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<id>\Extensions`. Version `1.0.24.0` and later no longer auto-loads the package on startup; the docked window loads when the visualizer command is invoked.

If the popup appears only when inspecting an image, also check whether `ActivityLog.xml` reports a missing or mismatched `Microsoft.VisualStudio.Threading` or Extensibility contract assembly. In 2.0.9 the in-process `RawBufferVisualizer.VisualStudio.Vssdk.dll` remains pinned to the qualified 17.9 threading contract, preloads asynchronously in the Debugging UI context, and the provider is packaged under `OutOfProc`; the release package step rejects a newer VSSDK threading reference, missing preload contract, unsafe initialization, mismatched `[17.9,18.0)` declaration, or stale debugger-side payload.

The package also writes a small diagnostic log here:

```text
%TEMP%\RawBufferVisualizer\VisualStudio\package.log
```

## Do not publish if

- The version in the VSIX does not match the Marketplace update version.
- Any required `netstandard2.0` debugger payload hash differs between the fresh routed Release output and the VSIX.
- `ImagePtr`, OpenCvSharp `Mat`, Emgu CV `Mat`, a typed image list or dictionary, a mixed object collection, or a supported image array fails to show the Raw Buffer Visualizer option.
- Visual Studio opens multiple viewer windows instead of one docked image list.
- The generated `.vsextension\extension.json` is missing `IDebuggerVisualizerProvider`, a required Modern debugger visualizer provider, or the open generic `List<>`/`Dictionary<,>` collection targets.
- A debugger snapshot invoked from one Visual Studio process appears in another Visual Studio process's docked viewer.
- Mouse-wheel zoom or drag pan is slow in the docked window.
- README or Marketplace screenshots show stale UI or unrelated private applications.
- `RawBufferVisualizer.VisualStudio.Vssdk.dll` references a `Microsoft.VisualStudio.Threading` contract newer than the qualified 17.9 floor.
- The generated `.pkgdef` is missing, is not named `RawBufferVisualizer.VisualStudio.Vssdk.pkgdef`, or does not point its package `CodeBase` to `RawBufferVisualizer.VisualStudio.Vssdk.dll`.
- Package, `Menus.ctmenu, 2`, or ToolWindow registration is missing or duplicated.
- The View menu does not show exactly one open command and one current-frame scan command.
- A clean PC reports `did not acknowledge the image handoff`.
- A request-file disappearance is treated as success without an explicit ACK, or a NACK reason is lost.
- Initialized OpenCvSharp/Emgu Mats do not open automatically, Bitmap appears as an automatic row, or repeated **Scan Now** duplicates rows.
- Fit changes image aspect after resize, or Manual resize resets zoom/center.
- The clean-PC smoke passes only after a registry repair.
- The update from public `2.0.8` to candidate/public `2.0.9` requires uninstall, repair, `/ResetSkipPkgs`, or another recovery action.
- Exact Visual Studio 2022 17.9 cannot open the reported `6768 x 3225` OpenCvSharp Mat, Emgu CV Mat, and ImagePtr through checked live process memory, or any row reports a debugger RPC snapshot failure.
- A visible debugger RPC error repeats only the localized host text instead of reporting operation, source or chunk, byte counts when applicable, exception type, and HRESULT; the original host text and stack must remain available in the support report.
- The unchanged candidate has not passed the stable VS2026 installed-runtime core matrix.

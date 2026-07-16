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
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 1.0.45
```

This updates all three version sources:

- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj`
- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Classic\RawBufferVisualizer.VisualStudio.Classic.csproj`
- `C:\Git\RawBufferVisualizer\src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest`

Example:

```xml
<AssemblyVersion>1.0.45.0</AssemblyVersion>
<FileVersion>1.0.45.0</FileVersion>
<Version>1.0.45</Version>
```

```xml
<Identity Id="RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f" Version="1.0.45.0" Language="en-US" Publisher="Noah Choi" />
```

## Local release check

Run this before pushing the version bump:

```powershell
dotnet restore C:\Git\RawBufferVisualizer\RawBufferVisualizer.sln
dotnet build C:\Git\RawBufferVisualizer\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project C:\Git\RawBufferVisualizer\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoZip
powershell -STA -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\SmokeDockedMemorySoak.ps1 -Configuration Release -Framework net472 -NoBuild
```

The VSIX to upload or smoke-test is:

```text
C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

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
| `expected_version` | Exact VSIX version, for example `1.0.45.0`. |

The workflow publishes only when `publish=true`; the default path is a dry validation build.

## Visual Studio update smoke

Use a real Visual Studio 2022 machine that already has the previous Marketplace version installed.

1. Wait for Marketplace propagation after publishing.
2. Open Visual Studio.
3. Go to `Extensions > Manage Extensions > Updates`.
4. Update `Raw Buffer Visualizer`.
5. Close all Visual Studio windows when prompted.
6. Reopen Visual Studio.
7. Verify the installed version:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.45.0
```

8. Debug `RawBufferVisualizer.VisualizerDebuggee`.
9. Inspect `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, typed OpenCvSharp/Emgu CV/Bitmap lists and dictionaries, `imageList`, `imageDictionary`, and `imageArray`.
10. Confirm single images and collections append only to the main docked `Raw Buffer Visualizer`; no second lower debugger ToolWindow opens.
11. Inspect a mixed `object[]` containing a valid image, `null`, and an unsupported object; confirm normal and red error rows appear together in the upper `Images` list.
12. Select an error row and confirm the viewer clears while the error overlay, Descriptor, and Diagnostics show the error ID and failure reason.
13. Use `Copy Report`; confirm the report includes extension/Visual Studio versions, source type, error details, and `Image payload included: No`. Use `Open Logs`; confirm `latest-error-report.txt` and `package.log` are discoverable.
14. Select a valid image row after the error and confirm the overlay closes and pan, mouse-wheel zoom, Save PNG, pixel status, selection marker, and diagnostics still work.
15. Start two separate Visual Studio `devenv.exe` processes, invoke the visualizer in each process, and confirm each snapshot appears only in the docked viewer belonging to the process that invoked it.
16. Restart Visual Studio with no solution open and confirm there is no `RawBufferVisualizerPackage did not load correctly` popup.

If Visual Studio reports `RawBufferVisualizerPackage did not load correctly`, close all Visual Studio windows and run:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

Then restart Visual Studio and repeat the smoke.

Root cause to check in `ActivityLog.xml`: Visual Studio may keep a stale VSSDK `CodeBase` pointing to a deleted folder under `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<id>\Extensions`. Version `1.0.24.0` and later no longer auto-loads the package on startup; the docked window loads when the visualizer command is invoked.

If the popup appears only when inspecting an image, also check whether `ActivityLog.xml` reports a missing or mismatched `Microsoft.VisualStudio.Threading` assembly. Version `1.0.25.0` and later build the docked VSSDK package against Visual Studio 2022 17.9-compatible references. The release package step fails if the VSSDK package references a newer `Microsoft.VisualStudio.Threading` version than 17.9.

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
- `RawBufferVisualizer.VisualStudio.Vssdk.dll` references `Microsoft.VisualStudio.Threading` newer than `17.9.0.0`.

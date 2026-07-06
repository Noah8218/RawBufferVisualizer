# Visual Studio Marketplace Checklist

Use this before publishing the first Marketplace preview build.

## Package

Build a Release VSIX:

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472
```

Expected output:

```text
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472.zip
artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

## Listing Metadata

Suggested Marketplace fields:

| Field | Value |
| --- | --- |
| Display name | Raw Buffer Visualizer |
| Publisher/author | Noah Choi |
| Short description | Image Watch style debugger visualizer for raw buffers, Bitmap, Mat, and pointer-backed image views. |
| Type | Tools |
| Categories | Debugging, Other Tools |
| Tags | image-watch, raw-buffer, vision, opencv, emgu |
| License | MIT |
| Release stage | Preview for the first Marketplace upload |

Overview copy:

```text
Raw Buffer Visualizer is an Image Watch style debugger visualizer for C# machine-vision developers.

Inspect raw buffers, RawBufferView pointer-backed images, ImagePtr-style pointer objects, System.Drawing.Bitmap, OpenCvSharp Mat, and Emgu CV Mat variables while debugging in Visual Studio. Every inspected image is appended to one docked image list with thumbnail, dimensions, pixel format, stride, source type, diagnostics, and visible error rows.

Use the docked viewer to pan, zoom, save the visible view as PNG, save raw snapshots, read pixel coordinates, GV/RGB channel values, source bytes, hover 5x5 statistics, selected/pinned marker values, high-zoom pixel grid overlays, Try interpretation, and A/B comparison.
```

## Required Screenshots

Keep these files current for the listing:

```text
docs\images\marketplace-icon.png
docs\images\viewer-vs-docked.png
docs\images\viewer-vs-docked-overlay.png
docs\images\viewer-vs-docked-error.png
docs\images\viewer-100k-file-backed.png
docs\images\viewer-200k-file-backed.png
```

The first Marketplace screenshot should show the docked Visual Studio workflow, not the standalone viewer.

Screenshot gate:

- Open every README and Marketplace screenshot before commit.
- Reject screenshots that include unrelated applications, browser tabs, private desktop content, stale UI, or a feature state that does not match the listing text.
- Prefer cropped ToolWindow-focused screenshots for the Visual Studio workflow.
- Store review evidence under `artifacts\ui\...` when screenshots are replaced.

## Required Validation

Run before uploading:

```powershell
dotnet build .\RawBufferVisualizer.sln --configuration Release --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --no-build
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 100000 -Height 100000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Width 200000 -Height 200000 -Configuration Release -Framework net472 -Dense -NoBuild
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -PixelFormat Mono16 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat BGR24 -Width 640 -Height 484
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeVisualStudioDockedPerformance.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoBuild -NoInstall -PixelFormat Float32 -Width 320 -Height 240
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild
```

The docked smoke must validate:

- One Visual Studio docked ToolWindow.
- Single image list accumulation.
- Error rows remain visible.
- Narrow docked layout keeps the Inspector collapsed by default and preserves image list, viewer, Save, and status strip access.
- Medium and wide layouts expose compact tab Inspector and full Inspector respectively.
- Save visible PNG, raw snapshot export path, pixel status, raw bytes, hover 5x5 statistics, selected/pinned marker, pan, zoom, drag, and wheel interaction.
- Non-blank framebuffer capture.

## Install, Update, Uninstall, Reinstall

Use the install script for developer verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Reinstall
```

Manual smoke checklist:

- Install from the generated VSIX.
- Restart Visual Studio.
- Confirm `Raw Buffer Visualizer` appears in `Extensions > Manage Extensions > Installed`.
- Debug `RawBufferVisualizer.VisualizerDebuggee`.
- Inspect `RawBufferSnapshot`, `RawBufferView`, `ImagePtr`, `Bitmap`, OpenCvSharp `Mat`, and Emgu CV `Mat`.
- Close Visual Studio.
- Install the same VSIX again and confirm update/reinstall path does not leave a broken package.
- Uninstall from Manage Extensions.
- Restart Visual Studio and confirm the visualizer is gone.
- Reinstall and repeat one debugger inspection.
- For scripted developer smoke, verify uninstall removes the extension manifest from `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_<instance>\Extensions`, then reinstall with `Install-VisualStudioExtension.ps1 -Reinstall`.
- After update, restart Visual Studio once with no solution open and confirm no `RawBufferVisualizerPackage did not load correctly` popup appears.
- If a PC already has stale VSSDK registration, close Visual Studio and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-VisualStudioExtensionRegistration.ps1
```

This rewrites the VSSDK package `CodeBase` to the current installed VSIX folder and removes old startup autoload registration.

## Marketplace CD

Use [release-runbook.md](release-runbook.md) for repeatable updates.

Version bump:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Bump-VisualStudioExtensionVersion.ps1 -Version 1.0.23
```

GitHub setup:

| Setting | Kind | Notes |
| --- | --- | --- |
| `VS_MARKETPLACE_TOKEN` | Secret | Azure DevOps PAT with Marketplace manage permission. |
| `VS_MARKETPLACE_PUBLISHER` | Repository variable | Marketplace publisher ID, not display name. |
| `visual-studio-marketplace` | Environment | Add required reviewer approval before publish. |

Workflow:

1. Run `Actions > Marketplace CD` with `publish=false`.
2. Verify the generated VSIX artifact and `vs-publish.json`.
3. Run again with `publish=true`.
4. Approve the `visual-studio-marketplace` environment.
5. Wait for Marketplace propagation.
6. Test Visual Studio update from `Extensions > Manage Extensions > Updates`.
7. Verify the installed version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.23.0
```

## Release Notes Template

```text
Raw Buffer Visualizer preview

- Adds a docked Visual Studio image inspector for debugger visualizer sessions.
- Supports RawBufferSnapshot, RawBufferView, ImagePtr-style pointer objects, System.Drawing.Bitmap, OpenCvSharp Mat, and Emgu CV Mat.
- Adds thumbnails, image list accumulation, responsive docked layouts, descriptor diagnostics, pixel status, raw bytes, hover 5x5 statistics, selected/pinned marker, and A/B comparison.
- Adds visible PNG export and raw snapshot export from the docked viewer.
- Includes large file-backed image validation up to 200000 x 200000 dense raw payloads.

Known limits:
- Vendor SDK-specific adapters are not shipped yet. Use RawBufferView for common camera and frame-grabber buffer shapes.
- Unsupported planar, compressed, YUV, signed integer, and additional packed formats fail with diagnostics instead of rendering silently.
```

## Evidence Artifacts

Keep these artifacts with the release validation notes:

```text
artifacts\perf\vs-docked\visual-studio-docked-performance.json
artifacts\perf\vs-docked\visual-studio-docked-session.json
artifacts\perf\vs-docked\visual-studio-docked-session.png
artifacts\perf\vs-docked\visual-studio-docked-framebuffer.png
artifacts\ui\docked-layout-widths\layout-widths.json
```

## Do Not Ship If

- User-facing text still mentions rendering implementation details.
- Narrow Visual Studio docking hides Save, image list, viewer, status strip, or Inspector access.
- Debugger inspections open multiple independent viewer windows instead of one docked image list.
- Install/update/uninstall/reinstall has not been checked.
- The README or listing does not include the MIT license and third-party notice requirement.
- Visual Studio shows `RawBufferVisualizerPackage did not load correctly` after updating and restarting.

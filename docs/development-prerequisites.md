# Development Prerequisites And Utility Recovery

Use this checklist after a Windows reinstall, on a new contributor PC, or before local release validation. It separates end-user requirements from development, release, and media-only utilities so optional tools are not presented as product runtime dependencies.

## Role Matrix

| Utility or prerequisite | Extension user | Build/test contributor | Installed-VSIX validation | Marketplace publisher | Demo author |
| --- | --- | --- | --- | --- | --- |
| Supported x64 Visual Studio | Required | Required | Required | Required | Required for real workflow capture |
| Raw Buffer Visualizer VSIX | Required | Not required for source-only tests | Required | Required exact candidate | Required |
| Git | No | Required | Required for a reproducible source checkout | Required | Required when producing repository media |
| Windows PowerShell 5.1 | No | Required by repository scripts | Required | Required | Required by the media helper |
| .NET 8 SDK or newer | No | Required | Required for sample/debuggee builds | Required | Required for sample builds |
| .NET desktop development workload | No | Required for the supported IDE/manual-test baseline | Required | Required | Required for source-based capture |
| Visual Studio extension development workload | No | Recommended for IDE-based extension development; not required by the verified CLI build | Optional unless an IDE workflow specifically needs it | Useful as a `VsixPublisher.exe` fallback | No |
| `vswhere.exe` / `VSIXInstaller.exe` | No separate install; Visual Studio Installer supplies them | Used automatically by scripts | Required and supplied by Visual Studio Installer | Used for discovery | No |
| `VsixPublisher.exe` | No | No | No | Required; normally restored by `Microsoft.VSSDK.BuildTools` | No |
| FFmpeg | No | No | No | No | Optional, required only by `tools\Create-DemoMedia.ps1` |
| OpenCvSharp, Emgu CV, SharpGL, VSSDK NuGet packages | No separate product install | Restored automatically by NuGet | Restored automatically for samples/tests | Restored automatically | Restored automatically when needed |
| Vendor camera SDKs/drivers | Only when the user's own application requires them | Optional qualification inputs | Optional qualification inputs | No | No |

## End-User Requirement

A normal user installs only:

1. Visual Studio 2022 17.14+ x64 or stable Visual Studio 2026 18.x x64.
2. Raw Buffer Visualizer from Visual Studio Marketplace or the exact approved VSIX.
3. A Visual Studio restart after install or update.

The extension does not require a separate .NET SDK, OpenCvSharp, Emgu CV, SharpGL, FFmpeg, camera SDK, `RAW_BUFFER_VISUALIZER_VIEWER` environment variable, or manually registered VSPackage. A debuggee that uses OpenCvSharp, Emgu CV, or a camera SDK must still carry its own normal application dependencies.

## Contributor Setup

Install or restore:

- [Visual Studio](https://visualstudio.microsoft.com/downloads/) 2022 17.14+ with **.NET desktop development**. Stable Visual Studio 2026 18.x is an additional compatibility target, not a replacement for the VS2022 baseline.
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or newer. Prefer having an 8.0 SDK available because `net8.0-windows` is the test target; .NET 9 and 10 are not required.
- [Git for Windows](https://git-scm.com/download/win).
- Windows PowerShell 5.1 (`powershell.exe`), which the repository scripts target.

The first restore needs NuGet network access or a populated package cache:

```powershell
Set-Location C:\Git\RawBufferVisualizer
dotnet restore .\RawBufferVisualizer.sln
dotnet build .\RawBufferVisualizer.sln -c Release -p:Platform="Any CPU" --no-restore
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --framework net8.0-windows --no-build
```

The solution carries `Microsoft.NETFramework.ReferenceAssemblies.net472`, so a contributor must not install an old .NET Framework developer pack merely because a script or machine path is stale. The hybrid extension project also restores its compatible `Microsoft.VSSDK.BuildTools` transitively. Never install the historical `17.9.3168` package to satisfy `SmokeSmartTypeMapper.ps1`; that hard-coded path is a test-harness defect recorded in the post-reinstall audit.

## Machine Detection Checklist

Run from Windows PowerShell:

```powershell
git --version
dotnet --list-sdks
$PSVersionTable.PSVersion

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
& $vswhere -all -products * -format table
& $vswhere -all -products * -requires Microsoft.VisualStudio.Workload.ManagedDesktop -property installationPath

Get-Command ffmpeg -ErrorAction SilentlyContinue
Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.vssdk.buildtools" -Directory -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1 -ExpandProperty FullName
```

Expected minimum results:

- `git --version` returns a Windows Git version.
- At least one 8.0 or newer .NET SDK is listed.
- Windows PowerShell reports major version 5.
- `vswhere` lists a complete, launchable supported Visual Studio instance with the managed desktop workload.
- `ffmpeg` may be absent unless demo media is being generated.
- `VsixPublisher.exe` is needed only for a real Marketplace dry run/publish and is normally found below the newest restored `microsoft.vssdk.buildtools` package.

## Release-Only Requirements

`scripts\Publish-VisualStudioMarketplace.ps1` finds `VsixPublisher.exe` in this order:

1. Explicit `-VsixPublisherPath`.
2. `VSIX_PUBLISHER_PATH`.
3. The newest restored `Microsoft.VSSDK.BuildTools` package.
4. A Visual Studio installation that contains the VSSDK component.

Publishing additionally requires a Marketplace publisher ID, an Azure DevOps PAT with Marketplace management permission, and the repository's required environment approval. These secrets must not be stored in source, copied into support reports, or exposed through an in-product installer.

## Optional Demo Media Utility

FFmpeg is used only to convert a reviewed real workflow capture to GIF and MP4:

```powershell
winget install --id Gyan.FFmpeg.Essentials --exact --accept-package-agreements --accept-source-agreements
ffmpeg -version
powershell -ExecutionPolicy Bypass -File .\tools\Create-DemoMedia.ps1 -InputPath <capture-file>
```

Installation is intentionally manual. Follow [demo-recording-guide.md](demo-recording-guide.md), visually review the source capture, and do not treat generated media as functional-test evidence.

## Test Storage

On the project Windows workstation, store generated test data and evidence under:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer
```

Route test-process `TEMP` and `TMP` there when practical. A machine without `D:` may use a clearly recorded isolated fallback. Source, tracked fixtures, and documentation stay in the repository.

## In-Product Environment Check

Source version `1.0.53` implements the approved **Environment** panel. Public Marketplace `1.0.52` does not contain it; do not confuse the local development candidate with the immutable public package.

The panel reports required runtime state first:

1. Current Visual Studio file version, x64 process state, and the supported `17.14+`/stable `18.x` host rule.
2. Raw Buffer Visualizer version loaded in the current Visual Studio session.
3. A create/write/delete probe below the extension's temporary-storage root.

It then reports optional contributor/media utilities:

- .NET 8 SDK: detected below `%ProgramFiles%\dotnet\sdk\8.*`.
- Visual Studio extension-development workload: detected through the current installation's `VSSDK` directory.
- FFmpeg: detected as `ffmpeg.exe` on `PATH`; it remains demo-media-only.

Actions follow these rules:

- **Refresh**, **Copy diagnostic report**, and **Close** do not scan a debugger frame, open an image, change the active document, install software, or modify VSPackage registration.
- **Open VS Installer**, **Official download**, and **Installation guide** require a visible Yes/No confirmation.
- The extension opens Visual Studio Installer or the official .NET, Microsoft Learn, or FFmpeg page. It does not download installers, select workloads, or run package-manager commands.
- The report excludes credentials, environment-variable values, and image payloads. It explicitly warns that local paths are present and must be reviewed before sharing.
- Vendor SDKs/drivers and Marketplace publisher credentials remain outside the feature.

A normal extension user still has no extra utility to install beyond a supported Visual Studio and the VSIX.

Source/markup safety contracts can be rechecked without launching an IDE:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-EnvironmentCheckContracts.ps1
```

## Recovery Decision

| Symptom | Correct action |
| --- | --- |
| Extension user cannot find the command | Confirm a supported Visual Studio version, exact VSIX registration, and one restart; do not install OpenCV or FFmpeg |
| `dotnet` is missing | Install the .NET 8 SDK from the official download page |
| `vswhere.exe` or `VSIXInstaller.exe` is missing | Repair Visual Studio Installer/Visual Studio; do not copy these executables from another machine |
| NuGet package path in a smoke script is missing | Fix the script to discover the restored version; do not install the stale hard-coded version |
| `VsixPublisher.exe` is missing | Restore the solution or install the VSSDK workload only for release tooling |
| FFmpeg is missing | Ignore unless producing demo media; install manually from the documented source when needed |
| Vendor SDK test is blocked | Obtain the legal SDK/hardware/emulator prerequisite; fixture evidence is not certification |

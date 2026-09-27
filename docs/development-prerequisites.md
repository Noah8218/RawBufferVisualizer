# Development Prerequisites

## Windows Setup

- Git for Windows and Windows PowerShell 5.1 (`powershell.exe`).
- .NET 8 SDK. Keep an 8.0 SDK available; newer SDKs alone do not establish the tested build baseline.
- Visual Studio 2022 17.9+ x64 with **.NET desktop development** for IDE development. **Visual Studio extension development** is useful for extension tooling but is not required by the CLI build.
- Network access to nuget.org for the first restore. NuGet restores the .NET Framework 4.7.2 reference assemblies, VSSDK tooling, and sample dependencies.

An extension user needs a supported Visual Studio installation and the extension, plus dependencies of their own application. A standalone .NET SDK, camera SDK, FFmpeg, or a manually registered VSPackage is not an extra extension runtime requirement.

## Clone And Build

Choose any writable checkout directory; the original developer's C: path is not required.

```powershell
git clone https://github.com/Noah8218/RawBufferVisualizer.git
Set-Location RawBufferVisualizer
git status --short
dotnet --list-sdks
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path (Get-Location) 'artifacts' }
$buildRoot = Join-Path $testRoot 'build'
New-Item -ItemType Directory -Force -Path (Join-Path $testRoot 'temp') | Out-Null
$env:TEMP = Join-Path $testRoot 'temp'
$env:TMP = $env:TEMP
dotnet restore .\RawBufferVisualizer.sln "-p:RawBufferVisualizerBuildRoot=$buildRoot"
dotnet build .\RawBufferVisualizer.sln -c Release '-p:Platform=Any CPU' "-p:RawBufferVisualizerBuildRoot=$buildRoot" --no-restore
dotnet "$buildRoot\bin\RawBufferVisualizer.Tests\Release\net8.0-windows\RawBufferVisualizer.Tests.dll" --usability
```

Stop after any nonzero restore/build/test exit code. Use the [Start Here route](README.md#start-here) to choose the IDE startup project. Opening the solution or building does not install the extension.

`Directory.Build.props` owns output routing. Without `RawBufferVisualizerBuildRoot`, IDE/CLI builds use `.build` below the checkout. Pass the same root to restore and build. Pass that root as `-BuildRoot` to the focused smoke scripts when inspecting an existing build; `-NoBuild` requires matching binaries already present. The six focused scanner/histogram/mapping/render/usability/viewer scripts default to the D: test root when available, otherwise `artifacts` below the checkout and an explicit warning. Their normal default build location is `<testRoot>\build`.

NuGet uses the normal user cache unless `NUGET_PACKAGES` is set. The debuggee's native dependency copy accepts a cache path with or without a trailing separator. Do not copy a previous PC's `.build`, `.vs`, `bin`, `obj`, or package cache as a substitute for restoring and building.

## What Git Does Not Transfer

- Visual Studio installation/workloads, installed extensions, startup-project selection, breakpoints, and user preferences.
- User mappings and preferences under `%APPDATA%\RawBufferVisualizer`. Solution-local mappings belong to their application's solution, not automatically to this repository.
- External source images and local screenshots, logs, or installers. The optional industrial-image smoke scenarios require explicit `-IndustrialImagePath` / `-IndustrialDataTipImagePath` inputs; see [image provenance](industrial-image-testing.md).

Built files may belong to an older version. Read the VSIX manifest and calculate its SHA-256 before choosing a package. A source build is not permission to install, publish, or restart a workstation.

## Verification Boundaries

Use focused groups from [architecture and validation](ARCHITECTURE_AND_VALIDATION.md). UI smoke and installed-IDE checks require their stated host, build and input prerequisites. Some older installed smoke scripts can install an extension by default: read their parameters before running them. A previous version's runtime result does not verify current source.

The extension's **Environment** panel reports the running host, loaded extension version and writable temporary storage. Refreshing or copying that report does not scan an image or repair registration. Follow [vendor policy](vendor-sdk-license-policy.md) before adding proprietary SDK dependencies.

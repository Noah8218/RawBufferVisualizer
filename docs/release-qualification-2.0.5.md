# Raw Buffer Visualizer 2.0.5 Review Candidate

Date: 2026-09-03 KST

## State

`2.0.5.0` is a local owner-review candidate. Source/package checks and the pointer/name/collection runtime audit passed on Visual Studio 2022 Community `17.14.37516.0`. The final package's six product runtime assemblies, generated debugger-provider metadata, VSPackage registration, and VSIX manifest are SHA-256 identical to the installed files used for that runtime audit. The final container was rebuilt only to embed the revised release note, so a clean install and public-`2.0.3` in-place update of the exact final container remain release gates. No commit, push, tag, Marketplace upload, or publication was performed.

The Visual Studio Marketplace Gallery API last checked on 2026-08-31 reported public `2.0.3.0`, updated `2026-08-24T12:52:32.877Z`; it was not queried again in this verification. The frozen local `2.0.4.0` candidate remains separate and immutable. Pointer-safety, expression-name, and array-registration changes produce different package bytes, so they use patch version `2.0.5` instead of overwriting `2.0.4`.

## Exact Review VSIX

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\final-publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,510,595` bytes |
| SHA-256 | `9A3A9C73AE61A83D00E5BA5BFCAC3D7F26EE2D354F080AC4F988CB6D2E6340C5` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Manifest version | `2.0.5.0` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72` |

The copied review candidate and the Release build output have the same SHA-256. The package contains the VSSDK DLL and `.pkgdef`, the out-of-process debugger provider, and embedded `2.0.5` release notes.

Evidence:

- `D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\REPORT.md`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\screenshots`
- Installed runtime root: `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_419f0858\Extensions\jpsx2jci.3uq`

## Included User-Visible Changes

- Shows the exact debugger object/expression name above each thumbnail and preserves collection roots in names such as `imageList[0]`, `imageDictionary[key]`, and `bitmapArray[1]`.
- Shows OpenCvSharp/Emgu `Ptr` separately from the pixel `Data`/`DataPointer` address, combines equal ImagePtr/RawBufferView addresses, and records Bitmap `Scan0 / Pixels` as captured provenance only.
- Adds separate **Copy pointer address** and **Copy pixel address** actions and includes both values in Descriptor details.
- Shows process ID and `LIVE`, `CAPTURED`, `PREVIEW`, or `UNAVAILABLE` state when provenance is available.
- Reads current bytes when the same address is opened again; an address is not treated as object identity.
- Rejects released, inaccessible, and partially readable native-memory ranges instead of accepting incomplete image data.
- Corrects row order for Bitmap sources with a negative native stride.
- Fixes direct Bitmap/OpenCvSharp/Emgu array registration by using complete assembly-qualified type identities.
- Retains the existing **Clear all**, collection, mapping, inspection, and Visual Studio compatibility contracts.

## Checks Run

| Check | Result |
| --- | --- |
| `Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.5` | Passed |
| Routed `dotnet build .\RawBufferVisualizer.sln -c Release` | Passed; 0 errors, 18 pre-existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` |
| Aggregate `RawBufferVisualizer.Tests` on `net8.0-windows` | Passed |
| `SmokeLegacyImageCompatibility.ps1 -Configuration Release` | Passed five OpenCvSharp and five Emgu package versions, including Ptr/pixel/Bitmap provenance assertions |
| `Publish-VisualStudioExtension.ps1 ... -BuildRoot <D:> -DirectoryBuildPropsPath <D:> -NoZip` under Windows PowerShell 5.1 | Passed package, 17.9 compatibility, registration, provider, and VSIX-content guards |
| Candidate/source SHA-256 equality | Passed |
| Installed-runtime/final-package equality | Passed for six product runtime assemblies, `.vsextension/extension.json`, VSPackage `.pkgdef`, and `extension.vsixmanifest` |
| Installed Visual Studio 2022 runtime audit | Passed individual Bitmap/OpenCvSharp/Emgu/RawBufferView/RawBufferSnapshot/ImagePtr, generic and non-generic collections, object/raw arrays, and direct Bitmap/OpenCvSharp/Emgu/RawBufferView arrays |
| Image-list splitter runtime audit | Passed minimum-width, default-width, and wide layouts; object names remain above thumbnails and pointer/state/summary values wrap instead of clipping |
| English/Korean Overview parity review | Passed for expression names, Ptr-versus-Pixels semantics, Bitmap Scan0 lifetime, array-registration fix, support range, and pointer safety |

The first custom-build-root packaging attempt failed because MSBuild received a literal `$(MSBuildProjectName)` path; the script now requires an explicit routed `Directory.Build.props` for a custom build root. A second attempt under PowerShell 7 built successfully but stopped at the existing .NET Framework-only `ReflectionOnlyLoadFrom` guard. The final Windows PowerShell 5.1 run passed. Neither failed attempt published or installed anything.

## Remaining Release Gates

1. Owner approval of the exact VSIX hash, runtime screenshots, and English/Korean Overview.
2. Commit and branch push only after explicit authorization, followed by CI on that exact source.
3. Clean install and in-place update from exact public `2.0.3` using the final `9A3A...40C5` container, without uninstall, repair, or `/ResetSkipPkgs`.
4. Installed changed-path checks on stable Visual Studio 2026; exact Visual Studio 2022 `17.9` runtime remains unverified because that host is not installed.
5. Decide whether to replace Marketplace media with the new pointer/name screenshots. Existing public-workflow media remains from exact installed `2.0.4` and does not demonstrate the new provenance rows.
6. Marketplace upload, public readback, tag, and GitHub Release remain separate explicit approvals.

Any source, manifest, embedded note, or packaged-content edit after approval creates different bytes and requires a new candidate hash and proportionate requalification.

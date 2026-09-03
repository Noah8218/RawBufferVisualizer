# Raw Buffer Visualizer 2.0.6 Installed Review Candidate

Date: 2026-09-03 KST

## State

`2.0.6.0` is a stable-channel owner-review candidate. Official Visual Studio Marketplace Gallery API readback at `2026-09-03T09:29:14+09:00` returned public `2.0.5.0`, last updated `2026-09-01T15:14:49.537Z`. Because published versions are immutable, this candidate advances the package identity instead of overwriting 2.0.5.

There is no functional or compatibility change from the validated 2.0.5 behavior. The extension, provider, Classic visualizer, VSPackage registration, release announcement, README, English/Korean Overview, and release notes identify 2.0.6. Release build, aggregate self-tests, the ten-version OpenCvSharp/Emgu matrix, release communication, package guards, exact package installation, and installed runtime checks pass on the available serviced VS2022 and stable VS2026 hosts. Exact VS2022 17.9 runtime remains unverified because that host is not installed.

The reviewed source change set is committed and pushed on `agent/vs2022-17.9-compat`. No tag, Marketplace upload, publication, deployment, or manual CI dispatch was performed.

## Exact Review VSIX

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

| Property | Value |
| --- | --- |
| Length | `2,510,240` bytes |
| SHA-256 | `2ED03932358F45E6B9981DE830558CACD03E7677780BAB3DFA5F8593A0E77AEA` |
| Extension ID | `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f` |
| Manifest version | `2.0.6.0` |
| Install targets | Community, Professional, Enterprise x64 `[17.9,18.0)` |
| VSIX entries | `72` |
| Embedded release notes | `Raw Buffer Visualizer 2.0.6` |

The package guard rebuilt the candidate from the routed D-drive Release output and validated the composite VSSDK package, provider registrations, complete image-array identities, 17.9 SDK/threading boundary, target range, VSPackage `.pkgdef`, menu/tool-window registrations, and required VSIX contents.

## Installed Host Qualification

| Host | Installation path | Transition | Result |
| --- | --- | --- | --- |
| VS2022 Community `17.14.37516.0` | `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_419f0858\Extensions\evm50mus.zui` | In-place `2.0.5.0 -> 2.0.6.0` without uninstall, repair, or `/ResetSkipPkgs`; then clean reinstall of the same VSIX | Passed |
| VS2026 Community `18.8.12105.206` | `C:\Users\USER\AppData\Local\Microsoft\VisualStudio\18.0_19923728\Extensions\hfbyfgka.j2f` | In-place `2.0.4.0 -> 2.0.6.0` without uninstall, repair, or `/ResetSkipPkgs` | Passed |

Both installed roots contain all 71 package payload files other than the archive-only `[Content_Types].xml`; every installed file matches the frozen VSIX entry by SHA-256, with zero missing files and zero mismatches. `Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.6.0` found one current extension per host, valid registration payloads, no retired split extension, and no conflicting per-machine installation.

The exact installed VSIX passed 15 recorded scenarios: seven after the VS2022 in-place update, one core scenario after its clean reinstall, and seven after the VS2026 in-place update. Both hosts passed `MultiLibraryHybrid`, `AutomaticCollections`, true-cold `ImagePtrColdStart`, `ConcurrentDictionary`, `AutomaticVisionInspector`, `ReleaseAnnouncement`, and `EnvironmentCheck`. VS2022 clean reinstall additionally passed `MultiLibraryHybrid`. Expected per-item failures in the collection fixtures remained isolated and did not fail either scenario.

The first VS2022 cold-start attempts correctly stopped because the Tool Window was already persisted open; after closing it and choosing **Don't Save** for generated debug state, the true-cold scenario passed. The first `ConcurrentDictionary` automation expected the former synthetic title and timed out even though the installed viewer opened the correct object-name title. Only the test expectation was corrected to `concurrentImageDictionary[concurrent-snapshot]`; the frozen VSIX was not rebuilt or changed.

## Version-Only Change

- `RawBufferVisualizer.VisualStudio.Vssdk`, `RawBufferVisualizer.VisualStudio.Extensibility`, and `RawBufferVisualizer.VisualStudio.Classic` use package version `2.0.6` and numeric assembly/file version `2.0.6.0`.
- `source.extension.vsixmanifest` uses Marketplace identity version `2.0.6.0` without changing the extension ID or installation range.
- The in-product announcement, embedded release notes, changelog, README, Marketplace Overview, Korean review copy, release runbook, and Marketplace checklist identify the new candidate.
- Public 2.0.5 documentation and its exact local evidence remain preserved as history.
- Runtime pointer provenance, object-name recovery, collection/array handling, buffer validation, rendering, and compatibility code was not modified for the version transition.

## Checks Run

| Check | Result |
| --- | --- |
| Official Marketplace Gallery API readback | Passed; public `2.0.5.0`, last updated `2026-09-01T15:14:49.537Z` |
| `Bump-VisualStudioExtensionVersion.ps1 -Version 2.0.6` | Passed; canonical project, manifest, and VSPackage version sources updated; stale generated VSIX outputs invalidated |
| `Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.6` | Passed |
| Routed `dotnet build .\RawBufferVisualizer.sln -c Release` | Passed after correcting the test-only `NUGET_PACKAGES` trailing separator; 0 errors and 18 pre-existing `VSTHRD010` warnings |
| Aggregate `RawBufferVisualizer.Tests` | Passed |
| `SmokeLegacyImageCompatibility.ps1 -Configuration Release` | Passed five OpenCvSharp and five Emgu package versions, including pointer/pixel/Bitmap provenance assertions |
| `Publish-VisualStudioExtension.ps1 ... -BuildRoot <D:> -DirectoryBuildPropsPath <D:> -NoZip` under Windows PowerShell 5.1 | Passed package, registration, provider, content, and 17.9 compatibility guards |
| Exact artifact readback | Passed; manifest `2.0.6.0`, 72 entries, three amd64 `[17.9,18.0)` edition targets, embedded 2.0.6 notes, and recorded SHA-256 |
| VS2022 in-place update | Passed from installed `2.0.5.0` to exact `2.0.6.0` without uninstall, repair, or skipped-package reset |
| VS2022 clean reinstall | Passed with the same frozen VSIX; post-install `MultiLibraryHybrid` opened 9 documents with 0 errors |
| VS2026 in-place update | Passed from installed `2.0.4.0` to exact `2.0.6.0` without uninstall, repair, or skipped-package reset |
| Installed payload equality | Passed on both hosts; 71/71 installed files matched the frozen VSIX, 0 missing, 0 hash mismatches |
| Installed runtime matrix | Passed 15/15 result records across VS2022 `17.14.37516.0` and VS2026 `18.8.12105.206` |
| Installed 2.0.6 documentation media | Passed `IndustrialMarketplace` and `IndustrialDataTip` on VS2022 `17.14.37516.0`; all six tracked assets were regenerated from the resulting current-version captures and visually reviewed |
| Media metadata and hashes | Passed; GIFs are 960 x 532 at 4 fps for 3.0, 3.25, and 6.0 seconds; three PNGs are 1880 x 1040; exact SHA-256 values are recorded in `industrial-image-testing.md` |
| Marketplace dry run | Passed with the frozen 2.0.6 VSIX, English Overview, and six current media assets; no publication occurred |
| `Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.6.0` | Passed on both hosts; current registration payload valid and legacy/conflicting installs absent |
| `git diff --check` and final documentation consistency | Passed; no stale current-candidate 2.0.5 language, unexpected `.csproj.user` file, or whitespace error found |

The first routed build joined `NUGET_PACKAGES` and `opencvsharp4.runtime.win` without a path separator and failed to find `OpenCvSharpExtern.dll`. No product source was changed. Re-running with a trailing `\` on the D-drive NuGet root passed; this is test-environment evidence, not a product defect.

## Evidence

- Gallery API response: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\marketplace-gallery-readback.json`
- Build/package root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903`
- VS2022 in-place-update evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\installed-update-vs2022`
- VS2022 clean-reinstall evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\clean-reinstall-vs2022`
- VS2026 in-place-update evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\installed-update-vs2026`
- 2.0.5 pointer/name runtime report: `D:\OpenVisionLab-TestData\RawBufferVisualizer\pointer-provenance-20260902\REPORT.md`
- English Overview: [marketplace-overview-2.0.6.md](marketplace-overview-2.0.6.md)
- Korean review copy: [marketplace-overview-2.0.6.ko.md](marketplace-overview-2.0.6.ko.md)
- Marketplace release notes: [marketplace-release-notes-2.0.6.md](marketplace-release-notes-2.0.6.md)
- Current 2.0.6 media and dry-run evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.6\20260903\marketplace-media-2.0.6`

## Remaining Release Gates

1. Run or review CI for the pushed source before using the package as a public-release input; this branch push does not trigger the repository's `main`-only push workflow.
2. Exact VS2022 17.9 runtime remains unverified because that host is not installed. The manifest/build compatibility contract is retained, but the available-host result must not be presented as exact 17.9 runtime proof.
3. Marketplace upload, public readback, tag, GitHub Release, and deployment remain separate explicit approvals.

The VS2022 update began from an installed extension reporting `2.0.5.0`; the starting installation's downloadable Marketplace bytes were not independently hashed before the update. This does not affect equality of the installed 2.0.6 payload, which was verified against the frozen candidate on both hosts.

Any source, manifest, embedded note, or packaged-content edit after owner review creates different bytes and requires a new candidate hash and proportionate requalification.

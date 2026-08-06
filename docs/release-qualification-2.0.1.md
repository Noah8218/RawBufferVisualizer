# Release Qualification: 2.0.1

Last verified: 2026-08-06 KST.

## Release State

Marketplace Gallery metadata reports version `2.0.0.0`, but a cache-busted download of its `Microsoft.VisualStudio.Ide.Payload` still contains the exact former `1.0.53.0` VSIX. Because the public item already owns version `2.0.0.0`, the recovery release uses the higher manifest version `2.0.1.0` while preserving the existing extension identity.

Current public readback:

```text
Gallery version: 2.0.0.0
Gallery last updated: 2026-08-06T04:25:45.133Z
Downloaded payload manifest: 1.0.53.0
Downloaded payload size: 1,914,615 bytes
Downloaded payload SHA-256: E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\prepublish-public-readback-20260806
```

## Current 2.0.1 Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\candidate-marketplace-recovery-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Manifest version: 2.0.1.0
Size: 1,923,778 bytes
SHA-256: 5E1C112089C7BE39067BC2B17C7E3D75659575356F1E8EF9FA06A7BDA955A20B
Marketplace extension ID: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
Publisher: Noah Choi
Package entries: 94
```

This candidate changes the release identity and communication from `2.0.0` to `2.0.1`; it does not add a product feature or change the Marketplace extension ID.

## Verification Completed

- `dotnet restore RawBufferVisualizer.sln`: passed.
- Release solution build: passed with zero errors and the existing 18 VSTHRD010 warnings in `ImageTypeRecognizer.cs`.
- `RawBufferVisualizer.Tests` on `net8.0-windows`: passed.
- `Test-ReleaseCommunication.ps1 -ExpectedVersion 2.0.1`: passed.
- `Test-EnvironmentCheckContracts.ps1`: passed.
- `Publish-VisualStudioExtension.ps1`: passed the hybrid package and registration guards and produced the exact candidate above.
- `Publish-VisualStudioMarketplace.ps1 -DryRun`: passed; generated `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\marketplace\vs-publish.json` without publishing.
- Package inspection: manifest `2.0.1.0`, unchanged extension ID, 94 entries, exact size and hash recorded above.
- Candidate source commit `0d57e10` was pushed to `origin/main`; GitHub Actions run `31074717609` completed successfully.
- In-place installation over the existing extension completed on VS2022 Community `17.14.37516.0` and VS2026 Community `18.8.12023.21` without uninstall, repair, or registration reset.
- `Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.1.0`: passed on both IDE instances; installed manifest version and registration payload are valid and no legacy configuration key is visible.
- All seven package-owned `RawBufferVisualizer*.dll` assemblies in the exact candidate match both installed copies by byte length and SHA-256: 14 comparisons, zero mismatches.
- ReleaseAnnouncement, EnvironmentCheck, and AutomaticCollections installed-VSIX scenarios passed on both IDEs. Each host exposed one Open command and one Automatic Inspector command with zero package-protocol errors.
- MultiLibraryHybrid passed on both IDEs with 9 documents, 0 product errors, 8 automatic candidates opened, the registered Bitmap path opened, one Open command, one scan command, and zero package-protocol errors.

Installed evidence roots:

```text
VS2022: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\installed-vs2022
VS2022 MultiLibraryHybrid: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\manual-vs2022
VS2026: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\installed-vs2026
VS2026 MultiLibraryHybrid: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\manual-vs2026
```

Two initial VS2022 MultiLibraryHybrid automation attempts timed out while selecting the visible Bitmap visualizer glyph because the harness fallback coordinate did not resolve the glyph element. A direct user-equivalent click on that same visible glyph immediately completed the handoff and the full scenario passed. This is a test-harness input-selection defect, not a product handoff failure; the failed attempts and click logs remain under `installed-vs2022`.

Test `TEMP` and `TMP` were routed to `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\validation-20260806`; repository build output remains backed by the existing D-drive `.build` junction.

## Remaining Publication Gates

- Obtain explicit final approval before changing the public Marketplace item.
- After publication, verify Gallery version `2.0.1.0`, downloadable VSIX length/SHA-256, manifest `2.0.1.0`, rendered Overview, and an actual Visual Studio update offer.

## Durable Checkpoint

Status: Complete

Scope: Pre-publication qualification of the exact `2.0.1.0` Marketplace recovery candidate, including version/identity, source build/tests, package guards, pushed source/CI, in-place installation, package-to-install equality, and installed runtime scenarios on VS2022 and VS2026.

Acceptance criteria: version is higher than Gallery `2.0.0.0` -> pass; extension identity is unchanged -> pass; source build/tests and package guards -> pass; exact installed update on both supported IDE generations -> pass; candidate/install assembly equality -> 14/14 pass; required installed runtime scenarios -> pass; pushed source and CI -> pass.

Verification: commands and outputs listed above.

Evidence: exact candidate and D-drive validation/readback paths listed above.

Boundary / next dependency: This proves local release readiness, not Marketplace publication or propagation. Public upload remains owner-controlled and requires final approval after the existing item upload form is checked; the public payload must then be read back and matched to the exact candidate. The two retained test IDE sessions contain only transient debug-solution state and have not been discarded without owner confirmation.

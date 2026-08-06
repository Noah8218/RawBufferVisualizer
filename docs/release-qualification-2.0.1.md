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

Test `TEMP` and `TMP` were routed to `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\validation-20260806`; repository build output remains backed by the existing D-drive `.build` junction.

## Pending Release Gates

- Close all Visual Studio windows, install the exact candidate over the currently installed `2.0.0.0`, and verify installed manifest/registration plus the core installed-VSIX smoke on supported IDEs.
- Commit and push the exact source, then confirm successful CI for that commit.
- Obtain explicit final approval before changing the public Marketplace item.
- After publication, verify Gallery version `2.0.1.0`, downloadable VSIX length/SHA-256, manifest `2.0.1.0`, rendered Overview, and an actual Visual Studio update offer.

## Durable Checkpoint

Status: Incomplete

Scope: Marketplace recovery version bump, release communication, exact `2.0.1.0` candidate packaging, public mismatch readback, and no-write Marketplace validation.

Acceptance criteria: version is higher than Gallery `2.0.0.0` -> pass; extension identity is unchanged -> pass; source build/tests and package guards pass -> pass; exact installed update -> pending; pushed source and CI -> pending; public `2.0.1.0` payload/readback -> pending.

Verification: commands and outputs listed above.

Evidence: exact candidate and D-drive validation/readback paths listed above.

Boundary / next dependency: Visual Studio is currently running, so installed update verification has not been executed. Public publication remains owner-controlled and requires final approval after the upload form is checked.

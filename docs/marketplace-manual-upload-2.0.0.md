# Marketplace Manual Upload: 2.0.0

Use this checklist only after the owner decides to publish version 2.0.0 **and** one consolidated current-source candidate has passed the complete release matrix, is committed/pushed, and has successful CI. The public Marketplace item currently remains `openvisionlab.RawBufferVisualizer` version `1.0.53.0`.

## Current Blocker: Source Commit, CI, And Owner Approval

Do not upload the preserved P0 or superseded pre-P0 candidates. The exact consolidated candidate below passed the complete local matrix, but its matching source is still uncommitted and unpushed and therefore has no CI result for the final change set.

Locally qualified exact candidate, held until the blocker above is cleared:

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-connect-doctor-docs-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 2.0.0.0
Size: 1,923,731 bytes
SHA-256: D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C
Disposition: local qualification complete; do not upload before commit/push, successful CI, and explicit owner approval
```

After commit/push and successful CI, append the exact source commit and CI run to this section before opening the Marketplace portal. Do not use `C:\Git\RawBufferVisualizer\artifacts\publish`, a preserved older candidate, or a fresh rebuild as a substitute for the exact bytes above.

## Marketplace Text

- English Overview to upload: `C:\Git\RawBufferVisualizer\docs\marketplace-overview-2.0.0.md`
- Korean review copy only; do not upload: `C:\Git\RawBufferVisualizer\docs\marketplace-overview-2.0.0.ko.md`
- Release notes: `C:\Git\RawBufferVisualizer\docs\marketplace-release-notes-2.0.0.md`

The Overview intentionally tells ordinary users to install the extension in a supported Visual Studio.

## Manual Update Steps

1. Open the existing `openvisionlab` publisher and edit the existing `RawBufferVisualizer` item; do not create a second Marketplace identity.
2. Upload only the exact consolidated VSIX recorded above after the blocker is cleared.
3. Replace the Overview with the English file above.
4. Enter the curated 2.0.0 release notes.
5. Confirm the Marketplace preview shows version `2.0.0.0`, the existing extension ID, Visual Studio 2022/2026 support wording, and no proprietary vendor support claim.
6. Publish only after the final Marketplace confirmation page is correct.

## Required Readback After Publication

After propagation, record the Gallery version, downloadable VSIX length/SHA-256, downloaded manifest version, and rendered Overview. The public values must match the approved consolidated candidate and English source exactly, allowing only Marketplace's normal rendering transformation.

Then run the installed update check on a profile that received the public update from `1.0.53.0` without uninstall, registration repair, or `/ResetSkipPkgs`:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.0.0
```

If the public asset hash differs, the manifest is not `2.0.0.0`, or the update requires repair/reinstall, stop and record the publication as incomplete.

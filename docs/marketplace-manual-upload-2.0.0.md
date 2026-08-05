# Marketplace Manual Upload: 2.0.0

Use this checklist only after the owner decides to publish version 2.0.0. The public Marketplace item currently remains `openvisionlab.RawBufferVisualizer` version `1.0.53.0`.

## Upload This Exact VSIX

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

Before selecting the file, verify:

```powershell
$vsix = 'D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\RawBufferVisualizer.VisualStudio.Extensibility.vsix'
Get-Item -LiteralPath $vsix | Select-Object FullName, Length
Get-FileHash -Algorithm SHA256 -LiteralPath $vsix
```

Expected values:

```text
Version: 2.0.0.0
Size: 1,914,538 bytes
SHA-256: 2A6D94016B03430BDF2EF5ECCF6282D32896C02AEFB3A9EB8F5519AFE4B13512
```

Do not upload a VSIX from `C:\Git\RawBufferVisualizer\artifacts\publish` or rebuild the frozen directory. Those paths may contain an older package.

## Marketplace Text

- English Overview to upload: `C:\Git\RawBufferVisualizer\docs\marketplace-overview-2.0.0.md`
- Korean review copy only; do not upload: `C:\Git\RawBufferVisualizer\docs\marketplace-overview-2.0.0.ko.md`
- Release notes: `C:\Git\RawBufferVisualizer\docs\marketplace-release-notes-2.0.0.md`

The Overview intentionally tells ordinary users to install the extension in a supported Visual Studio. It does not advertise FFmpeg, .NET SDK, VSSDK, OpenCV packages, or camera SDKs as extension requirements.

## Manual Update Steps

1. Open the existing `openvisionlab` publisher and edit the existing `RawBufferVisualizer` item; do not create a second Marketplace identity.
2. Upload the exact frozen VSIX above.
3. Replace the Overview with the English file above.
4. Enter the curated 2.0.0 release notes.
5. Confirm the Marketplace preview shows version `2.0.0.0`, the existing extension ID, Visual Studio 2022/2026 support wording, and no proprietary vendor support claim.
6. Publish only after the final Marketplace confirmation page is correct.

## Required Readback After Publication

After propagation, record the Gallery version, downloadable VSIX length/SHA-256, downloaded manifest version, and rendered Overview. The public values must match the frozen candidate and English source exactly, allowing only Marketplace's normal rendering transformation.

Then run the installed update check on a profile that received the public update from `1.0.53.0` without uninstall, registration repair, or `/ResetSkipPkgs`:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Git\RawBufferVisualizer\scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 2.0.0.0
```

If the public asset hash differs, the manifest is not `2.0.0.0`, or the update requires repair/reinstall, stop and record the publication as incomplete.

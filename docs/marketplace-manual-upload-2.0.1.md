# Marketplace Manual Upload: 2.0.1

Use this checklist to repair the existing `openvisionlab.RawBufferVisualizer` listing. Do not create another Marketplace item and do not upload a `2.0.0.0` package: Gallery metadata already reports `2.0.0.0`.

## Exact Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.1\candidate-marketplace-recovery-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Version: 2.0.1.0
Size: 1,923,778 bytes
SHA-256: 5E1C112089C7BE39067BC2B17C7E3D75659575356F1E8EF9FA06A7BDA955A20B
Extension ID: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
Publisher: Noah Choi
```

Do not substitute the public `1.0.53.0` payload, any preserved `2.0.0.0` candidate, or a later rebuild. A different size or SHA-256 requires a new qualification record.

## Marketplace Text

- English Overview: `docs\marketplace-overview-2.0.1.md`
- Release notes: `docs\marketplace-release-notes-2.0.1.md`
- Keep the existing public GIF and industrial screenshots referenced by the Overview.

## Required Gates Before Upload

- Exact candidate installed-update verification is complete.
- Matching source is committed and pushed.
- CI succeeds for that exact commit.
- Final Marketplace form shows `2.0.1.0`, the existing extension ID, and the expected Overview/release notes.
- The owner explicitly approves the final public action.

## Manual Update Steps

1. Open publisher `openvisionlab` and edit the existing `RawBufferVisualizer` item.
2. Select only the exact VSIX recorded above.
3. Replace the Overview with the English 2.0.1 file and enter the curated 2.0.1 release notes.
4. Confirm the preview shows version `2.0.1.0` and the unchanged extension identity.
5. Stop before the final public action and obtain explicit owner approval.
6. Publish only after that approval.

## Required Public Readback

After propagation, the Gallery version must be `2.0.1.0`; the downloadable VSIX must be 1,923,778 bytes with SHA-256 `5E1C112089C7BE39067BC2B17C7E3D75659575356F1E8EF9FA06A7BDA955A20B`; and its manifest must be `2.0.1.0`. Verify the rendered Overview and confirm Visual Studio offers the update from the affected earlier installation state.

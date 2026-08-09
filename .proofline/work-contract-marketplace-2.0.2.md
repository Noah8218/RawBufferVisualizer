# Raw Buffer Visualizer 2.0.2 Marketplace readiness work contract

## User goal

Determine whether the exact local 2.0.2 candidate is ready for Marketplace publication, replace the complete Marketplace Overview and public media set including the GIF with current-candidate material, and provide a separate Korean review copy before any upload.

User correction, 2026-08-09: the Korean review rendered the old public `main` assets because its image URLs were remote. Rebuild the media from the current 2.0.2 UI, make the owner-review copy resolve the local current files, and add a real breakpoint visualizer-icon opening sequence using the licensed industrial PCB image instead of a procedural/synthetic fixture.

User correction, 2026-08-09 (approved mockup): replace the grayscale Doctor demonstration with a real `BGR24` wrong-stride fixture whose top correction restores the same color scene; retain the Locals visualizer GIF; add a second breakpoint GIF using a different license-cleared industrial photograph and the code editor DataTip magnifier on a `Mat` or `Bitmap`; then deliver refreshed English and Korean Overview copies for owner review.

User authorization, 2026-08-09: the refreshed copy and media are approved for Git commit/push. Marketplace upload remains a separate owner action.

## Non-negotiable requirements

- Audit readiness conservatively against the exact frozen 2.0.2 VSIX and current repository state.
- Review every Overview image/GIF reference and visually inspect every public-facing media file.
- Replace the workflow GIF with a fresh, short, legible capture from the current installed candidate.
- Show a real Break Mode variable row, actual cursor hover, the Raw Buffer Visualizer selection, and the resulting color industrial image in the docked viewer.
- Use only the two reviewed CC0 industrial sources in the new public GIFs; no procedural stripe/gradient image may appear.
- Keep the main GIF within 5-8 seconds and add a shorter breakpoint-open GIF for the registered-image entry workflow.
- Produce the authoritative English Marketplace Overview and a separate Korean translation for user review.
- Keep user-facing copy focused on Visual Studio users and supported 2D debugging workflows.
- Do not upload, publish, commit, or push without a later explicit user instruction.
- The Doctor before/after media must prove color `BGR24` recovery rather than imply that a grayscale source can regain discarded channels.
- The new DataTip GIF must show the cursor over an initialized code-editor object, the debugger visualizer magnifier/entry, and the different industrial image opening in the docked viewer.
- Record the second photograph's source page, license, downloaded-file hash, and capture usage before it becomes public media.

## Checkpoints

1. Complete - audited release qualification, Overview, release notes, README, checklist, and all referenced media.
2. Complete - recaptured the public PNG/GIF set from the installed 2.0.2 candidate with the user-requested breakpoint visualizer flow and industrial source.
3. Complete - updated the English Overview and made the Korean owner-review copy render current local media immediately.
4. Complete - reran installed-flow, link, media, hash, copy, release-communication, and package dry-run checks and issued the revised publish-readiness verdict.
5. Complete - linked the revised Overview and all current media in the final handoff for owner approval.
6. Complete - implemented the approved color-Doctor fixture; aggregate tests and installed diagnosis ranked BGR24 2448 x 2048 stride 7424 first and restored color.
7. Complete - captured the approved code-editor DataTip visualizer flow with the second CC0 industrial image and verified OpenCvSharp Mat 1280 x 720 BGR24 handoff.
8. Complete - regenerated all affected PNG/GIF assets and synchronized the English/Korean Overview copies.
9. Complete - reran installed-flow, frame, copy, link, hash, release-communication, and six-asset Marketplace dry-run checks; no publish occurred.

## Verification plan

- Confirm exact VSIX version, length, hash, manifest range, and installed-host evidence.
- Resolve every local/remote media link used by the Overview and README.
- Inspect image/GIF dimensions, frame count, duration, file size, privacy, crop, and claimed feature accuracy.
- Render or extract representative GIF frames and inspect them visually.
- Re-run release communication and Marketplace checklist guards that do not publish.
- Record fresh media source paths and hashes on the D: evidence drive and in reusable documentation.

## Known risks or blockers

- Existing media may predate the final Clear-reset candidate even when its visible workflow is otherwise unchanged.
- Desktop capture can be invalidated by unrelated windows or private information.
- The public Gallery still serves 2.0.1 until the owner later uploads 2.0.2 and propagation completes.
- Exact-current VS2022 17.9 runtime was not repeated after the temporary 17.9 host was removed; the qualified boundary must remain explicit in internal release evidence.
- The current public Marketplace Overview still shows `What's New In 2.0.0` and a Visual Studio 2022 `17.14+` floor. Public `main` still serves the older four media bytes and does not yet contain the new breakpoint GIF, so source/media must reach `main` before the new Overview is uploaded.
- The exact VSIX contains `LICENSE` and `THIRD-PARTY-NOTICES.md`, and the Overview links both, but `extension.vsixmanifest` does not declare a `<License>` metadata element. Adding it is a recommended Marketplace metadata improvement that would change the frozen VSIX bytes and require a new candidate decision.

## User approval

- Final English Overview, Korean review copy, and public media set approved for commit/push.
- The owner will perform the Marketplace upload after the GitHub source/media and CI gates pass.

## Current readiness verdict

The final copy/media set is approved and committed to the compatibility branch and public `main`; CI passed for the same commit. The owner may now perform the manual Marketplace upload with the exact frozen VSIX and English Overview. Public package/Overview readback and the real `2.0.1 -> 2.0.2` update path remain post-upload gates. The current final UI bytes were not installed again on the removed Visual Studio 2022 17.9 host; public copy states the exact installed hosts and the 17.9 build/package floor without claiming an exact-current 17.9 installed run.

## Evidence

```text
Exact VSIX: D:\OpenVisionLab-TestData\RawBufferVisualizer\clear-inspector-reset-2.0.2\20260809\release-candidate-final\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
VSIX SHA-256: 46A84EFC5AB685139B3C16B0DA0C35B6277E359D05E3E6198CA9925D737E5D57
Media/GIF audit: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\encoded
Current installed Locals/color-Doctor capture: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-marketplace
Current installed DataTip capture: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-datatip-run1
Installed capture results: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-marketplace\IndustrialMarketplace-installed-vsix.json; D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-media-2.0.2\20260809-color-datatip\industrial-datatip-run1\IndustrialDataTip-installed-vsix.json
Public main byte audit: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-readiness-2.0.2\20260809\public-main-audit
Dry-run manifest: D:\OpenVisionLab-TestData\RawBufferVisualizer\marketplace-readiness-2.0.2\20260809-color-datatip\vs-publish-six-assets.json
```

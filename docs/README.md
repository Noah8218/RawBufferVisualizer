# Maintainer Documentation Index

This directory is the entry point for continuing Raw Buffer Visualizer work in a new conversation. The public product guide remains the repository root [README](../README.md).

## Current Resume Point

As of 2026-08-05 KST, Visual Studio Marketplace serves the exact qualified vendor-safe `1.0.53.0` package: 1,914,615 bytes with SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`. Gallery metadata, the public download, manifest version, and rendered Overview were read back after publication; the evidence is under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018`. Do not rebuild or upload another binary under that version. Read [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), [release-qualification-1.0.53.md](release-qualification-1.0.53.md), and [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md) before release work.

Version transition: matching product source/evidence commit `7ab84b7` is pushed and unchanged `1.0.53` is now the public 1.x stabilization bridge. Start the separate 2.0 development line with the vendor-neutral buffer-shape compatibility matrix. Do not relabel the qualified binary as `2.0.0`; a version change creates a different package and requires fresh qualification.

The previous public `1.0.52` package uses the stable `17.14` Extensibility SDK and declares Visual Studio 2022 `17.14+` plus stable Visual Studio 2026 `18.x`. That exact package passed ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, registration, and protocol checks on VS2022 Community `17.14.33` and VS2026 Community `18.8.2`. Document activation/removal/disposal belongs to `RawBufferDocumentWorkspace`, claimed handoff terminal policy belongs to `ClaimedHandoffOpenCoordinator`, and active file-backed documents hold snapshot-directory leases. The separate-PC in-place update from exact public `1.0.50.0` to `1.0.52.0` was not recorded before publication and remains historical validation debt.

The Windows-reinstall functional recheck is recorded separately in [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md). Qualified `1.0.53` repairs the two stale layout harness assumptions, adds page-budgeted large sampled previews, implements the reviewed essential-only Environment Check and panel toggles, and completes the vendor-neutral Connect Your Buffer workflow. Required and optional utilities plus the in-product behavior contract are listed in [development-prerequisites.md](development-prerequisites.md).

The first 2D camera-SDK experiment produced historical runtime evidence at exact pylon Software Suite `8.1.0.16743` and `26.07.2.18500` test points. That technical result was not legal clearance, so the direct adapter was removed from active source. Every future camera/frame-grabber/board SDK integration remains blocked until [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) passes; public Marketplace `1.0.53` contains no direct proprietary adapter.

## Required Reading Order

1. [AGENTS.md](../AGENTS.md) - repository rules and new-session orientation.
2. [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md) - current implementation/release snapshot, completed work, gaps, and exact next step.
3. [PRODUCT_DIRECTION_AND_ROADMAP.md](PRODUCT_DIRECTION_AND_ROADMAP.md) - product identity, scope boundaries, UX rules, and roadmap.
4. [ARCHITECTURE_AND_VALIDATION.md](ARCHITECTURE_AND_VALIDATION.md) - module map, debugger/viewer data flow, compatibility strategy, validation evidence, and commands.

After those four documents, read only the task-specific references below.

## Task Routing

| Task | Read |
| --- | --- |
| Build, install, or debug the Visual Studio extension | [visual-studio-integration.md](visual-studio-integration.md), [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md), [vsix-package-registration.md](vsix-package-registration.md) |
| Restore a development PC or check required utilities | [development-prerequisites.md](development-prerequisites.md), [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md) |
| Prepare or publish a Marketplace update | [release-runbook.md](release-runbook.md), [marketplace-checklist.md](marketplace-checklist.md) |
| Review public 1.0.53 evidence or the previous 1.0.52 baseline | [release-qualification-1.0.53.md](release-qualification-1.0.53.md), [release-qualification-1.0.52.md](release-qualification-1.0.52.md), [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md), then the preserved failed [1.0.51 record](release-qualification-1.0.51.md) |
| Validate or generate very large images | [large-image-samples.md](large-image-samples.md) |
| Research, qualify, or add a camera/frame-grabber integration | [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) first, then [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md) and [sdk-adapter-roadmap.md](sdk-adapter-roadmap.md) |
| Review the removed Basler experiment's historical evidence | [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md) |
| Review Image Watch-inspired UX | [image-watch-ux-analysis.md](image-watch-ux-analysis.md) |
| Prepare a short demo | [demo-recording-guide.md](demo-recording-guide.md) |
| Check or manually publish the current release text | [marketplace-manual-upload-1.0.53.md](marketplace-manual-upload-1.0.53.md), [marketplace-overview-1.0.53.md](marketplace-overview-1.0.53.md), Korean review copy [marketplace-overview-1.0.53.ko.md](marketplace-overview-1.0.53.ko.md), [marketplace-release-notes-1.0.53.md](marketplace-release-notes-1.0.53.md) |
| Work on buffer interpretation diagnosis | [buffer-doctor-design.md](buffer-doctor-design.md) |
| Work on user type mapping | [smart-type-mapper-design.md](smart-type-mapper-design.md) |
| Work on automatic discovery of unregistered image types | [automatic-vision-inspector.md](automatic-vision-inspector.md) |

## Source-Of-Truth Order

When documents conflict, use this order:

1. Current source, generated VSIX manifest, automated checks, and observed runtime behavior.
2. [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), when its `Last verified` date and baseline commit still match.
3. [PRODUCT_DIRECTION_AND_ROADMAP.md](PRODUCT_DIRECTION_AND_ROADMAP.md) and [ARCHITECTURE_AND_VALIDATION.md](ARCHITECTURE_AND_VALIDATION.md).
4. The public [README](../README.md), release runbook, and Marketplace checklist.
5. Historical release notes and research documents.

`visual-studio-integration.md` and `image-watch-ux-analysis.md` preserve design history. They are useful context, but their early prototype wording must not override the current implementation or handoff.

## Handoff Maintenance Rule

Update the handoff when any of these change:

- public or source version;
- baseline commit or branch;
- Marketplace/GitHub release state;
- supported input, collection, or pixel format;
- debugger transfer or viewer architecture;
- validation commands or recorded evidence;
- known limitation, release blocker, or next priority.

Do not record a check as passed unless it was run or externally verified for the stated version. Separate local package readiness, CI success, Marketplace publication, and manual installed-extension validation because they are different states.

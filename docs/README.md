# Maintainer Documentation Index

This directory is the entry point for continuing Raw Buffer Visualizer work in a new conversation. The public product guide remains the repository root [README](../README.md).

## Current Resume Point

The current local review candidate is `2.0.7.0` on `agent/vs2022-17.9-compat`; feature/copy/media commit `33b0a0b4855ce02aeed05f0726bf0071418611fa` is followed by the handoff closure in the same owner-authorized branch-push batch. Official Gallery API readback on 2026-09-03 confirms public `2.0.6.0`. The new candidate adds signed 32-bit single-channel visualization for OpenCvSharp `CV_32SC1`, Emgu CV `Cv32S` C1, mapped `int[]`, and vendor-neutral pointer/snapshot sources without changing the Visual Studio `17.9+` support floor.

The exact local 2.0.7 VSIX passed build, aggregate tests, ten-version OpenCvSharp/Emgu compatibility, package guards, installed direct/automatic Int32 industrial scenarios on VS2022 `17.14.37516.0`, and full 540/900/1160 px layout smoke. Confirm the remote branch equals local HEAD before external use. Stable VS2026 2.0.7 runtime, exact VS2022 17.9 runtime, CI, Marketplace upload/readback, tag, and GitHub Release remain separate gates. Read [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), [release-qualification-2.0.7.md](release-qualification-2.0.7.md), and the public-baseline [release-qualification-2.0.6.md](release-qualification-2.0.6.md) before release work.

Version transition: public `2.0.6.0` remains immutable; local `2.0.7.0` is the next stable patch candidate because it changes the supported pixel-format contract. The owner approved the English/Korean 2.0.7 copy and the two installed Int32 qualification captures for the branch release batch on 2026-09-03.

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
| Qualify Visual Studio 2022 17.9 compatibility | [vs2022-17.9-compatibility-candidate.md](vs2022-17.9-compatibility-candidate.md), then [visual-studio-integration.md](visual-studio-integration.md) and [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md) |
| Restore a development PC or check required utilities | [development-prerequisites.md](development-prerequisites.md), [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md) |
| Prepare or publish a Marketplace update | [release-qualification-2.0.7.md](release-qualification-2.0.7.md), [release-runbook.md](release-runbook.md), [marketplace-checklist.md](marketplace-checklist.md), [marketplace-overview-2.0.7.md](marketplace-overview-2.0.7.md), [Korean Overview review copy](marketplace-overview-2.0.7.ko.md), [marketplace-release-notes-2.0.7.md](marketplace-release-notes-2.0.7.md) |
| Review previous 2.0.x release baselines | [release-qualification-2.0.5.md](release-qualification-2.0.5.md), [release-qualification-2.0.4.md](release-qualification-2.0.4.md), [release-qualification-2.0.3.md](release-qualification-2.0.3.md), then earlier records as needed |
| Validate or generate very large images | [large-image-samples.md](large-image-samples.md) |
| Test or capture with a real industrial photograph | [industrial-image-testing.md](industrial-image-testing.md), then [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md) |
| Research, qualify, or add a camera/frame-grabber integration | [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) first, then [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md) and [sdk-adapter-roadmap.md](sdk-adapter-roadmap.md) |
| Work on the vendor-neutral 2.0 buffer contract or Connect Doctor | [vendor-neutral-buffer-compatibility-matrix.md](vendor-neutral-buffer-compatibility-matrix.md), then [smart-type-mapper-design.md](smart-type-mapper-design.md), [buffer-doctor-design.md](buffer-doctor-design.md), and [automatic-vision-inspector.md](automatic-vision-inspector.md) |
| Review the removed Basler experiment's historical evidence | [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md) |
| Review Image Watch-inspired UX | [image-watch-ux-analysis.md](image-watch-ux-analysis.md) |
| Prepare a short demo | [demo-recording-guide.md](demo-recording-guide.md) |
| Check the owner-controlled 2.0.7 Marketplace package and copy | [release-qualification-2.0.7.md](release-qualification-2.0.7.md), [marketplace-overview-2.0.7.md](marketplace-overview-2.0.7.md), [Korean Overview review copy](marketplace-overview-2.0.7.ko.md), and [marketplace-release-notes-2.0.7.md](marketplace-release-notes-2.0.7.md) |
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

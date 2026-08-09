# Maintainer Documentation Index

This directory is the entry point for continuing Raw Buffer Visualizer work in a new conversation. The public product guide remains the repository root [README](../README.md).

## Current Resume Point

The current `2.0.2.0` source and public media are on `agent/vs2022-17.9-compat` and `main`. The candidate uses a `net472` VSSDK 17.9 in-process package plus a `net8.0-windows8.0` Extensibility 17.9 out-of-process provider. The frozen 2,493,359-byte VSIX, SHA-256 `46A84EFC5AB685139B3C16B0DA0C35B6277E359D05E3E6198CA9925D737E5D57`, includes always-on bounded exact-Mat collection discovery, the responsive native-icon Tool Window, a complete Clear presentation reset, and current installed-candidate industrial media. Current-source build, tests, package guards, 320-1160 px layout/splitter checks, and exact-candidate installed scenarios passed on Visual Studio 2022 Community `17.14.37516.0` and Visual Studio 2026 Community `18.8.12023.21`. The removed 17.9 host was not reinstalled; current 17.9 evidence is the VSSDK build/package contract plus earlier runtime proof for the unchanged package architecture. The public Marketplace package remains `2.0.1.0` until the owner uploads and verifies `2.0.2.0`. See [release-qualification-2.0.2.md](release-qualification-2.0.2.md) and the historical architecture qualification in [vs2022-17.9-compatibility-candidate.md](vs2022-17.9-compatibility-candidate.md).

The last recorded Marketplace Gallery state is public `2.0.1.0`. Its exact package, source/package/CI checks, and installed verification are recorded in [release-qualification-2.0.1.md](release-qualification-2.0.1.md). Read [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), [release-qualification-2.0.2.md](release-qualification-2.0.2.md), and [release-qualification-2.0.1.md](release-qualification-2.0.1.md) before release work.

Version transition: the complete 2.0 feature line remains qualified by the `2.0.0.0` evidence, public `2.0.1.0` is the Marketplace recovery release, and source/media for release-qualified `2.0.2.0` are committed and pushed for the owner-controlled Marketplace update.

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
| Prepare or publish a Marketplace update | [release-qualification-2.0.2.md](release-qualification-2.0.2.md), [release-runbook.md](release-runbook.md), [marketplace-checklist.md](marketplace-checklist.md), [marketplace-overview-2.0.2.md](marketplace-overview-2.0.2.md), [Korean Overview review copy](marketplace-overview-2.0.2.ko.md), [marketplace-release-notes-2.0.2.md](marketplace-release-notes-2.0.2.md) |
| Review local 2.0.2 qualification, 2.0.1 recovery, or previous baselines | [release-qualification-2.0.2.md](release-qualification-2.0.2.md), [release-qualification-2.0.1.md](release-qualification-2.0.1.md), [release-qualification-2.0.0.md](release-qualification-2.0.0.md), then earlier records as needed |
| Validate or generate very large images | [large-image-samples.md](large-image-samples.md) |
| Test or capture with a real industrial photograph | [industrial-image-testing.md](industrial-image-testing.md), then [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md) |
| Research, qualify, or add a camera/frame-grabber integration | [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) first, then [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md) and [sdk-adapter-roadmap.md](sdk-adapter-roadmap.md) |
| Work on the vendor-neutral 2.0 buffer contract or Connect Doctor | [vendor-neutral-buffer-compatibility-matrix.md](vendor-neutral-buffer-compatibility-matrix.md), then [smart-type-mapper-design.md](smart-type-mapper-design.md), [buffer-doctor-design.md](buffer-doctor-design.md), and [automatic-vision-inspector.md](automatic-vision-inspector.md) |
| Review the removed Basler experiment's historical evidence | [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md) |
| Review Image Watch-inspired UX | [image-watch-ux-analysis.md](image-watch-ux-analysis.md) |
| Prepare a short demo | [demo-recording-guide.md](demo-recording-guide.md) |
| Check the owner-controlled 2.0.2 Marketplace package and copy | [release-qualification-2.0.2.md](release-qualification-2.0.2.md), [marketplace-overview-2.0.2.md](marketplace-overview-2.0.2.md), [Korean Overview review copy](marketplace-overview-2.0.2.ko.md), and [marketplace-release-notes-2.0.2.md](marketplace-release-notes-2.0.2.md) |
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

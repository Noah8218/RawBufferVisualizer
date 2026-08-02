# Maintainer Documentation Index

This directory is the entry point for continuing Raw Buffer Visualizer work in a new conversation. The public product guide remains the repository root [README](../README.md).

## Current Resume Point

As of 2026-08-02 KST, source and the exact local candidate are `1.0.52.0`, while the public Marketplace still serves `1.0.50.0`. The preserved `1.0.51.0` VSIX is a failed candidate: after a user-approved full VS2026 reinstall removed the historical per-machine conflict, its registered Bitmap provider could not activate on stable VS2026 `18.8.2`. Do not publish or overwrite that original artifact. Read [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), [release-qualification-1.0.51.md](release-qualification-1.0.51.md), and [release-qualification-1.0.52.md](release-qualification-1.0.52.md) before release work.

The exact `1.0.52` candidate uses the stable `17.14` Extensibility SDK and declares Visual Studio 2022 `17.14+` plus stable Visual Studio 2026 `18.x`. The same SHA-256 package passed ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, registration, and protocol checks on VS2022 Community `17.14.33` and VS2026 Community `18.8.2`. Document activation/removal/disposal now belongs to `RawBufferDocumentWorkspace`, claimed handoff terminal policy belongs to `ClaimedHandoffOpenCoordinator`, and active file-backed documents hold snapshot-directory leases. Marketplace metadata and the dry run are ready. Publication remains blocked until a separate PC proves an update from exact public `1.0.50.0` to the unchanged `1.0.52` VSIX without uninstall, repair, or `/ResetSkipPkgs`.

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
| Prepare or publish a Marketplace update | [release-runbook.md](release-runbook.md), [marketplace-checklist.md](marketplace-checklist.md) |
| Review the current 1.0.52 candidate and release evidence | [release-qualification-1.0.52.md](release-qualification-1.0.52.md), then the preserved failed [1.0.51 record](release-qualification-1.0.51.md) |
| Validate or generate very large images | [large-image-samples.md](large-image-samples.md) |
| Research, qualify, or add a camera/frame-grabber integration | [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md), [sdk-adapter-roadmap.md](sdk-adapter-roadmap.md) |
| Review Image Watch-inspired UX | [image-watch-ux-analysis.md](image-watch-ux-analysis.md) |
| Prepare a short demo | [demo-recording-guide.md](demo-recording-guide.md) |
| Check the current release text | [marketplace-overview-1.0.52.md](marketplace-overview-1.0.52.md), [marketplace-release-notes-1.0.52.md](marketplace-release-notes-1.0.52.md) |
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

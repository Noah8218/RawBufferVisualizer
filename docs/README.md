# Maintainer Documentation Index

This directory is the entry point for continuing Raw Buffer Visualizer work in a new conversation. The public product guide remains the repository root [README](../README.md).

## Current Resume Point

Public Marketplace version `2.0.8.0` has a field-reported debugger-RPC failure while opening `6768 x 3225 Mono8` OpenCvSharp Mat and ImagePtr values on Visual Studio 2022 `17.9.34902.65`. The current source and exact local candidate are `2.0.9.0`. Version 2.0.9 routes pointer-backed OpenCvSharp Mat, Emgu CV Mat, ImagePtr, and RawBufferView payloads of at least 8 MiB through checked live process-memory reads, and inferred ROI spans now end at the final pixel row instead of reading nonexistent trailing padding.

The exact 2.0.9 package passed the reported-size three-source workflow on Visual Studio Community 2022 `17.9.34902.65` and serviced Community 2022 `17.14.37516.0`. Each host opened three documents with zero errors, verified the expected pixels and pointer provenance, and changed all live rows to `UNAVAILABLE` after process exit. The same final bytes passed the pinned registered-open workflow on those hosts and stable Community 2026 `18.9.12128.139`; all 71 installed package entries matched the VSIX on every host. The final 18.9 reported-size run remains unexecuted because repeated Visual Studio starts did not register DTE or launch the debuggee. The five-version OpenCvSharp plus five-version Emgu CV matrix and the installed 17.9 release-announcement workflow passed.

Visible debugger-RPC failures no longer copy a localized Visual Studio exception message into the image row. They identify the transfer operation, source or chunk, byte range when applicable, exception type, and HRESULT; the original exception and stack remain available only in the local support report. The final VSIX binary contains the technical diagnostic strings and does not contain the reported `평가 시간 초과` text. Read [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md), [release-qualification-2.0.9.md](release-qualification-2.0.9.md), and [ARCHITECTURE_AND_VALIDATION.md](ARCHITECTURE_AND_VALIDATION.md) before continuing release work.

Exact local candidate: 2,520,780 bytes, SHA-256 `EB0F862EDA94FBDCC938A5AA26BF812C8A1BB1B25567FE8D7D1992A90799DDC0`. The package has not been tagged, uploaded, or published. Verify the matching release-source commit and CI state from Git and GitHub before publication. Earlier 2.0.9 artifacts are test history and must not replace this candidate.

The previous public `1.0.52` package uses the stable `17.14` Extensibility SDK and declares Visual Studio 2022 `17.14+` plus stable Visual Studio 2026 `18.x`. That exact package passed ReleaseAnnouncement, AutomaticCollections, MultiLibraryHybrid, menu-count, registration, and protocol checks on VS2022 Community `17.14.33` and VS2026 Community `18.8.2`. Document activation/removal/disposal belongs to `RawBufferDocumentWorkspace`, claimed handoff terminal policy belongs to `ClaimedHandoffOpenCoordinator`, and active file-backed documents hold snapshot-directory leases. The separate-PC in-place update from exact public `1.0.50.0` to `1.0.52.0` was not recorded before publication and remains historical validation debt.

The Windows-reinstall functional recheck is recorded separately in [post-reinstall-validation-2026-08-03.md](post-reinstall-validation-2026-08-03.md). Qualified `1.0.53` repairs the two stale layout harness assumptions, adds page-budgeted large sampled previews, implements the reviewed essential-only Environment Check and panel toggles, and completes the vendor-neutral Connect Your Buffer workflow. Required and optional utilities plus the in-product behavior contract are listed in [development-prerequisites.md](development-prerequisites.md).

The first 2D camera-SDK experiment produced historical runtime evidence at exact pylon Software Suite `8.1.0.16743` and `26.07.2.18500` test points. That technical result was not legal clearance, so the direct adapter was removed from active source. Every future camera/frame-grabber/board SDK integration remains blocked until [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) passes; public Marketplace `1.0.53` contains no direct proprietary adapter.

## Start Here

1. Open `RawBufferVisualizer.sln`; do not infer the executable project from solution order.
2. To debug the extension in an experimental Visual Studio instance, select `RawBufferVisualizer.VisualStudio.Extensibility` as the startup project. Its existing launch profile starts `devenv.exe /RootSuffix Exp`; the in-process package owner is `RawBufferVisualizer.VisualStudio.Vssdk` and the debugger providers run out of process.
3. To exercise an already installed VSIX, select `RawBufferVisualizer.VisualizerDebuggee` as the startup project and use its named scenarios at a breakpoint.
4. Run the aggregate self-test with `dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows`.
5. Read the four documents below in order, then follow only the matching task route.

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
| Prepare or publish a Marketplace update | [release-qualification-2.0.9.md](release-qualification-2.0.9.md), [release-runbook.md](release-runbook.md), [marketplace-checklist.md](marketplace-checklist.md), [marketplace-overview-2.0.9.md](marketplace-overview-2.0.9.md), [Korean Overview review copy](marketplace-overview-2.0.9.ko.md), [marketplace-release-notes-2.0.9.md](marketplace-release-notes-2.0.9.md) |
| Review previous 2.0.x release baselines | [release-qualification-2.0.5.md](release-qualification-2.0.5.md), [release-qualification-2.0.4.md](release-qualification-2.0.4.md), [release-qualification-2.0.3.md](release-qualification-2.0.3.md), then earlier records as needed |
| Validate or generate very large images | [large-image-samples.md](large-image-samples.md) |
| Test or capture with a real industrial photograph | [industrial-image-testing.md](industrial-image-testing.md), then [visual-studio-debug-test-scenarios.md](visual-studio-debug-test-scenarios.md) |
| Research, qualify, or add a camera/frame-grabber integration | [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) first, then [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md) and [sdk-adapter-roadmap.md](sdk-adapter-roadmap.md) |
| Work on the vendor-neutral 2.0 buffer contract or Connect Doctor | [vendor-neutral-buffer-compatibility-matrix.md](vendor-neutral-buffer-compatibility-matrix.md), then [smart-type-mapper-design.md](smart-type-mapper-design.md), [buffer-doctor-design.md](buffer-doctor-design.md), and [automatic-vision-inspector.md](automatic-vision-inspector.md) |
| Review the removed Basler experiment's historical evidence | [basler-pylon-2d-adapter.md](basler-pylon-2d-adapter.md) |
| Review Image Watch-inspired UX | [image-watch-ux-analysis.md](image-watch-ux-analysis.md) |
| Prepare a short demo | [demo-recording-guide.md](demo-recording-guide.md) |
| Check the 2.0.9 correction package and copy | [release-qualification-2.0.9.md](release-qualification-2.0.9.md), [marketplace-overview-2.0.9.md](marketplace-overview-2.0.9.md), [Korean Overview review copy](marketplace-overview-2.0.9.ko.md), and [marketplace-release-notes-2.0.9.md](marketplace-release-notes-2.0.9.md) |
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

# Industrial Camera And Board Buffer Compatibility

Last updated: 2026-08-04 KST

## Current Decision

Raw Buffer Visualizer does not claim direct compatibility with a proprietary camera, frame-grabber, transport-board, or imaging-board SDK. The active product contract is vendor-neutral: a user's application may expose an already acquired 2D buffer as `RawBufferView` or `RawBufferSnapshot`, or Automatic Vision Inspector may recognize a safe public member shape without loading or invoking a vendor SDK.

The same rule applies to camera-side and board-side SDKs. If a vendor does not provide applicable individual-developer, testing, integration, distribution, and compatibility-wording rights, this project does not provide that vendor-named direct integration. See [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md).

Camera discovery, acquisition, exposure, trigger, transport configuration, lighting, PLC, and I/O remain outside product scope. 3D point clouds, depth/coordinate containers, and multi-component 3D payloads remain outside the current roadmap.

## Active Compatibility Claim

| Surface | Current claim |
| --- | --- |
| `RawBufferView` / `RawBufferSnapshot` | Supported public contracts for buffers that the user's application lawfully owns and keeps alive. |
| Automatic Vision Inspector | Vendor-neutral structural inference only. It reads bounded public fields/properties and fails closed when stride, padding, offset, length, format, or lifetime is ambiguous. |
| EMVA GenICam PFNC names | Conservative aliases are supported for the implemented 2D layouts. PFNC naming support is not certification of a camera or SDK. |
| Proprietary camera SDK objects | No direct registered provider or support claim. |
| Proprietary frame-grabber/board SDK objects | No direct registered provider or support claim. Method-only/scoped buffers require an application-owned wrapper unless a future direct adapter first passes the legal gate. |

GigE Vision, USB3 Vision, Camera Link, and CoaXPress are transports. A transport name does not prove that a debugger object exposes a safe pointer, dimensions, stride, pixel format, length, or lifetime.

## Vendor-Neutral Safety Coverage

`tests/RawBufferVisualizer.Tests/IndustrialCameraContractTests.cs` verifies:

1. an explicit `DataPtr` and stride shape can open;
2. nonzero row padding without explicit stride is rejected;
3. extra payload without explicit stride is rejected;
4. `ImageData` is preferred over a base `Buffer` and an address offset is rejected;
5. exact `SizeInBytes` is accepted while unexplained extra size is rejected;
6. safe PFNC aliases resolve while ambiguous, planar, YUV, compressed, and 3D layouts stay explicit;
7. method-only scoped buffers do not auto-open;
8. saved pointer mappings fail closed on padding, offsets, and extra payload.

The installed-VSIX hybrid smoke uses neutral padding-aware, stride-aware, offset-aware, and sized-buffer fixtures. These fixtures validate inference behavior only; they do not simulate or certify a named vendor runtime.

## Historical Vendor Evidence

A removed Basler/pylon experiment previously passed two exact emulator/runtime test points. That record is retained in [Historical Basler pylon .NET 2D Experiment](basler-pylon-2d-adapter.md). It is not active code, a reusable setup instruction, a minimum-version claim, or permission to distribute/advertise Basler support.

Historical IDS peak assembly metadata and earlier vendor-shape fixtures likewise do not establish current SDK eligibility, runtime compatibility, hardware behavior, or a support claim.

## Direct Integration Gate

Before any future vendor-named adapter work:

1. Pass [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) for the exact developer, purpose, SDK version, hardware state, distribution model, and proposed wording.
2. Obtain written vendor authorization when the published terms do not clearly cover this individual project.
3. Obtain qualified legal review for material ambiguity, indemnity, audit, trademark, or redistribution obligations.
4. Ask the project owner to approve reintroducing direct vendor code.
5. Only then install the approved SDK and define exact target types, lifetime rules, formats, failure behavior, package registration, and runtime evidence.
6. Keep technical evidence and legal permission separate in every release record.

No SDK installer, assembly-audit script, or vendor-specific provider is kept in the repository while this gate is blocked.

## Official Terms Reviewed

- [Basler pylon EULA](https://docs.baslerweb.com/pylon-end-user-license-agreement)
- [Allied Vision Vimba X terms](https://www.alliedvision.com/assets/documents/products/software/Vimba_X/Terms_and_Conditions_VimbaX_Downloads-EN.pdf)
- [IDS peak license terms](https://en.ids-imaging.com/download-peak.html?os=windows&version=win10)
- [Teledyne FLIR Spinnaker EULA](https://intecore.teledynevisionsolutions.com/globalassets/support/iis/knowledge-base/flir-spinnaker-sdk-eula-2018.pdf)
- [EMVA GenICam downloads and PFNC](https://www.emva.org/standards-technology/genicam/genicam-downloads/)

## Current Gate Record

Status: Blocked

Scope: Any new direct proprietary camera/frame-grabber/board SDK integration.

Acceptance criteria: current official terms reviewed for the exact proposal -> required; applicable written authorization -> required when terms are unclear or restrictive; qualified legal review -> required for material obligations; owner implementation approval -> required.

Verification: [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) and the official terms above.

Evidence: No active vendor-specific provider, ObjectSource, SDK audit script, or vendor-named runtime fixture remains in the intended `1.0.53` source.

Boundary / next dependency: This blocked state does not affect vendor-neutral `RawBufferView`/`RawBufferSnapshot` use. A vendor reply and legal review are prerequisites before spending implementation effort on a direct adapter.

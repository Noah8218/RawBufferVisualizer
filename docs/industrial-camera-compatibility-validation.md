# Industrial Camera Compatibility Validation

Last updated: 2026-07-27 KST

## Decision

Raw Buffer Visualizer is not yet qualified for a general industrial-camera compatibility claim. The generic debugger path, deterministic SDK-contract fixtures, installed-VSIX Automatic Vision Inspector regression, and one real SDK assembly contract have passed. Real vendor runtime objects, buffer-lifetime transitions, camera emulators, and hardware remain incomplete.

The product boundary remains unchanged: inspect an already acquired 2D image buffer while a C# debuggee is paused. Camera discovery, acquisition, exposure, trigger, transport configuration, lighting, PLC, and I/O are not product scope.

## Evidence Levels

| Level | Meaning |
| --- | --- |
| Official contract | The vendor or standards body documentation was checked. |
| Deterministic contract test | A dependency-free fixture with the documented public member names and layout rules passed in `RawBufferVisualizer.Tests`. |
| Assembly metadata | An actual vendor assembly was loaded and its public type/property contract was checked. This is not a runtime or hardware test. |
| Installed VSIX | A current installed extension opened the generic automatic-inspection scenario in VS2022. |
| Runtime/hardware | A real SDK object or vendor simulator/camera was exercised through its documented lifetime. |

## Compatibility Matrix

| Ecosystem | Documented buffer contract relevant to this product | Current evidence | Current claim |
| --- | --- | --- | --- |
| EMVA GenICam PFNC | Standardized pixel-format names and packed layouts, including `Mono10p`, `Mono12p`, Bayer 8-bit phases, RGB8, and BGR8 | Official contract + deterministic format tests | Safe aliases implemented; legacy `Mono10Packed`/`Mono12Packed`, planar, YUV, compressed, signed, and 3D coordinate formats remain explicit/unsupported |
| Basler pylon .NET | `IGrabResult`: `PixelDataPointer`, width, height, `PaddingX`, `PayloadSize`, `PixelTypeValue`; `ComputeStride()` is a method, not a property | Official contract + deterministic contract test; SDK not installed | Automatic only when `PaddingX == 0` and payload is exactly contiguous; otherwise wrapper/adapter required |
| Teledyne FLIR Spinnaker .NET | Managed image examples use `DataPtr`, width, height, stride, and pixel format; image ownership must be released/disposed | Official contract/example + deterministic contract test; SDK not installed | Explicit-stride shape is recognized; real lifetime and driver behavior are unverified |
| Allied Vision Vimba X .NET | `IFrame` separates whole `Buffer`/`BufferSize` from `ImageData`; chunk prefixes can make the pointers differ | Official contract + deterministic contract test; SDK not installed | `ImageData` is preferred; differing `Buffer`/`ImageData` addresses are rejected and require an adapter |
| IDS peak ICV | `IDSImaging.Peak.ICV.Types.Image`: `Data`, width, height, pixel format, `SizeInBytes` | Official examples + deterministic contract test + actual ICV 1.4.0 assembly metadata | Exact contiguous images are recognized; padded runtime images and hardware lifetime remain unverified |
| HIKROBOT MVS | Public product material exposes Mono/RGB/Bayer packed-format families, but the exact current managed frame-object contract was not verified | Official product/download material only; SDK not installed | No direct compatibility claim |
| Euresys eGrabber | C# uses scoped buffers and `GetInfo<T>(...)` methods; data is requeued/released with the scope | Official API contract; SDK directory is empty | Method-only access is intentionally not invoked by Automatic Vision Inspector; adapter required |
| Teledyne DALSA Sapera LT | SDK/manual access is gated; exact current C# property contract was not verified | Official download page only; SDK not installed | No direct compatibility claim |
| Zebra Aurora Imaging Library | C# is supported, but detailed current buffer access contracts require the SDK/manual package | Official product page only; SDK not installed | No direct compatibility claim |
| Zivid 3D | Frames contain 3D point-cloud and 2D data APIs rather than a single generic 2D byte plane | Official .NET frame documentation | Point clouds are out of scope; only an explicitly exposed supported 2D plane could use `RawBufferView` |

GigE Vision, USB3 Vision, Camera Link, and CoaXPress are transports. Once a safe host buffer exists, the viewer mainly depends on pointer, dimensions, stride, pixel format, length, and lifetime. Frame-grabber SDKs frequently expose those through methods and scoped objects, so transport support must not be confused with safe debugger-object support.

## Safety Changes Implemented

- Data-member priority now prefers `PixelDataPointer`, `ImageData`, and `DataPtr` over a generic `Buffer` or `Data`.
- Automatic opening without an explicit stride requires an exact contiguous byte count.
- Nonzero/unreadable row padding blocks automatic opening when stride is absent.
- `ImageData` offset from a base `Buffer` is rejected to prevent a chunk prefix from being rendered or an oversized `BufferSize` from being read past the image data.
- Saved mappings and collection mappings no longer fall back to the older ImagePtr heuristic after a mapped-layout failure.
- Pointer-backed mappings reject extra payload without stride and reject the same padding/offset signals in both ObjectSource and VSSDK live-memory paths.
- Safe PFNC aliases were added for `Mono10p`, `Mono12p`, Bayer RG/GR/GB/BG 8-bit, RGB8 packed, and BGR8 packed.

## Automated Test Coverage

`tests/RawBufferVisualizer.Tests/IndustrialCameraContractTests.cs` covers:

1. Spinnaker `DataPtr` + explicit stride automatic opening.
2. Basler zero-padding/exact-payload automatic opening.
3. Basler nonzero row padding rejection.
4. Basler extra payload rejection without stride.
5. Vimba `ImageData` priority over `Buffer`.
6. Vimba chunk-offset rejection.
7. IDS peak exact `SizeInBytes` acceptance and padded size rejection.
8. PFNC safe aliases plus explicit rejection of ambiguous/unsupported layouts.
9. Method-only frame-grabber contracts and unsafe saved mappings remaining explicit.

The assembly audit script is dependency-free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-IndustrialCameraSdkContracts.ps1
```

It auto-discovers IDS peak ICV from the local NuGet cache and accepts explicit Basler, Spinnaker, and Vimba assembly paths. Use `-RequireAll` only on a qualification machine where every SDK is intentionally installed.

## Verification Performed

```powershell
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release
dotnet build .\RawBufferVisualizer.sln -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-IndustrialCameraSdkContracts.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -NoBuild -Reinstall
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario AutomaticVisionInspector -Configuration Release -NoBuild -NoInstall
```

Results:

- Release build: passed, 0 errors; 18 known unique `VSTHRD010` warnings in `ImageTypeRecognizer.cs`.
- Full self-test executable: passed.
- IDS peak ICV assembly contract: passed for assembly version `1.4.0.0`, SHA-256 `6BB00D10B20EF0B139F93FA02137D1DCB71FBB4759330B0402B5AD04D9EDFCB6`.
- Installed VSIX Automatic Vision Inspector: passed in VS2022 `17.14.37314.3`, five rows, zero errors, managed array opened, repeated scan remained duplicate-free.
- Multi-library installed-VSIX UI smoke: incomplete. The debuggee reached the intended breakpoint and DTE saw the expected locals, but Visual Studio UI Automation timed out while resolving the docked tool-window controls. This is not counted as a product pass.

Evidence:

- `artifacts/validation/industrial-camera-sdk-contracts.json`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/automatic-vision-inspector.png`
- `artifacts/ui/multi-library-debug/multi-library-run.stderr.log`
- `artifacts/ui/multi-library-debug/multi-library-failure.png`

## Release Qualification Gates

Before advertising general industrial-camera support:

1. Install and audit current Basler pylon, Spinnaker, and Vimba X managed assemblies on an isolated qualification machine.
2. Run each vendor's official camera emulator/simulator where available.
3. Exercise one area-scan GigE/USB3 object, one line-scan or Camera Link/CoaXPress frame-grabber object, and one chunk-enabled frame.
4. For every object, record SDK version, exact runtime type, pixel format, width/height/stride/padding, buffer address/length, and ownership lifetime.
5. Verify Break -> open -> Continue -> Break, disposal/requeue, incomplete frame, null pointer, extra payload, and unsupported format behavior.
6. Repeat the installed-VSIX test after a Visual Studio restart and on a second machine that already has the public Marketplace version installed.
7. Keep 3D point-cloud support outside the release claim unless a separate typed 3D model and viewer are deliberately designed.

## Official References Checked

- [EMVA GenICam downloads and PFNC](https://www.emva.org/standards-technology/genicam/genicam-downloads/)
- [GenICam PFNC 2.4](https://www.emva.org/wp-content/uploads/GenICam_PFNC_2_4.pdf)
- [Basler pylon .NET IGrabResult](https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IGrabResult)
- [Basler camera emulation](https://docs.baslerweb.com/camera-emulation)
- [Spinnaker Image API](https://softwareservices.flir.com/spinnaker/latest/class_spinnaker_1_1_image.html)
- [Teledyne Spinnaker C# OpenCV example](https://github.com/Teledyne-MV/Spinnaker-Examples/blob/main/AcquisitionOpenCV/AcquisitionOpenCV.cs)
- [Allied Vision Vimba X .NET API manual](https://docs.alliedvision.com/dotNetAPIManual.html)
- [IDS peak official examples](https://github.com/ids-imaging/ids-peak-examples)
- [Euresys eGrabber API guide](https://documentation.euresys.com/Products/eGrabber/eGrabber_25_12/00/en-us/Content/IOdoc/egrabber.html)
- [HIKROBOT MVS product/download page](https://www.hikrobotics.com/en/machinevision/visionproduct/?id=44&typeId=78)
- [Teledyne DALSA SDK downloads](https://www.teledynedalsa.com/en/support/downloads-center/software-development-kits/)
- [Zebra Aurora Imaging Library](https://www.zebra.com/us/en/software/machine-vision-and-fixed-industrial-scanning-software/aurora-imaging-library.html)
- [Zivid .NET Frame documentation](https://downloads.zivid.com/sdk/releases/2.15.0%2B5fcc365b-1/doc/dotnet/html/aec03546-b5df-1b09-4ddb-72310f2b6ffc.htm)

## Completion Record

Status: Incomplete

Scope: Official-contract research, conservative automatic-layout hardening, deterministic multi-vendor contract tests, IDS peak assembly metadata audit, current VSIX packaging/reinstall, and generic installed-VSIX regression.

Acceptance criteria: deterministic contract tests -> pass; Release build -> pass; IDS assembly audit -> pass; generic installed-VSIX Automatic Vision Inspector -> pass; installed multi-library SDK fixture UI smoke -> fail due UI Automation timeout; real vendor runtime/hardware matrix -> not run.

Verification: commands and results are recorded above.

Evidence: source/tests/scripts plus the artifact paths listed above.

Boundary / next dependency: Current Basler pylon, Spinnaker, Vimba X, HIKROBOT, Euresys, Sapera, and Zebra SDK installations or legal test machines, plus representative camera/frame-grabber objects and lifetime scenarios.

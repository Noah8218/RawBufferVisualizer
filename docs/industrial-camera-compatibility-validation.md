# Industrial Camera Compatibility Validation

Last updated: 2026-08-04 KST

## Decision

Raw Buffer Visualizer is not yet qualified for a general industrial-camera compatibility claim. Basler is the first completed 2D vendor vertical slice: exact pylon `8.1.0.16743` and `26.07.2.18500` test points passed the official camera emulator, actual `IGrabResult`, and direct installed-VSIX debugger open. Other pylon releases, vendor runtimes, physical-camera transports, and representative hardware lifetime transitions remain incomplete.

The product boundary remains unchanged: inspect an already acquired 2D image buffer while a C# debuggee is paused. Camera discovery, acquisition, exposure, trigger, transport configuration, lighting, PLC, and I/O are not product scope.

## Evidence Levels

| Level | Meaning |
| --- | --- |
| Official contract | The vendor or standards body documentation was checked. |
| Deterministic contract test | A dependency-free fixture with the documented public member names and layout rules passed in `RawBufferVisualizer.Tests`. |
| Assembly metadata | An actual vendor assembly was loaded and its public type/property contract was checked. This is not a runtime or hardware test. |
| Installed VSIX | A current installed extension opened either the registered debugger visualizer or the documented generic automatic-inspection scenario in Visual Studio. |
| Runtime/hardware | A real SDK object or vendor simulator/camera was exercised through its documented lifetime. |

## Compatibility Matrix

| Ecosystem | Documented buffer contract relevant to this product | Current evidence | Current claim |
| --- | --- | --- | --- |
| EMVA GenICam PFNC | Standardized pixel-format names and packed layouts, including `Mono10p`, `Mono12p`, Bayer 8-bit phases, RGB8, and BGR8 | Official contract + deterministic format tests | Safe aliases implemented; legacy `Mono10Packed`/`Mono12Packed`, planar, YUV, compressed, signed, and 3D coordinate formats remain explicit/unsupported |
| Basler pylon .NET | `IGrabResult`: `PixelDataPointer`, width, height, `PaddingX`, `PayloadSize`, `PixelTypeValue`; official nullable `ComputeStride(IImage)`; application-owned disposable buffer lifetime | Official contract + deterministic tests + exact installed pylon `8.1.0.16743` and `26.07.2.18500` assembly audits + 11/11 `BaslerCamEmu` matrices on each + registered direct Mono12 opens | Direct registered 2D `IGrabResult` compatibility is emulator-qualified at those two exact suite versions. Generic Automatic Inspector, packed `Mono10p`/`Mono12p` emulator coverage, physical-camera behavior, and untested pylon releases are not included |
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

`tests/RawBufferVisualizer.Tests/BaslerPylonGrabResultVisualizerTests.cs` additionally covers:

1. exact Basler interface detection and official `ComputeStride(IImage)` reflection shape;
2. padded Mono8 descriptor, preview, and chunk transfer;
3. image-only payload and top-down orientation gates;
4. visible image length excluding `PaddingY`/suffix bytes;
5. supported 2D pixel mappings and explicit legacy packed/YUV/3D rejection;
6. failed/disposed/undersized payload diagnostics;
7. no `Clone()` or `Dispose()` call on the application-owned result.

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

- Release build: passed with the same 18 existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and 0 errors.
- Full self-test executable: passed.
- Basler direct source tests: passed for padded stride, payload bounds, formats, preview/chunk, failure paths, and non-ownership.
- Packaged Basler provider registration: passed; full assembly-qualified `Basler.Pylon.IGrabResult` and concrete `Basler.Pylon.GrabResult` targets plus `BaslerPylonGrabResultVisualizerObjectSource` are present in `.vsextension/extension.json`.
- Official Basler-owned NuGet `10.3.2.636` assembly metadata: passed for `Basler.Pylon.dll` version `1.2.0.0`, SHA-256 `C20A5F8D8140FE6C920850431EF07127C80DD5ED2B4C991A6858A102D8451F89`; this is not an installed-runtime or emulator pass.
- Installed pylon Software Suite `26.07.2.18500`: assembly version `1.2.0.0`, file version `9.1.0.1300`, SHA-256 `588FDD275EE2F9BE3FD6EF57A5BC99FB70793C3407806E9D18D9B86D1DA2D8DC`; metadata audit passed.
- Basler camera emulator: 11 supported 2D formats passed with official stride agreement, live pointer validity before/after adapter access, direct-memory metadata, preview, and chunk reads. `Mono10p`/`Mono12p` were unavailable in this emulator profile and remain deterministic-test only.
- Installed VSIX Basler direct path: VS2022 Community `17.14.37516.0` opened a real emulated concrete `Basler.Pylon.GrabResult` at a Mono12 128 x 96 breakpoint through the registered **View** glyph and displayed the frame in the docked Tool Window.
- Exact-version swap regression: pylon `8.1.0.16743` passed its assembly contract, the same 11/11 emulator formats, and the registered direct Mono12 debugger visualizer in VS Community 2026 `18.8.12023.21`; after uninstalling 8.1 and restoring `26.07.2.18500`, all three gates passed again.
- Long TEMP handoff regression: passed after shortening request IDs and ignoring publishing names in the package watcher; the actual package log records final publish, claim, open start, and open end.
- IDS peak ICV assembly contract: passed for assembly version `1.4.0.0`, SHA-256 `6BB00D10B20EF0B139F93FA02137D1DCB71FBB4759330B0402B5AD04D9EDFCB6`.
- Installed VSIX Automatic Vision Inspector: passed in VS2022 `17.14.37314.3`; a function argument and five valid locals opened, one incomplete fixture remained `[Map]`, one invalid-pointer fixture remained `[Failed]`, and repeated scans remained duplicate-free.
- Multi-library installed-VSIX UI smoke: incomplete. The debuggee reached the intended breakpoint and DTE saw the expected locals, but Visual Studio UI Automation timed out while resolving the docked tool-window controls. This is not counted as a product pass.

Evidence:

- `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2\runtime-qualification\bin\Release\net472\basler-emulator-runtime-qualification.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2\industrial-camera-sdk-contracts.json`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2\visual-studio-qualification\basler-visual-studio-end-to-end-after.png`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2\visual-studio-qualification\temp\RawBufferVisualizer\VisualStudio\package.log`
- `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md`
- `artifacts/validation/industrial-camera-sdk-contracts.json`
- `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/automatic-vision-inspector.png`
- `artifacts/ui/multi-library-debug/multi-library-run.stderr.log`
- `artifacts/ui/multi-library-debug/multi-library-failure.png`

## Release Qualification Gates

Before advertising general industrial-camera support:

1. Install and audit current Spinnaker and Vimba X managed assemblies on an isolated qualification machine; retain the completed Basler result as the reference vertical slice.
2. Run each remaining vendor's official camera emulator/simulator where available.
3. Exercise one area-scan GigE/USB3 object, one line-scan or Camera Link/CoaXPress frame-grabber object, and one chunk-enabled frame.
4. For every object, record SDK version, exact runtime type, pixel format, width/height/stride/padding, buffer address/length, and ownership lifetime.
5. Verify Break -> open -> Continue -> Break, disposal/requeue, incomplete frame, null pointer, extra payload, and unsupported format behavior.
6. Repeat the installed-VSIX test after a Visual Studio restart and on a second machine that already has the public Marketplace version installed.
7. Keep 3D point-cloud, depth/coordinate, and multi-component support outside the product roadmap and release claim.

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

Status: Complete

Scope: First Basler pylon .NET 2D vertical slice through source/package registration plus exact pylon `8.1.0.16743` and `26.07.2.18500` assembly, 11-format camera-emulator, and actual registered debugger visualizer qualification.

Acceptance criteria: deterministic contract tests -> pass; Basler direct adapter tests -> pass; Release build -> pass with 18 existing `VSTHRD010` warnings and 0 errors; full assembly-qualified provider registration -> pass; pylon 8.1 and 26.07 installed assembly audits -> pass; official emulator supported-format matrix -> 11/11 on each; actual registered debugger open -> Mono12 128 x 96 on each; restored pylon 26.07 regression -> pass; long `D:` TEMP handoff -> pass.

Verification: commands and results are recorded above.

Evidence: source/tests/scripts plus the artifact paths listed above.

Boundary / next dependency: This completion does not prove untested pylon releases, physical Basler cameras, transport drivers, packed `Mono10p`/`Mono12p` emulator formats, generic Automatic Inspector integration, or another vendor SDK. Those require their own hardware/runtime evidence; 3D remains out of scope.

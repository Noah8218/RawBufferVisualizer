# Basler pylon .NET 2D Adapter

## Decision

Raw Buffer Visualizer will prioritize direct 2D camera-SDK image objects. 3D point clouds, coordinate/depth containers, and multi-component 3D payloads are not part of this adapter or the current product roadmap.

Basler pylon .NET `IGrabResult` is the first direct SDK target because Basler documents the image pointer, payload, format, stride calculation, ownership rules, and an official camera-emulation transport. The adapter reuses the existing pointer-backed Raw Buffer Visualizer transfer and viewer path. It does not add Basler assemblies to the VSIX and does not acquire or control a camera.

## Developer Workflow

1. The debugged application references and initializes its own Basler pylon runtime.
2. Stop at a breakpoint while an `IGrabResult` is still alive and before its `using` scope or image callback returns.
3. Open Raw Buffer Visualizer from the `IGrabResult` debugger value.
4. The adapter validates the result and reads the live image pointer only while Visual Studio remains paused.
5. Resume execution normally. Raw Buffer Visualizer never clones or disposes the application's grab result.

No pylon installation is required merely to install or use Raw Buffer Visualizer with other image types. pylon remains a dependency of the debugged Basler application.

## Accepted Contract

| Check | Required behavior |
| --- | --- |
| Target type | Implements exact `Basler.Pylon.IGrabResult` from the debugged application's strong-named `Basler.Pylon` assembly. The provider registers both the interface and concrete `Basler.Pylon.GrabResult` assembly-qualified names because Visual Studio matches the runtime value type. |
| Grab state | `GrabSucceeded == true` and `IsValid == true` |
| Payload | `PayloadTypeValue == Image`; raw, file, chunk-only, GenDC, device-specific, and 3D/multi-component payloads are rejected |
| Orientation | `TopDown`; `BottomUp` is rejected until the viewer has an explicit orientation contract |
| Pointer | `PixelDataPointer` is nonzero |
| Stride | The adapter invokes only Basler's documented `IImageExtensions.ComputeStride(IImage)` method and requires a non-null positive result |
| Length | The visible 2D length is `stride * height`; `PayloadSize` must contain that range. `PaddingY` and chunk suffixes are not exposed as image pixels |
| Lifetime | The application retains ownership. The adapter must not call `Clone()` or `Dispose()` |

## Supported Pixel Types

| Basler `PixelTypeValue` | Raw Buffer Visualizer format |
| --- | --- |
| `Mono8` | `Mono8`, valid bits 8 |
| `Mono10`, `Mono12`, `Mono16` | `Mono16`, valid bits 10, 12, or 16 |
| `Mono10p`, `Mono12p` | `Mono10PackedLsb` or `Mono12PackedLsb` |
| `BayerRG8`, `BayerGR8`, `BayerGB8`, `BayerBG8` | Corresponding 8-bit Bayer phase |
| `RGB8packed`, `BGR8packed` | `RGB24` or `BGR24` |
| `BGRA8packed` | `BGRA32` |

Legacy `Mono10packed` and `Mono12packed` have layouts different from PFNC `Mono10p` and `Mono12p` and are rejected. Signed, planar, YUV, compressed, packed Bayer, and 3D coordinate formats are also rejected rather than guessed.

## Validation Gates

- Deterministic tests: exact target contract, padded stride, payload bound, supported formats, pointer chunk/preview, failure messages, and no clone/dispose side effect.
- Build: Release solution build and complete self-test suite.
- Runtime: official current pylon .NET assembly metadata audit.
- Emulator: installed VSIX opens a real emulated `IGrabResult` for at least Mono8 and one padded or higher-bit format.
- Hardware: optional separate evidence; emulator success must not be described as physical-camera qualification.

The feature may be described as source-implemented after the deterministic and build gates pass. It may be described as Basler runtime-qualified only after the runtime and emulator gates pass.

## Installed Runtime Qualification

As of 2026-08-03, the source, installed-runtime, emulator, and direct installed-VSIX gates have passed:

- pylon Software Suite `26.07.2.18500` was installed with Camera Emulation Support and the development SDK; physical camera interface/driver selections were intentionally omitted;
- installed x64 assembly: `Basler.Pylon, Version=1.2.0.0, Culture=neutral, PublicKeyToken=e389355f398382ab`, file version `9.1.0.1300`, SHA-256 `588FDD275EE2F9BE3FD6EF57A5BC99FB70793C3407806E9D18D9B86D1DA2D8DC`;
- the installed assembly passed inherited-property and nullable `ComputeStride(IImage)` contract audit;
- the official `BaslerCamEmu` device produced 11 passing 128 x 96 frames: `Mono8`, `Mono10`, `Mono12`, `Mono16`, four Bayer 8-bit phases, `RGB8Packed`, `BGR8Packed`, and `BGRA8Packed`;
- every frame matched Basler's computed stride, remained valid after adapter access, exposed direct-memory metadata, and passed preview plus pointer-chunk transfer;
- `Mono10p` and `Mono12p` were not offered by this emulator profile, so those mappings remain deterministic-test evidence rather than installed-emulator evidence;
- the generated `.vsextension/extension.json` contains the full assembly-qualified `IGrabResult` and concrete `GrabResult` targets plus `BaslerPylonGrabResultVisualizerObjectSource`;
- candidate VSIX SHA-256 `608A9327F0261079A95F3B7F6FB864965135BFA00105D2272D5D487A23F1EFAD` was installed into VS2022 Community `17.14.37516.0` as version `1.0.53.0`;
- at an actual debugger break, a real emulated concrete `Basler.Pylon.GrabResult` with `Mono12`, 128 x 96, and 24,576-byte payload exposed the Raw Buffer Visualizer **View** glyph and opened successfully in the docked Tool Window;
- the long required `D:` test TEMP path exposed a .NET Framework path-length failure. Handoff request IDs were shortened, and the watcher now consumes only final `.rbuf-handoff` names; the final package log records `Published -> Queue -> Open start -> Open end` without an orphan publishing file;
- Release self-tests passed under the long `D:` TEMP path. The final restored-runtime Release solution build completed with the same 18 existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and 0 errors.

Evidence root: `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\installed-pylon-26.07.2`.

- Runtime matrix: `runtime-qualification\bin\Release\net472\basler-emulator-runtime-qualification.json`, SHA-256 `320B77EEEC23526488054E4A18BE7858EB657EED5AD7601E306DBCCB0B01556B`.
- SDK contract report: `industrial-camera-sdk-contracts.json`, SHA-256 `51257B2CC8FECA73B209D6BBC0C083D9DC0B1E12438482F05E86368CAEBB1735`.
- Installed-VSIX screenshot: `visual-studio-qualification\basler-visual-studio-end-to-end-after.png`, SHA-256 `80C9A66C4C0D5F699C11B756C4F8E2A3392CFCB585508A914F99F2B43298E055`.
- Package log: `visual-studio-qualification\temp\RawBufferVisualizer\VisualStudio\package.log`.

The registered debugger visualizer is the qualified Basler path. Generic **Auto Inspect on Break** still treats this object as a structural candidate, takes an excessive scan time on this test frame, and leaves it requiring mapping; that separate Automatic Inspector integration is not part of the current Basler compatibility claim. Physical-camera transport, driver, requeue, and hardware-lifetime behavior also remain unverified.

## Exact pylon Version Compatibility Check (2026-08-04)

The same adapter source, exact debuggee, and installed development VSIX were exercised through an uninstall/install swap to answer whether an older pylon release still works:

| Exact suite | Managed assembly evidence | Official emulator | Actual Visual Studio direct path |
| --- | --- | --- | --- |
| pylon `8.1.0.16743` | file `8.1.0.471`, assembly `1.2.0.0`, SHA-256 `9499FD0EE33C1156262B2BDDF1512616F7E33CE5EE3E341E4E443B9A543D69F7` | 11/11 supported 2D formats | VS Community 2026 `18.8.12023.21`: Mono12, 128 x 96, Mono16 storage, stride 256; passed |
| pylon `26.07.2.18500` restored | file `9.1.0.1300`, assembly `1.2.0.0`, SHA-256 `588FDD275EE2F9BE3FD6EF57A5BC99FB70793C3407806E9D18D9B86D1DA2D8DC` | 11/11 supported 2D formats | VS Community 2026 `18.8.12023.21`: same direct Mono12 scenario; passed |

The emulator formats were `Mono8`, `Mono10`, `Mono12`, `Mono16`, four Bayer 8-bit phases, `RGB8Packed`, `BGR8Packed`, and `BGRA8Packed`. Both direct visualizer runs saved a 233 x 901 PNG with the same SHA-256, `C19C51D62AB4C64CD5D20DEC676D31ACD7D76772EA4C89BBCA02CBAFB5D0B780`.

Evidence: `D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md`.

This proves the two exact suite versions above. It does not establish a blanket minimum pylon version or certify every release between them. Physical cameras and interface drivers, resume/requeue lifetime, packed `Mono10p`/`Mono12p`, camera control, and 3D remain outside the result.

## Official References

- [Basler pylon .NET IGrabResult](https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IGrabResult)
- [Basler pylon .NET IImageExtensions.ComputeStride](https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IImageExtensions)
- [Basler pylon .NET Programmer's Guide and camera emulation](https://docs.baslerweb.com/pylonapi/net/Guide)

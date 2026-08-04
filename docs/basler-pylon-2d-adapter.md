# Historical Basler pylon .NET 2D Experiment

> **Removed from active source on 2026-08-04.** This file preserves technical history only. Raw Buffer Visualizer does not currently provide, distribute, or advertise a direct Basler/pylon adapter. Reintroduction requires the full [vendor SDK license policy](vendor-sdk-license-policy.md), written vendor authorization where required, qualified legal review, new implementation approval, and fresh validation.

## Why It Was Removed

The experiment proved that a narrow direct debugger path could work technically, but technical compatibility and the absence of bundled SDK binaries did not establish permission to distribute or advertise it. The active provider, exact type registration, ObjectSource transfer, dedicated tests, and SDK audit script were therefore removed. Public Marketplace `1.0.52` never contained this experiment.

Users can still inspect buffers obtained by their own applications through the vendor-neutral `RawBufferView` or `RawBufferSnapshot` contracts. Raw Buffer Visualizer does not acquire cameras, control devices, install pylon, or claim direct compatibility with Basler objects.

## Historical Technical Evidence

The removed development adapter targeted exact `Basler.Pylon.IGrabResult`/`GrabResult` assembly identities and used the documented `IImageExtensions.ComputeStride(IImage)` contract. It rejected failed or invalid grabs, non-image and bottom-up payloads, unsupported formats, null pointers, and undersized payloads, and it did not clone or dispose the application-owned result.

Two exact pylon Software Suite test points were exercised before removal:

| Exact suite | Managed assembly evidence | Official emulator | Actual Visual Studio direct path |
| --- | --- | --- | --- |
| `8.1.0.16743` | file `8.1.0.471`, assembly `1.2.0.0`, SHA-256 `9499FD0EE33C1156262B2BDDF1512616F7E33CE5EE3E341E4E443B9A543D69F7` | 11/11 offered 2D formats | VS Community 2026 `18.8.12023.21`: Mono12 128 x 96 passed |
| `26.07.2.18500` | file `9.1.0.1300`, assembly `1.2.0.0`, SHA-256 `588FDD275EE2F9BE3FD6EF57A5BC99FB70793C3407806E9D18D9B86D1DA2D8DC` | 11/11 offered 2D formats | VS2022 and VS2026 direct Mono12 scenarios passed |

The emulator formats were `Mono8`, `Mono10`, `Mono12`, `Mono16`, four Bayer 8-bit phases, `RGB8Packed`, `BGR8Packed`, and `BGRA8Packed`. `Mono10p` and `Mono12p` were not offered by that emulator profile. Physical cameras, interface drivers, resume/requeue lifetime, camera control, 3D, and untested pylon releases were never proven.

Historical evidence root:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d
```

Durable combined report:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\basler-pylon-2d\BASLER_PYLON_8.1_AND_26.07_QUALIFICATION_2026-08-04.md
```

The former candidate VSIX had SHA-256 `608A9327F0261079A95F3B7F6FB864965135BFA00105D2272D5D487A23F1EFAD`. It is historical and must not be uploaded or presented as supported.

## Reintroduction Gate

Do not reinstall an SDK or reconstruct the removed adapter merely to repeat these tests. A future proposal starts with the current official license and written permission. Only after the legal gate passes should maintainers define exact SDK versions, target types, buffer lifetime, supported formats, package registration, emulator/hardware scope, and Marketplace wording.

## Official References

- [Basler pylon EULA](https://docs.baslerweb.com/pylon-end-user-license-agreement)
- [Historical IGrabResult API reference](https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IGrabResult)
- [Historical IImageExtensions API reference](https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IImageExtensions)

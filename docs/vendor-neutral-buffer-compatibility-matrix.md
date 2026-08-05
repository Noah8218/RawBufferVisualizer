# Vendor-Neutral 2D Buffer Compatibility Matrix

Status: Complete for the contract, shared transfer validation, and repository fixtures.
Last verified: 2026-08-05 KST.

## Scope

This is the 2.0 compatibility contract for already-acquired 2D buffers. It covers `RawBufferView`, `RawBufferSnapshot`, Connect Your Buffer mappings, and bounded structural inference without loading or invoking a proprietary SDK.

The matrix does not claim compatibility with a camera, frame grabber, transport board, SDK, driver, or hardware model. Camera acquisition/control, PLC/I/O, 3D, compressed payloads, and private native layouts remain outside scope.

## Contract Rules

1. Width and height must be positive.
2. Pixel format must identify one implemented memory layout; ambiguous names stay explicit.
3. Stride must be at least the minimum row size. When stride is absent, reported length must match the exact contiguous image size.
4. Buffer length must cover `stride * (height - 1) + minimum row bytes`. Trailing bytes are allowed only when an explicit stride already explains the image rows.
5. An image pointer offset from a separately exposed base buffer is not guessed. The application may instead expose the actual image-start pointer through `RawBufferView`.
6. Managed carriers are limited to `byte[]`, `ushort[]`, and `float[]`. The actual array byte length is authoritative.
7. `Mono16` uses explicit valid bits and byte order. Packed 10/12-bit formats use their fixed implemented LSB layouts.
8. Pointer-backed data is live only while the debuggee is paused and owns the buffer. Continue, reuse, disposal, or process exit makes the source unavailable.
9. Only fields and readable property getters, with at most one nested member level, participate in generic mapping. Arbitrary methods are never invoked.

## Verified Supported Shapes

| ID | Carrier / shape | Required metadata | Result | Repository evidence |
| --- | --- | --- | --- | --- |
| `VNB-S01` | `RawBufferView` pointer | nonzero pointer, length, width, height, stride, format, lifetime | Direct-memory metadata includes process ID/address; chunk and snapshot bytes match | `Program.RawBufferViewCreatesDescriptorAndChunks` |
| `VNB-S02` | `RawBufferSnapshot` managed copy/file | descriptor plus owned bytes | Snapshot and descriptor round-trip | `Program.SnapshotRoundTrips`, `Program.SnapshotReferenceLoadsMetadata` |
| `VNB-S03` | Mapped contiguous pointer | pointer, width, height, exact length, format | Minimum stride is derived and the pointer remains live-process-backed | `IndustrialCameraContractTests.MappedSupportedCarriersPreserveLayoutAndBytes` |
| `VNB-S04` | Mapped padded pointer | pointer, width, height, explicit stride, covering length, format | Explicit row padding is preserved; bytes and direct-memory ownership match | `IndustrialCameraContractTests.MappedSupportedCarriersPreserveLayoutAndBytes` |
| `VNB-S05` | Mapped `byte[]` | width, height, stride, format | Managed chunk bytes and descriptor match | `IndustrialCameraContractTests.MappedSupportedCarriersPreserveLayoutAndBytes` |
| `VNB-S06` | Mapped `ushort[]` | width, height, stride, `Mono16`, valid bits, byte order | 12 valid bits and big-endian byte conversion are preserved | `IndustrialCameraContractTests.MappedSupportedCarriersPreserveLayoutAndBytes` |
| `VNB-S07` | Mapped `float[]` | width, height, stride, `Float32`, byte order | Big-endian IEEE 754 bytes and descriptor match | `IndustrialCameraContractTests.MappedSupportedCarriersPreserveLayoutAndBytes` |
| `VNB-S08` | One-level nested mapped members | data/dimensions/format within root plus one child | Member paths resolve and transfer | `Program.TypeMappingReadsOneLevelNestedMemberPaths` |
| `VNB-S09` | Conservative PFNC-style names | one implemented, unambiguous 2D layout | `Mono10p`, `Mono12p`, Bayer 8-bit phases, RGB8/BGR8 packed resolve | `IndustrialCameraContractTests.GenICamPfncAliasesResolveConservatively` |
| `VNB-S10` | Live pointer after source process exits | previously valid process ID/address | Read becomes a controlled source-unavailable error | `Program.ProcessMemorySourceReportsUnavailableAfterProcessExit` |

## Verified Fail-Closed Shapes

| ID | Unsafe or ambiguous input | Required outcome | Repository evidence |
| --- | --- | --- | --- |
| `VNB-F01` | null pointer | no image metadata; visible null-pointer reason | `IndustrialCameraContractTests.MappedCoreContractsFailClosed` |
| `VNB-F02` | zero/negative width or height | reject descriptor | `IndustrialCameraContractTests.MappedCoreContractsFailClosed`, `RegisteredRawBufferViewContractsMatchMappedValidation` |
| `VNB-F03` | stride smaller than the pixel-format minimum | reject descriptor | `IndustrialCameraContractTests.MappedCoreContractsFailClosed`, `RegisteredRawBufferViewContractsMatchMappedValidation` |
| `VNB-F04` | buffer shorter than the descriptor requires | reject descriptor | `IndustrialCameraContractTests.MappedCoreContractsFailClosed`, `RegisteredRawBufferViewContractsMatchMappedValidation` |
| `VNB-F05` | nonzero/unreadable row padding without stride | require explicit layout; do not auto-open | `IndustrialCameraContractTests.PaddingAndPayloadShapeRequiresContiguousLayout` and mapped failure coverage |
| `VNB-F06` | unexplained extra payload without stride | require explicit layout; do not treat payload as pixels | `IndustrialCameraContractTests.PaddingAndPayloadShapeRequiresContiguousLayout`, `SizedBufferShapeRequiresContiguousSize`, and mapped failure coverage |
| `VNB-F07` | selected `ImageData` differs from exposed base `Buffer` | reject the offset layout | `IndustrialCameraContractTests.ImageDataOffsetShapeRequiresExplicitLayout` and mapped failure coverage |
| `VNB-F08` | method-only or scoped buffer | report missing debugger-visible data; invoke no method | `IndustrialCameraContractTests.MethodOnlyBufferContractsStayExplicit` |
| `VNB-F09` | array carrier other than `byte[]`, `ushort[]`, or `float[]` | reject with supported-carrier list | `IndustrialCameraContractTests.MappedCoreContractsFailClosed` |
| `VNB-F10` | ambiguous/unsupported pixel layout such as legacy packed mono, planar RGB, YUV, RGBA, or 3D coordinates | keep explicit; do not map by a similar name | `IndustrialCameraContractTests.GenICamPfncAliasesResolveConservatively` |
| `VNB-F11` | unknown byte-order value | reject mapping | `IndustrialCameraContractTests.MappedCoreContractsFailClosed` |
| `VNB-F12` | `Mono16` valid bits outside `1..16`, or packed-format valid bits that differ from the fixed layout | reject descriptor in registered and mapped paths | `IndustrialCameraContractTests.MappedCoreContractsFailClosed`, `ValidBitsContractsAreStrictAndPreserved` |

## Implemented Shared Validation Checkpoint

`VisualizerChunkedTransfer.CreateMetadataCore` now applies `RawBufferDiagnostics.AnalyzeLength` before creating metadata. Registered `RawBufferView`, registered library adapters, snapshots, and mapped managed/pointer carriers therefore share the same fail-closed dimension, stride, length, and valid-bits boundary.

`Mono16` accepts valid-bit counts `1..16`; repository fixtures explicitly preserve 10, 12, 14, and 16. `Mono10PackedLsb` requires 10 and `Mono12PackedLsb` requires 12. Incompatible values are errors rather than advisory diagnostics. No UI, SDK adapter, or offset abstraction was added.

## Fixture Checklist

- Fixtures use only repository-owned neutral classes and buffers.
- No vendor assembly, SDK binary, installer, header, sample, or vendor-named runtime type is used.
- Supported fixtures assert descriptor fields and transferred bytes, not inference confidence alone.
- Rejected fixtures assert the user-visible failure reason.
- Pointer fixtures assert process/address ownership or controlled unavailability.
- Tests run with physical temporary output under `D:\OpenVisionLab-TestData\RawBufferVisualizer\2.0-buffer-matrix` on this workstation.

## Durable Closure

Status: Complete
Scope: Vendor-neutral 2D carrier/layout compatibility contract, shared metadata validation, and supported/fail-closed repository fixtures
Acceptance criteria: managed arrays and pointers covered; dimensions/stride/length/offset/format/valid-bits/byte-order/lifetime decisions recorded; registered and mapped metadata reject invalid dimensions/stride/length/valid-bits; valid Mono16 and packed values preserved; proprietary SDK, UI, and 3D scope absent
Verification: aggregate Release self-tests passed; Release solution build passed with 0 errors and the 18 pre-existing `VSTHRD010` warnings; changed-document links and `git diff --check` passed
Evidence: `src\RawBufferVisualizer.Core\RawBufferDiagnostics.cs`; `src\RawBufferVisualizer.VisualStudio.ObjectSource\VisualizerChunkedTransfer.cs`; `tests\RawBufferVisualizer.Tests\IndustrialCameraContractTests.cs`; and this matrix
Boundary / next dependency: The exact frozen `2.0.0.0` VSIX later passed dual-IDE installed qualification; see [release-qualification-2.0.0.md](release-qualification-2.0.0.md). Marketplace publication/readback and proprietary SDK or hardware claims remain outside this contract. Test TEMP/TMP used `D:\OpenVisionLab-TestData\RawBufferVisualizer\2.0-buffer-validation\final`.

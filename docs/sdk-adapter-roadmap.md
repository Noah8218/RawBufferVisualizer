# SDK Adapter Roadmap

The first-class extension target for arbitrary acquisition SDKs is `RawBufferView`. There is no active vendor-named direct adapter. The removed Basler experiment is retained only as [historical evidence](basler-pylon-2d-adapter.md).

```csharp
var view = new RawBufferView
{
    Buffer = bufferPointer,
    BufferLength = stride * height,
    Width = width,
    Height = height,
    Stride = stride,
    PixelFormat = RawPixelFormat.BGR24,
    Channels = 3,
    BitDepth = 8,
    ByteOrder = RawByteOrder.LittleEndian,
    Name = "camera0"
};
```

Inspect `view` from Watch, Locals, Autos, or DataTip after the VSIX is installed.

## Current Support

| Source | Status | Notes |
| --- | --- | --- |
| `RawBufferSnapshot` | Supported | Best when the app already owns a managed snapshot. |
| `RawBufferView` | Supported | Best for `IntPtr` buffer + descriptor metadata. Chunks are copied from the unmanaged pointer on demand. |
| `System.Drawing.Bitmap` | Supported | `8bppIndexed`, `24bppRgb`, `32bppArgb`, `32bppPArgb`, `32bppRgb`. |
| OpenCvSharp `Mat` | Supported | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`. |
| Emgu CV `Mat` | Supported | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1. |

## Vendor Adapter Position

The current roadmap has no proprietary vendor-specific implementation target. Camera and frame-grabber/board SDKs follow the same gate: if applicable rights are not provided, Raw Buffer Visualizer does not provide the direct integration. See [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md) and [industrial-camera-compatibility-validation.md](industrial-camera-compatibility-validation.md).

| Integration route | Current position | Direction |
| --- | --- | --- |
| Application-owned `RawBufferView` / `RawBufferSnapshot` | Supported | Preferred route for camera and board buffers that the user's application lawfully acquires and keeps alive. |
| Vendor-neutral structural inference | Supported with fail-closed layout checks | Keep public-member inspection bounded; do not load an SDK or invoke arbitrary vendor methods. |
| Proprietary camera SDK direct adapter | Blocked by default | Start only after the license gate, any required written authorization/legal review, and explicit owner approval. |
| Proprietary frame-grabber/transport-board/imaging-board direct adapter | Blocked by default | Same gate as camera SDKs; no exception merely because the buffer originates on a board. |

## Pixel Format Mapping Rules

Use these mappings before adding a vendor-specific enum:

| SDK Reported Shape | Raw Buffer Visualizer Format |
| --- | --- |
| Mono 8-bit | `Mono8`, valid bits 8 |
| Mono 10/12/14/16 unpacked in 16-bit words | `Mono16`, valid bits 10/12/14/16 |
| Mono 10 packed LSB | `Mono10PackedLsb`, valid bits 10 |
| Mono 12 packed LSB | `Mono12PackedLsb`, valid bits 12 |
| RGB packed 8-bit | `RGB24`, channels 3, valid bits 8 |
| BGR packed 8-bit | `BGR24`, channels 3, valid bits 8 |
| BGRA packed 8-bit | `BGRA32`, channels 4, valid bits 8 |
| Bayer RG/GR/GB/BG 8-bit | `BayerRGGB8`, `BayerGRBG8`, `BayerGBRG8`, or `BayerBGGR8` |
| Float 32-bit single channel | `Float32`, valid bits 32 |

Vendor-specific adapters should fail clearly when an SDK reports unsupported planar, YUV, compressed, signed, or packed Bayer formats. Silent channel swapping is worse than refusing to render.

## Adapter Acceptance Checklist

- The project owner has explicitly approved reintroducing a direct vendor integration after the legal gate passed.
- The exact developer, purpose, SDK version, hardware state, distribution model, and compatibility wording pass [vendor-sdk-license-policy.md](vendor-sdk-license-policy.md).
- The adapter has no hard dependency in the core viewer unless the SDK is already installed by the target app.
- The Visual Studio provider is registered only for exact SDK target types, not for every `object`.
- The adapter captures width, height, stride, pixel format, valid bits, byte order, and buffer length.
- The adapter distinguishes a whole transport/chunk buffer from the actual image-data pointer and length.
- The adapter documents source buffer lifetime. If the SDK owns the memory only until the next grab callback, the adapter must snapshot immediately or keep the SDK buffer pinned/owned until transfer completes.
- Tests cover descriptor mapping and at least one chunked pointer read path.
- Installed-VSIX evidence uses a real SDK object or an official vendor emulator, not only a class with similar member names.
- 3D, multi-component, compressed, and otherwise unsupported payloads are rejected explicitly instead of being flattened into a guessed 2D image.
- Technical validation never overrides a failed or unknown license gate.

## References Checked

- EMVA GenICam PFNC downloads: https://www.emva.org/standards-technology/genicam/genicam-downloads/
- Basler pylon .NET `IGrabResult`: https://docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IGrabResult
- Basler camera emulation: https://docs.baslerweb.com/camera-emulation
- Spinnaker image API: https://softwareservices.flir.com/spinnaker/latest/class_spinnaker_1_1_image.html
- Allied Vision Vimba X .NET manual: https://docs.alliedvision.com/dotNetAPIManual.html
- IDS peak official examples: https://github.com/ids-imaging/ids-peak-examples
- Euresys eGrabber guide: https://documentation.euresys.com/Products/eGrabber/eGrabber_25_12/00/en-us/Content/IOdoc/egrabber.html
- HIKROBOT MVS product/download page: https://www.hikrobotics.com/en/machinevision/visionproduct/?id=44&typeId=78
- Teledyne DALSA SDK downloads: https://www.teledynedalsa.com/en/support/downloads-center/software-development-kits/
- Zebra Aurora Imaging Library: https://www.zebra.com/us/en/software/machine-vision-and-fixed-industrial-scanning-software/aurora-imaging-library.html

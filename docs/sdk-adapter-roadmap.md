# SDK Adapter Roadmap

The first-class extension target for arbitrary acquisition SDKs is `RawBufferView`.

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

Direct vendor adapters are possible, but should be added only when a public-property shape cannot safely express stride, image offset, or lifetime. The detailed evidence matrix is in `docs/industrial-camera-compatibility-validation.md`.

| Vendor / SDK | Current automatic-contract position | Adapter direction |
| --- | --- | --- |
| Basler pylon .NET | `PixelDataPointer` is safe only with zero `PaddingX` and exact contiguous `PayloadSize`; `ComputeStride()` is a method and is not invoked | A small user-side wrapper can call `ComputeStride()` and expose `RawBufferView`; snapshot if grab-result ownership cannot span the paused read |
| Teledyne FLIR Spinnaker | `DataPtr` + explicit `Stride` is structurally supported; runtime/lifetime unverified | Keep the managed image alive through the read or snapshot before release |
| Allied Vision Vimba X | `ImageData` is preferred over `Buffer`; differing addresses are rejected as a chunk/image offset | Adapter must compute the image-data length after the offset and retain/requeue `IFrame` correctly |
| IDS peak ICV | `Data`, width, height, pixel format, exact `SizeInBytes` contract tested; assembly metadata verified for ICV 1.4.0 | Expose explicit stride when runtime buffers are padded; retain the disposable image |
| HIKROBOT MVS SDK | Exact current managed frame contract not verified | Map the documented frame output object only after a legal SDK/runtime sample is available |
| Euresys eGrabber | Scoped buffer metadata is method-based and intentionally outside Automatic Vision Inspector | Adapter should call `GetInfo<T>()` while the scoped buffer is valid and expose/snapshot a `RawBufferView` |
| Teledyne DALSA Sapera LT | Exact current C# contract not verified | Audit `SapBuffer` address/format/stride/lifetime from the installed SDK before implementation |
| Zebra Aurora Imaging Library | Exact current buffer contract not verified | Audit bands, depth, packed/planar layout, host address, and lifetime before implementation |

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

- The adapter has no hard dependency in the core viewer unless the SDK is already installed by the target app.
- The Visual Studio provider is registered only for exact SDK target types, not for every `object`.
- The adapter captures width, height, stride, pixel format, valid bits, byte order, and buffer length.
- The adapter distinguishes a whole transport/chunk buffer from the actual image-data pointer and length.
- The adapter documents source buffer lifetime. If the SDK owns the memory only until the next grab callback, the adapter must snapshot immediately or keep the SDK buffer pinned/owned until transfer completes.
- Tests cover descriptor mapping and at least one chunked pointer read path.
- Installed-VSIX evidence uses a real SDK object or an official vendor emulator, not only a class with similar member names.

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

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

Direct vendor adapters are possible, but should be added after the generic `RawBufferView` path is stable in real debugging sessions.

| Vendor / SDK | Priority | Adapter Direction |
| --- | --- | --- |
| Basler pylon .NET | High | Map `IGrabResult`/`IImage` width, height, pixel type, payload data, and buffer lifetime into `RawBufferView` or `RawBufferSnapshot`. |
| HIKROBOT MVS SDK | High | Map frame output info width, height, pixel type, frame length, and data pointer into `RawBufferView`. |
| Teledyne FLIR Spinnaker | High | Map image width, height, pixel format, data pointer, payload size, and stride/padding into `RawBufferView`. |
| Euresys eGrabber | Very high | Map GenTL buffer metadata width, height, pixel format, and buffer pointer into `RawBufferView`. |
| Teledyne DALSA Sapera LT | Very high | Map `SapBuffer` width, height, format, and virtual address into `RawBufferView`. |
| Zebra Aurora / MIL | High | Map MIL/Aurora image buffer dimensions, bands/channels, depth/type, and host address into `RawBufferView`. |

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
- The adapter documents source buffer lifetime. If the SDK owns the memory only until the next grab callback, the adapter must snapshot immediately or keep the SDK buffer pinned/owned until transfer completes.
- Tests cover descriptor mapping and at least one chunked pointer read path.

## References Checked

- Emgu CV `Mat` exposes `DataPointer`, `Step`, `Rows`, `Cols`, `Depth`, and `NumberOfChannels`: https://www.emgu.com/wiki/files/4.5.5/document/html/P_Emgu_CV_Mat_DataPointer.htm
- Basler pylon .NET `IGrabResult` derives from `IImage`; valid image data depends on pixel type, width, and height, and `PixelData` is provided unless a custom buffer factory changes the storage: https://ja.docs.baslerweb.com/pylonapi/net/T_Basler_Pylon_IGrabResult
- Spinnaker image APIs expose width, height, pixel format, and raw data pointer access in the image model: https://softwareservices.flir.com/spinnaker/latest/class_spinnaker_1_1_image.html
- Euresys eGrabber buffer metadata includes width, height, and pixel format through GenTL buffer info: https://documentation.euresys.com/Products/COAXLINK/COAXLINK_23_02/en-us/Content/IOdoc/egrabber.html
- Sapera LT documentation describes acquisition width, height, and format being used to create compatible buffers: https://ftp.stemmer-imaging.com/webdavs/docmanager/164129-SaperaLT-User-Manual-V8.6.pdf
- Zebra Aurora / MIL documentation describes buffers for mono integer, floating point, packed/planar RGB, and YUV images: https://cdn.graftek.com/wp-content/uploads/2023/03/10230141/Matrox-Imaging-Library-X.pdf

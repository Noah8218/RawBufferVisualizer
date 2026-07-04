# Raw Buffer Visualizer

Raw Buffer Visualizer is a Windows desktop utility for C# machine-vision developers who need to inspect image buffers before they become `Mat`, `Bitmap`, or another high-level image type.

## Current MVP

- `net472` and modern .NET compatible core library.
- Raw descriptor validation for width, height, stride, pixel format, valid bits, and byte order.
- BGRA rendering for `Mono8`, `Mono16`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and 8-bit Bayer patterns.
- Snapshot SDK for `byte[]`, `ushort[]`, `float[]`, and `IntPtr`.
- Bitmap adapter for `System.Drawing.Bitmap` snapshots.
- OpenCvSharp adapter for `Mat` snapshots.
- WPF viewer for `.rbuf.json` metadata plus `.raw` payload files.
- Pixel inspector, histogram, zoom, and diagnostics panel.

## Snapshot format

The MVP uses two files:

```text
image.raw
image.rbuf.json
```

Example metadata:

```json
{
  "rawFile": "image.raw",
  "width": 2448,
  "height": 2048,
  "stride": 2448,
  "pixelFormat": "Mono8",
  "validBits": 8,
  "byteOrder": "LittleEndian"
}
```

## Quick start

Build everything:

```powershell
dotnet build .\RawBufferVisualizer.sln
```

Run self-tests:

```powershell
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj
```

Create a sample buffer:

```powershell
dotnet run --project .\samples\RawBufferVisualizer.Samples\RawBufferVisualizer.Samples.csproj
```

Open the WPF viewer:

```powershell
dotnet run --project .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj -f net10.0-windows -- .\artifacts\samples\mono8-gradient.rbuf.json
```

For .NET Framework deployments, build the `net472` target:

```powershell
dotnet build .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj -f net472
```

## SDK example

```csharp
var descriptor = new RawImageDescriptor
{
    Width = 2448,
    Height = 2048,
    Stride = 2448,
    PixelFormat = RawPixelFormat.Mono8,
    ValidBits = 8,
    ByteOrder = RawByteOrder.LittleEndian
};

RawBufferSnapshot.Save("cam1.rbuf.json", buffer, descriptor);
```

Bitmap adapter:

```csharp
using RawBufferVisualizer.BitmapAdapter;

var snapshot = BitmapSnapshot.FromBitmap(bitmap);
snapshot.Save("bitmap.rbuf.json");
```

OpenCvSharp adapter:

```csharp
using RawBufferVisualizer.OpenCvSharpAdapter;

var snapshot = MatSnapshot.FromMat(mat);
snapshot.Save("mat.rbuf.json");
```

`Mat` support stays in a separate project so applications that do not use OpenCvSharp do not inherit that dependency.

## GitHub setup

Local git is enough for history on this machine. For GitHub sync, create an empty GitHub repository or provide a remote URL, then run:

```powershell
git remote add origin https://github.com/<owner>/<repo>.git
git push -u origin master
```

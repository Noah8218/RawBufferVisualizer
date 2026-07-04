# Raw Buffer Visualizer

Raw Buffer Visualizer is a Windows desktop Image Watch utility for C# machine-vision developers who need to inspect image buffers before they become `Mat`, `Bitmap`, or another high-level image type.

The current priority is Image Watch / Raw Buffer Inspector work: raw buffers, `Mat`, `Bitmap`, `IntPtr`, pixel formats, large-image display, and inspection ergonomics. `Vision Replay Debugger` remains a later product direction, not the first development target.

- Product concept: [PRODUCT_CONCEPT.md](PRODUCT_CONCEPT.md)
- `.vrec` package draft: [docs/vrec-format-v0.md](docs/vrec-format-v0.md)

## Current roadmap

1. Finish the standalone Windows Image Watch program.
2. Publish the local repository to GitHub after a remote repository is available.
3. Add Visual Studio integration after the standalone viewer is stable.

## Current MVP

- `net472` and modern .NET compatible core library.
- Raw descriptor validation for width, height, stride, pixel format, valid bits, and byte order.
- BGRA rendering for `Mono8`, `Mono16`, packed `Mono10`/`Mono12` LSB, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and 8-bit Bayer patterns.
- Snapshot SDK for `byte[]`, `ushort[]`, `float[]`, and `IntPtr`.
- Bitmap adapter for `System.Drawing.Bitmap` snapshots.
- OpenCvSharp adapter for `Mat` snapshots.
- Vision Recorder SDK v0 for writing `.vrec` packages with `manifest.json` and raw image payloads.
- WPF viewer for `.rbuf.json` metadata plus `.raw` payload files.
- Drag/drop open, PNG export, snapshot export, pixel inspector, histogram, zoom, and diagnostics panel.
- WPF OpenGL canvas for tiled texture display.
- Large-image guard: CPU histogram/PNG cache is skipped above 512 MB, while OpenGL display remains tiled.

## WPF OpenGL image canvas

The viewer uses tiled texture upload instead of one full-frame WPF bitmap. The current shared rule is:

- default tile size: `5000 x 5000`
- tile planner: `RawImageTilePlanner.CreateTiles(width, height)`
- memory estimate: `RawImageTilePlanner.EstimateBgraByteCount(descriptor)`
- WPF canvas project: `RawBufferVisualizer.OpenGlCanvas`

This mirrors the local OpenGL ImageCanvas tiling approach, but the control is implemented as a WPF `UserControl` instead of a WinForms host.

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

Run WPF sample smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeOpenSamples.ps1
```

Create a sample buffer:

```powershell
dotnet run --project .\samples\RawBufferVisualizer.Samples\RawBufferVisualizer.Samples.csproj
```

The sample project creates `.rbuf.json` snapshots for every currently supported pixel format:

- `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`
- `RGB24`, `BGR24`, `BGRA32`
- `Float32`
- `BayerRGGB8`, `BayerGRBG8`, `BayerGBRG8`, `BayerBGGR8`

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
git push -u origin codex/initial-mvp
```

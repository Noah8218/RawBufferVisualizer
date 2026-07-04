# Raw Buffer Visualizer

[![CI](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml)

Raw Buffer Visualizer is a Windows desktop Image Watch utility for C# machine-vision developers who need to inspect image buffers before they become `Mat`, `Bitmap`, or another high-level image type.

The current priority is Image Watch / Raw Buffer Inspector work: raw buffers, `Mat`, `Bitmap`, `IntPtr`, pixel formats, large-image display, and inspection ergonomics. The final product goal is Visual Studio debugger integration similar to Image Watch.

- Product concept: [PRODUCT_CONCEPT.md](PRODUCT_CONCEPT.md)
- Visual Studio integration plan: [docs/visual-studio-integration.md](docs/visual-studio-integration.md)

## Current roadmap

1. Finish the standalone Windows Image Watch program.
2. Keep GitHub updated and produce release-ready Windows packages.
3. Add Visual Studio integration so supported image variables can be inspected while debugging.

## Current MVP

- `net472` and modern .NET compatible core library.
- Raw descriptor validation for width, height, stride, pixel format, valid bits, and byte order.
- BGRA rendering for `Mono8`, `Mono16`, packed `Mono10`/`Mono12` LSB, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, and 8-bit Bayer patterns.
- Snapshot SDK for `byte[]`, `ushort[]`, `float[]`, and `IntPtr`.
- Bitmap adapter for `System.Drawing.Bitmap` snapshots.
- OpenCvSharp adapter for `Mat` snapshots.
- Visual Studio bridge prototype for `RawBufferSnapshot` transfer and standalone viewer launch preparation.
- WPF viewer for `.rbuf.json` metadata plus `.raw` payload files.
- Drag/drop open, PNG export, snapshot export, pixel inspector, histogram, zoom, and diagnostics panel.
- WPF tiled canvas for large-image display.
- Large-image guard: CPU histogram/PNG cache is skipped above 512 MB, while tiled display remains available.
- Windows publish script for release-ready viewer zip packages.

## Download and run

No GitHub Release has been published yet. Until the first version tag is created, use the latest successful CI artifact:

1. Open the latest successful [CI run](https://github.com/Noah8218/RawBufferVisualizer/actions/workflows/ci.yml).
2. Download `RawBufferVisualizer-net10.0-windows-win-x64-sc.zip` from `Artifacts`.
3. Extract the zip to a writable folder such as `C:\Tools\RawBufferVisualizer`.
4. Run `RawBufferVisualizer.Wpf.exe`.
5. Click `Open Sample` to verify the viewer immediately.

After the first tagged release is created, download the same zip from the [Releases page](https://github.com/Noah8218/RawBufferVisualizer/releases).

The default package is self-contained for Windows x64, so it does not require installing a .NET runtime. If Windows SmartScreen appears because the executable is unsigned, choose `More info` and `Run anyway`, or unblock the zip before extracting it.

To inspect your own data:

- Drag and drop `.rbuf.json`, `.raw`, or `.bin` files into the window.
- Use `Open` for `.rbuf.json`, `.raw`, or `.bin`.
- For raw `.raw` or `.bin` files, fill in width, height, stride, pixel format, valid bits, and byte order, then click `Apply`.
- Keep `.raw` payload files beside their `.rbuf.json` metadata files.

## WPF Large Image Canvas

The viewer uses tiled display instead of one full-frame WPF bitmap. The current shared rule is:

- default tile size: `5000 x 5000`
- tile planner: `RawImageTilePlanner.CreateTiles(width, height)`
- memory estimate: `RawImageTilePlanner.EstimateBgraByteCount(descriptor)`

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

The viewer toolbar also includes `Open Sample` and `Sample Folder` when samples exist beside the packaged exe or under `artifacts\samples`.

For .NET Framework deployments, build the `net472` target:

```powershell
dotnet build .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj -f net472
```

## Publish a Windows package

Create a self-contained Windows x64 package for GitHub Releases:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Windows.ps1
```

The default output is:

```text
artifacts\publish\RawBufferVisualizer-net10.0-windows-win-x64-sc\
artifacts\publish\RawBufferVisualizer-net10.0-windows-win-x64-sc.zip
```

Create a .NET Framework 4.7.2 package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Windows.ps1 -Framework net472
```

Samples are copied into the package by default. Use `-SkipSamples` for a smaller package or `-NoZip` when only the publish folder is needed. The `net472` package requires .NET Framework 4.7.2 or newer on the target PC.

## Create a GitHub Release

GitHub Releases are created automatically from semantic version tags:

```powershell
git tag -a v0.1.0 -m "v0.1.0"
git push origin v0.1.0
```

The `Release` workflow builds, tests, publishes the Windows x64 zip, and attaches it to the GitHub Release.

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

## Visual Studio integration target

The standalone viewer is the first surface. The final target is Visual Studio integration for debugger-time image inspection:

- `RawBufferSnapshot` first
- `Bitmap` and OpenCvSharp `Mat` adapters next
- raw pointer buffers only when width, height, stride, pixel format, and ownership metadata are available
- same viewer behavior for zoom, pixel inspection, histogram, diagnostics, and export

The first implementation plan is documented in [docs/visual-studio-integration.md](docs/visual-studio-integration.md).

## GitHub setup

This repository currently tracks:

```text
https://github.com/Noah8218/RawBufferVisualizer.git
```

Push local commits with:

```powershell
git push
```

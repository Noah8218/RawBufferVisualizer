# Large Image Samples

Use this when validating `100000 x 100000` and `200000 x 200000` images on another PC.

The sample generator creates `.rbuf.json` metadata plus sparse `.raw` payloads. Do not store these files in Git. Generate them on the target PC because copying sparse files can expand them to their full logical size.

## Generate Mono8 Samples

```powershell
cd C:\Git\RawBufferVisualizer
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -BuildViewer
```

Output:

```text
artifacts\large-samples\large-100000x100000-mono8.rbuf.json
artifacts\large-samples\large-100000x100000-mono8.raw
artifacts\large-samples\large-200000x200000-mono8.rbuf.json
artifacts\large-samples\large-200000x200000-mono8.raw
```

Logical payload sizes:

| Sample | Pixel format | Logical raw size |
| --- | --- | --- |
| `100000 x 100000` | `Mono8` | 10 GB |
| `200000 x 200000` | `Mono8` | 40 GB |

The files are sparse on NTFS, so physical disk usage is much smaller than the logical size.

## Open In Viewer

```powershell
.\.build\bin\RawBufferVisualizer.Wpf\Debug\net472\RawBufferVisualizer.Wpf.exe .\artifacts\large-samples\large-100000x100000-mono8.rbuf.json
```

Or generate and open the first sample:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -BuildViewer -Open
```

## Generate Color Samples

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -PixelFormat Mono8,BGR24 -BuildViewer
```

Color logical payload sizes:

| Sample | Pixel format | Logical raw size |
| --- | --- | --- |
| `100000 x 100000` | `BGR24` | 30 GB |
| `200000 x 200000` | `BGR24` | 120 GB |

Use color samples only when validating channel order or color tiled rendering. Mono8 is enough for basic large-image pan, zoom, tile, and file-backed loading validation.

## Custom Sizes

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -Sizes 100000,200000 -PixelFormat Mono8,Mono16,BGR24
```

Supported sample formats:

- `Mono8`
- `Mono16`
- `BGR24`
- `BGRA32`
- `Float32`

## Expected Checks

After opening each `.rbuf.json`:

- Status shows the requested width, height, pixel format, and tile count.
- Fit view renders a nonblank striped pattern.
- Mouse wheel zoom and drag pan remain responsive.
- Pixel status changes as the cursor moves.
- `Save` exports the visible view as PNG.

## Notes

- Run on an NTFS volume. The script intentionally fails if sparse files are not supported.
- Do not use normal file copy tools to move generated `.raw` files between PCs. Regenerate them on each test PC.
- The `.rbuf.json` metadata file is tiny and can be inspected or copied safely.

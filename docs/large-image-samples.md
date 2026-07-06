# Large Image Samples

Use this when validating `100000 x 100000` and `200000 x 200000` images on another PC.

The sample generator creates `.rbuf.json` metadata plus `.raw` payloads. Do not store these files in Git.

By default the payloads are sparse files. Use `-Dense` when you need the full raw payload physically written to disk.

## Generate Mono8 Samples

```powershell
cd C:\Git\RawBufferVisualizer
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -BuildViewer
```

Output:

```text
artifacts\large-samples\large-100000x100000-mono8-sparse.rbuf.json
artifacts\large-samples\large-100000x100000-mono8-sparse.raw
artifacts\large-samples\large-200000x200000-mono8-sparse.rbuf.json
artifacts\large-samples\large-200000x200000-mono8-sparse.raw
```

Logical payload sizes:

| Sample | Pixel format | Logical raw size |
| --- | --- | --- |
| `100000 x 100000` | `Mono8` | 10 GB |
| `200000 x 200000` | `Mono8` | 40 GB |

The files are sparse on NTFS, so physical disk usage is much smaller than the logical size.

## Generate Dense Mono8 Sample

Use this when validating real disk I/O with every row physically written:

```powershell
cd C:\Git\RawBufferVisualizer
powershell -ExecutionPolicy Bypass -File .\scripts\New-LargeSampleImages.ps1 -Sizes 100000 -PixelFormat Mono8 -Dense -BuildViewer
```

Output:

```text
artifacts\large-samples\large-100000x100000-mono8-dense.rbuf.json
artifacts\large-samples\large-100000x100000-mono8-dense.raw
```

This writes about `10 GB` for `100000 x 100000 Mono8`. Ensure the target drive has enough free space before running it.

## Open In Viewer

```powershell
.\.build\bin\RawBufferVisualizer.Wpf\Debug\net472\RawBufferVisualizer.Wpf.exe .\artifacts\large-samples\large-100000x100000-mono8-sparse.rbuf.json
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

## Automated Dense Smoke

This writes a full `10 GB` raw file, opens it in the viewer, checks status text, and captures a nonblank screenshot:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Width 100000 -Height 100000 -PixelFormat Mono8 -Dense -NoBuild -OutputDir artifacts\ui\large-file-backed-100000x100000-dense
```

Expected status:

```text
100000 x 100000, Mono8, 10,000,000,000 bytes, tiles 9,604
```

For the full `200000 x 200000 Mono8` dense test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeLargeFileBacked.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -Width 200000 -Height 200000 -PixelFormat Mono8 -Dense -NoBuild -OutputDir artifacts\ui\large-file-backed-200000x200000-dense
```

Expected status:

```text
200000 x 200000, Mono8, 40,000,000,000 bytes, tiles 38,416
```

To confirm that the raw file is not sparse:

```powershell
fsutil sparse queryflag C:\Git\RawBufferVisualizer\artifacts\large-file-backed\huge-100000x100000-mono8-dense.raw
fsutil sparse queryflag C:\Git\RawBufferVisualizer\artifacts\large-file-backed\huge-200000x200000-mono8-dense.raw
```

Expected result:

```text
This file is NOT set as sparse
```

## Notes

- Run on an NTFS volume. The script intentionally fails if sparse files are not supported.
- Do not use normal file copy tools to move generated `.raw` files between PCs. Regenerate them on each test PC.
- The `.rbuf.json` metadata file is tiny and can be inspected or copied safely.

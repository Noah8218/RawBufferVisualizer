# Documentation Image Sources

Use this scenario when a human-readable debugger demonstration is more useful than the deterministic stripe and gradient fixtures. The existing procedural fixtures remain the source of truth for exact pixel-format, packing, and stride regression checks.

## Source Attribution

| Item | Value |
| --- | --- |
| Image | `Printed circuit boards 20240831 083726.jpg` |
| Author | IMG 4512 |
| Source page | https://commons.wikimedia.org/wiki/File:Printed_circuit_boards_20240831_083726.jpg |
| License | CC0 1.0 Universal public-domain dedication |
| Test derivative | Wikimedia Commons 1280 x 960 preview |
| Local SHA-256 | `E833DFE885BBB08D85F091D452A0B4FB7182C7B8A08EFF103AC49522597C9D46` |

| Item | Value |
| --- | --- |
| Image | `Mesin CNC.jpg` |
| Author | Naufal Shiddiq Fadhilah |
| Source page | https://commons.wikimedia.org/wiki/File:Mesin_CNC.jpg |
| License | CC0 1.0 Universal public-domain dedication |
| Test source | Wikimedia Commons original, 1280 x 720 |
| Local SHA-256 | `64901B8C11E37287060B997C891D219EA6DD47C53F1B5EF0758286AC722F25EF` |

The original photographs are not committed to the repository. Keep the source page, author, license, exact downloaded-file hash, and capture date with any published screenshot. CC0 does not require attribution, but retain it for traceability. Product screenshots do not imply affiliation with or endorsement by any product marks incidentally visible in the source photograph.

The following documentation captures were recorded on 2026-11-02 with Raw Buffer Visualizer 2.2.4.0 in Visual Studio 2026 18.10.12217.157:

| Files under `docs/images` | Source and demonstrated action |
| --- | --- |
| `debugger-memory-image.gif`, `pixel-neighborhood.gif` | The CNC photograph above; Locals entry into the docked viewer, then zoom and pixel-neighborhood inspection |
| `manual-stride.gif`, `manual-stride-before.png`, `manual-stride-after.png` | The PCB preview above; a snapshot file with 80 padding bytes per row, manually corrected from a 3,840-byte to a 3,920-byte stride in Format |

The GIFs are edited demonstrations. The pixel-neighborhood clip holds its final frame for readability. They do not measure processing time or show automatic detection of the manually entered stride.

# Buffer Doctor Design

Status: Implemented (Phase 1 in working tree). Phase 2 (top-down/Bayer phase/planar) is deferred.
Last updated: 2026-07-26.

## Goal

When an image looks broken (diagonal shear, torn rows, wrong brightness), Raw Buffer Visualizer should answer:

> "Which width/height/stride/pixel-format interpretation explains this buffer?"

The user presses **Diagnose Buffer** and gets a ranked list of interpretation candidates with scores, reasons, and thumbnails, instead of editing Width, Height, Stride, and Pixel Format one at a time in the Interpret section.

Product sentence: "Not just a tool that shows the image — a tool that tells you why the image is broken."

This feature is a direct extension of the product principle "Raw-buffer diagnosis is the differentiator" (`PRODUCT_DIRECTION_AND_ROADMAP.md`). It builds on the existing Interpret section (`RawBufferToolWindowControl.xaml`) and `RawImageSource.WithDescriptor` reinterpretation.

## Non-Goals And Honesty Rules

- **Never promise automatic detection.** RGB vs BGR, Bayer phase, and sometimes width/height factoring are mathematically ambiguous for real scenes. Public copy must say "suggests ranked candidates", never "detects the correct format".
- Candidates the tool cannot distinguish (e.g. `RGB24` vs `BGR24`, Bayer variants) are shown as a tied group with an explicit "cannot be distinguished from buffer content" reason.
- No camera/vendor SDK knowledge. Heuristics stay generic (alignment, common sensor sizes, content continuity).
- Scoring must never require reading the full buffer. All content checks run on bounded samples.

## User Flow

1. User opens an image (debugger handoff, `.rbuf.json`, live process memory) and it looks broken.
2. User clicks **Diagnose Buffer** in the Interpret section.
3. A candidate panel opens (Inspector area, dock-friendly). It lists up to 8 candidates:
   - thumbnail preview, score (0-100), descriptor fields, padding per row, and short reasons
   - e.g. "Stride 2560 = 2448 px + 112 bytes/row padding (32-byte aligned)"
4. User selects a candidate; the main viewer applies it immediately via `WithDescriptor` (no debugger round-trip).
5. User can still fine-tune in the existing Interpret controls afterwards.
6. (Later, with Smart Type Mapper) the accepted interpretation can be saved as the default for the source type.

## Core Design

New pure-managed code in `RawBufferVisualizer.Core`. No new dependencies.

### Types

```text
BufferInterpretationCandidate
    Descriptor        RawImageDescriptor   (Width/Height/Stride/PixelFormat/ValidBits/ByteOrder)
    Score             int                  (0-100)
    Reasons           IReadOnlyList<string>
    IsAmbiguousWithGroup  bool             (e.g. RGB/BGR tie group)

BufferDiagnosisResult
    Candidates        IReadOnlyList<BufferInterpretationCandidate>  (sorted, top 8)
    Notes             IReadOnlyList<RawDiagnostic>                  (structural findings about the buffer itself)
```

### Generation (`BufferInterpretationCandidateGenerator`)

Input: `bufferLength` (+ optional current descriptor as a hint for width/height seeds).

For each pixel format family (bytes-per-pixel from `RawImageDescriptor.GetBytesPerPixel`, packed strides from `GetMinimumStride` logic):

1. **Tight fit**: factor `bufferLength` into `width * height * bytesPerPixel` exactly; stride = minimum stride.
2. **Aligned-stride fit**: for alignment `A` in {4, 8, 16, 32, 64, 128, 256}, for plausible widths (divisors of length-derived ranges plus common sensor widths 320-8192), `stride = AlignUp(minStride, A)` and require `stride * height == bufferLength` exactly, or `stride * (height - 1) + minStride == bufferLength` (trailing-row variant, matching `GetRequiredByteCount`).
3. **Hint seeds**: if a current descriptor exists, re-derive its width/height with other formats (e.g. same byte length read as Mono16 vs Mono8).
4. Common camera widths (640, 1280, 1920, 2048, 2448, 2592, 4096) are tried as seeds when they divide plausibly.

Dedupe by (Width, Height, Stride, PixelFormat, ByteOrder). Cap pre-score pool at ~40 candidates. `RawBufferDiagnostics.AnalyzeLength` must report no Error for a candidate to enter the pool.

Endianness and ValidBits variants (Mono16: LE/BE, valid bits 10/12/14/16) expand surviving Mono16 candidates.

### Scoring (`BufferInterpretationScorer`)

Score = structural (max 40) + content (max 60), clamped to 0-100.

Structural:
- exact length fit (+15), trailing-row fit (+10)
- stride alignment to 16/32/64 (+10), alignment 4/8 (+5)
- plausible dimensions: both in [16, 65536], aspect ratio in [1:16, 16:1] (+5)
- known sensor width (+5)
- padding per row in (0, 512] (+5)

Content (sampled):
- **Row continuity**: on sampled rows, compare the first/last N pixels of adjacent rows; a correct stride yields high continuity, a wrong stride yields shear (+up to 30, scaled by measured correlation).
- **Mono16 valid-bits fit**: sampled values exceeding `2^ValidBits` reduce score; values matching expected distribution raise it (+up to 15).
- **Endianness**: compare LE vs BE sampled value smoothness (mean absolute delta of neighbors); the smoother order wins (+up to 15 to the winner).
- RGB/BGR and Bayer variants: not content-scored; emitted as ambiguous tie groups.

Sampling rules (hard caps):
- at most 64 rows, at most 4 MiB total bytes read, evenly spaced rows;
- every read goes through the new range-read API below;
- scoring accepts a `CancellationToken`.

### Range Read API

`RawImageSource` gains:

```csharp
public virtual bool TryReadRange(long offset, byte[] destination, int offsetInDestination, int count)
```

- `MemoryRawImageSource`: array copy.
- `RandomAccessRawImageSource` / file-backed: stream seek + read (existing stream discipline).
- `ProcessMemoryRawImageSource`: process-memory read at base + offset, fails cleanly when the live source is unavailable.

Default implementation returns `false`; the scorer skips content scoring (structural-only score, marked in Reasons) when range reads are unsupported.

### Existing Reuse

- Candidate validity filter: `RawBufferDiagnostics.AnalyzeLength` (no Error allowed).
- Applying a candidate: `RawImageSource.WithDescriptor` (already allocation-free for file/process sources).
- Thumbnails: existing document thumbnail path (`CreateThumbnailSource`) with `RenderTileSampled`.
- Diagnostics output: `RawDiagnostic`/`RawDiagnosticSeverity` for buffer-level notes.

## UI Design (Vssdk)

- New **Diagnose Buffer** button inside the existing Interpret section (no toolbar growth; narrow-dock contract preserved).
- Candidate panel hosted in the Inspector region: list rows with thumbnail, score badge, descriptor summary, reasons tooltip; selecting a row applies the candidate to the active document; a note row appears when candidates are ambiguous ("RGB24 and BGR24 cannot be distinguished from content").
- Panel is read-only about the original descriptor; Cancel restores the pre-diagnosis descriptor (which is just "don't apply").
- Rendering implementation names stay out of all user-facing text (AGENTS.md).

## Phases

### Phase 1 (this implementation)

- Candidate generation + scoring + range-read API + tests.
- Interpret section gains Width/Height edit boxes (descriptor already supports them).
- Diagnose Buffer panel with apply.
- Formats: Mono8, Mono16 (LE/BE, valid bits), RGB24/BGR24, BGRA32, Mono10/12PackedLsb, Float32. Bayer variants listed only as ambiguous group when 1-byte-per-pixel candidates score high and the user descriptor mentions Bayer.

### Phase 2 (separate change, needs renderer work)

- Top-down vs bottom-up row order (descriptor/render option does not exist yet).
- Bayer phase offset candidates.
- Planar vs interleaved.
- "Save interpretation for this type" hand-off to Smart Type Mapper.

## Tests (`RawBufferVisualizer.Tests`)

Deterministic synthetic buffers:

1. Mono8 2448x2048 stride 2560 (112B padding) → top candidate is exactly that descriptor; current wrong descriptor (stride = width) scores lower.
2. Diagonal-shear case: gradient image stored with padded stride → correct stride wins on row-continuity score.
3. Mono16 BE vs LE on a smooth ramp → correct endianness wins.
4. Mono16 valid bits: buffer with values < 4096 → ValidBits 12 candidate beats 16.
5. Mono12PackedLsb buffer → packed candidate found, `AnalyzeLength` clean.
6. Sampling cap: 100000x100000 file-backed source → scoring reads stay within caps (assert bytes read).
7. Ambiguity: RGB24 vs BGR24 emitted as tie group with reason.
8. Trailing-byte variant: length = stride*(h-1)+minStride accepted.

## Validation Commands

```powershell
dotnet build .\RawBufferVisualizer.sln -c Release
dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --framework net8.0-windows
powershell -STA -ExecutionPolicy Bypass -File .\scripts\SmokeDockedLayoutWidths.ps1 -Configuration Release -Framework net472 -NoBuild
```

UI change requires before/after captures under `artifacts/ui/`.

## Documentation Impact On Release

- README "Key Features" + Marketplace copy: add one bullet, worded as candidate suggestion.
- `docs/MAINTAINER_HANDOFF.md`: supported-input table unchanged; add evidence rows for the new tests.
- No VSIX version bump until the feature ships; when it does, bump via `Bump-VisualStudioExtensionVersion.ps1` and run the installed-extension matrix.

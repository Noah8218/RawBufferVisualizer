# Fast Marketplace Demo

Use this shot list for the short looping documentation GIF. The goal is to show the debugger workflow and its main inspection states quickly, without narration, slow cursor travel, or waiting frames.

## Capture Setup

- Build and install the current Release VSIX.
- Open `RawBufferVisualizer.sln` in Visual Studio 2022 with the dark theme.
- Set `RawBufferVisualizer.VisualizerDebuggee` as the startup project.
- Keep only the editor, Watch or DataTip, and the docked Raw Buffer Visualizer visible.
- For public portfolio capture, prefer the license-cleared `industrialOpenCvMat`, `industrialMonoSnapshot`, and `industrialBadStrideSnapshot` scenario from [Industrial Image Debug Testing](industrial-image-testing.md). Keep `matBgr24`, `rawMono8Snapshot`, and `imageDictionary` for deterministic regression demonstrations.
- Record at 1920 x 1080 or 1600 x 900. Hide account names, unrelated projects, desktop notifications, and private paths.
- Keep the Doctor fixture visibly labeled `BGR24`, with declared stride `7344` before correction and recovered stride `7424` after correction.

## Code DataTip Sequence

The separate three-second entry GIF uses a different CC0 industrial-machine photograph:

| Time | Action | Viewer evidence |
| ---: | --- | --- |
| 0.00-0.45 s | Show the initialized `dataTipIndustrialMat` one line above the active break. | The code object and Break Mode context are visible. |
| 0.45-1.20 s | Hover the object in the code editor. | The OpenCvSharp `Mat [720 x 1280 x CV_8UC3]` DataTip appears. |
| 1.20-1.75 s | Move onto the DataTip magnifying-glass icon and click. | The actual registered visualizer affordance is under the cursor. |
| 1.75-3.00 s | Hold the resulting docked image. | A real 1280 x 720 `BGR24` industrial-machine photograph is open. |

## Timeline

| Time | Action | Viewer evidence |
| ---: | --- | --- |
| 0.00-0.60 s | Open the registered Bitmap from the Locals visualizer menu. | The actual Raw Buffer Visualizer entry is under the cursor. |
| 0.60-1.25 s | Show the registered Bitmap. | The recognizable 1280 x 960 color PCB photograph appears in the docked viewer. |
| 1.25-1.90 s | Hover a PCB position. | X/Y, B/G/R, raw bytes, and 5 x 5 statistics are visible. |
| 1.90-2.65 s | Show the automatic industrial scan. | Supported color representations are open in one docked list. |
| 2.65-3.25 s | Show Diagnostics. | BGR24 stride, byte range, source mode, and timing are visible. |
| 3.25-4.00 s | Select the intentionally wrong `BGR24` stride fixture. | The color image is visibly row-sheared. |
| 4.00-4.80 s | Run Diagnose Buffer. | The top `BGR24`, 2448 x 2048, stride 7424 candidate is visible. |
| 4.80-6.00 s | Select the top candidate. | The correctly aligned color PCB scene is restored immediately. |

## Recording Checklist

- The GIF is 5-8 seconds total, and no individual state is held longer than 1.5 seconds.
- The main GIF starts from a real registered visualizer handoff and ends on the restored color image.
- Pixel values change while the pointer moves.
- Fit or 1:1 visibly changes the image scale.
- The docked window, image list, and Visual Studio editor are visible in the same frame.
- The Doctor input and recovered result are both visibly `BGR24`; verify that only stride metadata changes from `7344` to `7424`.
- No unrelated application UI, personal data, absolute customer path, or stale extension version is visible.
- A separate social/demo video may remain 15-30 seconds, silent by default, and readable at normal playback speed.

## Deliverables

Keep the reviewed source recording outside Git. Publish only compressed documentation assets:

```text
docs/images/raw-buffer-visualizer-breakpoint-open.gif
docs/images/raw-buffer-visualizer-datatip-open.gif
docs/images/raw-buffer-visualizer-demo.gif
docs/video/raw-buffer-visualizer-demo.mp4
```

Use the MP4 for social posts and the GIF for README or Marketplace Markdown. If the Marketplace does not animate the GIF, keep the reviewed static docked-view screenshot there and link to the MP4 or repository demo.

## Create A Continuous GIF And MP4

The tracked `2.0.6` documentation assets are a reviewed 3.0-second, 12-frame code-DataTip GIF, a 3.25-second, 13-frame Locals GIF, and the 6.0-second, 24-frame complete sequence above. All are 960 x 532 at 4 fps, use frames captured from the exact installed 2.0.6 extension, show the current labelled **Clear all** UI, and contain no procedural stripe or gradient fixture. The FFmpeg workflow below is optional for maintainers who start from a continuous screen recording; FFmpeg is not required to install or use Raw Buffer Visualizer.

The repository does not install FFmpeg automatically. Install a Windows build from the [official FFmpeg download page](https://ffmpeg.org/download.html), or use Winget manually:

```powershell
winget install --id Gyan.FFmpeg.Essentials --exact --accept-package-agreements --accept-source-agreements
```

Open a new PowerShell window and confirm both commands are available:

```powershell
ffmpeg -version
ffprobe -version
```

For a separate longer social/demo recording, select the reviewed 15-20 second interval and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Create-DemoMedia.ps1 `
  -InputPath "C:\path\raw-buffer-visualizer-recording.mp4" `
  -StartTime "00:00:25.500" `
  -EndTime "00:00:45.500" `
  -GifOutputPath ".\docs\images\raw-buffer-visualizer-demo.gif" `
  -Mp4OutputPath ".\docs\video\raw-buffer-visualizer-demo.mp4"
```

These example timestamps produce a 20-second continuous demo. The script uses `palettegen` and `paletteuse`, creates a 960 px wide 12 fps looping GIF, creates a muted H.264 MP4 within 1280 x 720 with faststart, and reports measured output metadata. They are not the timing recipe for the tracked 6.0-second documentation GIF.

Before adding any demo asset to README or Marketplace copy, apply the image review gate in `AGENTS.md` and inspect the final file at its published resolution.

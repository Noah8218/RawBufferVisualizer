# 20-Second Marketplace Demo

Use this shot list to record the debugger workflow without narration. The goal is to show the action that matters: a live C# image variable becomes an inspectable image without temporary save or conversion code.

## Capture Setup

- Build and install the current Release VSIX.
- Open `RawBufferVisualizer.sln` in Visual Studio 2022 with the dark theme.
- Set `RawBufferVisualizer.VisualizerDebuggee` as the startup project.
- Keep only the editor, Watch or DataTip, and the docked Raw Buffer Visualizer visible.
- Use the sample variables `matBgr24`, `rawMono8Snapshot`, and `imageDictionary` from `samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs`.
- Record at 1920 x 1080 or 1600 x 900. Hide account names, unrelated projects, desktop notifications, and private paths.
- Start with an empty Raw Buffer Visualizer image list.

## Timeline

| Time | Action | Viewer evidence |
| ---: | --- | --- |
| 0-3 s | Stop at `matBgr24` and open its DataTip or Watch row. | OpenCvSharp code and the live `Mat` variable are visible together. |
| 3-6 s | Click the debugger visualizer icon. | One thumbnail appears in the docked image list. |
| 6-10 s | Move the pointer over the image and use the mouse wheel to zoom. | X/Y, RGB values, swatches, raw bytes, and the high-zoom pixel overlay update. |
| 10-14 s | Open Inspector briefly. | Stride, pixel format, source type, and diagnostics are visible. |
| 14-18 s | Add `rawMono8Snapshot`, set A/B, and select Split or Diff. | Two images remain in one list and the comparison view appears. |
| 18-20 s | Hold the final frame. | Show: `Debug Mat, Bitmap and raw image buffers without leaving Visual Studio.` |

## Recording Checklist

- The visualizer icon click is visible; do not begin after the viewer has already opened.
- At least one real color image is visible.
- Pixel values change while the pointer moves.
- Zoom is performed directly over the image with the mouse wheel.
- The docked window, image list, and Visual Studio editor are visible in the same frame.
- No unrelated application UI, personal data, absolute customer path, or stale extension version is visible.
- The final video is 15-30 seconds, silent by default, and readable at normal playback speed.

## Deliverables

Keep the reviewed source recording outside Git. Publish only compressed documentation assets:

```text
docs/images/raw-buffer-visualizer-demo.gif
docs/video/raw-buffer-visualizer-demo.mp4
```

Use the MP4 for social posts and the GIF for README or Marketplace Markdown. If the Marketplace does not animate the GIF, keep the reviewed static docked-view screenshot there and link to the MP4 or repository demo.

## Create GIF And MP4

The repository does not install FFmpeg automatically. Install a Windows build from the [official FFmpeg download page](https://ffmpeg.org/download.html), or use Winget manually:

```powershell
winget install --id Gyan.FFmpeg.Essentials --exact --accept-package-agreements --accept-source-agreements
```

Open a new PowerShell window and confirm both commands are available:

```powershell
ffmpeg -version
ffprobe -version
```

Then select the reviewed 15-20 second interval and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Create-DemoMedia.ps1 `
  -InputPath "C:\path\raw-buffer-visualizer-recording.mp4" `
  -StartTime "00:00:25.500" `
  -EndTime "00:00:45.500" `
  -GifOutputPath ".\docs\images\raw-buffer-visualizer-demo.gif" `
  -Mp4OutputPath ".\docs\video\raw-buffer-visualizer-demo.mp4"
```

These timestamps reproduce the reviewed 20-second demo. The script uses `palettegen` and `paletteuse`, creates a 960 px wide 12 fps looping GIF, creates a muted H.264 MP4 within 1280 x 720 with faststart, and reports measured output metadata.

Before adding any demo asset to README or Marketplace copy, apply the image review gate in `AGENTS.md` and inspect the final file at its published resolution.

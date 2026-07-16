[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net472",
    [int]$Width = 5000,
    [int]$Height = 5000,
    [int]$ZoomIterations = 30,
    [int]$PanIterations = 30,
    [int]$MaxOpenPathMs = 3000,
    [int]$MaxCommandMs = 100,
    [int]$MaxFrameMs = 500,
    [int]$MaxUploadMs = 500,
    [int]$MaxFirstVisibleMs = 10000,
    [int]$SettleAfterInteractionMs = 2000,
    [string]$InputMetadataPath = "",
    [string]$OutputDir = "artifacts\perf\opengl-viewer",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    $arguments = @(
        "-NoProfile",
        "-STA",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $PSCommandPath,
        "-Configuration",
        $Configuration,
        "-Framework",
        $Framework,
        "-Width",
        $Width,
        "-Height",
        $Height,
        "-ZoomIterations",
        $ZoomIterations,
        "-PanIterations",
        $PanIterations,
        "-MaxOpenPathMs",
        $MaxOpenPathMs,
        "-MaxCommandMs",
        $MaxCommandMs,
        "-MaxFrameMs",
        $MaxFrameMs,
        "-MaxUploadMs",
        $MaxUploadMs,
        "-MaxFirstVisibleMs",
        $MaxFirstVisibleMs,
        "-SettleAfterInteractionMs",
        $SettleAfterInteractionMs,
        "-OutputDir",
        $OutputDir
    )

    if (-not [string]::IsNullOrWhiteSpace($InputMetadataPath)) {
        $arguments += @("-InputMetadataPath", $InputMetadataPath)
    }

    if ($NoBuild) {
        $arguments += "-NoBuild"
    }

    & powershell.exe @arguments
    exit $LASTEXITCODE
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (-not $NoBuild) {
    dotnet build .\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj --configuration $Configuration --framework $Framework | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

$bin = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework"
if (-not (Test-Path -LiteralPath $bin)) {
    throw "Viewer host output not found: $bin"
}

$outputRoot = Join-Path $repoRoot $OutputDir
$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null

$sampleName = "mono8-$Width-x-$Height"
$metadataPath = Join-Path $sampleRoot "$sampleName.rbuf.json"
$rawPath = Join-Path $sampleRoot "$sampleName.raw"
$stride = $Width
$rawLength = [int64]$stride * [int64]$Height
$materializeLimit = 512L * 1024L * 1024L

if (-not ("RawBufferViewerPerfNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferViewerPerfNative {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
    public const uint FSCTL_SET_SPARSE = 0x000900C4;
}
'@
}

function New-PatternRow([int]$rowIndex, [int]$width, [int]$stride) {
    $row = New-Object byte[] $stride
    for ($x = 0; $x -lt $width; $x++) {
        $block = ([int][Math]::Floor($x / 32.0)) + ([int][Math]::Floor($rowIndex / 32.0))
        $row[$x] = [byte](32 + ($block % 224))
    }

    $row
}

function New-Mono8Raw([string]$path, [int]$width, [int]$height, [int]$stride, [int64]$length, [int64]$sparseThreshold) {
    $existing = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
    if ($existing -and $existing.Length -eq $length) {
        return
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::ReadWrite)
    try {
        if ($length -gt $sparseThreshold) {
            $bytesReturned = 0
            $sparse = [RawBufferViewerPerfNative]::DeviceIoControl(
                $stream.SafeFileHandle.DangerousGetHandle(),
                [RawBufferViewerPerfNative]::FSCTL_SET_SPARSE,
                [IntPtr]::Zero,
                0,
                [IntPtr]::Zero,
                0,
                [ref]$bytesReturned,
                [IntPtr]::Zero)
            if (-not $sparse) {
                throw "Sparse file creation is not supported for $path"
            }

            $stream.SetLength($length)
            for ($y = 0; $y -lt $height; $y += 128) {
                $row = New-PatternRow $y $width $stride
                $stream.Position = [int64]$y * [int64]$stride
                $stream.Write($row, 0, $row.Length)
            }

            $stream.Position = $length - 1
            $stream.WriteByte(255)
            return
        }

        for ($y = 0; $y -lt $height; $y++) {
            $row = New-PatternRow $y $width $stride
            $stream.Write($row, 0, $row.Length)
        }
    }
    finally {
        $stream.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($InputMetadataPath)) {
    New-Mono8Raw $rawPath $Width $Height $stride $rawLength $materializeLimit
    $metadata = [ordered]@{
        rawFile = "$sampleName.raw"
        width = $Width
        height = $Height
        stride = $stride
        pixelFormat = "Mono8"
        validBits = 8
        byteOrder = "LittleEndian"
    } | ConvertTo-Json
    Set-Content -LiteralPath $metadataPath -Encoding UTF8 -Value $metadata
}
else {
    $metadataPath = (Resolve-Path -LiteralPath $InputMetadataPath).Path
    $metadataObject = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $Width = [int]$metadataObject.width
    $Height = [int]$metadataObject.height
    $stride = [int]$metadataObject.stride
    $rawPath = Join-Path (Split-Path -Parent $metadataPath) ([string]$metadataObject.rawFile)
    $rawLength = (Get-Item -LiteralPath $rawPath).Length
    $sampleName = [IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($metadataPath))
}

Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,WindowsFormsIntegration,System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class OpenGlSmokeCaptureNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    public static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hWnd,
        int attribute,
        out Rect value,
        int valueSize);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
'@

$vsPublicAssemblies = @(
    (Join-Path $env:ProgramFiles "Microsoft Visual Studio\2022\Community\Common7\IDE\PublicAssemblies"),
    (Join-Path $env:ProgramFiles "Microsoft Visual Studio\18\Community\Common7\IDE\PublicAssemblies")
)
$script:bin = $bin
$script:vsPublicAssemblies = $vsPublicAssemblies

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = ($args.Name -split ",")[0] + ".dll"
    foreach ($dir in @($script:bin) + $script:vsPublicAssemblies) {
        if ([string]::IsNullOrWhiteSpace($dir)) {
            continue
        }

        $path = Join-Path $dir $name
        if (Test-Path -LiteralPath $path) {
            return [Reflection.Assembly]::LoadFrom($path)
        }
    }

    return $null
})

foreach ($assemblyName in @(
    "RawBufferVisualizer.Core.dll",
    "RawBufferVisualizer.Sdk.dll",
    "SharpGL.dll",
    "SharpGL.SceneGraph.dll",
    "SharpGL.WinForms.dll",
    "RawBufferVisualizer.OpenGlCanvas.dll",
    "RawBufferVisualizer.VisualStudio.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $bin $assemblyName)) | Out-Null
}

$vssdkAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin "RawBufferVisualizer.VisualStudio.Vssdk.dll"))
$control = $vssdkAssembly.CreateInstance("RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl")
if ($null -eq $control) {
    throw "RawBufferToolWindowControl could not be created."
}

function Capture-Window([Windows.Window]$window, [string]$path) {
    $handle = ([Windows.Interop.WindowInteropHelper]::new($window)).Handle
    if ($handle -eq [IntPtr]::Zero) {
        throw "The smoke window handle is not available."
    }

    $rect = New-Object OpenGlSmokeCaptureNative+Rect
    $rectSize = [Runtime.InteropServices.Marshal]::SizeOf($rect)
    $dwmResult = [OpenGlSmokeCaptureNative]::DwmGetWindowAttribute($handle, 9, [ref]$rect, $rectSize)
    if ($dwmResult -ne 0 -and -not [OpenGlSmokeCaptureNative]::GetWindowRect($handle, [ref]$rect)) {
        throw "The smoke window bounds are not available. DWM error: $dwmResult; Win32 error: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid window bounds: $width x $height"
    }

    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $noMoveOrResize = 0x0001 -bor 0x0002 -bor 0x0040
        [OpenGlSmokeCaptureNative]::SetWindowPos($handle, [IntPtr](-1), 0, 0, 0, 0, $noMoveOrResize) | Out-Null
        $foregroundHandle = [OpenGlSmokeCaptureNative]::GetForegroundWindow()
        $foregroundThread = [OpenGlSmokeCaptureNative]::GetWindowThreadProcessId($foregroundHandle, [IntPtr]::Zero)
        $currentThread = [OpenGlSmokeCaptureNative]::GetCurrentThreadId()
        $attached = $false
        try {
            if ($foregroundThread -ne 0 -and $foregroundThread -ne $currentThread) {
                $attached = [OpenGlSmokeCaptureNative]::AttachThreadInput($currentThread, $foregroundThread, $true)
            }

            [OpenGlSmokeCaptureNative]::BringWindowToTop($handle) | Out-Null
            [OpenGlSmokeCaptureNative]::SetActiveWindow($handle) | Out-Null
            [OpenGlSmokeCaptureNative]::SetForegroundWindow($handle) | Out-Null
            [OpenGlSmokeCaptureNative]::SetFocus($handle) | Out-Null
        }
        finally {
            if ($attached) {
                [OpenGlSmokeCaptureNative]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
            }
        }

        Start-Sleep -Milliseconds 300
        $foregroundHandle = [OpenGlSmokeCaptureNative]::GetForegroundWindow()
        if ($foregroundHandle -ne $handle) {
            throw "The smoke window lost foreground focus before capture. Expected HWND=$handle; actual HWND=$foregroundHandle."
        }

        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height))
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        [OpenGlSmokeCaptureNative]::SetWindowPos($handle, [IntPtr](-2), 0, 0, 0, 0, $noMoveOrResize) | Out-Null
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Measure-Capture([string]$path) {
    $bitmap = [Drawing.Bitmap]::FromFile($path)
    try {
        $nonDark = 0
        $samples = 0
        $colors = New-Object 'System.Collections.Generic.HashSet[int]'
        for ($y = 80; $y -lt ($bitmap.Height - 40); $y += 10) {
            for ($x = 320; $x -lt ($bitmap.Width - 300); $x += 10) {
                $color = $bitmap.GetPixel($x, $y)
                $gray = [int](($color.R + $color.G + $color.B) / 3)
                if ($gray -gt 20) {
                    $nonDark++
                }

                $colors.Add($color.ToArgb()) | Out-Null
                $samples++
            }
        }

        [pscustomobject]@{
            NonDarkRatio = [Math]::Round($nonDark / [double][Math]::Max($samples, 1), 4)
            UniqueSampledColors = $colors.Count
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Convert-Stats($stats) {
    [ordered]@{
        frameCount = $stats.FrameCount
        textureUploadCount = $stats.TextureUploadCount
        averageFrameMs = [Math]::Round($stats.AverageFrameMilliseconds, 3)
        maxFrameMs = [Math]::Round($stats.MaxFrameMilliseconds, 3)
        averageUploadMs = [Math]::Round($stats.AverageTextureUploadMilliseconds, 3)
        maxUploadMs = [Math]::Round($stats.MaxTextureUploadMilliseconds, 3)
    }
}

$window = New-Object Windows.Window
$window.Title = "Raw Buffer Visualizer OpenGL Performance Smoke"
$window.Width = 1280
$window.Height = 820
$window.Left = 40
$window.Top = 40
$window.Topmost = $true
$window.Content = $control

$timer = New-Object Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(45)

$script:phase = "waitForInitialFrame"
$script:tickCount = 0
$script:zoomIndex = 0
$script:panIndex = 0
$script:openPathMs = 0.0
$script:maxZoomCommandMs = 0.0
$script:maxPanCommandMs = 0.0
$script:zoomStats = $null
$script:panStats = $null
$script:canvas = $null
$script:failure = $null
$script:firstVisibleMs = 0.0
$script:firstVisibleWatch = $null
$script:screenshotPath = Join-Path $outputRoot "open-gl-viewer-performance.png"
$script:metricsPath = Join-Path $outputRoot "open-gl-viewer-performance.json"
$zoomScales = @(0.12, 0.18, 0.25, 0.5, 1.0, 1.5, 2.0, 1.0, 0.35, 0.16)

$timer.add_Tick({
    try {
        if ($script:phase -eq "waitForInitialFrame") {
            $initialStats = $script:canvas.GetRenderStatsSnapshot()
            if ($initialStats.TextureUploadCount -le 0) {
                if ($script:firstVisibleWatch.Elapsed.TotalMilliseconds -gt $MaxFirstVisibleMs) {
                    throw "No visible texture was uploaded within $MaxFirstVisibleMs ms."
                }

                return
            }

            $script:firstVisibleWatch.Stop()
            $script:firstVisibleMs = $script:firstVisibleWatch.Elapsed.TotalMilliseconds
            $script:canvas.ResetRenderStats()
            $script:phase = "zoom"
            return
        }

        if ($script:phase -eq "zoom") {
            if ($script:zoomIndex -lt $ZoomIterations) {
                $scale = $zoomScales[$script:zoomIndex % $zoomScales.Count]
                $commandWatch = [Diagnostics.Stopwatch]::StartNew()
                $script:canvas.SetZoomScale($scale)
                $commandWatch.Stop()
                $script:maxZoomCommandMs = [Math]::Max($script:maxZoomCommandMs, $commandWatch.Elapsed.TotalMilliseconds)
                $script:zoomIndex++
                return
            }

            $script:zoomStats = $script:canvas.GetRenderStatsSnapshot()
            $script:canvas.SetZoomScale(1.0)
            $script:canvas.ResetRenderStats()
            $script:phase = "pan"
            return
        }

        if ($script:phase -eq "pan") {
            if ($script:panIndex -lt $PanIterations) {
                $dx = if (($script:panIndex % 4) -lt 2) { 160 } else { -160 }
                $dy = if (($script:panIndex % 6) -lt 3) { 96 } else { -96 }
                $commandWatch = [Diagnostics.Stopwatch]::StartNew()
                $script:canvas.PanByImagePixels($dx, $dy)
                $commandWatch.Stop()
                $script:maxPanCommandMs = [Math]::Max($script:maxPanCommandMs, $commandWatch.Elapsed.TotalMilliseconds)
                $script:panIndex++
                return
            }

            $script:canvas.FitToImage()
            $script:tickCount = 0
            $script:phase = "settleAfterInteraction"
            return
        }

        if ($script:phase -eq "settleAfterInteraction") {
            $script:tickCount++
            if (($script:tickCount * $timer.Interval.TotalMilliseconds) -lt $SettleAfterInteractionMs) {
                return
            }

            $script:panStats = $script:canvas.GetRenderStatsSnapshot()
            $timer.Stop()
            Capture-Window $window $script:screenshotPath
            $window.Close()
        }
    }
    catch {
        $script:failure = $_.Exception.ToString()
        $timer.Stop()
        $window.Close()
    }
})

$window.add_Loaded({
    try {
        $openWatch = [Diagnostics.Stopwatch]::StartNew()
        $control.OpenPath($metadataPath)
        $openWatch.Stop()
        $script:openPathMs = $openWatch.Elapsed.TotalMilliseconds

        $script:canvas = $control.FindName("OpenGlImageView")
        if ($null -eq $script:canvas) {
            throw "OpenGlImageView was not found."
        }

        $script:canvas.ResetRenderStats()
        $script:firstVisibleWatch = [Diagnostics.Stopwatch]::StartNew()
        $timer.Start()
    }
    catch {
        $script:failure = $_.Exception.ToString()
        $window.Close()
    }
})

[Windows.Application]::new().Run($window) | Out-Null

if ($script:failure) {
    throw $script:failure
}

$captureContent = Measure-Capture $script:screenshotPath
$result = [ordered]@{
    sample = [ordered]@{
        width = $Width
        height = $Height
        stride = $stride
        pixelFormat = "Mono8"
        rawLength = $rawLength
        metadataPath = $metadataPath
    }
    openPathMs = [Math]::Round($script:openPathMs, 3)
    firstVisibleMs = [Math]::Round($script:firstVisibleMs, 3)
    maxZoomCommandMs = [Math]::Round($script:maxZoomCommandMs, 3)
    maxPanCommandMs = [Math]::Round($script:maxPanCommandMs, 3)
    zoom = Convert-Stats $script:zoomStats
    pan = Convert-Stats $script:panStats
    screenshotPath = $script:screenshotPath
    nonDarkRatio = $captureContent.NonDarkRatio
    uniqueSampledColors = $captureContent.UniqueSampledColors
    thresholds = [ordered]@{
        maxOpenPathMs = $MaxOpenPathMs
        maxCommandMs = $MaxCommandMs
        maxFrameMs = $MaxFrameMs
        maxUploadMs = $MaxUploadMs
    }
}

$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $script:metricsPath -Encoding UTF8

$failures = New-Object System.Collections.Generic.List[string]
if ($script:openPathMs -gt $MaxOpenPathMs) {
    $failures.Add("OpenPathMs $([Math]::Round($script:openPathMs, 1)) > $MaxOpenPathMs")
}

if ($script:maxZoomCommandMs -gt $MaxCommandMs) {
    $failures.Add("MaxZoomCommandMs $([Math]::Round($script:maxZoomCommandMs, 1)) > $MaxCommandMs")
}

if ($script:maxPanCommandMs -gt $MaxCommandMs) {
    $failures.Add("MaxPanCommandMs $([Math]::Round($script:maxPanCommandMs, 1)) > $MaxCommandMs")
}

$statEntries = @(
    [pscustomobject]@{ Name = "zoom"; Stats = $script:zoomStats },
    [pscustomobject]@{ Name = "pan"; Stats = $script:panStats }
)
foreach ($entry in $statEntries) {
    $name = $entry.Name
    $stats = $entry.Stats
    if ($stats.MaxFrameMilliseconds -gt $MaxFrameMs) {
        $failures.Add("$name MaxFrameMs $([Math]::Round($stats.MaxFrameMilliseconds, 1)) > $MaxFrameMs")
    }

    if ($stats.MaxTextureUploadMilliseconds -gt $MaxUploadMs) {
        $failures.Add("$name MaxUploadMs $([Math]::Round($stats.MaxTextureUploadMilliseconds, 1)) > $MaxUploadMs")
    }
}

if ($captureContent.NonDarkRatio -le 0.02) {
    $failures.Add("Capture appears dark: NonDarkRatio $($captureContent.NonDarkRatio)")
}

if ($captureContent.UniqueSampledColors -lt 8) {
    $failures.Add("Capture appears blank or uniform: UniqueSampledColors $($captureContent.UniqueSampledColors)")
}

Get-Content -LiteralPath $script:metricsPath

if ($failures.Count -gt 0) {
    throw "OpenGL viewer performance smoke failed: $($failures -join '; ')"
}

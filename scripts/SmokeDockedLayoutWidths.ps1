param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\docked-layout-widths",
    [int[]]$Widths = @(540, 900, 1160),
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (-not $NoBuild) {
    dotnet build .\RawBufferVisualizer.sln --configuration $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

$outputRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
$width = 640
$height = 480
$rawPath = Join-Path $sampleRoot "layout-mono8.raw"
$metadataPath = Join-Path $sampleRoot "layout-mono8.rbuf.json"

$buffer = New-Object byte[] ($width * $height)
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $buffer[($y * $width) + $x] = [byte](32 + (($x + $y) % 224))
    }
}
[IO.File]::WriteAllBytes($rawPath, $buffer)
@{
    rawFile = "layout-mono8.raw"
    width = $width
    height = $height
    stride = $width
    pixelFormat = "Mono8"
    validBits = 8
    byteOrder = "LittleEndian"
} | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing

if (-not ("RawBufferDockedLayoutNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferDockedLayoutNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
}
'@
}

function Wait-Dispatcher([int]$Milliseconds) {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($Milliseconds)
    $timer.Add_Tick({
        $timer.Stop()
        $frame.Continue = $false
    })
    $timer.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object RawBufferDockedLayoutNative+RECT
    [RawBufferDockedLayoutNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $captureWidth = $rect.Right - $rect.Left
    $captureHeight = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $captureWidth, $captureHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($captureWidth, $captureHeight))
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$assemblyPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Vssdk.dll"
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null

$results = New-Object System.Collections.Generic.List[object]
foreach ($layoutWidth in $Widths) {
    $control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
    $window = New-Object System.Windows.Window
    $window.Title = "Raw Buffer Visualizer Layout $layoutWidth"
    $window.Width = $layoutWidth
    $window.Height = 520
    $window.Left = 20
    $window.Top = 20
    $window.Topmost = $true
    $window.Content = $control
    $window.Show()
    Wait-Dispatcher 500
    $control.OpenPath($metadataPath)
    Wait-Dispatcher 1500
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($window)
    [RawBufferDockedLayoutNative]::ShowWindow($helper.Handle, 9) | Out-Null
    [RawBufferDockedLayoutNative]::SetWindowPos($helper.Handle, [RawBufferDockedLayoutNative]::HWND_TOPMOST, 20, 20, $layoutWidth, 520, 0x0040) | Out-Null
    [RawBufferDockedLayoutNative]::BringWindowToTop($helper.Handle) | Out-Null
    [RawBufferDockedLayoutNative]::SetForegroundWindow($helper.Handle) | Out-Null
    Wait-Dispatcher 500

    $capturePath = Join-Path $outputRoot "layout-$layoutWidth.png"
    Capture-Window $helper.Handle $capturePath

    $inspector = $control.FindName("InspectorPanel")
    $compact = $control.FindName("CompactInspectorPanel")
    $imageView = $control.FindName("OpenGlImageView")
    $viewState = $imageView.GetViewState()
    $viewAspect = $viewState.Width / $viewState.Height
    $viewportAspect = $imageView.ActualWidth / $imageView.ActualHeight
    $aspectError = [Math]::Abs($viewAspect - $viewportAspect) / $viewportAspect
    if ($aspectError -gt 0.01) {
        throw "Fit aspect mismatch at width $layoutWidth. Relative error: $aspectError"
    }

    $imageView.PinMarkerAtImagePixel(100, 100) | Out-Null
    Wait-Dispatcher 50
    $pixelText = $control.FindName("CompactPixelText")
    $neighborhoodText = $control.FindName("CompactPixelNeighborhoodText")
    $statsText = $control.FindName("CompactRoiStatsText")
    $statusText = $control.FindName("StatusText")
    $pinnedPixel = $pixelText.Text
    $pinnedNeighborhood = $neighborhoodText.Text
    $pinnedStats = $statsText.Text
    $pinnedStatus = $statusText.Text
    $hoverHandler = $control.GetType().GetMethod("OpenGlImageView_PixelHovered", [Reflection.BindingFlags]"Instance,NonPublic")
    $hoverArgs = [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(310, 220)
    $invokeArgs = [object[]]@($imageView, $hoverArgs)
    $null = $hoverHandler.Invoke($control, $invokeArgs)
    Wait-Dispatcher 50
    $pinFrozen = ($pixelText.Text -eq $pinnedPixel) -and
        ($neighborhoodText.Text -eq $pinnedNeighborhood) -and
        ($statsText.Text -eq $pinnedStats) -and
        ($statusText.Text -eq $pinnedStatus) -and
        ($control.FindName("CompactPixelHeadingText").Text -eq "Pinned")
    if (-not $pinFrozen) {
        throw "Pinned inspector changed after hover at width $layoutWidth."
    }

    $imageView.ClearPinnedMarker()
    $null = $hoverHandler.Invoke($control, $invokeArgs)
    Wait-Dispatcher 50
    $hoverResumed = ($pixelText.Text -ne $pinnedPixel) -and
        ($control.FindName("CompactPixelHeadingText").Text -eq "Current")
    if (-not $hoverResumed) {
        throw "Inspector did not resume hover updates after clearing the pin at width $layoutWidth."
    }

    $results.Add([pscustomobject]@{
        Width = $layoutWidth
        Capture = $capturePath
        InspectorVisible = $inspector.Visibility.ToString()
        CompactInspectorVisible = $compact.Visibility.ToString()
        ImageViewWidth = [Math]::Round($imageView.ActualWidth, 1)
        ImageViewHeight = [Math]::Round($imageView.ActualHeight, 1)
        RelativeAspectError = [Math]::Round($aspectError, 8)
        PinFrozenAfterHover = $pinFrozen
        HoverResumedAfterClear = $hoverResumed
    })

    $window.Close()
    Wait-Dispatcher 250
}

$resultPath = Join-Path $outputRoot "layout-widths.json"
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding UTF8
$results | Format-Table -AutoSize
Write-Host "Docked layout width smoke passed. Results: $resultPath"

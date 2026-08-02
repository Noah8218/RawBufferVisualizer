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

$outputRoot = if ([IO.Path]::IsPathRooted($OutputDir)) {
    [IO.Path]::GetFullPath($OutputDir)
}
else {
    Join-Path $repoRoot $OutputDir
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
$width = 640
$height = 480
$rawPath = Join-Path $sampleRoot "layout-mono8.raw"
$metadataPath = Join-Path $sampleRoot "layout-mono8.rbuf.json"
$missingMetadataPath = Join-Path $sampleRoot "missing-layout-mono8.rbuf.json"

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

$interopCandidates = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll"
)
$interopPath = $interopCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($interopPath)) {
    throw "Microsoft.VisualStudio.Interop.dll was not found."
}
[Reflection.Assembly]::LoadFrom($interopPath) | Out-Null

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

function Measure-NonDarkRatio([string]$path) {
    $bitmap = New-Object System.Drawing.Bitmap $path
    try {
        $sampled = 0
        $nonDark = 0
        $stepX = [Math]::Max(1, [int]($bitmap.Width / 160))
        $stepY = [Math]::Max(1, [int]($bitmap.Height / 120))
        for ($y = 0; $y -lt $bitmap.Height; $y += $stepY) {
            for ($x = 0; $x -lt $bitmap.Width; $x += $stepX) {
                $color = $bitmap.GetPixel($x, $y)
                $sampled++
                if ([Math]::Max($color.R, [Math]::Max($color.G, $color.B)) -gt 24) {
                    $nonDark++
                }
            }
        }

        return $nonDark / [double][Math]::Max(1, $sampled)
    }
    finally {
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
    if (-not $viewState.IsFitMode) {
        throw "A newly opened image did not start in sticky Fit mode at width $layoutWidth."
    }
    $initialFitMargin = [Math]::Min($viewState.Width / $width, $viewState.Height / $height)
    if ([Math]::Abs($initialFitMargin - 1.05) -gt 0.005) {
        throw "Initial Fit margin was not 5% at width $layoutWidth. Margin: $initialFitMargin"
    }

    $distortedState = [RawBufferVisualizer.OpenGlCanvas.RawOpenGlViewState]::new(
        $width,
        $height,
        10.0,
        20.0,
        400.0,
        400.0)
    if (-not $imageView.TryApplyViewState($distortedState)) {
        throw "A matching manual view state could not be restored at width $layoutWidth."
    }
    Wait-Dispatcher 100
    $normalizedState = $imageView.GetViewState()
    $normalizedAspect = $normalizedState.Width / $normalizedState.Height
    $normalizedViewportAspect = $imageView.ActualWidth / $imageView.ActualHeight
    $normalizedAspectError = [Math]::Abs($normalizedAspect - $normalizedViewportAspect) / $normalizedViewportAspect
    if ($normalizedState.IsFitMode -or $normalizedAspectError -gt 0.01) {
        throw "A restored manual view was not normalized to the current viewport at width $layoutWidth. Relative error: $normalizedAspectError"
    }

    $imageView.FitToImage()
    $imageView.SetZoomScale(1.0)
    if ($imageView.GetViewState().IsFitMode) {
        throw "The 1:1 command did not switch the viewer to Manual mode at width $layoutWidth."
    }
    $imageView.FitToImage()
    $imageView.ZoomAtScreenPoint(
        [System.Windows.Point]::new($imageView.ActualWidth / 2, $imageView.ActualHeight / 2),
        120)
    if ($imageView.GetViewState().IsFitMode) {
        throw "Wheel zoom did not switch the viewer to Manual mode at width $layoutWidth."
    }
    $imageView.FitToImage()
    $imageView.PanByImagePixels(1.0, 1.0)
    if ($imageView.GetViewState().IsFitMode) {
        throw "Pan did not switch the viewer to Manual mode at width $layoutWidth."
    }

    $imageView.FitToImage()
    [RawBufferDockedLayoutNative]::SetWindowPos(
        $helper.Handle,
        [RawBufferDockedLayoutNative]::HWND_TOPMOST,
        20,
        20,
        $layoutWidth,
        620,
        0x0040) | Out-Null
    Wait-Dispatcher 500
    $resizedFitState = $imageView.GetViewState()
    $resizedFitAspect = $resizedFitState.Width / $resizedFitState.Height
    $resizedViewportAspect = $imageView.ActualWidth / $imageView.ActualHeight
    $resizedFitAspectError = [Math]::Abs($resizedFitAspect - $resizedViewportAspect) / $resizedViewportAspect
    $resizedFitMargin = [Math]::Min($resizedFitState.Width / $width, $resizedFitState.Height / $height)
    if (-not $resizedFitState.IsFitMode -or $resizedFitAspectError -gt 0.01) {
        throw "Fit mode did not remain aspect-correct after resize at width $layoutWidth. Relative error: $resizedFitAspectError"
    }
    if ([Math]::Abs($resizedFitMargin - 1.05) -gt 0.005) {
        throw "Fit mode did not retain its 5% margin after resize at width $layoutWidth. Margin: $resizedFitMargin"
    }

    $imageView.SetZoomScale(2.0)
    $imageView.PanByImagePixels(31.0, -17.0)
    Wait-Dispatcher 100
    $manualBeforeResize = $imageView.GetViewState()
    $manualZoomBeforeResize = $imageView.ZoomScale
    $manualCenterXBeforeResize = $manualBeforeResize.Left + ($manualBeforeResize.Width / 2)
    $manualCenterYBeforeResize = $manualBeforeResize.Top + ($manualBeforeResize.Height / 2)
    [RawBufferDockedLayoutNative]::SetWindowPos(
        $helper.Handle,
        [RawBufferDockedLayoutNative]::HWND_TOPMOST,
        20,
        20,
        $layoutWidth,
        560,
        0x0040) | Out-Null
    Wait-Dispatcher 500
    $manualAfterResize = $imageView.GetViewState()
    $manualZoomAfterResize = $imageView.ZoomScale
    $manualCenterXAfterResize = $manualAfterResize.Left + ($manualAfterResize.Width / 2)
    $manualCenterYAfterResize = $manualAfterResize.Top + ($manualAfterResize.Height / 2)
    $manualAspect = $manualAfterResize.Width / $manualAfterResize.Height
    $manualViewportAspect = $imageView.ActualWidth / $imageView.ActualHeight
    $manualAspectError = [Math]::Abs($manualAspect - $manualViewportAspect) / $manualViewportAspect
    if ($manualAfterResize.IsFitMode) {
        throw "Manual zoom/pan unexpectedly returned to Fit mode after resize at width $layoutWidth."
    }
    if ([Math]::Abs($manualZoomAfterResize - $manualZoomBeforeResize) -gt 0.01) {
        throw "Manual zoom changed after resize at width $layoutWidth. Before: $manualZoomBeforeResize After: $manualZoomAfterResize"
    }
    if ([Math]::Abs($manualCenterXAfterResize - $manualCenterXBeforeResize) -gt 0.001 -or
        [Math]::Abs($manualCenterYAfterResize - $manualCenterYBeforeResize) -gt 0.001) {
        throw "Manual center changed after resize at width $layoutWidth."
    }
    if ($manualAspectError -gt 0.01) {
        throw "Manual view aspect mismatch after resize at width $layoutWidth. Relative error: $manualAspectError"
    }

    $imageView.FitToImage()
    [RawBufferDockedLayoutNative]::SetWindowPos(
        $helper.Handle,
        [RawBufferDockedLayoutNative]::HWND_TOPMOST,
        20,
        20,
        $layoutWidth,
        520,
        0x0040) | Out-Null
    Wait-Dispatcher 500

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
    $raiseHover = $imageView.GetType().GetMethod("RaisePixelHovered", [Reflection.BindingFlags]"Instance,NonPublic")
    $pinnedHoverPoint = [System.Windows.Point]::new($imageView.ActualWidth * 0.55, $imageView.ActualHeight * 0.5)
    $null = $raiseHover.Invoke($imageView, [object[]]@($pinnedHoverPoint, $false))
    Wait-Dispatcher 100
    $pinFrozen = ($pixelText.Text -eq $pinnedPixel) -and
        ($neighborhoodText.Text -eq $pinnedNeighborhood) -and
        ($statsText.Text -eq $pinnedStats) -and
        ($statusText.Text -eq $pinnedStatus) -and
        ($control.FindName("CompactPixelHeadingText").Text -eq "Pinned")
    if (-not $pinFrozen) {
        throw "Pinned inspector changed after hover at width $layoutWidth."
    }

    $imageView.ClearPinnedMarker()
    $resumedHoverPoint = [System.Windows.Point]::new($imageView.ActualWidth * 0.65, $imageView.ActualHeight * 0.45)
    $null = $raiseHover.Invoke($imageView, [object[]]@($resumedHoverPoint, $false))
    Wait-Dispatcher 100
    $hoverResumed = ($pixelText.Text -ne $pinnedPixel) -and
        ($control.FindName("CompactPixelHeadingText").Text -eq "Current")
    if (-not $hoverResumed) {
        throw "Inspector did not resume hover updates after clearing the pin at width $layoutWidth."
    }

    $imageView.ResetRenderStats()
    $hoverPoints = @(
        [System.Windows.Point]::new($imageView.ActualWidth * 0.35, $imageView.ActualHeight * 0.35),
        [System.Windows.Point]::new($imageView.ActualWidth * 0.55, $imageView.ActualHeight * 0.50),
        [System.Windows.Point]::new($imageView.ActualWidth * 0.72, $imageView.ActualHeight * 0.38)
    )
    $hoverReadouts = foreach ($screenPoint in $hoverPoints) {
        $null = $raiseHover.Invoke($imageView, [object[]]@($screenPoint, $false))
        Wait-Dispatcher 100
        $pixelText.Text
    }
    $hoverStats = $imageView.GetRenderStatsSnapshot()
    $hoverFrameCount = $hoverStats.FrameCount
    $hoverReadoutCount = @($hoverReadouts | Select-Object -Unique).Count
    $hoverCapturePath = Join-Path $outputRoot "hover-$layoutWidth.png"
    Capture-Window $helper.Handle $hoverCapturePath
    if ($hoverFrameCount -lt $hoverPoints.Count) {
        throw "Hover marker did not render each settled pointer position at width $layoutWidth. Frames: $hoverFrameCount"
    }
    if ($hoverReadoutCount -ne $hoverPoints.Count) {
        throw "Hover inspector did not follow each pointer position at width $layoutWidth. Readouts: $hoverReadoutCount"
    }

    $hoverFramebufferPath = Join-Path $outputRoot "hover-framebuffer-$layoutWidth.png"
    $imageView.SaveFramebufferPng($hoverFramebufferPath)
    $hoverNonDarkRatio = Measure-NonDarkRatio $hoverFramebufferPath
    if ($hoverNonDarkRatio -lt 0.05) {
        throw "Hover redraw produced a blank framebuffer at width $layoutWidth. Non-dark ratio: $hoverNonDarkRatio"
    }

    $control.OpenPath($missingMetadataPath)
    Wait-Dispatcher 250
    $errorPanel = $control.FindName("ErrorPanel")
    $errorIdText = $control.FindName("ErrorIdText")
    $errorPanelVisible = $errorPanel.Visibility -eq [System.Windows.Visibility]::Visible
    $imageViewHiddenForError = $imageView.Visibility -eq [System.Windows.Visibility]::Collapsed
    $errorId = $errorIdText.Text
    $createReport = $control.GetType().GetMethod(
        "CreateActiveSupportReport",
        [Reflection.BindingFlags]"Instance,NonPublic")
    $errorReport = [string]$createReport.Invoke($control, @())
    $errorReportValid = $errorReport.Contains("Raw Buffer Visualizer Support Report") -and
        $errorReport.Contains("Error ID: $errorId") -and
        $errorReport.Contains("Error type: System.IO.FileNotFoundException") -and
        $errorReport.Contains("Image payload included: No") -and
        $errorReport.Contains("Package log:")
    if (-not $errorPanelVisible) {
        throw "Error overlay was not visible at width $layoutWidth."
    }
    if (-not $imageViewHiddenForError) {
        throw "Image host was not hidden behind the error overlay at width $layoutWidth."
    }
    if (-not $errorId.StartsWith("RBV-ERROR-", [System.StringComparison]::Ordinal)) {
        throw "Error overlay did not expose a support ID at width $layoutWidth. ID: $errorId"
    }
    if (-not $errorReportValid) {
        throw "Support report was missing required diagnostic fields at width $layoutWidth."
    }

    $copyReportButton = $control.FindName("CopyErrorReportButton")
    $copyReportButton.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
    Wait-Dispatcher 50
    $clipboardReportValid = [System.Windows.Clipboard]::ContainsText() -and
        ([System.Windows.Clipboard]::GetText()).Contains("Error ID: $errorId")
    if (-not $clipboardReportValid) {
        throw "Copy Report did not place the active error report on the clipboard at width $layoutWidth."
    }

    $writeReport = $control.GetType().GetMethod(
        "WriteActiveSupportReportFile",
        [Reflection.BindingFlags]"Instance,NonPublic")
    $supportReportPath = [string]$writeReport.Invoke($control, @())
    $supportReportFileValid = (Test-Path -LiteralPath $supportReportPath) -and
        (Get-Content -LiteralPath $supportReportPath -Raw).Contains("Error ID: $errorId")
    if (-not $supportReportFileValid) {
        throw "Open Logs report file was not generated correctly at width $layoutWidth."
    }

    $errorCapturePath = Join-Path $outputRoot "error-$layoutWidth.png"
    Capture-Window $helper.Handle $errorCapturePath

    $imageList = $control.FindName("ImageList")
    $imageList.SelectedIndex = 0
    Wait-Dispatcher 250
    $recoveredFromError = $errorPanel.Visibility -eq [System.Windows.Visibility]::Collapsed -and
        $imageView.Visibility -eq [System.Windows.Visibility]::Visible
    $recoveryFramebufferPath = Join-Path $outputRoot "recovered-framebuffer-$layoutWidth.png"
    $imageView.SaveFramebufferPng($recoveryFramebufferPath)
    $recoveryNonDarkRatio = Measure-NonDarkRatio $recoveryFramebufferPath
    if (-not $recoveredFromError) {
        throw "Normal image did not recover after selecting it from an error row at width $layoutWidth."
    }
    if ($recoveryNonDarkRatio -lt 0.05) {
        throw "Recovered image framebuffer was blank at width $layoutWidth. Non-dark ratio: $recoveryNonDarkRatio"
    }

    $recoveryCapturePath = Join-Path $outputRoot "recovered-$layoutWidth.png"
    Capture-Window $helper.Handle $recoveryCapturePath

    $malformedHandoffPath = Join-Path $sampleRoot "malformed-$layoutWidth.rbuf-handoff"
    [IO.File]::WriteAllText($malformedHandoffPath, "{")
    $control.OpenHandoffRequest($malformedHandoffPath)
    Wait-Dispatcher 250
    $malformedHandoffVisible = $errorPanel.Visibility -eq [System.Windows.Visibility]::Visible -and
        $imageView.Visibility -eq [System.Windows.Visibility]::Collapsed -and
        -not (Test-Path -LiteralPath $malformedHandoffPath)
    if (-not $malformedHandoffVisible) {
        throw "Malformed handoff did not become a visible error row at width $layoutWidth."
    }

    $imageList.SelectedIndex = 0
    Wait-Dispatcher 250
    $malformedHandoffRecovered = $errorPanel.Visibility -eq [System.Windows.Visibility]::Collapsed -and
        $imageView.Visibility -eq [System.Windows.Visibility]::Visible
    if (-not $malformedHandoffRecovered) {
        throw "Normal image did not recover after a malformed handoff at width $layoutWidth."
    }

    $results.Add([pscustomobject]@{
        Width = $layoutWidth
        Capture = $capturePath
        InspectorVisible = $inspector.Visibility.ToString()
        CompactInspectorVisible = $compact.Visibility.ToString()
        ImageViewWidth = [Math]::Round($imageView.ActualWidth, 1)
        ImageViewHeight = [Math]::Round($imageView.ActualHeight, 1)
        RelativeAspectError = [Math]::Round($aspectError, 8)
        InitialFitMargin = [Math]::Round($initialFitMargin, 6)
        RestoredManualAspectError = [Math]::Round($normalizedAspectError, 8)
        ResizedFitAspectError = [Math]::Round($resizedFitAspectError, 8)
        ResizedFitMargin = [Math]::Round($resizedFitMargin, 6)
        ManualResizeAspectError = [Math]::Round($manualAspectError, 8)
        ManualResizeZoomDelta = [Math]::Round([Math]::Abs($manualZoomAfterResize - $manualZoomBeforeResize), 8)
        ManualResizeCenterDelta = [Math]::Round(
            [Math]::Max(
                [Math]::Abs($manualCenterXAfterResize - $manualCenterXBeforeResize),
                [Math]::Abs($manualCenterYAfterResize - $manualCenterYBeforeResize)),
            8)
        PinFrozenAfterHover = $pinFrozen
        HoverResumedAfterClear = $hoverResumed
        HoverRenderFrameCount = $hoverFrameCount
        HoverDistinctReadoutCount = $hoverReadoutCount
        HoverTextureUploadCount = $hoverStats.TextureUploadCount
        HoverNonDarkRatio = [Math]::Round($hoverNonDarkRatio, 6)
        HoverCapture = $hoverCapturePath
        HoverFramebuffer = $hoverFramebufferPath
        ErrorPanelVisible = $errorPanelVisible
        ImageViewHiddenForError = $imageViewHiddenForError
        ErrorId = $errorId
        ErrorReportValid = $errorReportValid
        ClipboardReportValid = $clipboardReportValid
        SupportReportPath = $supportReportPath
        SupportReportFileValid = $supportReportFileValid
        ErrorCapture = $errorCapturePath
        RecoveredFromError = $recoveredFromError
        RecoveryNonDarkRatio = [Math]::Round($recoveryNonDarkRatio, 6)
        RecoveryCapture = $recoveryCapturePath
        RecoveryFramebuffer = $recoveryFramebufferPath
        MalformedHandoffVisible = $malformedHandoffVisible
        MalformedHandoffRecovered = $malformedHandoffRecovered
    })

    $window.Close()
    Wait-Dispatcher 250
}

$resultPath = Join-Path $outputRoot "layout-widths.json"
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding UTF8
$results | Format-Table -AutoSize
Write-Host "Docked layout width smoke passed. Results: $resultPath"

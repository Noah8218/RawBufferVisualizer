[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\preview-first-handoff",
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
        "-OutputDir",
        $OutputDir
    )
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

$outputRoot = if ([IO.Path]::IsPathRooted($OutputDir)) {
    [IO.Path]::GetFullPath($OutputDir)
}
else {
    Join-Path $repoRoot $OutputDir
}
$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null

function Write-Metadata(
    [string]$metadataPath,
    [string]$rawFile,
    [int]$width,
    [int]$height,
    [int]$stride,
    [string]$pixelFormat) {
    @{
        rawFile = $rawFile
        width = $width
        height = $height
        stride = $stride
        pixelFormat = $pixelFormat
        validBits = 8
        byteOrder = "LittleEndian"
    } | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8
}

function Wait-Dispatcher([int]$milliseconds) {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({
        $timer.Stop()
        $frame.Continue = $false
    })
    $timer.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

function Capture-Window([IntPtr]$handle, [string]$path) {
    $rect = New-Object PreviewFirstHandoffNative+RECT
    [PreviewFirstHandoffNative]::GetWindowRect($handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$previewWidth = 320
$previewHeight = 180
$previewStride = $previewWidth * 4
$previewRawPath = Join-Path $sampleRoot "preview.bgra32.raw"
$previewMetadataPath = Join-Path $sampleRoot "preview.rbuf.json"
$previewBuffer = New-Object byte[] ($previewStride * $previewHeight)
for ($y = 0; $y -lt $previewHeight; $y++) {
    for ($x = 0; $x -lt $previewWidth; $x++) {
        $offset = ($y * $previewStride) + ($x * 4)
        $value = [byte](32 + (($x + $y) % 224))
        $previewBuffer[$offset] = $value
        $previewBuffer[$offset + 1] = $value
        $previewBuffer[$offset + 2] = $value
        $previewBuffer[$offset + 3] = 255
    }
}
[IO.File]::WriteAllBytes($previewRawPath, $previewBuffer)
Write-Metadata $previewMetadataPath (Split-Path -Leaf $previewRawPath) $previewWidth $previewHeight $previewStride "BGRA32"

$fullWidth = 640
$fullHeight = 360
$fullRawPath = Join-Path $sampleRoot "full.mono8.raw"
$fullMetadataPath = Join-Path $sampleRoot "full.rbuf.json"
$fullBuffer = New-Object byte[] ($fullWidth * $fullHeight)
for ($y = 0; $y -lt $fullHeight; $y++) {
    for ($x = 0; $x -lt $fullWidth; $x++) {
        $fullBuffer[($y * $fullWidth) + $x] = [byte](16 + (($x * 3 + $y) % 240))
    }
}
[IO.File]::WriteAllBytes($fullRawPath, $fullBuffer)
Write-Metadata $fullMetadataPath (Split-Path -Leaf $fullRawPath) $fullWidth $fullHeight $fullWidth "Mono8"

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$screens = @([System.Windows.Forms.Screen]::AllScreens)
$testScreen = if ($screens.Count -eq 2) {
    $screens |
        Sort-Object @{ Expression = { $_.Bounds.Width * $_.Bounds.Height } }, @{ Expression = { $_.Bounds.Left } } |
        Select-Object -First 1
}
else {
    [System.Windows.Forms.Screen]::PrimaryScreen
}
$testX = $testScreen.WorkingArea.Left + 20
$testY = $testScreen.WorkingArea.Top + 20
$testWidth = [Math]::Min(1100, $testScreen.WorkingArea.Width - 40)
$testHeight = [Math]::Min(720, $testScreen.WorkingArea.Height - 40)

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

if (-not ("PreviewFirstHandoffNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PreviewFirstHandoffNative {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
}
'@
}

function Open-ClaimedRequestExpectRejection(
    $control,
    [string]$requestPath) {
    $processingPath = ""
    $reason = ""
    try {
        if (-not [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::TryClaimRequest(
            $requestPath,
            [ref]$processingPath)) {
            throw "Expected-rejection handoff could not be claimed."
        }

        if ($control.OpenClaimedHandoffRequest($requestPath, $processingPath)) {
            throw "Unreadable live-memory handoff was incorrectly acknowledged."
        }

        $state = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::GetRequestState(
            $requestPath)
        if ($state -ne [RawBufferVisualizer.VisualStudio.VisualizerHandoffRequestState]::Rejected) {
            throw "Unreadable live-memory handoff did not publish a NACK: $state"
        }

        if (-not [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::TryReadRejectionReason(
            $requestPath,
            [ref]$reason)) {
            throw "Unreadable live-memory handoff NACK had no reason."
        }

        return $reason
    }
    finally {
        [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::CleanupRequestArtifacts(
            $requestPath)
    }
}

function Prepare-WindowCapture([System.Windows.Window]$targetWindow) {
    $targetWindow.UpdateLayout()
    $targetWindow.Activate() | Out-Null
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($targetWindow)
    [PreviewFirstHandoffNative]::ShowWindow($helper.Handle, 5) | Out-Null
    [PreviewFirstHandoffNative]::SetWindowPos(
        $helper.Handle,
        [PreviewFirstHandoffNative]::HWND_TOPMOST,
        $testX,
        $testY,
        $testWidth,
        $testHeight,
        0x0040) | Out-Null
    [PreviewFirstHandoffNative]::BringWindowToTop($helper.Handle) | Out-Null
    [PreviewFirstHandoffNative]::SetForegroundWindow($helper.Handle) | Out-Null
    Wait-Dispatcher 500
    $actualWindowRect = New-Object PreviewFirstHandoffNative+RECT
    [PreviewFirstHandoffNative]::GetWindowRect($helper.Handle, [ref]$actualWindowRect) | Out-Null
    $intersectionWidth = [Math]::Max(0, [Math]::Min($actualWindowRect.Right, $testScreen.Bounds.Right) - [Math]::Max($actualWindowRect.Left, $testScreen.Bounds.Left))
    $intersectionHeight = [Math]::Max(0, [Math]::Min($actualWindowRect.Bottom, $testScreen.Bounds.Bottom) - [Math]::Max($actualWindowRect.Top, $testScreen.Bounds.Top))
    if ($intersectionWidth -le 0 -or $intersectionHeight -le 0) {
        throw "Test window did not intersect the selected monitor $($testScreen.DeviceName)."
    }

    return $helper
}

$assemblyPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Vssdk.dll"
foreach ($dependencyName in @("Microsoft.VisualStudio.Imaging.dll", "Microsoft.VisualStudio.ImageCatalog.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path (Split-Path -Parent $assemblyPath) $dependencyName)) | Out-Null
}
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null

$control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
$window = New-Object System.Windows.Window
$window.Title = "Raw Buffer Visualizer Preview-First Smoke"
$window.Width = 1100
$window.Height = 720
$window.Left = 30
$window.Top = 30
$window.Topmost = $true
$window.Content = $control
$window.Show()

try {
    Wait-Dispatcher 500
    $handoffId = "preview-first-smoke"
    $previewRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteSnapshotRequest(
        $PID,
        $previewMetadataPath,
        "cameraFrame",
        "OpenCvSharp.Mat (sampled preview of 640x360 Mono8)",
        $handoffId,
        $true)
    $control.OpenHandoffRequest($previewRequest)
    Wait-Dispatcher 1200

    $imageList = $control.FindName("ImageList")
    $statusText = $control.FindName("StatusText")
    if ($imageList.Items.Count -ne 1) {
        throw "Preview handoff created $($imageList.Items.Count) items instead of one."
    }
    if (-not $statusText.Text.StartsWith("Preview ", [StringComparison]::Ordinal)) {
        throw "Preview status was not visible: $($statusText.Text)"
    }
    $previewSummary = [string]$imageList.Items[0].Summary
    if (-not $previewSummary.StartsWith("Preview  ", [StringComparison]::Ordinal)) {
        throw "Preview list summary was not visible: $previewSummary"
    }

    $helper = Prepare-WindowCapture $window
    $previewCapture = Join-Path $outputRoot "preview-stage.png"
    Capture-Window $helper.Handle $previewCapture

    $fullRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteSnapshotRequest(
        $PID,
        $fullMetadataPath,
        "cameraFrame",
        "OpenCvSharp.Mat",
        $handoffId,
        $false)
    $control.OpenHandoffRequest($fullRequest)
    Wait-Dispatcher 1500

    if ($imageList.Items.Count -ne 1) {
        throw "Full handoff left duplicate image items: $($imageList.Items.Count)."
    }
    if ($statusText.Text.StartsWith("Preview ", [StringComparison]::Ordinal)) {
        throw "Preview status remained after full handoff: $($statusText.Text)"
    }
    if (-not $statusText.Text.Contains("640x360 Mono8")) {
        throw "Full-resolution status was not applied: $($statusText.Text)"
    }
    $fullSummary = [string]$imageList.Items[0].Summary
    if ($fullSummary.StartsWith("Preview  ", [StringComparison]::Ordinal)) {
        throw "Preview summary remained after full handoff: $fullSummary"
    }
    $fullStatus = $statusText.Text

    $helper = Prepare-WindowCapture $window
    $fullCapture = Join-Path $outputRoot "full-stage.png"
    Capture-Window $helper.Handle $fullCapture

    $missingMetadataPath = Join-Path $sampleRoot "missing-full.rbuf.json"
    $failedReplacementRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteSnapshotRequest(
        $PID,
        $missingMetadataPath,
        "cameraFrame",
        "OpenCvSharp.Mat",
        $handoffId,
        $false)
    $failedReplacementProcessingPath = ""
    $failedReplacementReason = ""
    try {
        if (-not [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::TryClaimRequest(
            $failedReplacementRequest,
            [ref]$failedReplacementProcessingPath)) {
            throw "Failed replacement handoff could not be claimed."
        }

        if ($control.OpenClaimedHandoffRequest(
            $failedReplacementRequest,
            $failedReplacementProcessingPath)) {
            throw "Failed full-resolution replacement was incorrectly acknowledged."
        }

        $failedReplacementState =
            [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::GetRequestState(
                $failedReplacementRequest)
        if ($failedReplacementState -ne
            [RawBufferVisualizer.VisualStudio.VisualizerHandoffRequestState]::Rejected) {
            throw "Failed full-resolution replacement did not publish a NACK: $failedReplacementState"
        }

        if (-not [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::TryReadRejectionReason(
            $failedReplacementRequest,
            [ref]$failedReplacementReason)) {
            throw "Failed full-resolution replacement NACK had no reason."
        }
    }
    finally {
        [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::CleanupRequestArtifacts(
            $failedReplacementRequest)
    }

    $unreadableAddress = [IntPtr]1

    $unreadableDescriptor = New-Object RawBufferVisualizer.Core.RawImageDescriptor
    $unreadableDescriptor.Width = 1
    $unreadableDescriptor.Height = 1
    $unreadableDescriptor.Stride = 1
    $unreadableDescriptor.PixelFormat = [RawBufferVisualizer.Core.RawPixelFormat]::Mono8
    $unreadableDescriptor.ValidBits = 8
    $unreadableDescriptor.ByteOrder = [RawBufferVisualizer.Core.RawByteOrder]::LittleEndian

    $initialUnreadableHandoffId = "initial-unreadable-live-smoke"
    $initialUnreadableRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteLiveMemoryRequest(
        $PID,
        $PID,
        $unreadableAddress.ToInt64(),
        1,
        $unreadableDescriptor,
        "releasedBuffer",
        "System.IntPtr",
        $initialUnreadableHandoffId)
    $itemCountBeforeInitialUnreadable = $imageList.Items.Count
    $initialUnreadableReason = Open-ClaimedRequestExpectRejection $control $initialUnreadableRequest
    Wait-Dispatcher 250
    $errorPanel = $control.FindName("ErrorPanel")
    $initialUnreadableLiveRejected =
        $null -eq @($imageList.Items | Where-Object {
            $_.HandoffId -eq $initialUnreadableHandoffId
        })[0] -and
        $imageList.Items.Count -eq ($itemCountBeforeInitialUnreadable + 1) -and
        $imageList.Items[$imageList.Items.Count - 1].IsError -and
        $errorPanel.Visibility -eq [System.Windows.Visibility]::Visible
    if (-not $initialUnreadableLiveRejected) {
        throw "An unreadable initial live-memory handoff left a LIVE document behind."
    }
    $unreadableLiveCapture = Join-Path $outputRoot "unreadable-live-rejected.png"
    Capture-Window $helper.Handle $unreadableLiveCapture

    $failedLiveHandoffId = "preview-unreadable-live-smoke"
    $failedLivePreviewRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteSnapshotRequest(
        $PID,
        $previewMetadataPath,
        "cameraFrame",
        "System.IntPtr preview",
        $failedLiveHandoffId,
        $true,
        $PID,
        $unreadableAddress.ToInt64())
    $control.OpenHandoffRequest($failedLivePreviewRequest)
    Wait-Dispatcher 500
    $previewBeforeFailedLive = @($imageList.Items | Where-Object {
        $_.HandoffId -eq $failedLiveHandoffId
    })[0]
    if ($null -eq $previewBeforeFailedLive -or -not $previewBeforeFailedLive.IsPreview) {
        throw "The preview for the failed live-memory replacement was not created."
    }
    $previewPixelBeforeFailedLive = $previewBeforeFailedLive.Source.DescribePixel(0, 0)

    $failedLiveRequest = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteLiveMemoryRequest(
        $PID,
        $PID,
        $unreadableAddress.ToInt64(),
        1,
        $unreadableDescriptor,
        "cameraFrame",
        "System.IntPtr",
        $failedLiveHandoffId)
    $failedLiveReason = Open-ClaimedRequestExpectRejection $control $failedLiveRequest
    $previewAfterFailedLive = @($imageList.Items | Where-Object {
        $_.HandoffId -eq $failedLiveHandoffId
    })[0]
    $previewPreservedAfterUnreadableLive =
        [Object]::ReferenceEquals($previewBeforeFailedLive, $previewAfterFailedLive) -and
        $previewAfterFailedLive.IsPreview -and
        $previewAfterFailedLive.SourceAddressState -eq "PREVIEW" -and
        $previewAfterFailedLive.Source.DescribePixel(0, 0) -eq $previewPixelBeforeFailedLive
    if (-not $previewPreservedAfterUnreadableLive) {
        throw "An unreadable live-memory replacement discarded or changed its safe preview."
    }

    $finalWindowRect = New-Object PreviewFirstHandoffNative+RECT
    [PreviewFirstHandoffNative]::GetWindowRect($helper.Handle, [ref]$finalWindowRect) | Out-Null
    $result = [ordered]@{
        monitor = $testScreen.DeviceName
        monitorBounds = "$($testScreen.Bounds.Left),$($testScreen.Bounds.Top),$($testScreen.Bounds.Width),$($testScreen.Bounds.Height)"
        windowRect = "$($finalWindowRect.Left),$($finalWindowRect.Top),$($finalWindowRect.Right - $finalWindowRect.Left),$($finalWindowRect.Bottom - $finalWindowRect.Top)"
        dpi = [PreviewFirstHandoffNative]::GetDpiForWindow($helper.Handle)
        previewItemCount = 1
        fullItemCount = 1
        failedReplacementRejected = $true
        failedReplacementReason = $failedReplacementReason
        initialUnreadableLiveRejected = $initialUnreadableLiveRejected
        initialUnreadableLiveReason = $initialUnreadableReason
        unreadableLiveCapture = $unreadableLiveCapture
        previewPreservedAfterUnreadableLive = $previewPreservedAfterUnreadableLive
        failedLiveReason = $failedLiveReason
        previewStatus = "Preview 320x180 BGRA32"
        fullStatus = $fullStatus
        previewCapture = $previewCapture
        fullCapture = $fullCapture
    }
    $resultPath = Join-Path $outputRoot "preview-first-handoff.json"
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    $result | ConvertTo-Json -Depth 5 | Write-Output
}
finally {
    $window.Close()
    Wait-Dispatcher 200
}

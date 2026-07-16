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

$outputRoot = Join-Path $repoRoot $OutputDir
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
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
}
'@
}

function Prepare-WindowCapture([System.Windows.Window]$targetWindow) {
    $targetWindow.UpdateLayout()
    $targetWindow.Activate() | Out-Null
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($targetWindow)
    [PreviewFirstHandoffNative]::ShowWindow($helper.Handle, 5) | Out-Null
    [PreviewFirstHandoffNative]::SetWindowPos(
        $helper.Handle,
        [PreviewFirstHandoffNative]::HWND_TOPMOST,
        30,
        30,
        1100,
        720,
        0x0040) | Out-Null
    [PreviewFirstHandoffNative]::BringWindowToTop($helper.Handle) | Out-Null
    [PreviewFirstHandoffNative]::SetForegroundWindow($helper.Handle) | Out-Null
    Wait-Dispatcher 500
    return $helper
}

$assemblyPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Vssdk.dll"
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

    $helper = Prepare-WindowCapture $window
    $fullCapture = Join-Path $outputRoot "full-stage.png"
    Capture-Window $helper.Handle $fullCapture

    $result = [ordered]@{
        previewItemCount = 1
        fullItemCount = 1
        previewStatus = "Preview 320x180 BGRA32"
        fullStatus = $statusText.Text
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

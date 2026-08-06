param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\buffer-doctor",
    [string]$CaptureName = "after-diagnose-1160.png",
    [string]$ApplyCaptureName = "after-apply-1160.png",
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
$width = 2448
$height = 2048
$stride = 2560
$rawPath = Join-Path $sampleRoot "doctor-padded-mono8.raw"
$metadataPath = Join-Path $sampleRoot "doctor-padded-mono8.rbuf.json"

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

if (-not ("BufferDoctorSample" -as [type])) {
    Add-Type @'
public static class BufferDoctorSample {
    public static byte[] Create(int width, int height, int stride) {
        var buffer = new byte[(stride * (height - 1)) + width];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                buffer[(y * stride) + x] = (byte)((x + (2 * y)) % 256);
            }
        }

        return buffer;
    }
}
'@
}

[IO.File]::WriteAllBytes($rawPath, [BufferDoctorSample]::Create($width, $height, $stride))

# The stored descriptor is deliberately wrong (stride = width, no padding) so the
# diagnosis panel has something to recover: the true layout is stride 2560.
@{
    rawFile = "doctor-padded-mono8.raw"
    width = $width
    height = $height
    stride = $width
    pixelFormat = "Mono8"
    validBits = 8
    byteOrder = "LittleEndian"
} | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8

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

$control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
$window = New-Object System.Windows.Window
$window.Title = "Raw Buffer Visualizer Buffer Doctor"
$window.Width = 1160
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
[RawBufferDockedLayoutNative]::SetWindowPos($helper.Handle, [RawBufferDockedLayoutNative]::HWND_TOPMOST, 20, 20, 1160, 520, 0x0040) | Out-Null
[RawBufferDockedLayoutNative]::BringWindowToTop($helper.Handle) | Out-Null
[RawBufferDockedLayoutNative]::SetForegroundWindow($helper.Handle) | Out-Null
Wait-Dispatcher 300

$diagnoseButton = $control.FindName("DiagnoseBufferButton")
if ($null -eq $diagnoseButton) {
    throw "Diagnose Buffer button was not found in the Interpret section."
}

$diagnoseButton.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))

$panel = $control.FindName("DiagnosisPanel")
$candidateList = $control.FindName("DiagnosisCandidateList")
$deadline = [DateTime]::UtcNow.AddSeconds(30)
while (($candidateList.Items.Count -eq 0) -and ([DateTime]::UtcNow -lt $deadline)) {
    Wait-Dispatcher 250
}

if ($panel.Visibility -ne [System.Windows.Visibility]::Visible) {
    throw "Diagnosis panel did not open after clicking Diagnose Buffer."
}

if ($candidateList.Items.Count -eq 0) {
    throw "Diagnosis produced no candidate rows."
}

$topCandidate = $candidateList.Items[0].Candidate.Descriptor
if ($topCandidate.Width -ne $width -or $topCandidate.Height -ne $height -or $topCandidate.Stride -ne $stride -or $topCandidate.PixelFormat.ToString() -ne "Mono8") {
    throw "Top candidate was not the true padded descriptor. Got: $($topCandidate.PixelFormat) $($topCandidate.Width)x$($topCandidate.Height) stride $($topCandidate.Stride)"
}

$panel.BringIntoView()
Wait-Dispatcher 300

$diagnoseCapturePath = Join-Path $outputRoot $CaptureName
Capture-Window $helper.Handle $diagnoseCapturePath

$candidateList.SelectedIndex = 0
Wait-Dispatcher 800

$appliedStride = $control.FindName("InterpretStrideTextBox").Text
if ($appliedStride -ne "$stride") {
    throw "Applying the top candidate did not sync the Interpret stride box. Got: $appliedStride"
}

$panel.BringIntoView()
Wait-Dispatcher 200

$applyCapturePath = Join-Path $outputRoot $ApplyCaptureName
Capture-Window $helper.Handle $applyCapturePath

$statusText = $control.FindName("DiagnosisStatusText").Text
$window.Close()
Wait-Dispatcher 250

[pscustomobject]@{
    CandidateCount = $candidateList.Items.Count
    TopCandidate = "$($topCandidate.PixelFormat) $($topCandidate.Width)x$($topCandidate.Height) stride $($topCandidate.Stride)"
    AppliedStride = $appliedStride
    Status = $statusText
    Capture = $diagnoseCapturePath
    ApplyCapture = $applyCapturePath
} | Format-List
Write-Host "Buffer Doctor panel smoke passed. Captures: $diagnoseCapturePath, $applyCapturePath"

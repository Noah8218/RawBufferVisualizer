param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\automatic-vision-inspector\layout-smoke",
    [int]$WindowWidth = 1160,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (-not $NoBuild) {
    dotnet build .\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj --configuration $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

$outputRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$rawPath = Join-Path $outputRoot "automatic-inspector-mono8.raw"
$metadataPath = Join-Path $outputRoot "automatic-inspector-mono8.rbuf.json"
$capturePath = Join-Path $outputRoot "automatic-inspector-$WindowWidth.png"
$resultPath = Join-Path $outputRoot "automatic-inspector-layout.json"

$width = 320
$height = 240
$buffer = New-Object byte[] ($width * $height)
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $buffer[($y * $width) + $x] = [byte](32 + (($x + $y) % 224))
    }
}

[IO.File]::WriteAllBytes($rawPath, $buffer)
@{
    rawFile = [IO.Path]::GetFileName($rawPath)
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

if (-not ("AutomaticVisionInspectorLayoutNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class AutomaticVisionInspectorLayoutNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
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
    $rect = New-Object AutomaticVisionInspectorLayoutNative+RECT
    [AutomaticVisionInspectorLayoutNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $captureWidth = $rect.Right - $rect.Left
    $captureHeight = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $captureWidth, $captureHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            $printed = [AutomaticVisionInspectorLayoutNative]::PrintWindow($hwnd, $deviceContext, 2)
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }

        if (-not $printed) {
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($captureWidth, $captureHeight))
        }
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
$window.Title = "Automatic Vision Inspector Layout"
$window.Width = $WindowWidth
$window.Height = 600
$window.Left = 20
$window.Top = 20
$window.Topmost = $true
$window.Content = $control
$window.Show()
Wait-Dispatcher 400
$control.OpenPath($metadataPath)
Wait-Dispatcher 1000

$inventoryType = [System.Collections.Generic.List``1].MakeGenericType(
    [RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem])
$inventory = [Activator]::CreateInstance($inventoryType)
foreach ($item in @(
    @{ Name = "ImageAddress"; TypeName = "IntPtr"; SampleValue = "0x0000012345678000" },
    @{ Name = "Info.Width"; TypeName = "Int32"; SampleValue = "320" },
    @{ Name = "Info.Height"; TypeName = "Int32"; SampleValue = "240" },
    @{ Name = "Info.Stride"; TypeName = "Int32"; SampleValue = "320" },
    @{ Name = "Info.PixelFormat"; TypeName = "VendorPixelFormat"; SampleValue = "Mono8" }
)) {
    $entry = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $entry.Name = $item.Name
    $entry.Kind = "Member"
    $entry.TypeName = $item.TypeName
    $entry.SampleValue = $item.SampleValue
    $inventory.Add($entry)
}

$members = [Activator]::CreateInstance(
    [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingMembers])
$members.Data = "ImageAddress"
$members.Width = "Info.Width"
$members.Height = "Info.Height"
$members.Stride = "Info.Stride"
$members.PixelFormat = "Info.PixelFormat"

$bindingFlags = [Reflection.BindingFlags]"Instance,NonPublic"
$document = $control.GetType().GetField("_activeDocument", $bindingFlags).GetValue($control)
$setInspection = $document.GetType().GetMethod("SetAutomaticInspection", [Reflection.BindingFlags]"Instance,Public")
$arguments = New-Object object[] 8
$arguments[0] = 96
$arguments[1] = "Data=ImageAddress`nWidth=Info.Width, Height=Info.Height`nStride=Info.Stride, Format=Info.PixelFormat"
$arguments[2] = "Member inference and live memory descriptor validation passed."
$arguments[3] = $inventory
$arguments[4] = "Vendor.CameraSdk"
$arguments[5] = 1234
$arguments[6] = $false
$arguments[7] = $members
$setInspection.Invoke($document, $arguments) | Out-Null
$control.FindName("ImageList").Items.Refresh()
$updatePanel = $control.GetType().GetMethod("UpdateAutomaticInspectionPanel", $bindingFlags)
$updatePanel.Invoke($control, @($document)) | Out-Null
Wait-Dispatcher 300

$helper = New-Object System.Windows.Interop.WindowInteropHelper($window)
[AutomaticVisionInspectorLayoutNative]::SetWindowPos(
    $helper.Handle,
    [AutomaticVisionInspectorLayoutNative]::HWND_TOPMOST,
    20,
    20,
    $WindowWidth,
    600,
    0x0040) | Out-Null
[AutomaticVisionInspectorLayoutNative]::SetForegroundWindow($helper.Handle) | Out-Null
Wait-Dispatcher 300
Capture-Window $helper.Handle $capturePath

$panel = $control.FindName("AutomaticInspectionPanel")
$collectionBox = $control.FindName("IncludeImageCollectionsBox")
$confidence = $control.FindName("AutomaticInspectionConfidenceText").Text
$membersText = $control.FindName("AutomaticInspectionMembersText").Text
$validation = $control.FindName("AutomaticInspectionValidationText").Text
$expectFullInspector = $WindowWidth -ge 1040
if (($expectFullInspector -and $panel.Visibility -ne [System.Windows.Visibility]::Visible) -or
    ($null -eq $collectionBox) -or
    ($collectionBox.Content.ToString() -ne "Mat collections") -or
    ($expectFullInspector -and $confidence -ne "Confidence 96%") -or
    ($expectFullInspector -and -not $membersText.Contains("Info.Width")) -or
    ($expectFullInspector -and -not $validation.Contains("validation passed"))) {
    throw "Automatic Vision Inspector panel did not render the expected evidence."
}

@{
    Capture = $capturePath
    WindowWidth = $WindowWidth
    PanelVisible = ($panel.Visibility -eq [System.Windows.Visibility]::Visible)
    CollectionOptionVisible = $true
    CollectionOptionChecked = ($collectionBox.IsChecked -eq $true)
    Confidence = $confidence
    Members = $membersText
    Validation = $validation
} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8

$window.Close()
Write-Host "Automatic Vision Inspector layout smoke passed. Capture: $capturePath"

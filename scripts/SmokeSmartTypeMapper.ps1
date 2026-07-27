param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\smart-type-mapper",
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

$publicAssembliesPath = Split-Path -Parent $interopPath
foreach ($assemblyName in @(
    "Microsoft.VisualStudio.OLE.Interop.dll",
    "Microsoft.VisualStudio.Shell.Interop.dll",
    "Microsoft.VisualStudio.Debugger.InteropA.dll",
    "envdte.dll")) {
    $dependencyPath = Join-Path $publicAssembliesPath $assemblyName
    if (Test-Path -LiteralPath $dependencyPath) {
        [Reflection.Assembly]::LoadFrom($dependencyPath) | Out-Null
    }
}

$nugetPackagesRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    Join-Path $env:USERPROFILE ".nuget\packages"
} else {
    $env:NUGET_PACKAGES
}
$vssdkToolsPath = Join-Path $nugetPackagesRoot "microsoft.vssdk.buildtools\17.9.3168\tools\vssdk"
foreach ($assemblyName in @(
    "Microsoft.VisualStudio.Validation.dll",
    "Microsoft.VisualStudio.Threading.dll",
    "Microsoft.VisualStudio.Shell.Framework.dll",
    "Microsoft.VisualStudio.Shell.15.0.dll")) {
    $dependencyPath = Join-Path $vssdkToolsPath $assemblyName
    if (-not (Test-Path -LiteralPath $dependencyPath)) {
        throw "$assemblyName was not found at $vssdkToolsPath."
    }
    [Reflection.Assembly]::LoadFrom($dependencyPath) | Out-Null
}

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

if (-not ("MapperSmokeSample" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MapperSmokeSample {
    private static GCHandle _handle;
    public static byte[] Buffer;
    public static long Create(int width, int height) {
        Buffer = new byte[width * height];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                Buffer[(y * width) + x] = (byte)((x + (2 * y)) % 256);
            }
        }

        _handle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        return _handle.AddrOfPinnedObject().ToInt64();
    }

    public static void Free() {
        if (_handle.IsAllocated) {
            _handle.Free();
        }
    }
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
$assemblyDirectory = Split-Path -Parent $assemblyPath
foreach ($dependency in @("RawBufferVisualizer.Core.dll", "RawBufferVisualizer.Sdk.dll", "RawBufferVisualizer.VisualStudio.ObjectSource.dll", "RawBufferVisualizer.VisualStudio.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $assemblyDirectory $dependency)) | Out-Null
}
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null

$mappingPath = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::GetDefaultUserMappingPath()
$mappingBackupPath = $null
if (Test-Path -LiteralPath $mappingPath) {
    $mappingBackupPath = $mappingPath + ".smoke-backup"
    Copy-Item -LiteralPath $mappingPath -Destination $mappingBackupPath -Force
}

$currentPid = [Diagnostics.Process]::GetCurrentProcess().Id
$bufferAddress = [MapperSmokeSample]::Create(640, 480)
$dialogCapturePath = Join-Path $outputRoot "after-mapping-dialog.png"
$menuCapturePath = Join-Path $outputRoot "after-error-row-menu.png"

try {
    $inventory = New-Object "System.Collections.Generic.List[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]"
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "ImageAddress"; $member.Kind = "Property"; $member.TypeName = "IntPtr"
    $member.SampleValue = ("0x{0:X}" -f $bufferAddress)
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "SizeX"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "640"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "SizeY"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "480"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "LinePitch"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "640"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "PixelType"; $member.Kind = "Property"; $member.TypeName = "CompanyPixelType"; $member.SampleValue = "Mono8"
    $enumValues = New-Object "System.Collections.Generic.List[string]"
    $enumValues.Add("Mono8"); $enumValues.Add("Mono12"); $enumValues.Add("Bgr")
    $member.EnumValues = $enumValues
    $inventory.Add($member)

    $requestPath = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteErrorRequest(
        $currentPid,
        "[0]",
        "Company.Vision.CompanyFrame",
        "Unsupported collection image type: Company.Vision.CompanyFrame",
        $null, $null, $null,
        $inventory,
        "Company.Vision",
        $currentPid)

    $control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
    $window = New-Object System.Windows.Window
    $window.Title = "Raw Buffer Visualizer Smart Type Mapper"
    $window.Width = 1160
    $window.Height = 520
    $window.Left = 20
    $window.Top = 20
    $window.Topmost = $true
    $window.Content = $control
    $window.Show()
    Wait-Dispatcher 500

    $control.OpenHandoffRequest($requestPath)
    Wait-Dispatcher 500

    $imageList = $control.FindName("ImageList")
    if ($imageList.Items.Count -eq 0) {
        throw "Error handoff did not create a document row."
    }

    $imageList.SelectedIndex = 0
    Wait-Dispatcher 250

    $imageList.ContextMenu.IsOpen = $true
    Wait-Dispatcher 300
    $mapMenuItem = $control.FindName("MapThisTypeMenuItem")
    if ($mapMenuItem.Visibility -ne [System.Windows.Visibility]::Visible) {
        throw "Map This Type menu item was not visible for an error row with a member inventory."
    }

    Capture-Window (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle $menuCapturePath
    $imageList.ContextMenu.IsOpen = $false
    Wait-Dispatcher 200

    $driveTimer = New-Object System.Windows.Threading.DispatcherTimer
    $driveTimer.Interval = [TimeSpan]::FromMilliseconds(800)
    $driveTimer.Add_Tick({
        $driveTimer.Stop()
        $dialog = $null
        foreach ($source in [System.Windows.Interop.HwndSource]::CurrentSources) {
            $candidate = $source.RootVisual
            if ($candidate -is [RawBufferVisualizer.VisualStudio.Vssdk.TypeMappingDialog]) {
                $dialog = $candidate
                $script:dialogHwnd = $source.Handle
                break
            }
        }

        if ($null -eq $dialog) {
            throw "Type mapping dialog did not open from the error row."
        }

        if ($dialog.FindName("DataBox").SelectedItem -ne "ImageAddress") { throw "Data preselection failed." }
        if ($dialog.FindName("WidthBox").SelectedItem -ne "SizeX") { throw "Width preselection failed." }
        if ($dialog.FindName("HeightBox").SelectedItem -ne "SizeY") { throw "Height preselection failed." }
        if ($dialog.FindName("StrideBox").SelectedItem -ne "LinePitch") { throw "Stride preselection failed." }
        if ($dialog.FindName("PixelFormatBox").SelectedItem -ne "PixelType") { throw "Pixel format preselection failed." }
        if ($dialog.FindName("EnumMappingPanel").Visibility -ne [System.Windows.Visibility]::Visible) { throw "Enum mapping grid was not shown for the enum member." }

        $dialog.FindName("PreviewButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 600
        if ($null -eq $dialog.FindName("PreviewImage").Source) {
            throw "Preview did not render from live process memory."
        }

        $enumRows = $dialog.FindName("EnumMappingRows").Children
        foreach ($row in $enumRows) {
            $label = $row.Children[0].Text
            $box = $row.Children[1]
            if ($label -eq "Mono12") { $box.SelectedItem = "Mono12PackedLsb" }
            if ($label -eq "Bgr") { $box.SelectedItem = "BGR24" }
        }

        Wait-Dispatcher 300
        Capture-Window $script:dialogHwnd $dialogCapturePath
        Wait-Dispatcher 300
        $dialog.FindName("SaveButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
    })
    $driveTimer.Start()

    $mapMenuItem.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.MenuItem]::ClickEvent)))
    Wait-Dispatcher 3000

    if (-not (Test-Path -LiteralPath $mappingPath)) {
        throw "Mapping file was not written."
    }

    $savedJson = [IO.File]::ReadAllText($mappingPath)
    foreach ($expected in @("Company.Vision.CompanyFrame", "Company.Vision", "ImageAddress", "SizeX", "LinePitch", "pixelFormatMap", "Mono12PackedLsb", "BGR24")) {
        if (-not $savedJson.Contains($expected)) {
            throw "Saved mapping file is missing '$expected'."
        }
    }

    $window.Close()
    Wait-Dispatcher 250

    [pscustomobject]@{
        MenuCapture = $menuCapturePath
        DialogCapture = $dialogCapturePath
        MappingFile = $mappingPath
    } | Format-List
    Write-Host "Smart Type Mapper smoke passed. Captures: $menuCapturePath, $dialogCapturePath"
}
finally {
    [MapperSmokeSample]::Free()
    if ($mappingBackupPath -ne $null) {
        Copy-Item -LiteralPath $mappingBackupPath -Destination $mappingPath -Force
        Remove-Item -LiteralPath $mappingBackupPath -Force
    }
    elseif (Test-Path -LiteralPath $mappingPath) {
        Remove-Item -LiteralPath $mappingPath -Force
    }
}

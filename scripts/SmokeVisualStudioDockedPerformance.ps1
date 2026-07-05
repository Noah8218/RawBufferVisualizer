[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net472",
    [string]$ViewerFramework = "net472",
    [string]$VisualStudioInstanceId = "",
    [string]$RootSuffix = "",
    [ValidateSet("Mono8", "Mono16", "BGR24", "Float32")]
    [string]$PixelFormat = "Mono8",
    [int]$Width = 5000,
    [int]$Height = 5000,
    [int]$MaxOpenPathMs = 5000,
    [int]$MaxCommandMs = 150,
    [int]$MaxFrameMs = 350,
    [int]$MaxUploadMs = 150,
    [string]$OutputDir = "artifacts\perf\vs-docked",
    [string]$SolutionPath = "",
    [switch]$IncludeExternalInput,
    [switch]$NoBuild,
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$outputRoot = Join-Path $repoRoot $OutputDir
$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $outputRoot "RawBufferVisualizerSmoke.sln"
    if (-not (Test-Path -LiteralPath $SolutionPath)) {
        @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
EndGlobal
"@ | Set-Content -LiteralPath $SolutionPath -Encoding UTF8
    }
}

$rawMetricsPath = Join-Path $outputRoot "visual-studio-docked-performance.raw.json"
$resultPath = Join-Path $outputRoot "visual-studio-docked-performance.json"
$screenshotPath = Join-Path $outputRoot "visual-studio-docked-performance.png"
$sessionScreenshotPath = Join-Path $outputRoot "visual-studio-docked-session.png"
$errorSessionScreenshotPath = Join-Path $outputRoot "visual-studio-docked-session-error.png"
$framebufferPath = Join-Path $outputRoot "visual-studio-docked-framebuffer.png"
$sessionPath = Join-Path $outputRoot "visual-studio-docked-session.json"
$activityLogPath = Join-Path $outputRoot "visual-studio-activity-log.xml"
$installLogPath = Join-Path $outputRoot "vsix-install.log"
$externalReadyPath = Join-Path $outputRoot "visual-studio-docked-performance.external-ready"
$packageLogPath = [System.IO.Path]::ChangeExtension($rawMetricsPath, ".package.log")
Remove-Item -LiteralPath $rawMetricsPath, $resultPath, $screenshotPath, $sessionScreenshotPath, $errorSessionScreenshotPath, $framebufferPath, $sessionPath, $activityLogPath, $packageLogPath, $externalReadyPath -ErrorAction SilentlyContinue

function Find-VisualStudioInstance {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
        (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
    )
    $vswhere = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $vswhere) {
        throw "vswhere.exe was not found."
    }

    $json = & $vswhere -all -format json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        throw "vswhere failed."
    }

    $instances = $json | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstanceId)) {
        $selected = $instances | Where-Object { $_.instanceId -eq $VisualStudioInstanceId } | Select-Object -First 1
        if (-not $selected) {
            throw "Visual Studio instance was not found: $VisualStudioInstanceId"
        }

        return $selected
    }

    $instance = $instances |
        Where-Object { $_.installationVersion -like "17.*" -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    if (-not $instance) {
        throw "A launchable Visual Studio 2022 instance was not found."
    }

    $instance
}

function Find-VsixInstaller([string]$installationPath) {
    $candidate = Join-Path $installationPath "Common7\IDE\VSIXInstaller.exe"
    if (Test-Path -LiteralPath $candidate) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }

    $fallback = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe"
    if (Test-Path -LiteralPath $fallback) {
        return (Resolve-Path -LiteralPath $fallback).Path
    }

    throw "VSIXInstaller.exe was not found."
}

function Wait-Until([string]$description, [scriptblock]$condition, [int]$timeoutSeconds = 120) {
    $deadline = [DateTime]::Now.AddSeconds($timeoutSeconds)
    $lastError = $null
    do {
        try {
            $value = & $condition
            if ($value) {
                return $value
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::Now -lt $deadline)

    if ($lastError) {
        throw "$description timed out. Last error: $lastError"
    }

    throw "$description timed out."
}

function New-PatternRow([int]$rowIndex, [int]$width, [int]$stride, [string]$pixelFormat) {
    $row = New-Object byte[] $stride
    for ($x = 0; $x -lt $width; $x++) {
        if ($pixelFormat -eq "BGR24") {
            $offset = $x * 3
            $blue = [byte](32 + (([int][Math]::Floor($x / 16.0)) % 224))
            $green = [byte](32 + (([int][Math]::Floor($rowIndex / 16.0)) % 224))
            $red = [byte](32 + ((([int][Math]::Floor($x / 16.0)) + ([int][Math]::Floor($rowIndex / 16.0))) % 224))
            $row[$offset] = $blue
            $row[$offset + 1] = $green
            $row[$offset + 2] = $red
        }
        elseif ($pixelFormat -eq "Mono16") {
            $offset = $x * 2
            $value = [uint16](256 + ((($x * 7) + ($rowIndex * 11)) % 4096))
            $bytes = [BitConverter]::GetBytes($value)
            $row[$offset] = $bytes[0]
            $row[$offset + 1] = $bytes[1]
        }
        elseif ($pixelFormat -eq "Float32") {
            $offset = $x * 4
            $value = [single](((($x % 256) / 255.0) + (($rowIndex % 256) / 255.0)) / 2.0)
            $bytes = [BitConverter]::GetBytes($value)
            $row[$offset] = $bytes[0]
            $row[$offset + 1] = $bytes[1]
            $row[$offset + 2] = $bytes[2]
            $row[$offset + 3] = $bytes[3]
        }
        else {
            $block = ([int][Math]::Floor($x / 32.0)) + ([int][Math]::Floor($rowIndex / 32.0))
            $row[$x] = [byte](32 + ($block % 224))
        }
    }

    $row
}

function New-SampleRaw([string]$path, [int]$width, [int]$height, [int]$stride, [string]$pixelFormat) {
    $length = [int64]$stride * [int64]$height
    $existing = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
    if ($existing -and $existing.Length -eq $length) {
        return
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::ReadWrite)
    try {
        for ($y = 0; $y -lt $height; $y++) {
            $row = New-PatternRow $y $width $stride $pixelFormat
            $stream.Write($row, 0, $row.Length)
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Write-HandoffRequest([string]$metadataPath, [string]$displayName = "", [string]$sourceType = "") {
    $inbox = Join-Path $env:TEMP "RawBufferVisualizer\VisualStudio\Inbox"
    New-Item -ItemType Directory -Force -Path $inbox | Out-Null
    $name = "{0}_{1}.rbuf-handoff" -f ([DateTime]::UtcNow.ToString("yyyyMMddHHmmssfffffff", [Globalization.CultureInfo]::InvariantCulture)), ([Guid]::NewGuid().ToString("N"))
    $requestPath = Join-Path $inbox $name
    if ([string]::IsNullOrWhiteSpace($displayName) -and [string]::IsNullOrWhiteSpace($sourceType)) {
        Set-Content -LiteralPath $requestPath -Encoding UTF8 -NoNewline -Value ([System.IO.Path]::GetFullPath($metadataPath))
    }
    else {
        ([ordered]@{
            metadataPath = [System.IO.Path]::GetFullPath($metadataPath)
            displayName = $displayName
            sourceType = $sourceType
        } | ConvertTo-Json -Compress) | Set-Content -LiteralPath $requestPath -Encoding UTF8 -NoNewline
    }

    $requestPath
}

function Clear-HandoffInbox {
    $tempRoot = [System.IO.Path]::GetFullPath($env:TEMP)
    $inbox = Join-Path $env:TEMP "RawBufferVisualizer\VisualStudio\Inbox"
    New-Item -ItemType Directory -Force -Path $inbox | Out-Null
    $fullInbox = [System.IO.Path]::GetFullPath($inbox)
    if (-not $fullInbox.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean handoff inbox outside TEMP: $fullInbox"
    }

    Get-ChildItem -LiteralPath $fullInbox -Filter "*.rbuf-handoff" -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

if (-not ("RawBufferVsDockedPerfNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferVsDockedPerfNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
}
'@
}

function Focus-VisualStudioWindow([IntPtr]$hwnd) {
    [RawBufferVsDockedPerfNative]::ShowWindow($hwnd, 9) | Out-Null
    for ($i = 0; $i -lt 3; $i++) {
        [RawBufferVsDockedPerfNative]::SetWindowPos($hwnd, [RawBufferVsDockedPerfNative]::HWND_TOPMOST, 20, 20, 1500, 900, 0x0040) | Out-Null
        [RawBufferVsDockedPerfNative]::BringWindowToTop($hwnd) | Out-Null
        [RawBufferVsDockedPerfNative]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 150
    }
}

if (-not ("RawBufferVsDockedPerfRot" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class RawBufferVsDockedPerfRot {
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx bindCtx);

    public static object GetDte(int processId) {
        IRunningObjectTable rot;
        IBindCtx bindCtx;
        if (GetRunningObjectTable(0, out rot) != 0 || rot == null) {
            return null;
        }

        if (CreateBindCtx(0, out bindCtx) != 0 || bindCtx == null) {
            return null;
        }

        IEnumMoniker enumMoniker;
        rot.EnumRunning(out enumMoniker);
        IMoniker[] monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0) {
            string displayName;
            try {
                monikers[0].GetDisplayName(bindCtx, null, out displayName);
            }
            catch {
                continue;
            }

            if (displayName.IndexOf("VisualStudio.DTE.17.0:" + processId, StringComparison.OrdinalIgnoreCase) >= 0) {
                object dte;
                rot.GetObject(monikers[0], out dte);
                return dte;
            }
        }

        return null;
    }
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleMessageFilter {
    [PreserveSig]
    int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

    [PreserveSig]
    int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}

public sealed class RawBufferVsDockedPerfMessageFilter : IOleMessageFilter, IDisposable {
    private IOleMessageFilter previousFilter;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);

    public static RawBufferVsDockedPerfMessageFilter Register() {
        var filter = new RawBufferVsDockedPerfMessageFilter();
        IOleMessageFilter oldFilter;
        CoRegisterMessageFilter(filter, out oldFilter);
        filter.previousFilter = oldFilter;
        return filter;
    }

    public void Dispose() {
        IOleMessageFilter ignored;
        CoRegisterMessageFilter(previousFilter, out ignored);
    }

    public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) {
        return 0;
    }

    public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) {
        if (dwTickCount < 30000) {
            return 250;
        }

        return -1;
    }

    public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) {
        return 2;
    }
}
'@
}

function Invoke-RawBufferVisualizerCommand([int]$ProcessId) {
    $dte = Wait-Until "Visual Studio DTE" {
        [RawBufferVsDockedPerfRot]::GetDte($ProcessId)
    } 120

    $messageFilter = [RawBufferVsDockedPerfMessageFilter]::Register()
    try {
        Wait-Until "Raw Buffer Visualizer command" {
            $commands = $dte.GetType().InvokeMember(
                "Commands",
                [Reflection.BindingFlags]::GetProperty,
                $null,
                $dte,
                @())
            $args = @("{8e7bc2db-12a4-4f45-8f5a-38c1846a0f26}", 0x0100, $null, $null)
            $commands.GetType().InvokeMember(
                "Raise",
                [Reflection.BindingFlags]::InvokeMethod,
                $null,
                $commands,
                $args) | Out-Null
            $true
        } 120 | Out-Null

        try {
            Wait-Until "Raw Buffer Visualizer tool window activation" {
                $windows = $dte.Windows
                $count = [int]$windows.Count
                for ($i = 1; $i -le $count; $i++) {
                    $window = $windows.Item($i)
                    $caption = [string]$window.Caption
                    if ($caption -like "*Raw Buffer Visualizer*") {
                        try {
                            $window.Visible = $true
                        }
                        catch {
                        }

                        $window.Activate()
                        return $true
                    }
                }

                return $false
            } 20 | Out-Null
        }
        catch {
            Write-Host "ToolWindow activation probe skipped: $($_.Exception.Message)"
        }
    }
    finally {
        $messageFilter.Dispose()
    }
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    Add-Type -AssemblyName System.Drawing
    $rect = New-Object RawBufferVsDockedPerfNative+RECT
    [RawBufferVsDockedPerfNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid Visual Studio window bounds: $width x $height"
    }

    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height))
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Measure-ImageContent([string]$path) {
    Add-Type -AssemblyName System.Drawing
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Image content file was not found: $path"
    }

    $bitmap = [Drawing.Bitmap]::FromFile($path)
    try {
        $sampled = 0
        $nonDark = 0
        $bright = 0
        $colors = New-Object 'System.Collections.Generic.HashSet[int]'
        $stepX = [Math]::Max(1, [int]($bitmap.Width / 256))
        $stepY = [Math]::Max(1, [int]($bitmap.Height / 256))
        for ($y = 0; $y -lt $bitmap.Height; $y += $stepY) {
            for ($x = 0; $x -lt $bitmap.Width; $x += $stepX) {
                $color = $bitmap.GetPixel($x, $y)
                $luma = ($color.R + $color.G + $color.B) / 3.0
                $sampled++
                if ($luma -gt 25) { $nonDark++ }
                if ($luma -gt 150) { $bright++ }
                [void]$colors.Add(($color.R -shl 16) -bor ($color.G -shl 8) -bor $color.B)
            }
        }

        [ordered]@{
            path = $path
            width = $bitmap.Width
            height = $bitmap.Height
            sampled = $sampled
            nonDarkRatio = [Math]::Round($nonDark / [double]$sampled, 4)
            brightRatio = [Math]::Round($bright / [double]$sampled, 4)
            uniqueSampledColors = $colors.Count
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Find-ImageViewBounds([IntPtr]$hwnd) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    if (-not $root) {
        throw "Visual Studio automation root was not found."
    }

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "RawBufferOpenGlImageView")
    $elements = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)

    foreach ($element in $elements) {
        $rect = $element.Current.BoundingRectangle
        if ($rect.Width -gt 20 -and $rect.Height -gt 20) {
            return $rect
        }
    }

    throw "RawBufferOpenGlImageView automation element was not found."
}

function Invoke-RealMouseInput([System.Windows.Rect]$bounds) {
    $centerX = [int]($bounds.Left + ($bounds.Width / 2))
    $centerY = [int]($bounds.Top + ($bounds.Height / 2))
    [RawBufferVsDockedPerfNative]::SetCursorPos($centerX, $centerY) | Out-Null
    Start-Sleep -Milliseconds 100

    for ($i = 0; $i -lt 36; $i++) {
        $delta = if (($i % 8) -lt 4) { 120 } else { -120 }
        [RawBufferVsDockedPerfNative]::mouse_event([RawBufferVsDockedPerfNative]::MOUSEEVENTF_WHEEL, 0, 0, $delta, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 12
    }

    $startX = [int]($bounds.Left + [Math]::Max(20, $bounds.Width * 0.35))
    $startY = [int]($bounds.Top + [Math]::Max(20, $bounds.Height * 0.45))
    [RawBufferVsDockedPerfNative]::SetCursorPos($startX, $startY) | Out-Null
    Start-Sleep -Milliseconds 100
    [RawBufferVsDockedPerfNative]::mouse_event([RawBufferVsDockedPerfNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    try {
        for ($i = 0; $i -lt 140; $i++) {
            $phase = $i % 70
            $dx = if ($phase -lt 35) { $phase * 4 } else { (70 - $phase) * 4 }
            $dy = if (($i % 40) -lt 20) { ($i % 20) * 2 } else { (20 - ($i % 20)) * 2 }
            [RawBufferVsDockedPerfNative]::SetCursorPos($startX + $dx, $startY + $dy) | Out-Null
            Start-Sleep -Milliseconds 8
        }
    }
    finally {
        [RawBufferVsDockedPerfNative]::mouse_event([RawBufferVsDockedPerfNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    }
}

$vsInstance = Find-VisualStudioInstance
$devenv = $vsInstance.productPath
$sampleName = "$($PixelFormat.ToLowerInvariant())-$Width-x-$Height"
$metadataPath = Join-Path $sampleRoot "$sampleName.rbuf.json"
$metadataPath2 = Join-Path $sampleRoot "$sampleName-second.rbuf.json"
$missingMetadataPath = Join-Path $sampleRoot "$sampleName-missing.rbuf.json"
$rawPath = Join-Path $sampleRoot "$sampleName.raw"
$stride = switch ($PixelFormat) {
    "BGR24" { $Width * 3 }
    "Mono16" { $Width * 2 }
    "Float32" { $Width * 4 }
    default { $Width }
}
$validBits = if ($PixelFormat -eq "Float32") { 32 } elseif ($PixelFormat -eq "Mono16") { 16 } else { 8 }
$rawLength = [int64]$stride * [int64]$Height
New-SampleRaw $rawPath $Width $Height $stride $PixelFormat
Clear-HandoffInbox
([ordered]@{
    rawFile = "$sampleName.raw"
    width = $Width
    height = $Height
    stride = $stride
    pixelFormat = $PixelFormat
    validBits = $validBits
    byteOrder = "LittleEndian"
} | ConvertTo-Json) | Set-Content -LiteralPath $metadataPath -Encoding UTF8

([ordered]@{
    rawFile = "$sampleName.raw"
    width = $Width
    height = $Height
    stride = $stride
    pixelFormat = $PixelFormat
    validBits = $validBits
    byteOrder = "LittleEndian"
} | ConvertTo-Json) | Set-Content -LiteralPath $metadataPath2 -Encoding UTF8

if (-not $NoBuild) {
    & powershell -ExecutionPolicy Bypass -File .\scripts\Publish-VisualStudioExtension.ps1 -Framework $Framework -Configuration $Configuration -ViewerFramework $ViewerFramework -NoZip
    if ($LASTEXITCODE -ne 0) {
        throw "VSIX publish failed with exit code $LASTEXITCODE."
    }
}

$vsixPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Extensibility.vsix"
if (-not (Test-Path -LiteralPath $vsixPath)) {
    throw "VSIX was not found: $vsixPath"
}

if (-not $NoInstall) {
    $installer = Find-VsixInstaller $vsInstance.installationPath
    $installArgs = @(
        "/quiet",
        "/force",
        "/instanceIds:$($vsInstance.instanceId)",
        "/logFile:$installLogPath",
        $vsixPath
    )
    $install = Start-Process -FilePath $installer -ArgumentList $installArgs -Wait -PassThru -WindowStyle Hidden
    if ($install.ExitCode -ne 0) {
        throw "VSIX install failed with exit code $($install.ExitCode). Log: $installLogPath"
    }
}

$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = $devenv
$resolvedSolutionPath = if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    ""
}
elseif ([System.IO.Path]::IsPathRooted($SolutionPath)) {
    (Resolve-Path -LiteralPath $SolutionPath).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $repoRoot $SolutionPath)).Path
}
$devenvArguments = @()
if (-not [string]::IsNullOrWhiteSpace($resolvedSolutionPath)) {
    $devenvArguments += "`"$resolvedSolutionPath`""
}

$devenvArguments += @("/NoSplash", "/Log", "`"$activityLogPath`"")
if (-not [string]::IsNullOrWhiteSpace($RootSuffix)) {
    $devenvArguments += @("/RootSuffix", $RootSuffix)
}
$psi.Arguments = $devenvArguments -join " "
$psi.UseShellExecute = $false
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_JSON"] = $rawMetricsPath
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_ZOOM_ITERATIONS"] = "30"
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_PAN_ITERATIONS"] = "30"
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_WHEEL_ITERATIONS"] = "60"
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_DRAG_ITERATIONS"] = "120"
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_EXTERNAL_INPUT_SECONDS"] = if ($IncludeExternalInput) { "10" } else { "0" }
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_PERF_EXTERNAL_READY_FILE"] = $externalReadyPath
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_FRAMEBUFFER_PNG"] = $framebufferPath
$psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_SESSION_JSON"] = $sessionPath
$process = [Diagnostics.Process]::Start($psi)

try {
    $hwnd = Wait-Until "Visual Studio main window" {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) { $process.MainWindowHandle } else { $null }
    } 120

    Focus-VisualStudioWindow $hwnd

    Invoke-RawBufferVisualizerCommand $process.Id
    Start-Sleep -Seconds 2
    $displayPrefix = switch ($PixelFormat) {
        "BGR24" { "rawBgr24" }
        "Mono16" { "rawMono16" }
        "Float32" { "rawFloat32" }
        default { "rawMono8" }
    }
    $requestPath = Write-HandoffRequest $metadataPath "$($displayPrefix)Snapshot" "RawBufferVisualizer.Sdk.RawBufferSnapshot"

    if ($IncludeExternalInput) {
        Wait-Until "external mouse input probe" {
            if (Test-Path -LiteralPath $externalReadyPath) { $true } else { $null }
        } 180 | Out-Null
        Focus-VisualStudioWindow $hwnd
        $imageBounds = Find-ImageViewBounds $hwnd
        Focus-VisualStudioWindow $hwnd
        Invoke-RealMouseInput $imageBounds
    }

    $metrics = Wait-Until "docked performance metrics" {
        if (-not (Test-Path -LiteralPath $rawMetricsPath)) {
            return $null
        }

        $json = Get-Content -LiteralPath $rawMetricsPath -Raw
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        $parsed = $json | ConvertFrom-Json
        $expectedMetadataPath = [System.IO.Path]::GetFullPath($metadataPath)
        $actualMetadataPath = if ($parsed.metadataPath) { [System.IO.Path]::GetFullPath([string]$parsed.metadataPath) } else { "" }
        if (-not [string]::Equals($actualMetadataPath, $expectedMetadataPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $null
        }

        $parsed
    } 180

    Start-Sleep -Seconds 1
    Focus-VisualStudioWindow $hwnd
    Capture-Window $hwnd $screenshotPath
    $framebufferContent = Measure-ImageContent $framebufferPath

    $secondRequestPath = Write-HandoffRequest $metadataPath2 "$($displayPrefix)View" "RawBufferVisualizer.Sdk.RawBufferView"
    Wait-Until "second docked image session state" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        $session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
        if ($session.documentCount -ge 2) { $session } else { $null }
    } 120 | Out-Null
    Focus-VisualStudioWindow $hwnd
    Capture-Window $hwnd $sessionScreenshotPath

    $badRequestPath = Write-HandoffRequest $missingMetadataPath "missingBuffer" "InvalidSample"
    $sessionState = Wait-Until "error docked image session state" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        $session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
        if ($session.documentCount -ge 3 -and $session.errorCount -ge 1) { $session } else { $null }
    } 120
    Focus-VisualStudioWindow $hwnd
    Capture-Window $hwnd $errorSessionScreenshotPath

    $result = [ordered]@{
        sample = [ordered]@{
            width = $Width
            height = $Height
            stride = $stride
            pixelFormat = $PixelFormat
            rawLength = $rawLength
            metadataPath = $metadataPath
            handoffRequestPath = $requestPath
            secondMetadataPath = $metadataPath2
            secondHandoffRequestPath = $secondRequestPath
            errorMetadataPath = $missingMetadataPath
            errorHandoffRequestPath = $badRequestPath
        }
        rootSuffix = $RootSuffix
        visualStudioInstanceId = $vsInstance.instanceId
        visualStudioVersion = $vsInstance.installationVersion
        devenv = $devenv
        solutionPath = $resolvedSolutionPath
        screenshotPath = $screenshotPath
        sessionScreenshotPath = $sessionScreenshotPath
        errorSessionScreenshotPath = $errorSessionScreenshotPath
        framebufferPath = $framebufferPath
        sessionPath = $sessionPath
        framebufferContent = $framebufferContent
        sessionState = $sessionState
        activityLogPath = $activityLogPath
        thresholds = [ordered]@{
            maxOpenPathMs = $MaxOpenPathMs
            maxCommandMs = $MaxCommandMs
            maxFrameMs = $MaxFrameMs
            maxUploadMs = $MaxUploadMs
        }
        metrics = $metrics
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding UTF8

    $failures = New-Object System.Collections.Generic.List[string]
    if ($metrics.openPathMs -gt $MaxOpenPathMs) {
        $failures.Add("OpenPathMs $($metrics.openPathMs) > $MaxOpenPathMs")
    }
    if ($metrics.error) {
        $failures.Add("Docked probe error: $($metrics.error)")
    }
    if ($metrics.imageViewActualWidth -le 10 -or $metrics.imageViewActualHeight -le 10) {
        $failures.Add("Image view did not receive a visible layout size: $($metrics.imageViewActualWidth)x$($metrics.imageViewActualHeight)")
    }
    if ($metrics.toolWindowActualWidth -lt 980 -and -not $metrics.compactInspectorVisible) {
        $failures.Add("Compact inspector was not visible in narrow docked layout: width=$($metrics.toolWindowActualWidth)")
    }
    if ($metrics.toolWindowActualWidth -ge 980 -and -not $metrics.inspectorVisible) {
        $failures.Add("Inspector was not visible in wide docked layout: width=$($metrics.toolWindowActualWidth)")
    }
    if (-not $metrics.pixelGridOverlayVisible) {
        $failures.Add("High-zoom pixel grid overlay was not enabled during the docked probe")
    }
    if (-not $metrics.pixelGridOverlayVisible -and -not $metrics.pixelOverlayVisible) {
        $failures.Add("No high-zoom pixel overlay was visible during the docked probe")
    }
    if (-not $metrics.pixelGridOverlayVisible -and ([string]::IsNullOrWhiteSpace([string]$metrics.pixelOverlayText) -or ([string]$metrics.pixelOverlayText).IndexOf("X=", [System.StringComparison]::Ordinal) -lt 0)) {
        $failures.Add("High-zoom pixel overlay text was not captured")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.pixelStatusPosition) -or ([string]$metrics.pixelStatusPosition).IndexOf("X", [System.StringComparison]::Ordinal) -lt 0) {
        $failures.Add("Pixel status position was not captured")
    }
    if (([string]$metrics.pixelStatusPosition).IndexOf(",", [System.StringComparison]::Ordinal) -ge 0) {
        $failures.Add("Pixel status position still contains thousands separators: $($metrics.pixelStatusPosition)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.pixelStatusColor) -or [string]::Equals([string]$metrics.pixelStatusColor, "RGB -", [System.StringComparison]::Ordinal)) {
        $failures.Add("Pixel status color/value was not captured")
    }
    if ($PixelFormat -eq "BGR24" -and ([string]$metrics.pixelStatusColor).IndexOf("RGB", [System.StringComparison]::Ordinal) -lt 0) {
        $failures.Add("BGR24 pixel status did not expose RGB channel values: $($metrics.pixelStatusColor)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.pixelStatusRaw) -or [string]::Equals([string]$metrics.pixelStatusRaw, "Bytes -", [System.StringComparison]::Ordinal)) {
        $failures.Add("Pixel status raw bytes were not captured")
    }
    if (([string]$metrics.pixelStatusRaw).IndexOf("Raw ", [System.StringComparison]::Ordinal) -ge 0) {
        $failures.Add("Pixel status raw byte label is still unclear: $($metrics.pixelStatusRaw)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.roiStats) -or ([string]$metrics.roiStats).IndexOf("mean=", [System.StringComparison]::Ordinal) -lt 0) {
        $failures.Add("ROI statistics were not captured: $($metrics.roiStats)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.markerText) -or ([string]$metrics.markerText).IndexOf("X ", [System.StringComparison]::Ordinal) -lt 0) {
        $failures.Add("Pinned marker text was not captured: $($metrics.markerText)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.pinnedMarker) -or ([string]$metrics.pinnedMarker).IndexOf("X ", [System.StringComparison]::Ordinal) -lt 0) {
        $failures.Add("Pinned marker was not kept by the image canvas: $($metrics.pinnedMarker)")
    }
    if ([string]::IsNullOrWhiteSpace([string]$metrics.blackLevel) -or [string]::IsNullOrWhiteSpace([string]$metrics.whiteLevel)) {
        $failures.Add("Render levels were not exposed: black=$($metrics.blackLevel), white=$($metrics.whiteLevel)")
    }
    if (($metrics.zoom.frameCount + $metrics.pan.frameCount) -le 0) {
        $failures.Add("No viewer frames were recorded in the docked ToolWindow")
    }
    if ($framebufferContent.nonDarkRatio -lt 0.05) {
        $failures.Add("Docked framebuffer appears blank: nonDarkRatio $($framebufferContent.nonDarkRatio)")
    }
    if ($framebufferContent.uniqueSampledColors -lt 8) {
        $failures.Add("Docked framebuffer has too little image variation: uniqueSampledColors $($framebufferContent.uniqueSampledColors)")
    }
    if ($metrics.maxZoomCommandMs -gt $MaxCommandMs) {
        $failures.Add("MaxZoomCommandMs $($metrics.maxZoomCommandMs) > $MaxCommandMs")
    }
    if ($metrics.maxPanCommandMs -gt $MaxCommandMs) {
        $failures.Add("MaxPanCommandMs $($metrics.maxPanCommandMs) > $MaxCommandMs")
    }
    if ($metrics.maxWheelCommandMs -gt $MaxCommandMs) {
        $failures.Add("MaxWheelCommandMs $($metrics.maxWheelCommandMs) > $MaxCommandMs")
    }
    if ($metrics.maxDragCommandMs -gt $MaxCommandMs) {
        $failures.Add("MaxDragCommandMs $($metrics.maxDragCommandMs) > $MaxCommandMs")
    }
    if ($metrics.zoom.maxFrameMs -gt $MaxFrameMs) {
        $failures.Add("Zoom MaxFrameMs $($metrics.zoom.maxFrameMs) > $MaxFrameMs")
    }
    if ($metrics.pan.maxFrameMs -gt $MaxFrameMs) {
        $failures.Add("Pan MaxFrameMs $($metrics.pan.maxFrameMs) > $MaxFrameMs")
    }
    if ($metrics.wheel.maxFrameMs -gt $MaxFrameMs) {
        $failures.Add("Wheel MaxFrameMs $($metrics.wheel.maxFrameMs) > $MaxFrameMs")
    }
    if ($metrics.drag.maxFrameMs -gt $MaxFrameMs) {
        $failures.Add("Drag MaxFrameMs $($metrics.drag.maxFrameMs) > $MaxFrameMs")
    }
    if ($metrics.zoom.maxUploadMs -gt $MaxUploadMs) {
        $failures.Add("Zoom MaxUploadMs $($metrics.zoom.maxUploadMs) > $MaxUploadMs")
    }
    if ($metrics.pan.maxUploadMs -gt $MaxUploadMs) {
        $failures.Add("Pan MaxUploadMs $($metrics.pan.maxUploadMs) > $MaxUploadMs")
    }
    if ($metrics.wheel.maxUploadMs -gt $MaxUploadMs) {
        $failures.Add("Wheel MaxUploadMs $($metrics.wheel.maxUploadMs) > $MaxUploadMs")
    }
    if ($metrics.drag.maxUploadMs -gt $MaxUploadMs) {
        $failures.Add("Drag MaxUploadMs $($metrics.drag.maxUploadMs) > $MaxUploadMs")
    }
    if ($IncludeExternalInput) {
        if ($metrics.externalInput.wheelInputCount -le 0) {
            $failures.Add("External mouse wheel input was not recorded")
        }
        if ($metrics.externalInput.dragInputCount -le 0) {
            $failures.Add("External mouse drag input was not recorded")
        }
        if ($metrics.externalInput.maxWheelInputMs -gt $MaxCommandMs) {
            $failures.Add("External MaxWheelInputMs $($metrics.externalInput.maxWheelInputMs) > $MaxCommandMs")
        }
        if ($metrics.externalInput.maxDragInputMs -gt $MaxCommandMs) {
            $failures.Add("External MaxDragInputMs $($metrics.externalInput.maxDragInputMs) > $MaxCommandMs")
        }
        if ($metrics.externalInput.maxFrameMs -gt $MaxFrameMs) {
            $failures.Add("External MaxFrameMs $($metrics.externalInput.maxFrameMs) > $MaxFrameMs")
        }
        if ($metrics.externalInput.maxUploadMs -gt $MaxUploadMs) {
            $failures.Add("External MaxUploadMs $($metrics.externalInput.maxUploadMs) > $MaxUploadMs")
        }
    }
    if ($sessionState.documentCount -lt 3) {
        $failures.Add("Docket session did not accumulate three rows: $($sessionState.documentCount)")
    }
    if ($sessionState.errorCount -lt 1) {
        $failures.Add("Docket session did not keep an error row")
    }
    $sourceTypes = @($sessionState.documents | ForEach-Object { $_.sourceType })
    if ($sourceTypes -notcontains "RawBufferVisualizer.Sdk.RawBufferSnapshot") {
        $failures.Add("Session source type for RawBufferSnapshot was not preserved")
    }
    if ($sourceTypes -notcontains "RawBufferVisualizer.Sdk.RawBufferView") {
        $failures.Add("Session source type for RawBufferView was not preserved")
    }

    Get-Content -LiteralPath $resultPath
    if ($failures.Count -gt 0) {
        throw "Visual Studio docked performance smoke failed: $($failures -join '; ')"
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 5
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
}

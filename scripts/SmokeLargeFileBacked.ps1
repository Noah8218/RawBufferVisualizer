param(
    [string]$Configuration = "Debug",
    [string]$ViewerFramework = "net472",
    [ValidateSet("Mono8", "Mono10PackedLsb", "Mono12PackedLsb")]
    [string]$PixelFormat = "Mono8",
    [string]$OutputDir = "artifacts\ui\large-file-backed",
    [string]$CaptureFileName = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
Set-Location $repoRoot

dotnet build .\RawBufferVisualizer.sln --configuration $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$outputRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$sampleRoot = Join-Path $repoRoot "artifacts\large-file-backed"
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
$sampleName = "huge-$($PixelFormat.ToLowerInvariant())"
$metadataPath = Join-Path $sampleRoot "$sampleName.rbuf.json"
$rawPath = Join-Path $sampleRoot "$sampleName.raw"
if ([string]::IsNullOrWhiteSpace($CaptureFileName)) {
    $CaptureFileName = "viewer-100k-$($PixelFormat.ToLowerInvariant())-file-backed.png"
}

switch ($PixelFormat) {
    "Mono10PackedLsb" {
        $validBits = 10
        $stride = [int][Math]::Ceiling((100000 * 10) / 8.0)
    }
    "Mono12PackedLsb" {
        $validBits = 12
        $stride = [int][Math]::Ceiling((100000 * 12) / 8.0)
    }
    default {
        $validBits = 8
        $stride = 100000
    }
}

$metadata = [ordered]@{
    rawFile = "$sampleName.raw"
    width = 100000
    height = 100000
    stride = $stride
    pixelFormat = $PixelFormat
    validBits = $validBits
    byteOrder = "LittleEndian"
} | ConvertTo-Json
Set-Content -LiteralPath $metadataPath -Value $metadata -Encoding UTF8

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ("RawBufferLargeFileBackedNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferLargeFileBackedNative {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint FSCTL_SET_SPARSE = 0x000900C4;
}
'@
}

function Set-PackedValue([byte[]]$row, [int]$x, [int]$bitsPerPixel, [int]$value) {
    $bitOffset = $x * $bitsPerPixel
    $byteOffset = [int][Math]::Floor($bitOffset / 8)
    $shift = $bitOffset % 8
    $packedValue = $value -shl $shift
    $bytesToWrite = [int][Math]::Floor(($shift + $bitsPerPixel + 7) / 8)
    for ($i = 0; $i -lt $bytesToWrite; $i++) {
        $row[$byteOffset + $i] = [byte]($row[$byteOffset + $i] -bor (($packedValue -shr ($i * 8)) -band 0xFF))
    }
}

function New-PatternRow([string]$pixelFormat, [int]$width, [int]$stride) {
    $row = New-Object byte[] $stride
    switch ($pixelFormat) {
        "Mono10PackedLsb" {
            for ($x = 0; $x -lt $width; $x++) {
                Set-PackedValue $row $x 10 (64 + (($x / 512) % 960))
            }
        }
        "Mono12PackedLsb" {
            for ($x = 0; $x -lt $width; $x++) {
                Set-PackedValue $row $x 12 (256 + (($x / 512) % 3840))
            }
        }
        default {
            for ($x = 0; $x -lt $width; $x++) {
                $row[$x] = [byte](32 + (($x / 512) % 224))
            }
        }
    }

    $row
}

function New-SparsePatternRaw([string]$path, [int]$width, [int]$height, [int]$stride, [string]$pixelFormat) {
    $length = [int64]$stride * [int64]$height
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::ReadWrite)
    try {
        $bytesReturned = 0
        $sparse = [RawBufferLargeFileBackedNative]::DeviceIoControl(
            $stream.SafeFileHandle.DangerousGetHandle(),
            [RawBufferLargeFileBackedNative]::FSCTL_SET_SPARSE,
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
        $row = New-PatternRow $pixelFormat $width $stride

        for ($y = 0; $y -lt $height; $y += 512) {
            $stream.Position = [int64]$y * [int64]$stride
            $stream.Write($row, 0, $row.Length)
        }

        $stream.Position = $length - 1
        $stream.WriteByte(255)
    }
    finally {
        $stream.Dispose()
    }
}

function Wait-Until([string]$description, [scriptblock]$condition, [int]$timeoutSeconds = 30) {
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

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::Now -lt $deadline)

    if ($lastError) {
        throw "$description timed out. Last error: $lastError"
    }

    throw "$description timed out."
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object RawBufferLargeFileBackedNative+RECT
    [RawBufferLargeFileBackedNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
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

function Minimize-OtherWindows([int]$exceptPid) {
    Get-Process | Where-Object {
        $_.MainWindowHandle -ne 0 -and $_.Id -ne $exceptPid
    } | ForEach-Object {
        [RawBufferLargeFileBackedNative]::ShowWindow($_.MainWindowHandle, 6) | Out-Null
    }
}

function Measure-Capture([string]$path) {
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $nonDark = 0
        $samples = 0
        for ($y = 80; $y -lt ($bitmap.Height - 40); $y += 8) {
            for ($x = 280; $x -lt ($bitmap.Width - 280); $x += 8) {
                $c = $bitmap.GetPixel($x, $y)
                if ((($c.R + $c.G + $c.B) / 3) -gt 20) {
                    $nonDark++
                }

                $samples++
            }
        }

        [Math]::Round($nonDark / [double][Math]::Max($samples, 1), 4)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-Root([IntPtr]$hwnd) {
    [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
}

function Get-ElementName([System.Windows.Automation.AutomationElement]$root, [string]$automationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
    if ($null -eq $element) {
        throw "Automation element not found: $automationId"
    }

    $element.Current.Name
}

New-SparsePatternRaw $rawPath 100000 100000 $stride $PixelFormat

$viewerExe = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.Wpf\$Configuration\$ViewerFramework\RawBufferVisualizer.Wpf.exe"
if (-not (Test-Path $viewerExe)) {
    throw "Viewer exe not found: $viewerExe"
}

$process = Start-Process -FilePath $viewerExe -ArgumentList @($metadataPath) -PassThru
try {
    $hwnd = Wait-Until "viewer window" {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) { $process.MainWindowHandle } else { $null }
    }

    Minimize-OtherWindows $process.Id
    [RawBufferLargeFileBackedNative]::ShowWindow($hwnd, 9) | Out-Null
    [RawBufferLargeFileBackedNative]::SetWindowPos($hwnd, [RawBufferLargeFileBackedNative]::HWND_TOPMOST, 8, 8, 1280, 800, 0x0040) | Out-Null
    [RawBufferLargeFileBackedNative]::BringWindowToTop($hwnd) | Out-Null
    [RawBufferLargeFileBackedNative]::SetForegroundWindow($hwnd) | Out-Null

    Wait-Until "100K status" {
        $status = Get-ElementName (Get-Root $hwnd) "StatusText"
        $status -match "100000 x 100000, $PixelFormat" -and $status -match "tiles 400"
    } | Out-Null

    $capturePath = Join-Path $outputRoot $CaptureFileName
    Wait-Until "nonblank large-image capture" {
        Start-Sleep -Milliseconds 500
        Capture-Window $hwnd $capturePath
        $ratio = Measure-Capture $capturePath
        if ($ratio -gt 0.02) { $ratio } else { $null }
    } | Out-Null

    [pscustomobject]@{
        Status = Get-ElementName (Get-Root $hwnd) "StatusText"
        Capture = $capturePath
        NonDarkRatio = Measure-Capture $capturePath
        RawLength = (Get-Item -LiteralPath $rawPath).Length
    } | Format-List
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
}

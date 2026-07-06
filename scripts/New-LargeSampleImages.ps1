param(
    [string[]]$Sizes = @("100000", "200000"),
    [string[]]$PixelFormat = @("Mono8"),
    [string]$OutputDir = "artifacts\large-samples",
    [int]$RowInterval = 512,
    [string]$Configuration = "Debug",
    [string]$ViewerFramework = "net472",
    [switch]$Dense,
    [int]$DenseBlockRows = 64,
    [switch]$BuildViewer,
    [switch]$Open
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$outputRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$requestedSizes = New-Object System.Collections.Generic.List[int]
foreach ($sizeArgument in $Sizes) {
    foreach ($sizeText in ($sizeArgument -split ",")) {
        if ([string]::IsNullOrWhiteSpace($sizeText)) {
            continue
        }

        $size = 0
        if (-not [int]::TryParse($sizeText.Trim(), [ref]$size) -or $size -le 0) {
            throw "Invalid size: $sizeText"
        }

        $requestedSizes.Add($size) | Out-Null
    }
}

$allowedFormats = @("Mono8", "Mono16", "BGR24", "BGRA32", "Float32")
$requestedFormats = New-Object System.Collections.Generic.List[string]
foreach ($formatArgument in $PixelFormat) {
    foreach ($formatText in ($formatArgument -split ",")) {
        if ([string]::IsNullOrWhiteSpace($formatText)) {
            continue
        }

        $format = $allowedFormats | Where-Object { $_ -ieq $formatText.Trim() } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($format)) {
            throw "Invalid pixel format: $formatText. Supported values: $($allowedFormats -join ', ')"
        }

        $requestedFormats.Add($format) | Out-Null
    }
}

if ($BuildViewer) {
    Push-Location $repoRoot
    try {
        dotnet build .\src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj --configuration $Configuration --framework $ViewerFramework | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Viewer build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not ("RawBufferVisualizerLargeSampleNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RawBufferVisualizerLargeSampleNative {
    public const uint FSCTL_SET_SPARSE = 0x000900C4;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}
'@
}

function Get-LargeSampleDescriptor([int]$width, [int]$height, [string]$pixelFormat) {
    switch ($pixelFormat) {
        "Mono16" {
            return [pscustomobject]@{ Stride = $width * 2; ValidBits = 16 }
        }
        "BGR24" {
            return [pscustomobject]@{ Stride = $width * 3; ValidBits = 8 }
        }
        "BGRA32" {
            return [pscustomobject]@{ Stride = $width * 4; ValidBits = 8 }
        }
        "Float32" {
            return [pscustomobject]@{ Stride = $width * 4; ValidBits = 32 }
        }
        default {
            return [pscustomobject]@{ Stride = $width; ValidBits = 8 }
        }
    }
}

function New-LargeSampleRow([int]$width, [int]$stride, [string]$pixelFormat) {
    $row = New-Object byte[] $stride
    switch ($pixelFormat) {
        "Mono16" {
            for ($x = 0; $x -lt $width; $x++) {
                $value = [uint16](512 + (([int][Math]::Floor($x / 128.0)) % 60000))
                $offset = $x * 2
                $row[$offset] = [byte]($value -band 0xFF)
                $row[$offset + 1] = [byte](($value -shr 8) -band 0xFF)
            }
        }
        "BGR24" {
            for ($x = 0; $x -lt $width; $x++) {
                $offset = $x * 3
                $blue = [byte]($x * 255 / [Math]::Max($width - 1, 1))
                $green = [byte](32 + (([int][Math]::Floor($x / 512.0)) % 224))
                $red = [byte](255 - $blue)
                $row[$offset] = $blue
                $row[$offset + 1] = $green
                $row[$offset + 2] = $red
            }
        }
        "BGRA32" {
            for ($x = 0; $x -lt $width; $x++) {
                $offset = $x * 4
                $blue = [byte]($x * 255 / [Math]::Max($width - 1, 1))
                $green = [byte](32 + (([int][Math]::Floor($x / 512.0)) % 224))
                $red = [byte](255 - $blue)
                $row[$offset] = $blue
                $row[$offset + 1] = $green
                $row[$offset + 2] = $red
                $row[$offset + 3] = 255
            }
        }
        "Float32" {
            for ($x = 0; $x -lt $width; $x++) {
                $value = [float]([Math]::Sin($x * 0.002) + [Math]::Cos($x * 0.0005))
                [Buffer]::BlockCopy([BitConverter]::GetBytes($value), 0, $row, $x * 4, 4)
            }
        }
        default {
            for ($x = 0; $x -lt $width; $x++) {
                $row[$x] = [byte](32 + (([int][Math]::Floor($x / 512.0)) % 224))
            }
        }
    }

    return ,$row
}

function New-SparseRawFile([string]$path, [int]$width, [int]$height, [int]$stride, [string]$pixelFormat, [int]$rowInterval) {
    $length = [int64]$stride * [int64]$height
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::ReadWrite)
    try {
        $bytesReturned = 0
        $isSparse = [RawBufferVisualizerLargeSampleNative]::DeviceIoControl(
            $stream.SafeFileHandle.DangerousGetHandle(),
            [RawBufferVisualizerLargeSampleNative]::FSCTL_SET_SPARSE,
            [IntPtr]::Zero,
            0,
            [IntPtr]::Zero,
            0,
            [ref]$bytesReturned,
            [IntPtr]::Zero)

        if (-not $isSparse) {
            throw "Sparse files are not supported here. Use an NTFS volume and rerun the script."
        }

        $stream.SetLength($length)
        $row = New-LargeSampleRow $width $stride $pixelFormat

        for ($y = 0; $y -lt $height; $y += $rowInterval) {
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

function New-DenseRawFile([string]$path, [int]$width, [int]$height, [int]$stride, [string]$pixelFormat, [int]$blockRows) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }

    $rowsPerBlock = [Math]::Max(1, [Math]::Min($blockRows, $height))
    $row = New-LargeSampleRow $width $stride $pixelFormat
    $block = New-Object byte[] ([int64]$stride * [int64]$rowsPerBlock)
    for ($i = 0; $i -lt $rowsPerBlock; $i++) {
        [Buffer]::BlockCopy($row, 0, $block, $i * $stride, $stride)
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
    try {
        $remainingRows = $height
        while ($remainingRows -gt 0) {
            $writeRows = [Math]::Min($rowsPerBlock, $remainingRows)
            $stream.Write($block, 0, $writeRows * $stride)
            $remainingRows -= $writeRows
        }
    }
    finally {
        $stream.Dispose()
    }
}

$created = New-Object System.Collections.Generic.List[object]

foreach ($size in $requestedSizes) {
    foreach ($format in $requestedFormats) {
        $descriptor = Get-LargeSampleDescriptor $size $size $format
        $mode = if ($Dense) { "dense" } else { "sparse" }
        $sampleName = "large-$($size)x$($size)-$($format.ToLowerInvariant())-$mode"
        $rawPath = Join-Path $outputRoot "$sampleName.raw"
        $metadataPath = Join-Path $outputRoot "$sampleName.rbuf.json"

        if ($Dense) {
            New-DenseRawFile $rawPath $size $size $descriptor.Stride $format $DenseBlockRows
        }
        else {
            New-SparseRawFile $rawPath $size $size $descriptor.Stride $format $RowInterval
        }

        $metadata = [ordered]@{
            rawFile = "$sampleName.raw"
            width = $size
            height = $size
            stride = $descriptor.Stride
            pixelFormat = $format
            validBits = $descriptor.ValidBits
            byteOrder = "LittleEndian"
        } | ConvertTo-Json
        Set-Content -LiteralPath $metadataPath -Value $metadata -Encoding UTF8

        $created.Add([pscustomobject]@{
            Metadata = $metadataPath
            Raw = $rawPath
            PixelFormat = $format
            Width = $size
            Height = $size
            Mode = $mode
            LogicalBytes = (Get-Item -LiteralPath $rawPath).Length
        }) | Out-Null
    }
}

$created | Format-Table -AutoSize

if ($Open -and $created.Count -gt 0) {
    $viewerExe = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.Wpf\$Configuration\$ViewerFramework\RawBufferVisualizer.Wpf.exe"
    if (-not (Test-Path -LiteralPath $viewerExe)) {
        throw "Viewer exe not found: $viewerExe. Rerun with -BuildViewer."
    }

    Start-Process -FilePath $viewerExe -ArgumentList @($created[0].Metadata)
}

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [int]$Iterations = 240,
    [int]$WarmupIterations = 16,
    [int]$BatchSize = 8,
    [int]$Width = 2048,
    [int]$Height = 2048,
    [int]$CheckpointEveryBatches = 2,
    [int]$MaxManagedGrowthMiB = 32,
    [int]$MaxPrivateGrowthMiB = 192,
    [int]$MaxWorkingSetGrowthMiB = 192,
    [int]$MaxHandleGrowth = 32,
    [int]$MaxGdiGrowth = 8,
    [int]$MaxUserGrowth = 8,
    [string]$OutputDir = "artifacts\perf\docked-memory-soak",
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
        "-Iterations",
        $Iterations,
        "-WarmupIterations",
        $WarmupIterations,
        "-BatchSize",
        $BatchSize,
        "-Width",
        $Width,
        "-Height",
        $Height,
        "-CheckpointEveryBatches",
        $CheckpointEveryBatches,
        "-MaxManagedGrowthMiB",
        $MaxManagedGrowthMiB,
        "-MaxPrivateGrowthMiB",
        $MaxPrivateGrowthMiB,
        "-MaxWorkingSetGrowthMiB",
        $MaxWorkingSetGrowthMiB,
        "-MaxHandleGrowth",
        $MaxHandleGrowth,
        "-MaxGdiGrowth",
        $MaxGdiGrowth,
        "-MaxUserGrowth",
        $MaxUserGrowth,
        "-OutputDir",
        $OutputDir
    )

    if ($NoBuild) {
        $arguments += "-NoBuild"
    }

    & powershell.exe @arguments
    exit $LASTEXITCODE
}

if ($Iterations -le 0 -or $WarmupIterations -lt 0 -or $BatchSize -le 0) {
    throw "Iterations and BatchSize must be positive; WarmupIterations cannot be negative."
}

if (($Iterations % $BatchSize) -ne 0 -or ($WarmupIterations % $BatchSize) -ne 0) {
    throw "Iterations and WarmupIterations must be divisible by BatchSize."
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (-not $NoBuild) {
    dotnet build .\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj --configuration $Configuration --framework $Framework | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

$bin = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework"
if (-not (Test-Path -LiteralPath $bin)) {
    throw "Viewer host output not found: $bin"
}

$outputRoot = Join-Path $repoRoot $OutputDir
$sampleRoot = Join-Path $outputRoot "samples"
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
$resultPath = Join-Path $outputRoot "docked-memory-soak.json"
$rawPath = Join-Path $sampleRoot "soak-$Width-x-$Height-mono8.raw"

function New-Mono8Sample([string]$path, [int]$width, [int]$height) {
    $expectedLength = [int64]$width * [int64]$height
    $existing = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
    if ($existing -and $existing.Length -eq $expectedLength) {
        return
    }

    $row = New-Object byte[] $width
    for ($x = 0; $x -lt $width; $x++) {
        $row[$x] = [byte](32 + ($x % 224))
    }

    $stream = [IO.File]::Open($path, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        for ($y = 0; $y -lt $height; $y++) {
            $stream.Write($row, 0, $row.Length)
        }
    }
    finally {
        $stream.Dispose()
    }
}

New-Mono8Sample $rawPath $Width $Height

Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,WindowsFormsIntegration,System.Windows.Forms,System.Drawing
if (-not ("RawBufferMemorySoakNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RawBufferMemorySoakNative
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr process, int flags);
}
'@
}

$vsPublicAssemblies = @(
    (Join-Path $env:ProgramFiles "Microsoft Visual Studio\2022\Community\Common7\IDE\PublicAssemblies"),
    (Join-Path $env:ProgramFiles "Microsoft Visual Studio\18\Community\Common7\IDE\PublicAssemblies")
)
$script:bin = $bin
$script:vsPublicAssemblies = $vsPublicAssemblies

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = ($args.Name -split ",")[0] + ".dll"
    foreach ($directory in @($script:bin) + $script:vsPublicAssemblies) {
        if ([string]::IsNullOrWhiteSpace($directory)) {
            continue
        }

        $candidate = Join-Path $directory $name
        if (Test-Path -LiteralPath $candidate) {
            return [Reflection.Assembly]::LoadFrom($candidate)
        }
    }

    return $null
})

foreach ($assemblyName in @(
    "RawBufferVisualizer.Core.dll",
    "RawBufferVisualizer.Sdk.dll",
    "SharpGL.dll",
    "SharpGL.SceneGraph.dll",
    "SharpGL.WinForms.dll",
    "RawBufferVisualizer.OpenGlCanvas.dll",
    "RawBufferVisualizer.VisualStudio.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $bin $assemblyName)) | Out-Null
}

$vssdkAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin "RawBufferVisualizer.VisualStudio.Vssdk.dll"))
$control = $vssdkAssembly.CreateInstance("RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl")
if ($null -eq $control) {
    throw "RawBufferToolWindowControl could not be created."
}

function Wait-Dispatcher([int]$milliseconds) {
    $frame = New-Object Windows.Threading.DispatcherFrame
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({
        $timer.Stop()
        $frame.Continue = $false
    })
    $timer.Start()
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}

function Force-GarbageCollection {
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
    Wait-Dispatcher 100
}

function Get-MemorySnapshot([string]$name, [int]$completedIterations) {
    Force-GarbageCollection
    $process = [Diagnostics.Process]::GetCurrentProcess()
    $process.Refresh()
    [pscustomobject]@{
        Name = $name
        CompletedIterations = $completedIterations
        TimestampUtc = [DateTime]::UtcNow.ToString("o")
        ManagedBytes = [GC]::GetTotalMemory($false)
        WorkingSetBytes = $process.WorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
        HandleCount = $process.HandleCount
        GdiObjects = [RawBufferMemorySoakNative]::GetGuiResources($process.Handle, 0)
        UserObjects = [RawBufferMemorySoakNative]::GetGuiResources($process.Handle, 1)
    }
}

function New-OwnedMetadata([int]$index) {
    $directory = [RawBufferVisualizer.VisualStudio.VisualStudioTempStore]::CreateSnapshotDirectory()
    $metadataPath = Join-Path $directory ("soak-{0:D5}.rbuf.json" -f $index)
    [ordered]@{
        rawFile = $rawPath
        width = $Width
        height = $Height
        stride = $Width
        pixelFormat = "Mono8"
        validBits = 8
        byteOrder = "LittleEndian"
    } | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8

    [pscustomobject]@{
        Directory = $directory
        MetadataPath = $metadataPath
    }
}

$removeSelectedMethod = $control.GetType().GetMethod(
    "RemoveSelectedDocument",
    [Reflection.BindingFlags]"Instance,NonPublic")
$clearMethod = $control.GetType().GetMethod(
    "Clear_Click",
    [Reflection.BindingFlags]"Instance,NonPublic")
if ($null -eq $removeSelectedMethod -or $null -eq $clearMethod) {
    throw "Docked viewer cleanup methods could not be found."
}

$imageList = $control.FindName("ImageList")
$imageView = $control.FindName("OpenGlImageView")
if ($null -eq $imageList -or $null -eq $imageView) {
    throw "Docked viewer controls could not be found."
}

$window = New-Object Windows.Window
$window.Title = "Raw Buffer Visualizer Memory Soak"
$window.Width = 1100
$window.Height = 720
$window.Left = 40
$window.Top = 40
$window.ShowInTaskbar = $false
$window.Content = $control

$createdDirectories = New-Object 'System.Collections.Generic.List[string]'
$checkpoints = New-Object 'System.Collections.Generic.List[object]'
$maxDocumentCount = 0
$renderedBatchCount = 0
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$failure = $null

function Invoke-SoakBatch([int]$startIndex, [int]$count, [bool]$useClear) {
    $imageView.ResetRenderStats()
    for ($offset = 0; $offset -lt $count; $offset++) {
        $owned = New-OwnedMetadata ($startIndex + $offset)
        $createdDirectories.Add($owned.Directory)
        $control.OpenPath($owned.MetadataPath)
        Wait-Dispatcher 20
    }

    $script:maxDocumentCount = [Math]::Max($script:maxDocumentCount, $imageList.Items.Count)
    $imageView.SetZoomScale(1.5)
    $imageView.PanByImagePixels(96, 64)
    Wait-Dispatcher 200
    $stats = $imageView.GetRenderStatsSnapshot()
    if ($stats.TextureUploadCount -gt 0) {
        $script:renderedBatchCount++
    }

    if ($useClear) {
        $clearMethod.Invoke($control, [object[]]@($null, [Windows.RoutedEventArgs]::new())) | Out-Null
    }
    else {
        while ($imageList.Items.Count -gt 0) {
            $imageList.SelectedIndex = $imageList.Items.Count - 1
            $removeSelectedMethod.Invoke($control, @()) | Out-Null
        }
    }

    Wait-Dispatcher 50
    if ($imageList.Items.Count -ne 0) {
        throw "Image documents remained after cleanup: $($imageList.Items.Count)."
    }
}

try {
    $window.Show()
    Wait-Dispatcher 500

    for ($index = 0; $index -lt $WarmupIterations; $index += $BatchSize) {
        Invoke-SoakBatch $index $BatchSize ((($index / $BatchSize) % 2) -eq 1)
    }

    $baseline = Get-MemorySnapshot "baseline-after-warmup" 0
    $checkpoints.Add($baseline)

    $batchNumber = 0
    for ($index = 0; $index -lt $Iterations; $index += $BatchSize) {
        $batchNumber++
        Invoke-SoakBatch ($WarmupIterations + $index) $BatchSize (($batchNumber % 2) -eq 0)
        if (($batchNumber % $CheckpointEveryBatches) -eq 0 -or ($index + $BatchSize) -eq $Iterations) {
            $checkpoints.Add((Get-MemorySnapshot ("batch-{0:D3}" -f $batchNumber) ($index + $BatchSize)))
        }
    }

    $final = Get-MemorySnapshot "final-before-close" $Iterations
    $checkpoints.Add($final)

    $remainingDirectories = @($createdDirectories | Where-Object { Test-Path -LiteralPath $_ })
    $managedGrowth = $final.ManagedBytes - $baseline.ManagedBytes
    $workingSetGrowth = $final.WorkingSetBytes - $baseline.WorkingSetBytes
    $privateGrowth = $final.PrivateBytes - $baseline.PrivateBytes
    $handleGrowth = $final.HandleCount - $baseline.HandleCount
    $gdiGrowth = $final.GdiObjects - $baseline.GdiObjects
    $userGrowth = $final.UserObjects - $baseline.UserObjects

    $limits = [ordered]@{
        ManagedGrowthBytes = [int64]$MaxManagedGrowthMiB * 1MB
        WorkingSetGrowthBytes = [int64]$MaxWorkingSetGrowthMiB * 1MB
        PrivateGrowthBytes = [int64]$MaxPrivateGrowthMiB * 1MB
        HandleGrowth = $MaxHandleGrowth
        GdiGrowth = $MaxGdiGrowth
        UserGrowth = $MaxUserGrowth
        RemainingTempDirectories = 0
    }

    $violations = New-Object 'System.Collections.Generic.List[string]'
    if ($managedGrowth -gt $limits.ManagedGrowthBytes) { $violations.Add("Managed memory growth exceeded the limit.") }
    if ($workingSetGrowth -gt $limits.WorkingSetGrowthBytes) { $violations.Add("Working set growth exceeded the limit.") }
    if ($privateGrowth -gt $limits.PrivateGrowthBytes) { $violations.Add("Private memory growth exceeded the limit.") }
    if ($handleGrowth -gt $limits.HandleGrowth) { $violations.Add("Process handle growth exceeded the limit.") }
    if ($gdiGrowth -gt $limits.GdiGrowth) { $violations.Add("GDI object growth exceeded the limit.") }
    if ($userGrowth -gt $limits.UserGrowth) { $violations.Add("USER object growth exceeded the limit.") }
    if ($remainingDirectories.Count -gt 0) { $violations.Add("Owned snapshot directories remained after cleanup.") }
    if ($renderedBatchCount -eq 0) { $violations.Add("No OpenGL texture upload was observed during the soak.") }

    $stopwatch.Stop()
    $result = [ordered]@{
        Passed = $violations.Count -eq 0
        Configuration = $Configuration
        Framework = $Framework
        Iterations = $Iterations
        WarmupIterations = $WarmupIterations
        BatchSize = $BatchSize
        Image = [ordered]@{
            Width = $Width
            Height = $Height
            PixelFormat = "Mono8"
            Bytes = [int64]$Width * [int64]$Height
        }
        DurationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        MaxDocumentCount = $maxDocumentCount
        RenderedBatchCount = $renderedBatchCount
        CleanupModes = @("Delete selected item", "Clear all")
        Baseline = $baseline
        Final = $final
        Growth = [ordered]@{
            ManagedBytes = $managedGrowth
            WorkingSetBytes = $workingSetGrowth
            PrivateBytes = $privateGrowth
            HandleCount = $handleGrowth
            GdiObjects = $gdiGrowth
            UserObjects = $userGrowth
            RemainingTempDirectories = $remainingDirectories.Count
        }
        Limits = $limits
        Violations = $violations.ToArray()
        Checkpoints = $checkpoints.ToArray()
    }

    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    $result | ConvertTo-Json -Depth 5 | Write-Host
    if (-not $result.Passed) {
        throw "Docked memory soak failed. See $resultPath"
    }
}
catch {
    $failure = $_
    throw
}
finally {
    try {
        if ($imageList.Items.Count -gt 0) {
            $clearMethod.Invoke($control, [object[]]@($null, [Windows.RoutedEventArgs]::new())) | Out-Null
        }
    }
    catch {
        if ($null -eq $failure) {
            Write-Warning "Final viewer cleanup failed: $($_.Exception.Message)"
        }
    }

    $window.Close()
    Wait-Dispatcher 200
    foreach ($directory in $createdDirectories) {
        if (Test-Path -LiteralPath $directory) {
            [RawBufferVisualizer.VisualStudio.VisualStudioTempStore]::TryDeleteDirectory($directory)
        }
    }
}

Write-Host "Docked memory soak passed. Results: $resultPath"

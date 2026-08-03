[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [int]$MaximumDimension = 512,
    [string[]]$MetadataPaths = @(
        "artifacts\large-file-backed\huge-100000x100000-mono8-dense.rbuf.json",
        "artifacts\large-file-backed\huge-200000x200000-mono8-dense.rbuf.json"
    ),
    [string]$OutputPath = "artifacts\perf\sampled-preview\sampled-preview-performance.json",
    [int]$MaximumElapsedMs = 5000,
    [ValidateRange(1, 10)]
    [int]$RepeatCount = 1,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (-not $NoBuild) {
    dotnet build .\src\RawBufferVisualizer.VisualStudio.ObjectSource\RawBufferVisualizer.VisualStudio.ObjectSource.csproj --configuration $Configuration --framework $Framework | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Object source build failed with exit code $LASTEXITCODE."
    }
}

$assemblyRoot = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.ObjectSource\$Configuration\$Framework"
$coreAssemblyPath = Join-Path $assemblyRoot "RawBufferVisualizer.Core.dll"
$objectSourceAssemblyPath = Join-Path $assemblyRoot "RawBufferVisualizer.VisualStudio.ObjectSource.dll"
if (-not (Test-Path -LiteralPath $coreAssemblyPath) -or -not (Test-Path -LiteralPath $objectSourceAssemblyPath)) {
    throw "Object source output was not found under '$assemblyRoot'. Build the project first or omit -NoBuild."
}

[Reflection.Assembly]::LoadFrom($coreAssemblyPath) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $assemblyRoot "RawBufferVisualizer.Sdk.dll")) | Out-Null
[Reflection.Assembly]::LoadFrom($objectSourceAssemblyPath) | Out-Null

if (-not ("SampledPreviewPerformanceProbe" -as [type])) {
    $probeSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

public sealed class SampledPreviewPerformanceResult
{
    public string RawPath { get; set; }
    public string DriveRoot { get; set; }
    public string DriveType { get; set; }
    public DateTime SourceLastWriteTimeUtc { get; set; }
    public int SourceRunIndex { get; set; }
    public string CacheState { get; set; }
    public long SourceBytes { get; set; }
    public bool IsSparse { get; set; }
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public int PreviewWidth { get; set; }
    public int PreviewHeight { get; set; }
    public long PreviewBytes { get; set; }
    public long MapAndPreviewMilliseconds { get; set; }
    public long MapCreationMilliseconds { get; set; }
    public long ViewCreationMilliseconds { get; set; }
    public long PointerAcquisitionMilliseconds { get; set; }
    public long PreviewMilliseconds { get; set; }
    public long CleanupMilliseconds { get; set; }
    public long WorkingSetDeltaBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public byte MinimumSample { get; set; }
    public byte MaximumSample { get; set; }
    public long SampleChecksum { get; set; }
}

public static unsafe class SampledPreviewPerformanceProbe
{
    public static SampledPreviewPerformanceResult Run(
        string rawPath,
        int width,
        int height,
        int stride,
        string pixelFormat,
        int validBits,
        string byteOrder,
        int maximumDimension,
        int sourceRunIndex)
    {
        var file = new FileInfo(rawPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Raw payload was not found.", rawPath);
        }

        var descriptor = new RawImageDescriptor
        {
            Width = width,
            Height = height,
            Stride = stride,
            PixelFormat = (RawPixelFormat)Enum.Parse(typeof(RawPixelFormat), pixelFormat, true),
            ValidBits = validBits,
            ByteOrder = (RawByteOrder)Enum.Parse(typeof(RawByteOrder), byteOrder, true)
        };
        var driveRoot = Path.GetPathRoot(file.FullName) ?? string.Empty;
        var driveType = string.Empty;
        try
        {
            driveType = string.IsNullOrWhiteSpace(driveRoot)
                ? string.Empty
                : new DriveInfo(driveRoot).DriveType.ToString();
        }
        catch
        {
            driveType = "Unavailable";
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var workingSetBefore = process.WorkingSet64;
        var totalWatch = Stopwatch.StartNew();
        VisualizerSnapshotTransfer preview;
        long mapCreationMilliseconds;
        long viewCreationMilliseconds;
        long pointerAcquisitionMilliseconds;
        long previewMilliseconds;
        var cleanupWatch = new Stopwatch();

        var mapWatch = Stopwatch.StartNew();
        using (var mappedFile = MemoryMappedFile.CreateFromFile(
                   rawPath,
                   FileMode.Open,
                   null,
                   0,
                   MemoryMappedFileAccess.Read))
        {
            mapWatch.Stop();
            mapCreationMilliseconds = mapWatch.ElapsedMilliseconds;
            var viewWatch = Stopwatch.StartNew();
            using (var accessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                viewWatch.Stop();
                viewCreationMilliseconds = viewWatch.ElapsedMilliseconds;
                byte* pointer = null;
                var pointerWatch = Stopwatch.StartNew();
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                pointerWatch.Stop();
                pointerAcquisitionMilliseconds = pointerWatch.ElapsedMilliseconds;
                try
                {
                    var dataAddress = new IntPtr((long)pointer + accessor.PointerOffset);
                    var previewWatch = Stopwatch.StartNew();
                    preview = VisualizerSampledPreview.Create(
                        dataAddress,
                        file.Length,
                        descriptor,
                        "Dense file-backed benchmark",
                        file.Name,
                        maximumDimension,
                        maximumDimension);
                    previewWatch.Stop();
                    previewMilliseconds = previewWatch.ElapsedMilliseconds;
                }
                finally
                {
                    cleanupWatch.Start();
                    if (pointer != null)
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                    cleanupWatch.Stop();
                }
            }
        }

        totalWatch.Stop();
        process.Refresh();
        var minimum = byte.MaxValue;
        var maximum = byte.MinValue;
        long checksum = 0;
        for (var index = 0; index < preview.Buffer.Length; index += 4)
        {
            var value = preview.Buffer[index];
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            checksum = unchecked((checksum * 31) + value);
        }

        return new SampledPreviewPerformanceResult
        {
            RawPath = file.FullName,
            DriveRoot = driveRoot,
            DriveType = driveType,
            SourceLastWriteTimeUtc = file.LastWriteTimeUtc,
            SourceRunIndex = sourceRunIndex,
            CacheState = sourceRunIndex == 1
                ? "First source access in this benchmark process; operating-system file cache was not controlled."
                : "Repeated source access in this benchmark process; operating-system file cache was not controlled.",
            SourceBytes = file.Length,
            IsSparse = (file.Attributes & FileAttributes.SparseFile) != 0,
            SourceWidth = width,
            SourceHeight = height,
            PreviewWidth = preview.Descriptor.Width,
            PreviewHeight = preview.Descriptor.Height,
            PreviewBytes = preview.Buffer.LongLength,
            MapAndPreviewMilliseconds = totalWatch.ElapsedMilliseconds,
            MapCreationMilliseconds = mapCreationMilliseconds,
            ViewCreationMilliseconds = viewCreationMilliseconds,
            PointerAcquisitionMilliseconds = pointerAcquisitionMilliseconds,
            PreviewMilliseconds = previewMilliseconds,
            CleanupMilliseconds = cleanupWatch.ElapsedMilliseconds,
            WorkingSetDeltaBytes = Math.Max(0, process.WorkingSet64 - workingSetBefore),
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            MinimumSample = minimum,
            MaximumSample = maximum,
            SampleChecksum = checksum
        };
    }
}
'@

    $compilerParameters = New-Object System.CodeDom.Compiler.CompilerParameters
    $compilerParameters.GenerateInMemory = $true
    $compilerParameters.CompilerOptions = "/unsafe"
    $compilerParameters.ReferencedAssemblies.Add([System.Diagnostics.Process].Assembly.Location) | Out-Null
    $compilerParameters.ReferencedAssemblies.Add([System.IO.MemoryMappedFiles.MemoryMappedFile].Assembly.Location) | Out-Null
    $compilerParameters.ReferencedAssemblies.Add($coreAssemblyPath) | Out-Null
    $compilerParameters.ReferencedAssemblies.Add($objectSourceAssemblyPath) | Out-Null
    Add-Type -TypeDefinition $probeSource -Language CSharp -CompilerParameters $compilerParameters
}

$results = @()
foreach ($metadataInput in $MetadataPaths) {
    $metadataPath = (Resolve-Path -LiteralPath (Join-Path $repoRoot $metadataInput)).Path
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $rawPath = Join-Path (Split-Path -Parent $metadataPath) ([string]$metadata.rawFile)
    if (-not (Test-Path -LiteralPath $rawPath)) {
        throw "Raw payload referenced by '$metadataPath' was not found: '$rawPath'."
    }

    for ($sourceRunIndex = 1; $sourceRunIndex -le $RepeatCount; $sourceRunIndex++) {
        $result = [SampledPreviewPerformanceProbe]::Run(
            $rawPath,
            [int]$metadata.width,
            [int]$metadata.height,
            [int]$metadata.stride,
            [string]$metadata.pixelFormat,
            [int]$metadata.validBits,
            [string]$metadata.byteOrder,
            $MaximumDimension,
            $sourceRunIndex)

        if ($result.IsSparse) {
            throw "Performance input must be dense, but '$rawPath' has the SparseFile attribute."
        }
        if ($result.PreviewWidth -gt $MaximumDimension -or $result.PreviewHeight -gt $MaximumDimension) {
            throw "Preview dimensions exceeded the requested maximum for '$rawPath'."
        }
        if ($result.MinimumSample -eq $result.MaximumSample) {
            throw "Preview did not preserve visible source variation for '$rawPath'."
        }
        if ($result.MapAndPreviewMilliseconds -gt $MaximumElapsedMs) {
            throw "Preview generation for '$rawPath' run $sourceRunIndex took $($result.MapAndPreviewMilliseconds) ms, exceeding $MaximumElapsedMs ms."
        }

        $results += $result
        Write-Host ("{0}x{1} run {2}: preview {3}x{4}, map {5} ms, sample {6} ms, total {7} ms, {8:N1} MiB working-set delta" -f `
            $result.SourceWidth,
            $result.SourceHeight,
            $result.SourceRunIndex,
            $result.PreviewWidth,
            $result.PreviewHeight,
            $result.MapCreationMilliseconds,
            $result.PreviewMilliseconds,
            $result.MapAndPreviewMilliseconds,
            ($result.WorkingSetDeltaBytes / 1MB))
    }
}

$resolvedOutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    Join-Path $repoRoot $OutputPath
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutputPath) | Out-Null
$report = [ordered]@{
    generatedAt = [DateTimeOffset]::Now.ToString("o")
    maximumDimension = $MaximumDimension
    maximumElapsedMs = $MaximumElapsedMs
    repeatCount = $RepeatCount
    cacheControl = "The script records first/repeat access but does not flush or claim control of the Windows file cache."
    results = $results
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resolvedOutputPath -Encoding UTF8
$report | ConvertTo-Json -Depth 5 | Write-Output

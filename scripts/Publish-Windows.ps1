[CmdletBinding()]
param(
    [ValidateSet('net10.0-windows', 'net472')]
    [string]$Framework = 'net10.0-windows',

    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [switch]$FrameworkDependent,
    [switch]$NoZip,
    [switch]$SkipSamples
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj'
$publishRoot = Join-Path $repoRoot 'artifacts\publish'

if ($Framework -eq 'net472') {
    $packageName = 'RawBufferVisualizer-net472'
} else {
    $deployment = if ($FrameworkDependent) { 'fd' } else { 'sc' }
    $packageName = "RawBufferVisualizer-$Framework-$Runtime-$deployment"
}

$publishDir = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$publishArgs = @(
    'publish',
    $project,
    '-c', 'Release',
    '-f', $Framework,
    '-o', $publishDir,
    '/nologo'
)

if ($Framework -ne 'net472') {
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    $publishArgs += @('-r', $Runtime, '--self-contained', $selfContained)

    if (-not $FrameworkDependent) {
        $publishArgs += @(
            '-p:PublishSingleFile=true',
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:EnableCompressionInSingleFile=true'
        )
    }
}

Push-Location $repoRoot
try {
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not $SkipSamples) {
        & dotnet run --project (Join-Path $repoRoot 'samples\RawBufferVisualizer.Samples\RawBufferVisualizer.Samples.csproj') --configuration Release --framework net10.0
        if ($LASTEXITCODE -ne 0) {
            throw "sample generation failed with exit code $LASTEXITCODE"
        }

        $sampleSource = Join-Path $repoRoot 'artifacts\samples'
        $sampleTarget = Join-Path $publishDir 'samples'
        if (Test-Path -LiteralPath $sampleTarget) {
            Remove-Item -LiteralPath $sampleTarget -Recurse -Force
        }

        Copy-Item -LiteralPath $sampleSource -Destination $sampleTarget -Recurse
    }
} finally {
    Pop-Location
}

if (-not $NoZip) {
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "zip package was not created: $zipPath"
    }
}

Write-Host "Published: $publishDir"
if (-not $NoZip) {
    Write-Host "Package:   $zipPath"
}

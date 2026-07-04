[CmdletBinding()]
param(
    [string]$Framework = 'net8.0-windows',
    [string]$Configuration = 'Release',
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
$publishRoot = Join-Path $repoRoot 'artifacts\publish'
$packageName = "RawBufferVisualizer-VisualStudioExtensibility-$Framework"
$publishDir = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"
$buildOutput = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\$Framework"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Push-Location $repoRoot
try {
    & dotnet build $project --configuration $Configuration --framework $Framework
    if ($LASTEXITCODE -ne 0) {
        throw "Visual Studio extension build failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath (Join-Path $buildOutput '.vsextension\extension.json'))) {
    throw "Visual Studio extension metadata was not created: $buildOutput\.vsextension\extension.json"
}

Get-ChildItem -LiteralPath $buildOutput -Force | Copy-Item -Destination $publishDir -Recurse -Force

$readmePath = Join-Path $publishDir 'README.txt'
Set-Content -LiteralPath $readmePath -Encoding UTF8 -Value @(
    'Raw Buffer Visualizer Visual Studio extension prototype',
    '',
    'This is the built VisualStudio.Extensibility output for manual validation.',
    'It is not a Marketplace-ready installer yet.',
    '',
    'Manual validation prerequisites:',
    '- Visual Studio 2022 17.9 or newer',
    '- Visual Studio extension development workload',
    '- RawBufferVisualizer.Wpf.exe available on disk',
    '',
    'Set RAW_BUFFER_VISUALIZER_VIEWER to the full RawBufferVisualizer.Wpf.exe path before testing.',
    'Then debug the extension project from Visual Studio and inspect a RawBufferSnapshot variable from DataTip, Watch, Locals, or Autos.'
)

if (-not $NoZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishDir,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "zip package was not created: $zipPath"
    }
}

Write-Host "Published: $publishDir"
if (-not $NoZip) {
    Write-Host "Package:   $zipPath"
}

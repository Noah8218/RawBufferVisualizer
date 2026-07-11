[CmdletBinding()]
param(
    [ValidateSet('net472')]
    [string]$Framework = 'net472',
    [string]$Configuration = 'Release',
    [string]$ViewerFramework = 'net472',
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
$vsixPath = Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.vsix'

function Get-VsixEntryNames {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Message`: $Path"
    }
}

function Assert-DebuggerVisualizerTargetTypes {
    param([string]$ExtensionJsonPath)

    $extension = Get-Content -Raw -LiteralPath $ExtensionJsonPath | ConvertFrom-Json
    foreach ($part in @($extension.parts)) {
        if ($part.contract -ne 'Microsoft.VisualStudio.RpcContracts.DebuggerVisualizers.IDebuggerVisualizerProvider') {
            continue
        }

        foreach ($metadata in @($part.metadata)) {
            foreach ($target in @($metadata.values.targets)) {
                $targetType = [string]$target.targetType
                if ([string]::IsNullOrWhiteSpace($targetType)) {
                    throw "Debugger visualizer targetType is empty in $ExtensionJsonPath"
                }

                if ($targetType.IndexOf(',') -lt 0) {
                    throw "Debugger visualizer targetType must include an assembly name: '$targetType'"
                }

                if ($targetType -like 'Cressem.ImageModel.ImagePtr,*' -and $targetType -notlike '*Version=*') {
                    throw "ImagePtr targetType must be fully assembly-qualified: '$targetType'"
                }
            }
        }
    }
}

function Assert-VssdkReferenceCompatibility {
    param([string]$AssemblyPath)

    $maxThreadingVersion = [Version]'17.9.0.0'
    $references = [Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath).GetReferencedAssemblies()
    $threading = $references | Where-Object { $_.Name -eq 'Microsoft.VisualStudio.Threading' } | Select-Object -First 1
    if ($null -eq $threading) {
        throw "VSSDK package does not reference Microsoft.VisualStudio.Threading: $AssemblyPath"
    }

    if ($threading.Version -gt $maxThreadingVersion) {
        throw "VSSDK package references Microsoft.VisualStudio.Threading $($threading.Version), but Marketplace support starts at Visual Studio 2022 17.9. Build against 17.9-compatible VSSDK references."
    }
}

if ($ViewerFramework -ne 'net472') {
    throw 'The Visual Studio ToolWindow is packaged into the single net472 hybrid VSIX. Use -ViewerFramework net472 or omit it.'
}

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
}
finally {
    Pop-Location
}

$extensionJsonPath = Join-Path $buildOutput '.vsextension\extension.json'
Assert-FileExists -Path $extensionJsonPath -Message 'Visual Studio extension metadata was not created'
Assert-DebuggerVisualizerTargetTypes -ExtensionJsonPath $extensionJsonPath
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef') -Message 'Visual Studio docked ToolWindow pkgdef was not created'
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.dll') -Message 'Visual Studio docked ToolWindow package DLL was not created'
Assert-VssdkReferenceCompatibility -AssemblyPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.dll')
Assert-FileExists -Path $vsixPath -Message 'Visual Studio extension VSIX was not created'

$manifestPath = Join-Path $buildOutput 'extension.vsixmanifest'
Assert-FileExists -Path $manifestPath -Message 'Visual Studio extension manifest was not created'

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$extensionType = $manifest.PackageManifest.Installation.ExtensionType
if ($extensionType -ne 'VSSDK+VisualStudio.Extensibility') {
    throw "Expected a hybrid VSSDK+VisualStudio.Extensibility VSIX, but found '$extensionType'."
}

$entryNames = Get-VsixEntryNames -Path $vsixPath
$requiredEntries = @(
    'extension.vsixmanifest',
    '.vsextension/extension.json',
    'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef',
    'RawBufferVisualizer.VisualStudio.Vssdk.dll',
    'RawBufferVisualizer.OpenGlCanvas.dll',
    'SharpGL.dll',
    'SharpGL.WinForms.dll'
)

foreach ($entryName in $requiredEntries) {
    if ($entryNames -notcontains $entryName) {
        throw "VSIX is missing required entry: $entryName"
    }
}

Get-ChildItem -LiteralPath $buildOutput -Force | Copy-Item -Destination $publishDir -Recurse -Force

$readmePath = Join-Path $publishDir 'README.txt'
Set-Content -LiteralPath $readmePath -Encoding UTF8 -Value @(
    'Raw Buffer Visualizer Visual Studio extension prototype',
    '',
    'Install this single VSIX:',
    'RawBufferVisualizer.VisualStudio.Extensibility.vsix',
    '',
    'The VSIX contains both parts required for normal operation:',
    '- Visual Studio debugger visualizers for RawBufferSnapshot, RawBufferView, Bitmap, OpenCvSharp Mat, Emgu CV Mat, and supported image collections',
    '- In-process Visual Studio ToolWindow used as the docked image viewer',
    '',
    'Manual validation prerequisites:',
    '- Visual Studio 2022 17.9 or newer',
    '- Visual Studio extension development workload',
    '',
    'Close Visual Studio before installing, then restart Visual Studio before debugger testing.'
)

if (-not $NoZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishDir,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    Assert-FileExists -Path $zipPath -Message 'zip package was not created'
}

Write-Host "Published: $publishDir"
if (-not $NoZip) {
    Write-Host "Package:   $zipPath"
}

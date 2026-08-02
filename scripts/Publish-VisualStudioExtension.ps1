[CmdletBinding()]
param(
    [ValidateSet('net472')]
    [string]$Framework = 'net472',
    [string]$Configuration = 'Release',
    [string]$ViewerFramework = 'net472',
    [string]$PublishRoot = '',
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    $publishRoot = Join-Path $repoRoot 'artifacts\publish'
}
else {
    $publishRoot = [IO.Path]::GetFullPath($PublishRoot)
}
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
    $providerCount = 0
    foreach ($part in @($extension.parts)) {
        if ($part.contract -ne 'Microsoft.VisualStudio.RpcContracts.DebuggerVisualizers.IDebuggerVisualizerProvider') {
            continue
        }

        $providerCount++
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

    if ($providerCount -eq 0) {
        throw "VSIX contains no debugger visualizer providers: $ExtensionJsonPath"
    }
}

function Assert-ModernDebuggerVisualizerProvidersPresent {
    param([string]$ExtensionJsonPath)

    $extensionJson = Get-Content -Raw -LiteralPath $ExtensionJsonPath
    foreach ($provider in @(
        'RawBufferSnapshotDebuggerVisualizerProvider',
        'RawBufferViewDebuggerVisualizerProvider',
        'BitmapDebuggerVisualizerProvider',
        'OpenCvSharpMatDebuggerVisualizerProvider',
        'EmguCvMatDebuggerVisualizerProvider',
        'ImagePtrDebuggerVisualizerProvider',
        'ImageCollectionDebuggerVisualizerProvider'
    )) {
        if ($extensionJson -notmatch [regex]::Escape($provider)) {
            throw "Required Marketplace debugger visualizer is missing: $provider"
        }
    }

    if ($extensionJson -notmatch [regex]::Escape('IDebuggerVisualizerProvider')) {
        throw 'Marketplace package contains no debugger visualizer contract.'
    }

    foreach ($targetType in @(
        'OpenCvSharp.Mat, OpenCvSharp, Version=1.0.0.0',
        'OpenCvSharp.Mat, OpenCvSharp, Version=4.0.0.0',
        'Emgu.CV.Mat, Emgu.CV.World, Version=3.4.3.3016',
        'Emgu.CV.Mat, Emgu.CV.World.NetStandard, Version=1.0.0.0',
        'Emgu.CV.Mat, Emgu.CV.Platform.NetStandard, Version=4.5.5.4823',
        'Emgu.CV.Mat, Emgu.CV, Version=4.8.1.5350',
        'Emgu.CV.Mat, Emgu.CV, Version=4.13.0.5924',
        'System.Collections.Generic.List`1, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.Dictionary`2, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.List`1, System.Private.CoreLib',
        'System.Collections.Generic.Dictionary`2, System.Private.CoreLib',
        'OpenCvSharp.Mat[], OpenCvSharp',
        'Emgu.CV.Mat[], Emgu.CV'
    )) {
        if ($extensionJson -notmatch [regex]::Escape($targetType)) {
            throw "Required debugger visualizer target is missing: $targetType"
        }
    }
}

function Assert-ModernCollectionRegistrationsOpen {
    param([string]$SourcePath)

    $source = Get-Content -Raw -LiteralPath $SourcePath
    foreach ($requiredRegistration in @(
        'typeof(List<>)',
        'typeof(Dictionary<,>)',
        'typeof(object[])',
        'System.Collections.Generic.List`1, System.Private.CoreLib',
        'System.Collections.Generic.Dictionary`2, System.Private.CoreLib',
        'OpenCvSharp.Mat[], OpenCvSharp',
        'Emgu.CV.Mat[], Emgu.CV'
    )) {
        if (-not $source.Contains($requiredRegistration)) {
            throw "Required collection visualizer registration is missing: $requiredRegistration"
        }
    }
}

function Assert-VssdkReferenceCompatibility {
    param([string]$AssemblyPath)

    $maxThreadingVersion = [Version]'17.14.0.0'
    $references = [Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath).GetReferencedAssemblies()
    $threading = $references | Where-Object { $_.Name -eq 'Microsoft.VisualStudio.Threading' } | Select-Object -First 1
    if ($null -eq $threading) {
        throw "VSSDK package does not reference Microsoft.VisualStudio.Threading: $AssemblyPath"
    }

    if ($threading.Version -gt $maxThreadingVersion) {
        throw "VSSDK package references Microsoft.VisualStudio.Threading $($threading.Version), but the 1.0.52 Marketplace support floor is Visual Studio 2022 17.14. Build against 17.14-compatible VSSDK references."
    }
}

function Assert-HybridVssdkRegistration {
    param(
        [string]$PkgdefPath,
        [string]$GeneratedManifestPath,
        [string]$SourceManifestPath
    )

    $pkgdef = Get-Content -Raw -LiteralPath $PkgdefPath
    foreach ($requiredRegistration in @(
        '[$RootKey$\Packages\{1977574b-f107-465f-bfd1-5fc022907039}]',
        '"Class"="RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage"',
        '"CodeBase"="$PackageFolder$\RawBufferVisualizer.VisualStudio.Extensibility.dll"',
        '[$RootKey$\Menus]',
        '"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"',
        '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
    )) {
        if (-not $pkgdef.Contains($requiredRegistration)) {
            throw "Hybrid VSSDK pkgdef is missing registration '$requiredRegistration': $PkgdefPath"
        }
    }

    if ($pkgdef.Contains('RawBufferVisualizer.VisualStudio.Vssdk.dll')) {
        throw "The VSSDK package must be owned by the hybrid extension assembly, not the ToolWindow support library: $PkgdefPath"
    }

    if ($pkgdef.Contains('{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}')) {
        throw "The retired 1.0.47/1.0.48 package GUID must not remain in the recovery VSIX registration: $PkgdefPath"
    }

    foreach ($singleRegistration in @(
        '[$RootKey$\Packages\{1977574b-f107-465f-bfd1-5fc022907039}]',
        '"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"',
        '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
    )) {
        if ([regex]::Matches($pkgdef, [regex]::Escape($singleRegistration)).Count -ne 1) {
            throw "Hybrid VSSDK pkgdef must contain exactly one registration '$singleRegistration': $PkgdefPath"
        }
    }

    $generatedManifest = Get-Content -Raw -LiteralPath $GeneratedManifestPath
    if (-not $generatedManifest.Contains('Type="Microsoft.VisualStudio.VsPackage" Path="RawBufferVisualizer.VisualStudio.Extensibility.pkgdef"')) {
        throw "Generated VSIX manifest does not reference the hybrid project's pkgdef: $GeneratedManifestPath"
    }

    $sourceManifest = Get-Content -Raw -LiteralPath $SourceManifestPath
    if (-not $sourceManifest.Contains('Path="|%CurrentProject%;PkgdefProjectOutputGroup|"')) {
        throw "Source VSIX manifest must use the current hybrid project's PkgdefProjectOutputGroup: $SourceManifestPath"
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
    & dotnet build $project --configuration $Configuration --framework $Framework /nodeReuse:false
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
Assert-ModernDebuggerVisualizerProvidersPresent -ExtensionJsonPath $extensionJsonPath
Assert-ModernCollectionRegistrationsOpen -SourcePath (Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\ImageCollectionDebuggerVisualizerProvider.cs')
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.pkgdef') -Message 'Hybrid Visual Studio package registration was not created'
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.dll') -Message 'Visual Studio docked ToolWindow package DLL was not created'
Assert-VssdkReferenceCompatibility -AssemblyPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.dll')
Assert-FileExists -Path $vsixPath -Message 'Visual Studio extension VSIX was not created'

$manifestPath = Join-Path $buildOutput 'extension.vsixmanifest'
Assert-FileExists -Path $manifestPath -Message 'Visual Studio extension manifest was not created'

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$extensionType = $manifest.PackageManifest.Installation.ExtensionType
if ($extensionType -ne 'VSSDK+VisualStudio.Extensibility') {
    throw "Expected a hybrid VSSDK+VisualStudio.Extensibility VSIX, but found '$extensionType'."
}

Assert-HybridVssdkRegistration `
    -PkgdefPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.pkgdef') `
    -GeneratedManifestPath $manifestPath `
    -SourceManifestPath (Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest')

$entryNames = Get-VsixEntryNames -Path $vsixPath
$requiredEntries = @(
    'extension.vsixmanifest',
    '.vsextension/extension.json',
    'RawBufferVisualizer.VisualStudio.Extensibility.pkgdef',
    'RawBufferVisualizer.VisualStudio.Extensibility.dll',
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

if ($entryNames -contains 'RawBufferVisualizer.VisualStudio.Classic.dll') {
    throw 'VSIX must not contain the obsolete Classic debugger visualizer assembly.'
}

if ($entryNames -contains 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef') {
    throw 'VSIX must not contain the obsolete split-project VSSDK pkgdef.'
}

Get-ChildItem -LiteralPath $buildOutput -Force |
    Where-Object { $_.Name -ne 'RawBufferVisualizer.VisualStudio.Classic.dll' } |
    Copy-Item -Destination $publishDir -Recurse -Force

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
    '- Marketplace-installed debugger providers that forward inspected values to the same docked image list',
    '',
    'Manual validation prerequisites:',
    '- Visual Studio 2022 17.14 or newer, or Visual Studio 2026 18.x',
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

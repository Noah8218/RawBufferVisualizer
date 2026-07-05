[CmdletBinding()]
param(
    [string]$Framework = 'net8.0-windows',
    [string]$Configuration = 'Release',
    [string]$ViewerFramework = 'net472',
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
$toolWindowProject = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj'
$publishRoot = Join-Path $repoRoot 'artifacts\publish'
$packageName = "RawBufferVisualizer-VisualStudioExtensibility-$Framework"
$publishDir = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"
$buildOutput = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\$Framework"
$toolWindowVsixPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$ViewerFramework\RawBufferVisualizer.VisualStudio.Vssdk.vsix"

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Value
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

function Add-VssdkAssetToExtensionManifest {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content -Raw -LiteralPath $ManifestPath
    $namespace = $manifest.DocumentElement.NamespaceURI
    $assets = $manifest.GetElementsByTagName('Assets', $namespace) | Select-Object -First 1
    if ($null -eq $assets) {
        $assets = $manifest.CreateElement('Assets', $namespace)
        $manifest.DocumentElement.AppendChild($assets) | Out-Null
    }

    $asset = $manifest.CreateElement('Asset', $namespace)
    $asset.SetAttribute('Type', 'Microsoft.VisualStudio.VsPackage')
    $asset.SetAttribute('Path', 'Vssdk/RawBufferVisualizer.VisualStudio.Vssdk.pkgdef')
    $assets.AppendChild($asset) | Out-Null
    $manifest.Save($ManifestPath)
}

function Add-PkgdefContentType {
    param([string]$ContentTypesPath)

    [xml]$contentTypes = Get-Content -Raw -LiteralPath $ContentTypesPath
    $namespace = $contentTypes.DocumentElement.NamespaceURI
    $exists = $false
    foreach ($node in $contentTypes.GetElementsByTagName('Default', $namespace)) {
        if ($node.Extension -eq 'pkgdef') {
            $exists = $true
        }
    }

    if (-not $exists) {
        $node = $contentTypes.CreateElement('Default', $namespace)
        $node.SetAttribute('Extension', 'pkgdef')
        $node.SetAttribute('ContentType', 'application/octet-stream')
        $contentTypes.DocumentElement.AppendChild($node) | Out-Null
        $contentTypes.Save($ContentTypesPath)
    }
}

function Get-RelativeZipPath {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootWithSlash = $Root.TrimEnd('\') + '\'
    return $Path.Substring($rootWithSlash.Length).Replace('\', '/')
}

function Update-VisualStudioManifestJson {
    param(
        [string]$ExtensionDirectory,
        [string]$AssetDirectory
    )

    $manifestPath = Join-Path $ExtensionDirectory 'manifest.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $known = @{}
    foreach ($file in $manifest.files) {
        $known[$file.fileName] = $true
    }

    $files = @($manifest.files)
    foreach ($file in Get-ChildItem -LiteralPath $AssetDirectory -Recurse -File) {
        $relative = '/' + (Get-RelativeZipPath -Root $ExtensionDirectory -Path $file.FullName)
        if (-not $known.ContainsKey($relative)) {
            $files += [pscustomobject]@{
                fileName = $relative
                sha256 = $null
            }
        }
    }

    $manifest.files = $files
    $size = (Get-ChildItem -LiteralPath $ExtensionDirectory -Recurse -File | Measure-Object -Property Length -Sum).Sum
    $manifest.installSizes.targetDrive = [int64]$size
    Write-Utf8NoBom -Path $manifestPath -Value ($manifest | ConvertTo-Json -Depth 30)
}

function Update-VisualStudioCatalogJson {
    param([string]$ExtensionDirectory)

    $catalogPath = Join-Path $ExtensionDirectory 'catalog.json'
    $catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
    $size = (Get-ChildItem -LiteralPath $ExtensionDirectory -Recurse -File | Measure-Object -Property Length -Sum).Sum
    foreach ($package in $catalog.packages) {
        if ($package.type -eq 'Vsix') {
            $package.installSizes.targetDrive = [int64]$size
        }
    }

    Write-Utf8NoBom -Path $catalogPath -Value ($catalog | ConvertTo-Json -Depth 30)
}

function Compress-DirectoryToZip {
    param(
        [string]$SourceDirectory,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $zip = [System.IO.Compression.ZipFile]::Open($DestinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File) {
            $entryName = Get-RelativeZipPath -Root $SourceDirectory -Path $file.FullName
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Merge-VssdkToolWindowIntoVsix {
    param(
        [string]$ExtensionVsixPath,
        [string]$ToolWindowVsixPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $tempRoot = Join-Path $env:TEMP ('RawBufferVisualizerVsixMerge-' + [Guid]::NewGuid().ToString('N'))
    $extensionDir = Join-Path $tempRoot 'extension'
    $toolWindowDir = Join-Path $tempRoot 'toolwindow'
    $assetDir = Join-Path $extensionDir 'Vssdk'

    try {
        New-Item -ItemType Directory -Force -Path $extensionDir, $toolWindowDir, $assetDir | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ExtensionVsixPath, $extensionDir)
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ToolWindowVsixPath, $toolWindowDir)

        $excluded = @(
            '[Content_Types].xml',
            'catalog.json',
            'extension.vsixmanifest',
            'manifest.json'
        )

        foreach ($file in Get-ChildItem -LiteralPath $toolWindowDir -Recurse -File) {
            $relative = Get-RelativeZipPath -Root $toolWindowDir -Path $file.FullName
            if ($excluded -contains $relative) {
                continue
            }

            $destination = Join-Path $assetDir $relative.Replace('/', '\')
            $destinationDirectory = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $destinationDirectory)) {
                New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
            }

            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }

        $pkgdefPath = Join-Path $assetDir 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef'
        if (-not (Test-Path -LiteralPath $pkgdefPath)) {
            throw "OpenGL ToolWindow pkgdef was not found after merge: $pkgdefPath"
        }

        $pkgdef = Get-Content -Raw -LiteralPath $pkgdefPath
        $pkgdef = $pkgdef.Replace('"CodeBase"="$PackageFolder$\RawBufferVisualizer.VisualStudio.Vssdk.dll"', '"CodeBase"="$PackageFolder$\Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.dll"')
        $pkgdef = $pkgdef.Replace('"$PackageFolder$"=""', '"$PackageFolder$\Vssdk"=""')
        Write-Utf8NoBom -Path $pkgdefPath -Value $pkgdef

        Add-VssdkAssetToExtensionManifest -ManifestPath (Join-Path $extensionDir 'extension.vsixmanifest')
        Add-PkgdefContentType -ContentTypesPath (Join-Path $extensionDir '[Content_Types].xml')
        Update-VisualStudioManifestJson -ExtensionDirectory $extensionDir -AssetDirectory $assetDir
        Update-VisualStudioCatalogJson -ExtensionDirectory $extensionDir

        $mergedPath = Join-Path $tempRoot 'RawBufferVisualizer.VisualStudio.Extensibility.merged.vsix'
        Compress-DirectoryToZip -SourceDirectory $extensionDir -DestinationPath $mergedPath

        Copy-Item -LiteralPath $mergedPath -Destination $ExtensionVsixPath -Force

        $zip = [System.IO.Compression.ZipFile]::OpenRead($ExtensionVsixPath)
        try {
            if ($null -eq ($zip.Entries | Where-Object { $_.FullName -eq 'Vssdk/RawBufferVisualizer.VisualStudio.Vssdk.pkgdef' } | Select-Object -First 1)) {
                throw 'Merged VSIX does not contain the OpenGL ToolWindow pkgdef.'
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
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

    & dotnet msbuild $toolWindowProject '/t:Build;GeneratePkgDef;CreateVsixContainer' /p:Configuration=$Configuration /p:TargetFramework=$ViewerFramework /v:m
    if ($LASTEXITCODE -ne 0) {
        throw "Visual Studio OpenGL ToolWindow VSIX build failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath (Join-Path $buildOutput '.vsextension\extension.json'))) {
    throw "Visual Studio extension metadata was not created: $buildOutput\.vsextension\extension.json"
}

$vsixPath = Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.vsix'
if (-not (Test-Path -LiteralPath $vsixPath)) {
    throw "Visual Studio extension VSIX was not created: $vsixPath"
}

if (-not (Test-Path -LiteralPath $toolWindowVsixPath)) {
    throw "Visual Studio OpenGL ToolWindow VSIX was not created: $toolWindowVsixPath"
}

Merge-VssdkToolWindowIntoVsix -ExtensionVsixPath $vsixPath -ToolWindowVsixPath $toolWindowVsixPath

Get-ChildItem -LiteralPath $buildOutput -Force | Copy-Item -Destination $publishDir -Recurse -Force

$readmePath = Join-Path $publishDir 'README.txt'
Set-Content -LiteralPath $readmePath -Encoding UTF8 -Value @(
    'Raw Buffer Visualizer Visual Studio extension prototype',
    '',
    'This package contains the debugger visualizer and the in-process OpenGL ToolWindow.',
    'Install RawBufferVisualizer.VisualStudio.Extensibility.vsix before testing the debugger visualizer.',
    'It is not a Marketplace-ready package yet.',
    '',
    'Manual validation prerequisites:',
    '- Visual Studio 2022 17.9 or newer',
    '- Visual Studio extension development workload',
    '',
    'The debugger visualizer opens inside a Visual Studio docked tool window and accumulates inspected images in one session.',
    'Debug the sample project from Visual Studio and inspect RawBufferSnapshot, RawBufferView, Bitmap, OpenCvSharp Mat, or Emgu CV Mat variables from DataTip, Watch, Locals, or Autos.'
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

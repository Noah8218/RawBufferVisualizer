[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$VsixPath,
    [Parameter(Mandatory = $true)]
    [string]$Publisher,
    [string]$PersonalAccessToken,
    [string]$InternalName = 'RawBufferVisualizer',
    [string]$Categories = 'other',
    [string]$OverviewPath,
    [string]$RepositoryUrl = 'https://github.com/Noah8218/RawBufferVisualizer',
    [string]$PublishManifestPath,
    [string]$VsixPublisherPath,
    [switch]$Private,
    [switch]$DisableQna,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sourceProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'

if ([string]::IsNullOrWhiteSpace($PublishManifestPath)) {
    $PublishManifestPath = Join-Path $repoRoot 'artifacts\marketplace\vs-publish.json'
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Message`: $Path"
    }
}

function Get-VsixMetadata {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.GetEntry('extension.vsixmanifest')
        if ($null -eq $entry) {
            throw "VSIX is missing extension.vsixmanifest: $Path"
        }

        $stream = $entry.Open()
        try {
            $reader = New-Object System.IO.StreamReader($stream)
            try {
                [xml]$manifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        $identity = $manifest.PackageManifest.Metadata.Identity
        return [pscustomobject]@{
            Id          = [string]$identity.Id
            Version     = [string]$identity.Version
            Publisher   = [string]$identity.Publisher
            DisplayName = [string]$manifest.PackageManifest.Metadata.DisplayName
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Get-SourceReleaseVersion {
    param([string]$ProjectPath)

    Assert-FileExists -Path $ProjectPath -Message 'Visual Studio extension project was not found'
    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
    $version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace([string]$version)) {
        throw "Visual Studio extension project does not declare a release Version: $ProjectPath"
    }

    return [string]$version
}

function ConvertTo-FourPartVersion {
    param(
        [string]$Version,
        [string]$Label
    )

    try {
        $parsed = [version]$Version
    }
    catch {
        throw "$Label is not a valid version: '$Version'"
    }

    $build = if ($parsed.Build -ge 0) { $parsed.Build } else { 0 }
    $revision = if ($parsed.Revision -ge 0) { $parsed.Revision } else { 0 }
    return "$($parsed.Major).$($parsed.Minor).$build.$revision"
}

function Find-VsixPublisher {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path -LiteralPath $ExplicitPath -PathType Leaf) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }

        throw "VsixPublisher.exe was not found: $ExplicitPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:VSIX_PUBLISHER_PATH) -and
        (Test-Path -LiteralPath $env:VSIX_PUBLISHER_PATH -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $env:VSIX_PUBLISHER_PATH).Path
    }

    $nugetRoot = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($nugetRoot)) {
        $nugetRoot = Join-Path $env:USERPROFILE '.nuget\packages'
    }

    $packageRoot = Join-Path $nugetRoot 'microsoft.vssdk.buildtools'
    if (Test-Path -LiteralPath $packageRoot -PathType Container) {
        $packages = Get-ChildItem -LiteralPath $packageRoot -Directory |
            Sort-Object @{ Expression = { [version]$_.Name }; Descending = $true }

        foreach ($package in $packages) {
            $candidate = Join-Path $package.FullName 'tools\vssdk\bin\VsixPublisher.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        $installPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VSSDK -property installationPath
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installPath)) {
            $candidate = Join-Path $installPath 'VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    return $null
}

function Get-AssetFiles {
    param([string]$MarkdownPath)

    $imageRoot = Join-Path $repoRoot 'docs\images'
    if (-not (Test-Path -LiteralPath $imageRoot -PathType Container)) {
        return @()
    }

    $markdown = Get-Content -LiteralPath $MarkdownPath -Raw
    $assetPaths = @([regex]::Matches(
            $markdown,
            'docs/images/(?<path>[A-Za-z0-9._/-]+\.(?:png|jpe?g|gif))',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase) |
        ForEach-Object { $_.Groups['path'].Value.Replace('/', '\') } |
        Sort-Object -Unique)

    return @($assetPaths | ForEach-Object {
            $assetPath = Join-Path $imageRoot $_
            Assert-FileExists -Path $assetPath -Message 'Marketplace Overview image was not found'
            $resolved = (Resolve-Path -LiteralPath $assetPath).Path
            if (-not $resolved.StartsWith($imageRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Marketplace Overview image escapes docs\images: $assetPath"
            }

            $relative = $resolved.Substring($repoRoot.Length + 1).Replace('\', '/')
            [ordered]@{
                pathOnDisk = $resolved
                targetPath = $relative
            }
        })
}

Assert-FileExists -Path $VsixPath -Message 'VSIX payload was not found'
$VsixPath = (Resolve-Path -LiteralPath $VsixPath).Path
$metadata = Get-VsixMetadata -Path $VsixPath
$sourceVersion = Get-SourceReleaseVersion -ProjectPath $sourceProjectPath
$expectedManifestVersion = ConvertTo-FourPartVersion -Version $sourceVersion -Label 'Source release version'
$actualManifestVersion = ConvertTo-FourPartVersion -Version $metadata.Version -Label 'VSIX manifest version'
if ($actualManifestVersion -ne $expectedManifestVersion) {
    throw "VSIX manifest version $actualManifestVersion does not match current source release $sourceVersion ($expectedManifestVersion). Select the exact qualified candidate explicitly; do not publish a preserved or stale VSIX. Path: $VsixPath"
}

$parsedVersion = [version]$metadata.Version
$packageVersion = "$($parsedVersion.Major).$($parsedVersion.Minor).$($parsedVersion.Build)"
if ([string]::IsNullOrWhiteSpace($OverviewPath)) {
    $OverviewPath = Join-Path $repoRoot "docs\marketplace-overview-$packageVersion.md"
}
Assert-FileExists -Path $OverviewPath -Message 'Marketplace overview file was not found'

& (Join-Path $PSScriptRoot 'Test-ReleaseCommunication.ps1') -ExpectedVersion $metadata.Version

if ($Publisher -match '\s') {
    throw "Publisher must be the Marketplace publisher ID, not the display name: '$Publisher'"
}

if ($InternalName -notmatch '^[^\s-]+$') {
    throw "InternalName must not contain whitespace or hyphen: '$InternalName'"
}

$validCategories = @(
    'ajax',
    'build',
    'coding',
    'connected services',
    'data',
    'database',
    'documentation',
    'extension sdk',
    'framework and libraries',
    'lightswitch',
    'lightswitch controls',
    'lightswitch templates',
    'modelling',
    'office',
    'other',
    'other templates',
    'performance',
    'process templates',
    'programming languages',
    'reporting',
    'scaffolding',
    'security',
    'services',
    'setup and deployment',
    'sharepoint',
    'sharepoint controls',
    'sharepoint templates',
    'silverlight controls',
    'source control',
    'start pages',
    'team development',
    'testing',
    'theme',
    'visual studio extensions',
    'wcf',
    'web',
    'windows forms templates',
    'windows forms controls',
    'workflow',
    'wpf templates',
    'wpf controls',
    'xna'
)

$categoryList = @($Categories -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
if ($categoryList.Count -eq 0 -or $categoryList.Count -gt 3) {
    throw 'Specify between 1 and 3 Marketplace categories.'
}

foreach ($category in $categoryList) {
    if ($validCategories -notcontains $category) {
        throw "Unsupported VsixPublisher category '$category'. Use one of: $($validCategories -join ', ')"
    }
}

Write-Host "VSIX: $($metadata.DisplayName) $($metadata.Version) [$($metadata.Id)]"

$manifest = [ordered]@{
    '$schema'      = 'http://json.schemastore.org/vsix-publish'
    categories     = $categoryList
    identity       = [ordered]@{
        internalName = $InternalName
    }
    overview       = $OverviewPath
    priceCategory  = 'free'
    publisher      = $Publisher
    private        = [bool]$Private
    qna            = -not [bool]$DisableQna
    repo           = $RepositoryUrl
}

$assetFiles = Get-AssetFiles -MarkdownPath $OverviewPath
if ($assetFiles.Count -gt 0) {
    $manifest.assetFiles = $assetFiles
}

$manifestDirectory = Split-Path -Parent $PublishManifestPath
New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $PublishManifestPath -Encoding UTF8
Write-Host "Publish manifest: $PublishManifestPath"

$publisherExe = Find-VsixPublisher -ExplicitPath $VsixPublisherPath
if ([string]::IsNullOrWhiteSpace($publisherExe)) {
    if ($DryRun) {
        Write-Warning 'VsixPublisher.exe was not found. Dry run completed without publishing.'
        return
    }

    throw 'VsixPublisher.exe was not found. Restore Microsoft.VSSDK.BuildTools or install the Visual Studio SDK.'
}

Write-Host "VsixPublisher: $publisherExe"

if ($DryRun) {
    Write-Host 'Dry run completed. Marketplace publish was not executed.'
    return
}

if ([string]::IsNullOrWhiteSpace($PersonalAccessToken)) {
    throw 'PersonalAccessToken is required when not using -DryRun.'
}

& $publisherExe publish `
    -payload $VsixPath `
    -publishManifest $PublishManifestPath `
    -personalAccessToken $PersonalAccessToken

if ($LASTEXITCODE -ne 0) {
    throw "VsixPublisher publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published $($metadata.DisplayName) $($metadata.Version) to Visual Studio Marketplace."

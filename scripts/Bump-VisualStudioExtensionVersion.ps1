[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ProjectPath,
    [string]$ProviderProjectPath,
    [string]$ClassicProjectPath,
    [string]$ManifestPath,
    [string]$PackageSourcePath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj'
}

if ([string]::IsNullOrWhiteSpace($ProviderProjectPath)) {
    $ProviderProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\source.extension.vsixmanifest'
}

if ([string]::IsNullOrWhiteSpace($PackageSourcePath)) {
    $PackageSourcePath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizerPackage.cs'
}

if ([string]::IsNullOrWhiteSpace($ClassicProjectPath)) {
    $ClassicProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Classic\RawBufferVisualizer.VisualStudio.Classic.csproj'
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

function Get-NormalizedVersion {
    param([string]$Value)

    $trimmed = $Value.Trim().TrimStart('v', 'V')
    if ($trimmed -notmatch '^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?$') {
        throw "Version must be Major.Minor.Patch or Major.Minor.Patch.0: '$Value'"
    }

    $major = $Matches[1]
    $minor = $Matches[2]
    $patch = $Matches[3]
    $revision = $Matches[4]

    if (-not [string]::IsNullOrWhiteSpace($revision) -and $revision -ne '0') {
        throw "Use a three-part Marketplace version. Four-part input is accepted only when the revision is 0: '$Value'"
    }

    return [pscustomobject]@{
        Package = "$major.$minor.$patch"
        Assembly = "$major.$minor.$patch.0"
    }
}

function Replace-One {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Description
    )

    $matches = [regex]::Matches($Content, $Pattern)
    if ($matches.Count -ne 1) {
        throw "Expected one $Description entry, found $($matches.Count)."
    }

    return [regex]::Replace($Content, $Pattern, $Replacement, 1)
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

Assert-FileExists -Path $ProjectPath -Message 'Visual Studio extension project was not found'
Assert-FileExists -Path $ProviderProjectPath -Message 'Out-of-process provider project was not found'
Assert-FileExists -Path $ClassicProjectPath -Message 'Classic Bitmap visualizer project was not found'
Assert-FileExists -Path $ManifestPath -Message 'Visual Studio extension manifest was not found'
Assert-FileExists -Path $PackageSourcePath -Message 'Hybrid VSSDK package source was not found'

$versions = Get-NormalizedVersion -Value $Version

$project = Get-Content -Raw -LiteralPath $ProjectPath
$project = Replace-One -Content $project -Pattern '<AssemblyVersion>[^<]+</AssemblyVersion>' -Replacement "<AssemblyVersion>$($versions.Assembly)</AssemblyVersion>" -Description 'AssemblyVersion'
$project = Replace-One -Content $project -Pattern '<FileVersion>[^<]+</FileVersion>' -Replacement "<FileVersion>$($versions.Assembly)</FileVersion>" -Description 'FileVersion'
$project = Replace-One -Content $project -Pattern '<Version>[^<]+</Version>' -Replacement "<Version>$($versions.Package)</Version>" -Description 'Version'
Write-Utf8NoBom -Path $ProjectPath -Content $project

$providerProject = Get-Content -Raw -LiteralPath $ProviderProjectPath
$providerProject = Replace-One -Content $providerProject -Pattern '<AssemblyVersion>[^<]+</AssemblyVersion>' -Replacement "<AssemblyVersion>$($versions.Assembly)</AssemblyVersion>" -Description 'provider AssemblyVersion'
$providerProject = Replace-One -Content $providerProject -Pattern '<FileVersion>[^<]+</FileVersion>' -Replacement "<FileVersion>$($versions.Assembly)</FileVersion>" -Description 'provider FileVersion'
$providerProject = Replace-One -Content $providerProject -Pattern '<Version>[^<]+</Version>' -Replacement "<Version>$($versions.Package)</Version>" -Description 'provider Version'
Write-Utf8NoBom -Path $ProviderProjectPath -Content $providerProject

$classicProject = Get-Content -Raw -LiteralPath $ClassicProjectPath
$classicProject = Replace-One -Content $classicProject -Pattern '<AssemblyVersion>[^<]+</AssemblyVersion>' -Replacement "<AssemblyVersion>$($versions.Assembly)</AssemblyVersion>" -Description 'classic AssemblyVersion'
$classicProject = Replace-One -Content $classicProject -Pattern '<FileVersion>[^<]+</FileVersion>' -Replacement "<FileVersion>$($versions.Assembly)</FileVersion>" -Description 'classic FileVersion'
$classicProject = Replace-One -Content $classicProject -Pattern '<Version>[^<]+</Version>' -Replacement "<Version>$($versions.Package)</Version>" -Description 'classic Version'
Write-Utf8NoBom -Path $ClassicProjectPath -Content $classicProject

$manifest = Get-Content -Raw -LiteralPath $ManifestPath
$manifest = Replace-One -Content $manifest -Pattern '(<Identity\b[^>]*\bVersion=")[^"]+(")' -Replacement "`${1}$($versions.Assembly)`${2}" -Description 'VSIX Identity Version'
Write-Utf8NoBom -Path $ManifestPath -Content $manifest

$packageSource = Get-Content -Raw -LiteralPath $PackageSourcePath
$packageSource = Replace-One `
    -Content $packageSource `
    -Pattern '(\[InstalledProductRegistration\("[^"]+",\s*"[^"]+",\s*")[^"]+("\)\])' `
    -Replacement "`${1}$($versions.Package)`${2}" `
    -Description 'InstalledProductRegistration version'
Write-Utf8NoBom -Path $PackageSourcePath -Content $packageSource

$generatedManifestRoot = Join-Path $repoRoot '.build\intermediate\RawBufferVisualizer.VisualStudio.Vssdk'
if (Test-Path -LiteralPath $generatedManifestRoot -PathType Container) {
    Get-ChildItem -LiteralPath $generatedManifestRoot -Filter 'extension.vsixmanifest' -File -Recurse |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

$builtVsixRoot = Join-Path $repoRoot '.build\bin\RawBufferVisualizer.VisualStudio.Vssdk'
if (Test-Path -LiteralPath $builtVsixRoot -PathType Container) {
    Get-ChildItem -LiteralPath $builtVsixRoot -Filter 'RawBufferVisualizer.VisualStudio.Extensibility.vsix' -File -Recurse |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

Write-Host "Updated Visual Studio extension version:"
Write-Host "  Package:  $($versions.Package)"
Write-Host "  Assembly: $($versions.Assembly)"
Write-Host "  Project:  $ProjectPath"
Write-Host "  Provider: $ProviderProjectPath"
Write-Host "  Classic:  $ClassicProjectPath"
Write-Host "  Manifest: $ManifestPath"
Write-Host "  VSSDK:    $PackageSourcePath"
Write-Host "Invalidated generated manifests and VSIX outputs so the next package cannot reuse the prior version."
Write-Host "Release communication is intentionally not rewritten by this script."
Write-Host "Update CHANGELOG, Marketplace overview/notes, embedded VSIX notes, and ReleaseAnnouncement.cs, then run Test-ReleaseCommunication.ps1."

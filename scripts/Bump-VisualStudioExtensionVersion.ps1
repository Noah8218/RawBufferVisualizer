[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ProjectPath,
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest'
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
Assert-FileExists -Path $ManifestPath -Message 'Visual Studio extension manifest was not found'

$versions = Get-NormalizedVersion -Value $Version

$project = Get-Content -Raw -LiteralPath $ProjectPath
$project = Replace-One -Content $project -Pattern '<AssemblyVersion>[^<]+</AssemblyVersion>' -Replacement "<AssemblyVersion>$($versions.Assembly)</AssemblyVersion>" -Description 'AssemblyVersion'
$project = Replace-One -Content $project -Pattern '<FileVersion>[^<]+</FileVersion>' -Replacement "<FileVersion>$($versions.Assembly)</FileVersion>" -Description 'FileVersion'
$project = Replace-One -Content $project -Pattern '<Version>[^<]+</Version>' -Replacement "<Version>$($versions.Package)</Version>" -Description 'Version'
Write-Utf8NoBom -Path $ProjectPath -Content $project

$manifest = Get-Content -Raw -LiteralPath $ManifestPath
$manifest = Replace-One -Content $manifest -Pattern '(<Identity\b[^>]*\bVersion=")[^"]+(")' -Replacement "`${1}$($versions.Assembly)`${2}" -Description 'VSIX Identity Version'
Write-Utf8NoBom -Path $ManifestPath -Content $manifest

Write-Host "Updated Visual Studio extension version:"
Write-Host "  Package:  $($versions.Package)"
Write-Host "  Assembly: $($versions.Assembly)"
Write-Host "  Project:  $ProjectPath"
Write-Host "  Manifest: $ManifestPath"

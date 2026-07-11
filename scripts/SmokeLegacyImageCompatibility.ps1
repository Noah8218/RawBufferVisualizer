[CmdletBinding()]
param(
    [string[]]$EmguVersions = @(
        '3.4.3.3016',
        '4.2.0.3662',
        '4.5.5.4823',
        '4.8.1.5350',
        '4.13.0.5924'
    ),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'tests\RawBufferVisualizer.LegacyCompatibility\RawBufferVisualizer.LegacyCompatibility.csproj'

foreach ($version in $EmguVersions) {
    $includeRuntime = if ($version -eq '3.4.3.3016') { 'false' } else { 'true' }
    & dotnet run `
        --project $project `
        --configuration $Configuration `
        -p:EmguVersion=$version `
        -p:IncludeEmguRuntime=$includeRuntime `
        -- $version

    if ($LASTEXITCODE -ne 0) {
        throw "Legacy image compatibility smoke failed for Emgu CV $version."
    }
}

Write-Host "Legacy Bitmap and Emgu CV compatibility smoke passed for $($EmguVersions.Count) Emgu package version(s)."

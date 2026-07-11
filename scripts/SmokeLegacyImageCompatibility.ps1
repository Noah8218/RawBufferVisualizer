[CmdletBinding()]
param(
    [string[]]$OpenCvSharpVersions = @(
        '4.0.0.20181225',
        '4.2.0.20200208',
        '4.5.5.20211231',
        '4.8.0.20230708',
        '4.13.0.20260627'
    ),
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
        -- emgu $version

    if ($LASTEXITCODE -ne 0) {
        throw "Legacy image compatibility smoke failed for Emgu CV $version."
    }
}

foreach ($version in $OpenCvSharpVersions) {
    & dotnet run `
        --project $project `
        --configuration $Configuration `
        -p:OpenCvSharpVersion=$version `
        -- opencvsharp $version

    if ($LASTEXITCODE -ne 0) {
        throw "Legacy image compatibility smoke failed for OpenCvSharp $version."
    }
}

Write-Host "Legacy Bitmap, OpenCvSharp, and Emgu CV compatibility smoke passed for $($OpenCvSharpVersions.Count) OpenCvSharp and $($EmguVersions.Count) Emgu package version(s)."

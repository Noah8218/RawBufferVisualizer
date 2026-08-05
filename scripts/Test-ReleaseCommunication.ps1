[CmdletBinding()]
param(
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

function Assert-FileExists {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required release communication file was not found: $Path"
    }
}

function Assert-Contains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Description
    )

    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -notmatch $Pattern) {
        throw "$Description is missing or stale: $Path"
    }
}

function Get-ThreePartVersion {
    param([string]$Value)

    $trimmed = $Value.Trim().TrimStart('v', 'V')
    $parsed = $null
    if (-not [version]::TryParse($trimmed, [ref]$parsed)) {
        throw "Invalid release version: '$Value'"
    }

    if ($parsed.Build -lt 0) {
        throw "Release version must include Major.Minor.Patch: '$Value'"
    }

    if ($parsed.Revision -gt 0) {
        throw "Marketplace release revision must be zero: '$Value'"
    }

    return "$($parsed.Major).$($parsed.Minor).$($parsed.Build)"
}

$manifestPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\source.extension.vsixmanifest'
$projectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizer.VisualStudio.Extensibility.csproj'
$classicProjectPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Classic\RawBufferVisualizer.VisualStudio.Classic.csproj'
$packageSourcePath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\RawBufferVisualizerPackage.cs'
$announcementPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio\ReleaseAnnouncement.cs'
$toolWindowXamlPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferToolWindowControl.xaml'
$toolWindowCodePath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferToolWindowControl.xaml.cs'
$changeLogPath = Join-Path $repoRoot 'CHANGELOG.md'
$readmePath = Join-Path $repoRoot 'README.md'
$releaseWorkflowPath = Join-Path $repoRoot '.github\workflows\release.yml'

foreach ($requiredPath in @(
    $manifestPath,
    $projectPath,
    $classicProjectPath,
    $packageSourcePath,
    $announcementPath,
    $toolWindowXamlPath,
    $toolWindowCodePath,
    $changeLogPath,
    $readmePath,
    $releaseWorkflowPath)) {
    Assert-FileExists $requiredPath
}

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$manifestVersion = [string]$manifest.PackageManifest.Metadata.Identity.Version
$packageVersion = Get-ThreePartVersion $manifestVersion
if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $expectedPackageVersion = Get-ThreePartVersion $ExpectedVersion
    if ($packageVersion -ne $expectedPackageVersion) {
        throw "Release communication version $packageVersion does not match expected version $expectedPackageVersion."
    }
}

$overviewPath = Join-Path $repoRoot "docs\marketplace-overview-$packageVersion.md"
$marketplaceNotesPath = Join-Path $repoRoot "docs\marketplace-release-notes-$packageVersion.md"
$embeddedNotesPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\Resources\ReleaseNotes.txt'
foreach ($requiredPath in @($overviewPath, $marketplaceNotesPath, $embeddedNotesPath)) {
    Assert-FileExists $requiredPath
}

$escapedPackageVersion = [regex]::Escape($packageVersion)
$escapedAssemblyVersion = [regex]::Escape("$packageVersion.0")

Assert-Contains $projectPath "<Version>$escapedPackageVersion</Version>" 'Hybrid project package version'
Assert-Contains $projectPath "<AssemblyVersion>$escapedAssemblyVersion</AssemblyVersion>" 'Hybrid project assembly version'
Assert-Contains $classicProjectPath "<Version>$escapedPackageVersion</Version>" 'Classic project package version'
Assert-Contains $packageSourcePath ('InstalledProductRegistration\([^\r\n]+"' + $escapedPackageVersion + '"\)') 'Installed product version'
Assert-Contains $announcementPath ('CurrentVersion\s*=\s*"' + $escapedPackageVersion + '"') 'In-product announcement version'
Assert-Contains $toolWindowXamlPath '<ToggleButton x:Name="WhatsNewButton"[\s\S]*?AutomationProperties\.AutomationId="ReleaseAnnouncementOpenButton"[\s\S]*?Click="WhatsNew_Click"' "What's New toggle control"
Assert-Contains $toolWindowCodePath 'WhatsNewButton\.IsChecked\s*=\s*shouldShow' "What's New initial toggle state"
Assert-Contains $toolWindowCodePath 'private void WhatsNew_Click[\s\S]*?WhatsNewButton\.IsChecked != true[\s\S]*?ReleaseAnnouncementBanner\.Visibility = Visibility\.Collapsed[\s\S]*?Release highlights closed' "What's New repeated-click close behavior"
Assert-Contains $toolWindowCodePath 'private void MarkCurrentReleaseSeen[\s\S]*?ReleaseAnnouncementBanner\.Visibility = Visibility\.Collapsed;[\s\S]*?WhatsNewButton\.IsChecked = false;' "What's New dismissed-state synchronization"
Assert-Contains $changeLogPath "(?m)^## \[$escapedPackageVersion\]" 'CHANGELOG entry'
Assert-Contains $overviewPath '(?m)^# Raw Buffer Visualizer$' 'Marketplace overview product heading'
Assert-Contains $overviewPath "(?m)^## What's New In $escapedPackageVersion$" 'Marketplace overview current-version section'
Assert-Contains $marketplaceNotesPath "(?m)^# Raw Buffer Visualizer $escapedPackageVersion$" 'Marketplace release-notes heading'
Assert-Contains $embeddedNotesPath "(?m)^Raw Buffer Visualizer $escapedPackageVersion$" 'Embedded VSIX release-notes heading'
Assert-Contains $readmePath "CHANGELOG\.md" 'README changelog link'
Assert-Contains $readmePath "marketplace-overview-$escapedPackageVersion\.md" 'README current Marketplace overview link'
Assert-Contains $readmePath "marketplace-release-notes-$escapedPackageVersion\.md" 'README current release-notes link'
Assert-Contains $releaseWorkflowPath "Test-ReleaseCommunication\.ps1" 'GitHub Release communication gate'
Assert-Contains $releaseWorkflowPath "--notes-file" 'Curated GitHub Release notes'

$metadata = $manifest.PackageManifest.Metadata
if ([string]$metadata.MoreInfo -ne 'https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer') {
    throw 'VSIX MoreInfo must point to the stable Marketplace item URL.'
}
if ([string]$metadata.ReleaseNotes -ne 'Resources\ReleaseNotes.txt') {
    throw 'VSIX ReleaseNotes must point to Resources\ReleaseNotes.txt.'
}
if ([string]::IsNullOrWhiteSpace([string]$metadata.Tags)) {
    throw 'VSIX search Tags are missing.'
}

Write-Host "Release communication validation passed for Raw Buffer Visualizer $packageVersion."
Write-Host "  CHANGELOG: $changeLogPath"
Write-Host "  Marketplace overview: $overviewPath"
Write-Host "  Marketplace notes: $marketplaceNotesPath"
Write-Host "  Embedded notes: $embeddedNotesPath"

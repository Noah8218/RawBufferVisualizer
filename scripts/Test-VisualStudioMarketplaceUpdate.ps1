[CmdletBinding()]
param(
    [string]$ExpectedVersion,
    [string]$ExtensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f'
)

$ErrorActionPreference = 'Stop'

$vsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\VisualStudio'
if (-not (Test-Path -LiteralPath $vsRoot -PathType Container)) {
    throw "Visual Studio user data folder was not found: $vsRoot"
}

$matches = @()
foreach ($instance in Get-ChildItem -LiteralPath $vsRoot -Directory -Filter '17.0_*') {
    $extensionsRoot = Join-Path $instance.FullName 'Extensions'
    if (-not (Test-Path -LiteralPath $extensionsRoot -PathType Container)) {
        continue
    }

    $manifests = Get-ChildItem -LiteralPath $extensionsRoot -Recurse -Filter 'extension.vsixmanifest' -File -ErrorAction SilentlyContinue
    foreach ($manifestPath in $manifests) {
        try {
            [xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath.FullName
            $identity = $manifest.PackageManifest.Metadata.Identity
            if ([string]$identity.Id -eq $ExtensionId) {
                $matches += [pscustomobject]@{
                    Instance = $instance.Name
                    Version  = [string]$identity.Version
                    Path     = $manifestPath.DirectoryName
                }
            }
        }
        catch {
            Write-Warning "Skipped invalid VSIX manifest: $($manifestPath.FullName)"
        }
    }
}

if ($matches.Count -eq 0) {
    throw "Raw Buffer Visualizer is not installed for any Visual Studio 2022 instance under $vsRoot."
}

$matches | Sort-Object Instance, Version | Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $expected = $ExpectedVersion.TrimStart('v')
    $bad = @($matches | Where-Object { $_.Version -ne $expected })
    if ($bad.Count -gt 0) {
        throw "Installed extension version does not match expected version $expected."
    }
}

$activityLogs = Get-ChildItem -LiteralPath $vsRoot -Recurse -Filter 'ActivityLog.xml' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 5

foreach ($log in $activityLogs) {
    $raw = Get-Content -Raw -LiteralPath $log.FullName
    if ($raw -match 'RawBufferVisualizerPackage|RawBufferVisualizer') {
        if ($raw -match 'RawBufferVisualizerPackage.*(SetSite failed|did not load correctly)|Could not load file or assembly') {
            Write-Warning "Visual Studio ActivityLog contains Raw Buffer Visualizer package load errors. If the current Visual Studio session shows the same popup, run Repair-VisualStudioExtensionRegistration.ps1: $($log.FullName)"
            continue
        }

        Write-Host "Checked ActivityLog: $($log.FullName)"
    }
}

Write-Host 'Marketplace update verification completed.'

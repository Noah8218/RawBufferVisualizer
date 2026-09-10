[CmdletBinding()]
param(
    [string]$ExpectedVersion,
    [string]$ExtensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f',
    [switch]$IncludeExperimental
)

$ErrorActionPreference = 'Stop'

$packageGuid = '{1977574b-f107-465f-bfd1-5fc022907039}'
$retiredPackageGuid = '{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}'
$legacySplitExtensionId = 'RawBufferVisualizer.VisualStudio.Vssdk'
$packageRegistrationHeader = '[$RootKey$\Packages\' + $packageGuid + ']'
$menuRegistration = '"' + $packageGuid + '"=", Menus.ctmenu, 2"'
$toolWindowRegistrationHeader = '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
$vsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\VisualStudio'
if (-not (Test-Path -LiteralPath $vsRoot -PathType Container)) {
    throw "Visual Studio user data folder was not found: $vsRoot"
}

$installedExtensions = @()
$legacySplitExtensions = @()
$perMachineConflicts = @()
$instances = @(Get-ChildItem -LiteralPath $vsRoot -Directory |
    Where-Object {
        $_.Name -match '^(17|18)\.0_' -and
        ($IncludeExperimental -or $_.Name -match '^(17|18)\.0_[0-9a-fA-F]{8}$')
    })

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $setupInstances = @((& $vswhere -all -products * -format json | ConvertFrom-Json) | ForEach-Object { $_ })
    foreach ($setupInstance in $setupInstances) {
        if ($setupInstance.installationVersion -notmatch '^(17|18)\.') {
            continue
        }

        $perMachineRoot = Join-Path ([string]$setupInstance.installationPath) 'Common7\IDE\VSExtensions'
        if (-not (Test-Path -LiteralPath $perMachineRoot -PathType Container)) {
            continue
        }

        foreach ($manifestPath in Get-ChildItem -LiteralPath $perMachineRoot -Recurse -Filter 'extension.vsixmanifest' -File -ErrorAction SilentlyContinue) {
            try {
                [xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath.FullName
                $identity = $manifest.PackageManifest.Metadata.Identity
                if ([string]$identity.Id -eq $ExtensionId -or [string]$identity.Id -eq $legacySplitExtensionId) {
                    $perMachineConflicts += [pscustomobject]@{
                        Instance = "$(([Version]$setupInstance.installationVersion).Major).0_$($setupInstance.instanceId)"
                        Id       = [string]$identity.Id
                        Version  = [string]$identity.Version
                        Path     = $manifestPath.DirectoryName
                    }
                }
            }
            catch {
                Write-Warning "Skipped invalid per-machine VSIX manifest: $($manifestPath.FullName)"
            }
        }
    }
}

foreach ($instance in $instances) {
    $extensionsRoot = Join-Path $instance.FullName 'Extensions'
    if (-not (Test-Path -LiteralPath $extensionsRoot -PathType Container)) {
        continue
    }

    $manifests = Get-ChildItem -LiteralPath $extensionsRoot -Recurse -Filter 'extension.vsixmanifest' -File -ErrorAction SilentlyContinue
    foreach ($manifestPath in $manifests) {
        try {
            [xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath.FullName
            $identity = $manifest.PackageManifest.Metadata.Identity
            if ([string]$identity.Id -eq $legacySplitExtensionId) {
                $legacySplitExtensions += [pscustomobject]@{
                    Instance = $instance.Name
                    Version  = [string]$identity.Version
                    Path     = $manifestPath.DirectoryName
                }
                continue
            }

            if ([string]$identity.Id -eq $ExtensionId) {
                $packageKey = "HKCU:\Software\Microsoft\VisualStudio\$($instance.Name)_Config\Packages\$packageGuid"
                $packageRegistered = Test-Path -LiteralPath $packageKey
                $codeBase = if ($packageRegistered) {
                    [string](Get-ItemProperty -LiteralPath $packageKey -Name CodeBase -ErrorAction SilentlyContinue).CodeBase
                }
                else {
                    ''
                }
                $expectedCodeBase = Join-Path $manifestPath.DirectoryName 'RawBufferVisualizer.VisualStudio.Vssdk.dll'
                $codeBaseMatchesInstall = if ($packageRegistered) {
                    -not [string]::IsNullOrWhiteSpace($codeBase) `
                        -and (Test-Path -LiteralPath $codeBase -PathType Leaf) `
                        -and [string]::Equals(
                            [IO.Path]::GetFullPath($codeBase),
                            [IO.Path]::GetFullPath($expectedCodeBase),
                            [StringComparison]::OrdinalIgnoreCase)
                }
                else {
                    $null
                }
                $pkgdefPath = Join-Path $manifestPath.DirectoryName 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef'
                $pkgdefText = if (Test-Path -LiteralPath $pkgdefPath -PathType Leaf) {
                    Get-Content -Raw -LiteralPath $pkgdefPath
                }
                else {
                    ''
                }
                $registrationPayloadValid = $pkgdefText.Contains($packageRegistrationHeader) `
                    -and $pkgdefText.Contains('"Class"="RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage"') `
                    -and $pkgdefText.Contains('"CodeBase"="$PackageFolder$\RawBufferVisualizer.VisualStudio.Vssdk.dll"') `
                    -and $pkgdefText.Contains($menuRegistration) `
                    -and $pkgdefText.Contains($toolWindowRegistrationHeader) `
                    -and [regex]::Matches($pkgdefText, [regex]::Escape($packageRegistrationHeader)).Count -eq 1 `
                    -and [regex]::Matches($pkgdefText, [regex]::Escape($menuRegistration)).Count -eq 1 `
                    -and [regex]::Matches($pkgdefText, [regex]::Escape($toolWindowRegistrationHeader)).Count -eq 1 `
                    -and -not $pkgdefText.Contains($retiredPackageGuid)

                $installedExtensions += [pscustomobject]@{
                    Instance                 = $instance.Name
                    Version                  = [string]$identity.Version
                    RegistrationPayloadValid = $registrationPayloadValid
                    LegacyConfigKeyVisible    = $packageRegistered
                    CodeBaseMatchesInstall = $codeBaseMatchesInstall
                    CodeBase                 = $codeBase
                    Path                     = $manifestPath.DirectoryName
                }
            }
        }
        catch {
            Write-Warning "Skipped invalid VSIX manifest: $($manifestPath.FullName)"
        }
    }
}

if ($perMachineConflicts.Count -gt 0) {
    $perMachineConflicts | Sort-Object Instance, Id, Version | Format-Table -AutoSize
    throw 'A per-machine Raw Buffer Visualizer installation already owns the extension ID. Do not delete Program Files content manually. Remove or update it through Visual Studio Manage Extensions/Installer with administrator rights before qualifying a Marketplace update.'
}

if ($installedExtensions.Count -eq 0) {
    throw "Raw Buffer Visualizer is not installed for any Visual Studio 2022 or Visual Studio 2026 instance under $vsRoot."
}

if ($legacySplitExtensions.Count -gt 0) {
    $legacySplitExtensions | Sort-Object Instance, Version | Format-Table -AutoSize
    throw "The retired split VSSDK extension is still installed. Uninstall '$legacySplitExtensionId', restart Visual Studio, and repeat the qualification."
}

$duplicateMainInstalls = @($installedExtensions |
    Group-Object Instance |
    Where-Object Count -ne 1)
if ($duplicateMainInstalls.Count -gt 0) {
    throw 'Each Visual Studio instance must contain exactly one Raw Buffer Visualizer Marketplace extension manifest.'
}

$installedExtensions | Sort-Object Instance, Version | Format-Table -AutoSize

$registrationFailures = @($installedExtensions | Where-Object { -not $_.RegistrationPayloadValid })
if ($registrationFailures.Count -gt 0) {
    throw 'The installed VSIX does not contain the required current-project VSSDK registration payload. Do not qualify or upload this package.'
}

$staleLegacyKeys = @($installedExtensions |
    Where-Object { $_.LegacyConfigKeyVisible -and -not $_.CodeBaseMatchesInstall })
if ($staleLegacyKeys.Count -gt 0) {
    throw 'A stale developer/manual VSSDK CodeBase is visible for this Visual Studio profile. Remove the stale registration, reinstall, restart, and run the installed-VSIX smoke before qualifying the package.'
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $expected = $ExpectedVersion.TrimStart('v')
    $bad = @($installedExtensions | Where-Object { $_.Version -ne $expected })
    if ($bad.Count -gt 0) {
        throw "Installed extension version does not match expected version $expected."
    }
}

$activityLogs = $instances |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter 'ActivityLog.xml' -File -ErrorAction SilentlyContinue } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 5

foreach ($log in $activityLogs) {
    $raw = Get-Content -Raw -LiteralPath $log.FullName
    if ($raw -match 'RawBufferVisualizerPackage|RawBufferVisualizer') {
        if ($raw -match 'RawBufferVisualizerPackage.*(SetSite failed|did not load correctly)|Could not load file or assembly') {
            Write-Warning "Visual Studio ActivityLog contains Raw Buffer Visualizer package load errors. Do not qualify this build; preserve the log and fix the package before publishing: $($log.FullName)"
            continue
        }

        Write-Host "Checked ActivityLog: $($log.FullName)"
    }
}

Write-Host 'Marketplace update payload verification completed. Run SmokeInstalledVsixNewFeatures.ps1 after restart to prove runtime handoff acknowledgement.'

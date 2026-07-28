[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('net472')]
    [string]$Framework = 'net472',
    [string]$Configuration = 'Debug',
    [string]$ViewerFramework = 'net472',
    [string]$VisualStudioInstanceId = '',
    [string]$VsixInstallerPath = '',
    [switch]$NoBuild,
    [switch]$NoViewerEnv,
    [switch]$Reinstall,
    [switch]$AllowRunningVisualStudio,
    [switch]$RepairRegistrationOnly
)

$ErrorActionPreference = 'Stop'

$extensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f'
$toolWindowExtensionId = 'RawBufferVisualizer.VisualStudio.Vssdk'
$minimumVisualStudioVersion = [Version]'17.9.0'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publishScript = Join-Path $repoRoot 'scripts\Publish-VisualStudioExtension.ps1'
$repairScript = Join-Path $repoRoot 'scripts\Repair-VisualStudioExtensionRegistration.ps1'
$vsixPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Extensibility.vsix"

function Assert-VisualStudioNotRunning {
    if ($AllowRunningVisualStudio -and $WhatIfPreference) {
        return
    }

    $devenvProcesses = Get-Process -Name devenv -ErrorAction SilentlyContinue
    if (-not $devenvProcesses) {
        return
    }

    $processList = ($devenvProcesses |
        Sort-Object Id |
        ForEach-Object { "PID $($_.Id): $($_.MainWindowTitle)" }) -join '; '
    throw "Visual Studio is running. Close all Visual Studio windows before installing Raw Buffer Visualizer, then retry this script. Running devenv: $processList"
}

function Find-VsixInstaller {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "VSIXInstaller.exe was not found: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VsixInstaller\VSIXInstaller.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'VSIXInstaller.exe was not found. Pass -VsixInstallerPath explicitly.'
}

function Get-VisualStudioInstances {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    )
    $vswhere = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $vswhere) {
        return @()
    }

    $json = & $vswhere -all -format json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return @()
    }

    @($json | ConvertFrom-Json)
}

function Find-VisualStudioInstanceId {
    if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstanceId)) {
        return $VisualStudioInstanceId
    }

    $instances = Get-VisualStudioInstances
    $compatibleInstance = $instances |
        Where-Object {
            $_.installationVersion -like '17.*' -and
            $_.isLaunchable -eq $true -and
            ([Version]$_.installationVersion) -ge $minimumVisualStudioVersion
        } |
        Select-Object -First 1
    if ($compatibleInstance) {
        return $compatibleInstance.instanceId
    }

    $unsupportedInstance = $instances |
        Where-Object { $_.installationVersion -like '17.*' -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    if ($unsupportedInstance) {
        throw "Raw Buffer Visualizer requires Visual Studio 2022 $minimumVisualStudioVersion or newer. Installed Visual Studio version is $($unsupportedInstance.installationVersion). Update Visual Studio, then rerun this script."
    }

    ''
}

function Invoke-VsixInstaller {
    param(
        [string]$InstallerPath,
        [string[]]$Arguments,
        [string]$Action,
        [switch]$AllowFailure
    )

    if (-not $PSCmdlet.ShouldProcess($Action, "$InstallerPath $($Arguments -join ' ')")) {
        return
    }

    $process = Start-Process -FilePath $InstallerPath -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        if ($AllowFailure) {
            Write-Warning "$Action failed with exit code $($process.ExitCode). Continuing."
            return
        }

        throw "$Action failed with exit code $($process.ExitCode). Close Visual Studio and retry."
    }
}

function Find-InstalledExtensionFolder {
    param([string]$InstanceId)

    if ([string]::IsNullOrWhiteSpace($InstanceId)) {
        return ''
    }

    $extensionRoot = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio\17.0_$InstanceId\Extensions"
    if (-not (Test-Path -LiteralPath $extensionRoot)) {
        return ''
    }

    $extensionPath = Get-ChildItem -LiteralPath $extensionRoot -Filter extension.vsixmanifest -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                (Get-Content -LiteralPath $_.FullName -Raw) -match [regex]::Escape($extensionId)
            }
            catch {
                $false
            }
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($extensionPath) {
        return $extensionPath.DirectoryName
    }

    ''
}

function Remove-LegacyClassicVisualizer {
    $visualizersDirectory = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Visual Studio 2022\Visualizers'
    $relativePaths = @(
        'RawBufferVisualizer.VisualStudio.Classic.dll',
        'RawBufferVisualizer.Core.dll',
        'RawBufferVisualizer.Sdk.dll',
        'RawBufferVisualizer.VisualStudio.dll',
        'RawBufferVisualizer.VisualStudio.ObjectSource.dll',
        'netstandard2.0\RawBufferVisualizer.Core.dll',
        'netstandard2.0\RawBufferVisualizer.Sdk.dll',
        'netstandard2.0\RawBufferVisualizer.VisualStudio.ObjectSource.dll'
    )

    foreach ($relativePath in $relativePaths) {
        $path = Join-Path $visualizersDirectory $relativePath
        if (Test-Path -LiteralPath $path) {
            if ($PSCmdlet.ShouldProcess($path, 'Remove legacy Classic debugger visualizer file')) {
                Remove-Item -LiteralPath $path -Force
            }
        }
    }

    Write-Host 'Removed legacy Classic visualizer files; the VSIX now owns debugger icon registration.'
}

function Test-DebuggerVisualizerVsixInstall {
    param([string]$InstanceId)

    if ([string]::IsNullOrWhiteSpace($InstanceId)) {
        throw 'Visual Studio 2022 instance id was not found. Cannot validate debugger visualizer installation.'
    }

    $extensionPath = Find-InstalledExtensionFolder -InstanceId $InstanceId
    if ([string]::IsNullOrWhiteSpace($extensionPath)) {
        $extensionRoot = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio\17.0_$InstanceId\Extensions"
        throw "Raw Buffer Visualizer VSIX is not installed in this Visual Studio instance. Debugger icons will not appear. Expected extension id '$extensionId' under: $extensionRoot"
    }

    $extensionJson = Join-Path $extensionPath '.vsextension\extension.json'
    if (-not (Test-Path -LiteralPath $extensionJson)) {
        throw "Debugger visualizer metadata is missing: $extensionJson"
    }

    $extensionJsonText = Get-Content -LiteralPath $extensionJson -Raw
    $objectSource = Join-Path $extensionPath 'netstandard2.0\RawBufferVisualizer.VisualStudio.ObjectSource.dll'
    if (-not (Test-Path -LiteralPath $objectSource)) {
        throw "Debugger object source is missing: $objectSource"
    }

    $packageDll = Join-Path $extensionPath 'RawBufferVisualizer.VisualStudio.Extensibility.dll'
    if (-not (Test-Path -LiteralPath $packageDll)) {
        throw "Hybrid VSSDK package assembly is missing: $packageDll"
    }

    $pkgdef = Join-Path $extensionPath 'RawBufferVisualizer.VisualStudio.Extensibility.pkgdef'
    if (-not (Test-Path -LiteralPath $pkgdef)) {
        throw "Hybrid VSSDK package registration is missing: $pkgdef"
    }

    $pkgdefText = Get-Content -LiteralPath $pkgdef -Raw
    foreach ($requiredRegistration in @(
        '[$RootKey$\Packages\{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}]',
        '"Class"="RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage"',
        '"CodeBase"="$PackageFolder$\RawBufferVisualizer.VisualStudio.Extensibility.dll"',
        '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
    )) {
        if (-not $pkgdefText.Contains($requiredRegistration)) {
            throw "Installed VSIX has invalid hybrid VSSDK registration '$requiredRegistration': $pkgdef"
        }
    }

    foreach ($requiredText in @(
        'RawBufferSnapshotDebuggerVisualizerProvider',
        'RawBufferViewDebuggerVisualizerProvider',
        'BitmapDebuggerVisualizerProvider',
        'OpenCvSharpMatDebuggerVisualizerProvider',
        'EmguCvMatDebuggerVisualizerProvider',
        'ImagePtrDebuggerVisualizerProvider',
        'ImageCollectionDebuggerVisualizerProvider',
        'IDebuggerVisualizerProvider'
    )) {
        if ($extensionJsonText -notmatch [regex]::Escape($requiredText)) {
            throw "Installed VSIX is missing debugger visualizer metadata '$requiredText': $extensionJson"
        }
    }

    Write-Host "Validated debugger visualizer VSIX metadata: $extensionPath"
}

function Stop-DotNetBuildServers {
    try {
        & dotnet build-server shutdown | Out-Null
    }
    catch {
        Write-Warning "Could not stop dotnet build servers. Continuing. $($_.Exception.Message)"
    }

    $orphanedMsBuildNodes = @(Get-CimInstance Win32_Process -Filter "Name='MSBuild.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -match '/nodemode:\d+' -and
            $null -eq (Get-Process -Id $_.ParentProcessId -ErrorAction SilentlyContinue)
        })
    foreach ($node in $orphanedMsBuildNodes) {
        Stop-Process -Id $node.ProcessId -Force -ErrorAction SilentlyContinue
    }

    if ($orphanedMsBuildNodes.Count -gt 0) {
        Write-Host "Stopped $($orphanedMsBuildNodes.Count) orphaned Visual Studio MSBuild node(s)."
    }
}

if ($RepairRegistrationOnly) {
    $instanceId = Find-VisualStudioInstanceId
    if ([string]::IsNullOrWhiteSpace($instanceId)) {
        throw 'Visual Studio 2022 instance was not found.'
    }

    & powershell -ExecutionPolicy Bypass -File $repairScript -VisualStudioInstanceId $instanceId
    if ($LASTEXITCODE -ne 0) {
        throw "Registration repair failed with exit code $LASTEXITCODE"
    }
    return
}

Push-Location $repoRoot
try {
    Assert-VisualStudioNotRunning

    if (-not $NoBuild) {
        & powershell -ExecutionPolicy Bypass -File $publishScript -Framework $Framework -Configuration $Configuration -ViewerFramework $ViewerFramework -NoZip
        if ($LASTEXITCODE -ne 0) {
            throw "Visual Studio extension publish failed with exit code $LASTEXITCODE"
        }
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $vsixPath)) {
    throw "VSIX was not found: $vsixPath"
}

Stop-DotNetBuildServers

$installer = Find-VsixInstaller -ExplicitPath $VsixInstallerPath
$instanceId = Find-VisualStudioInstanceId
Write-Host "VSIXInstaller: $installer"
if (-not [string]::IsNullOrWhiteSpace($instanceId)) {
    Write-Host "Visual Studio instance: $instanceId"
}

if ($Reinstall) {
    $uninstallArguments = @('/quiet')
    if (-not [string]::IsNullOrWhiteSpace($instanceId)) {
        $uninstallArguments += "/instanceIds:$instanceId"
    }

    Invoke-VsixInstaller -InstallerPath $installer -Arguments ($uninstallArguments + @("/uninstall:$extensionId")) -Action 'Uninstall Raw Buffer Visualizer VSIX' -AllowFailure
    Invoke-VsixInstaller -InstallerPath $installer -Arguments ($uninstallArguments + @("/uninstall:$toolWindowExtensionId")) -Action 'Uninstall legacy split ToolWindow VSIX' -AllowFailure
}

$installArguments = @('/quiet', '/force')
if (-not [string]::IsNullOrWhiteSpace($instanceId)) {
    $installArguments += "/instanceIds:$instanceId"
}

Invoke-VsixInstaller -InstallerPath $installer -Arguments ($installArguments + @($vsixPath)) -Action 'Install Raw Buffer Visualizer VSIX'
Remove-LegacyClassicVisualizer
if ($WhatIfPreference) {
    Write-Host 'WhatIf completed: no VSIX changes were made.'
}
else {
    Test-DebuggerVisualizerVsixInstall -InstanceId $instanceId
    Write-Host "Installed: $vsixPath"
    Write-Host 'Restart Visual Studio before testing the debugger visualizer.'
}

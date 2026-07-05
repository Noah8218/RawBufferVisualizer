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
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publishScript = Join-Path $repoRoot 'scripts\Publish-VisualStudioExtension.ps1'
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

function Find-VisualStudioInstanceId {
    if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstanceId)) {
        return $VisualStudioInstanceId
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    )
    $vswhere = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $vswhere) {
        return ''
    }

    $json = & $vswhere -all -format json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return ''
    }

    $instances = $json | ConvertFrom-Json
    $instance = $instances |
        Where-Object { $_.installationVersion -like '17.*' -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    if ($instance) {
        return $instance.instanceId
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

function Register-VssdkToolWindow {
    param([string]$InstanceId)

    if ([string]::IsNullOrWhiteSpace($InstanceId)) {
        return
    }

    $configRoot = "HKCU:\Software\Microsoft\VisualStudio\17.0_$($InstanceId)_Config"
    if (-not (Test-Path -LiteralPath $configRoot)) {
        return
    }

    $packageFolder = Find-VssdkPackageFolder -InstanceId $InstanceId
    $packageDll = Join-Path $packageFolder 'RawBufferVisualizer.VisualStudio.Vssdk.dll'
    if ($WhatIfPreference) {
        Write-Host "WhatIf: would register VSSDK ToolWindow package: $packageDll"
        return
    }

    $packageGuid = '{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}'
    $windowGuid = '{a329e331-089a-4186-8fd7-57a241fd1917}'
    $solutionExplorerGuid = '{3ae79031-e1bc-11d0-8f78-00a0c9110057}'
    $shellInitializedGuid = '{e80ef1cb-6d64-4609-8faa-feacfd3bc89f}'
    $noSolutionGuid = '{adfc4e64-0397-11d1-9f4e-00a0c911004f}'
    $solutionExistsGuid = '{f1536ef8-92ec-443c-9ed7-fdadf150da82}'
    $debuggingGuid = '{adfc4e61-0397-11d1-9f4e-00a0c911004f}'
    $solutionFullyLoadedGuid = '{10534154-102d-46e2-aba8-a6bfa25ba0be}'
    $solutionReadyGuid = '{d0e4deec-1b53-4cda-8559-d454583ad23b}'

    $packageKey = Join-Path $configRoot "Packages\$packageGuid"
    New-Item -Path $packageKey -Force | Out-Null
    Set-ItemProperty -Path $packageKey -Name '(default)' -Value 'RawBufferVisualizerPackage'
    New-ItemProperty -Path $packageKey -Name 'InprocServer32' -Value "$env:WINDIR\SYSTEM32\MSCOREE.DLL" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $packageKey -Name 'Class' -Value 'RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $packageKey -Name 'CodeBase' -Value $packageDll -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $packageKey -Name 'AllowsBackgroundLoad' -Value 1 -PropertyType DWord -Force | Out-Null

    foreach ($contextGuid in @($shellInitializedGuid, $noSolutionGuid, $solutionExistsGuid, $debuggingGuid, $solutionFullyLoadedGuid, $solutionReadyGuid)) {
        $autoLoadKey = Join-Path $configRoot "AutoLoadPackages\$contextGuid"
        New-Item -Path $autoLoadKey -Force | Out-Null
        New-ItemProperty -Path $autoLoadKey -Name $packageGuid -Value 2 -PropertyType DWord -Force | Out-Null
    }

    $bindingKey = Join-Path $configRoot "BindingPaths\$packageGuid"
    if (Test-Path -LiteralPath $bindingKey) {
        Remove-Item -LiteralPath $bindingKey -Recurse -Force
    }
    New-Item -Path $bindingKey -Force | Out-Null
    New-ItemProperty -Path $bindingKey -Name $packageFolder -Value '' -PropertyType String -Force | Out-Null

    $menuKey = Join-Path $configRoot 'Menus'
    New-Item -Path $menuKey -Force | Out-Null
    New-ItemProperty -Path $menuKey -Name $packageGuid -Value ', Menus.ctmenu, 1' -PropertyType String -Force | Out-Null

    $toolWindowKey = Join-Path $configRoot "ToolWindows\$windowGuid"
    New-Item -Path $toolWindowKey -Force | Out-Null
    Set-ItemProperty -Path $toolWindowKey -Name '(default)' -Value $packageGuid
    New-ItemProperty -Path $toolWindowKey -Name 'Name' -Value 'RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindow' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $toolWindowKey -Name 'Style' -Value 4 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $toolWindowKey -Name 'Window' -Value $solutionExplorerGuid -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $toolWindowKey -Name 'Orientation' -Value 3 -PropertyType DWord -Force | Out-Null

    Write-Host "Registered VSSDK ToolWindow package: $packageDll"
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
    foreach ($requiredText in @(
        'IDebuggerVisualizerProvider',
        'RawBufferSnapshotDebuggerVisualizerProvider',
        'BitmapDebuggerVisualizerProvider',
        'OpenCvSharpMatDebuggerVisualizerProvider',
        'EmguCvMatDebuggerVisualizerProvider'
    )) {
        if ($extensionJsonText -notmatch [regex]::Escape($requiredText)) {
            throw "Debugger visualizer metadata does not contain '$requiredText': $extensionJson"
        }
    }

    $objectSource = Join-Path $extensionPath 'netstandard2.0\RawBufferVisualizer.VisualStudio.ObjectSource.dll'
    if (-not (Test-Path -LiteralPath $objectSource)) {
        throw "Debugger object source is missing: $objectSource"
    }

    Write-Host "Validated debugger visualizer VSIX metadata: $extensionPath"
}

function Find-VssdkPackageFolder {
    param([string]$InstanceId)

    $buildPackageFolder = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\net472"
    $buildPackageDll = Join-Path $buildPackageFolder 'RawBufferVisualizer.VisualStudio.Vssdk.dll'
    if (Test-Path -LiteralPath $buildPackageDll) {
        return (Resolve-Path -LiteralPath $buildPackageFolder).Path
    }

    $installedExtensionFolder = Find-InstalledExtensionFolder -InstanceId $InstanceId
    if (-not [string]::IsNullOrWhiteSpace($installedExtensionFolder)) {
        $installedPackageDll = Join-Path $installedExtensionFolder 'RawBufferVisualizer.VisualStudio.Vssdk.dll'
        if (Test-Path -LiteralPath $installedPackageDll) {
            return (Resolve-Path -LiteralPath $installedExtensionFolder).Path
        }
    }

    throw "RawBufferVisualizer.VisualStudio.Vssdk.dll was not found. Build the solution first, or reinstall after closing Visual Studio. Expected: $buildPackageDll"
}

function Stop-DotNetBuildServers {
    try {
        & dotnet build-server shutdown | Out-Null
    }
    catch {
        Write-Warning "Could not stop dotnet build servers. Continuing. $($_.Exception.Message)"
    }
}

if ($RepairRegistrationOnly) {
    $instanceId = Find-VisualStudioInstanceId
    if ([string]::IsNullOrWhiteSpace($instanceId)) {
        throw 'Visual Studio 2022 instance was not found.'
    }

    Write-Host "Visual Studio instance: $instanceId"
    Register-VssdkToolWindow -InstanceId $instanceId
    Write-Host 'Repaired Raw Buffer Visualizer VSSDK registration. Restart Visual Studio before testing.'
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
Register-VssdkToolWindow -InstanceId $instanceId
if ($WhatIfPreference) {
    Write-Host 'WhatIf completed: no VSIX changes were made.'
}
else {
    Test-DebuggerVisualizerVsixInstall -InstanceId $instanceId
    Write-Host "Installed: $vsixPath"
    Write-Host 'Restart Visual Studio before testing the debugger visualizer.'
}

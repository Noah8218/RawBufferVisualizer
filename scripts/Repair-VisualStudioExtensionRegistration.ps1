[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$VisualStudioInstanceId = '',
    [switch]$AllowRunningVisualStudio
)

$ErrorActionPreference = 'Stop'

$extensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f'
$packageGuid = '{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}'
$windowGuid = '{a329e331-089a-4186-8fd7-57a241fd1917}'
$solutionExplorerGuid = '{3ae79031-e1bc-11d0-8f78-00a0c9110057}'
$autoLoadContextGuids = @(
    '{e80ef1cb-6d64-4609-8faa-feacfd3bc89f}',
    '{adfc4e64-0397-11d1-9f4e-00a0c911004f}',
    '{f1536ef8-92ec-443c-9ed7-fdadf150da82}',
    '{adfc4e61-0397-11d1-9f4e-00a0c911004f}',
    '{10534154-102d-46e2-aba8-a6bfa25ba0be}',
    '{d0e4deec-1b53-4cda-8559-d454583ad23b}'
)

function Assert-VisualStudioNotRunning {
    if ($AllowRunningVisualStudio) {
        Write-Warning 'Visual Studio is running. The repair will be written to registry, but restart Visual Studio before testing.'
        return
    }

    $devenvProcesses = Get-Process -Name devenv -ErrorAction SilentlyContinue
    if (-not $devenvProcesses) {
        return
    }

    $processList = ($devenvProcesses |
        Sort-Object Id |
        ForEach-Object { "PID $($_.Id): $($_.MainWindowTitle)" }) -join '; '
    throw "Visual Studio is running. Close all Visual Studio windows before repairing Raw Buffer Visualizer registration. Running devenv: $processList"
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
            $_.isLaunchable -eq $true
        } |
        Select-Object -First 1
    if ($compatibleInstance) {
        return $compatibleInstance.instanceId
    }

    throw 'Visual Studio 2022 instance was not found.'
}

function Find-InstalledExtensionFolder {
    param([string]$InstanceId)

    $extensionRoot = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio\17.0_$InstanceId\Extensions"
    if (-not (Test-Path -LiteralPath $extensionRoot)) {
        throw "Visual Studio extension folder was not found: $extensionRoot"
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

    if (-not $extensionPath) {
        throw "Raw Buffer Visualizer VSIX is not installed under: $extensionRoot"
    }

    $packageDll = Join-Path $extensionPath.DirectoryName 'RawBufferVisualizer.VisualStudio.Vssdk.dll'
    if (-not (Test-Path -LiteralPath $packageDll)) {
        throw "Raw Buffer Visualizer VSSDK package DLL was not found: $packageDll"
    }

    $extensionPath.DirectoryName
}

function Set-StringProperty {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Value
    )

    if ($Name -eq '(default)') {
        Set-ItemProperty -Path $Path -Name '(default)' -Value $Value
        return
    }

    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType String -Force | Out-Null
}

function Remove-AutoLoadRegistration {
    param([string]$ConfigRoot)

    foreach ($contextGuid in $autoLoadContextGuids) {
        $autoLoadKey = Join-Path $ConfigRoot "AutoLoadPackages\$contextGuid"
        if (-not (Test-Path -LiteralPath $autoLoadKey)) {
            continue
        }

        Remove-ItemProperty -Path $autoLoadKey -Name $packageGuid -ErrorAction SilentlyContinue
    }
}

Assert-VisualStudioNotRunning

$instanceId = Find-VisualStudioInstanceId
$configRoot = "HKCU:\Software\Microsoft\VisualStudio\17.0_$($instanceId)_Config"
if (-not (Test-Path -LiteralPath $configRoot)) {
    throw "Visual Studio config root was not found: $configRoot"
}

$packageFolder = Find-InstalledExtensionFolder -InstanceId $instanceId
$packageDll = Join-Path $packageFolder 'RawBufferVisualizer.VisualStudio.Vssdk.dll'

if ($PSCmdlet.ShouldProcess("Visual Studio instance $instanceId", "repair Raw Buffer Visualizer VSSDK registration to $packageFolder")) {
    $packageKey = Join-Path $configRoot "Packages\$packageGuid"
    New-Item -Path $packageKey -Force | Out-Null
    Set-StringProperty -Path $packageKey -Name '(default)' -Value 'RawBufferVisualizerPackage'
    Set-StringProperty -Path $packageKey -Name 'InprocServer32' -Value "$env:WINDIR\SYSTEM32\MSCOREE.DLL"
    Set-StringProperty -Path $packageKey -Name 'Class' -Value 'RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage'
    Set-StringProperty -Path $packageKey -Name 'CodeBase' -Value $packageDll
    New-ItemProperty -Path $packageKey -Name 'AllowsBackgroundLoad' -Value 1 -PropertyType DWord -Force | Out-Null

    Remove-AutoLoadRegistration -ConfigRoot $configRoot

    $bindingKey = Join-Path $configRoot "BindingPaths\$packageGuid"
    if (Test-Path -LiteralPath $bindingKey) {
        Remove-Item -LiteralPath $bindingKey -Recurse -Force
    }
    New-Item -Path $bindingKey -Force | Out-Null
    Set-StringProperty -Path $bindingKey -Name $packageFolder -Value ''

    $menuKey = Join-Path $configRoot 'Menus'
    New-Item -Path $menuKey -Force | Out-Null
    Set-StringProperty -Path $menuKey -Name $packageGuid -Value ', Menus.ctmenu, 1'

    $toolWindowKey = Join-Path $configRoot "ToolWindows\$windowGuid"
    New-Item -Path $toolWindowKey -Force | Out-Null
    Set-StringProperty -Path $toolWindowKey -Name '(default)' -Value $packageGuid
    Set-StringProperty -Path $toolWindowKey -Name 'Name' -Value 'RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindow'
    New-ItemProperty -Path $toolWindowKey -Name 'Style' -Value 4 -PropertyType DWord -Force | Out-Null
    Set-StringProperty -Path $toolWindowKey -Name 'Window' -Value $solutionExplorerGuid
    New-ItemProperty -Path $toolWindowKey -Name 'Orientation' -Value 3 -PropertyType DWord -Force | Out-Null
}

Write-Host "Visual Studio instance: $instanceId"
Write-Host "Raw Buffer Visualizer package: $packageDll"
Write-Host 'Repair completed. Restart Visual Studio before testing.'

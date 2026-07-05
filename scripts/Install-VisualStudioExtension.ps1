[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('net472')]
    [string]$Framework = 'net472',
    [string]$Configuration = 'Debug',
    [string]$ViewerFramework = 'net472',
    [string]$VsixInstallerPath = '',
    [switch]$NoBuild,
    [switch]$NoViewerEnv,
    [switch]$Reinstall
)

$ErrorActionPreference = 'Stop'

$extensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f'
$toolWindowExtensionId = 'RawBufferVisualizer.VisualStudio.Vssdk'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publishScript = Join-Path $repoRoot 'scripts\Publish-VisualStudioExtension.ps1'
$vsixPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Extensibility.vsix"

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

Push-Location $repoRoot
try {
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

$installer = Find-VsixInstaller -ExplicitPath $VsixInstallerPath
Write-Host "VSIXInstaller: $installer"

if ($Reinstall) {
    Invoke-VsixInstaller -InstallerPath $installer -Arguments @('/quiet', "/uninstall:$extensionId") -Action 'Uninstall Raw Buffer Visualizer VSIX' -AllowFailure
    Invoke-VsixInstaller -InstallerPath $installer -Arguments @('/quiet', "/uninstall:$toolWindowExtensionId") -Action 'Uninstall legacy split OpenGL ToolWindow VSIX' -AllowFailure
}

Invoke-VsixInstaller -InstallerPath $installer -Arguments @('/quiet', $vsixPath) -Action 'Install Raw Buffer Visualizer VSIX'
if ($WhatIfPreference) {
    Write-Host 'WhatIf completed: no VSIX changes were made.'
}
else {
    Write-Host "Installed: $vsixPath"
    Write-Host 'Restart Visual Studio before testing the debugger visualizer.'
}

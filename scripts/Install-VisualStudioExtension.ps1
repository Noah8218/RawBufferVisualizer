[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Framework = 'net8.0-windows',
    [string]$Configuration = 'Debug',
    [string]$ViewerFramework = 'net472',
    [string]$VsixInstallerPath = '',
    [switch]$NoBuild,
    [switch]$NoViewerEnv,
    [switch]$Reinstall
)

$ErrorActionPreference = 'Stop'

$extensionId = 'RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$viewerProject = Join-Path $repoRoot 'src\RawBufferVisualizer.Wpf\RawBufferVisualizer.Wpf.csproj'
$publishScript = Join-Path $repoRoot 'scripts\Publish-VisualStudioExtension.ps1'
$viewerExe = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.Wpf\$Configuration\$ViewerFramework\RawBufferVisualizer.Wpf.exe"
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
        [string]$Action
    )

    if (-not $PSCmdlet.ShouldProcess($Action, "$InstallerPath $($Arguments -join ' ')")) {
        return
    }

    $process = Start-Process -FilePath $InstallerPath -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "$Action failed with exit code $($process.ExitCode). Close Visual Studio and retry."
    }
}

Push-Location $repoRoot
try {
    if (-not $NoBuild) {
        & dotnet build $viewerProject --configuration $Configuration --framework $ViewerFramework
        if ($LASTEXITCODE -ne 0) {
            throw "Viewer build failed with exit code $LASTEXITCODE"
        }

        & powershell -ExecutionPolicy Bypass -File $publishScript -Framework $Framework -Configuration $Configuration -NoZip
        if ($LASTEXITCODE -ne 0) {
            throw "Visual Studio extension publish failed with exit code $LASTEXITCODE"
        }
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $viewerExe)) {
    throw "Viewer exe was not found: $viewerExe"
}

if (-not (Test-Path -LiteralPath $vsixPath)) {
    throw "VSIX was not found: $vsixPath"
}

if (-not $NoViewerEnv) {
    [Environment]::SetEnvironmentVariable('RAW_BUFFER_VISUALIZER_VIEWER', $viewerExe, 'User')
    $env:RAW_BUFFER_VISUALIZER_VIEWER = $viewerExe
    Write-Host "RAW_BUFFER_VISUALIZER_VIEWER=$viewerExe"
}

$installer = Find-VsixInstaller -ExplicitPath $VsixInstallerPath
Write-Host "VSIXInstaller: $installer"

if ($Reinstall) {
    Invoke-VsixInstaller -InstallerPath $installer -Arguments @('/quiet', "/uninstall:$extensionId") -Action 'Uninstall Raw Buffer Visualizer VSIX'
}

Invoke-VsixInstaller -InstallerPath $installer -Arguments @('/quiet', $vsixPath) -Action 'Install Raw Buffer Visualizer VSIX'
if ($WhatIfPreference) {
    Write-Host 'WhatIf completed: no VSIX changes were made.'
}
else {
    Write-Host "Installed: $vsixPath"
    Write-Host 'Restart Visual Studio before testing the debugger visualizer.'
}

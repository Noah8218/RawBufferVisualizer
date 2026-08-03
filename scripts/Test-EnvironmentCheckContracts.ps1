[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$xamlPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferToolWindowControl.xaml'
$controlPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferToolWindowControl.xaml.cs'
$servicePath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio\VisualizerEnvironmentCheck.cs'
$testPath = Join-Path $repoRoot 'tests\RawBufferVisualizer.Tests\VisualizerEnvironmentCheckTests.cs'

function Assert-Contains {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Description
    )

    if ($Content -notmatch $Pattern) {
        throw "$Description is missing or stale."
    }
}

foreach ($path in @($xamlPath, $controlPath, $servicePath, $testPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Environment Check contract file was not found: $path"
    }
}

[xml](Get-Content -LiteralPath $xamlPath -Raw) | Out-Null
$xaml = Get-Content -LiteralPath $xamlPath -Raw
$control = Get-Content -LiteralPath $controlPath -Raw
$service = Get-Content -LiteralPath $servicePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw

Assert-Contains $xaml 'x:Name="EnvironmentCheckPanel"[\s\S]*?Visibility="Collapsed"' 'Collapsed-by-default panel'
Assert-Contains $xaml 'AutomationProperties\.AutomationId="EnvironmentCheckOpenButton"' 'Environment open automation ID'
Assert-Contains $xaml 'AutomationProperties\.AutomationId="EnvironmentCheckRefreshButton"' 'Environment refresh automation ID'
Assert-Contains $xaml 'AutomationProperties\.AutomationId="EnvironmentCheckCopyReportButton"' 'Environment report automation ID'
Assert-Contains $xaml 'AutomationProperties\.AutomationId="EnvironmentCheckCloseButton"' 'Environment close automation ID'
Assert-Contains $xaml '<WrapPanel Margin="0,8,0,0">[\s\S]*?AutomationProperties\.AutomationId="EnvironmentCheckRefreshButton"[\s\S]*?AutomationProperties\.AutomationId="EnvironmentCheckCopyReportButton"[\s\S]*?AutomationProperties\.AutomationId="EnvironmentCheckCloseButton"[\s\S]*?</WrapPanel>' 'Compact-safe environment action row'
foreach ($statusName in @(
    'EnvironmentVisualStudioText',
    'EnvironmentExtensionText',
    'EnvironmentTempStorageText',
    'EnvironmentDotNetText',
    'EnvironmentVisualStudioWorkloadText',
    'EnvironmentFfmpegText')) {
    Assert-Contains $xaml ([regex]::Escape("x:Name=`"$statusName`"")) "Status surface $statusName"
}
foreach ($actionId in @(
    'EnvironmentDotNetDownloadButton',
    'EnvironmentVisualStudioInstallerButton',
    'EnvironmentFfmpegGuideButton')) {
    Assert-Contains $xaml ([regex]::Escape("AutomationProperties.AutomationId=`"$actionId`"")) "Action surface $actionId"
}
Assert-Contains $xaml '<Trigger Property="IsKeyboardFocused" Value="True">' 'Keyboard focus visual'

$openHandler = [regex]::Match(
    $control,
    'private void EnvironmentCheck_Click[\s\S]*?(?=\r?\n\s*private void RefreshEnvironmentCheck_Click)').Value
if ([string]::IsNullOrWhiteSpace($openHandler)) {
    throw 'Environment Check open handler was not found.'
}
if ($openHandler -match 'ScanLocals|OpenPath|OpenHandoff|Process\.Start') {
    throw 'Environment Check open handler acquired a forbidden side effect.'
}

$refreshHandler = [regex]::Match(
    $control,
    'private void RefreshEnvironmentCheck\(\)[\s\S]*?(?=\r?\n\s*private void CloseEnvironmentCheck_Click)').Value
if ([string]::IsNullOrWhiteSpace($refreshHandler)) {
    throw 'Environment Check refresh handler was not found.'
}
if ($refreshHandler -match 'ScanLocals|OpenPath|OpenHandoff|Process\.Start') {
    throw 'Environment Check refresh handler acquired a forbidden side effect.'
}

Assert-Contains $control 'MessageBoxButton\.YesNo' 'External-action confirmation'
Assert-Contains $control 'MessageBoxResult\.No' 'Safe confirmation default'
Assert-Contains $service 'https://dotnet\.microsoft\.com/' 'Official .NET URL'
Assert-Contains $service 'https://learn\.microsoft\.com/' 'Official Visual Studio guidance URL'
Assert-Contains $service 'https://ffmpeg\.org/' 'Official FFmpeg URL'
if ($service -match 'Invoke-WebRequest|DownloadFile|HttpClient|WebClient|winget|Process\.Start') {
    throw 'Environment Check service must not download, install, or launch software.'
}

Assert-Contains $tests 'Credentials or environment-variable values included: No' 'Credential privacy assertion'
Assert-Contains $tests 'Image payload included: No' 'Image privacy assertion'
Assert-Contains $tests 'left a temporary probe file behind' 'Temporary probe cleanup assertion'

Write-Host 'Environment Check source/UI safety contracts passed.'

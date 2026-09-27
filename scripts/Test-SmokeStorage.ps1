[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$areas = @{
    'SmokeAutomaticScanResponsiveness.ps1' = 'automatic-scan'
    'SmokeHistogram.ps1' = 'histogram'
    'SmokeMappingSave.ps1' = 'mapping-save'
    'SmokeRenderInitialization.ps1' = 'render-initialization'
    'SmokeUsability.ps1' = 'usability'
    'SmokeViewerInteractions.ps1' = 'viewer-interactions'
}
$checks = 0
foreach ($name in $areas.Keys) {
    $path = Join-Path $PSScriptRoot $name
    $tokens = $null
    $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -ne 0) { throw "$name has PowerShell syntax errors: $parseErrors" }
    $content = Get-Content -LiteralPath $path -Raw
    $configuration = [regex]::Match($content, '(?m)^\$testRoot = [\s\S]*?(?=^New-Item|^Set-Location)').Value
    if ([string]::IsNullOrWhiteSpace($configuration)) { throw "$name has no isolated storage setup." }

    foreach ($hasD in @($true, $false)) {
        foreach ($custom in @($false, $true)) {
            $result = & {
                param($configuration, $hasD, $custom)
                # Simulate drive availability only; do not create folders or launch UI.
                function Test-Path { param([string]$LiteralPath) if ($LiteralPath -ne 'D:\') { throw "Unexpected path probe: $LiteralPath" }; return $hasD }
                $repoRoot = 'C:\isolated-checkout'
                $BuildRoot = if ($custom) { 'D:\custom-build' } else { '' }
                $OutputDir = if ($custom) { 'D:\custom-evidence' } else { '' }
                . ([scriptblock]::Create($configuration))
                [pscustomobject]@{ Build = $BuildRoot; Output = $OutputDir }
            } $configuration $hasD $custom
            $expectedRoot = if ($hasD) { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { 'C:\isolated-checkout\artifacts' }
            $expectedBuild = if ($custom) { 'D:\custom-build' } else { Join-Path $expectedRoot 'build' }
            $expectedOutput = if ($custom) { 'D:\custom-evidence' } else { Join-Path $expectedRoot ('ui\' + $areas[$name]) }
            if ($result.Build -ne $expectedBuild -or $result.Output -ne $expectedOutput) { throw "${name}: storage route mismatch (D=$hasD, custom=$custom)." }
            $checks++
        }
    }
}
Write-Host "Smoke storage: $checks configuration cases passed across $($areas.Count) scripts; no desktop windows launched."

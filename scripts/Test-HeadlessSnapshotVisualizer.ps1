[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VisualStudioRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [string]$DotNet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$output = [IO.Path]::GetFullPath($OutputRoot)
$build = Join-Path $output 'build'
$runtime = Join-Path $VisualStudioRoot 'Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.DebuggerVisualizers.dll'
$jsonRuntime = Join-Path $VisualStudioRoot 'Common7\Packages\Debugger\Visualizers\Newtonsoft.Json\net4.5'
if (-not (Test-Path -LiteralPath $runtime) -or -not (Test-Path -LiteralPath $jsonRuntime)) {
    throw 'The selected Visual Studio installation must supply the debugger visualizer and JSON runtimes.'
}

New-Item -ItemType Directory -Force -Path $output, "$output\temp", "$output\logs" | Out-Null
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    $env:TEMP = "$output\temp"
    $env:TMP = $env:TEMP
    $testProject = Join-Path $repoRoot 'tests\RawBufferVisualizer.Classic.Tests\RawBufferVisualizer.Classic.Tests.csproj'
    & $DotNet build $testProject -c Release -p:DynamicVisualizerRegistration=true "-p:RawBufferVisualizerBuildRoot=$build" /nodeReuse:false -v:minimal *> "$output\logs\build.log"
    if ($LASTEXITCODE -ne 0) { throw "Classic build failed. See $output\logs\build.log" }
    $testOutput = Join-Path $build 'bin\RawBufferVisualizer.Classic.Tests\Release\net472'
    Copy-Item -LiteralPath $runtime -Destination $testOutput -Force
    New-Item -ItemType Directory -Force -Path "$testOutput\Newtonsoft.Json" | Out-Null
    Copy-Item -LiteralPath $jsonRuntime -Destination "$testOutput\Newtonsoft.Json" -Recurse -Force
    & "$testOutput\RawBufferVisualizer.Classic.Tests.exe" *> "$output\logs\tests.log"
    if ($LASTEXITCODE -ne 0) { throw "Classic tests failed. See $output\logs\tests.log" }
    Get-Content -LiteralPath "$output\logs\tests.log"

    & "$testOutput\RawBufferVisualizer.Classic.Tests.exe" --collection *> "$output\logs\collection-tests.log"
    if ($LASTEXITCODE -ne 0) { throw "Collection tests failed. See $output\logs\collection-tests.log" }
    Get-Content -LiteralPath "$output\logs\collection-tests.log"

    $objectSourceProject = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.ObjectSource\RawBufferVisualizer.VisualStudio.ObjectSource.csproj'
    & $DotNet build $objectSourceProject -c Release -f netstandard2.0 "-p:RawBufferVisualizerBuildRoot=$build" /nodeReuse:false -v:minimal *> "$output\logs\object-source-build.log"
    if ($LASTEXITCODE -ne 0) { throw "ObjectSource build failed. See $output\logs\object-source-build.log" }

    # Staging only: no installed VSIX, debugger registration, or running IDE is changed.
    $stage = Join-Path $output 'Visualizers'
    New-Item -ItemType Directory -Force -Path $stage, "$stage\netstandard2.0" | Out-Null
    $classicOutput = Join-Path $build 'bin\RawBufferVisualizer.VisualStudio.Classic\Release\net472'
    foreach ($name in @('RawBufferVisualizer.VisualStudio.Classic.dll', 'RawBufferVisualizer.Core.dll',
        'RawBufferVisualizer.Sdk.dll', 'RawBufferVisualizer.VisualStudio.dll', 'RawBufferVisualizer.VisualStudio.ObjectSource.dll')) {
        Copy-Item -LiteralPath (Join-Path $classicOutput $name) -Destination $stage -Force
    }
    $sourceOutput = Join-Path $build 'bin\RawBufferVisualizer.VisualStudio.ObjectSource\Release\netstandard2.0'
    foreach ($name in @('RawBufferVisualizer.Core.dll', 'RawBufferVisualizer.Sdk.dll',
        'RawBufferVisualizer.VisualStudio.ObjectSource.dll', 'RawBufferVisualizer.VisualStudio.ObjectSource.deps.json')) {
        Copy-Item -LiteralPath (Join-Path $sourceOutput $name) -Destination "$stage\netstandard2.0" -Force
    }
    Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($stage.Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    } | ConvertTo-Json | Set-Content -LiteralPath "$output\payload.json" -Encoding UTF8
    Write-Output "Prepared transfer-only visualizers with self-targeted install-path registrations: $stage. Runtime Visual Studio qualification remains required."
}
finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}

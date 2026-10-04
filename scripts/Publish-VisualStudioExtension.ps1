[CmdletBinding()]
param(
    [ValidateSet('net472')]
    [string]$Framework = 'net472',
    [string]$Configuration = 'Release',
    [string]$ViewerFramework = 'net472',
    [string]$PublishRoot = '',
    [string]$BuildRoot = '',
    [string]$DirectoryBuildPropsPath = '',
    [switch]$NoBuild,
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj'
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $buildRoot = Join-Path $repoRoot '.build'
}
else {
    $buildRoot = [IO.Path]::GetFullPath($BuildRoot)
}
if (-not [string]::IsNullOrWhiteSpace($DirectoryBuildPropsPath)) {
    $directoryBuildPropsPath = [IO.Path]::GetFullPath($DirectoryBuildPropsPath)
    if (-not (Test-Path -LiteralPath $directoryBuildPropsPath)) {
        throw "Directory build props file was not found: $directoryBuildPropsPath"
    }
}
if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    $publishRoot = Join-Path $repoRoot 'artifacts\publish'
}
else {
    $publishRoot = [IO.Path]::GetFullPath($PublishRoot)
}
$packageName = "RawBufferVisualizer-VisualStudioExtensibility-$Framework"
$publishDir = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"
$buildOutput = Join-Path $buildRoot "bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework"
$providerOutput = Join-Path $buildRoot "bin\RawBufferVisualizer.VisualStudio.Extensibility\$Configuration\net8.0-windows8.0"
$vsixPath = Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Extensibility.vsix'

function Get-VsixEntryNames {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Message`: $Path"
    }
}

function Assert-DebuggerVisualizerTargetTypes {
    param([string]$ExtensionJsonPath)

    $extension = Get-Content -Raw -LiteralPath $ExtensionJsonPath | ConvertFrom-Json
    $providerCount = 0
    foreach ($part in @($extension.parts)) {
        if ($part.contract -ne 'Microsoft.VisualStudio.RpcContracts.DebuggerVisualizers.IDebuggerVisualizerProvider') {
            continue
        }

        $providerCount++
        foreach ($metadata in @($part.metadata)) {
            foreach ($target in @($metadata.values.targets)) {
                $targetType = [string]$target.targetType
                if ([string]::IsNullOrWhiteSpace($targetType)) {
                    throw "Debugger visualizer targetType is empty in $ExtensionJsonPath"
                }

                if ($targetType.IndexOf(',') -lt 0) {
                    throw "Debugger visualizer targetType must include an assembly name: '$targetType'"
                }

                if ($targetType -like 'Cressem.ImageModel.ImagePtr,*' -and $targetType -notlike '*Version=*') {
                    throw "ImagePtr targetType must be fully assembly-qualified: '$targetType'"
                }
            }
        }
    }

    if ($providerCount -eq 0) {
        throw "VSIX contains no debugger visualizer providers: $ExtensionJsonPath"
    }
}

function Get-VsixEntryText {
    param(
        [string]$Path,
        [string]$EntryName
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.Entries |
            Where-Object { $_.FullName -eq $EntryName } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "VSIX entry was not found: $EntryName"
        }

        $reader = New-Object IO.StreamReader($entry.Open())
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Get-VsixEntrySha256 {
    param(
        [string]$Path,
        [string]$EntryName
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.Entries |
            Where-Object { $_.FullName -eq $EntryName } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "VSIX entry was not found: $EntryName"
        }

        $stream = $entry.Open()
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $sha256.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-ModernDebuggerVisualizerProvidersPresent {
    param([string]$ExtensionJsonPath)

    $extensionJson = Get-Content -Raw -LiteralPath $ExtensionJsonPath
    foreach ($provider in @(
        'RawBufferSnapshotDebuggerVisualizerProvider',
        'RawBufferViewDebuggerVisualizerProvider',
        'BitmapDebuggerVisualizerProvider',
        'OpenCvSharpMatDebuggerVisualizerProvider',
        'EmguCvMatDebuggerVisualizerProvider',
        'ImagePtrDebuggerVisualizerProvider',
        'ImageCollectionDebuggerVisualizerProvider'
    )) {
        if ($extensionJson -notmatch [regex]::Escape($provider)) {
            throw "Required Marketplace debugger visualizer is missing: $provider"
        }
    }

    if ($extensionJson -notmatch [regex]::Escape('IDebuggerVisualizerProvider')) {
        throw 'Marketplace package contains no debugger visualizer contract.'
    }

    foreach ($targetType in @(
        'OpenCvSharp.Mat, OpenCvSharp, Version=1.0.0.0',
        'OpenCvSharp.Mat, OpenCvSharp, Version=4.0.0.0',
        'Emgu.CV.Mat, Emgu.CV.World, Version=3.4.3.3016',
        'Emgu.CV.Mat, Emgu.CV.World.NetStandard, Version=1.0.0.0',
        'Emgu.CV.Mat, Emgu.CV.Platform.NetStandard, Version=4.5.5.4823',
        'Emgu.CV.Mat, Emgu.CV, Version=4.8.1.5350',
        'Emgu.CV.Mat, Emgu.CV, Version=4.13.0.5924',
        'System.Collections.Generic.List`1, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.Dictionary`2, mscorlib, Version=4.0.0.0',
        'System.Collections.Concurrent.ConcurrentDictionary`2, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.List`1, System.Private.CoreLib',
        'System.Collections.Generic.Dictionary`2, System.Private.CoreLib',
        'System.Collections.Concurrent.ConcurrentDictionary`2, System.Collections.Concurrent',
        'System.Drawing.Bitmap[], System.Drawing, Version=4.0.0.0',
        'System.Drawing.Bitmap[], System.Drawing.Common, Version=8.0.0.0',
        'System.Drawing.Bitmap[], System.Drawing.Common, Version=10.0.0.0',
        'OpenCvSharp.Mat[], OpenCvSharp, Version=1.0.0.0',
        'OpenCvSharp.Mat[], OpenCvSharp, Version=4.0.0.0',
        'Emgu.CV.Mat[], Emgu.CV.World, Version=3.4.3.3016',
        'Emgu.CV.Mat[], Emgu.CV.World.NetStandard, Version=1.0.0.0',
        'Emgu.CV.Mat[], Emgu.CV.Platform.NetStandard, Version=4.5.5.4823',
        'Emgu.CV.Mat[], Emgu.CV, Version=4.8.1.5350',
        'Emgu.CV.Mat[], Emgu.CV, Version=4.13.0.5924'
    )) {
        if ($extensionJson -notmatch [regex]::Escape($targetType)) {
            throw "Required debugger visualizer target is missing: $targetType"
        }
    }
}

function Assert-ModernCollectionRegistrationsOpen {
    param([string]$SourcePath)

    $source = Get-Content -Raw -LiteralPath $SourcePath
    foreach ($requiredRegistration in @(
        'typeof(List<>)',
        'typeof(Dictionary<,>)',
        'typeof(ConcurrentDictionary<,>)',
        'typeof(object[])',
        'System.Collections.Generic.List`1, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.Dictionary`2, mscorlib, Version=4.0.0.0',
        'System.Collections.Concurrent.ConcurrentDictionary`2, mscorlib, Version=4.0.0.0',
        'System.Collections.Generic.List`1, System.Private.CoreLib',
        'System.Collections.Generic.Dictionary`2, System.Private.CoreLib',
        'System.Collections.Concurrent.ConcurrentDictionary`2, System.Collections.Concurrent',
        'System.Drawing.Bitmap[], System.Drawing, Version=4.0.0.0',
        'System.Drawing.Bitmap[], System.Drawing.Common, Version=8.0.0.0',
        'System.Drawing.Bitmap[], System.Drawing.Common, Version=10.0.0.0',
        'OpenCvSharp.Mat[], OpenCvSharp, Version=1.0.0.0',
        'OpenCvSharp.Mat[], OpenCvSharp, Version=4.0.0.0',
        'Emgu.CV.Mat[], Emgu.CV.World, Version=3.4.3.3016',
        'Emgu.CV.Mat[], Emgu.CV.World.NetStandard, Version=1.0.0.0',
        'Emgu.CV.Mat[], Emgu.CV.Platform.NetStandard, Version=4.5.5.4823',
        'Emgu.CV.Mat[], Emgu.CV, Version=4.8.1.5350',
        'Emgu.CV.Mat[], Emgu.CV, Version=4.13.0.5924'
    )) {
        if (-not $source.Contains($requiredRegistration)) {
            throw "Required collection visualizer registration is missing: $requiredRegistration"
        }
    }
}

function Assert-VssdkReferenceCompatibility {
    param([string]$AssemblyPath)

    $qualifiedThreadingVersion = [Version]'17.9.0.0'
    $references = [Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath).GetReferencedAssemblies()
    $threading = $references | Where-Object { $_.Name -eq 'Microsoft.VisualStudio.Threading' } | Select-Object -First 1
    if ($null -eq $threading) {
        throw "VSSDK package does not reference Microsoft.VisualStudio.Threading: $AssemblyPath"
    }

    if ($threading.Version -ne $qualifiedThreadingVersion) {
        throw "VSSDK package references Microsoft.VisualStudio.Threading $($threading.Version), but the qualified Visual Studio 2022 floor requires $qualifiedThreadingVersion. Requalify every supported Visual Studio generation before changing this dependency."
    }
}

function Assert-VssdkPackageLifecycleContract {
    param([string]$SourcePath)

    $source = Get-Content -Raw -LiteralPath $SourcePath
    if (-not $source.Contains('[ProvideAutoLoad(UIContextGuids80.Debugging, PackageAutoLoadFlags.BackgroundLoad)]')) {
        throw 'The VSSDK package must preload asynchronously while debugging so the first debugger visualizer does not trigger a cyclic package load.'
    }

    $initializeStart = $source.IndexOf('protected override async Task InitializeAsync', [StringComparison]::Ordinal)
    $initializeEnd = $source.IndexOf('protected override void Dispose', $initializeStart, [StringComparison]::Ordinal)
    if ($initializeStart -lt 0 -or $initializeEnd -le $initializeStart) {
        throw "Could not locate the VSSDK package initialization body: $SourcePath"
    }

    $initializeBody = $source.Substring($initializeStart, $initializeEnd - $initializeStart)
    foreach ($forbiddenCall in @('StartInboxWatcher();', 'ScanInbox()', 'ShowRawBufferToolWindowAsync')) {
        if ($initializeBody.Contains($forbiddenCall)) {
            throw "VSSDK package initialization must not call '$forbiddenCall'; opening a ToolWindow while its package is loading causes VS_E_CYCLICPACKAGELOAD."
        }
    }

    $monitoringStart = $initializeBody.IndexOf('EnsureInboxMonitoringStarted();', [StringComparison]::Ordinal)
    $initializeCompleteLog = $initializeBody.IndexOf('WriteAutomationLog("InitializeAsync end")', [StringComparison]::Ordinal)
    if ($monitoringStart -lt 0 -or $initializeCompleteLog -le $monitoringStart) {
        throw 'The VSSDK package must start passive inbox monitoring before initialization completes, without scanning or opening the ToolWindow.'
    }

    $showStart = $source.IndexOf('private async Task<RawBufferToolWindow> ShowRawBufferToolWindowAsync', [StringComparison]::Ordinal)
    $showEnd = $source.IndexOf('private async Task OpenHandoffAsync', $showStart, [StringComparison]::Ordinal)
    if ($showStart -lt 0 -or $showEnd -le $showStart) {
        throw "Could not locate the ToolWindow open path: $SourcePath"
    }

    $showBody = $source.Substring($showStart, $showEnd - $showStart)
    $frameShow = $showBody.IndexOf('frame.Show()', [StringComparison]::Ordinal)
    $showMonitoringStart = $showBody.IndexOf('EnsureInboxMonitoringStarted();', [StringComparison]::Ordinal)
    if ($frameShow -lt 0 -or $showMonitoringStart -le $frameShow) {
        throw 'Inbox monitoring must start only after the docked ToolWindow frame has been shown.'
    }

    foreach ($forbiddenCleanup in @('ScheduleDebuggerVisualizerHostCleanup', 'CloseDuplicateDebuggerVisualizerHostsAsync', 'CloseFrame(')) {
        if ($source.Contains($forbiddenCleanup)) {
            throw "The VSSDK package must not close same-caption debugger visualizer frames through '$forbiddenCleanup'; VisualizerTarget disposal owns the temporary debugger host lifetime."
        }
    }
}

function Assert-VisualStudioCompatibilityContract {
    param([xml]$Manifest)

    if ([string]$Manifest.PackageManifest.Metadata.Preview -ne 'false') {
        throw 'The stable VSIX must explicitly declare Preview=false so Marketplace searches that exclude Preview extensions can include it.'
    }

    $installationTargets = @($Manifest.PackageManifest.Installation.InstallationTarget)
    $expectedIds = @(
        'Microsoft.VisualStudio.Community',
        'Microsoft.VisualStudio.Pro',
        'Microsoft.VisualStudio.Enterprise'
    )
    foreach ($expectedId in $expectedIds) {
        $target = $installationTargets |
            Where-Object { [string]$_.Id -eq $expectedId } |
            Select-Object -First 1
        if ($null -eq $target) {
            throw "VSIX manifest is missing installation target '$expectedId'."
        }
        if ([string]$target.Version -ne '[17.9,18.0)') {
            throw "VSIX installation target '$expectedId' must declare [17.9,18.0), but found '$($target.Version)'."
        }
        if ([string]$target.ProductArchitecture -ne 'amd64') {
            throw "VSIX installation target '$expectedId' must be amd64."
        }
    }

    $coreEditor = @($Manifest.PackageManifest.Prerequisites.Prerequisite) |
        Where-Object { [string]$_.Id -eq 'Microsoft.VisualStudio.Component.CoreEditor' } |
        Select-Object -First 1
    if ($null -eq $coreEditor -or [string]$coreEditor.Version -ne '[17.9,)') {
        $actualVersion = if ($null -eq $coreEditor) { '<missing>' } else { [string]$coreEditor.Version }
        throw "VSIX CoreEditor prerequisite must declare [17.9,), but found '$actualVersion'."
    }
}

function Get-CompiledInstructions {
    param([Reflection.MethodBase]$Method)

    $codes = @{}
    foreach ($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
        if ($field.FieldType -eq [Reflection.Emit.OpCode]) {
            $code = $field.GetValue($null)
            $codes[[int]$code.Value -band 0xffff] = $code
        }
    }
    $body = $Method.GetMethodBody()
    if ($null -eq $body) { return }
    $bytes = $body.GetILAsByteArray()
    $cursor = 0
    while ($cursor -lt $bytes.Length) {
        $value = [int]$bytes[$cursor++]
        if ($value -eq 0xfe) { $value = 0xfe00 -bor [int]$bytes[$cursor++] }
        $code = $codes[$value]
        $operand = $null
        switch ($code.OperandType.ToString()) {
            'InlineMethod' { $operand = $Method.Module.ResolveMethod([BitConverter]::ToInt32($bytes, $cursor)) }
            'InlineField' { $operand = $Method.Module.ResolveField([BitConverter]::ToInt32($bytes, $cursor)) }
            'InlineString' { $operand = $Method.Module.ResolveString([BitConverter]::ToInt32($bytes, $cursor)) }
        }
        [pscustomobject]@{ Code = $code.Name; Operand = $operand }
        switch ($code.OperandType.ToString()) {
            'InlineNone' { }
            { $_ -in @('ShortInlineBrTarget','ShortInlineI','ShortInlineVar') } { $cursor++ }
            'InlineVar' { $cursor += 2 }
            { $_ -in @('InlineI8','InlineR') } { $cursor += 8 }
            'InlineSwitch' { $cursor += 4 + 4 * [BitConverter]::ToInt32($bytes, $cursor) }
            default { $cursor += 4 }
        }
    }
}

function Assert-ClassicVisualizerInstallPathContract {
    param(
        [string]$SourcePath,
        [string]$SourceManifestPath,
        [string]$GeneratedManifestPath,
        [string]$ClassicAssemblyPath,
        [string]$DebuggerVisualizersReferencePath,
        [string]$DebuggerAssemblyPath,
        [string]$EngineReferencePath,
        [string]$MetadataReferencePath
    )

    $source = Get-Content -Raw -LiteralPath $SourcePath
    if (-not $source.Contains('ClassicAssemblyIdentity.Value, DkmClrCustomVisualizerAssemblyLocation.Extension')) {
        throw 'Classic candidates must use Extension location so the debuggee ObjectSource probes the VSIX installation folder. ResolveAssembly alone does not provide this path.'
    }
    if ($source.Contains('DkmClrCustomVisualizerAssemblyLocation.Debuggee')) {
        throw 'The extension-owned Classic visualizer must not be registered as a debuggee assembly.'
    }

    foreach ($path in @($SourceManifestPath, $GeneratedManifestPath)) {
        [xml]$manifest = Get-Content -Raw -LiteralPath $path
        $assets = @($manifest.PackageManifest.Assets.Asset | Where-Object {
            [string]$_.Type -eq 'Microsoft.VisualStudio.DotnetCustomVisualizer' -and
            [string]$_.Path -eq 'RawBufferVisualizer.VisualStudio.Classic.dll'
        })
        if ($assets.Count -ne 1) {
            throw "Exactly one Classic DotnetCustomVisualizer asset is required for installation-path lookup: $path"
        }
    }

    # SDK packages contain reference assemblies, so inspect compiled IL without executing them.
    [void][Reflection.Assembly]::ReflectionOnlyLoadFrom($MetadataReferencePath)
    [void][Reflection.Assembly]::ReflectionOnlyLoadFrom($EngineReferencePath)
    [void][Reflection.Assembly]::ReflectionOnlyLoadFrom($DebuggerVisualizersReferencePath)
    $assemblyDirectory = Split-Path -Parent $ClassicAssemblyPath
    $resolver = [ResolveEventHandler] {
        param($sender, $eventArgs)
        $name = [Reflection.AssemblyName]::new($eventArgs.Name).Name
        $candidate = Join-Path $assemblyDirectory ($name + '.dll')
        if (Test-Path -LiteralPath $candidate) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate) }
        return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
    }.GetNewClosure()
    [AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
    try {
        $debugger = [Reflection.Assembly]::ReflectionOnlyLoadFrom($DebuggerAssemblyPath)
        $provider = $debugger.GetType('RawBufferVisualizer.VisualStudio.Debugger.ImageVisualizerResultProvider', $true)
        $field = $provider.GetField('ClassicAssemblyIdentity', [Reflection.BindingFlags]'NonPublic,Static')
        $reader = $provider.GetMethod('ReadClassicAssemblyIdentity', [Reflection.BindingFlags]'NonPublic,Static')
        if ($null -eq $field -or $null -eq $reader) {
            throw 'Compiled Classic candidate must read the full assembly identity used by VS installation-path lookup.'
        }
        $instructions = @(Get-CompiledInstructions -Method $reader)
        $calls = @($instructions | Where-Object { $_.Operand -is [Reflection.MethodBase] } | ForEach-Object { $_.Operand.DeclaringType.FullName + '.' + $_.Operand.Name })
        foreach ($required in @('System.Reflection.AssemblyName.GetAssemblyName', 'System.Reflection.AssemblyName.get_FullName', 'System.Reflection.Assembly.get_Location', 'System.IO.Path.Combine')) {
            if ($required -notin $calls) { throw "Compiled Classic identity reader is missing $required." }
        }
        if ('RawBufferVisualizer.VisualStudio.Classic.dll' -notin @($instructions | ForEach-Object { $_.Operand })) {
            throw 'Compiled candidate must read the adjacent Classic DLL rather than infer or hard-code its identity.'
        }
        $initializer = @(Get-CompiledInstructions -Method $provider.TypeInitializer)
        if (@($initializer | Where-Object { $_.Code -eq 'ldftn' -and $_.Operand.Name -eq $reader.Name }).Count -ne 1 -or
            @($initializer | Where-Object { $_.Code -eq 'stsfld' -and $_.Operand.Name -eq $field.Name }).Count -ne 1) {
            throw 'Compiled Classic identity cache is not initialized by the DLL identity reader.'
        }
        $candidateReads = 0
        foreach ($type in @($provider) + @($provider.GetNestedTypes([Reflection.BindingFlags]'Public,NonPublic'))) {
            foreach ($method in $type.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
                $ops = @(Get-CompiledInstructions -Method $method)
                $creates = @($ops | Where-Object { $_.Code -eq 'call' -and $_.Operand.DeclaringType.FullName -eq 'Microsoft.VisualStudio.Debugger.Evaluation.DkmCustomUIVisualizerInfo' -and $_.Operand.Name -eq 'Create' -and $_.Operand.GetParameters().Length -eq 9 })
                if ($creates.Count -gt 0) {
                    if (@($ops | Where-Object { $_.Code -eq 'ldsfld' -and $_.Operand.Name -eq $field.Name }).Count -ne 1) {
                        throw 'Compiled owned candidate must use the full Classic identity cache.'
                    }
                    $candidateReads += $creates.Count
                }
            }
        }
        if ($candidateReads -ne 1) { throw 'Could not verify the compiled owned-candidate identity route.' }
        $classic = [Reflection.Assembly]::ReflectionOnlyLoadFrom($ClassicAssemblyPath)
        $attributes = @($classic.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Diagnostics.DebuggerVisualizerAttribute' })
        if ($attributes.Count -ne 2) { throw 'Compiled Classic DLL must register exactly two self-targeted UI IDs for installation-path lookup.' }
        $expected = @{
            'RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer' = 'RawBufferVisualizer.VisualStudio.ObjectSource.RawBufferSnapshotVisualizerObjectSource'
            'RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer' = 'RawBufferVisualizer.VisualStudio.ObjectSource.ImageCollectionVisualizerObjectSource'
        }
        foreach ($attribute in $attributes) {
            # Invoke getters explicitly: PowerShell can suppress a property-getter load failure.
            $arguments = $attribute.GetType().GetProperty('ConstructorArguments').GetValue($attribute, $null)
            $named = $attribute.GetType().GetProperty('NamedArguments').GetValue($attribute, $null)
            $uiType = [string]$arguments[0].Value.FullName
            $sourceType = [string]$arguments[1].Value.FullName
            $target = @($named | Where-Object { $_.MemberName -eq 'Target' })
            if (-not $expected.ContainsKey($uiType) -or $expected[$uiType] -ne $sourceType -or
                $target.Count -ne 1 -or [string]$target[0].TypedValue.Value.FullName -ne $uiType) {
                throw "Invalid compiled Classic installation-path registration: $uiType"
            }
            $expected.Remove($uiType)
        }
        if ($expected.Count -ne 0) { throw 'A compiled Classic UI installation-path registration is missing.' }
    }
    finally { [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver) }
}

function Get-ProjectCompileReferencePath {
    param([string]$AssetsPath, [string]$PackageName)

    $assets = Get-Content -Raw -LiteralPath $AssetsPath -Encoding UTF8 | ConvertFrom-Json
    $library = $assets.libraries.PSObject.Properties | Where-Object { $_.Name -like ($PackageName + '/*') } | Select-Object -First 1
    foreach ($target in $assets.targets.PSObject.Properties) {
        $dependency = $target.Value.PSObject.Properties | Where-Object { $_.Name -eq $library.Name } | Select-Object -First 1
        if ($null -eq $dependency) { continue }
        $reference = $dependency.Value.compile.PSObject.Properties | Where-Object { $_.Name -like ('*/' + $PackageName + '.dll') } | Select-Object -First 1
        foreach ($folder in $assets.packageFolders.PSObject.Properties) {
            $path = Join-Path (Join-Path $folder.Name $library.Value.path) $reference.Name
            if (Test-Path -LiteralPath $path -PathType Leaf) { return $path }
        }
    }
    throw "The restored $PackageName reference was not found: $AssetsPath"
}

function Assert-HybridVssdkRegistration {
    param(
        [string]$PkgdefPath,
        [string]$GeneratedManifestPath,
        [string]$SourceManifestPath
    )

    $pkgdef = Get-Content -Raw -LiteralPath $PkgdefPath
    foreach ($requiredRegistration in @(
        '[$RootKey$\Packages\{1977574b-f107-465f-bfd1-5fc022907039}]',
        '"Class"="RawBufferVisualizer.VisualStudio.Vssdk.RawBufferVisualizerPackage"',
        '"CodeBase"="$PackageFolder$\RawBufferVisualizer.VisualStudio.Vssdk.dll"',
        '[$RootKey$\Menus]',
        '"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"',
        '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
    )) {
        if (-not $pkgdef.Contains($requiredRegistration)) {
            throw "Hybrid VSSDK pkgdef is missing registration '$requiredRegistration': $PkgdefPath"
        }
    }

    if ($pkgdef.Contains('RawBufferVisualizer.VisualStudio.Extensibility.dll')) {
        throw "The in-process VSSDK package must not be owned by the newer out-of-process Extensibility assembly: $PkgdefPath"
    }

    if ($pkgdef.Contains('{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}')) {
        throw "The retired 1.0.47/1.0.48 package GUID must not remain in the recovery VSIX registration: $PkgdefPath"
    }

    foreach ($singleRegistration in @(
        '[$RootKey$\Packages\{1977574b-f107-465f-bfd1-5fc022907039}]',
        '"{1977574b-f107-465f-bfd1-5fc022907039}"=", Menus.ctmenu, 2"',
        '[$RootKey$\ToolWindows\{a329e331-089a-4186-8fd7-57a241fd1917}]'
    )) {
        if ([regex]::Matches($pkgdef, [regex]::Escape($singleRegistration)).Count -ne 1) {
            throw "Hybrid VSSDK pkgdef must contain exactly one registration '$singleRegistration': $PkgdefPath"
        }
    }

    $generatedManifest = Get-Content -Raw -LiteralPath $GeneratedManifestPath
    if (-not $generatedManifest.Contains('Type="Microsoft.VisualStudio.VsPackage" Path="RawBufferVisualizer.VisualStudio.Vssdk.pkgdef"')) {
        throw "Generated VSIX manifest does not reference the isolated VSSDK pkgdef: $GeneratedManifestPath"
    }

    $sourceManifest = Get-Content -Raw -LiteralPath $SourceManifestPath
    if (-not $sourceManifest.Contains('Path="RawBufferVisualizer.VisualStudio.Vssdk.pkgdef"')) {
        throw "Source VSIX manifest must reference the isolated VSSDK pkgdef: $SourceManifestPath"
    }
    foreach ($manifestText in @($sourceManifest, $generatedManifest)) {
        if ($manifestText -notmatch 'Type="DebuggerEngineExtension"\s+Path="RawBufferVisualizer\.VisualStudio\.Debugger\.vsdconfig"') {
            throw 'VSIX manifest must register the debugger routing component.'
        }
    }
}

if ($ViewerFramework -ne 'net472') {
    throw 'The Visual Studio ToolWindow is packaged into the single net472 hybrid VSIX. Use -ViewerFramework net472 or omit it.'
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

if (-not $NoBuild) {
    Push-Location $repoRoot
    try {
        $buildArguments = @(
            $project,
            '--configuration',
            $Configuration,
            '/nodeReuse:false',
            "-p:RawBufferVisualizerBuildRoot=$buildRoot"
        )
        if (-not [string]::IsNullOrWhiteSpace($DirectoryBuildPropsPath)) {
            $buildArguments += "-p:DirectoryBuildPropsPath=$directoryBuildPropsPath"
        }

        & dotnet build @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Visual Studio extension build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

$extensionJsonPath = Join-Path $providerOutput '.vsextension\extension.json'
Assert-FileExists -Path $extensionJsonPath -Message 'Visual Studio extension metadata was not created'
Assert-DebuggerVisualizerTargetTypes -ExtensionJsonPath $extensionJsonPath
Assert-ModernDebuggerVisualizerProvidersPresent -ExtensionJsonPath $extensionJsonPath
Assert-ModernCollectionRegistrationsOpen -SourcePath (Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Extensibility\ImageCollectionDebuggerVisualizerProvider.cs')
Assert-VssdkPackageLifecycleContract -SourcePath (Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizerPackage.cs')
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef') -Message 'Isolated Visual Studio package registration was not created'
Assert-FileExists -Path (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.dll') -Message 'Visual Studio docked ToolWindow package DLL was not created'
Assert-VssdkReferenceCompatibility -AssemblyPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.dll')
Assert-FileExists -Path $vsixPath -Message 'Visual Studio extension VSIX was not created'

$manifestPath = Join-Path $buildOutput 'extension.vsixmanifest'
Assert-FileExists -Path $manifestPath -Message 'Visual Studio extension manifest was not created'

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$sourceManifestPath = Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Vssdk\source.extension.vsixmanifest'
[xml]$sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath
Assert-VisualStudioCompatibilityContract -Manifest $sourceManifest
Assert-VisualStudioCompatibilityContract -Manifest $manifest
$generatedVersion = [string]$manifest.PackageManifest.Metadata.Identity.Version
$sourceVersion = [string]$sourceManifest.PackageManifest.Metadata.Identity.Version
if ($generatedVersion -ne $sourceVersion) {
    throw "Generated VSIX manifest version $generatedVersion does not match source manifest version $sourceVersion. Clean the hybrid extension project before packaging; do not publish the stale VSIX."
}

$extensionType = $manifest.PackageManifest.Installation.ExtensionType
if ($extensionType -ne 'VSSDK+VisualStudio.Extensibility') {
    throw "Expected a hybrid VSSDK+VisualStudio.Extensibility VSIX, but found '$extensionType'."
}

Assert-HybridVssdkRegistration `
    -PkgdefPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef') `
    -GeneratedManifestPath $manifestPath `
    -SourceManifestPath $sourceManifestPath

Assert-ClassicVisualizerInstallPathContract `
    -SourcePath (Join-Path $repoRoot 'src\RawBufferVisualizer.VisualStudio.Debugger\ImageVisualizerResultProvider.cs') `
    -SourceManifestPath $sourceManifestPath `
    -GeneratedManifestPath $manifestPath `
    -ClassicAssemblyPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Classic.dll') `
    -DebuggerVisualizersReferencePath (Get-ProjectCompileReferencePath -AssetsPath (Join-Path $buildRoot 'intermediate\RawBufferVisualizer.VisualStudio.Classic\project.assets.json') -PackageName 'Microsoft.VisualStudio.DebuggerVisualizers') `
    -DebuggerAssemblyPath (Join-Path $buildOutput 'RawBufferVisualizer.VisualStudio.Debugger.dll') `
    -EngineReferencePath (Get-ProjectCompileReferencePath -AssetsPath (Join-Path $buildRoot 'intermediate\RawBufferVisualizer.VisualStudio.Debugger\project.assets.json') -PackageName 'Microsoft.VisualStudio.Debugger.Engine') `
    -MetadataReferencePath (Get-ProjectCompileReferencePath -AssetsPath (Join-Path $buildRoot 'intermediate\RawBufferVisualizer.VisualStudio.Debugger\project.assets.json') -PackageName 'Microsoft.VisualStudio.Debugger.Metadata')

$entryNames = Get-VsixEntryNames -Path $vsixPath
$requiredEntries = @(
    'extension.vsixmanifest',
    '.vsextension/extension.json',
    'RawBufferVisualizer.VisualStudio.Vssdk.pkgdef',
    'OutOfProc/RawBufferVisualizer.VisualStudio.Extensibility.dll',
    'RawBufferVisualizer.VisualStudio.Vssdk.dll',
    'RawBufferVisualizer.VisualStudio.Classic.dll',
    'RawBufferVisualizer.VisualStudio.Debugger.dll',
    'RawBufferVisualizer.VisualStudio.Debugger.vsdconfig',
    'RawBufferVisualizer.OpenGlCanvas.dll',
    'netstandard2.0/RawBufferVisualizer.Core.dll',
    'netstandard2.0/RawBufferVisualizer.Sdk.dll',
    'netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.dll',
    'netstandard2.0/RawBufferVisualizer.VisualStudio.ObjectSource.deps.json',
    'SharpGL.dll',
    'SharpGL.WinForms.dll'
)

foreach ($entryName in $requiredEntries) {
    if ($entryNames -notcontains $entryName) {
        throw "VSIX is missing required entry: $entryName"
    }
}

$objectSourceOutput = Join-Path $buildRoot "bin\RawBufferVisualizer.VisualStudio.ObjectSource\$Configuration\netstandard2.0"
foreach ($payloadName in @(
    'RawBufferVisualizer.Core.dll',
    'RawBufferVisualizer.Sdk.dll',
    'RawBufferVisualizer.VisualStudio.ObjectSource.dll',
    'RawBufferVisualizer.VisualStudio.ObjectSource.deps.json'
)) {
    $builtPayloadPath = Join-Path $objectSourceOutput $payloadName
    Assert-FileExists -Path $builtPayloadPath -Message 'Fresh netstandard2.0 debugger payload was not found'
    $builtPayloadHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $builtPayloadPath).Hash
    $packagedPayloadHash = Get-VsixEntrySha256 `
        -Path $vsixPath `
        -EntryName "netstandard2.0/$payloadName"
    if (-not [string]::Equals($builtPayloadHash, $packagedPayloadHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "VSIX contains a stale netstandard2.0 debugger payload: $payloadName. Built SHA-256 $builtPayloadHash; packaged SHA-256 $packagedPayloadHash."
    }
}

foreach ($payload in @(
    @{ Project = 'RawBufferVisualizer.VisualStudio.Classic'; Name = 'RawBufferVisualizer.VisualStudio.Classic.dll' },
    @{ Project = 'RawBufferVisualizer.VisualStudio.Debugger'; Name = 'RawBufferVisualizer.VisualStudio.Debugger.dll' },
    @{ Project = 'RawBufferVisualizer.VisualStudio.Debugger'; Name = 'RawBufferVisualizer.VisualStudio.Debugger.vsdconfig' }
)) {
    $builtPath = Join-Path $buildRoot "bin\$($payload.Project)\$Configuration\net472\$($payload.Name)"
    Assert-FileExists -Path $builtPath -Message 'Fresh debugger routing payload was not found'
    $builtHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $builtPath).Hash
    $packagedHash = Get-VsixEntrySha256 -Path $vsixPath -EntryName $payload.Name
    if (-not [string]::Equals($builtHash, $packagedHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "VSIX contains a stale debugger routing payload: $($payload.Name). Built SHA-256 $builtHash; packaged SHA-256 $packagedHash."
    }
}

if ($entryNames -contains 'RawBufferVisualizer.VisualStudio.Extensibility.pkgdef') {
    throw 'VSIX must not contain the obsolete Extensibility-owned VSSDK pkgdef.'
}

[xml]$packagedManifest = Get-VsixEntryText -Path $vsixPath -EntryName 'extension.vsixmanifest'
Assert-VisualStudioCompatibilityContract -Manifest $packagedManifest
$classicAssets = @($packagedManifest.PackageManifest.Assets.Asset | Where-Object {
    [string]$_.Type -eq 'Microsoft.VisualStudio.DotnetCustomVisualizer' -and
    [string]$_.Path -eq 'RawBufferVisualizer.VisualStudio.Classic.dll'
})
if ($classicAssets.Count -ne 1) {
    throw 'Packaged VSIX must register the Classic DotnetCustomVisualizer installation path.'
}
$packagedVersion = [string]$packagedManifest.PackageManifest.Metadata.Identity.Version
if ($packagedVersion -ne $sourceVersion) {
    throw "Packaged VSIX manifest version $packagedVersion does not match source manifest version $sourceVersion. Do not publish or install the stale VSIX."
}

Get-ChildItem -LiteralPath $buildOutput -Force |
    Copy-Item -Destination $publishDir -Recurse -Force

$readmePath = Join-Path $publishDir 'README.txt'
Set-Content -LiteralPath $readmePath -Encoding UTF8 -Value @(
    'Raw Buffer Visualizer for Visual Studio',
    '',
    'Install this single VSIX:',
    'RawBufferVisualizer.VisualStudio.Extensibility.vsix',
    '',
    'The VSIX contains both parts required for normal operation:',
    '- Visual Studio debugger visualizers for RawBufferSnapshot, RawBufferView, Bitmap, OpenCvSharp Mat, Emgu CV Mat, and supported image collections',
    '- In-process Visual Studio ToolWindow used as the docked image viewer',
    '- Marketplace-installed debugger providers that forward inspected values to the same docked image list',
    '',
    'Supported environment:',
    '- Visual Studio 2022 17.9 or newer, or Visual Studio 2026 18.x',
    '',
    'Close Visual Studio before installing, then restart Visual Studio before debugger testing.'
)

if (-not $NoZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishDir,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    Assert-FileExists -Path $zipPath -Message 'zip package was not created'
}

Write-Host "Published: $publishDir"
if (-not $NoZip) {
    Write-Host "Package:   $zipPath"
}

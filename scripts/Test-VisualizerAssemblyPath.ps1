[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VsixPath,
    [Parameter(Mandatory = $true)][string]$VisualStudioRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [string]$InstalledExtensionPath = '',
    [string]$VisualizerAttributeCachePath = ''
)

$ErrorActionPreference = 'Stop'
$vsix = (Resolve-Path -LiteralPath $VsixPath).Path
$vsRoot = (Resolve-Path -LiteralPath $VisualStudioRoot).Path
$output = [IO.Path]::GetFullPath($OutputRoot)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$globalVisualizers = Join-Path $vsRoot 'Common7\Packages\Debugger\Visualizers'
$loader = Join-Path $globalVisualizers 'netstandard2.0\Microsoft.VisualStudio.DebuggerVisualizers.dll'
foreach ($path in @($compiler, $loader, (Join-Path $vsRoot 'Common7\IDE\Microsoft.VisualStudio.Debugger.Engine.dll'),
    (Join-Path $vsRoot 'Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Debugger.Metadata.dll'),
    (Join-Path $vsRoot 'Common7\IDE\PrivateAssemblies\vsdebugeng.manimpl.dll'),
    (Join-Path $vsRoot 'Common7\IDE\PrivateAssemblies\ClrCustomVisualizerVSHost.dll'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required runtime was not found: $path" }
}
if (-not [string]::IsNullOrWhiteSpace($VisualizerAttributeCachePath) -and [string]::IsNullOrWhiteSpace($InstalledExtensionPath)) {
    throw 'A real IDE attribute cache must be checked against an explicitly selected installed extension.'
}
if (-not [string]::IsNullOrWhiteSpace($VisualizerAttributeCachePath)) {
    $VisualizerAttributeCachePath = (Resolve-Path -LiteralPath $VisualizerAttributeCachePath).Path
}
if (Test-Path -LiteralPath (Join-Path $globalVisualizers 'RawBufferVisualizer.VisualStudio.ObjectSource.dll')) {
    throw 'The global-only regression requires no RawBufferVisualizer ObjectSource in the IDE global Visualizers directory. Do not delete installed files to run this test.'
}
if (Test-Path -LiteralPath $output) { throw "Use a fresh isolated output directory: $output" }

New-Item -ItemType Directory -Path $output | Out-Null
if (-not [string]::IsNullOrWhiteSpace($VisualizerAttributeCachePath)) {
    Copy-Item -LiteralPath $VisualizerAttributeCachePath -Destination (Join-Path $output 'attribcache.before.bin')
}
$stage = Join-Path $output 'extension'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($vsix, $stage)
$extensionToProbe = $stage
if (-not [string]::IsNullOrWhiteSpace($InstalledExtensionPath)) {
    $extensionToProbe = (Resolve-Path -LiteralPath $InstalledExtensionPath).Path
    foreach ($relativePath in @(
        'RawBufferVisualizer.VisualStudio.Classic.dll', 'RawBufferVisualizer.VisualStudio.Debugger.dll',
        'RawBufferVisualizer.VisualStudio.Debugger.vsdconfig', 'RawBufferVisualizer.VisualStudio.Vssdk.dll',
        'netstandard2.0\RawBufferVisualizer.Core.dll', 'netstandard2.0\RawBufferVisualizer.Sdk.dll',
        'netstandard2.0\RawBufferVisualizer.VisualStudio.ObjectSource.dll',
        'netstandard2.0\RawBufferVisualizer.VisualStudio.ObjectSource.deps.json'
    )) {
        $installedHash = (Get-FileHash -LiteralPath (Join-Path $extensionToProbe $relativePath) -Algorithm SHA256).Hash
        $packagedHash = (Get-FileHash -LiteralPath (Join-Path $stage $relativePath) -Algorithm SHA256).Hash
        if ($installedHash -ne $packagedHash) { throw "Installed payload differs from the checked VSIX: $relativePath" }
    }
}
$probeSource = Join-Path $output 'AssemblyPathProbe.cs'
$probeExe = Join-Path $output 'AssemblyPathProbe.exe'

$source = @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

/// <summary>
/// 역할: VS의 등록 해석기·후보 ID·UI 타입 생성과 실제 ObjectSource 로더를 검사한다.
/// 흐름: 등록 해석 및 선택된 IDE 캐시 조회 또는 격리된 로더 호출 → 결과 기록.
/// 상태: 임시 Bitmap과 로더를 현재 시험 프로세스에서만 소유하고 해제한다.
/// 연관 클래스:
/// - Assembly: 배포된 등록 및 후보 ID의 확인.
/// - Bitmap: 실제 로더에 전달할 임시 이미지.
/// </summary>
internal static class AssemblyPathProbe
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static int assertions;
    private static readonly Dictionary<string, object> Result = new Dictionary<string, object>();

    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string mode = args[0], vs = args[1], stage = args[2];
        Result["Mode"] = mode;
        Result["Runtime"] = Environment.Version.ToString();
        Result["Is64BitProcess"] = Environment.Is64BitProcess;
        try
        {
            Require(Environment.Is64BitProcess, "The probe must be x64.");
            if (mode == "registration") CheckRegistration(vs, args[4], stage, args[5]);
            else if (mode == "ui") CheckUiLoader(vs, stage, args[3]);
            else CheckLoader(mode, vs, stage, args[3]);
            Result["Passed"] = true;
            return 0;
        }
        catch (Exception ex)
        {
            Result["Passed"] = false;
            Result["Failure"] = Unwrap(ex).ToString();
            return 1;
        }
        finally
        {
            Result["Assertions"] = assertions;
            Console.WriteLine(Json.Serialize(Result));
        }
    }

    private static void CheckRegistration(string vs, string extensionRoot, string stage, string cachePath)
    {
        Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PublicAssemblies", "Microsoft.VisualStudio.DebuggerVisualizers.dll"));
        Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "Microsoft.VisualStudio.Debugger.Engine.dll"));
        Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PrivateAssemblies", "Microsoft.VisualStudio.Debugger.Metadata.dll"));
        var manifest = new XmlDocument();
        manifest.Load(Path.Combine(extensionRoot, "extension.vsixmanifest"));
        var ns = new XmlNamespaceManager(manifest.NameTable);
        ns.AddNamespace("v", "http://schemas.microsoft.com/developer/vsx-schema/2011");
        var assets = manifest.SelectNodes("/v:PackageManifest/v:Assets/v:Asset[@Type='Microsoft.VisualStudio.DotnetCustomVisualizer']", ns);
        Require(assets.Count == 1, "Exactly one Classic DotnetCustomVisualizer asset is required.");
        string relativePath = assets[0].Attributes["Path"].Value;
        Require(relativePath == "RawBufferVisualizer.VisualStudio.Classic.dll", "Unexpected Classic asset path.");
        Assembly classic = Assembly.LoadFrom(Path.Combine(extensionRoot, relativePath));
        var registrations = classic.GetCustomAttributes(typeof(DebuggerVisualizerAttribute), false).Cast<DebuggerVisualizerAttribute>().ToArray();
        Require(registrations.Length == 2, "Both Classic UI IDs need installation-path registration.");
        var map = DecodeInstallPathIds(vs, extensionRoot, relativePath);
        Result["InstallPaths"] = map;
        foreach (string name in new[] { "RawBufferClassicDebuggerVisualizer", "ImageCollectionClassicDebuggerVisualizer" })
        {
            Type type = classic.GetType("RawBufferVisualizer.VisualStudio.Classic." + name, true);
            var matches = registrations.Where(r => r.VisualizerTypeName == type.AssemblyQualifiedName).ToArray();
            Require(matches.Length == 1, "Missing or duplicate Classic UI registration: " + name);
            Require(matches[0].TargetTypeName == type.AssemblyQualifiedName, "Registration must target its own UI class.");
            string sourceType = name == "RawBufferClassicDebuggerVisualizer" ? "RawBufferSnapshotVisualizerObjectSource" : "ImageCollectionVisualizerObjectSource";
            Require(matches[0].VisualizerObjectSourceTypeName.StartsWith("RawBufferVisualizer.VisualStudio.ObjectSource." + sourceType + ",", StringComparison.Ordinal), "Incorrect ObjectSource registration.");
            string id = type.FullName + "-" + classic.FullName;
            Require(map.ContainsKey(id), "The real VS decoder did not produce the full Classic UI ID: " + id);
            Require(!map.ContainsKey(type.FullName + "-" + classic.GetName().Name), "VS install-path keys must not use a short assembly name.");
            Require(Activator.CreateInstance(type) != null, "The Classic UI type could not be created: " + name);
        }
        Assembly debugger = Assembly.LoadFrom(Path.Combine(extensionRoot, "RawBufferVisualizer.VisualStudio.Debugger.dll"));
        Type provider = debugger.GetType("RawBufferVisualizer.VisualStudio.Debugger.ImageVisualizerResultProvider", true);
        FieldInfo identityField = provider.GetField("ClassicAssemblyIdentity", BindingFlags.NonPublic | BindingFlags.Static);
        Require(identityField != null, "The candidate still uses a short assembly ID; VS registers the full assembly identity.");
        var identity = (Lazy<string>)identityField.GetValue(null);
        Require(identity.Value == classic.FullName, "Compiled candidate assembly ID differs from the real VS registration.");
        foreach (string field in new[] { "SingleImageVisualizerTypeName", "CollectionVisualizerTypeName" })
        {
            string typeName = (string)provider.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
            Require(map.ContainsKey(typeName + "-" + identity.Value), "Candidate UI ID is not registered by the real VS decoder.");
        }
        CheckCandidateLocation(provider);
        if (!string.IsNullOrEmpty(cachePath)) CheckActualIdeCache(vs, cachePath, extensionRoot, map);
        Result["CandidateAssemblyIdentity"] = identity.Value;
        Result["UiTypesCreated"] = 2;
        File.WriteAllText(Path.Combine(stage, "probe-install-paths.json"), Json.Serialize(map));
    }

    private static void CheckUiLoader(string vs, string stage, string kind)
    {
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "RawBufferVisualizer.VisualStudio.Classic"), "Classic must not be preloaded in the UI loader case.");
        Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PublicAssemblies", "Microsoft.VisualStudio.DebuggerVisualizers.dll"));
        Assembly host = Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PrivateAssemblies", "ClrCustomVisualizerVSHost.dll"));
        Type resolverType = host.GetType("ClrCustomVisualizerVSHost.AssemblyResolver", true);
        var map = Json.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(stage, "probe-install-paths.json")));
        string uiName = kind == "single" ? "RawBufferClassicDebuggerVisualizer" : "ImageCollectionClassicDebuggerVisualizer";
        string id = map.Keys.Single(key => key.StartsWith("RawBufferVisualizer.VisualStudio.Classic." + uiName + "-", StringComparison.Ordinal));
        string typeName = id.Substring(0, id.IndexOf('-'));
        string identity = id.Substring(id.IndexOf('-') + 1);
        object resolver = resolverType.GetMethod("Create").Invoke(null, new object[] { null, map[id] });
        try
        {
            Assembly classic = Assembly.Load(new AssemblyName(identity));
            Require(string.Equals(classic.Location, Path.Combine(map[id], "RawBufferVisualizer.VisualStudio.Classic.dll"), StringComparison.OrdinalIgnoreCase), "The real UI resolver loaded another Classic binary.");
            Type type = classic.GetType(typeName, true);
            Require(type.BaseType.FullName == "Microsoft.VisualStudio.DebuggerVisualizers.DialogDebuggerVisualizer", "The UI entry has an incompatible base type.");
            object visualizer = Activator.CreateInstance(type);
            Require(visualizer != null, "The UI constructor failed.");
            PropertyInfo formatter = type.BaseType.GetProperty("PreferredFormatterPolicy", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(formatter != null && formatter.GetValue(visualizer, null).ToString() == "NewtonsoftJson", "The UI entry must use the JSON formatter.");
            Result["Kind"] = kind;
            Result["VisualizerId"] = id;
            Result["LoadedClassic"] = classic.Location;
            Result["UiAssemblyResolver"] = host.Location;
        }
        finally { ((IDisposable)resolver).Dispose(); }
    }

    private static Dictionary<string, string> DecodeInstallPathIds(string vs, string extensionRoot, string relativePath)
    {
        Assembly engine = Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PrivateAssemblies", "vsdebugeng.manimpl.dll"));
        Type resolverType = engine.GetType("VSDebugEngine.ManagedEE.LocalAssemblyResolver", true);
        object resolver = Activator.CreateInstance(resolverType, true);
        try
        {
            MethodInfo load = resolverType.GetMethod("LoadFromFile");
            load.Invoke(resolver, new object[] { Path.Combine(extensionRoot, "RawBufferVisualizer.VisualStudio.ObjectSource.dll") });
            object metadataAssembly = load.Invoke(resolver, new object[] { Path.Combine(extensionRoot, relativePath) });
            Type decoderType = engine.GetType("VSDebugEngine.EvalAttributes.AttributeDecoder", true);
            object decoder = Activator.CreateInstance(decoderType, new[] { resolver });
            // Supply a directory classification only to avoid native engine initialization in this isolated process.
            // UI type/assembly IDs are decoded by the installed VS implementation, never reconstructed from short names.
            decoderType.GetMethod("SetVisualizerDirectoryPaths").Invoke(decoder, new object[] { null, extensionRoot });
            var attributes = (IEnumerable)decoderType.GetMethod("GetEvalAttributesFromAssembly").Invoke(decoder, new[] { metadataAssembly });
            var map = new Dictionary<string, string>();
            foreach (object attribute in attributes)
            {
                Type type = attribute.GetType();
                if (type.FullName != "VSDebugEngine.EvalAttributes.VisualizerAttribute") continue;
                string ui = (string)type.GetProperty("UISideVisualizerTypeName").GetValue(attribute, null);
                string assembly = (string)type.GetProperty("UISideVisualizerAssemblyName").GetValue(attribute, null);
                map.Add(ui + "-" + assembly, extensionRoot);
            }
            Require(map.Count == 2, "The actual VS decoder must recognize both self-targeted registrations.");
            Result["RegistrationDecoder"] = engine.Location;
            Result["NativeExtensionDiscoveryTested"] = false;
            return map;
        }
        finally { ((IDisposable)resolver).Dispose(); }
    }

    private static void CheckActualIdeCache(string vs, string cachePath, string extensionRoot, Dictionary<string, string> expected)
    {
        Assembly engine = Assembly.LoadFrom(Path.Combine(vs, "Common7", "IDE", "PrivateAssemblies", "vsdebugeng.manimpl.dll"));
        Type type = engine.GetType("VSDebugEngine.EvalAttributes.LocalAttributeManager", true);
        object manager = Activator.CreateInstance(type, true);
        using (Stream stream = File.OpenRead(cachePath)) type.GetMethod("LoadFromStream").Invoke(manager, new object[] { stream });
        MethodInfo lookup = type.GetMethod("GetInstallPathForDotnetCustomVisualizer");
        foreach (var item in expected)
        {
            string actual = (string)lookup.Invoke(manager, new object[] { item.Key });
            Require(string.Equals(actual, extensionRoot, StringComparison.OrdinalIgnoreCase), "Actual IDE cache points to a different installed extension: " + actual);
            Exception failure = null;
            try { lookup.Invoke(manager, new object[] { item.Key.Split(',')[0] }); }
            catch (Exception ex) { failure = Unwrap(ex); }
            Require(failure is KeyNotFoundException, "The old short-ID lookup must reproduce KeyNotFoundException in the real IDE cache.");
        }
        Result["ActualIdeCacheChecked"] = cachePath;
        Result["ActualIdeCacheLookups"] = expected.Count;
    }

    private static void CheckCandidateLocation(Type provider)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => unchecked((ushort)c.Value));
        int ownedCalls = 0;
        foreach (Type type in provider.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Concat(new[] { provider }))
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var body = method.GetMethodBody();
            if (body == null) continue;
            byte[] bytes = body.GetILAsByteArray();
            int cursor = 0;
            int? lastInteger = null;
            bool usesFullIdentity = false;
            while (cursor < bytes.Length)
            {
                ushort value = bytes[cursor++];
                if (value == 0xfe) value = (ushort)(0xfe00 | bytes[cursor++]);
                OpCode code = codes[value];
                if (code.Value >= OpCodes.Ldc_I4_0.Value && code.Value <= OpCodes.Ldc_I4_8.Value) lastInteger = code.Value - OpCodes.Ldc_I4_0.Value;
                if (code == OpCodes.Ldc_I4) lastInteger = BitConverter.ToInt32(bytes, cursor);
                if (code == OpCodes.Ldc_I4_S) lastInteger = (sbyte)bytes[cursor];
                if (code == OpCodes.Ldsfld && code.OperandType == OperandType.InlineField)
                {
                    FieldInfo field = method.Module.ResolveField(BitConverter.ToInt32(bytes, cursor));
                    if (field.DeclaringType == provider && field.Name == "ClassicAssemblyIdentity") usesFullIdentity = true;
                }
                if (code == OpCodes.Call && code.OperandType == OperandType.InlineMethod)
                {
                    MethodBase called = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, cursor));
                    if (called.Name == "Create" && called.DeclaringType.FullName == "Microsoft.VisualStudio.Debugger.Evaluation.DkmCustomUIVisualizerInfo" && called.GetParameters().Length == 9)
                    {
                        Require(lastInteger == 4, "Compiled Classic candidate must use Extension location (4), not Debuggee (3).");
                        Require(usesFullIdentity, "The compiled owned candidate must pass the full Classic assembly identity.");
                        ownedCalls++;
                        usesFullIdentity = false;
                    }
                }
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: cursor++; break;
                    case OperandType.InlineVar: cursor += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: cursor += 8; break;
                    case OperandType.InlineSwitch: cursor += 4 + 4 * BitConverter.ToInt32(bytes, cursor); break;
                    default: cursor += 4; break;
                }
            }
        }
        Require(ownedCalls == 1, "Could not verify the compiled owned-candidate creation route.");
        Result["CompiledCandidateLocation"] = "Extension";
    }

    private static void CheckLoader(string mode, string vs, string stage, string kind)
    {
        string sourceName = "RawBufferVisualizer.VisualStudio.ObjectSource";
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == sourceName), "ObjectSource must not be preloaded.");
        string global = Path.Combine(vs, "Common7", "Packages", "Debugger", "Visualizers");
        var paths = new List<string> { global };
        var map = Json.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(stage, "probe-install-paths.json")));
        string uiName = kind == "single" ? "RawBufferClassicDebuggerVisualizer" : "ImageCollectionClassicDebuggerVisualizer";
        string id = map.Keys.Single(key => key.StartsWith("RawBufferVisualizer.VisualStudio.Classic." + uiName + "-", StringComparison.Ordinal));
        string install = map[id];
        if (mode == "missing-payload") install = Path.Combine(stage, "missing-payload");
        if (mode != "global-only")
        {
            paths.Add(Path.Combine(install, "netstandard2.0"));
            paths.Add(Path.Combine(install, "net2.0"));
            paths.Add(Path.Combine(install, "net4.6.2"));
            paths.Add(install);
        }
        string sourceType = sourceName + (kind == "single" ? ".BitmapVisualizerObjectSource" : ".ImageCollectionVisualizerObjectSource");
        string assemblyName = mode == "qualified" ? AssemblyName.GetAssemblyName(Path.Combine(map[id], "netstandard2.0", sourceName + ".dll")).FullName : sourceName;
        Assembly loader = Assembly.LoadFrom(Path.Combine(global, "netstandard2.0", "Microsoft.VisualStudio.DebuggerVisualizers.dll"));
        Type hostType = loader.GetType("Microsoft.VisualStudio.DebuggerVisualizers.DebuggeeSide.Impl.ClrCustomVisualizerDebuggeeHost", true);
        int formatter = Convert.ToInt32(Enum.Parse(loader.GetType("Microsoft.VisualStudio.DebuggerVisualizers.FormatterPolicy", true), "NewtonsoftJson"));
        MethodInfo create = hostType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(string[]), typeof(int) }, null);
        object host = null;
        Exception failure = null;
        try { host = create.Invoke(null, new object[] { sourceType, assemblyName, paths.ToArray(), formatter }); }
        catch (Exception ex) { failure = Unwrap(ex); }
        Result["Kind"] = kind;
        Result["VisualizerId"] = id;
        Result["ProbePaths"] = paths;
        Result["AssemblyName"] = assemblyName;
        Result["CreateSucceeded"] = host != null;
        if (mode == "global-only" || mode == "missing-payload")
        {
            Require(failure != null, "The missing-source route must fail.");
            Require(failure.GetType().FullName == "Microsoft.VisualStudio.DebuggerVisualizers.DebuggeeSide.Impl.VisualizerAssemblyNotFoundException", "Unexpected failure: " + failure);
            PropertyInfo missingAssembly = failure.GetType().GetProperty("VisualizerAssemblyName");
            Require(missingAssembly != null && Convert.ToString(missingAssembly.GetValue(failure, null)).StartsWith(sourceName, StringComparison.Ordinal), "Failure must identify the missing ObjectSource.");
            Result["ExpectedException"] = failure.GetType().FullName;
            Result["ExpectedError"] = failure.Message;
            return;
        }
        Require(failure == null, "Source creation failed: " + failure);
        try
        {
            string loaded = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == sourceName).Location;
            Require(string.Equals(loaded, Path.Combine(install, "netstandard2.0", sourceName + ".dll"), StringComparison.OrdinalIgnoreCase), "Wrong ObjectSource binary was loaded: " + loaded);
            Result["LoadedObjectSource"] = loaded;
            using (var bitmap = new Bitmap(320, 240))
            {
                object target = kind == "single" ? (object)bitmap : new object[] { bitmap, bitmap };
                byte[] bytes = (byte[])hostType.GetMethod("GetData").Invoke(host, new[] { target });
                Require(bytes.Length > 0, "Metadata is empty.");
                string text = Encoding.UTF8.GetString(bytes);
                var metadata = Json.Deserialize<Dictionary<string, object>>(text);
                if (kind == "single")
                {
                    var descriptor = (Dictionary<string, object>)metadata["Descriptor"];
                    Require(Convert.ToInt32(descriptor["Width"]) == 320, "Incorrect bitmap width.");
                    Require(Convert.ToInt32(descriptor["Height"]) == 240, "Incorrect bitmap height.");
                    Require(Convert.ToInt64(metadata["BufferLength"]) == 307200, "Incorrect bitmap buffer length.");
                }
                else
                {
                    Require(Convert.ToInt32(metadata["TotalCount"]) == 2, "Incorrect collection total count.");
                    Require(Convert.ToInt32(metadata["ItemCount"]) == 2, "Incorrect collection item count.");
                }
                Result["Metadata"] = metadata;
            }
        }
        finally { var disposable = host as IDisposable; if (disposable != null) disposable.Dispose(); }
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
        return exception;
    }
}
'@
[IO.File]::WriteAllText($probeSource, $source, [Text.UTF8Encoding]::new($false))
& $compiler /nologo /target:exe /platform:x64 /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Xml.dll "/out:$probeExe" $probeSource
if ($LASTEXITCODE -ne 0) { throw 'The isolated assembly-path probe did not compile.' }

function Invoke-Probe {
    param([string]$Mode, [string]$Kind = '')
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $probeExe
    $cacheToProbe = if ([string]::IsNullOrWhiteSpace($VisualizerAttributeCachePath)) { '' } else { Join-Path $output 'attribcache.before.bin' }
    $start.Arguments = (@($Mode, $vsRoot, $stage, $Kind, $extensionToProbe, $cacheToProbe) | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "Isolated probe timed out: $Mode/$Kind"
        }
        $text = $stdout.Result
        [IO.File]::WriteAllText((Join-Path $output "$Mode-$Kind.json"), $text)
        if ($process.ExitCode -ne 0) { throw "Probe failed: $Mode/$Kind`n$text`n$($stderr.Result)" }
        return ($text | ConvertFrom-Json)
    }
    finally { $process.Dispose() }
}

$results = @(Invoke-Probe -Mode registration)
New-Item -ItemType Directory -Path (Join-Path $stage 'missing-payload') | Out-Null
foreach ($kind in @('single', 'collection')) {
    $results += Invoke-Probe -Mode ui -Kind $kind
    foreach ($mode in @('global-only', 'short', 'qualified', 'missing-payload')) {
        $results += Invoke-Probe -Mode $mode -Kind $kind
    }
}
$payloadHashes = @(Get-ChildItem -LiteralPath (Join-Path $stage 'netstandard2.0') -File | Where-Object {
    $_.Name -in @('RawBufferVisualizer.Core.dll', 'RawBufferVisualizer.Sdk.dll', 'RawBufferVisualizer.VisualStudio.ObjectSource.dll', 'RawBufferVisualizer.VisualStudio.ObjectSource.deps.json')
} | ForEach-Object { [pscustomobject]@{ File = $_.Name; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
if ($payloadHashes.Count -ne 4) { throw 'The VSIX must contain all four netstandard2.0 payloads.' }
$report = [ordered]@{
    Passed = $true
    Cases = $results.Count
    Assertions = ($results | Measure-Object -Property Assertions -Sum).Sum
    HostRoot = $vsRoot
    HostVersion = (Get-Item -LiteralPath (Join-Path $vsRoot 'Common7\IDE\devenv.exe')).VersionInfo.FileVersion
    HostLoaderSHA256 = (Get-FileHash -LiteralPath $loader -Algorithm SHA256).Hash
    VsixPath = $vsix
    VsixSHA256 = (Get-FileHash -LiteralPath $vsix -Algorithm SHA256).Hash
    ExtensionPathProbed = $extensionToProbe
    InstalledPayloadsCompared = (-not [string]::IsNullOrWhiteSpace($InstalledExtensionPath))
    VisualizerAttributeCachePath = $VisualizerAttributeCachePath
    VisualizerAttributeCacheSHA256 = $(if ([string]::IsNullOrWhiteSpace($VisualizerAttributeCachePath)) { $null } else { (Get-FileHash -LiteralPath (Join-Path $output 'attribcache.before.bin') -Algorithm SHA256).Hash })
    Payloads = $payloadHashes
    Results = $results
    Boundary = 'Actual VS metadata decoder, compiled candidate identity/location, real UI assembly resolver/Classic construction and real debuggee loader in isolated .NET Framework x64 processes. Optional actual IDE cache lookup is read-only. Native extension discovery/Concord dispatch, UI opening, other host versions and device operations are not tested.'
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'verification.json') -Encoding UTF8
Write-Output "PASS: $($report.Cases) cases, $($report.Assertions) assertions. Results: $output\verification.json"
Write-Output $report.Boundary

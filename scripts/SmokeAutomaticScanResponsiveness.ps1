param(
    [string]$BuildRoot = '',
    [string]$OutputDir = '',
    [switch]$NoBuild,
    [switch]$Baseline,
    [switch]$SkipWindow,
    [switch]$WindowOnly
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path $repoRoot 'artifacts' }
if (-not (Test-Path -LiteralPath 'D:\')) { Write-Warning "D: is unavailable; using $testRoot for test outputs." }
if ([string]::IsNullOrWhiteSpace($BuildRoot)) { $BuildRoot = Join-Path $testRoot 'build' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $testRoot 'ui\automatic-scan' }
New-Item -ItemType Directory -Force -Path $OutputDir, "$OutputDir\temp" | Out-Null
$env:TEMP = "$OutputDir\temp"; $env:TMP = $env:TEMP
$env:RAWBUFFERVISUALIZER_DOCKED_PERF_JSON = "$OutputDir\docked.json"
if (-not $NoBuild) {
    dotnet build "$repoRoot\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj" -c Release -p:CreateVsixContainer=false "-p:RawBufferVisualizerBuildRoot=$BuildRoot" *> "$OutputDir\build.log"
    if ($LASTEXITCODE -ne 0) { throw 'VSSDK build failed.' }
}
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing
$assemblyDirectory = "$BuildRoot\bin\RawBufferVisualizer.VisualStudio.Vssdk\Release\net472"
[xml]$hostProject = Get-Content "$repoRoot\src\RawBufferVisualizer.VisualStudio.Vssdk\RawBufferVisualizer.VisualStudio.Vssdk.csproj"
$toolsVersion = ($hostProject.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Microsoft.VSSDK.BuildTools').Version
$interop = Get-ChildItem "$env:ProgramFiles\Microsoft Visual Studio" -Filter Microsoft.VisualStudio.Interop.dll -Recurse | Where-Object { $_.DirectoryName -like '*\IDE\PublicAssemblies' -and [Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version.Major -eq 17 -and [version]$_.VersionInfo.FileVersion -ge [version]$toolsVersion } | Sort-Object { [version]$_.VersionInfo.FileVersion } | Select-Object -First 1
if ($null -eq $interop) { throw 'Matching installed Visual Studio interop assembly missing.' }
[Reflection.Assembly]::LoadFrom($interop.FullName) | Out-Null
$packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$env:USERPROFILE\.nuget\packages" }
$vssdkTools = "$packageRoot\microsoft.vssdk.buildtools\$toolsVersion\tools\vssdk"
foreach ($name in @('Microsoft.VisualStudio.Validation','Microsoft.VisualStudio.Threading','Microsoft.VisualStudio.Shell.Framework','Microsoft.VisualStudio.Shell.15.0')) { [Reflection.Assembly]::LoadFrom("$vssdkTools\$name.dll") | Out-Null }
foreach ($name in @('Microsoft.VisualStudio.Imaging','Microsoft.VisualStudio.ImageCatalog','RawBufferVisualizer.Core','RawBufferVisualizer.Sdk','RawBufferVisualizer.VisualStudio.ObjectSource','RawBufferVisualizer.VisualStudio','RawBufferVisualizer.OpenGlCanvas')) { [Reflection.Assembly]::LoadFrom("$assemblyDirectory\$name.dll") | Out-Null }
$assembly = [Reflection.Assembly]::LoadFrom("$assemblyDirectory\RawBufferVisualizer.VisualStudio.Vssdk.dll")
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new())
# The isolated STA host supplies the UI-thread initialization normally performed by Visual Studio.
[Microsoft.VisualStudio.Shell.ThreadHelper].GetMethod('SetUIThread', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())
$references = @($interop.FullName, "$assemblyDirectory\RawBufferVisualizer.VisualStudio.ObjectSource.dll", [Windows.Threading.Dispatcher].Assembly.Location, [Windows.Application].Assembly.Location, [Windows.Point].Assembly.Location)
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RawBufferVisualizer.VisualStudio.ObjectSource;

public sealed class ScanDteProxy : RealProxy {
    readonly Func<string, object[], object> call;
    readonly Type type;
    readonly int thread = Thread.CurrentThread.ManagedThreadId;
    public ScanDteProxy(Type type, Func<string, object[], object> call) : base(type) { this.type=type; this.call = call; }
    public override IMessage Invoke(IMessage message) {
        var method = (IMethodCallMessage)message;
        try {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("EnvDTE accessed from another thread");
            object result;
            switch(method.MethodName) {
                case "GetType": result=type; break;
                case "ToString": result=type.FullName; break;
                case "GetHashCode": result=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); break;
                case "Equals": result=object.ReferenceEquals(GetTransparentProxy(),method.Args[0]); break;
                default: result=call(method.MethodName,method.Args); break;
            }
            return new ReturnMessage(result, null, 0, method.LogicalCallContext, method);
        } catch (Exception ex) { return new ReturnMessage(ex, method); }
    }
    public static T Create<T>(Func<string, object[], object> call) { return (T)new ScanDteProxy(typeof(T), call).GetTransparentProxy(); }
}

public sealed class ScanFixture {
    public int DelayMilliseconds, RootReads, DirectReads, NestedReads, CountReads, Evaluations;
    public bool Cancel, ThrowItems, ThrowNestedItems, ThrowLocals;
    public int CancelAtRoot = -1, CancelAtDirect = -1, CancelAtNested = -1, CollectionCount = 3;
    public Action OnRead;
    public EnvDTE.dbgDebugMode Mode = EnvDTE.dbgDebugMode.dbgBreakMode;
    public EnvDTE.Expression[] Roots = new EnvDTE.Expression[0], Arguments = new EnvDTE.Expression[0];
    public string FrameName = "Probe.Frame", SolutionPath = "";
    public readonly EnvDTE.Debugger Debugger;
    public readonly EnvDTE80.DTE2 Dte;
    public ScanFixture() {
        Debugger = ScanDteProxy.Create<EnvDTE.Debugger>((name,args) => {
            if (name == "get_CurrentMode") return Mode;
            if (name == "get_CurrentProcess") return null;
            if (name == "get_CurrentStackFrame") return Frame();
            if (name == "GetExpression") {
                Evaluations++; var text=(string)args[0];
                return text.EndsWith(".Count") || text.EndsWith(".Length")
                    ? Expression(text,"System.Int32",CollectionCount.ToString(),null)
                    : Expression(text,"System.IntPtr","1234",null);
            }
            throw new NotSupportedException(name);
        });
        Dte = ScanDteProxy.Create<EnvDTE80.DTE2>((name,args) => {
            if (name == "get_Debugger") return Debugger;
            if (name == "get_Solution") return ScanDteProxy.Create<EnvDTE.Solution>((n,a) => { if (n == "get_FullName") return SolutionPath; throw new NotSupportedException(n); });
            throw new NotSupportedException(name);
        });
    }
    void Read() {
        if (DelayMilliseconds > 0) Thread.Sleep(DelayMilliseconds);
        if ((CancelAtRoot >= 0 && RootReads >= CancelAtRoot) || (CancelAtDirect >= 0 && DirectReads >= CancelAtDirect) || (CancelAtNested >= 0 && NestedReads >= CancelAtNested)) Cancel=true;
        if (OnRead != null) OnRead();
    }
    public EnvDTE.Expression Expression(string name, string type, string value, EnvDTE.Expressions members) {
        return ScanDteProxy.Create<EnvDTE.Expression>((method,args) => {
            Read();
            switch(method) {
                case "get_Name": return name;
                case "get_Type": return type;
                case "get_Value": return value;
                case "get_IsValidValue": return true;
                case "get_DataMembers": return members;
                default: throw new NotSupportedException(method);
            }
        });
    }
    public EnvDTE.Expressions Members(EnvDTE.Expression[] items, string kind) {
        return ScanDteProxy.Create<EnvDTE.Expressions>((method,args) => {
            if (method == "get_Count") { CountReads++; return items.Length; }
            if (method == "Item" || method == "get_Item") {
                if (kind == "root") RootReads++; else if (kind == "direct") DirectReads++; else NestedReads++;
                Read();
                if ((ThrowItems && kind != "root") || (ThrowNestedItems && kind == "nested")) throw new InvalidOperationException("unreadable member");
                return items[Convert.ToInt32(args[0])-1];
            }
            throw new NotSupportedException(method);
        });
    }
    EnvDTE.StackFrame Frame() {
        var roots = Roots; var arguments = Arguments; var frameName = FrameName;
        return ScanDteProxy.Create<EnvDTE.StackFrame>((name,args) => {
            if (name == "get_FunctionName") return frameName;
            if (name == "get_Locals") { if (ThrowLocals) throw new InvalidOperationException("locals unavailable"); return Members(roots,"root"); }
            if (name == "get_Arguments") return Members(arguments,"root");
            throw new NotSupportedException(name);
        });
    }
    public void KnownRoots(int count) {
        Roots = new EnvDTE.Expression[count];
        for (int i=0;i<count;i++) Roots[i] = Expression("mat"+i,"OpenCvSharp.Mat","{OpenCvSharp.Mat}",null);
    }
    public void Wrapper(bool nested, int extraMembers) {
        var metadata = new List<EnvDTE.Expression>();
        metadata.Add(Expression("Width","System.Int32","4",null));
        metadata.Add(Expression("Height","System.Int32","2",null));
        for (int i=0;i<extraMembers;i++) metadata.Add(Expression("Unused"+i,"System.Int32","0",null));
        var direct = new List<EnvDTE.Expression>();
        direct.Add(Expression("Data","System.IntPtr","1234",null));
        if (nested) direct.Add(Expression("Info","Company.Metadata","{Info}",Members(metadata.ToArray(),"nested")));
        else direct.AddRange(metadata);
        Roots = new[] { Expression("frame","Company.BufferFrame","{frame}",Members(direct.ToArray(),"direct")) };
    }
    public void BufferRoots(int count, string prefix) {
        Wrapper(false,0); var members=Roots[0].DataMembers;
        Roots=new EnvDTE.Expression[count];
        for (int i=0;i<count;i++) Roots[i]=Expression(prefix+i,"Company.BufferFrame","{frame}",members);
        RootReads=0; DirectReads=0; NestedReads=0;
    }
    public async Task<bool> ContinueAsync() {
        if (Cancel) return false;
        await Dispatcher.Yield(DispatcherPriority.Background);
        return !Cancel;
    }
}

public sealed class ScanMeasurement {
    public double ElapsedMilliseconds, MaximumInputGapMilliseconds;
    public int InputTicks, Candidates;
    public object Result;
}
public static class AutomaticScanProbe {
    public static object NewInspector(Assembly assembly) { return Activator.CreateInstance(assembly.GetType("RawBufferVisualizer.VisualStudio.Vssdk.AutomaticVisionInspector"),true); }
    public static async Task<object> Scan(object inspector, ScanFixture fixture, TypeMappingStore store) {
        var method = inspector.GetType().GetMethod("ScanAsync");
        if (method == null) return inspector.GetType().GetMethod("Scan",new[] { typeof(EnvDTE.Debugger),typeof(bool),typeof(TypeMappingStore) }).Invoke(inspector,new object[] {fixture.Debugger,true,store});
        var task = (Task)method.Invoke(inspector,new object[] { fixture.Debugger, new Func<Task<bool>>(fixture.ContinueAsync), true, store });
        await task;
        return task.GetType().GetProperty("Result").GetValue(task,null);
    }
    public static async Task<ScanMeasurement> Measure(object inspector, ScanFixture fixture, TypeMappingStore store) {
        var measurement = new ScanMeasurement();
        var watch = Stopwatch.StartNew(); double previous = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Input);
        timer.Interval = TimeSpan.FromMilliseconds(5);
        timer.Tick += (s,e) => { var now=watch.Elapsed.TotalMilliseconds; measurement.MaximumInputGapMilliseconds=Math.Max(measurement.MaximumInputGapMilliseconds,now-previous); previous=now; measurement.InputTicks++; };
        timer.Start();
        try { measurement.Result=await Scan(inspector,fixture,store); }
        finally { timer.Stop(); measurement.ElapsedMilliseconds=watch.Elapsed.TotalMilliseconds; measurement.MaximumInputGapMilliseconds=Math.Max(measurement.MaximumInputGapMilliseconds,measurement.ElapsedMilliseconds-previous); }
        measurement.Candidates=((IList)measurement.Result.GetType().GetProperty("Inspections").GetValue(measurement.Result,null)).Count;
        return measurement;
    }
}
public static class ScanWindowNative {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
}
'@
$checks = [Collections.Generic.List[string]]::new()
function Check($condition, [string]$message) { if (-not $condition) { throw $message }; $checks.Add($message) }
function Pump([int]$milliseconds = 10) {
    $frame = [Windows.Threading.DispatcherFrame]::new(); $timer = [Windows.Threading.DispatcherTimer]::new()
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
    $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Await($task) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $task.IsCompleted) { Pump; if ($watch.Elapsed.TotalSeconds -gt 30) { throw 'Probe timed out.' } }
    $task.GetAwaiter().GetResult()
}
function Inspector { [AutomaticScanProbe]::NewInspector($assembly) }
$store = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new("$OutputDir\solution.json", "$OutputDir\user.json")
$measurements = @()
if (-not $WindowOnly) {
for ($sample=0; $sample -lt 3; $sample++) {
    $fixture = [ScanFixture]::new(); $fixture.KnownRoots(80); $fixture.DelayMilliseconds=3
    $measurement = Await ([AutomaticScanProbe]::Measure((Inspector),$fixture,$store))
    Check ($measurement.Candidates -eq 80) 'All 80 known image candidates remain discoverable'
    if (-not $Baseline) { Check ($measurement.InputTicks -gt 0) 'Dispatcher input runs before root discovery completes' }
    $measurements += @{ElapsedMs=$measurement.ElapsedMilliseconds; MaxInputGapMs=$measurement.MaximumInputGapMilliseconds; InputTicks=$measurement.InputTicks; Candidates=$measurement.Candidates; InjectedDelayPerExpressionCallMs=3}
}
$fixture = [ScanFixture]::new(); $fixture.Wrapper($false,600); $fixture.ThrowItems=$true
$result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
$unreadable = @{DirectAttempts=$fixture.DirectReads; CountReads=$fixture.CountReads}
if (-not $Baseline) { Check ($fixture.DirectReads -eq 128) 'Unreadable direct members cannot bypass the 128-attempt limit'; Check ($fixture.CountReads -eq 3) 'Direct and root collection counts are read once each' }
$fixture = [ScanFixture]::new(); $fixture.Wrapper($true,600)
$result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
$nested = @{NestedAttempts=$fixture.NestedReads; CountReads=$fixture.CountReads}
if (-not $Baseline) { Check ($fixture.NestedReads -eq 64) 'Nested members retain the 64-attempt limit'; Check ($fixture.CountReads -eq 4) 'Nested member collection count is read once' }
if (-not $Baseline) {
    $fixture = [ScanFixture]::new(); $fixture.Wrapper($true,600); $fixture.ThrowNestedItems=$true
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($fixture.NestedReads -eq 64) 'Unreadable nested members cannot bypass the 64-attempt limit'
    $fixture = [ScanFixture]::new(); $fixture.KnownRoots(80); $fixture.CancelAtRoot=3
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($fixture.RootReads -eq 3 -and -not $result.IsComplete -and $result.Inspections.Count -eq 3) 'Stop during roots retains partial candidates and starts no further root read'
    foreach ($area in @('direct','nested')) {
        $fixture = [ScanFixture]::new(); $fixture.Wrapper(($area -eq 'nested'),10); $inspector=Inspector
        if ($area -eq 'direct') { $fixture.CancelAtDirect=3 } else { $fixture.CancelAtNested=3 }
        $result = Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$store))
        Check (-not $result.IsComplete -and $result.Inspections.Count -eq 0) "Cancelled $area inventory is not published"
        $fixture.Cancel=$false; $fixture.CancelAtDirect=-1; $fixture.CancelAtNested=-1; $fixture.DirectReads=0; $fixture.NestedReads=0
        $result = Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$store))
        Check ($result.Inspections.Count -eq 1 -and -not $result.Inspections[0].UsesCachedInference -and $fixture.DirectReads -gt 0) "Cancelled $area inventory does not poison the type cache"
        $result = Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$store))
        Check ($result.Inspections[0].UsesCachedInference -and -not $result.Inspections[0].Inference.CanAutoOpen) "Completed $area analysis is cached without bypassing the mapping gate"
    }
    $fixture = [ScanFixture]::new(); $fixture.KnownRoots(140)
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($result.Inspections.Count -eq 128 -and $fixture.RootReads -eq 128 -and $result.TruncatedRootExpressionCount -eq 12 -and -not $result.IsComplete) 'Root and candidate limits remain 128'
    $fixture = [ScanFixture]::new(); $fixture.KnownRoots(2)
    $fixture.Arguments=@($fixture.Roots[0],$fixture.Expression('argMat','Emgu.CV.Mat','{Mat}',$null))
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($result.Inspections.Count -eq 3 -and $result.DuplicateExpressionCount -eq 1 -and $result.IsComplete) 'Locals and arguments merge with one duplicate removed'
    $fixture.ThrowLocals=$true
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($result.Inspections.Count -eq 2 -and -not $result.IsComplete) 'Unavailable locals still permit independent argument discovery'
    $fixture = [ScanFixture]::new(); $fixture.CollectionCount=140
    $fixture.Roots=@($fixture.Expression('frames','OpenCvSharp.Mat[]','{Mat[140]}',$null))
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($result.Inspections.Count -eq 128 -and $result.TruncatedCandidateCount -eq 12 -and $fixture.Evaluations -eq 1) 'Known collection expansion remains bounded with one count evaluation'
    $fixture = [ScanFixture]::new()
    $fixture.Roots=@($fixture.Expression('bitmap','System.Drawing.Bitmap','{Bitmap}',$null),$fixture.Expression('nothing','OpenCvSharp.Mat','null',$null),$fixture.Expression('number','int','1',$null))
    $result = Await ([AutomaticScanProbe]::Scan((Inspector),$fixture,$store))
    Check ($result.Inspections.Count -eq 0 -and $fixture.DirectReads -eq 0) 'Registered-only, null and primitive roots remain excluded'
    $fixture = [ScanFixture]::new(); $fixture.Wrapper($true,30); $fixture.DelayMilliseconds=1
    $measurement=Await ([AutomaticScanProbe]::Measure((Inspector),$fixture,$store))
    Check ($measurement.InputTicks -gt 5 -and $measurement.Candidates -eq 1) 'Dispatcher input runs repeatedly inside one nested inventory'
    $fixture=[ScanFixture]::new(); $fixture.Wrapper($false,0); $inspector=Inspector
    $mappedStore=[RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new($null,"$OutputDir\mapped-user.json")
    $mappingFile=$mappedStore.LoadUserFile(); $mappingFile.Mappings.Clear()
    $mapping=[RawBufferVisualizer.VisualStudio.ObjectSource.TypeMapping]::new(); $mapping.TypeName='Company.BufferFrame'
    $mapping.Members.Data='Data'; $mapping.Members.Width='Width'; $mapping.Members.Height='Height'
    $mappingFile.Mappings.Add($mapping); $mappedStore.Save($mappingFile)
    $result=Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$mappedStore))
    Check ($result.Inspections[0].UsesSavedMapping -and $fixture.DirectReads -eq 0 -and $result.Inspections[0].Mapping.Members.Data -eq 'Data') 'Explicit saved mapping bypasses inventory through the async entry point'
    $mapping.AssemblyName='Different.Assembly'; $mappedStore.Save($mappingFile)
    $result=Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$mappedStore))
    Check (-not $result.Inspections[0].UsesSavedMapping -and $fixture.DirectReads -gt 0) 'Unknown assembly never borrows an assembly-specific mapping after a mapping change'
    $result=Await ([AutomaticScanProbe]::Scan($inspector,$fixture,$store))
    Check (-not $result.Inspections[0].UsesSavedMapping -and -not $result.Inspections[0].UsesCachedInference) 'Changing mapping context invalidates the session type cache'
}
}
$windows = [Collections.Generic.List[object]]::new()
$transitions = [Collections.Generic.List[object]]::new()
if (-not $SkipWindow) {
    $screens = @([Windows.Forms.Screen]::AllScreens)
    $screen = if ($screens.Count -eq 2) { $screens | Sort-Object { $_.WorkingArea.Width * $_.WorkingArea.Height }, { $_.Bounds.Left } | Select-Object -First 1 } else { $screens | Select-Object -First 1 }
    if ($null -eq $screen) { throw 'A desktop monitor is required for the window workflow.' }
    $control = [RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl]::new()
    $window = [Windows.Window]::new(); $window.Title='Automatic scan responsiveness'; $window.Content=$control
    $window.Width=900; $window.Height=740; $window.Topmost=$true
    $window.Left=$screen.WorkingArea.Left+25; $window.Top=$screen.WorkingArea.Top+25
    $flags=[Reflection.BindingFlags]'Instance,NonPublic'
    function Field([string]$name) { $control.GetType().GetField($name,$flags).GetValue($control) }
    function SetField([string]$name,$value) { $control.GetType().GetField($name,$flags).SetValue($control,$value) }
    function ButtonClick([string]$name) { $control.FindName($name).RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)) }
    function Idle {
        $watch=[Diagnostics.Stopwatch]::StartNew()
        do { Pump 20; if ($watch.Elapsed.TotalSeconds -gt 30) { throw 'ToolWindow scan did not settle.' } } while ((Field '_automaticScanRunning') -or (Field '_automaticScanPending') -or (Field '_automaticScanDispatchQueued'))
    }
    function Capture([string]$name) {
        $hwnd=[Windows.Interop.WindowInteropHelper]::new($window).Handle; $rect=[ScanWindowNative+Rect]::new()
        [ScanWindowNative]::GetWindowRect($hwnd,[ref]$rect) | Out-Null
        Check ($rect.Right -gt $screen.Bounds.Left -and $rect.Left -lt $screen.Bounds.Right -and $rect.Bottom -gt $screen.Bounds.Top -and $rect.Top -lt $screen.Bounds.Bottom) "Monitor placement: $name"
        $windows.Add(@{Name=$name; Rectangle=$rect; Dpi=[Windows.Media.VisualTreeHelper]::GetDpi($window).PixelsPerInchX})
        $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top); $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size); $bitmap.Save("$OutputDir\$name.png",[Drawing.Imaging.ImageFormat]::Png) }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    try {
        SetField '_automaticInspectionPreferencesStore' ([RawBufferVisualizer.VisualStudio.AutomaticInspectionPreferencesStore]::new("$OutputDir\preferences.json"))
        $fixture=[ScanFixture]::new(); $fixture.SolutionPath="$OutputDir\test.sln"; $fixture.BufferRoots(12,'frame')
        SetField '_mappingStore' ([RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new("$OutputDir\.rawbuffervisualizer.json","$OutputDir\user.json"))
        $control.SetDte($fixture.Dte); $window.Show(); Pump 200
        $control.ScanLocals(); Idle
        $items=$control.FindName('ImageList').Items
        Check ($items.Count -eq 8) 'Actual ToolWindow first batch remains eight mapping candidates'
        $original=@($items)
        Capture 'initial-batch'
        ButtonClick 'AutomaticLoadNextButton'; ButtonClick 'AutomaticLoadNextButton'; Idle
        Check ($items.Count -eq 12) 'Repeated next-batch input produces twelve unique rows'
        Check ([object]::ReferenceEquals($items[0],$original[0])) 'Existing document identity survives next-batch loading'
        $control.ScanLocals(); Idle
        Check ($items.Count -eq 12 -and [object]::ReferenceEquals($items[0],$original[0])) 'Same-Break rescan refreshes existing rows in place'
        $scenarios = if ($Baseline) { @('stop') } else { @('stop','newer','run','design','disable','clear','dispose') }
        foreach ($scenario in $scenarios) {
            $fixture.OnRead=$null; $fixture.DelayMilliseconds=0; $fixture.BufferRoots(80,'frame'); $fixture.DelayMilliseconds=1
            $state=@{Queued=$false; Error=$null}
            $action=[Action]{
                try {
                    switch ($scenario) {
                        'stop' {
                            if ($Baseline) { ButtonClick 'AutomaticStopButton' }
                            else {
                                $stop=$control.FindName('AutomaticStopButton'); $stop.Focus() | Out-Null
                                $point=$stop.PointToScreen([Windows.Point]::new($stop.ActualWidth/2,$stop.ActualHeight/2))
                                [ScanWindowNative]::SetCursorPos([int]$point.X,[int]$point.Y) | Out-Null; Pump 20
                                Check ($stop.IsMouseOver -and $stop.IsKeyboardFocused) 'Stop is reachable by pointer and keyboard focus while discovering'
                                Capture 'stop-hover-focus'
                                [ScanWindowNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Pump 20
                                Check $stop.IsPressed 'Actual pointer-down reaches Stop during discovery'
                                Capture 'stop-pressed'
                                [ScanWindowNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
                            }
                        }
                        'newer' { $fixture.OnRead=$null; $fixture.DelayMilliseconds=0; $fixture.BufferRoots(4,'newFrame'); $fixture.FrameName='Newest.Frame'; $control.ScanLocals(); $control.ScanLocals() }
                        'run' { $fixture.Mode=[EnvDTE.dbgDebugMode]::dbgRunMode; $control.InvalidateLiveSources() | Out-Null }
                        'design' { $fixture.Mode=[EnvDTE.dbgDebugMode]::dbgDesignMode; $control.EndAutomaticInspectionSession() }
                        'disable' { $control.FindName('AutoInspectBox').IsChecked=$false }
                        'clear' { ButtonClick 'ClearButton' }
                        'dispose' { $control.Dispose() }
                    }
                } catch { $state.Error=$_ }
            }.GetNewClosure()
            $fixture.OnRead=[Action]{
                if (-not $state.Queued -and $fixture.RootReads -ge 3) {
                    $state.Queued=$true
                    if ($scenario -eq 'stop') { Capture 'discovering-before-stop' }
                    [Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Input,$action) | Out-Null
                }
            }.GetNewClosure()
            $fixture.Mode=[EnvDTE.dbgDebugMode]::dbgBreakMode
            if ($control.FindName('AutoInspectBox').IsChecked -ne $true) { $control.FindName('AutoInspectBox').IsChecked=$true }
            $control.ScanLocals(); Idle
            if ($state.Error) { throw $state.Error }
            Check $state.Queued "Transition was triggered during discovery: $scenario"
            $transitions.Add(@{Scenario=$scenario; RootsRead=$fixture.RootReads; DocumentCount=$items.Count})
            if (-not $Baseline) {
                Check ($fixture.RootReads -lt 80) "No remaining obsolete roots read after $scenario"
                if ($scenario -eq 'stop') { Check (-not (Field '_automaticScanResult').IsComplete -and $items.Count -eq 12) 'Stopped discovery retains unmatched existing rows'; Capture 'stopped' }
                elseif ($scenario -eq 'newer') { Check ($items.Count -eq 4 -and $control.FindName('AutomaticFrameText').Text -eq 'Newest.Frame') 'Only the newest coalesced scan publishes results' }
                else { Check ($null -eq (Field '_automaticScanResult')) "No old discovery result published after $scenario" }
                if ($scenario -in @('clear','dispose')) { Check ($items.Count -eq 0) "No rows resurrected after $scenario" }
            }
        }
    } finally { $fixture.OnRead=$null; $control.Dispose(); $window.Close() }
}
@{ Status='Complete'; Baseline=[bool]$Baseline; Checks=$checks; Measurements=$measurements; UnreadableMembers=$unreadable; NestedMembers=$nested; Transitions=$transitions; Windows=$windows; Monitor=if ($screen) {$screen.DeviceName} else {$null}; Bounds=if ($screen) {$screen.Bounds.ToString()} else {$null}; Boundary='Compiled scanner and actual ToolWindow in an STA WPF host with controlled EnvDTE interface proxies; injected call latency is not an installed debugger benchmark.' } | ConvertTo-Json -Depth 6 | Set-Content "$OutputDir\verification.json" -Encoding UTF8
Write-Host "Automatic discovery probe passed: $($checks.Count) assertions."

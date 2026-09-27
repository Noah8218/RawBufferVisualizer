param(
    [string]$BuildRoot = '',
    [string]$OutputDir = '',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path $repoRoot 'artifacts' }
if (-not (Test-Path -LiteralPath 'D:\')) { Write-Warning "D: is unavailable; using $testRoot for test outputs." }
if ([string]::IsNullOrWhiteSpace($BuildRoot)) { $BuildRoot = Join-Path $testRoot 'build' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $testRoot 'ui\histogram' }
New-Item -ItemType Directory -Force -Path $OutputDir, "$OutputDir\temp" | Out-Null
$env:TEMP = "$OutputDir\temp"; $env:TMP = $env:TEMP
if (-not $NoBuild) {
    foreach ($project in @('RawBufferVisualizer.VisualStudio.Vssdk', 'RawBufferVisualizer.Wpf')) {
        dotnet build "$repoRoot\src\$project\$project.csproj" -c Release -p:CreateVsixContainer=false "-p:RawBufferVisualizerBuildRoot=$BuildRoot" *> "$OutputDir\build-$project.log"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
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
[Microsoft.VisualStudio.Shell.ThreadHelper].GetMethod('SetUIThread', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())
Add-Type -ReferencedAssemblies @("$assemblyDirectory\RawBufferVisualizer.Core.dll", 'System.dll', 'System.Core.dll') -TypeDefinition @'
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using RawBufferVisualizer.Core;
public static class HistogramNative {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
public sealed class HistogramProbe : RawImageSource {
    public readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false);
    public readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);
    public bool IgnoreCancellation, Fail, Live, Disposed;
    public int Calls, WorkerThread;
    public static int Active, MaximumActive;
    readonly RawImageSource inner;
    public HistogramProbe(byte value) : this(value, false) { }
    public HistogramProbe(byte value, bool color) : base(new RawImageDescriptor { Width=1, Height=1, Stride=color ? 3 : 1, PixelFormat=color ? RawPixelFormat.RGB24 : RawPixelFormat.Mono8, ValidBits=8 }, color ? 3 : 1, null) {
        inner=RawImageSource.FromMemory(color ? new byte[] { value, (byte)(value+10), (byte)(value+20) } : new byte[] { value }, Descriptor);
    }
    public override bool IsFileBacked { get { return false; } }
    public override bool IsLiveProcessBacked { get { return Live; } }
    public override RawPixelHistogram GetHistogram(RawHistogramChannel channel, CancellationToken token) {
        Interlocked.Increment(ref Calls); WorkerThread=Thread.CurrentThread.ManagedThreadId;
        int active=Interlocked.Increment(ref Active); MaximumActive=Math.Max(MaximumActive,active); Entered.Set();
        try {
            var deadline=DateTime.UtcNow.AddSeconds(20);
            while (!Gate.Wait(10)) { if (!IgnoreCancellation) token.ThrowIfCancellationRequested(); if(DateTime.UtcNow>deadline) throw new TimeoutException("Probe gate timeout"); }
            if (!IgnoreCancellation) token.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("Test read failure");
            return inner.GetHistogram(channel, CancellationToken.None);
        } finally { Interlocked.Decrement(ref Active); }
    }
    public override RawImageSource WithDescriptor(RawImageDescriptor d) { return inner.WithDescriptor(d); }
    public override RawRenderOptions CreateRenderOptions() { return inner.CreateRenderOptions(); }
    public override RenderedImage RenderTile(int x,int y,int w,int h,RawRenderOptions o) { return inner.RenderTile(x,y,w,h,o); }
    public override string DescribePixel(int x,int y) { return inner.DescribePixel(x,y); }
    public override byte[] ReadAllBytes() { throw new InvalidOperationException("Full frame read is forbidden"); }
    public override void CopyRawTo(string path) { throw new NotSupportedException(); }
    public override void Dispose() { Disposed=true; inner.Dispose(); Gate.Dispose(); Entered.Dispose(); }
}
public sealed class HistogramLease : IDisposable {
    public static int Acquired, Released;
    public static bool FailRelease;
    public static readonly Func<IDisposable> Factory = () => { Acquired++; return new HistogramLease(); };
    public void Dispose() { Released++; if(FailRelease) throw new IOException("Test lease release failure"); }
}
public sealed class HistogramNotifications {
    public int Count, WrongThread;
    readonly int thread=Thread.CurrentThread.ManagedThreadId;
    public HistogramNotifications(object vm) { ((INotifyPropertyChanged)vm).PropertyChanged += (s,e) => { Count++; if(Thread.CurrentThread.ManagedThreadId!=thread) WrongThread++; }; }
}
'@
$screens = @([Windows.Forms.Screen]::AllScreens)
$screen = if ($screens.Count -eq 2) { $screens | Sort-Object { $_.WorkingArea.Width * $_.WorkingArea.Height }, { $_.Bounds.Left } | Select-Object -First 1 } else { $screens | Select-Object -First 1 }
if ($null -eq $screen) { throw 'A desktop monitor is required for visual verification.' }
$checks = [Collections.Generic.List[string]]::new()
$captures = [Collections.Generic.List[object]]::new()
function Check($condition, [string]$message) { if (-not $condition) { throw $message }; $checks.Add($message); Write-Host "PASS $message" }
function Pump([int]$milliseconds = 30) {
    $frame = [Windows.Threading.DispatcherFrame]::new(); $timer = [Windows.Threading.DispatcherTimer]::new()
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
    $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Until([scriptblock]$predicate) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (& $predicate)) { if ([DateTime]::UtcNow -gt $deadline) { throw 'Dispatcher condition timed out.' }; Pump }
    Pump
}
function SetSource($vm, $source, $lease = $null, $reason = $null) { $vm.SetSource($source, $lease, $reason) }
function Done($vm) { Until { $vm.Completion.IsCompleted }; if ($vm.Completion.IsFaulted) { throw $vm.Completion.Exception }; Pump }
function Place($window) { $window.WindowStartupLocation='Manual'; $window.Left=$screen.WorkingArea.Left+20; $window.Top=$screen.WorkingArea.Top+15; $window.Topmost=$true }
function Capture($window, [string]$name) {
    Pump 120
    $rect=[HistogramNative+Rect]::new()
    [HistogramNative]::GetWindowRect([Windows.Interop.WindowInteropHelper]::new($window).Handle,[ref]$rect)|Out-Null
    Check ($rect.Right -gt $screen.Bounds.Left -and $rect.Left -lt $screen.Bounds.Right -and $rect.Bottom -gt $screen.Bounds.Top -and $rect.Top -lt $screen.Bounds.Bottom) "Monitor: $name"
    $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top); $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size); $bitmap.Save("$OutputDir\$name.png") } finally { $graphics.Dispose();$bitmap.Dispose() }
    $captures.Add(@{Name=$name;Rectangle=$rect;Dpi=[Windows.Media.VisualTreeHelper]::GetDpi($window).PixelsPerInchX})
}
function Pointer($control) {
    $point=$control.PointToScreen([Windows.Point]::new($control.ActualWidth/2,$control.ActualHeight/2))
    [HistogramNative]::SetCursorPos([int]$point.X,[int]$point.Y)|Out-Null; Pump
}
function Press($control) { Pointer $control; [HistogramNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Pump }
function Release { [HistogramNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Pump }
function Click($control) { Press $control; Release }
function Key([byte]$key) { [HistogramNative]::keybd_event($key,0,0,[UIntPtr]::Zero);Pump;[HistogramNative]::keybd_event($key,0,2,[UIntPtr]::Zero);Pump }
function Modal($window, [scriptblock]$action) {
    $state=@{Error=$null};$timer=[Windows.Threading.DispatcherTimer]::new();$timer.Interval=[TimeSpan]::FromMilliseconds(50)
    $timer.Add_Tick({
        $timer.Stop()
        try { & $action } catch { $state.Error=$_ } finally { $window.Close() }
    }.GetNewClosure())
    $timer.Start()
    try { $window.ShowDialog()|Out-Null } finally { $timer.Stop() }
    if($state.Error) { throw $state.Error }
}
function BoundResult($view,$vm) {
    Pump
    Check ([object]::ReferenceEquals($view.FindName('HistogramCanvas').Histogram,$vm.Result)) 'Rendered plot binding matches current result'
    Check ($view.FindName('HistogramCoverage').Text -eq $vm.Coverage -and $view.FindName('HistogramStatus').Text -eq $vm.Status) 'Rendered coverage/status match ViewModel'
    Check ($view.FindName('HistogramSourceDescription').Text -eq $vm.SourceDescription -and $view.FindName('HistogramValueRange').Text -eq $vm.ValueRange -and $view.FindName('HistogramExclusions').Text -eq $vm.Exclusions) 'Rendered value basis/range/exclusions match current result'
    Check ($view.FindName('HistogramLowerLabel').Text -eq $vm.LowerLabel -and $view.FindName('HistogramUpperLabel').Text -eq $vm.UpperLabel) 'Rendered axes match numeric labels'
    Check ($view.FindName('HistogramFrequencyScale').Text -eq $vm.FrequencyScale -and $view.FindName('HistogramAxisDescription').Text -eq $vm.AxisDescription) 'Rendered frequency and value-axis explanations match the result'
    Check ($view.FindName('HistogramSamplingDescription').Text -eq $vm.SamplingDescription -and $view.FindName('HistogramChannelDescription').Text -eq $vm.ChannelDescription) 'Rendered sampling and RGB-mean explanations follow the current source/channel'
    $textColor=$view.FindResource('RbvTextBrush').Color
    Check ($view.FindName('HistogramCoverage').Foreground.Color -eq $textColor -and $view.FindName('HistogramStatus').Foreground.Color -eq $textColor) 'Optional text preserves the rendered theme foreground'
}
function Exercise($window,$view,$vm,$hostName) {
    $button=$view.FindName('HistogramAction'); $combo=$view.FindName('HistogramChannel')
    $watch=[HistogramNotifications]::new($vm)
    $descriptor=[RawBufferVisualizer.Core.RawImageDescriptor]::new(); $descriptor.Width=3;$descriptor.Height=1;$descriptor.Stride=9;$descriptor.PixelFormat='RGB24';$descriptor.ValidBits=8
    $color=[RawBufferVisualizer.Core.RawImageSource]::FromMemory([byte[]]@(9,30,90,9,30,90,9,30,90),$descriptor)
    SetSource $vm $color; Done $vm; $view.BringIntoView(); Pump
    Check ($vm.HasChannels -and $vm.Result.Minimum -eq 43 -and $combo.SelectedIndex -eq 0) "$hostName RGB mean and initial binding"
    $vm.SelectedChannelIndex=1; Done $vm
    Check ($combo.SelectedIndex -eq 1 -and $vm.Result.Minimum -eq 9) "$hostName model-to-view R selection"
    $combo.SelectedIndex=2; Done $vm
    Check ($vm.SelectedChannelIndex -eq 2 -and $vm.Result.Minimum -eq 30) "$hostName view-to-model G selection"
    # A real click grants foreground input to the isolated host before keyboard input.
    Click $combo; Check $combo.IsDropDownOpen "$hostName pointer opens channel popup"; Capture $window "$hostName-channel-popup"
    Key 0x1B; Check (-not $combo.IsDropDownOpen) "$hostName Escape closes popup"
    $combo.Focus()|Out-Null; Pump
    Check $combo.IsKeyboardFocused "$hostName channel receives keyboard focus"
    Key 0x73; Check $combo.IsDropDownOpen "$hostName keyboard opens channel popup"
    Key 0x28; Key 0x0D; Done $vm
    Check ($vm.SelectedChannelIndex -eq 3 -and $vm.Result.Minimum -eq 90) "$hostName keyboard B selection"
    $button.Focus()|Out-Null; Key 0x09; Check $combo.IsKeyboardFocused "$hostName Tab moves from action to channel"
    [HistogramNative]::keybd_event(0x10,0,0,[UIntPtr]::Zero);Key 0x09;[HistogramNative]::keybd_event(0x10,0,2,[UIntPtr]::Zero);Pump
    Check $button.IsKeyboardFocused "$hostName Shift+Tab returns to action"
    $button.Focus()|Out-Null; Pointer $button; Check ($button.IsKeyboardFocused -and $button.IsMouseOver) "$hostName focused/hover action"
    Capture $window "$hostName-hover-focus"
    Press $button; Check $button.IsPressed "$hostName actual pointer-down state"; Capture $window "$hostName-pressed"
    [HistogramNative]::SetCursorPos($screen.WorkingArea.Left+5,$screen.WorkingArea.Top+5)|Out-Null; Release
    Check (-not $button.IsPressed -and -not $button.IsMouseOver) "$hostName mouse-leave recovery"
    $changing=[HistogramProbe]::new(20,$true);$changing.IgnoreCancellation=$true
    SetSource $vm $changing; Until { $changing.Entered.IsSet };$combo.SelectedIndex=1;$combo.SelectedIndex=3;Pump;$changing.Gate.Set();Done $vm
    Check ($vm.Result.Minimum -eq 40 -and $changing.Calls -eq 2 -and $combo.SelectedIndex -eq 3) "$hostName channel changes discard the earlier channel result"
    $slow=[HistogramProbe]::new(11);$slow.IgnoreCancellation=$true
    SetSource $vm $slow ([HistogramLease]::Factory); Until { $slow.Entered.IsSet }
    Check ($vm.IsRunning -and $button.Content -eq 'Cancel' -and $slow.WorkerThread -ne [Threading.Thread]::CurrentThread.ManagedThreadId) "$hostName calculation runs off UI thread"
    Capture $window "$hostName-calculating"
    Click $button
    Check (-not $button.IsEnabled -and $vm.Result -eq $null -and $vm.Status -eq 'Cancelled.') "$hostName Cancel clears result and prevents duplicate action"
    Capture $window "$hostName-cancelled-disabled"
    Check ([HistogramLease]::Acquired -eq [HistogramLease]::Released+1) "$hostName borrowed lifetime retained until worker completes"
    $slow.Gate.Set(); Done $vm
    Check ($vm.Result -eq $null -and [HistogramLease]::Acquired -eq [HistogramLease]::Released) "$hostName late cancelled result discarded and lease released"
    Click $button; Done $vm; Check ($vm.Result.Minimum -eq 11 -and $slow.Calls -eq 2) "$hostName Refresh succeeds after cancellation"
    $first=[HistogramProbe]::new(12);$first.IgnoreCancellation=$true
    $middle=[HistogramProbe]::new(13);$last=[HistogramProbe]::new(14);$last.Gate.Set()
    SetSource $vm $first; Until { $first.Entered.IsSet }; SetSource $vm $middle; SetSource $vm $last; $first.Gate.Set(); Done $vm
    Check ($vm.Result.Minimum -eq 14 -and $middle.Calls -eq 0 -and [HistogramProbe]::MaximumActive -eq 1) "$hostName rapid changes coalesce to latest without parallel reads"
    BoundResult $view $vm
    $before=$last.Calls; $window.Width=$window.Width-20; Pump; $window.Width=$window.Width+20; Pump
    Check ($last.Calls -eq $before) "$hostName resize redraws without source reads"
    $last.Fail=$true; Click $button; Done $vm
    Check ($vm.Result -eq $null -and $vm.Status -like '*Test read failure*' -and $button.IsEnabled) "$hostName read failure clears graph and permits retry"
    Capture $window "$hostName-read-failure"
    $last.Fail=$false; Click $button; Done $vm; Check ($vm.Result.Minimum -eq 14) "$hostName retry recovers"
    [HistogramLease]::FailRelease=$true; SetSource $vm $slow ([HistogramLease]::Factory); Done $vm
    Check (-not $vm.IsRunning -and $vm.Result -eq $null -and $vm.Status -like '*Test lease release failure*') "$hostName release failure settles state"
    [HistogramLease]::FailRelease=$false
    $live=[HistogramProbe]::new(15);$live.Live=$true;$live.IgnoreCancellation=$true
    SetSource $vm $live; Until { $live.Entered.IsSet }
    if ($hostName -eq 'docked') { $window.Content.InvalidateLiveSources()|Out-Null } else { $vm.OnDebuggerResumed() }
    $live.Gate.Set(); Done $vm
    Check ($vm.Result -eq $null -and $vm.Status -like '*debuggee is running*' -and -not $button.IsEnabled) "$hostName Continue invalidates live histogram"
    Capture $window "$hostName-live-unavailable"
    SetSource $vm $color; Done $vm; SetSource $vm $null; Pump
    Check ($vm.Result -eq $null -and -not $button.IsEnabled) "$hostName empty source disables action"
    $descriptor.PixelFormat='Int32';$descriptor.Stride=12;$descriptor.ValidBits=32
    $bytes=[byte[]]([BitConverter]::GetBytes([int]::MinValue)+[BitConverter]::GetBytes([int]0)+[BitConverter]::GetBytes([int]::MaxValue))
    $signed=[RawBufferVisualizer.Core.RawImageSource]::FromMemory($bytes,$descriptor);SetSource $vm $signed;Done $vm
    Check ($vm.LowerLabel -eq '-2147483648' -and $vm.UpperLabel -eq '2147483647' -and -not $combo.IsVisible) "$hostName exact Int32 range and scalar channel visibility"
    BoundResult $view $vm
    Capture $window "$hostName-int32-long-range"
    $descriptor.PixelFormat='Float32'
    $bytes=[byte[]]([BitConverter]::GetBytes([float]::NaN)+[BitConverter]::GetBytes([float]::PositiveInfinity)+[BitConverter]::GetBytes([float]::NegativeInfinity))
    $nonfinite=[RawBufferVisualizer.Core.RawImageSource]::FromMemory($bytes,$descriptor); SetSource $vm $nonfinite; Done $vm
    Check ($vm.Exclusions -like '*3*' -and $vm.Status -like 'No finite*' -and $vm.LowerLabel -eq '') "$hostName non-finite inputs show explicit empty plot state"
    BoundResult $view $vm
    Capture $window "$hostName-nonfinite"
    $difference=[RawBufferVisualizer.Core.RawImageDifferenceSource]::new($color,$color); SetSource $vm $difference; Done $vm
    Check ($vm.SourceDescription -like 'Display values*' -and $vm.Result.Minimum -eq 0) "$hostName generated comparison is labelled display values"
    BoundResult $view $vm
    Check ($watch.WrongThread -eq 0) "$hostName result notifications stay on UI thread"
    foreach($probe in @($slow,$first,$middle,$last,$live,$changing)) { Check (-not $probe.Disposed) "$hostName source lifetime remains with caller"; $probe.Dispose() }
    SetSource $vm $null
    foreach($source in @($color,$signed,$nonfinite,$difference)) { $source.Dispose() }
}
function ClosePending($window,$vm,$hostName) {
    $source=[HistogramProbe]::new(42);$source.IgnoreCancellation=$true
    SetSource $vm $source ([HistogramLease]::Factory); Until { $source.Entered.IsSet }
    $watch=[HistogramNotifications]::new($vm)
    if($hostName -eq 'docked') { $window.Content.Dispose() }
    $window.Close(); $count=$watch.Count
    $source.Gate.Set(); Done $vm
    Check ($vm.Result -eq $null -and $watch.Count -eq $count -and [HistogramLease]::Acquired -eq [HistogramLease]::Released -and -not $source.Disposed) "$hostName close rejects pending result, releases lease and preserves caller ownership"
    $source.Dispose()
}
$descriptor=[RawBufferVisualizer.Core.RawImageDescriptor]::new();$descriptor.Width=320;$descriptor.Height=240;$descriptor.Stride=640;$descriptor.PixelFormat='Mono16';$descriptor.ValidBits=12
$bytes=[byte[]]::new(320*240*2)
for($i=0;$i -lt 320*240;$i++) { $value=($i%320)*12; $bytes[$i*2]=($value -band 255);$bytes[$i*2+1]=($value -shr 8) }
    $path="$OutputDir\sample.rbuf.json";[RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save($path,$bytes,$descriptor)|Out-Null
$control=[RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl]::new();$window=[Windows.Window]::new();$window.Content=$control;$window.Width=1160;$window.Height=920;Place $window
try { Modal $window {
    Pump;$control.OpenPath($path);$view=$control.FindName('HistogramPanel');$vm=$view.DataContext;Done $vm;$view.BringIntoView();Pump
    Check ($vm.Result.SampleCount -eq 61440 -and $vm.Result.TotalPixels -eq 76800 -and $vm.SourceDescription -like 'Raw values / Mono16*') 'Docked OpenPath uses raw bounded histogram'
    Capture $window 'after-docked-wide'
    Exercise $window $view $vm 'docked'
    $control.OpenPath($path);Done $vm;$window.Width=540;Pump;$control.FindName('InspectorToggleButton').IsChecked=$true;Pump
    $compact=$control.FindName('CompactHistogramPanel');$compact.BringIntoView();Pump
    Check ([object]::ReferenceEquals($compact.DataContext,$vm) -and $compact.IsVisible) 'Compact and wide inspectors share one histogram state'
    BoundResult $compact $vm; Capture $window 'after-docked-compact'
    $scroll=$control.FindName('CompactInspectorTabs').Items[0].Content;$scroll.ScrollToTop();Capture $window 'after-docked-compact-top';$scroll.ScrollToBottom();Capture $window 'after-docked-compact-bottom'
    $hidden=[HistogramProbe]::new(66);SetSource $vm $hidden;Until {$hidden.Entered.IsSet};$control.FindName('InspectorToggleButton').IsChecked=$false;Done $vm
    Check (-not $vm.IsRunning -and $vm.Result -eq $null -and -not $compact.IsVisible) 'Inspector hide cancels current histogram'
    $hidden.Gate.Set();$control.FindName('InspectorToggleButton').IsChecked=$true;Done $vm
    Check ($vm.Result.Minimum -eq 66 -and $hidden.Calls -eq 2) 'Inspector reopen starts the current source once'
    $hidden.Dispose()
    $cleared=[HistogramProbe]::new(67);$cleared.IgnoreCancellation=$true;SetSource $vm $cleared;Until {$cleared.Entered.IsSet}
    $control.GetType().GetMethod('Clear_Click',[Reflection.BindingFlags]'NonPublic,Instance').Invoke($control,@($null,[Windows.RoutedEventArgs]::new()))|Out-Null;Pump
    $cleared.Gate.Set();Done $vm;$cleared.Dispose()
    Check ($vm.Result -eq $null -and -not $vm.ActionCommand.CanExecute($null)) 'Actual docked Clear invalidates histogram'
    ClosePending $window $vm 'docked'
} } finally { $control.Dispose();$window.Close() }
$wpf=[Reflection.Assembly]::LoadFrom("$BuildRoot\bin\RawBufferVisualizer.Wpf\Release\net472\RawBufferVisualizer.Wpf.exe")
# Base Application avoids invoking the product command-line startup in a test host.
$app=[Windows.Application]::new();$app.ShutdownMode='OnExplicitShutdown'
[xml]$appXaml=Get-Content "$repoRoot\src\RawBufferVisualizer.Wpf\App.xaml" -Raw
$app.Resources=[Windows.Markup.XamlReader]::Parse('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+$appXaml.Application.'Application.Resources'.InnerXml+'</ResourceDictionary>')
$window=[RawBufferVisualizer.Wpf.MainWindow]::new();Place $window
try { Modal $window {
    Pump;$window.OpenPath($path);$view=$window.FindName('HistogramPanel');$vm=$view.DataContext;Done $vm
    Check ($vm.Result.SampleCount -eq 61440 -and $vm.Result.UpperBound -eq 4095) 'Standalone OpenPath uses same raw histogram'
    Capture $window 'after-standalone'
    Exercise $window $view $vm 'standalone'
    $window.OpenPath($path);Done $vm;$window.Width=$window.MinWidth;Pump;Capture $window 'after-standalone-min-width'
    $window.WindowState='Maximized';Pump;Capture $window 'after-standalone-maximized';$window.WindowState='Normal';Pump
    ClosePending $window $vm 'standalone'
} } finally { $window.Close();$app.Shutdown() }
@{Status='Complete';Checks=$checks;Screens=$screens;SelectedMonitor=$screen.DeviceName;Bounds=$screen.Bounds;Captures=$captures;Boundary='Compiled viewers in an STA WPF host, existing dark resources and actual reported DPI. Installed IDE/registered launch and OS DPI changes are not exercised.'}|ConvertTo-Json -Depth 6|Set-Content "$OutputDir\ui-verification.json"

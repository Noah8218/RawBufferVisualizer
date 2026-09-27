param(
    [string]$BuildRoot = '',
    [string]$OutputDir = '',
    [switch]$NoBuild,
    [ValidateSet('all','mapping','export')][string]$Area = 'all',
    [string[]]$ExportScenarios = @('success','cancel','failure','shutdown','escape','owner_close','live_continue','viewer_close')
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path $repoRoot 'artifacts' }
if (-not (Test-Path -LiteralPath 'D:\')) { Write-Warning "D: is unavailable; using $testRoot for test outputs." }
if ([string]::IsNullOrWhiteSpace($BuildRoot)) { $BuildRoot = Join-Path $testRoot 'build' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $testRoot 'ui\mapping-save' }
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
if ($null -eq $interop) { throw 'Installed Visual Studio interop assembly missing.' }
[Reflection.Assembly]::LoadFrom($interop.FullName) | Out-Null
$packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$env:USERPROFILE\.nuget\packages" }
$vssdkTools = "$packageRoot\microsoft.vssdk.buildtools\$toolsVersion\tools\vssdk"
foreach ($name in @('Microsoft.VisualStudio.Validation','Microsoft.VisualStudio.Threading','Microsoft.VisualStudio.Shell.Framework','Microsoft.VisualStudio.Shell.15.0')) { [Reflection.Assembly]::LoadFrom("$vssdkTools\$name.dll") | Out-Null }
foreach ($name in @('Microsoft.VisualStudio.Imaging','Microsoft.VisualStudio.ImageCatalog','RawBufferVisualizer.Core','RawBufferVisualizer.Sdk','RawBufferVisualizer.VisualStudio.ObjectSource','RawBufferVisualizer.VisualStudio','RawBufferVisualizer.OpenGlCanvas')) { [Reflection.Assembly]::LoadFrom("$assemblyDirectory\$name.dll") | Out-Null }
$assembly = [Reflection.Assembly]::LoadFrom("$assemblyDirectory\RawBufferVisualizer.VisualStudio.Vssdk.dll")
[System.Threading.SynchronizationContext]::SetSynchronizationContext([System.Windows.Threading.DispatcherSynchronizationContext]::new())
Add-Type -ReferencedAssemblies "$assemblyDirectory\RawBufferVisualizer.Core.dll" -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
using RawBufferVisualizer.Core;
public static class MappingSaveNative {
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
}
public sealed class ExportSmokeSource : RawImageSource {
 public bool Live;
 public int Delay = 80;
 public long FailAt = long.MaxValue;
 public int Reads;
 public ExportSmokeSource(long length) : base(new RawImageDescriptor { Width = 1, Height = 1, Stride = 1, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 }, length, null) { }
 public override bool IsFileBacked { get { return false; } }
 public override bool IsLiveProcessBacked { get { return Live; } }
 public override bool TryReadRange(long offset, byte[] buffer, int start, int count) { Interlocked.Increment(ref Reads); Thread.Sleep(Delay); return offset < FailAt; }
 public override RawImageSource WithDescriptor(RawImageDescriptor descriptor) { throw new NotSupportedException(); }
 public override RawRenderOptions CreateRenderOptions() { throw new NotSupportedException(); }
 public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions options) { throw new NotSupportedException(); }
 public override string DescribePixel(int x, int y) { throw new NotSupportedException(); }
 public override byte[] ReadAllBytes() { throw new NotSupportedException(); }
 public override void CopyRawTo(string path) { throw new NotSupportedException(); }
}
"@
$testScreens = @([System.Windows.Forms.Screen]::AllScreens)
$testScreen = if ($testScreens.Count -eq 2) { $testScreens | Sort-Object { $_.WorkingArea.Width * $_.WorkingArea.Height }, { $_.Bounds.Left } | Select-Object -First 1 } else { $testScreens | Select-Object -First 1 }
if ($null -eq $testScreen) { throw 'A desktop monitor is required for this visual smoke.' }
$checks = [Collections.Generic.List[string]]::new()
$windows = [Collections.Generic.List[object]]::new()
function Check($condition, [string]$message) { if (-not $condition) { throw $message }; $checks.Add($message) }
function Pump([int]$milliseconds = 40) {
 $frame = [System.Windows.Threading.DispatcherFrame]::new()
 $timer = [System.Windows.Threading.DispatcherTimer]::new()
 $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
 $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
 $timer.Start(); [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Place($window) {
 $window.Topmost = $true
 $window.WindowStartupLocation = 'Manual'
 $window.Left = $testScreen.WorkingArea.Left + 35; $window.Top = $testScreen.WorkingArea.Top + 30
}
function Capture($window, [string]$name) {
 Pump 200
 $hwnd = [System.Windows.Interop.WindowInteropHelper]::new($window).Handle
 $rect = [MappingSaveNative+Rect]::new()
 [MappingSaveNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
 Check ($rect.Right -gt $testScreen.Bounds.Left -and $rect.Left -lt $testScreen.Bounds.Right -and $rect.Bottom -gt $testScreen.Bounds.Top -and $rect.Top -lt $testScreen.Bounds.Bottom) "Monitor placement: $name"
 $windows.Add(@{ Name = $name; Rectangle = $rect; Dpi = [System.Windows.Media.VisualTreeHelper]::GetDpi($window).PixelsPerInchX })
 $bitmap = [Drawing.Bitmap]::new($rect.Right - $rect.Left, $rect.Bottom - $rect.Top)
 $graphics = [Drawing.Graphics]::FromImage($bitmap)
 try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size); $bitmap.Save("$OutputDir\$name.png", [Drawing.Imaging.ImageFormat]::Png) }
 finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Pointer($control) {
 $point = $control.PointToScreen([System.Windows.Point]::new($control.ActualWidth / 2, $control.ActualHeight / 2))
 [MappingSaveNative]::SetCursorPos([int]$point.X, [int]$point.Y) | Out-Null
 Pump
}
function Press($control) { Pointer $control; [MappingSaveNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Pump }
function Release { [MappingSaveNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Pump }
function Click($control) { Press $control; Release }
function Modal($window, [scriptblock]$action) {
 $driveState = @{ Error = $null }
 $timer = [System.Windows.Threading.DispatcherTimer]::new()
 $timer.Interval = [TimeSpan]::FromMilliseconds(100)
 $timer.Add_Tick({
  $timer.Stop()
  try { & $action $window } catch { $driveState.Error = $_; $window.Close() }
 }.GetNewClosure())
 Place $window; $timer.Start()
 try { $result = $window.ShowDialog() } finally { $timer.Stop(); if ($window.IsVisible) { $window.Close() } }
 if ($driveState.Error) { throw $driveState.Error }
 return $result
}
$runRoot = Join-Path $OutputDir ('data-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $runRoot | Out-Null
$solutionPath = "$runRoot\.rawbuffervisualizer.json"; $userPath = "$runRoot\user.json"
$store = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new($solutionPath, $userPath)
$inventory = [Collections.Generic.List[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]]::new()
foreach ($entry in @(@('Buffer','IntPtr','0x12345678'), @('Width','Int32','640'), @('Height','Int32','480'), @('Stride','Int32','640'))) {
 $item = [RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]::new()
 $item.Name = $entry[0]; $item.TypeName = $entry[1]; $item.SampleValue = $entry[2]; $inventory.Add($item)
}
function Mapper([string]$type = 'Company.Vision.Frame') { [RawBufferVisualizer.VisualStudio.Vssdk.TypeMappingDialog]::new($inventory, $type, 'Company.Vision', 0, $null, $false, $store) }
try {
 if ($Area -ne 'export') {
 $dialog = Mapper
 $saved = Modal $dialog {
  param($view)
  $scope = $view.FindName('MappingScopeBox')
  Check ($scope.SelectedValue.ToString() -eq 'Solution') 'New mapping defaults to current solution'
  $scope.SelectedValue = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingScope]::User
  Check ($view.DataContext.SavePath -eq $userPath) 'Scope binding writes ViewModel and path'
  $view.DataContext.SelectedScope = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingScope]::Solution
  Pump
  Check ($scope.SelectedValue.ToString() -eq 'Solution') 'Scope binding restores rendered selection'
  Check (-not (Test-Path $solutionPath) -and -not (Test-Path $userPath)) 'Open and scope changes do not write files'
  $view.FindName('UseSuggestedRolesButton').RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Button]::ClickEvent))
  Check ($null -eq $view.FindName('PreviewImage').Source -and -not (Test-Path $solutionPath)) 'Reset does not preview or save'
  Capture $view 'after-mapping'
  Click $scope
  Check $scope.IsDropDownOpen 'Mouse opens scope popup'
  [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); Pump
  $scope.Focus() | Out-Null
  [System.Windows.Forms.SendKeys]::SendWait('{F4}'); Pump
  Check $scope.IsDropDownOpen 'Keyboard opens scope popup'
  Capture $view 'after-scope-popup'
  $scope.IsDropDownOpen = $false
  $button = $view.FindName('SaveButton')
  $button.Focus() | Out-Null; Pump
  Capture $view 'after-save-focus'
  Press $button
  Check $button.IsPressed 'Actual pointer-down reaches Save button'
  Capture $view 'after-save-pressed'
  [MappingSaveNative]::SetCursorPos($testScreen.WorkingArea.Left + 10, $testScreen.WorkingArea.Top + 10) | Out-Null
  Pump
  Release
  Check (-not (Test-Path $solutionPath) -and -not $button.IsPressed) 'Mouse-leave recovers without an accidental save'
  $view.FindName('ByteOrderBox').SelectedItem = 'BigEndian'
  Click $button
 }
 Check ($saved -and (Test-Path $solutionPath) -and -not (Test-Path $userPath)) 'Save button commits selected scope'
 $dialog = Mapper
 Modal $dialog {
  param($view)
  Check ($view.FindName('ByteOrderBox').SelectedItem -eq 'BigEndian') 'Reopen restores saved mapping values'
  Check ($view.DataContext.SelectedScope.ToString() -eq 'Solution') 'Reopen restores mapping scope'
  $view.FindName('MappingScopeBox').SelectedValue = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingScope]::User
  Check ($view.FindName('MappingSaveStatus').Text -like '*takes priority*') 'User scope explains existing solution precedence'
  Click ($view.FindName('CancelButton'))
 } | Out-Null
 $dialog = Mapper 'Company.Vision.AnotherFrame'
 $before = [IO.File]::ReadAllText($solutionPath)
 Modal $dialog {
  param($view)
  [IO.File]::AppendAllText($solutionPath, ' ')
  Click ($view.FindName('SaveButton'))
  Check ($view.IsVisible -and $view.FindName('MappingSaveStatus').Text -like '*changed*') 'Stale edit remains open with a visible conflict'
  Check ([IO.File]::ReadAllText($solutionPath) -eq ($before + ' ')) 'Stale UI save preserves concurrent bytes'
  Capture $view 'after-mapping-conflict'
  Click ($view.FindName('CancelButton'))
 } | Out-Null
 [IO.File]::WriteAllText($solutionPath, '{')
 $dialog = Mapper
 Modal $dialog {
  param($view)
  Check (-not $view.FindName('SaveButton').IsEnabled) 'Corrupt scope disables Save'
  Check ($view.FindName('MappingSaveStatus').Text -like '*could not be opened*') 'Corrupt scope explains recovery'
  Capture $view 'after-mapping-disabled'
  $view.FindName('MappingScopeBox').SelectedValue = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingScope]::User
  Pump
  Check ($view.FindName('SaveButton').IsEnabled) 'Selecting a healthy scope restores Save'
  Click ($view.FindName('SaveButton'))
 } | Out-Null
 Check ([IO.File]::ReadAllText($solutionPath) -eq '{' -and (Test-Path $userPath)) 'User scope save preserves corrupt solution file'
 $store = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new($null, $userPath)
 $dialog = Mapper
 Modal $dialog {
  param($view)
  Check ($view.FindName('MappingScopeBox').Items.Count -eq 1 -and $view.DataContext.SelectedScope.ToString() -eq 'User') 'No-solution editor restores user mapping without a stale solution'
  Click ($view.FindName('CancelButton'))
 } | Out-Null
 $longPath = Join-Path $runRoot ((('workspace-' + ('x' * 55))) + '\.rawbuffervisualizer.json')
 $store = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new($longPath, $userPath)
 $dialog = Mapper ('Company.Vision.' + ('NestedFrame.' * 8) + 'LongBuffer')
 Modal $dialog {
  param($view)
  $view.SizeToContent = 'Manual'; $view.Width = 400; $view.Height = 540; Pump
  Check ($view.FindName('MappingSavePath').Text -eq $longPath) 'Long mapping path stays available in full'
  $button = $view.FindName('SaveButton')
  $point = $button.TranslatePoint([System.Windows.Point]::new(0,0), $view)
  Check ($point.Y -ge 0 -and $point.Y + $button.ActualHeight -lt $view.ActualHeight) 'Compact layout keeps Save reachable'
  Capture $view 'after-mapping-long-compact'
  $pathScroller = [System.Windows.Media.VisualTreeHelper]::GetParent($view.FindName('MappingSavePath'))
  while ($pathScroller -and $pathScroller -isnot [System.Windows.Controls.ScrollViewer]) { $pathScroller = [System.Windows.Media.VisualTreeHelper]::GetParent($pathScroller) }
  $pathScroller.ScrollToEnd(); Pump
  Check ($pathScroller.VerticalOffset -gt 0) 'Long path scroll reaches the destination filename'
  Capture $view 'after-mapping-path-scroll'
  $view.WindowState = 'Maximized'; Pump; Capture $view 'after-mapping-maximized'
  $view.WindowState = 'Normal'; Pump
  Check ($view.FindName('MappingScopeBox').SelectedValue.ToString() -eq 'Solution') 'Maximize and restore retain selected scope'
  Click ($view.FindName('CancelButton'))
 } | Out-Null
 Check (-not (Test-Path $longPath)) 'Resize and cancel do not create mappings'

 }
 if ($Area -ne 'mapping') {
 $hostAssemblies = @($assembly, [Reflection.Assembly]::LoadFrom("$BuildRoot\bin\RawBufferVisualizer.Wpf\Release\net472\RawBufferVisualizer.Wpf.exe"))
 foreach ($hostAssembly in $hostAssemblies) {
 $hostName = $hostAssembly.GetName().Name
 $modelType = $hostAssembly.GetType('RawBufferVisualizer.Presentation.SnapshotExportViewModel', $true)
 $dialogType = $hostAssembly.GetType('RawBufferVisualizer.Presentation.SnapshotExportDialog', $true)
 $owner = [System.Windows.Window]::new(); $owner.Title = 'Raw Buffer Visualizer export smoke'; $owner.Width = 700; $owner.Height = 400
 Place $owner; $owner.Show(); Pump
 foreach ($scenario in $ExportScenarios) {
  Write-Output "Starting: $hostName / $scenario"
  $source = [ExportSmokeSource]::new(32MB)
  if ($scenario -eq 'failure') { $source.FailAt = 2MB }
  $operation = [RawBufferVisualizer.Sdk.SnapshotExportOperation]::new()
  $control = $null
  if ($scenario -eq 'live_continue' -or $scenario -eq 'viewer_close') {
   if ($hostName -ne 'RawBufferVisualizer.VisualStudio.Vssdk') { $operation.Dispose(); continue }
   $operation.Dispose()
   $control = [RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl]::new()
   $operation = $control.GetType().GetField('_snapshotExport', [Reflection.BindingFlags]'NonPublic,Instance').GetValue($control)
   $source.Live = $true
  }
  $path = "$runRoot\$hostName-$scenario.rbuf.json"
  [System.Threading.SynchronizationContext]::SetSynchronizationContext([System.Windows.Threading.DispatcherSynchronizationContext]::new())
  $model = [Activator]::CreateInstance($modelType, [object[]]@($operation, $path, $source, $source.Descriptor))
  $exportState = @{ Error = $null; Driven = $false }
  $timer = [System.Windows.Threading.DispatcherTimer]::new(); $timer.Interval = [TimeSpan]::FromMilliseconds(100)
  $timer.Add_Tick({
   if ($exportState.Driven) { return }
   try {
    $exportView = [System.Windows.Interop.HwndSource]::CurrentSources | ForEach-Object { $_.RootVisual } | Where-Object { $_ -and $_.GetType() -eq $dialogType } | Select-Object -First 1
    if ($null -eq $exportView) { return }
    if ($scenario -eq 'failure' -and -not $model.IsFinished) { return }
    if ($scenario -ne 'failure' -and $model.Percent -le 0) { return }
    $exportState.Driven = $true; $timer.Stop()
    Check ($exportView.DataContext -eq $model -and $exportView.FindName('ExportPath').Text -eq $path) "Export bindings: $scenario"
    Capture $exportView "after-$hostName-export-$scenario"
    if ($scenario -eq 'success') {
     Check ($exportView.FindName('ExportProgress').Value -gt 0 -and $source.Reads -lt 32) 'Progress renders before the worker completes'
     $exportView.Width = 350; Pump; Capture $exportView 'after-export-compact'
    } elseif ($scenario -eq 'cancel') {
     $button = $exportView.FindName('ExportCancel'); $button.Focus() | Out-Null; Pointer $button; Capture $exportView 'after-cancel-hover-focus'
     Press $button; Check $button.IsPressed 'Actual pointer-down reaches Cancel'; Capture $exportView 'after-cancel-pressed'; Release
    } elseif ($scenario -eq 'failure') {
     Check ($exportView.FindName('ExportStatus').Text -like '*failed*' -and $exportView.FindName('ExportCancel').Content -eq 'Close') 'Export failure exposes error and Close'
     Click ($exportView.FindName('ExportCancel'))
    } elseif ($scenario -eq 'escape') {
     Click ($exportView.FindName('ExportPath'))
     [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    } elseif ($scenario -eq 'owner_close') { $owner.Close() }
    elseif ($scenario -eq 'live_continue') { $control.InvalidateLiveSources() | Out-Null }
    elseif ($scenario -eq 'viewer_close') { $control.Dispose() }
    else { $operation.Dispose() }
   } catch { $exportState.Error = $_; $timer.Stop(); $operation.Cancel(); if ($exportView -and $model.IsFinished) { $exportView.Close() } }
  }.GetNewClosure())
  $timer.Start()
  try {
   $task = $dialogType.GetMethod('ShowAsync').Invoke($null, [object[]]@($owner,$model))
   Write-Output "Modal returned: $scenario; task=$($task.Status); modelFinished=$($model.IsFinished); workerRunning=$($operation.IsRunning)"
   $completionWatch = [Diagnostics.Stopwatch]::StartNew()
   while (-not $task.IsCompleted) {
    if ($completionWatch.ElapsedMilliseconds -gt 5000) { throw "Export did not settle: $scenario; task=$($task.Status); modelFinished=$($model.IsFinished); workerRunning=$($operation.IsRunning)" }
    Pump
   }
   $result = $task.GetAwaiter().GetResult()
  } finally { $timer.Stop(); if ($control) { $control.Dispose() }; $operation.Dispose(); $source.Dispose() }
  if ($exportState.Error) { throw $exportState.Error }
  Check $exportState.Driven "Export UI exercised: $hostName / $scenario"
  if ($scenario -eq 'success') {
   Check ($null -ne $result -and [IO.FileInfo]::new($result.RawPath).Length -eq 32MB) 'Successful UI export commits complete payload'
  } else { Check (-not (Test-Path $path) -and @(Get-ChildItem $runRoot -Filter "$hostName-$scenario.*.raw").Count -eq 0) "Cancelled/failed UI export cleans unpublished output: $hostName / $scenario" }
  Pump
  if (-not $owner.IsVisible) { $owner = [System.Windows.Window]::new(); $owner.Width = 700; $owner.Height = 400; Place $owner; $owner.Show(); Pump }
 }
 $owner.Close()
 }
 }
 @{ Status = 'Complete'; Monitor = $testScreen.DeviceName; Bounds = $testScreen.Bounds.ToString(); Checks = $checks; Windows = $windows; Data = $runRoot; Boundary = 'Direct WPF harness at actual 100% DPI; installed IDE, OS scaling changes and registered debugger launch not exercised.' } | ConvertTo-Json -Depth 6 | Set-Content "$OutputDir\ui-verification.json"
 Write-Output "Mapping/export UI smoke passed: $($checks.Count) assertions."
} finally {
 foreach ($source in @([System.Windows.Interop.HwndSource]::CurrentSources)) { if ($source.RootVisual -is [System.Windows.Window]) { $source.RootVisual.Close() } }
}

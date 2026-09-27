param(
    [string]$BuildRoot = '',
    [string]$OutputDir = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path $repoRoot 'artifacts' }
if (-not (Test-Path -LiteralPath 'D:\')) { Write-Warning "D: is unavailable; using $testRoot for test outputs." }
if ([string]::IsNullOrWhiteSpace($BuildRoot)) { $BuildRoot = Join-Path $testRoot 'build' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $testRoot 'ui\usability' }
New-Item -ItemType Directory -Force -Path $OutputDir, "$OutputDir\temp" | Out-Null
$env:TEMP = "$OutputDir\temp"; $env:TMP = $env:TEMP
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing
$directory = "$BuildRoot\bin\RawBufferVisualizer.Wpf\Release\net472"
foreach ($name in @('RawBufferVisualizer.Core','RawBufferVisualizer.Sdk','RawBufferVisualizer.OpenGlCanvas')) { [Reflection.Assembly]::LoadFrom("$directory\$name.dll") | Out-Null }
$assembly = [Reflection.Assembly]::LoadFrom("$directory\RawBufferVisualizer.Wpf.exe")
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new())
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class UsabilityNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern int GetClassName(IntPtr handle, StringBuilder name, int max);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
$screens = @([Windows.Forms.Screen]::AllScreens)
$screen = if ($screens.Count -eq 2) { $screens | Sort-Object { $_.WorkingArea.Width * $_.WorkingArea.Height }, { $_.Bounds.Left } | Select-Object -First 1 } else { $screens | Select-Object -First 1 }
if ($null -eq $screen) { throw 'Desktop monitor required.' }
$checks = [Collections.Generic.List[string]]::new(); $captures = [Collections.Generic.List[object]]::new()
function Check($condition, [string]$message) { if (-not $condition) { throw $message }; $checks.Add($message); Write-Host "PASS $message" }
function Pump([int]$milliseconds = 40) {
 $frame = [Windows.Threading.DispatcherFrame]::new(); $timer = [Windows.Threading.DispatcherTimer]::new()
 $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
 $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
 $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Until([scriptblock]$condition) { $deadline = [DateTime]::UtcNow.AddSeconds(15); while (-not (& $condition)) { if ([DateTime]::UtcNow -gt $deadline) { throw 'UI operation timeout.' }; Pump }; Pump }
function Done { Until { $model.Histogram.Completion.IsCompleted }; Check (-not $model.Histogram.Completion.IsFaulted) 'Histogram settled' }
function Tree($root) { $root; for ($i = 0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($root); $i++) { Tree ([Windows.Media.VisualTreeHelper]::GetChild($root, $i)) } }
function Button([string]$id) { Tree $window | Where-Object { $_ -is [Windows.Controls.Button] -and [Windows.Automation.AutomationProperties]::GetAutomationId($_) -eq $id } | Select-Object -First 1 }
function CaptureHandle([IntPtr]$handle, [string]$name) {
 Pump 100; $rect = [UsabilityNative+Rect]::new(); [UsabilityNative]::GetWindowRect($handle, [ref]$rect) | Out-Null
 Check ($rect.Right -gt $screen.Bounds.Left -and $rect.Left -lt $screen.Bounds.Right -and $rect.Bottom -gt $screen.Bounds.Top -and $rect.Top -lt $screen.Bounds.Bottom) "Monitor placement: $name"
 $bitmap = [Drawing.Bitmap]::new($rect.Right - $rect.Left, $rect.Bottom - $rect.Top); $graphics = [Drawing.Graphics]::FromImage($bitmap)
 try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size); $bitmap.Save("$OutputDir\$name.png") } finally { $graphics.Dispose(); $bitmap.Dispose() }
 $captures.Add(@{Name=$name;Rectangle=$rect})
}
function Capture($view, [string]$name) { CaptureHandle ([Windows.Interop.WindowInteropHelper]::new($view).Handle) $name }
function Pointer($control) { $point = $control.PointToScreen([Windows.Point]::new($control.ActualWidth / 2, $control.ActualHeight / 2)); [UsabilityNative]::SetCursorPos([int]$point.X, [int]$point.Y) | Out-Null; Pump }
function Press($control) { Pointer $control; [UsabilityNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Pump }
function Release { [UsabilityNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Pump }
function Click($control) { Press $control; Release }
function Key([byte]$key) { [UsabilityNative]::keybd_event($key,0,0,[UIntPtr]::Zero); Pump; [UsabilityNative]::keybd_event($key,0,2,[UIntPtr]::Zero); Pump }
function DriveRaw([scriptblock]$action) {
 $state = @{Error=$null;Driven=$false}; $timer = [Windows.Threading.DispatcherTimer]::new(); $deadline = [DateTime]::UtcNow.AddSeconds(10)
 $timer.Interval = [TimeSpan]::FromMilliseconds(80)
 $timer.Add_Tick({
  $dialog = [Windows.Interop.HwndSource]::CurrentSources | ForEach-Object { $_.RootVisual } | Where-Object { $_ -and $_.GetType().Name -eq 'RawImportDialog' } | Select-Object -First 1
  if ($null -eq $dialog) { if ([DateTime]::UtcNow -gt $deadline) { $state.Error='RAW dialog did not appear'; $timer.Stop() }; return }
  $timer.Stop(); $state.Driven=$true
  try { & $action $dialog } catch { $state.Error=$_; $dialog.Close() }
 }.GetNewClosure())
 $timer.Start(); try { $window.OpenPath($rawPath) } finally { $timer.Stop() }
 if ($state.Error) { throw $state.Error }; Check $state.Driven 'RAW setup was shown before opening'
}
function SaveThroughDialog([string]$buttonId, [string]$path, [bool]$cancel = $false) {
 $state=@{Error=$null;Driven=$false}; $timer=[Windows.Threading.DispatcherTimer]::new(); $deadline=[DateTime]::UtcNow.AddSeconds(10)
 $timer.Interval=[TimeSpan]::FromMilliseconds(100)
 $timer.Add_Tick({
  $handle=[UsabilityNative]::GetForegroundWindow(); $ownerPid=[uint32]0; [UsabilityNative]::GetWindowThreadProcessId($handle,[ref]$ownerPid)|Out-Null
  $name=[Text.StringBuilder]::new(128); [UsabilityNative]::GetClassName($handle,$name,128)|Out-Null
  if ($ownerPid -ne $PID -or $name.ToString() -ne '#32770') { if([DateTime]::UtcNow -gt $deadline) {$state.Error='Owned save dialog timeout';$timer.Stop()}; return }
  $timer.Stop(); $state.Driven=$true
  try {
   CaptureHandle $handle ('save-picker-'+$buttonId+$(if($cancel){'-cancel'}else{''}))
   if ($cancel) { [Windows.Forms.SendKeys]::SendWait('{ESC}') }
   else { [Windows.Forms.SendKeys]::SendWait('%n'); [Windows.Forms.SendKeys]::SendWait('^a'); [Windows.Forms.SendKeys]::SendWait($path); [Windows.Forms.SendKeys]::SendWait('{ENTER}') }
  } catch { $state.Error=$_; [Windows.Forms.SendKeys]::SendWait('{ESC}') }
 }.GetNewClosure())
 $timer.Start(); try { Click (Button $buttonId); Until { $model.ExportCompletion.IsCompleted } } finally { $timer.Stop() }
 if($state.Error){throw $state.Error}; Check $state.Driven "Owned file-picker exercised: $buttonId"
}
$app=[Windows.Application]::new(); $app.ShutdownMode='OnExplicitShutdown'
[xml]$appXaml=Get-Content "$repoRoot\src\RawBufferVisualizer.Wpf\App.xaml" -Raw
$app.Resources=[Windows.Markup.XamlReader]::Parse('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+$appXaml.Application.'Application.Resources'.InnerXml+'</ResourceDictionary>')
$window=[RawBufferVisualizer.Wpf.MainWindow]::new(); $window.Left=$screen.WorkingArea.Left+20; $window.Top=$screen.WorkingArea.Top+15; $window.Topmost=$true
$model=$window.DataContext
try {
 $window.Show(); Pump
 Check ($window.FindName('EmptyState').IsVisible -and $window.FindName('ZoomText').Text -eq [string][char]0x2014 -and $window.FindName('WidthBox').Text -eq [string][char]0x2014) 'Empty instructions and placeholders render'
 foreach ($id in @('SavePngButton','SaveSnapshotButton','FitButton','ActualSizeButton','CloseDocumentButton')) { Check (-not (Button $id).IsEnabled) "Empty command disabled: $id" }
 Capture $window 'after-empty'
 $exampleButton=Button 'OpenExampleButton'; $exampleButton.Focus()|Out-Null; Pointer $exampleButton; Capture $window 'example-hover-focus'
 Press $exampleButton; Check $exampleButton.IsPressed 'Example receives actual pointer-down'; Capture $window 'example-pressed'; Release; Done
 Check ($model.SelectedDocument.IsExample -and $window.FindName('OpenGlImageView').IsVisible -and (Button 'SavePngButton').IsEnabled) 'Example click opens a rendered exportable image'
 Pointer $window.FindName('StatusText'); Check (-not $exampleButton.IsMouseOver -and -not $exampleButton.IsPressed) 'Pointer leave restores button state'
 Capture $window 'after-example'
 $sampleRoot="$OutputDir\samples"; New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
 $mono=[RawBufferVisualizer.Core.RawImageDescriptor]::new(); $mono.Width=640; $mono.Height=480; $mono.Stride=640; $mono.PixelFormat='Mono8'; $mono.ValidBits=8
 $monoBytes=[byte[]]::new(640*480); for($i=0;$i -lt $monoBytes.Length;$i++){$monoBytes[$i]=[byte]($i%256)}
 [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save("$sampleRoot\mono8-gradient.rbuf.json",$monoBytes,$mono)|Out-Null
 $rgb=[RawBufferVisualizer.Core.RawImageDescriptor]::new(); $rgb.Width=320; $rgb.Height=240; $rgb.Stride=960; $rgb.PixelFormat='RGB24'; $rgb.ValidBits=8
 [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save("$sampleRoot\rgb24-color.rbuf.json",[byte[]]::new(320*240*3),$rgb)|Out-Null
 $window.OpenPath("$sampleRoot\rgb24-color.rbuf.json"); Done; $previous=$model.SelectedDocument; $count=$model.Documents.Count
 $rawPath="$OutputDir\mono8.raw"; $reference=[RawBufferVisualizer.Sdk.RawBufferSnapshot]::LoadReference("$sampleRoot\mono8-gradient.rbuf.json"); Copy-Item -LiteralPath $reference.RawPath -Destination $rawPath
 DriveRaw {
  param($dialog)
  Check ($dialog.FindName('RawWidth').Text -eq '' -and $dialog.FindName('RawHeight').Text -eq '' -and -not $dialog.FindName('ConfirmRawButton').IsEnabled) 'RAW dialog does not inherit RGB dimensions'
  $dialog.FindName('RawWidth').Text='640'; $dialog.FindName('RawHeight').Text='480'; $dialog.FindName('RawStride').Text='639'; Pump
  Check ($dialog.DataContext.Stride -eq '639' -and $dialog.DataContext.HasError -and -not $dialog.FindName('ConfirmRawButton').IsEnabled) 'Rendered invalid stride updates validation and disables Open'
  Capture $dialog 'raw-invalid-stride'
  $dialog.Width=$dialog.MinWidth; $dialog.Height=400; Pump; Capture $dialog 'raw-compact-top'
  $dialog.FindName('RawValidation').BringIntoView(); Pump; Capture $dialog 'raw-compact-bottom'
  Check ($dialog.FindName('ConfirmRawButton').IsVisible -and $dialog.FindName('RawValidation').ActualWidth -lt $dialog.ActualWidth) 'Compact RAW setup keeps actions and wrapped validation reachable'
  $dialog.Width=560; $dialog.Height=640; $dialog.FindName('RawWidth').BringIntoView(); Pump
  $combo=$dialog.FindName('RawFormat'); $combo.IsDropDownOpen=$true; Pump; Capture $dialog 'raw-format-popup'; $combo.IsDropDownOpen=$false
  $dialog.Activate()|Out-Null; $dialog.FindName('RawWidth').Focus()|Out-Null; Pump; Key 27
  if ($dialog.IsVisible) { $dialog.Close(); throw 'Escape did not close the RAW dialog' }
 }
 Check ($model.Documents.Count -eq $count -and $model.SelectedDocument -eq $previous -and $window.FindName('ActionStatus').Text -like '*cancelled*') 'RAW Escape preserves document/selection with visible cancellation'
 DriveRaw {
  param($dialog)
  $dialog.FindName('RawWidth').Text='640'; $dialog.FindName('RawHeight').Text='480'; $dialog.FindName('RawStride').Text='640'; Pump
  Check ($dialog.DataContext.Descriptor.PixelFormat -eq 'Mono8' -and $dialog.FindName('ConfirmRawButton').IsEnabled -and $dialog.FindName('RawByteSummary').Text -like '*307,200*') 'Valid settings and exact byte counts render'
  Capture $dialog 'raw-valid'; Press $dialog.FindName('ConfirmRawButton'); Check $dialog.FindName('ConfirmRawButton').IsPressed 'RAW Open receives pointer-down'; Capture $dialog 'raw-confirm-pressed'; Release
 }
 Done; $raw=$model.SelectedDocument; Capture $window 'after-raw'
 Check ($window.FindName('WidthBox').Text -eq '640' -and $window.FindName('FormatBox').Text -eq 'Mono8') 'Confirmed descriptor reaches rendered fields'
 $window.OpenPath("$sampleRoot\mono8-gradient.rbuf.json"); Done
 $sha=[Security.Cryptography.SHA256]::Create(); try { $a=[BitConverter]::ToString($sha.ComputeHash($raw.Rendered.Bgra32)); $b=[BitConverter]::ToString($sha.ComputeHash($model.SelectedDocument.Rendered.Bgra32)) } finally {$sha.Dispose()}
 Check ($a -eq $b) 'Actual RAW and metadata opening render identical pixels'
 $count=$model.Documents.Count; $window.OpenPath($rawPath); Pump
 Check ($model.Documents.Count -eq $count -and $window.FindName('DocumentTabs').SelectedItem -eq $raw -and $window.FindName('ImageList').SelectedItem -eq $raw) 'Reopen selects existing document in both controls'
 # Include the generated RAW suffix within this net472 host's existing Windows path limit.
 $longName='long-buffer-name-for-first-use-verification-'+('image-'*10)+'.rbuf.json'; $longPath=Join-Path $OutputDir $longName
 [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save($longPath,[IO.File]::ReadAllBytes($reference.RawPath),$reference.Descriptor)|Out-Null
 $window.OpenPath($longPath); Done
 $window.Width=$window.MinWidth; $window.Height=$window.MinHeight; Pump
 $tabs=$window.FindName('DocumentTabs'); $container=$tabs.ItemContainerGenerator.ContainerFromItem($tabs.SelectedItem); $point=$container.TranslatePoint([Windows.Point]::new(0,0),$tabs)
 Check ($point.X -ge -1 -and $point.X+$container.ActualWidth -le $tabs.ActualWidth+1) 'Active long-name tab and close action fit the minimum-width viewport'
 Check ($tabs.Items.Count -eq $model.Documents.Count) 'All documents remain in the scrollable strip'
 Capture $window 'after-minimum-tabs'
 $window.FindName('InspectorScroll').ScrollToBottom(); Pump; Capture $window 'after-minimum-diagnostics'
 $list=$window.FindName('DiagnosticsList'); $texts=@(Tree $list | Where-Object {$_ -is [Windows.Controls.TextBlock] -and $_.Text -like '*bytes*'})
 Check ($texts.Count -ge 1 -and @($texts | Where-Object {$_.TextWrapping -ne 'Wrap' -or $_.ActualWidth -gt $list.ActualWidth}).Count -eq 0) 'Diagnostics containing values and byte units wrap within the inspector'
 $window.FindName('ImageList').SelectedItem=$previous; Pump
 Check ($model.SelectedDocument -eq $previous -and $tabs.SelectedItem -eq $previous) 'Images selection updates model and document strip'
 $tabs.SelectedItem=$raw; Pump; Check ($model.SelectedDocument -eq $raw -and $window.FindName('ImageList').SelectedItem -eq $raw) 'Strip selection updates model and Images'
 Click (Button 'ActualSizeButton'); Pump; Check ([Math]::Abs($window.FindName('OpenGlImageView').ZoomScale-1) -lt 0.001) '1:1 click reaches canvas'
 $window.WindowState='Maximized'; Pump; Capture $window 'after-maximized'; $window.WindowState='Normal'; Pump
 Check ($model.SelectedDocument -eq $raw -and [Math]::Abs($window.FindName('OpenGlImageView').ZoomScale-1) -lt 0.001) 'Maximize and restore preserve selection and manual zoom'
 $window.FindName('ZoomSlider').Value=2; Pump; Check ([Math]::Abs($window.FindName('OpenGlImageView').ZoomScale-2) -lt 0.001) 'Zoom slider writes through to canvas'
 $window.FindName('OpenGlImageView').SetZoomScale(1); Pump; Check ($window.FindName('ZoomSlider').Value -eq 1) 'Canvas zoom writes back to slider'
 $window.FindName('LinkViewsBox').IsChecked=$true; Pump; Check $model.LinkViews 'Link views selection reaches model'; $model.LinkViews=$false; Pump; Check (-not $window.FindName('LinkViewsBox').IsChecked) 'Link views model reaches rendered toggle'
 Click (Button 'DuplicateButton'); Done; $count=$model.Documents.Count
 Click $window.FindName('OpenGlImageView'); [UsabilityNative]::keybd_event(17,0,0,[UIntPtr]::Zero); Key 87; [UsabilityNative]::keybd_event(17,0,2,[UIntPtr]::Zero); Pump
 Check ($model.Documents.Count -eq $count-1) 'Ctrl+W closes from the image canvas'
 $window.FindName('ImageList').Focus()|Out-Null; $count=$model.Documents.Count; Key 46
 Check ($model.Documents.Count -eq $count-1) 'Images Delete remains available'
 $container=$tabs.ItemContainerGenerator.ContainerFromItem($tabs.SelectedItem); $close=Tree $container | Where-Object {$_ -is [Windows.Controls.Button]} | Select-Object -First 1
 $count=$model.Documents.Count; Click $close; Pump; Check ($model.Documents.Count -eq $count-1) 'Visible tab close button removes its document'
 $window.Width=1280; $window.Height=800; Click (Button 'OpenExampleButton'); Done
 SaveThroughDialog 'SavePngButton' "$OutputDir\ui-output.png" $true
 Check ($window.FindName('ActionStatus').Text -like '*PNG export cancelled*') 'PNG cancellation is visible in the viewer'
 SaveThroughDialog 'SavePngButton' "$OutputDir\ui-output.png"
 Check ((Test-Path "$OutputDir\ui-output.png") -and $window.FindName('ActionStatus').Text -like '*PNG saved*') 'PNG save result displays actual output path'; Capture $window 'after-png-save'
 SaveThroughDialog 'SaveSnapshotButton' "$OutputDir\ui-output.rbuf.json"
 Check ((Test-Path "$OutputDir\ui-output.rbuf.json") -and $window.FindName('ActionStatus').Text -like '*Metadata:*RAW data:*') 'Snapshot result explains and names both saved files'; Capture $window 'after-snapshot-save'
 $window.OpenPath("$OutputDir\missing.rbuf.json"); Pump
 Check ($window.FindName('ActionStatus').Text -like '*Open failed*both*') 'Missing snapshot recovery is rendered'; Capture $window 'after-missing-file'
 while($model.Documents.Count -gt 0){Click (Button 'CloseDocumentButton')}; Pump
 Check ($window.FindName('EmptyState').IsVisible -and -not (Button 'SavePngButton').IsEnabled -and $window.FindName('ZoomText').Text -eq [string][char]0x2014) 'Last close restores all empty UI states'
 Capture $window 'after-last-close'
 @{Status='Complete';Checks=$checks;Captures=$captures;Screens=$screens;SelectedMonitor=$screen.DeviceName;Bounds=$screen.Bounds;Dpi=[Windows.Media.VisualTreeHelper]::GetDpi($window).PixelsPerInchX;Boundary='Compiled standalone viewer in an STA host, existing dark theme at actual DPI; installed IDE and OS DPI changes are not exercised.'}|ConvertTo-Json -Depth 6|Set-Content "$OutputDir\ui-verification.json"
} finally { foreach($source in @([Windows.Interop.HwndSource]::CurrentSources)){if($source.RootVisual -is [Windows.Window]){$source.RootVisual.Close()}}; $app.Shutdown() }

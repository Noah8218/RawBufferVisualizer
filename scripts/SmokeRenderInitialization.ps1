param(
    [string]$BuildRoot = '',
    [string]$OutputDir = '',
    [switch]$Baseline
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = if (Test-Path -LiteralPath 'D:\') { 'D:\OpenVisionLab-TestData\RawBufferVisualizer' } else { Join-Path $repoRoot 'artifacts' }
if (-not (Test-Path -LiteralPath 'D:\')) { Write-Warning "D: is unavailable; using $testRoot for test outputs." }
if ([string]::IsNullOrWhiteSpace($BuildRoot)) { $BuildRoot = Join-Path $testRoot 'build' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $testRoot 'ui\render-initialization' }
New-Item -ItemType Directory -Force -Path $OutputDir, "$OutputDir\temp" | Out-Null
$env:TEMP = "$OutputDir\temp"; $env:TMP = $env:TEMP
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing
$directory = "$BuildRoot\bin\RawBufferVisualizer.Wpf\Release\net472"
foreach ($name in @('SharpGL','SharpGL.WinForms','RawBufferVisualizer.Core','RawBufferVisualizer.OpenGlCanvas')) { [Reflection.Assembly]::LoadFrom("$directory\$name.dll") | Out-Null }
$checks = [Collections.Generic.List[string]]::new()
function Check($condition, [string]$message) { if (-not $condition) { throw $message }; $checks.Add($message); Write-Host "PASS $message" }
function Pump {
    $frame = [Windows.Threading.DispatcherFrame]::new(); $timer = [Windows.Threading.DispatcherTimer]::new()
    $timer.Interval = [TimeSpan]::FromMilliseconds(30)
    $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
    $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Ready {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not $canvas.IsCurrentViewReady) { if ([DateTime]::UtcNow -gt $deadline) { throw 'First frame timed out' }; Pump }
    Pump
    Check ($canvas.LastTextureError -eq 0) 'Frame has no texture error'
}
$screens = @([Windows.Forms.Screen]::AllScreens)
$screen = if ($screens.Count -eq 2) { $screens | Sort-Object { $_.WorkingArea.Width * $_.WorkingArea.Height }, { $_.Bounds.Left } | Select-Object -First 1 } else { $screens | Select-Object -First 1 }
if ($null -eq $screen) { throw 'Desktop monitor required for the view capture.' }
$record = [ordered]@{Baseline=[bool]$Baseline; CanvasHash=(Get-FileHash "$directory\RawBufferVisualizer.OpenGlCanvas.dll").Hash; Monitor=$screen.DeviceName; Bounds=$screen.Bounds; Checks=$checks; EarlyException=$null}
$app = [Windows.Application]::new(); $app.ShutdownMode='OnExplicitShutdown'
$canvas = [RawBufferVisualizer.OpenGlCanvas.RawOpenGlImageCanvas]::new()
$window = [Windows.Window]::new(); $window.Title='Rendering initialization regression'; $window.Content=$canvas
$window.Width=900; $window.Height=620; $window.Left=$screen.WorkingArea.Left+20; $window.Top=$screen.WorkingArea.Top+20; $window.Topmost=$true
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$control=$canvas.GetType().GetField('_openGlControl',$flags).GetValue($canvas)
$descriptor=[RawBufferVisualizer.Core.RawImageDescriptor]::new()
$descriptor.Width=6768; $descriptor.Height=3225; $descriptor.Stride=6768; $descriptor.PixelFormat='Mono8'; $descriptor.ValidBits=8
# A constant image gives an independent framebuffer oracle, including after resizes and reopening.
$bytes=[byte[]]::new($descriptor.Width*$descriptor.Height)
$bytes[0]=183
for ($count=1; $count -lt $bytes.Length; $count*=2) { [Array]::Copy($bytes,0,$bytes,$count,[Math]::Min($count,$bytes.Length-$count)) }
try {
    $canvas.LoadRawBuffer($bytes,$descriptor)
    $canvas.Measure([Windows.Size]::new(690,560)); $canvas.Arrange([Windows.Rect]::new(0,0,690,560))
    $handle=$control.Handle; $canvas.FitToImage()
    Check ($null -eq $control.OpenGL.RenderContextProvider) 'Valid image can be loaded before the native context exists'
    try { $control.DoRender() } catch { $record.EarlyException=$_.Exception.ToString() }
    if ($Baseline) { Check ($record.EarlyException -match 'error 1282') 'Baseline reproduces the premature upload exception' }
    else {
        Check ($null -eq $record.EarlyException) 'Early native paint is deferred without an exception'
        Check ($canvas.GetRenderStatsSnapshot().TextureUploadCount -eq 0 -and -not $canvas.IsCurrentViewReady) 'Early paint does not upload or claim a ready image'
        $canvas.GetType().GetMethod('RenderQueuedFrame',$flags).Invoke($canvas,@()) | Out-Null
        Check ($canvas.GetRenderStatsSnapshot().FrameCount -eq 0) 'Queued paint also waits for initialization'
    }
    $window.Show(); Ready
    $record.GlVendor=$control.OpenGL.GetString([SharpGL.OpenGL]::GL_VENDOR)
    $record.GlRenderer=$control.OpenGL.GetString([SharpGL.OpenGL]::GL_RENDERER)
    $record.GlVersion=$control.OpenGL.GetString([SharpGL.OpenGL]::GL_VERSION)
    $point=$window.PointToScreen([Windows.Point]::new(0,0))
    Check ($point.X -ge $screen.Bounds.Left -and $point.X -lt $screen.Bounds.Right -and $point.Y -ge $screen.Bounds.Top -and $point.Y -lt $screen.Bounds.Bottom) 'Rendered view is on the selected monitor'
    foreach ($mode in @('fit','actual-size','resize','reopen')) {
        switch ($mode) {
            'fit' { $canvas.FitToImage() }
            'actual-size' { $canvas.SetZoomScale(1) }
            'resize' { $window.Width=730; $window.Height=510; $canvas.FitToImage() }
            'reopen' { $canvas.ClearImage(); $canvas.LoadRawBuffer($bytes,$descriptor) }
        }
        Pump; Ready
        $path="$OutputDir\$mode.png"; $canvas.SaveFramebufferPng($path)
        $bitmap=[Drawing.Bitmap]::FromFile($path)
        try {
            $pixel=$bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/2))
            Check ($pixel.R -eq 183 -and $pixel.G -eq 183 -and $pixel.B -eq 183) "Framebuffer matches source value 183: $mode"
        } finally { $bitmap.Dispose() }
    }
    Check ($canvas.GetRenderStatsSnapshot().TextureUploadCount -gt 0) 'Loaded callback requests and completes deferred rendering'
    $record.Status='Pass'
} catch { $record.Status='Fail'; $record.Error=$_.Exception.ToString(); throw }
finally {
    $canvas.ClearImage(); $window.Close(); $app.Shutdown()
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$OutputDir\result.json" -Encoding UTF8
}

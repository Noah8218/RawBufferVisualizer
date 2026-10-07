[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BuildRoot,
    [Parameter(Mandatory = $true)][string]$VisualStudioRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [int[]]$Widths = @(320, 640, 759, 760, 800, 1000, 1440),
    [int[]]$Heights = @(440, 720)
)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run in Windows PowerShell with -STA.' }
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $output, "$output\temp" | Out-Null
$env:TEMP = "$output\temp"; $env:TMP = $env:TEMP
$bin = Join-Path $BuildRoot 'bin\RawBufferVisualizer.VisualStudio.Vssdk\Release\net472'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Join-Path $VisualStudioRoot 'Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll')) | Out-Null
foreach ($name in @('Microsoft.VisualStudio.Imaging.dll', 'Microsoft.VisualStudio.ImageCatalog.dll', 'RawBufferVisualizer.VisualStudio.Vssdk.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) | Out-Null
}
function Arrange-Control($element, [double]$width, [double]$height) {
    $element.Measure([Windows.Size]::new($width, $height))
    $element.Arrange([Windows.Rect]::new(0, 0, $width, $height))
    [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::DataBind)
    $element.UpdateLayout()
}
function Save-Render($element, [int]$width, [int]$height, [string]$name) {
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width, $height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($element)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $output $name))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
function Assert-InBounds($element, $parent, [string]$case) {
    $point = $element.TranslatePoint([Windows.Point]::new(0, 0), $parent)
    if ($element.Visibility -ne 'Visible' -or $element.ActualWidth -le 0 -or $point.X -lt -0.5 -or $point.Y -lt -0.5 -or
        $point.X + $element.ActualWidth -gt $parent.ActualWidth + 0.5 -or $point.Y + $element.ActualHeight -gt $parent.ActualHeight + 0.5) {
        throw "$case is hidden or clipped: $point / $($element.ActualWidth)x$($element.ActualHeight), parent $($parent.ActualWidth)x$($parent.ActualHeight)"
    }
}
function Click-Button($button) { $button.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent, $button)) }
$control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$cases = @(); $sections = @(); $formats = @()
try {
    if ($control.FontFamily.Source -ne 'Segoe UI' -or $control.FontSize -ne 12) { throw 'View typography differs from the approved mockup' }
    $palette = @{ PanelBrush='#FF252526'; InputBrush='#FF1E1E1E'; ToolbarBrush='#FF2D2D30'; BorderBrush='#FF3F3F46'; TextBrush='#FFF1F1F1'; SecondaryTextBrush='#FFA3A7AE'; FaintTextBrush='#FF6E737B'; LinkBrush='#FF3794FF' }
    foreach ($key in $palette.Keys) { if ($control.Resources[$key].Color.ToString() -ne $palette[$key]) { throw "Mockup palette mismatch: $key" } }
    $descriptor = [RawBufferVisualizer.Core.RawImageDescriptor]::new()
    $descriptor.Width = 17; $descriptor.Height = 5; $descriptor.Stride = 56; $descriptor.PixelFormat = 'Bgr24'; $descriptor.ValidBits = 8
    $bytes = New-Object byte[] 280
    for ($y = 0; $y -lt 5; $y++) { for ($x = 0; $x -lt 17; $x++) {
        $offset = $y * 56 + $x * 3; $bytes[$offset] = 164; $bytes[$offset + 1] = 165; $bytes[$offset + 2] = 155
    } }
    $fixture = Join-Path $output 'padded-bgr.rbuf.json'
    [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save($fixture, $bytes, $descriptor) | Out-Null
    $control.OpenPath($fixture)
    $document = $control.GetType().GetProperty('_activeDocument', $flags).GetValue($control, $null)
    $hover = $control.GetType().GetMethod('OpenGlImageView_PixelHovered', $flags)
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(8, 2))) | Out-Null
    $nav = $control.FindName('InspectorRail').DataContext
    $bar = $control.FindName('PixelBar')
    foreach ($width in $Widths) { foreach ($height in $Heights) {
        $control.Width = $width; $control.Height = $height
        Arrange-Control $control $width $height
        if ([string]$control.FindName('MeasureButton').ToolTip -ne 'Click two image pixels to measure their horizontal and vertical distance (M)') { throw 'Full-image measurement tooltip differs from the approved mockup' }
        $orientation = if ($height -lt 560) { 'Vertical' } else { 'Horizontal' }
        if ([string]$control.FindName('ToolbarCommandsPanel').Orientation -ne $orientation) { throw "Wrong toolbar orientation at $width x $height" }
        $toolbar = $control.FindName('ToolbarBorder')
        foreach ($name in @('SaveMenuButton', 'MoreMenuButton')) { Assert-InBounds $control.FindName($name) $toolbar "Trailing toolbar $name at $width x $height" }
        $zoomBefore = if ($height -lt 560) { 'ZoomInButton' } else { 'ZoomOutButton' }
        if ($control.FindName('ToolbarCommandsPanel').Children[2] -ne $control.FindName($zoomBefore)) { throw 'Zoom commands differ from the mockup orientation order' }
        if ($height -lt 560 -and $control.FindName('TrailingGroupSeparator').Visibility -ne 'Visible') { throw 'Vertical Save group separator is missing' }
        if ($width -eq 1440 -and $control.FindName('ImagesColumn').Width.Value -ne $(if ($height -lt 560) { 260 } else { 280 })) { throw 'Default wide image-panel width differs from the mockup' }
        foreach ($name in @('ImageNameText', 'ImageFormatChip')) { Assert-InBounds $control.FindName($name) $control.FindName('ImageInfoBar') "Required header $name at $width x $height" }
        if ($width -eq 1000 -and $height -eq 720 -and $control.FindName('ImageStateChip').Visibility -ne 'Visible') { throw 'Measured header unnecessarily hid a fitting state chip' }
        foreach ($name in @('FitButton','ZoomOutButton','ZoomInButton','PinPixelButton','SaveMenuButton','MoreMenuButton')) {
            $icon = $control.FindName($name).Content.Children[0]
            if ($icon -isnot [Windows.Controls.Viewbox] -or $icon.Child.Children[0].Data.Bounds.IsEmpty) { throw "Prototype vector icon unavailable: $name" }
        }
        foreach ($name in @('SaveVisiblePngButton', 'OpenButton', 'WhatsNewButton', 'EnvironmentCheckButton', 'ZoomText', 'DescriptorText')) {
            if ($null -eq $control.FindName($name)) { throw "NameScope contract lost: $name" }
        }
        foreach ($name in @('PixelRText', 'PixelGText', 'PixelBText', 'PixelGvText')) { Assert-InBounds $control.FindName($name) $bar "$name at $width x $height" }
        if ($control.FindName('PixelOffsetText').Text -ne 'Offset 136' -or $control.FindName('PixelRawText').Text -ne 'Raw A4 A5 9B' -or $control.FindName('PixelGvText').Text -ne 'GV 161') { throw 'Padded BGR readout disagrees with fixture bytes' }
        if ($control.FindName('ImageInfoBar').DataContext.FormatText -ne 'BGR24') { throw 'Header does not describe the active image' }
        $nav.Show('Pixel')
        Arrange-Control $control $width $height
        $image = "layout-$width-$height.png"
        Save-Render $control $width $height $image
        $cases += [pscustomobject]@{ Width=$width; Height=$height; Toolbar=$orientation; Overlay=$nav.IsNarrow; Evidence=$image }
        foreach ($section in @('Pixel', 'Histogram', 'Format', 'Compare', 'Diagnostics')) {
            $button = $control.FindName("Inspector${section}Button")
            $button.IsChecked = $true
            Arrange-Control $control $width $height
            $panel = $control.FindName('InspectorContainer')
            if ($nav.Section -ne $section -or -not $nav.IsOpen -or $control.FindName("${section}Section").Visibility -ne 'Visible') { throw "Rail binding did not display $section at $width x $height" }
            Assert-InBounds $button $control.FindName('InspectorRail') "Rail $section at $width x $height"
            Assert-InBounds $button $control "Rail viewport $section at $width x $height"
            Assert-InBounds $button.Content.Children[1] $button "Rail caption $section at $width x $height"
            if ($button.ActualHeight -ne 48 -or $button.Template.FindName('RailActiveLine', $button).Visibility -ne 'Visible' -or
                $button.Template.FindName('RailBackground', $button).Background.Color.A -ne 0) { throw "Selected rail chrome differs from the mockup: $section" }
            if ($nav.IsNarrow) {
                if ($control.FindName('InspectorOverlayPopup').Child -ne $panel -or $panel.Width -gt $width - 44) { throw "Overlay ownership/bounds failed at $width x $height" }
                # Arrange the same popup content without opening a native window.
                Arrange-Control $panel $panel.Width $height
            } elseif ($control.FindName('DockedInspectorHost').Content -ne $panel -or [Math]::Abs($panel.ActualWidth - 300) -gt 0.5) { throw 'Docked panel did not reuse the inspector' }
            if ($section -eq 'Pixel' -and $control.FindName('RowProfileGraph').ActualWidth -le 0) { throw 'Row profile is unavailable' }
            if ($section -eq 'Format' -and [string]::IsNullOrEmpty($control.FindName('DescriptorText').Text)) { throw 'Descriptor is unavailable' }
            if ($section -eq 'Compare' -and -not $control.FindName('BlinkButton').IsEnabled) { throw 'Compare is unavailable' }
            if ($width -in @(320,1000) -and $height -eq 720) {
                if ($nav.IsNarrow) {
                    $popup = $control.FindName('InspectorOverlayPopup')
                    $popup.Child = $null
                    $renderRoot = [Windows.Controls.Grid]::new()
                    $renderRoot.Resources = $control.Resources
                    [Windows.Documents.TextElement]::SetForeground($renderRoot, $control.Foreground)
                    [Windows.Documents.TextElement]::SetFontFamily($renderRoot, $control.FontFamily)
                    [Windows.Documents.TextElement]::SetFontSize($renderRoot, $control.FontSize)
                    [Windows.NameScope]::SetNameScope($renderRoot, [Windows.NameScope]::GetNameScope($control))
                    try {
                        $renderRoot.Children.Add($panel) | Out-Null
                        Arrange-Control $renderRoot $panel.Width $height
                        if ($panel.Background -ne $control.Resources['PanelBrush']) { throw 'Detached popup capture lost the view resource scope' }
                        Save-Render $renderRoot ([int]$panel.ActualWidth) $height "inspector-$section-$width.png"
                    } finally { $renderRoot.Children.Clear(); $popup.Child = $panel }
                } else { Save-Render $panel ([int]$panel.ActualWidth) $height "inspector-$section-$width.png" }
            }
            $sections += [pscustomobject]@{ Width=$width; Height=$height; Section=$section; PanelWidth=$panel.ActualWidth; Overlay=$nav.IsNarrow }
        }
        $nav.Show('Pixel')
    } }
    $headerCases = @()
    foreach ($width in @(320, 640, 760)) { foreach ($startHeight in @(260, 560)) {
        $control.Width = $width; $control.Height = $startHeight
        Arrange-Control $control $width $startHeight
        if ($nav.IsImageStrip) { $nav.ToggleImageListCommand.Execute($null) }
        Arrange-Control $control $width $startHeight
        Click-Button $control.FindName('ClearButton')
        Arrange-Control $control $width $startHeight
        $control.Height = 260
        Arrange-Control $control $width 260
        $headerScroller = $control.FindName('ImagePanelTopScroller')
        foreach ($name in @('ClearAllConfirmationText', 'ConfirmClearAllButton', 'CancelClearAllButton', 'AutoInspectBox', 'ScanNowButton', 'AutomaticHelpButton', 'StatusText')) {
            $element = $control.FindName($name)
            $element.BringIntoView()
            Arrange-Control $control $width 260
            Assert-InBounds $element $headerScroller "Header scroll viewport $name at $width x 260"
            Assert-InBounds $element $control "Control viewport $name at $width x 260"
        }
        $headerScroller.ScrollToTop()
        Arrange-Control $control $width 260
        Save-Render $control $width 260 "header-confirm-top-$width-$startHeight.png"
        $headerScroller.ScrollToBottom()
        Arrange-Control $control $width 260
        Save-Render $control $width 260 "header-confirm-bottom-$width-$startHeight.png"
        Click-Button $control.FindName('CancelClearAllButton')
        Arrange-Control $control $width 260
        if ($control.FindName('ImageList').Items.Count -ne 1 -or $headerScroller.VerticalOffset -ne 0 -or $control.FindName('ImageList').ActualHeight -le 0) { throw 'Header overflow did not recover after cancelling clear' }
        $headerCases += [pscustomobject]@{ Width=$width; Height=260; StartHeight=$startHeight; Reachable='confirmation, cancel, automatic scan and status'; Recovery='cancel restores image list and scroll offset' }
    } }
    $control.Width = $Widths[-1]; $control.Height = $Heights[-1]
    Arrange-Control $control $control.Width $control.Height
    $model = $control.FindName('PixelVisualization').DataContext
    $selector = $control.FindName('NeighborhoodChannelBox')
    $selector.SelectedItem = 'R'
    Arrange-Control $control $control.Width $control.Height
    if ($model.SelectedChannel -ne 'R' -or $model.Cells[12].Value -ne '155' -or $model.Mean -ne '155') { throw 'Rendered selector did not update neighborhood/statistics' }
    $model.SelectedChannel = 'B'
    Arrange-Control $control $control.Width $control.Height
    if ($selector.SelectedItem -ne 'B' -or $model.Cells[12].Value -ne '164') { throw 'Model did not update rendered selector' }
    $model.SelectedChannel = 'GV'
    if ($control.FindName('LayoutDerivedText').Text -ne 'Row bytes 51 · padding 5 B/row · buffer 275 B') { throw 'Derived layout disagrees with final-row extent contract' }
    foreach ($method in @('SetCompareA_Click', 'SetCompareB_Click')) { $control.GetType().GetMethod($method, $flags).Invoke($control, [object[]]@($null, [Windows.RoutedEventArgs]::new())) | Out-Null }
    if ($control.FindName('CompareASlot').DataContext -ne $document -or $control.FindName('CompareBSlot').DataContext -ne $document) { throw 'Compare slots lost document bindings' }
    $canvas = $control.FindName('OpenGlImageView'); $measure = $control.FindName('MeasureButton')
    $measure.IsChecked = $true
    $canvas.SelectPixelAtImagePixel(1, 0) | Out-Null; $canvas.SelectPixelAtImagePixel(4, 2) | Out-Null
    Arrange-Control $control $control.Width $control.Height
    if ($control.FindName('MeasurementStatusText').Text -ne 'Width 3 px · Height 2 px') { throw 'Measurement binding did not update' }
    $clear = $control.FindName('ClearMeasurementButton')
    if (-not $clear.Command.CanExecute($null)) { throw 'Clear measurement is unavailable' }
    $clear.Command.Execute($null)
    Arrange-Control $control $control.Width $control.Height
    if ($control.FindName('MeasurementStatusText').Text -ne 'Measure: click the first point') { throw 'Clear measurement did not reset status' }
    $measure.IsChecked = $false
    $canvas.PinMarkerAtImagePixel(8, 2) | Out-Null
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0, 0))) | Out-Null
    if ($control.FindName('PixelPositionText').Text -ne 'X 8  Y 2' -or $control.FindName('PixelBadgeText').Text -ne 'PINNED') { throw 'Pinned values changed on hover' }
    $canvas.ClearPinnedMarker()
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(-1, -1))) | Out-Null
    if ($control.FindName('PixelPositionText').Text -ne 'X 4  Y 2' -or $control.FindName('PixelBadgeText').Text -ne 'SELECTED') { throw 'Hover leave did not restore selection' }
    Click-Button $control.FindName('ClearButton')
    if ($control.FindName('ImageList').Items.Count -ne 1 -or $control.FindName('ClearAllConfirmation').Visibility -ne 'Visible') { throw 'Clear ran before confirmation' }
    Click-Button $control.FindName('CancelClearAllButton')
    if ($control.FindName('ImageList').Items.Count -ne 1 -or $control.FindName('ClearAllConfirmation').Visibility -ne 'Collapsed') { throw 'Cancel changed the list' }
    Click-Button $control.FindName('ClearButton'); Click-Button $control.FindName('ConfirmClearAllButton')
    if ($control.FindName('ImageList').Items.Count -ne 0 -or $control.FindName('SaveMenuButton').IsEnabled) { throw 'Confirmed clear did not reset presentation' }
    $status = $control.FindName('StatusText')
    if ($status.Text -ne $status.DataContext.Status -or -not $status.DataContext.StatusDetails.Contains('0 images')) { throw 'Clear lost the debugger-status binding or image-count detail' }
    $control.OpenPath($fixture)
    if ($control.FindName('ImageList').Items.Count -ne 1 -or -not $control.FindName('SaveMenuButton').IsEnabled) { throw 'Reopen did not recover' }
    foreach ($format in @('BGRA32', 'Mono16', 'Float32', 'Int32')) {
        $scalar = [RawBufferVisualizer.Core.RawImageDescriptor]::new()
        $scalar.Width = 1; $scalar.Height = 1; $scalar.PixelFormat = $format; $scalar.ValidBits = if ($format -eq 'Mono16') { 16 } elseif ($format -eq 'BGRA32') { 8 } else { 32 }
        $data = switch ($format) { 'BGRA32' { [byte[]]@(164,165,155,255) } 'Mono16' { [BitConverter]::GetBytes([uint16]65535) } 'Float32' { [BitConverter]::GetBytes([single]-1.234567e-30) } 'Int32' { [BitConverter]::GetBytes([int]-123456789) } }
        $scalar.Stride = $data.Length
        $path = Join-Path $output "$format.rbuf.json"
        [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save($path, [byte[]]$data, $scalar) | Out-Null
        $control.OpenPath($path)
        $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0, 0))) | Out-Null
        foreach ($width in $Widths) { foreach ($height in $Heights) { foreach ($strip in @($true, $false)) {
            $control.Width = $width; $control.Height = $height
            Arrange-Control $control $width $height
            if ($nav.IsImageStrip -ne $strip) { $nav.ToggleImageListCommand.Execute($null) }
            Arrange-Control $control $width $height
            $required = if ($format -eq 'BGRA32') { @('PixelRText','PixelGText','PixelBText','PixelAText','PixelGvText') } else { @('PixelValueText') }
            foreach ($name in $required) {
                Assert-InBounds $control.FindName($name) $bar "$format $name at $width x $height, strip=$strip"
                Assert-InBounds $control.FindName($name) $control "$format $name control viewport at $width x $height, strip=$strip"
            }
            if ($format -in @('BGRA32','Float32','Int32') -and $width -in @(320,760) -and $height -eq 260) {
                Save-Render $control $width $height "values-$format-$width-$height-strip-$strip.png"
            }
            $formats += [pscustomobject]@{ Format=$format; Width=$width; Height=$height; ImageStrip=$strip; Value=$control.FindName('PixelValueText').Text }
        } } }
    }
    $control.Width = 320; $control.Height = 260
    Arrange-Control $control 320 260
    if ($nav.IsImageStrip) { $nav.ToggleImageListCommand.Execute($null) }
    Arrange-Control $control 320 260
    $measure.IsChecked = $true
    Arrange-Control $control 320 260
    if ([Windows.Controls.Grid]::GetRow($control.FindName('PixelReadoutHost')) -ne 0) { throw 'Measurement did not restore the shared readout row' }
    foreach ($name in @('MeasurementStatusText','ClearMeasurementButton')) { Assert-InBounds $control.FindName($name) $bar "Scalar to measurement $name" }
    Save-Render $control 320 260 'scalar-measure-320.png'
    $measure.IsChecked = $false
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0, 0))) | Out-Null
    Arrange-Control $control 320 260
    Assert-InBounds $control.FindName('PixelValueText') $bar 'Scalar restored after measurement'
    Save-Render $control 320 260 'scalar-measure-recovery-320.png'
    [pscustomobject]@{ Layout=$cases; InspectorSections=$sections; ChannelFormats=$formats; HeaderOverflow=$headerCases; Bindings='selector, comparison, measurement'; Recovery='pin/hover leave, clear/cancel/confirm/reopen, scalar/measurement/scalar'; Scope='In-process WPF at 96 DPI; no desktop window, native canvas pixels, popup interaction, installed IDE or Windows DPI qualification' } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$output\results.json" -Encoding UTF8
    Write-Output "PASS: $($cases.Count) layouts, $($sections.Count) inspector section selections, $($formats.Count) format/width cases; $($headerCases.Count) expanded-header overflow cases; bindings and recovery transitions."
    Write-Output "Evidence: $output\results.json"
    Write-Output 'Scope: in-process WPF chrome and popup-content arrangement; native desktop/popup and installed IDE remain unverified.'
} finally { $control.Dispose() }

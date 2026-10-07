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
    [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Loaded)
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
function Find-TemplatePresenter($root, $template) {
    $pending=[Collections.Generic.Queue[Windows.DependencyObject]]::new(); $pending.Enqueue($root)
    while ($pending.Count -gt 0) {
        $node=$pending.Dequeue()
        if ($node -is [Windows.Controls.ContentPresenter] -and $node.ContentTemplate -eq $template) { return $node }
        for ($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++) { $pending.Enqueue([Windows.Media.VisualTreeHelper]::GetChild($node,$i)) }
    }
    throw 'Actual data-template presenter was not instantiated'
}
$control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$cases = @(); $sections = @(); $formats = @()
try {
    if ($control.FontFamily.Source -ne 'Segoe UI' -or $control.FontSize -ne 12) { throw 'View typography differs from the approved mockup' }
    $palette = @{ PanelBrush='#FF252526'; InputBrush='#FF1E1E1E'; ToolbarBrush='#FF2D2D30'; BorderBrush='#FF3F3F46'; TextBrush='#FFF1F1F1'; SecondaryTextBrush='#FFA3A7AE'; FaintTextBrush='#FF6E737B'; LinkBrush='#FF3794FF' }
    foreach ($key in $palette.Keys) { if ($control.Resources[$key].Color.ToString() -ne $palette[$key]) { throw "Mockup palette mismatch: $key" } }
    if ($control.FindName('OpenGlImageView').Background.Color.ToString() -ne '#FF141414') { throw 'View canvas underlay differs from the approved background' }
    $aliases=@{ RbvPanelBrush='PanelBrush'; RbvWindowBrush='InputBrush'; RbvInputBrush='InputBrush'; RbvInputHoverBrush='CommandHoverBrush'; RbvBorderBrush='BorderBrush'; RbvTextBrush='TextBrush'; RbvMutedTextBrush='SecondaryTextBrush'; RbvAttentionTextBrush='AttentionTextBrush'; RbvAccentBrush='StatusAccentBrush'; RbvAccentHoverBrush='LinkBrush'; RbvPressedBrush='CommandPressedBrush' }
    foreach ($key in $aliases.Keys) { $control.Resources[$key]=[Windows.Media.Brushes]::White }
    $control.GetType().GetMethod('ApplyVisualStudioTheme',$flags).Invoke($control,@()) | Out-Null
    foreach ($key in $aliases.Keys) { if ($control.Resources[$key] -ne $control.Resources[$aliases[$key]]) { throw "View theme did not restore its approved semantic brush: $key" } }
    foreach ($key in $palette.Keys) { if ($control.Resources[$key].Color.ToString() -ne $palette[$key]) { throw "View theme overwrote the approved primary palette: $key" } }
    $formatsInOrder=@($control.FindName('InterpretPixelFormatBox').Items | ForEach-Object { $_.ToString() })
    $approvedOrder=@('Mono8','Mono16','Mono10PackedLsb','Mono12PackedLsb','Binary','RGB24','BGR24','BGRA32','Float32','Int32','BayerRGGB8','BayerGRBG8','BayerGBRG8','BayerBGGR8')
    if (($formatsInOrder -join ',') -ne ($approvedOrder -join ',')) { throw 'Layout choices differ from the approved HTML option order' }
    if ($formatsInOrder.Count -ne [Enum]::GetValues([RawBufferVisualizer.Core.RawPixelFormat]).Length) { throw 'Layout option ordering removed an existing pixel format' }

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
        if ($width -eq 1440 -and $height -eq 720 -and $bar.ActualHeight -ne 30) { throw 'Wide pixel bar does not match the approved 30px row' }
        $pinAction=$control.FindName('PixelBarPinButton')
        if ($pinAction.Visibility -eq 'Visible') {
            $right=$pinAction.TranslatePoint([Windows.Point]::new($pinAction.ActualWidth,0),$bar).X
            if ([Math]::Abs($right-($bar.ActualWidth-8)) -gt 0.5) { throw 'Pixel Pin action is not aligned with the right edge' }
        }
        foreach ($name in @('PixelRText','PixelGText','PixelBText','PixelGvText')) {
            $runs=@($control.FindName($name).Inlines)
            if ($runs[0].Foreground.Color.ToString() -ne '#FFA3A7AE' -or $runs[-1].Foreground.Color.ToString() -ne '#FFF1F1F1') { throw "Pixel label/value contrast differs from the approved colors: $name" }
        }
        $nav.Show('Pixel')
        Arrange-Control $control $width $height
        $headerName=$control.FindName('ImageNameText'); $headerType=$control.FindName('ImageTypeText')
        if ($control.FindName('PixelPositionText').Text -ne 'X    8  Y    2') { throw 'Pixel coordinates lost the approved minimum four-character columns' }
        if ($headerType.Visibility -eq 'Visible') {
            $namePoint=$headerName.TranslatePoint([Windows.Point]::new(0,0),$control.FindName('ImageHeaderNames'))
            $typePoint=$headerType.TranslatePoint([Windows.Point]::new(0,0),$control.FindName('ImageHeaderNames'))
            if ([Math]::Abs($typePoint.X-$namePoint.X-$headerName.ActualWidth-8) -gt 0.5) { throw 'Visible source type does not follow the image name with the approved 8px gap' }
        }
        $scanAction=$control.FindName('ScanNowButton')
        if ([Windows.Controls.Grid]::GetRow($scanAction) -ne 1 -or [Windows.Controls.Grid]::GetColumnSpan($scanAction) -ne 2 -or $scanAction.Margin.Top -ne 6) { throw 'Scan now left the approved second action row' }
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
                # A closed PopupRoot can remeasure its child to zero. Arrange identical content in-process.
                $probePopup=$control.FindName('InspectorOverlayPopup'); $probePopup.Child=$null
                $probeRoot=[Windows.Controls.Grid]::new(); $probeRoot.Resources=$control.Resources
                [Windows.Documents.TextElement]::SetForeground($probeRoot,$control.Foreground)
                [Windows.Documents.TextElement]::SetFontFamily($probeRoot,$control.FontFamily)
                [Windows.Documents.TextElement]::SetFontSize($probeRoot,$control.FontSize)
                [Windows.NameScope]::SetNameScope($probeRoot,[Windows.NameScope]::GetNameScope($control))
                $probeRoot.Children.Add($panel) | Out-Null
                try { Arrange-Control $probeRoot $panel.Width $height }
                finally { $probeRoot.Children.Clear(); $probePopup.Child=$panel }
            } elseif ($control.FindName('DockedInspectorHost').Content -ne $panel -or [Math]::Abs($panel.ActualWidth - 300) -gt 0.5) { throw 'Docked panel did not reuse the inspector' }
            if ($section -eq 'Pixel' -and $control.FindName('RowProfileGraph').ActualWidth -le 0) { throw 'Row profile is unavailable' }
            if ($section -eq 'Pixel') {
                $graph=$control.FindName('RowProfileGraph')
                if ([Math]::Abs($graph.ActualHeight-$graph.ActualWidth*96/276) -gt 0.5) { throw 'Row profile no longer uses the approved SVG aspect ratio' }
                $stats=$control.FindName('PixelStatisticsCards')
                if ([Math]::Abs($stats.ActualHeight-39.8) -gt 1) { throw 'Statistics card typography differs from the approved row heights' }
            }
            if ($section -eq 'Pixel') {
                $cells = $control.FindName('PixelNeighborhoodGrid')
                $center = $cells.ItemContainerGenerator.ContainerFromIndex(12)
                $cell = [Windows.Media.VisualTreeHelper]::GetChild($center, 0)
                if ([Math]::Abs($cell.ActualWidth - 44) -gt 0.1 -or [Math]::Abs($cell.ActualHeight - 44) -gt 0.1) { throw 'Neighborhood cells differ from the approved 44px square layout' }
                $outline = $cells.ItemTemplate.FindName('NeighborhoodCenterBorder', $center)
                if ($outline.Visibility -ne 'Visible') { throw 'Neighborhood center outline is missing' }
                if ($width -ge 1000 -and $control.FindName('PixelNeighborhoodHeadingText').ActualHeight -gt 24) { throw 'Neighborhood heading wrapped despite the supported inspector width' }
            }
            if ($section -eq 'Format' -and [string]::IsNullOrEmpty($control.FindName('DescriptorText').Text)) { throw 'Descriptor is unavailable' }
            if ($section -eq 'Format') {
                foreach ($name in @('InterpretPixelFormatBox','InterpretWidthTextBox','InterpretHeightTextBox','InterpretStrideTextBox','InterpretValidBitsTextBox','InterpretByteOrderBox')) {
                    if ($control.FindName($name).ActualHeight -ne 24) { throw "Layout input height differs from the approved 24px: $name" }
                }
                if ($control.FindName('ApplyInterpretationButton').Background.Color.ToString() -ne '#FF007ACC') { throw 'Layout Apply lost the primary action color' }
                if ($control.FindName('DescriptorPanel').TranslatePoint([Windows.Point]::new(0,0), $control.FindName('FormatSection')).Y -le
                    $control.FindName('DiagnoseBufferButton').TranslatePoint([Windows.Point]::new(0,0), $control.FindName('FormatSection')).Y) { throw 'Descriptor displaced the approved Source/Layout/Doctor workflow' }
            }
            if ($section -eq 'Compare') {
                $slotA=$control.FindName('CompareASlot'); $slotB=$control.FindName('CompareBSlot'); $compare=$control.FindName('CompareSection')
                if ($slotB.TranslatePoint([Windows.Point]::new(0,0),$compare).Y -lt $slotA.ActualHeight) { throw 'Compare A/B slots are not stacked vertically' }
                foreach ($slot in @($slotA,$slotB)) {
                    $thumbnail=$slot.Child.Children[0]
                    if ($thumbnail.ActualWidth -ne 64 -or $thumbnail.ActualHeight -ne 48) { throw 'Compare thumbnail does not match the approved 64x48 slot' }
                }
            }
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
                        if ($control.FindName('InspectorTitleText').Foreground.Color.ToString() -ne '#FFF1F1F1') { throw 'Popup title lost its foreground inheritance' }
                        Save-Render $renderRoot ([int]$panel.ActualWidth) $height "inspector-$section-$width.png"
                    } finally { $renderRoot.Children.Clear(); $popup.Child = $panel }
                } else { Save-Render $panel ([int]$panel.ActualWidth) $height "inspector-$section-$width.png" }
            }
            $targets = switch ($section) {
                'Pixel' { @('RowProfileGraph') }
                'Format' { @('ApplyInterpretationButton','DiagnoseBufferButton','DescriptorPanel') }
                'Compare' { @('BlinkButton') }
                'Diagnostics' { @('DiagnosticsList') }
                default { @() }
            }
            $scroller=$control.FindName('InspectorPanel')
            $scrollRoot=$control
            if ($nav.IsNarrow) {
                # Closed native popups do not lay out their visual tree. Host the identical content in-process.
                $popup=$control.FindName('InspectorOverlayPopup'); $popup.Child=$null
                $scrollRoot=[Windows.Controls.Grid]::new(); $scrollRoot.Resources=$control.Resources
                [Windows.Documents.TextElement]::SetForeground($scrollRoot,$control.Foreground)
                [Windows.Documents.TextElement]::SetFontFamily($scrollRoot,$control.FontFamily)
                [Windows.Documents.TextElement]::SetFontSize($scrollRoot,$control.FontSize)
                [Windows.NameScope]::SetNameScope($scrollRoot,[Windows.NameScope]::GetNameScope($control))
                $scrollRoot.Children.Add($panel) | Out-Null
            }
            try {
                $scrollWidth=if ($nav.IsNarrow) { $panel.Width } else { $width }
                Arrange-Control $scrollRoot $scrollWidth $height
                foreach ($targetName in $targets) {
                    $target=$control.FindName($targetName); $target.BringIntoView()
                    Arrange-Control $scrollRoot $scrollWidth $height
                    Assert-InBounds $target $scroller "Inspector scroll access $targetName at $width x $height"
                }
                $scroller.ScrollToTop(); Arrange-Control $scrollRoot $scrollWidth $height
            } finally {
                if ($nav.IsNarrow) { $scrollRoot.Children.Clear(); $popup.Child=$panel }
            }
            $sections += [pscustomobject]@{ Width=$width; Height=$height; Section=$section; PanelWidth=$panel.ActualWidth; Overlay=$nav.IsNarrow; ScrollAccess=$targets }
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
    $popupRows=@{ AddImagePopup=3; SavePopup=2; MorePopup=4; ZoomPopup=8 }
    $control.FindName('ZoomSlider').Value=1
    foreach ($popupName in $popupRows.Keys) {
        $popup=$control.FindName($popupName); $menu=$popup.Child; $popup.Child=$null
        $menuRoot=[Windows.Controls.Grid]::new(); $menuRoot.Resources=$control.Resources
        [Windows.NameScope]::SetNameScope($menuRoot,[Windows.NameScope]::GetNameScope($control))
        try {
            $menuRoot.Children.Add($menu) | Out-Null
            $menu.Measure([Windows.Size]::new([double]::PositiveInfinity,[double]::PositiveInfinity))
            Arrange-Control $menuRoot $menu.DesiredSize.Width $menu.DesiredSize.Height
            if ($menu.ActualWidth -ne 196 -or $menu.Background.Color.ToString() -ne '#FF2D2D30') { throw "Popup palette/width differs from the mockup: $popupName" }
            $buttons=@($menu.Child.Children | Where-Object { $_ -is [Windows.Controls.Primitives.ButtonBase] })
            if ($buttons.Count -ne $popupRows[$popupName]) { throw "Popup action count changed: $popupName" }
            foreach ($button in $buttons) {
                if ($button.ActualHeight -ne 26) { throw "Popup row height differs from 26px: $popupName" }
                Assert-InBounds $button $menu "Popup command row $popupName"
            }
            if ($popupName -eq 'ZoomPopup') {
                foreach ($button in $buttons) {
                    $selected=$button.Content.Children[0].Opacity -eq 1
                    if ($selected -ne ([double]::Parse($button.Tag,[Globalization.CultureInfo]::InvariantCulture) -eq 1)) { throw 'Zoom check mark disagrees with the actual zoom slider value' }
                    if ([Windows.Automation.AutomationProperties]::GetName($button) -ne $button.Content.Children[1].Text) { throw 'Zoom accessibility caption lost its displayed value' }
                }
            }
            Save-Render $menuRoot ([int]$menu.ActualWidth) ([int]$menu.ActualHeight) "popup-$popupName.png"
        } finally { $menuRoot.Children.Clear(); $popup.Child=$menu }
    }
    $selector = $control.FindName('NeighborhoodChannelBox')
    $segments = $selector.Template.FindName('SegmentList', $selector)
    $segments.SelectedItem = 'R'
    Arrange-Control $control $control.Width $control.Height
    if ($selector.SelectedItem -ne 'R' -or $model.SelectedChannel -ne 'R' -or $model.Cells[12].Value -ne '155' -or $model.Mean -ne '155.00') { throw 'Rendered selector did not update neighborhood/statistics' }
    $model.SelectedChannel = 'B'
    Arrange-Control $control $control.Width $control.Height
    if ($segments.SelectedItem -ne 'B' -or $selector.SelectedItem -ne 'B' -or $model.Cells[12].Value -ne '164') { throw 'Model did not update rendered selector' }
    $model.SelectedChannel = 'GV'
    if ($control.FindName('LayoutDerivedText').Text -ne 'Row bytes 51 · padding 5 B/row · buffer 275 B') { throw 'Derived layout disagrees with final-row extent contract' }
    foreach ($method in @('SetCompareA_Click', 'SetCompareB_Click')) { $control.GetType().GetMethod($method, $flags).Invoke($control, [object[]]@($null, [Windows.RoutedEventArgs]::new())) | Out-Null }
    if ($control.FindName('CompareASlot').DataContext -ne $document -or $control.FindName('CompareBSlot').DataContext -ne $document) { throw 'Compare slots lost document bindings' }
    $nav.Show('Compare'); Arrange-Control $control $control.Width $control.Height
    foreach ($name in @('CompareASlot','CompareBSlot')) {
        $slot=$control.FindName($name)
        if ($slot.Child.Children[1].Children[0].Text -ne $document.ObjectName -or $slot.Child.Children[1].Children[1].Text -ne '17×5 · BGR24') { throw 'Compare rendered metadata lost its source binding' }
    }
    $nav.Show('Diagnostics')
    $control.GetType().GetMethod('UpdatePerformanceText',$flags).Invoke($control,@()) | Out-Null
    $metrics=$control.FindName('RenderingMetricsPanel')
    if ($metrics.DataContext -eq $null) { throw 'Rendering metrics do not receive the current render snapshot' }
    $snapshot=[RawBufferVisualizer.OpenGlCanvas.RawOpenGlRenderStats]::new()
    $snapshot.FrameCount=1234; $snapshot.MaxFrameMilliseconds=2.5; $snapshot.WheelInputCount=7; $snapshot.MaxWheelInputMilliseconds=0.7
    $snapshot.DragInputCount=8; $snapshot.MaxDragInputMilliseconds=0.8; $snapshot.TextureUploadCount=9; $snapshot.MaxTextureUploadMilliseconds=0.9
    $metrics.DataContext=$snapshot; Arrange-Control $control $control.Width $control.Height
    $expected=@('1,234','2.5 ms','7 · max 0.7 ms','8 · max 0.8 ms','9 · max 0.9 ms')
    for ($row=0;$row -lt 5;$row++) {
        $value=$metrics.Children[$row*2+1].Text
        if ($value -ne $expected[$row]) { throw "Rendering metric row $row lost the count, duration or unit: $value" }
    }
    $control.GetType().GetMethod('UpdatePerformanceText',$flags).Invoke($control,@()) | Out-Null
    $nav.Show('Pixel')

    $canvas = $control.FindName('OpenGlImageView'); $measure = $control.FindName('MeasureButton')
    $measure.IsChecked = $true
    $canvas.SelectPixelAtImagePixel(1, 0) | Out-Null; $canvas.SelectPixelAtImagePixel(4, 2) | Out-Null
    Arrange-Control $control $control.Width $control.Height
    if ($control.FindName('MeasurementStatusText').Text -ne 'Width 3 px · Height 2 px') { throw 'Measurement binding did not update' }
    if ($control.FindName('InspectorPixelStateBadge').Visibility -ne 'Collapsed') { throw 'Pixel inspector badge is displayed during measurement' }
    $clear = $control.FindName('ClearMeasurementButton')
    if (-not $clear.Command.CanExecute($null)) { throw 'Clear measurement is unavailable' }
    $clear.Command.Execute($null)
    Arrange-Control $control $control.Width $control.Height
    if ($control.FindName('MeasurementStatusText').Text -ne 'Measure: click the first point') { throw 'Clear measurement did not reset status' }
    $measure.IsChecked = $false
    $canvas.PinMarkerAtImagePixel(8, 2) | Out-Null
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0, 0))) | Out-Null
    if ($control.FindName('PixelPositionText').Text -ne 'X    8  Y    2' -or $control.FindName('PixelBadgeText').Text -ne 'PINNED') { throw 'Pinned values changed on hover' }
    if ($control.FindName('PixelBadgeText').Foreground.Color.ToString() -ne '#FF1B1203' -or $control.FindName('PixelBadgePath').Data -ne $control.Resources['IconPinGeometry']) { throw 'Pinned badge lost its approved foreground or vector icon' }
    $canvas.ClearPinnedMarker()
    $hover.Invoke($control, [object[]]@($null, [RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(-1, -1))) | Out-Null
    if ($control.FindName('PixelPositionText').Text -ne 'X    4  Y    2' -or $control.FindName('PixelBadgeText').Text -ne 'SELECTED') { throw 'Hover leave did not restore selection' }
    Click-Button $control.FindName('ClearButton')
    if ($control.FindName('ImageList').Items.Count -ne 1 -or $control.FindName('ClearAllConfirmation').Visibility -ne 'Visible') { throw 'Clear ran before confirmation' }
    Click-Button $control.FindName('CancelClearAllButton')
    if ($control.FindName('ImageList').Items.Count -ne 1 -or $control.FindName('ClearAllConfirmation').Visibility -ne 'Collapsed') { throw 'Cancel changed the list' }
    Click-Button $control.FindName('ClearButton'); Click-Button $control.FindName('ConfirmClearAllButton')
    if ($control.FindName('ImageList').Items.Count -ne 0 -or $control.FindName('SaveMenuButton').IsEnabled) { throw 'Confirmed clear did not reset presentation' }
    $status = $control.FindName('StatusText')
    if ($status.Text -ne $status.DataContext.Status -or -not $status.DataContext.StatusDetails.Contains('0 images')) { throw 'Clear lost the debugger-status binding or image-count detail' }
    foreach ($emptyCase in @(@(320,260),@(1000,720),@(1440,440))) {
        $emptyWidth=$emptyCase[0];$emptyHeight=$emptyCase[1];$control.Width=$emptyWidth;$control.Height=$emptyHeight
        Arrange-Control $control $emptyWidth $emptyHeight
        if ($control.FindName('PixelEmptyText').Text -ne 'No image selected.' -or $control.FindName('InspectorPixelStateBadge').Visibility -ne 'Collapsed') { throw 'No-image state retained pixel data or an inspector badge' }
        if ($control.FindName('EmptyViewerScanButton').Foreground.Color.ToString() -ne '#FFFFFFFF' -or $control.FindName('ConfirmClearAllButton').Foreground.Color.ToString() -ne '#FFFFFFFF' -or $control.FindName('ConfirmClearAllButton').BorderBrush.Color.ToString() -ne '#FFC42B1C') { throw 'Primary/danger normal-state colors differ from the approved CSS' }
        if ($control.FindName('EmptyViewerScanButton').IsEnabled -ne $control.FindName('ScanNowButton').IsEnabled) { throw 'Empty Scan action disagrees with the existing scan availability binding' }
        $emptyScroll=$control.FindName('EmptyViewerScroller')
        foreach ($name in @('EmptyViewerScanButton','EmptyViewerOpenButton')) {
            $action=$control.FindName($name);$action.BringIntoView();Arrange-Control $control $emptyWidth $emptyHeight
            Assert-InBounds $action $emptyScroll "Empty viewer action $name at $emptyWidth x $emptyHeight"
        }
        Save-Render $control $emptyWidth $emptyHeight "empty-$emptyWidth-$emptyHeight.png"
    }
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
            $list=$control.FindName('ImageList')
            $container=$list.ItemContainerGenerator.ContainerFromIndex($list.SelectedIndex)
            $cardPresenter=Find-TemplatePresenter $container $list.ItemTemplate
            $stateBadge=$list.ItemTemplate.FindName('StripStateText',$cardPresenter)
            if ($strip) {
                if ($stateBadge.Visibility -ne 'Visible' -or $stateBadge.ActualWidth -ne 12 -or $stateBadge.ActualHeight -ne 12 -or $stateBadge.Child.ContentTemplate -ne $control.Resources['ImageStateIconTemplate']) { throw 'Thumbnail strip lost the approved state shape badge' }
                $thumbnail=$list.ItemTemplate.FindName('CardThumbnail',$cardPresenter)
                Assert-InBounds $stateBadge $thumbnail "Strip state badge in thumbnail at $width x $height"
                $viewport=$stateBadge
                while ($viewport -ne $null -and $viewport -isnot [Windows.Controls.ScrollContentPresenter]) { $viewport=[Windows.Media.VisualTreeHelper]::GetParent($viewport) }
                if ($null -eq $viewport) { throw 'Thumbnail scroll viewport is unavailable' }
                # Verify the appended document through the list's real scrolling path.
                $scrollOwner=$viewport
                while ($scrollOwner -ne $null -and $scrollOwner -isnot [Windows.Controls.ScrollViewer]) { $scrollOwner=[Windows.Media.VisualTreeHelper]::GetParent($scrollOwner) }
                $scrollOwner.ScrollToBottom(); Arrange-Control $control $width $height
                Assert-InBounds $stateBadge $viewport "Strip state badge in scroll viewport at $width x $height"
                if ($container.Margin.Bottom -ne 4) { throw 'Thumbnail strip gap differs from the approved 4px' }
                if ($width -in @(320,640) -and $height -eq 260) { Save-Render $control $width $height "strip-scrolled-$format-$width-$height.png" }
                if ([string]$stateBadge.ToolTip -ne $list.SelectedItem.StateName) { throw 'State shape lost its existing state label' }
            } elseif ($stateBadge.Visibility -ne 'Collapsed') { throw 'Strip state shape remains visible in the expanded image list' }
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
    $invalid=Join-Path $output 'invalid.rbuf.json'; [IO.File]::WriteAllText($invalid,'{"descriptor":')
    $control.OpenPath($invalid)
    $errorDocument=$control.GetType().GetProperty('_activeDocument',$flags).GetValue($control,$null)
    if (-not $errorDocument.IsError -or $control.FindName('ErrorPanel').Visibility -ne 'Visible' -or $control.FindName('ErrorMessageText').Text -ne $errorDocument.ErrorMessage) { throw 'Malformed input did not render the actual error document' }
    foreach ($overlayCase in @(@(320,260),@(640,440),@(1000,720))) {
        $overlayWidth=$overlayCase[0];$overlayHeight=$overlayCase[1];$control.Width=$overlayWidth;$control.Height=$overlayHeight
        Arrange-Control $control $overlayWidth $overlayHeight
        $errorScroll=$control.FindName('ErrorScroller');$errorCard=$control.FindName('ErrorCard')
        if ($errorCard.BorderBrush.Color.ToString() -ne '#FFF48771' -or $errorCard.CornerRadius.TopLeft -ne 3 -or $errorCard.Child.Children[0].FontSize -ne 14) { throw 'Error card differs from the approved style' }
        if ($control.FindName('ErrorMessageText').MaxHeight -ne [double]::PositiveInfinity) { throw 'Error message is still limited to a clipped fixed height' }
        Save-Render $control $overlayWidth $overlayHeight "error-top-$overlayWidth-$overlayHeight.png"
        foreach ($name in @('CopyErrorReportButton','OpenErrorLogsButton')) {
            $action=$control.FindName($name);$action.BringIntoView();Arrange-Control $control $overlayWidth $overlayHeight
            Assert-InBounds $action $errorScroll "Error action $name at $overlayWidth x $overlayHeight"
            if ($action.Content -isnot [Windows.Controls.TextBlock] -or $action.Content.TextWrapping -ne 'Wrap') { throw 'Error caption cannot wrap at very narrow widths' }
            Assert-InBounds $action.Content $action "Error caption $name"
        }
        Save-Render $control $overlayWidth $overlayHeight "error-actions-$overlayWidth-$overlayHeight.png"
        $popup=$control.FindName('ReleaseAnnouncementPopup');$newsScroll=$popup.Child;$popup.Child=$null
        $newsRoot=[Windows.Controls.Grid]::new();$newsRoot.Resources=$control.Resources
        [Windows.Documents.TextElement]::SetForeground($newsRoot,$control.Resources['TextBrush'])
        [Windows.NameScope]::SetNameScope($newsRoot,[Windows.NameScope]::GetNameScope($control))
        try {
            $control.FindName('ReleaseAnnouncementBanner').Visibility='Visible'
            $newsRoot.Children.Add($newsScroll) | Out-Null
            $newsWidth=$control.FindName('CanvasArea').ActualWidth;$newsHeight=$control.FindName('CanvasArea').ActualHeight
            Arrange-Control $newsRoot $newsWidth $newsHeight
            $banner=$control.FindName('ReleaseAnnouncementBanner')
            if ($banner.Background.Color.ToString() -ne '#FF17384A' -or $banner.BorderBrush.Color.ToString() -ne '#FF2B88B8' -or $banner.BorderThickness.Left -ne 1 -or $banner.CornerRadius.TopLeft -ne 3 -or $control.FindName('ReleaseAnnouncementTitleText').FontSize -ne 12.5) { throw 'Release banner differs from the approved style' }
            foreach ($name in @('ReleaseNotesButton','DismissNewsButton')) {
                $action=$control.FindName($name);$action.BringIntoView();Arrange-Control $newsRoot $newsWidth $newsHeight
                Assert-InBounds $action $newsScroll "Release action $name at $overlayWidth x $overlayHeight"
                if ($name -eq 'ReleaseNotesButton') { Assert-InBounds $action.Content $action 'Release link caption' }
            }
            Save-Render $newsRoot ([int]$newsWidth) ([int]$newsHeight) "news-$overlayWidth-$overlayHeight.png"
        } finally { $newsRoot.Children.Clear();$popup.Child=$newsScroll;$control.FindName('ReleaseAnnouncementBanner').Visibility='Collapsed' }
    }
    $control.OpenPath($fixture);Arrange-Control $control 1000 720
    if ($control.FindName('ErrorPanel').Visibility -ne 'Collapsed' -or -not $control.FindName('SaveMenuButton').IsEnabled -or $control.FindName('ErrorMessageText').Text -ne '') { throw 'Valid reopen after malformed input did not recover the view' }
    $tip=$control.FindName('AutomaticHelpTip');$tip.PlacementTarget=$control.FindName('AutomaticInspectionActions')
    if ($tip.Background.Color.ToString() -ne '#FF2D2D30' -or $tip.FontSize -ne 11.5 -or $tip.Content.Children[1].Text -ne 'Refreshes image-like locals and arguments when the debugger pauses. This choice is saved. Scan now works while paused, even with Auto scan off.') { throw "Automatic help differs from the approved copy/style: background=$($tip.Background), font=$($tip.FontSize), text=$($tip.Content.Children[1].Text)" }
    $tip.ApplyTemplate() | Out-Null
    $tip.Measure([Windows.Size]::new(340,[double]::PositiveInfinity));$helpWidth=[int][Math]::Ceiling($tip.DesiredSize.Width);$helpHeight=[int][Math]::Ceiling($tip.DesiredSize.Height)
    Arrange-Control $tip $helpWidth $helpHeight;Save-Render $tip $helpWidth $helpHeight 'automatic-help-content.png'
    Assert-InBounds $tip.Content.Children[1] $tip 'Automatic help rendered explanation'
    if ([Math]::Abs($tip.Width-[Math]::Min(340,$control.FindName('AutomaticInspectionBar').ActualWidth+4)) -gt 0.5) { throw 'Automatic help width does not follow the image-panel content width' }
    $nav.Show('Format');Arrange-Control $control $control.Width $control.Height
    foreach ($name in @('InterpretPixelFormatBox','InterpretByteOrderBox')) {
        $choice=$control.FindName($name);$choice.ApplyTemplate() | Out-Null
        if ($choice.ActualHeight -ne 24 -or $choice.Template.FindName('ChoiceToggle',$choice) -eq $null) { throw "Layout choice lost the approved input template: $name" }
        $choiceContent=$choice.Template.FindName('ChoiceContent',$choice)
        Assert-InBounds $choiceContent $choice "Selected layout choice $name"
        $drop=$choice.Template.FindName('PART_Popup',$choice);$dropContent=$drop.Child;$drop.Child=$null
        $dropRoot=[Windows.Controls.Grid]::new();$dropRoot.Resources=$control.Resources
        try {
            $dropRoot.Children.Add($dropContent) | Out-Null
            $dropContent.Measure([Windows.Size]::new($choice.ActualWidth,260))
            $dropHeight=[int][Math]::Ceiling($dropContent.DesiredSize.Height)
            Arrange-Control $dropRoot $choice.ActualWidth $dropHeight
            if ($dropContent.Background.Color.ToString() -ne '#FF1E1E1E' -or $dropContent.BorderBrush.Color.ToString() -ne '#FF3F3F46') { throw "Layout dropdown has a platform color leak: $name" }
            if ($dropHeight -le 2) { throw "Layout dropdown did not render its actual items: $name" }
            Save-Render $dropRoot ([int]$choice.ActualWidth) $dropHeight "dropdown-$name.png"
        } finally { $dropRoot.Children.Clear();$drop.Child=$dropContent }
    }
    $imageMenu=$control.FindName('ImageList').ContextMenu
    $control.GetType().GetMethod('ImageListContextMenu_Opening',$flags).Invoke($control,[object[]]@($control.FindName('ImageList'),[Windows.RoutedEventArgs]::new())) | Out-Null
    $imageMenu.ApplyTemplate() | Out-Null;$imageMenu.Measure([Windows.Size]::new(280,[double]::PositiveInfinity))
    $menuWidth=[int][Math]::Ceiling($imageMenu.DesiredSize.Width);$menuHeight=[int][Math]::Ceiling($imageMenu.DesiredSize.Height)
    Arrange-Control $imageMenu $menuWidth $menuHeight
    if ($imageMenu.Background.Color.ToString() -ne '#FF2D2D30' -or $menuWidth -ne 196) { throw 'Image context menu differs from the approved menu width/palette' }
    foreach ($item in $imageMenu.Items) {
        if ($item -is [Windows.Controls.MenuItem] -and $item.Visibility -eq 'Visible') {
            if ($item.ActualHeight -ne 26) { throw 'Image context-menu row differs from 26px' }
            Assert-InBounds $item $imageMenu 'Existing image context-menu action'
            if (-not $item.IsEnabled -and $item.Foreground.Color.ToString() -ne '#FF6E737B') { throw 'Unavailable image menu action is not visibly disabled' }
        }
    }
    Save-Render $imageMenu $menuWidth $menuHeight 'image-context-menu.png'
    $list=$control.FindName('ImageList');$container=$list.ItemContainerGenerator.ContainerFromIndex($list.SelectedIndex)
    $cardPresenter=Find-TemplatePresenter $container $list.ItemTemplate
    $metadata=$list.ItemTemplate.FindName('CardMetadataText',$cardPresenter)
    if ($metadata.Foreground.Color.ToString() -ne '#FFF1F1F1' -or $metadata.Opacity -ne 0.85 -or $metadata.LineHeight -ne 13) { throw 'Selected image metadata differs from the approved color/line height' }
    $summary=$list.ItemTemplate.FindName('CardSummary',$cardPresenter)
    if ($summary.Children[0].LineHeight -ne 15) { throw 'Image title line height differs from the approved 15px' }
    $cardMore=$list.ItemTemplate.FindName('CardMoreButton',$cardPresenter)
    if ($cardMore.ActualWidth -ne 22 -or $cardMore.ActualHeight -ne 22 -or $cardMore.Visibility -ne 'Visible') { throw 'Image-card More action lost its approved bounds or availability' }
    Assert-InBounds $cardMore $container 'Image-card More action'
    if ([Windows.Automation.AutomationProperties]::GetName($cardMore) -ne ('More actions for '+$list.SelectedItem.ObjectName)) { throw 'Image-card More accessibility lost its target image name' }
    if ($nav.IsImageStrip) { throw 'Card action test requires the expanded image list' }
    $nav.ToggleImageListCommand.Execute($null);Arrange-Control $control $control.Width $control.Height
    if ($cardMore.Visibility -ne 'Collapsed') { throw 'Image-card More action remains visible in the thumbnail strip' }
    $nav.ToggleImageListCommand.Execute($null);$nav.Show('Pixel');Arrange-Control $control $control.Width $control.Height
    $hover.Invoke($control,[object[]]@($null,[RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0,0))) | Out-Null
    Arrange-Control $control $control.Width $control.Height
    if (@($control.FindName('PixelVisualization').DataContext.Cells | Where-Object { $_.Value -eq '—' }).Count -ne 16) { throw 'Corner neighborhood lost the required out-of-image cells' }
    Save-Render $control $control.Width $control.Height 'pixel-corner-neighborhood.png'

    # Exercise the reusable actual View template with isolated StateName rows, not fake live documents.
    $stateRows=[System.Data.DataTable]::new(); $stateRows.Columns.Add('StateName',[string]) | Out-Null
    $iconCases=@()
    $stateColors=@{ LIVE='#FF89D185'; CAPTURED='#FF75BEFF'; PREVIEW='#FFE2C08D'; MAP='#FFE2C08D'; FAILED='#FFF48771' }
    foreach ($state in @('LIVE','CAPTURED','PREVIEW','MAP','FAILED')) {
        $stateRows.Rows.Add($state) | Out-Null
        $iconHost=[Windows.Controls.ContentControl]::new(); $iconHost.Content=$stateRows.DefaultView[$stateRows.Rows.Count-1]; $iconHost.ContentTemplate=$control.Resources['ImageStateIconTemplate']
        $iconBadge=[Windows.Controls.Border]::new(); $iconBadge.Width=12; $iconBadge.Height=12; $iconBadge.CornerRadius=[Windows.CornerRadius]::new(2); $iconBadge.Background=$control.Resources['InputBrush']; $iconBadge.Child=$iconHost
        $iconHost.HorizontalAlignment='Center'; $iconHost.VerticalAlignment='Center'; $iconBadge.Resources=$control.Resources
        Arrange-Control $iconBadge 12 12
        $iconPresenter=Find-TemplatePresenter $iconHost $iconHost.ContentTemplate
        $shape=$iconHost.ContentTemplate.FindName('StateShape',$iconPresenter); $ring=$iconHost.ContentTemplate.FindName('PreviewRing',$iconPresenter)
        $color=if ($state -eq 'FAILED') { $shape.Stroke.Color.ToString() } else { $shape.Fill.Color.ToString() }
        if ($color -ne $stateColors[$state] -or $shape.Data.Bounds.IsEmpty) { throw "State shape lost its approved color/geometry: $state" }
        if (($ring.Visibility -eq 'Visible') -ne ($state -eq 'PREVIEW')) { throw 'Preview half-circle outline disagrees with state' }
        if ($state -eq 'LIVE' -and $shape.Data -isnot [Windows.Media.EllipseGeometry]) { throw 'Live state does not use its circle' }
        if ($state -eq 'FAILED' -and ($shape.Fill -ne $null -or $shape.StrokeThickness -ne 1.6)) { throw 'Failed state does not use its stroked cross' }
        Save-Render $iconBadge 12 12 "state-icon-$state.png"
        $iconCases+=[pscustomobject]@{State=$state;Color=$color;Geometry=[string]$shape.Data;Ring=[string]$ring.Visibility;Scope='synthetic row bound to the actual View template'}
    }

    # Supply host visibility only for this disconnected View; calculations/bindings remain real.
    $histogramCases=@(); $histogramView=$control.FindName('HistogramPanel'); $histogram=$histogramView.DataContext
    $previousContext=[Threading.SynchronizationContext]::Current
    [Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new())
    function Wait-HistogramResult($vm) {
        $deadline=[DateTime]::UtcNow.AddSeconds(15)
        while (-not $vm.Completion.IsCompleted) {
            [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background)
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Histogram result timed out' }
            [Threading.Thread]::Sleep(5)
        }
        if ($vm.Completion.IsFaulted) { throw $vm.Completion.Exception }
    }
    $histogramSource=$null
    try {
        $histogram.SetVisible($false)
        $hDescriptor=[RawBufferVisualizer.Core.RawImageDescriptor]::new(); $hDescriptor.Width=3; $hDescriptor.Height=1; $hDescriptor.Stride=9; $hDescriptor.PixelFormat='RGB24'; $hDescriptor.ValidBits=8
        $histogramSource=[RawBufferVisualizer.Core.RawImageSource]::FromMemory([byte[]]@(9,30,90,9,30,90,9,30,90),$hDescriptor)
        $histogram.SetSource($histogramSource); $histogram.SetVisible($true); Wait-HistogramResult $histogram
        Arrange-Control $histogramView 300 360
        if ($histogramView.FindName('HistogramFrequencyScale').Text -ne 'Count · linear · max 3' -or $histogramView.FindName('HistogramCoverage').Text -ne 'Coverage: full image · 3 px' -or $histogramView.FindName('HistogramValueRange').Text -ne 'min 43 · max 43') { throw 'Docked histogram captions disagree with the three RGB samples' }
        if ($histogramView.FindName('HistogramFrequencyScale').LineHeight -ne 16.675 -or $histogramView.FindName('HistogramCoverage').LineHeight -ne 18 -or $histogramView.FindName('HistogramCoverage').Foreground.Color.ToString() -ne '#FFF1F1F1') { throw 'Docked histogram result typography differs from approved captions' }
        $histogramAction=$histogramView.FindName('HistogramAction')
        if ($histogramAction.Content -ne 'Refresh' -or $histogramAction.ContentTemplate -ne $histogramView.Resources['DockedHistogramActionTemplate']) { throw 'Docked histogram action lost the Refresh icon template or caption' }
        Find-TemplatePresenter $histogramAction $histogramAction.ContentTemplate | Out-Null
        Save-Render $histogramView 300 360 'histogram-docked-full.png'
        $standaloneHistogram=New-Object RawBufferVisualizer.Presentation.HistogramView; $standaloneHistogram.DataContext=$histogram
        # Reproduce the existing App/MainWindow resource contract without launching the standalone app.
        [xml]$standaloneApp=Get-Content -Raw (Join-Path $PSScriptRoot '..\src\RawBufferVisualizer.Wpf\App.xaml')
        foreach ($key in @('MutedTextBrush','BorderBrush')) {
            $brushNode=@($standaloneApp.Application.'Application.Resources'.SolidColorBrush | Where-Object { $_.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml') -eq $key })[0]
            $standaloneHistogram.Resources[$key]=[Windows.Media.BrushConverter]::new().ConvertFromString($brushNode.Color)
        }
        $standaloneTextStyle=[Windows.Style]::new([Windows.Controls.TextBlock]); $standaloneTextStyle.BasedOn=$standaloneHistogram.FindResource('RbvTextBlockStyle')
        $standaloneHistogram.Resources[[Windows.Controls.TextBlock]]=$standaloneTextStyle
        $standaloneHistogram.Background=$standaloneHistogram.FindResource('RbvWindowBrush')
        Arrange-Control $standaloneHistogram 420 360
        $standaloneTitle=$standaloneHistogram.Content.Children[0].Children[0]
        if ($standaloneTitle.Foreground.Color.ToString() -ne '#FFF1F1F1' -or $standaloneHistogram.Content.Children[0].Children[2].Children[0].Foreground.Color.ToString() -ne '#FFF1F1F1' -or $standaloneHistogram.FindName('HistogramCanvas').PlotBrush.Color.ToString() -ne '#FF9CDCFE') { throw 'Standalone histogram lost its actual parent theme resource contract' }
        if ($standaloneHistogram.FindName('HistogramCoverage').Text -ne 'All pixels: 3' -or $standaloneHistogram.FindName('HistogramValueRange').Text -ne 'Range: 43 to 43' -or $standaloneHistogram.FindName('HistogramFrequencyScale').Text -ne 'Y: pixels per bin · 0 to 3' -or $standaloneHistogram.FindName('HistogramAction').ContentTemplate -ne $null) { throw 'Standalone histogram defaults changed with docked presentation' }
        Save-Render $standaloneHistogram 420 360 'histogram-standalone-full.png'
        $histogramCases+=[pscustomobject]@{Case='RGB full';Docked=$histogram.DockedCoverage;Standalone=$histogram.Coverage;Range=$histogram.DockedValueRange}
        $histogram.SetVisible($true); $histogramView.FindName('HistogramChannel').SelectedIndex=2; Wait-HistogramResult $histogram
        if ($histogram.SelectedChannelIndex -ne 2 -or $histogram.DockedValueRange -ne 'min 30 · max 30' -or $standaloneHistogram.FindName('HistogramChannel').SelectedIndex -ne 2) { throw 'Shared histogram channel binding lost its two-way contract' }
        $histogramCases+=[pscustomobject]@{Case='G selection';Range=$histogram.DockedValueRange}
        $histogram.SetSource($null); $histogramSource.Dispose(); $histogramSource=$null
        $hDescriptor.Width=2; $hDescriptor.Stride=8; $hDescriptor.PixelFormat='Float32'; $hDescriptor.ValidBits=32
        $hBytes=[byte[]]([BitConverter]::GetBytes([single]0.25)+[BitConverter]::GetBytes([single]1))
        $histogramSource=[RawBufferVisualizer.Core.RawImageSource]::FromMemory($hBytes,$hDescriptor)
        $histogram.SetSource($histogramSource); Wait-HistogramResult $histogram
        Arrange-Control $histogramView 300 360
        if ($histogram.DockedValueRange -ne 'min 0.250 · max 1.000' -or $histogram.ValueRange -ne 'Range: 0.25 to 1') { throw 'Float histogram formatting changed data or standalone output' }
        $histogramCases+=[pscustomobject]@{Case='Float32';Docked=$histogram.DockedValueRange;Standalone=$histogram.ValueRange}
        $histogram.SetSource($null); $histogramSource.Dispose(); $histogramSource=$null
        $hDescriptor.Width=1; $hDescriptor.Stride=4
        $histogramSource=[RawBufferVisualizer.Core.RawImageSource]::FromMemory([BitConverter]::GetBytes([single]::MaxValue),$hDescriptor)
        $histogram.SetVisible($true); $histogram.SetSource($histogramSource); Wait-HistogramResult $histogram
        if ($histogram.DockedValueRange -ne 'min 3.40282347E+38 · max 3.40282347E+38') { throw 'Large Float32 histogram lost its bounded scientific display' }
        $histogramCases+=[pscustomobject]@{Case='Float32 maximum';Docked=$histogram.DockedValueRange}
        $histogram.SetSource($null); $histogramSource.Dispose(); $histogramSource=$null
        $hDescriptor.Width=257; $hDescriptor.Height=257; $hDescriptor.Stride=257; $hDescriptor.PixelFormat='Mono8'; $hDescriptor.ValidBits=8
        $histogramSource=[RawBufferVisualizer.Core.RawImageSource]::FromMemory([byte[]]::new(66049),$hDescriptor)
        $histogram.SetVisible($true); $histogram.SetSource($histogramSource); Wait-HistogramResult $histogram
        if (-not $histogram.Result.IsSampled -or -not $histogram.DockedCoverage.StartsWith('Coverage: sampled · ') -or $histogram.DockedCoverage.Contains('full image')) { throw 'Sampled histogram falsely claims full-image coverage' }
        $histogramCases+=[pscustomobject]@{Case='Sampled';Docked=$histogram.DockedCoverage;Sampled=$histogram.Result.IsSampled}
        $histogram.SetSource($null); Arrange-Control $histogramView 300 360
        if ($histogram.DockedFrequencyScale -ne '' -or $histogram.DockedCoverage -ne '' -or $histogram.DockedValueRange -ne '' -or $histogramView.FindName('HistogramCoverage').Visibility -ne 'Collapsed') { throw 'Empty histogram retained stale result captions' }
        $histogramCases+=[pscustomobject]@{Case='Empty';StaleCaptions=$false}
    } finally {
        $histogram.SetVisible($false); $histogram.SetSource($null)
        if ($null -ne $histogramSource) { $histogramSource.Dispose() }
        [Threading.SynchronizationContext]::SetSynchronizationContext($previousContext)
    }

    # Reproduce a fixed-width RGB -> Float32 document switch through the real binding path.
    $sourceDescriptor=[RawBufferVisualizer.Core.RawImageDescriptor]::new()
    $sourceDescriptor.Width=640; $sourceDescriptor.Height=480; $sourceDescriptor.Stride=2560
    $sourceDescriptor.PixelFormat='Float32'; $sourceDescriptor.ValidBits=32
    $sourceValues=New-Object single[] (640*480); $sourceValues[0]=0.75
    $sourceBytes=New-Object byte[] (640*480*4); [Buffer]::BlockCopy($sourceValues,0,$sourceBytes,0,$sourceBytes.Length)
    $sourcePath=Join-Path $output 'depthMapF32.rbuf.json'
    [RawBufferVisualizer.Sdk.RawBufferSnapshot]::Save($sourcePath,$sourceBytes,$sourceDescriptor) | Out-Null
    $control.Width=1000; $control.Height=698; Arrange-Control $control 1000 698
    $control.GetType().GetMethod('OpenPath',$flags).Invoke($control,[object[]]@([string]$sourcePath.PSObject.BaseObject,'depthMapF32','OpenCvSharp.Mat',$null,$false,0,[long]0,[long]0,$null)) | Out-Null
    $hover.Invoke($control,[object[]]@($null,[RawBufferVisualizer.OpenGlCanvas.RawOpenGlPixelEventArgs]::new(0,0))) | Out-Null
    Arrange-Control $control 1000 698
    $imageName=$control.FindName('ImageNameText')
    if ($imageName.Text -ne 'depthMapF32' -or $imageName.ActualWidth -lt 84 -or $control.FindName('ImageTypeText').Visibility -ne 'Collapsed') { throw 'Document switch still reserves SourceType while clipping the required image name' }
    if ($control.FindName('PixelValueText').Text -ne '0.75000' -or $control.FindName('PixelNeighborhoodStatsText').Text -ne '5×5 μ 0.0833 σ 0.2357') { throw 'Float pixel bar differs from the approved precision or the fixture statistics' }
    $sourceDocument=$control.GetType().GetProperty('_activeDocument',$flags).GetValue($control,$null)
    $inventory=[Collections.Generic.List[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]]::new()
    $member=[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]::new(); $member.Name='DataPointer'; $inventory.Add($member)
    $sourceCases=@()
    foreach ($validation in @('', 'Fixture validation warning')) {
        $sourceDocument.SetAutomaticInspection(100,'Data → DataPointer · Width → Cols · Height → Rows · Stride → Step() · Format → Type()',$validation,$inventory,'',0,$false,$null)
        $control.GetType().GetMethod('UpdateAutomaticInspectionPanel',$flags).Invoke($control,[object[]]@($sourceDocument)) | Out-Null
        $nav.Show('Format'); Arrange-Control $control 1000 698
        $confidence=$control.FindName('AutomaticInspectionConfidenceText'); $validationText=$control.FindName('AutomaticInspectionValidationText')
        if ($confidence.Text -ne 'Automatic Vision Inspector · Confidence 100%' -or $confidence.FontSize -ne 12 -or $confidence.LineHeight -ne 18 -or $confidence.Foreground.Color.ToString() -ne '#FFF1F1F1') { throw 'Source caption differs from the approved text/typography' }
        if (($validationText.Visibility -eq 'Visible') -ne (-not [string]::IsNullOrEmpty($validation)) -or $validationText.Text -ne $validation) { throw 'Source validation text was hidden or left an empty row' }
        if ($control.FindName('EditAutomaticMappingButton').Visibility -ne 'Visible') { throw 'Source styling changed the existing member-inventory mapping contract' }
        $sourceCases+=[pscustomobject]@{ Validation=$validation; Visibility=[string]$validationText.Visibility; Confidence=$confidence.Text; ImageNameWidth=$imageName.ActualWidth }
        Save-Render $control 1000 698 $(if ($validation) { 'source-validation.png' } else { 'source-clean.png' })
    }
    $sourceDocument.SetAutomaticInspection(100,'','',[Collections.Generic.List[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]]::new(),'',0,$false,$null)
    $control.GetType().GetMethod('UpdateAutomaticInspectionPanel',$flags).Invoke($control,[object[]]@($sourceDocument)) | Out-Null
    if ($control.FindName('EditAutomaticMappingButton').Visibility -ne 'Collapsed') { throw 'Source styling enabled mapping without member inventory' }
    $targetUpdated=$control.GetType().GetMethod('ImageInfoBar_TargetUpdated',$flags)
    $headerOperation=$control.GetType().GetField('_imageHeaderUpdate',$flags)
    foreach ($cleanup in @('InspectorHost_Unloaded','Dispose')) {
        $targetUpdated.Invoke($control,[object[]]@($null,$null)) | Out-Null
        $pendingHeader=$headerOperation.GetValue($control)
        if ($null -eq $pendingHeader -or $pendingHeader.Status -ne 'Pending') { throw 'Header refresh was not deferred' }
        if ($cleanup -eq 'Dispose') { $control.Dispose() }
        else { $control.GetType().GetMethod($cleanup,$flags).Invoke($control,[object[]]@($null,[Windows.RoutedEventArgs]::new())) | Out-Null }
        if ($null -ne $headerOperation.GetValue($control) -or $pendingHeader.Status -ne 'Aborted') { throw "Header refresh survived its lifetime owner: $cleanup" }
    }
    $targetUpdated.Invoke($control,[object[]]@($null,$null)) | Out-Null
    if ($null -ne $headerOperation.GetValue($control)) { throw 'Disposed view accepted a new header refresh' }

    [pscustomobject]@{ HistogramPresentation=$histogramCases; StateIcons=$iconCases; SourcePresentation=$sourceCases; HeaderRefresh='document switch, unloaded/disposed cancellation'; Layout=$cases; InspectorSections=$sections; ChannelFormats=$formats; HeaderOverflow=$headerCases; Bindings='selector, comparison, measurement'; Recovery='pin/hover leave, clear/cancel/confirm/reopen, scalar/measurement/scalar'; Scope='In-process WPF at 96 DPI; no desktop window, native canvas pixels, popup interaction, installed IDE or Windows DPI qualification' } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$output\results.json" -Encoding UTF8
    Write-Output "PASS: $($cases.Count) layouts, $($sections.Count) inspector section selections, $($formats.Count) format/width cases; $($headerCases.Count) expanded-header overflow cases; bindings and recovery transitions."
    Write-Output "Evidence: $output\results.json"
    Write-Output 'Scope: in-process WPF chrome and popup-content arrangement; native desktop/popup and installed IDE remain unverified.'
} finally { $control.Dispose() }

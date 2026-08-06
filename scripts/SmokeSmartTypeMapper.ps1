param(
    [string]$Configuration = "Release",
    [string]$Framework = "net472",
    [string]$OutputDir = "artifacts\ui\smart-type-mapper",
    [switch]$VerifyVssdkDiscoveryOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

function Find-VssdkToolsPath {
    $nugetPackagesRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        Join-Path $env:USERPROFILE ".nuget\packages"
    } else {
        $env:NUGET_PACKAGES
    }
    $vssdkPackageRoot = Join-Path $nugetPackagesRoot "microsoft.vssdk.buildtools"
    if (-not (Test-Path -LiteralPath $vssdkPackageRoot -PathType Container)) {
        throw "Microsoft.VSSDK.BuildTools was not found below $nugetPackagesRoot. Restore the solution first."
    }

    $resolvedPath = Get-ChildItem -LiteralPath $vssdkPackageRoot -Directory |
        Sort-Object @{ Expression = { [version]$_.Name }; Descending = $true } |
        ForEach-Object { Join-Path $_.FullName "tools\vssdk" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($resolvedPath)) {
        throw "No restored Microsoft.VSSDK.BuildTools package contains tools\vssdk. Restore the solution first."
    }

    return $resolvedPath
}

if (-not $NoBuild) {
    dotnet build .\RawBufferVisualizer.sln --configuration $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

if ($VerifyVssdkDiscoveryOnly) {
    $verifiedVssdkToolsPath = Find-VssdkToolsPath
    foreach ($assemblyName in @(
        "Microsoft.VisualStudio.Validation.dll",
        "Microsoft.VisualStudio.Threading.dll",
        "Microsoft.VisualStudio.Shell.Framework.dll",
        "Microsoft.VisualStudio.Shell.15.0.dll")) {
        $dependencyPath = Join-Path $verifiedVssdkToolsPath $assemblyName
        if (-not (Test-Path -LiteralPath $dependencyPath -PathType Leaf)) {
            throw "$assemblyName was not found at $verifiedVssdkToolsPath."
        }
    }

    Write-Host "Smart Type Mapper VSSDK discovery validation passed: $verifiedVssdkToolsPath"
    return
}

$outputRoot = if ([IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $repoRoot $OutputDir }
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$interopCandidates = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll"
)
$interopPath = $interopCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($interopPath)) {
    throw "Microsoft.VisualStudio.Interop.dll was not found."
}
[Reflection.Assembly]::LoadFrom($interopPath) | Out-Null

$publicAssembliesPath = Split-Path -Parent $interopPath
foreach ($assemblyName in @(
    "Microsoft.VisualStudio.OLE.Interop.dll",
    "Microsoft.VisualStudio.Shell.Interop.dll",
    "Microsoft.VisualStudio.Debugger.InteropA.dll",
    "envdte.dll")) {
    $dependencyPath = Join-Path $publicAssembliesPath $assemblyName
    if (Test-Path -LiteralPath $dependencyPath) {
        [Reflection.Assembly]::LoadFrom($dependencyPath) | Out-Null
    }
}

$vssdkToolsPath = Find-VssdkToolsPath
foreach ($assemblyName in @(
    "Microsoft.VisualStudio.Validation.dll",
    "Microsoft.VisualStudio.Threading.dll",
    "Microsoft.VisualStudio.Shell.Framework.dll",
    "Microsoft.VisualStudio.Shell.15.0.dll")) {
    $dependencyPath = Join-Path $vssdkToolsPath $assemblyName
    if (-not (Test-Path -LiteralPath $dependencyPath)) {
        throw "$assemblyName was not found at $vssdkToolsPath."
    }
    [Reflection.Assembly]::LoadFrom($dependencyPath) | Out-Null
}

if (-not ("RawBufferDockedLayoutNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferDockedLayoutNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
}
'@
}

if (-not ("MapperSmokeSample" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MapperSmokeSample {
    private static GCHandle _handle;
    public static byte[] Buffer;
    public static long Create(int width, int height) {
        Buffer = new byte[width * height];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                Buffer[(y * width) + x] = (byte)((x + (2 * y)) % 256);
            }
        }

        _handle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        return _handle.AddrOfPinnedObject().ToInt64();
    }

    public static void Free() {
        if (_handle.IsAllocated) {
            _handle.Free();
        }
    }
}
'@
}

function Wait-Dispatcher([int]$Milliseconds) {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($Milliseconds)
    $timer.Add_Tick({
        $timer.Stop()
        $frame.Continue = $false
    })
    $timer.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object RawBufferDockedLayoutNative+RECT
    [RawBufferDockedLayoutNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $captureWidth = $rect.Right - $rect.Left
    $captureHeight = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $captureWidth, $captureHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($captureWidth, $captureHeight))
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$assemblyPath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualStudio.Vssdk\$Configuration\$Framework\RawBufferVisualizer.VisualStudio.Vssdk.dll"
$assemblyDirectory = Split-Path -Parent $assemblyPath
foreach ($dependency in @("RawBufferVisualizer.Core.dll", "RawBufferVisualizer.Sdk.dll", "RawBufferVisualizer.VisualStudio.ObjectSource.dll", "RawBufferVisualizer.VisualStudio.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $assemblyDirectory $dependency)) | Out-Null
}
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null

$mappingPath = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::GetDefaultUserMappingPath()
$mappingBackupPath = $null
if (Test-Path -LiteralPath $mappingPath) {
    $mappingBackupPath = $mappingPath + ".smoke-backup"
    Copy-Item -LiteralPath $mappingPath -Destination $mappingBackupPath -Force

    $smokeStore = [RawBufferVisualizer.VisualStudio.ObjectSource.TypeMappingStore]::new($null, $mappingPath)
    $smokeFile = $smokeStore.LoadUserFile()
    for ($i = $smokeFile.Mappings.Count - 1; $i -ge 0; $i--) {
        if ($smokeFile.Mappings[$i].TypeName -eq "Company.Vision.CompanyFrame" -and
            $smokeFile.Mappings[$i].AssemblyName -eq "Company.Vision") {
            $smokeFile.Mappings.RemoveAt($i)
        }
    }
    $smokeStore.Save($smokeFile)
}
$mappingStateBeforeDialog = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }

$currentPid = [Diagnostics.Process]::GetCurrentProcess().Id
$bufferAddress = [MapperSmokeSample]::Create(640, 480)
$dialogCapturePath = Join-Path $outputRoot "after-mapping-dialog.png"
$diagnosisCapturePath = Join-Path $outputRoot "after-connect-doctor-selection.png"
$popupCapturePath = Join-Path $outputRoot "after-pixel-format-popup.png"
$buttonStateCapturePath = Join-Path $outputRoot "after-button-hover-focus.png"
$menuCapturePath = Join-Path $outputRoot "after-error-row-menu.png"
$testScreen = [System.Windows.Forms.Screen]::AllScreens |
    Sort-Object { $_.Bounds.Left } |
    Select-Object -First 1

try {
    $inventory = New-Object "System.Collections.Generic.List[RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem]"
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "ImageAddress"; $member.Kind = "Property"; $member.TypeName = "IntPtr"
    $member.SampleValue = ("0x{0:X}" -f $bufferAddress)
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "SizeX"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "640"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "SizeY"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "480"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "LinePitch"; $member.Kind = "Property"; $member.TypeName = "Int32"; $member.SampleValue = "640"
    $inventory.Add($member)
    $member = New-Object RawBufferVisualizer.VisualStudio.ObjectSource.VisualizerMemberInventoryItem
    $member.Name = "PixelType"; $member.Kind = "Property"; $member.TypeName = "CompanyPixelType"; $member.SampleValue = "Mono8"
    $enumValues = New-Object "System.Collections.Generic.List[string]"
    $enumValues.Add("Mono8"); $enumValues.Add("Mono12"); $enumValues.Add("Bgr")
    $member.EnumValues = $enumValues
    $inventory.Add($member)

    $requestPath = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteErrorRequest(
        $currentPid,
        "[0]",
        "Company.Vision.CompanyFrame",
        "Unsupported collection image type: Company.Vision.CompanyFrame",
        $null, $null, $null,
        $inventory,
        "Company.Vision",
        $currentPid)

    $control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
    $window = New-Object System.Windows.Window
    $window.Title = "Raw Buffer Visualizer Smart Type Mapper"
    $window.Width = 1160
    $window.Height = 520
    $window.Left = $testScreen.WorkingArea.Left + 20
    $window.Top = $testScreen.WorkingArea.Top + 20
    $window.Topmost = $true
    $window.Content = $control
    $window.Show()
    Wait-Dispatcher 500

    $windowRect = New-Object RawBufferDockedLayoutNative+RECT
    $windowHandle = (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle
    [RawBufferDockedLayoutNative]::GetWindowRect($windowHandle, [ref]$windowRect) | Out-Null
    $intersectsTestScreen = $windowRect.Right -gt $testScreen.Bounds.Left -and
        $windowRect.Left -lt $testScreen.Bounds.Right -and
        $windowRect.Bottom -gt $testScreen.Bounds.Top -and
        $windowRect.Top -lt $testScreen.Bounds.Bottom
    if (-not $intersectsTestScreen) {
        throw "Smart Type Mapper smoke window did not intersect the leftmost monitor."
    }

    $control.OpenHandoffRequest($requestPath)
    Wait-Dispatcher 500

    $imageList = $control.FindName("ImageList")
    if ($imageList.Items.Count -eq 0) {
        throw "Error handoff did not create a document row."
    }

    $imageList.SelectedIndex = 0
    Wait-Dispatcher 250

    $imageList.ContextMenu.IsOpen = $true
    Wait-Dispatcher 300
    $mapMenuItem = $control.FindName("MapThisTypeMenuItem")
    if ($mapMenuItem.Visibility -ne [System.Windows.Visibility]::Visible) {
        throw "Connect Your Buffer menu item was not visible for an error row with a member inventory."
    }

    Capture-Window (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle $menuCapturePath
    $imageList.ContextMenu.IsOpen = $false
    Wait-Dispatcher 200

    $driveTimer = New-Object System.Windows.Threading.DispatcherTimer
    $driveTimer.Interval = [TimeSpan]::FromMilliseconds(800)
    $driveTimer.Add_Tick({
        $driveTimer.Stop()
        $dialog = $null
        foreach ($source in [System.Windows.Interop.HwndSource]::CurrentSources) {
            $candidate = $source.RootVisual
            if ($candidate -is [RawBufferVisualizer.VisualStudio.Vssdk.TypeMappingDialog]) {
                $dialog = $candidate
                $script:dialogHwnd = $source.Handle
                break
            }
        }

        if ($null -eq $dialog) {
            throw "Type mapping dialog did not open from the error row."
        }

        if ($dialog.FindName("DataBox").SelectedItem -ne "ImageAddress") { throw "Data preselection failed." }
        if ($dialog.FindName("WidthBox").SelectedItem -ne "SizeX") { throw "Width preselection failed." }
        if ($dialog.FindName("HeightBox").SelectedItem -ne "SizeY") { throw "Height preselection failed." }
        if ($dialog.FindName("StrideBox").SelectedItem -ne "LinePitch") { throw "Stride preselection failed." }
        if ($dialog.FindName("PixelFormatBox").SelectedItem -ne "PixelType") { throw "Pixel format preselection failed." }
        if ($dialog.FindName("EnumMappingPanel").Visibility -ne [System.Windows.Visibility]::Visible) { throw "Enum mapping grid was not shown for the enum member." }

        $dialog.FindName("WidthBox").SelectedItem = "SizeY"
        $dialog.FindName("ByteOrderBox").SelectedItem = "BigEndian"
        $dialog.FindName("UseSuggestedRolesButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 200
        if ($dialog.FindName("WidthBox").SelectedItem -ne "SizeX") { throw "Use Suggested Roles did not restore the inferred width." }
        if ($dialog.FindName("ByteOrderBox").SelectedItem -ne "LittleEndian") { throw "Use Suggested Roles did not restore default byte order." }
        if ($null -ne $dialog.FindName("PreviewImage").Source) { throw "Use Suggested Roles unexpectedly rendered a preview." }
        $mappingStateAfterReset = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }
        if ($mappingStateAfterReset -ne $mappingStateBeforeDialog) { throw "Use Suggested Roles unexpectedly changed the mapping file." }

        $diagnoseToggle = $dialog.FindName("DiagnoseInterpretationButton")
        $diagnoseToggle.IsChecked = $true
        Wait-Dispatcher 1800
        $diagnosisPanel = $dialog.FindName("DiagnosisResultPanel")
        $diagnosisList = $dialog.FindName("DiagnosisCandidateList")
        if ($diagnosisPanel.Visibility -ne [System.Windows.Visibility]::Visible) {
            throw "Connect Doctor result panel did not open."
        }
        if ($diagnosisList.Items.Count -eq 0) {
            throw "Connect Doctor returned no ranked candidates."
        }

        $matchingCandidate = $null
        foreach ($candidateItem in $diagnosisList.Items) {
            $candidate = $candidateItem.Candidate.Descriptor
            if ($candidate.Width -eq 640 -and $candidate.Height -eq 480 -and
                $candidate.Stride -eq 640 -and $candidate.PixelFormat.ToString() -eq "Mono8") {
                $matchingCandidate = $candidateItem
                break
            }
        }
        if ($null -eq $matchingCandidate) {
            throw "Connect Doctor did not retain the current valid Mono8 interpretation."
        }
        $diagnosisList.UpdateLayout()
        $matchingContainer = $diagnosisList.ItemContainerGenerator.ContainerFromItem($matchingCandidate)
        $matchingAccessibleName = [System.Windows.Automation.AutomationProperties]::GetName($matchingContainer)
        if ($matchingAccessibleName -notlike "*Mono8*640*480*stride 640*") {
            throw "Connect Doctor candidate did not expose a useful accessible name. Actual: '$matchingAccessibleName'"
        }

        $unrepresentableCandidate = $null
        foreach ($candidateItem in $diagnosisList.Items) {
            $candidate = $candidateItem.Candidate.Descriptor
            if ($candidate.Width -ne 640 -or $candidate.Height -ne 480 -or $candidate.Stride -ne 640) {
                $unrepresentableCandidate = $candidateItem
                break
            }
        }
        if ($null -eq $unrepresentableCandidate) {
            throw "Connect Doctor did not expose an interpretation that the current numeric members cannot represent."
        }

        $diagnosisList.SelectedItem = $unrepresentableCandidate
        Wait-Dispatcher 300
        if (-not $dialog.FindName("DiagnosisApplyStatusText").Text.Contains("cannot persist")) {
            throw "Connect Doctor did not explain why the selected candidate cannot be persisted."
        }
        $dialog.FindName("SaveButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 200
        if (-not $dialog.FindName("StatusText").Text.StartsWith("Mapping was not saved.")) {
            throw "Save Mapping did not block a diagnosis that the visible member roles cannot reproduce."
        }
        $mappingStateAfterBlockedSave = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }
        if ($mappingStateAfterBlockedSave -ne $mappingStateBeforeDialog) {
            throw "Blocked Connect Doctor save unexpectedly changed the mapping file."
        }

        $diagnosisList.SelectedItem = $matchingCandidate
        Wait-Dispatcher 500
        if ($null -eq $dialog.FindName("PreviewImage").Source) {
            throw "Selecting a Connect Doctor candidate did not render a preview."
        }
        if ($dialog.FindName("WidthBox").SelectedItem -ne "SizeX" -or
            $dialog.FindName("HeightBox").SelectedItem -ne "SizeY" -or
            $dialog.FindName("StrideBox").SelectedItem -ne "LinePitch" -or
            $dialog.FindName("ByteOrderBox").SelectedItem -ne "LittleEndian") {
            throw "Selecting a Connect Doctor candidate did not update the visible mapping draft."
        }
        if (-not $dialog.FindName("DiagnosisApplyStatusText").Text.Contains("nothing has been saved")) {
            throw "Connect Doctor did not expose the draft-only persistence boundary."
        }
        $mappingStateAfterDiagnosis = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }
        if ($mappingStateAfterDiagnosis -ne $mappingStateBeforeDialog) { throw "Connect Doctor selection unexpectedly changed the mapping file." }
        if ($imageList.Items.Count -ne 1) { throw "Connect Doctor selection unexpectedly appended an image row." }
        Capture-Window $script:dialogHwnd $diagnosisCapturePath

        $diagnoseToggle.IsChecked = $false
        Wait-Dispatcher 200
        if ($diagnosisPanel.Visibility -ne [System.Windows.Visibility]::Collapsed) {
            throw "Selecting Diagnose interpretation a second time did not close the result panel."
        }
        $mappingStateAfterDiagnosisClose = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }
        if ($mappingStateAfterDiagnosis -ne $mappingStateAfterDiagnosisClose) {
            throw "Closing Connect Doctor unexpectedly changed the mapping file."
        }

        $dialog.FindName("UseSuggestedRolesButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 200
        if ($null -ne $diagnosisList.SelectedItem) { throw "Use Suggested Roles did not clear the selected diagnosis candidate." }
        if ($null -ne $dialog.FindName("PreviewImage").Source) { throw "Use Suggested Roles did not clear the candidate preview." }

        $dialog.FindName("ByteOrderBox").SelectedItem = "BigEndian"
        $dialog.FindName("PreviewButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 600
        if ($null -eq $dialog.FindName("PreviewImage").Source) {
            throw "Preview did not render from live process memory."
        }

        $enumRows = $dialog.FindName("EnumMappingRows").Children
        foreach ($row in $enumRows) {
            $label = $row.Children[0].Text
            $box = $row.Children[1]
            if ($label -eq "Mono12") { $box.SelectedItem = "Mono12PackedLsb" }
            if ($label -eq "Bgr") { $box.SelectedItem = "BGR24" }
        }

        $copyButton = $dialog.FindName("CopyTemplateButton")
        $copyButton.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Dispatcher 200
        $copiedTemplate = [System.Windows.Clipboard]::GetText()
        foreach ($expectedTemplateText in @("RawBufferView", "frame.ImageAddress", "frame.SizeX", "RawPixelFormat.Mono8")) {
            if (-not $copiedTemplate.Contains($expectedTemplateText)) {
                throw "Copied RawBufferView template is missing '$expectedTemplateText'."
            }
        }
        foreach ($forbiddenTemplateText in @("Basler", "Pylon", "Spinnaker")) {
            if ($copiedTemplate.Contains($forbiddenTemplateText)) {
                throw "Copied RawBufferView template contains proprietary SDK text '$forbiddenTemplateText'."
            }
        }
        $mappingStateAfterCopy = if (Test-Path -LiteralPath $mappingPath) { [IO.File]::ReadAllText($mappingPath) } else { $null }
        if ($mappingStateAfterCopy -ne $mappingStateBeforeDialog) { throw "Copy RawBufferView Template unexpectedly changed the mapping file." }
        if ($imageList.Items.Count -ne 1) { throw "Draft actions unexpectedly appended an image row." }

        $copyButton.Focus() | Out-Null
        $copyButtonPoint = $copyButton.PointToScreen([System.Windows.Point]::new(8, 8))
        [RawBufferDockedLayoutNative]::SetCursorPos([int]$copyButtonPoint.X, [int]$copyButtonPoint.Y) | Out-Null
        Wait-Dispatcher 100
        if (-not $copyButton.IsKeyboardFocusWithin) { throw "Copy template button did not expose keyboard focus." }
        if (-not $copyButton.IsMouseOver) { throw "Copy template button did not expose pointer hover." }
        Capture-Window $script:dialogHwnd $buttonStateCapturePath
        $copyButton.IsEnabled = $false
        Wait-Dispatcher 100
        if ([Math]::Abs($copyButton.Opacity - 0.45) -gt 0.01) { throw "Disabled button theme state did not apply." }
        $copyButton.IsEnabled = $true

        $dialog.FindName("PixelFormatBox").IsDropDownOpen = $true
        Wait-Dispatcher 300
        Capture-Window $script:dialogHwnd $popupCapturePath
        $dialog.FindName("PixelFormatBox").IsDropDownOpen = $false
        Wait-Dispatcher 300
        Capture-Window $script:dialogHwnd $dialogCapturePath
        Wait-Dispatcher 300
        $dialog.FindName("SaveButton").RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
    })
    $driveTimer.Start()

    $mapMenuItem.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.MenuItem]::ClickEvent)))
    Wait-Dispatcher 3000

    if (-not (Test-Path -LiteralPath $mappingPath)) {
        throw "Mapping file was not written."
    }

    $savedJson = [IO.File]::ReadAllText($mappingPath)
    foreach ($expected in @("Company.Vision.CompanyFrame", "Company.Vision", "ImageAddress", "SizeX", "LinePitch", "pixelFormatMap", "Mono12PackedLsb", "BGR24", "BigEndian")) {
        if (-not $savedJson.Contains($expected)) {
            throw "Saved mapping file is missing '$expected'."
        }
    }

    $reopenedDialog = [RawBufferVisualizer.VisualStudio.Vssdk.TypeMappingDialog]::new(
        $inventory,
        "Company.Vision.CompanyFrame",
        "Company.Vision",
        $currentPid,
        $null,
        $false)
    $reopenedDialog.Owner = $window
    $reopenedDialog.Show()
    Wait-Dispatcher 350
    if ($reopenedDialog.FindName("DataBox").SelectedItem -ne "ImageAddress") { throw "Saved data role was not restored on reopen." }
    if ($reopenedDialog.FindName("WidthBox").SelectedItem -ne "SizeX") { throw "Saved width role was not restored on reopen." }
    if ($reopenedDialog.FindName("StrideBox").SelectedItem -ne "LinePitch") { throw "Saved stride role was not restored on reopen." }
    if ($reopenedDialog.FindName("ByteOrderBox").SelectedItem -ne "BigEndian") { throw "Saved byte order was not restored on reopen." }
    $reopenedDialog.Close()
    Wait-Dispatcher 150
    if ([IO.File]::ReadAllText($mappingPath) -ne $savedJson) { throw "Reopening the mapping dialog unexpectedly changed the saved file." }

    $unavailableDialog = [RawBufferVisualizer.VisualStudio.Vssdk.TypeMappingDialog]::new(
        $inventory,
        "Company.Vision.CompanyFrame",
        "Company.Vision",
        0,
        $null,
        $false)
    $unavailableDialog.Owner = $window
    $unavailableDialog.Show()
    Wait-Dispatcher 150
    $unavailableDialog.FindName("DiagnoseInterpretationButton").IsChecked = $true
    Wait-Dispatcher 150
    if (-not $unavailableDialog.FindName("DiagnosisStatusText").Text.StartsWith("Diagnosis unavailable:")) {
        throw "Connect Doctor did not present a controlled unavailable state without a paused process source."
    }
    if ($unavailableDialog.FindName("DiagnosisCandidateList").Items.Count -ne 0) {
        throw "Unavailable Connect Doctor state unexpectedly exposed candidates."
    }
    $unavailableDialog.Close()
    Wait-Dispatcher 150
    if ([IO.File]::ReadAllText($mappingPath) -ne $savedJson) { throw "Unavailable diagnosis unexpectedly changed the saved file." }

    $window.Close()
    Wait-Dispatcher 250

    [pscustomobject]@{
        MenuCapture = $menuCapturePath
        DialogCapture = $dialogCapturePath
        DiagnosisCapture = $diagnosisCapturePath
        PopupCapture = $popupCapturePath
        ButtonStateCapture = $buttonStateCapturePath
        MappingFile = $mappingPath
        MonitorName = $testScreen.DeviceName
        MonitorBounds = $testScreen.Bounds.ToString()
        WindowBounds = "Left=$($windowRect.Left),Top=$($windowRect.Top),Right=$($windowRect.Right),Bottom=$($windowRect.Bottom)"
    } | Format-List
    Write-Host "Smart Type Mapper smoke passed. Captures: $menuCapturePath, $dialogCapturePath"
}
finally {
    [MapperSmokeSample]::Free()
    if ($mappingBackupPath -ne $null) {
        Copy-Item -LiteralPath $mappingBackupPath -Destination $mappingPath -Force
        Remove-Item -LiteralPath $mappingBackupPath -Force
    }
    elseif (Test-Path -LiteralPath $mappingPath) {
        Remove-Item -LiteralPath $mappingPath -Force
    }
}

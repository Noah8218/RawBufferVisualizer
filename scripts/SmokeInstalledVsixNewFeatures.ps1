[CmdletBinding()]
param(
    [ValidateSet("BufferDoctor", "SmartTypeMapper", "OpenVariable", "AutomaticVisionInspector", "AutomaticCollections", "MultiLibraryHybrid", "ReleaseAnnouncement")]
    [string]$Scenario = "BufferDoctor",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$VisualStudioInstanceId = "",
    [string]$OutputRoot = "",
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedReleaseVersion = "1.0.52",
    [switch]$NoBuild,
    [switch]$NoInstall,
    [switch]$KeepVisualStudio,
    [ValidateSet("Any", "Enabled", "Disabled")]
    [string]$ExpectedInitialAutoInspectPreference = "Any",
    [ValidateSet("Unchanged", "Enabled", "Disabled")]
    [string]$SetAutoInspectPreference = "Unchanged"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$defaultOutputRoot = Join-Path $repoRoot "artifacts\ui\installed-vsix-new-features"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $testDrive = Get-PSDrive -Name D -ErrorAction SilentlyContinue
    if ($testDrive) {
        $OutputRoot = "D:\OpenVisionLab-TestData\RawBufferVisualizer\installed-vsix-new-features"
    }
    else {
        $OutputRoot = $defaultOutputRoot
    }
}
$outputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$sessionPath = Join-Path $outputRoot "$Scenario-session.json"
$resultPath = Join-Path $outputRoot "$Scenario-installed-vsix.json"
$failureScreenshotPath = Join-Path $outputRoot "$Scenario-failure.png"
$activityLogPath = Join-Path $outputRoot "$Scenario-activity-log.xml"
$userMappingPath = Join-Path $env:APPDATA "RawBufferVisualizer\type-mappings.json"
$userMappingBackupPath = Join-Path $outputRoot ("SmartTypeMapper-user-mapping-backup-" + $PID + ".json")
$automaticPreferencePath = Join-Path $env:APPDATA "RawBufferVisualizer\automatic-inspector-settings.json"
$automaticPreferenceBackupPath = Join-Path $outputRoot ("AutomaticInspector-preference-backup-" + $PID + ".json")
$releaseAnnouncementPreferencePath = Join-Path $env:APPDATA "RawBufferVisualizer\release-announcement-settings.json"
$releaseAnnouncementPreferenceBackupPath = Join-Path $outputRoot ("ReleaseAnnouncement-preference-backup-" + $PID + ".json")
$packageLogPath = Join-Path ([IO.Path]::GetTempPath()) "RawBufferVisualizer\VisualStudio\package.log"
Remove-Item -LiteralPath $sessionPath, $resultPath, $failureScreenshotPath, $activityLogPath -ErrorAction SilentlyContinue

function Assert-InteractiveDesktop {
    $sessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
    $lockScreen = Get-Process -Name LogonUI -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -eq $sessionId } |
        Select-Object -First 1
    if ($lockScreen) {
        throw "The Windows desktop is locked. Unlock the interactive session before running the installed VSIX UI smoke test."
    }
}

function Find-VisualStudioInstance {
    $vswhere = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
        (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $vswhere) {
        throw "vswhere.exe was not found."
    }

    $json = & $vswhere -all -format json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        throw "vswhere.exe failed."
    }

    $parsedInstances = ($json -join [Environment]::NewLine) | ConvertFrom-Json
    $instances = @($parsedInstances | ForEach-Object { $_ })
    if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstanceId)) {
        $selected = $instances | Where-Object { $_.instanceId -eq $VisualStudioInstanceId } | Select-Object -First 1
        if (-not $selected) {
            throw "Visual Studio instance was not found: $VisualStudioInstanceId"
        }

        return $selected
    }

    $selected2022 = $instances |
        Where-Object { $_.installationVersion -like "17.*" -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    $selected2026 = $instances |
        Where-Object { $_.installationVersion -like "18.*" -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    $selected = @($selected2022, $selected2026) |
        Where-Object { $null -ne $_ } |
        Select-Object -First 1
    if (-not $selected) {
        throw "A launchable Visual Studio 2022 or Visual Studio 2026 instance was not found."
    }

    $selected
}

function Wait-Until([string]$Description, [scriptblock]$Condition, [int]$TimeoutSeconds = 120, [int]$PollMilliseconds = 500) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null
    do {
        try {
            $value = & $Condition
            if ($null -ne $value -and $value -ne $false) {
                return $value
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($lastError) {
        throw "$Description timed out. Last error: $lastError"
    }

    throw "$Description timed out."
}

if (-not ("RawBufferInstalledVsixNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RawBufferInstalledVsixNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extraInfo);
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;
}
'@
}

if (-not ("RawBufferInstalledVsixRot" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class RawBufferInstalledVsixRot {
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);

    public static object GetDte(int processId) {
        IRunningObjectTable table;
        IBindCtx bindContext;
        if (GetRunningObjectTable(0, out table) != 0 || table == null ||
            CreateBindCtx(0, out bindContext) != 0 || bindContext == null) {
            return null;
        }

        IEnumMoniker enumerator;
        table.EnumRunning(out enumerator);
        IMoniker[] monikers = new IMoniker[1];
        while (enumerator.Next(1, monikers, IntPtr.Zero) == 0) {
            string displayName;
            try {
                monikers[0].GetDisplayName(bindContext, null, out displayName);
            }
            catch {
                continue;
            }

            if (displayName.IndexOf("VisualStudio.DTE.17.0:" + processId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("VisualStudio.DTE.18.0:" + processId, StringComparison.OrdinalIgnoreCase) >= 0) {
                object dte;
                table.GetObject(monikers[0], out dte);
                return dte;
            }
        }

        return null;
    }
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRawBufferInstalledVsixMessageFilter {
    [PreserveSig] int HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr interfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr callee, int tickCount, int rejectType);
    [PreserveSig] int MessagePending(IntPtr callee, int tickCount, int pendingType);
}

public sealed class RawBufferInstalledVsixMessageFilter : IRawBufferInstalledVsixMessageFilter, IDisposable {
    private IRawBufferInstalledVsixMessageFilter previous;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IRawBufferInstalledVsixMessageFilter next, out IRawBufferInstalledVsixMessageFilter previous);

    public static RawBufferInstalledVsixMessageFilter Register() {
        var filter = new RawBufferInstalledVsixMessageFilter();
        IRawBufferInstalledVsixMessageFilter previous;
        CoRegisterMessageFilter(filter, out previous);
        filter.previous = previous;
        return filter;
    }

    public void Dispose() {
        IRawBufferInstalledVsixMessageFilter ignored;
        CoRegisterMessageFilter(previous, out ignored);
    }

    public int HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr interfaceInfo) { return 0; }
    public int RetryRejectedCall(IntPtr callee, int tickCount, int rejectType) { return tickCount < 30000 ? 250 : -1; }
    public int MessagePending(IntPtr callee, int tickCount, int pendingType) { return 2; }
}
'@
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-LeftmostMonitor {
    $screens = @([Windows.Forms.Screen]::AllScreens)
    if ($screens.Count -eq 0) {
        throw "No interactive monitor was reported for the installed VSIX smoke test."
    }

    $screen = $screens |
        Sort-Object @{ Expression = { $_.Bounds.Left }; Ascending = $true },
                    @{ Expression = { $_.Bounds.Top }; Ascending = $true } |
        Select-Object -First 1

    [pscustomobject]@{
        DeviceName = [string]$screen.DeviceName
        Bounds = $screen.Bounds
        WorkingArea = $screen.WorkingArea
        IsPrimary = [bool]$screen.Primary
        IsSingleMonitorFallback = ($screens.Count -eq 1)
    }
}

$script:TestMonitor = Get-LeftmostMonitor
$script:LastWindowRect = $null

function Focus-Window([IntPtr]$Handle, [int]$Width = 1920, [int]$Height = 1040) {
    $bounds = $script:TestMonitor.Bounds
    $targetX = $bounds.Left + 20
    $targetY = $bounds.Top + 20
    $targetWidth = [Math]::Max(320, [Math]::Min($Width, $bounds.Width - 40))
    $targetHeight = [Math]::Max(240, [Math]::Min($Height, $bounds.Height - 40))

    [RawBufferInstalledVsixNative]::ShowWindow($Handle, 9) | Out-Null
    [RawBufferInstalledVsixNative]::SetWindowPos($Handle, [RawBufferInstalledVsixNative]::HWND_TOPMOST, $targetX, $targetY, $targetWidth, $targetHeight, 0x0040) | Out-Null
    [RawBufferInstalledVsixNative]::BringWindowToTop($Handle) | Out-Null
    [RawBufferInstalledVsixNative]::SetForegroundWindow($Handle) | Out-Null
    Start-Sleep -Milliseconds 250
    [RawBufferInstalledVsixNative]::SetWindowPos($Handle, [RawBufferInstalledVsixNative]::HWND_NOTOPMOST, $targetX, $targetY, $targetWidth, $targetHeight, 0x0040) | Out-Null

    $rect = New-Object RawBufferInstalledVsixNative+RECT
    if (-not [RawBufferInstalledVsixNative]::GetWindowRect($Handle, [ref]$rect)) {
        throw "Visual Studio window bounds could not be read after monitor placement."
    }

    $intersects = $rect.Right -gt $bounds.Left -and
        $rect.Left -lt $bounds.Right -and
        $rect.Bottom -gt $bounds.Top -and
        $rect.Top -lt $bounds.Bottom
    if (-not $intersects) {
        throw "Visual Studio window does not intersect the selected leftmost monitor $($script:TestMonitor.DeviceName)."
    }

    $script:LastWindowRect = [pscustomobject]@{
        Left = $rect.Left
        Top = $rect.Top
        Right = $rect.Right
        Bottom = $rect.Bottom
        Width = $rect.Right - $rect.Left
        Height = $rect.Bottom - $rect.Top
    }
}

function Capture-Window([IntPtr]$Handle, [string]$Path) {
    Focus-Window $Handle
    $rect = New-Object RawBufferInstalledVsixNative+RECT
    [RawBufferInstalledVsixNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid window bounds: $width x $height"
    }

    Capture-ScreenRegion -X $rect.Left -Y $rect.Top -Width $width -Height $height -Path $Path
}

function Capture-ScreenRegion([int]$X, [int]$Y, [int]$Width, [int]$Height, [string]$Path) {
    if ($Width -le 0 -or $Height -le 0) {
        throw "Invalid screen region: $Width x $Height"
    }

    $bitmap = New-Object Drawing.Bitmap $Width, $Height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($X, $Y, 0, 0, [Drawing.Size]::new($Width, $Height))
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Get-AutomationRoot([IntPtr]$Handle) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
    if (-not $root) {
        throw "Visual Studio UI Automation root was not found."
    }

    $root
}

function Find-ElementByAutomationId([System.Windows.Automation.AutomationElement]$Root, [string]$AutomationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-ElementsByControlType([System.Windows.Automation.AutomationElement]$Root, [System.Windows.Automation.ControlType]$ControlType) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        $ControlType)
    $collection = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $elements = @()
    for ($index = 0; $index -lt $collection.Count; $index++) {
        $elements += $collection.Item($index)
    }

    $elements
}

function Find-TreeItem([System.Windows.Automation.AutomationElement]$Root, [string]$Name) {
    foreach ($element in Get-ElementsByControlType $Root ([System.Windows.Automation.ControlType]::TreeItem)) {
        if ([string]::Equals($element.Current.Name, $Name, [StringComparison]::Ordinal)) {
            return $element
        }
    }

    $null
}

function Find-LocalsTreeItem(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$Name) {
    $item = Find-TreeItem $Root $Name
    if ($item -and -not [bool]$item.Current.IsOffscreen) {
        return $item
    }

    $anchor = Find-TreeItem $Root "caseNumber"
    if (-not $anchor) {
        $anchor = Get-ElementsByControlType $Root ([System.Windows.Automation.ControlType]::TreeItem) |
            Where-Object { -not [bool]$_.Current.IsOffscreen } |
            Select-Object -First 1
        if (-not $anchor) {
            return $null
        }
    }

    $current = $anchor
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    while ($current) {
        $scrollPattern = $null
        if ($current.TryGetCurrentPattern(
            [System.Windows.Automation.ScrollPattern]::Pattern,
            [ref]$scrollPattern)) {
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                try {
                    ([System.Windows.Automation.ScrollPattern]$scrollPattern).Scroll(
                        [System.Windows.Automation.ScrollAmount]::NoAmount,
                        [System.Windows.Automation.ScrollAmount]::LargeIncrement)
                }
                catch {
                    return $null
                }

                Start-Sleep -Milliseconds 150
                $item = Find-TreeItem $Root $Name
                if ($item -and -not [bool]$item.Current.IsOffscreen) {
                    return $item
                }
            }

            return $null
        }

        $current = $walker.GetParent($current)
    }

    $null
}

function Get-ImageListItems([System.Windows.Automation.AutomationElement]$Root) {
    $imageList = Find-ElementByAutomationId $Root "ImageList"
    if (-not $imageList) {
        return @()
    }

    Get-ElementsByControlType $imageList ([System.Windows.Automation.ControlType]::ListItem)
}

function Select-AutomationItem([System.Windows.Automation.AutomationElement]$Element) {
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        ([System.Windows.Automation.SelectionItemPattern]$pattern).Select()
        return
    }

    $rect = $Element.Current.BoundingRectangle
    [RawBufferInstalledVsixNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
    [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Select-ComboBoxItem(
    [System.Windows.Automation.AutomationElement]$ComboBox,
    [string]$ItemName) {
    $expandPattern = $null
    if (-not $ComboBox.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expandPattern)) {
        throw "Combo box '$($ComboBox.Current.AutomationId)' does not support ExpandCollapsePattern."
    }

    ([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Expand()
    $item = Wait-Until "combo-box item '$ItemName'" {
        Get-ElementsByControlType ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.ControlType]::ListItem) |
            Where-Object {
                [string]$_.Current.Name -eq $ItemName -and
                -not [bool]$_.Current.IsOffscreen
            } |
            Select-Object -First 1
    } 10

    Select-AutomationItem $item
    Start-Sleep -Milliseconds 250
    ([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Collapse()
}

function Click-VisualizerGlyph(
    [System.Windows.Automation.AutomationElement]$Root,
    [System.Windows.Automation.AutomationElement]$TreeItem) {
    $itemName = [string]$TreeItem.Current.Name
    Select-AutomationItem $TreeItem
    Start-Sleep -Milliseconds 250

    # VS 2026 can virtualize and reposition a Locals row after SelectionItem.Select().
    # Reacquire the live row before using its bounds so the glyph click cannot land on
    # a child or neighboring variable.
    $refreshedTreeItem = Find-LocalsTreeItem $Root $itemName
    if ($refreshedTreeItem) {
        $TreeItem = $refreshedTreeItem
    }

    $rect = $TreeItem.Current.BoundingRectangle
    if ($rect.Width -lt 80 -or $rect.Height -lt 8 -or [bool]$TreeItem.Current.IsOffscreen) {
        throw "Variable row has invalid bounds after selection: $($rect.Width) x $($rect.Height)"
    }

    $glyphOffset = if ($rect.Width -lt 900) { 180 } elseif ($rect.Width -lt 1300) { 230 } else { 280 }
    $hoverX = [int][Math]::Max($rect.Left + 20, $rect.Right - $glyphOffset)
    $hoverY = [int]($rect.Top + $rect.Height / 2)
    [RawBufferInstalledVsixNative]::SetCursorPos($hoverX, $hoverY) | Out-Null
    Start-Sleep -Milliseconds 200
    $logPath = Join-Path $outputRoot "click-visualizer-glyph.log"
    $descendants = $TreeItem.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $viewElement = $null
    $elementLog = @()
    for ($index = 0; $index -lt $descendants.Count; $index++) {
        $element = $descendants.Item($index)
        $elementLog += "type=$($element.Current.ControlType.ProgrammaticName) name=$($element.Current.Name) id=$($element.Current.AutomationId) rect=$($element.Current.BoundingRectangle)"
        if (-not $viewElement -and @("View", "보기") -contains [string]$element.Current.Name) {
            $viewElement = $element
        }
    }

    if (-not $viewElement) {
        $desktopElements = $Root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        for ($index = 0; $index -lt $desktopElements.Count; $index++) {
            $element = $desktopElements.Item($index)
            if (@("View", "보기") -notcontains [string]$element.Current.Name) {
                continue
            }

            $candidateRect = $element.Current.BoundingRectangle
            $intersectsRow = $candidateRect.Right -gt $rect.Left -and
                $candidateRect.Left -lt $rect.Right -and
                $candidateRect.Bottom -gt $rect.Top -and
                $candidateRect.Top -lt $rect.Bottom
            if ($intersectsRow) {
                $viewElement = $element
                $elementLog += "root-match type=$($element.Current.ControlType.ProgrammaticName) name=$($element.Current.Name) id=$($element.Current.AutomationId) rect=$candidateRect"
                break
            }
        }
    }

    if ($viewElement) {
        $viewRect = $viewElement.Current.BoundingRectangle
        $x = [int]($viewRect.Left + $viewRect.Width / 2)
        $y = [int]($viewRect.Top + $viewRect.Height / 2)
    }
    else {
        $x = $hoverX
        $y = [int]($rect.Top + $rect.Height / 2)
    }

    @(
        "name=$($TreeItem.Current.Name) rect=$($rect) x=$x y=$y viewElementFound=$($viewElement -ne $null)"
        $elementLog
    ) | Set-Content -LiteralPath $logPath -Encoding UTF8
    [RawBufferInstalledVsixNative]::SetCursorPos($x, $y) | Out-Null
    [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Dismiss-DebuggerEvaluationWarning {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $localizedOk = [string]([char]0xD655) + [string]([char]0xC778)
    $elements = $desktop.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    for ($index = 0; $index -lt $elements.Count; $index++) {
        $messageElement = $elements.Item($index)
        $name = [string]$messageElement.Current.Name
        if ($name.IndexOf("ClrCustomVisualizerDebuggeeHost", [StringComparison]::OrdinalIgnoreCase) -lt 0 -and
            $name.IndexOf("DebuggerVisualizers.DebuggeeSide", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            continue
        }

        $window = $messageElement
        while ($window -and $window.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
            $window = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($window)
        }

        if ($window) {
            foreach ($button in Get-ElementsByControlType $window ([System.Windows.Automation.ControlType]::Button)) {
                if (@("OK", $localizedOk) -contains [string]$button.Current.Name) {
                    $pattern = $null
                    if ($button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
                        ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
                    }
                    else {
                        $rect = $button.Current.BoundingRectangle
                        [RawBufferInstalledVsixNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
                        [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
                        [RawBufferInstalledVsixNative]::mouse_event([RawBufferInstalledVsixNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
                    }

                    Start-Sleep -Milliseconds 300
                    return $true
                }
            }
        }

        [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
        Start-Sleep -Milliseconds 300
        return $true
    }

    $false
}

function Invoke-Dte([int]$ProcessId, [scriptblock]$Action) {
    $dte = Wait-Until "Visual Studio DTE" { [RawBufferInstalledVsixRot]::GetDte($ProcessId) } 120
    $filter = [RawBufferInstalledVsixMessageFilter]::Register()
    try {
        & $Action $dte
    }
    finally {
        $filter.Dispose()
    }
}

function Show-RawBufferToolWindow([int]$ProcessId) {
    Invoke-Dte $ProcessId {
        param($dte)
        $commands = $dte.GetType().InvokeMember("Commands", [Reflection.BindingFlags]::GetProperty, $null, $dte, @())
        $commands.GetType().InvokeMember(
            "Raise",
            [Reflection.BindingFlags]::InvokeMethod,
            $null,
            $commands,
            @("{8e7bc2db-12a4-4f45-8f5a-38c1846a0f26}", 0x0100, $null, $null)) | Out-Null
    }
}

function Assert-RawBufferViewMenuContract(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    Focus-Window $MainHandle
    $root = Get-AutomationRoot $MainHandle
    $topLevelMenuItems = Get-ElementsByControlType $root ([System.Windows.Automation.ControlType]::MenuItem)
    $viewMenu = $topLevelMenuItems |
        Where-Object {
            $name = [string]$_.Current.Name
            $name -eq "View" -or
            $name -like "View(*" -or
            $name -eq ([string]([char]0xBCF4) + [string]([char]0xAE30)) -or
            $name -like (([string]([char]0xBCF4) + [string]([char]0xAE30)) + "(*")
        } |
        Select-Object -First 1
    if (-not $viewMenu) {
        throw "Visual Studio View menu was not found."
    }

    $expandPattern = $null
    if (-not $viewMenu.TryGetCurrentPattern(
        [System.Windows.Automation.ExpandCollapsePattern]::Pattern,
        [ref]$expandPattern)) {
        throw "Visual Studio View menu does not support UI Automation expansion."
    }

    ([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Expand()
    try {
        $menuItems = Wait-Until "Raw Buffer Visualizer View menu entries" {
            try {
                if (([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Current.ExpandCollapseState -ne
                    [System.Windows.Automation.ExpandCollapseState]::Expanded) {
                    ([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Expand()
                    Start-Sleep -Milliseconds 150
                }
            }
            catch {
                return $null
            }

            $desktop = [System.Windows.Automation.AutomationElement]::RootElement
            $viewBounds = $viewMenu.Current.BoundingRectangle
            $menuItemTypeCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::MenuItem)
            $openNameCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                "Raw Buffer Visualizer")
            $scanNameCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                "Raw Buffer Visualizer: Scan Current Frame")
            $nameCondition = [System.Windows.Automation.OrCondition]::new(
                [System.Windows.Automation.Condition[]]@(
                    $openNameCondition,
                    $scanNameCondition))
            $matchingCondition = [System.Windows.Automation.AndCondition]::new(
                [System.Windows.Automation.Condition[]]@(
                    $menuItemTypeCondition,
                    $nameCondition))
            $matchingCollection = $desktop.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                $matchingCondition)
            $visibleItems = @()
            for ($index = 0; $index -lt $matchingCollection.Count; $index++) {
                $item = $matchingCollection.Item($index)
                if (
                    $item.Current.ProcessId -eq $Process.Id -and
                    -not [bool]$item.Current.IsOffscreen -and
                    $item.Current.BoundingRectangle.Left -ge ($viewBounds.Left - 64) -and
                    $item.Current.BoundingRectangle.Left -le ($viewBounds.Left + 640)) {
                    $visibleItems += $item
                }
            }
            if ($visibleItems.Count -gt 0) { $visibleItems } else { $null }
        } 45

        $menuItems = @($menuItems |
            Group-Object {
                $bounds = $_.Current.BoundingRectangle
                "{0}|{1}|{2}|{3}|{4}" -f
                    [string]$_.Current.Name,
                    [Math]::Round($bounds.Left, 1),
                    [Math]::Round($bounds.Top, 1),
                    [Math]::Round($bounds.Width, 1),
                    [Math]::Round($bounds.Height, 1)
            } |
            ForEach-Object { $_.Group[0] })
        $openCount = @($menuItems |
            Where-Object { [string]$_.Current.Name -eq "Raw Buffer Visualizer" }).Count
        $scanCount = @($menuItems |
            Where-Object { [string]$_.Current.Name -eq "Raw Buffer Visualizer: Scan Current Frame" }).Count
        if ($openCount -ne 1 -or $scanCount -ne 1) {
            $menuItems |
                ForEach-Object {
                    "name=$($_.Current.Name) id=$($_.Current.AutomationId) rect=$($_.Current.BoundingRectangle) offscreen=$($_.Current.IsOffscreen)"
                } |
                Set-Content -LiteralPath (Join-Path $outputRoot "ViewMenu-contract-failure.log") -Encoding UTF8
            Capture-Window $MainHandle (Join-Path $outputRoot "ViewMenu-contract-failure.png")
            throw "View menu contract failed: expected one open command and one scan command; found open=$openCount, scan=$scanCount."
        }

        $menuScreenshotPath = Join-Path $outputRoot "$Scenario-view-menu-after.png"
        Capture-Window $MainHandle $menuScreenshotPath

        [pscustomobject]@{
            openCommandCount = $openCount
            scanCommandCount = $scanCount
            screenshotPath = $menuScreenshotPath
        }
    }
    finally {
        try {
            ([System.Windows.Automation.ExpandCollapsePattern]$expandPattern).Collapse()
        }
        catch {
            [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
        }
    }
}

function Find-RawBufferToolWindowElement([IntPtr]$MainHandle) {
    $root = Get-AutomationRoot $MainHandle
    $elements = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    for ($i = 0; $i -lt $elements.Count; $i++) {
        $e = $elements.Item($i)
        if ($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Pane) {
            $children = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
            for ($j = 0; $j -lt $children.Count; $j++) {
                $c = $children.Item($j)
                if ([string]$c.Current.Name -eq "Raw Buffer Visualizer") {
                    return $e
                }
            }
        }
    }

    return $null
}

function Show-LocalsWindow([int]$ProcessId) {
    Invoke-Dte $ProcessId {
        param($dte)
        $dte.ExecuteCommand("Debug.Locals")
    }
}

function Start-Debugging([int]$ProcessId) {
    Invoke-Dte $ProcessId {
        param($dte)
        $dte.ExecuteCommand("Debug.Start")
    }
}

function Stop-Debugging([int]$ProcessId) {
    Invoke-Dte $ProcessId {
        param($dte)
        $dte.ExecuteCommand("Debug.StopDebugging")
    }
}

function Close-DebugSolutionWithoutSaving([int]$ProcessId) {
    Invoke-Dte $ProcessId {
        param($dte)
        $dte.Solution.Close($false)
    }
}

function Invoke-BufferDoctorScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    $treeItem = Wait-Until "badStrideSnapshot in Locals" { Find-TreeItem (Get-AutomationRoot $MainHandle) "badStrideSnapshot" } 60
    Click-VisualizerGlyph (Get-AutomationRoot $MainHandle) $treeItem
    Start-Sleep -Milliseconds 500
    Dismiss-DebuggerEvaluationWarning | Out-Null

    $toolRoot = Wait-Until "Raw Buffer Visualizer tool window element" { Find-RawBufferToolWindowElement $MainHandle } 30
    if (-not $toolRoot) {
        throw "Raw Buffer Visualizer tool window element was not found."
    }

    # Wait for the tool window to finish loading an image before looking for tabs.
    Wait-Until "Raw Buffer Visualizer loaded" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        @(Get-ImageListItems $toolRoot).Count -gt 0
    } 30 | Out-Null

    # Prefer the compact inspector Interpret tab when the tool window is narrow.
    $interpretTab = Wait-Until "Interpret tab" {
        Get-ElementsByControlType $toolRoot ([System.Windows.Automation.ControlType]::TabItem) |
            Where-Object { [string]$_.Current.Name -eq "Interpret" } |
            Select-Object -First 1
    } 30
    if ($interpretTab) {
        Select-AutomationItem $interpretTab
        Start-Sleep -Milliseconds 300
    }

    $diagnoseButton = Wait-Until "Diagnose Buffer button" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        $byId = Find-ElementByAutomationId $toolRoot "CompactDiagnoseBufferButton"
        if ($byId) { return $byId }
        $byId = Find-ElementByAutomationId $toolRoot "DiagnoseBufferButton"
        if ($byId) { return $byId }
        Get-ElementsByControlType $toolRoot ([System.Windows.Automation.ControlType]::Button) |
            Where-Object { [string]$_.Current.Name -eq "Diagnose" -or [string]$_.Current.Name -eq "Diagnose Buffer" } |
            Select-Object -First 1
    } 30
    if (-not $diagnoseButton) {
        throw "Diagnose Buffer button was not found by automation id or name."
    }
    $pattern = $null
    if (-not $diagnoseButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        throw "Diagnose Buffer button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()

    function Capture-ToolWindow([string]$Path) {
        $rect = $toolRoot.Current.BoundingRectangle
        if ($rect.Width -le 0 -or $rect.Height -le 0) {
            throw "Invalid tool window bounds for screen capture."
        }
        Capture-ScreenRegion -X $rect.Left -Y $rect.Top -Width $rect.Width -Height $rect.Height -Path $Path
    }

    $beforeCandidatesPath = Join-Path $outputRoot "buffer-doctor-before-candidates.png"
    Start-Sleep -Milliseconds 1500
    Dismiss-DebuggerEvaluationWarning | Out-Null
    Capture-ToolWindow $beforeCandidatesPath

    $candidateList = Wait-Until "Diagnosis candidate list" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        $byId = Find-ElementByAutomationId $toolRoot "CompactDiagnosisCandidateList"
        if ($byId) { return $byId }
        Find-ElementByAutomationId $toolRoot "DiagnosisCandidateList"
    } 30
    $items = Get-ElementsByControlType $candidateList ([System.Windows.Automation.ControlType]::ListItem)
    if ($items.Count -eq 0) {
        throw "No buffer diagnosis candidates were generated."
    }

    Select-AutomationItem $items[0]
    Start-Sleep -Milliseconds 1500
    Dismiss-DebuggerEvaluationWarning | Out-Null

    $afterApplyPath = Join-Path $outputRoot "buffer-doctor-after-apply.png"
    Capture-ToolWindow $afterApplyPath

    $imageView = Find-ElementByAutomationId $toolRoot "RawBufferOpenGlImageView"
    $bounds = $imageView.Current.BoundingRectangle
    $centerX = [int]($bounds.Left + $bounds.Width / 2)
    $centerY = [int]($bounds.Top + $bounds.Height / 2)
    [RawBufferInstalledVsixNative]::SetCursorPos($centerX, $centerY) | Out-Null
    Start-Sleep -Milliseconds 200

    $pixelText = ""
    $pixelElement = Find-ElementByAutomationId $toolRoot "PixelValueText"
    if ($pixelElement) {
        $pixelText = [string]$pixelElement.Current.Name
    }

    [ordered]@{
        scenario = "BufferDoctor"
        beforeCandidatesScreenshotPath = $beforeCandidatesPath
        afterApplyScreenshotPath = $afterApplyPath
        candidateCount = $items.Count
        firstCandidateName = [string]$items[0].Current.Name
        pixelValue = $pixelText
    }
}

function Invoke-SmartTypeMapperScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    $sourceType = "RawBufferVisualizer.VisualizerDebuggee.UnmappedCompanyFrame"
    Show-RawBufferToolWindow $Process.Id
    $toolRoot = Wait-Until "Raw Buffer Visualizer tool window after break" {
        Find-RawBufferToolWindowElement $MainHandle
    } 30
    Focus-Window $MainHandle

    $beforeState = Wait-Until "ambiguous automatic mapping candidate" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Encoding UTF8 -Raw | ConvertFrom-Json
            $matches = @($state.documents | Where-Object { [string]$_.sourceType -eq $sourceType })
            if ($matches.Count -eq 1 -and
                [bool]$matches[0].isError -and
                [string]$matches[0].errorType -eq "MappingRequired" -and
                [string]$matches[0].errorMessage -like "Pixel format needs one explicit mapping*") {
                return $state
            }
        }
        catch {
        }

        $null
    } 45

    $candidateDocument = @($beforeState.documents | Where-Object { [string]$_.sourceType -eq $sourceType })[0]
    $beforeMapPath = Join-Path $outputRoot "smart-type-mapper-automatic-before-map.png"
    Capture-Window $MainHandle $beforeMapPath

    $editMappingButton = Wait-Until "automatic candidate Map button" {
        Get-ElementsByControlType (Get-AutomationRoot $MainHandle) ([System.Windows.Automation.ControlType]::Button) |
            Where-Object {
                [string]$_.Current.Name -in @("Map this detected image type", "Map") -and
                -not [bool]$_.Current.IsOffscreen
            } |
            Select-Object -First 1
    } 30
    $invokePattern = $null
    if (-not $editMappingButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        throw "Edit Mapping button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    $dialog = Wait-Until "Confirm Pixel Format dialog" {
        $desktop = [System.Windows.Automation.AutomationElement]::RootElement
        Get-ElementsByControlType $desktop ([System.Windows.Automation.ControlType]::Window) |
            Where-Object { [string]$_.Current.Name -eq "Confirm Pixel Format" } |
            Select-Object -First 1
    } 30

    $mono12MappingBox = Find-ElementByAutomationId $dialog "PixelFormatMapping_Mono12"
    if (-not $mono12MappingBox) {
        throw "Mono12 pixel-format mapping combo box was not found."
    }
    Select-ComboBoxItem $mono12MappingBox "Mono12PackedLsb"

    $previewButton = Find-ElementByAutomationId $dialog "PreviewMappingButton"
    if (-not $previewButton) {
        throw "Preview button was not found in the mapping dialog."
    }
    $invokePattern = $null
    if (-not $previewButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        throw "Preview button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    $previewStatus = Wait-Until "live mapping preview" {
        $statusElement = Find-ElementByAutomationId $dialog "MappingPreviewStatusText"
        if ($statusElement -and [string]$statusElement.Current.Name -eq "Preview rendered from live debuggee memory.") {
            return [string]$statusElement.Current.Name
        }

        $null
    } 30

    $dialogPath = Join-Path $outputRoot "smart-type-mapper-dialog-preview.png"
    Capture-Window $dialog.Current.NativeWindowHandle $dialogPath

    $saveButton = Find-ElementByAutomationId $dialog "SaveMappingButton"
    if (-not $saveButton) {
        throw "Save for This Type button was not found in the mapping dialog."
    }

    $invokePattern = $null
    if (-not $saveButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        throw "Save button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    $afterState = Wait-Until "mapped type automatic reopen" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Encoding UTF8 -Raw | ConvertFrom-Json
            $matches = @($state.documents | Where-Object { [string]$_.sourceType -eq $sourceType })
            if ($matches.Count -eq 1 -and
                -not [bool]$matches[0].isError -and
                [string]$matches[0].pixelFormat -eq "Mono12PackedLsb" -and
                [int]$matches[0].width -eq 640 -and
                [int]$matches[0].height -eq 484 -and
                [int]$matches[0].stride -eq 960 -and
                [string]$matches[0].sourceMode -eq "live" -and
                [bool]$matches[0].hasThumbnail) {
                return $state
            }
        }
        catch {
        }

        $null
    } 45

    if (-not (Test-Path -LiteralPath $userMappingPath)) {
        throw "The user mapping file was not written."
    }
    $savedMapping = Get-Content -LiteralPath $userMappingPath -Encoding UTF8 -Raw
    foreach ($expected in @("UnmappedCompanyFrame", "ImageAddress", "SizeX", "LinePitch", "PixelType", "Mono12PackedLsb")) {
        if (-not $savedMapping.Contains($expected)) {
            throw "Saved mapping file is missing '$expected'."
        }
    }

    $afterMapPath = Join-Path $outputRoot "smart-type-mapper-automatic-after-reopen.png"
    Capture-Window $MainHandle $afterMapPath

    $mappedDocument = @($afterState.documents | Where-Object { [string]$_.sourceType -eq $sourceType })[0]
    [ordered]@{
        scenario = "SmartTypeMapper"
        beforeMapScreenshotPath = $beforeMapPath
        dialogScreenshotPath = $dialogPath
        afterMapScreenshotPath = $afterMapPath
        sourceType = $sourceType
        candidateSummary = [string]$candidateDocument.summary
        selectedPixelFormat = "Mono12PackedLsb"
        previewStatus = $previewStatus
        mappedRowName = [string]$mappedDocument.title
        reopenedWidth = [int]$mappedDocument.width
        reopenedHeight = [int]$mappedDocument.height
        reopenedStride = [int]$mappedDocument.stride
        reopenedSourceMode = [string]$mappedDocument.sourceMode
        finalErrorCount = [int]$afterState.errorCount
    }
}

function Invoke-AutomaticVisionInspectorScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    $expectedSuccessfulSources = [ordered]@{
        companyFrame = "RawBufferVisualizer.VisualizerDebuggee.CompanyFrame"
        companyArrayFrame = "RawBufferVisualizer.VisualizerDebuggee.CompanyArrayFrame"
        nestedCompanyFrame = "RawBufferVisualizer.VisualizerDebuggee.NestedCompanyFrame"
        parameterFrame = "RawBufferVisualizer.VisualizerDebuggee.ParameterCompanyFrame"
    }
    $expectedSourceCounts = [ordered]@{
        "RawBufferVisualizer.VisualizerDebuggee.PinnedRawBufferView" = 2
        "RawBufferVisualizer.VisualizerDebuggee.CompanyFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.CompanyArrayFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.NestedCompanyFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.ParameterCompanyFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.IncompleteAutomaticFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.InvalidAutomaticFrame" = 1
    }

    Show-RawBufferToolWindow $Process.Id
    Start-Sleep -Milliseconds 750
    $autoInspectBox = Wait-Until "Automatic Vision Inspector preference check box" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "AutomaticVisionAutoInspectCheckBox"
    } 30
    $togglePattern = $null
    if (-not $autoInspectBox.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$togglePattern)) {
        throw "Automatic Vision Inspector preference check box does not support TogglePattern."
    }
    $initialAutoInspectEnabled =
        ([System.Windows.Automation.TogglePattern]$togglePattern).Current.ToggleState -eq
        [System.Windows.Automation.ToggleState]::On
    if ($ExpectedInitialAutoInspectPreference -ne "Any") {
        $expectedInitialEnabled = $ExpectedInitialAutoInspectPreference -eq "Enabled"
        if ($initialAutoInspectEnabled -ne $expectedInitialEnabled) {
            throw "Auto Inspect initial preference was $initialAutoInspectEnabled; expected $expectedInitialEnabled."
        }
    }

    $scanButton = Wait-Until "Automatic Vision Inspector Scan Now button" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "AutomaticVisionScanNowButton"
    } 30
    $invokePattern = $null
    if (-not $scanButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        throw "Automatic Vision Inspector Scan Now button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    $beforeState = Wait-Until "Automatic Vision Inspector session state" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            foreach ($sourceType in $expectedSuccessfulSources.Values) {
                $matches = @($state.documents | Where-Object { [string]$_.sourceType -eq $sourceType })
                if ($matches.Count -ne 1 -or [bool]$matches[0].isError) {
                    return $null
                }
            }

            if ([int]$state.documentCount -ne 8 -or [int]$state.errorCount -ne 2) {
                return $null
            }

            $mappingCandidate = @($state.documents | Where-Object {
                [string]$_.sourceType -eq "RawBufferVisualizer.VisualizerDebuggee.IncompleteAutomaticFrame"
            })[0]
            $openFailure = @($state.documents | Where-Object {
                [string]$_.sourceType -eq "RawBufferVisualizer.VisualizerDebuggee.InvalidAutomaticFrame"
            })[0]
            if (-not [bool]$mappingCandidate.isError -or
                -not [bool]$mappingCandidate.automaticMappingRequired -or
                -not [bool]$openFailure.isError -or
                [bool]$openFailure.automaticMappingRequired -or
                [string]$openFailure.errorType -ne "AutomaticOpenFailed") {
                return $null
            }

            if ([string]$state.automaticScanStatus -notmatch "8 detected: 6 opened, 1 need mapping, 1 failed" -or
                [bool]$state.errorPanelVisible) {
                return $null
            }

            $state
        }
        catch {
            $null
        }
    } 60

    $beforeNames = @($beforeState.documents | ForEach-Object { [string]$_.title })
    $arrayDocument = @($beforeState.documents | Where-Object {
        [string]$_.sourceType -eq $expectedSuccessfulSources.companyArrayFrame
    })[0]
    if ([string]$arrayDocument.sourceMode -ne "mem" -or
        [int]$arrayDocument.width -ne 64 -or
        [int]$arrayDocument.height -ne 48) {
        throw "Managed array did not auto-open as the expected 64x48 in-memory image."
    }

    $beforeRepeatedScanStamp = (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()
    Start-Sleep -Milliseconds 1000
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    $afterState = Wait-Until "Automatic Vision Inspector repeated-scan state" {
        if (-not (Test-Path -LiteralPath $sessionPath) -or
            (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc -le $beforeRepeatedScanStamp) {
            return $null
        }

        try {
            Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
        }
        catch {
            $null
        }
    } 30

    foreach ($entry in $expectedSourceCounts.GetEnumerator()) {
        $sourceType = [string]$entry.Key
        $expectedCount = [int]$entry.Value
        $matches = @($afterState.documents | Where-Object { [string]$_.sourceType -eq $sourceType })
        if ($matches.Count -ne $expectedCount) {
            throw "Repeated scans accumulated or lost $sourceType. Expected $expectedCount row(s), found $($matches.Count)."
        }
    }
    $afterNames = @($afterState.documents | ForEach-Object { [string]$_.title })

    $capturePath = Join-Path $outputRoot "automatic-vision-inspector.png"
    Capture-Window $MainHandle $capturePath

    $finalAutoInspectEnabled = $initialAutoInspectEnabled
    if ($SetAutoInspectPreference -ne "Unchanged") {
        $requestedAutoInspectEnabled = $SetAutoInspectPreference -eq "Enabled"
        if ($finalAutoInspectEnabled -ne $requestedAutoInspectEnabled) {
            ([System.Windows.Automation.TogglePattern]$togglePattern).Toggle()
            $updatedPreference = Wait-Until "Automatic Vision Inspector preference update" {
                $currentEnabled =
                    ([System.Windows.Automation.TogglePattern]$togglePattern).Current.ToggleState -eq
                    [System.Windows.Automation.ToggleState]::On
                if ($currentEnabled -eq $requestedAutoInspectEnabled) {
                    if ($currentEnabled) { "Enabled" } else { "Disabled" }
                }
                else {
                    $null
                }
            } 15
            $finalAutoInspectEnabled = $updatedPreference -eq "Enabled"
        }
    }

    [ordered]@{
        scenario = "AutomaticVisionInspector"
        screenshotPath = $capturePath
        rowsBeforeRepeatedScan = $beforeNames
        rowsAfterRepeatedScan = $afterNames
        managedArrayOpened = $true
        functionArgumentOpened = $true
        partialFailureIsolated = $true
        openedCount = 6
        mappingRequiredCount = 1
        failedCount = 1
        duplicateFree = $true
        initialAutoInspectEnabled = $initialAutoInspectEnabled
        finalAutoInspectEnabled = $finalAutoInspectEnabled
    }
}

function Invoke-AutomaticCollectionsScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    Show-RawBufferToolWindow $Process.Id
    Start-Sleep -Milliseconds 750
    $automationRoot = Get-AutomationRoot $MainHandle
    $collectionBox = Wait-Until "Automatic collection preference check box" {
        Find-ElementByAutomationId $automationRoot "AutomaticVisionIncludeCollectionsCheckBox"
    } 30
    $collectionTogglePattern = $null
    if (-not $collectionBox.TryGetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern,
        [ref]$collectionTogglePattern)) {
        throw "Automatic collection preference check box does not support TogglePattern."
    }
    if (([System.Windows.Automation.TogglePattern]$collectionTogglePattern).Current.ToggleState -ne
        [System.Windows.Automation.ToggleState]::On) {
        throw "Automatic collection inspection was not restored as enabled."
    }

    $scanButton = Wait-Until "Automatic collection Scan Now button" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "AutomaticVisionScanNowButton"
    } 30
    $invokePattern = $null
    if (-not $scanButton.TryGetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern,
        [ref]$invokePattern)) {
        throw "Automatic collection Scan Now button does not support InvokePattern."
    }

    $scanStopwatch = [Diagnostics.Stopwatch]::StartNew()
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()
    $firstState = Wait-Until "Automatic collection scan state" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            $openCvRows = @($state.documents | Where-Object {
                [string]$_.sourceType -eq "OpenCvSharp.Mat"
            })
            $emguRows = @($state.documents | Where-Object {
                [string]$_.sourceType -eq "Emgu.CV.Mat"
            })
            if ([int]$state.documentCount -ne 7 -or
                [int]$state.errorCount -ne 2 -or
                $openCvRows.Count -ne 5 -or
                $emguRows.Count -ne 2 -or
                @($openCvRows | Where-Object { -not [bool]$_.isError }).Count -ne 3 -or
                @($openCvRows | Where-Object { [bool]$_.isError }).Count -ne 2 -or
                @($emguRows | Where-Object { [bool]$_.isError }).Count -ne 0 -or
                -not [bool]$state.automaticCollectionsEnabled) {
                return $null
            }

            $status = [string]$state.automaticScanStatus
            if ($status -notmatch "7 detected: 5 opened, 0 need mapping, 2 failed" -or
                $status -notmatch "partialOpenCvMatList: 5 inspected, 3 opened, 2 failed" -or
                $status -notmatch "emguMatArray: 2 inspected, 2 opened") {
                return $null
            }

            $state
        }
        catch {
            $null
        }
    } 60
    $scanStopwatch.Stop()
    if ($scanStopwatch.Elapsed.TotalSeconds -gt 15) {
        throw "Automatic collection scan exceeded the 15 second installed-VSIX smoke budget."
    }

    $firstNames = @($firstState.documents | ForEach-Object { [string]$_.title })
    $firstSessionStamp = (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()
    $secondState = Wait-Until "Automatic collection duplicate-free rescan" {
        if (-not (Test-Path -LiteralPath $sessionPath) -or
            (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc -le $firstSessionStamp) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            if ([int]$state.documentCount -eq 7 -and
                [int]$state.errorCount -eq 2) {
                $state
            }
            else {
                $null
            }
        }
        catch {
            $null
        }
    } 30
    $secondNames = @($secondState.documents | ForEach-Object { [string]$_.title })

    $capturePath = Join-Path $outputRoot "automatic-collections.png"
    Capture-Window $MainHandle $capturePath

    [ordered]@{
        scenario = "AutomaticCollections"
        screenshotPath = $capturePath
        rowsBeforeRepeatedScan = $firstNames
        rowsAfterRepeatedScan = $secondNames
        listInspected = $true
        arrayInspected = $true
        openedCount = 5
        failedCount = 2
        partialFailureIsolated = $true
        duplicateFree = $true
        collectionPreferenceRestored = $true
        scanElapsedMilliseconds = [Math]::Round($scanStopwatch.Elapsed.TotalMilliseconds, 2)
    }
}

function Invoke-ReleaseAnnouncementScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    Show-RawBufferToolWindow $Process.Id
    Start-Sleep -Milliseconds 750
    $root = Get-AutomationRoot $MainHandle
    $title = Wait-Until "$ExpectedReleaseVersion release announcement title" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementTitle"
    } 30
    if (-not $title -or [string]$title.Current.Name -ne "New in Raw Buffer Visualizer $ExpectedReleaseVersion") {
        throw "The installed Tool Window did not expose the expected $ExpectedReleaseVersion release title."
    }

    $rowsBefore = @(Get-ImageListItems $root).Count
    $capturePath = Join-Path $outputRoot "release-announcement-installed.png"
    Capture-Window $MainHandle $capturePath

    $dismissButton = Find-ElementByAutomationId $root "ReleaseAnnouncementDismissButton"
    $dismissPattern = $null
    if (-not $dismissButton -or -not $dismissButton.TryGetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern,
        [ref]$dismissPattern)) {
        throw "The release announcement Dismiss button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$dismissPattern).Invoke()

    Wait-Until "persisted release announcement dismissal" {
        if (-not (Test-Path -LiteralPath $releaseAnnouncementPreferencePath)) {
            return $null
        }
        try {
            $saved = Get-Content -LiteralPath $releaseAnnouncementPreferencePath -Raw | ConvertFrom-Json
            if ([string]$saved.lastSeenVersion -eq $ExpectedReleaseVersion -and
                -not (Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementTitle")) {
                return $saved
            }
        }
        catch {
        }
        $null
    } 30 | Out-Null

    $openButton = Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementOpenButton"
    $togglePattern = $null
    if (-not $openButton -or -not $openButton.TryGetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern,
        [ref]$togglePattern)) {
        throw "The What's New button does not support TogglePattern."
    }
    $togglePattern = [System.Windows.Automation.TogglePattern]$togglePattern
    if ($togglePattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) {
        throw "The dismissed What's New toggle is not off."
    }
    $togglePattern.Toggle()
    Wait-Until "reopened release announcement" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementTitle"
    } 30 | Out-Null
    $togglePattern.Toggle()
    Wait-Until "closed release announcement from What's New" {
        $currentButton = Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementOpenButton"
        $currentPattern = $null
        if (-not $currentButton -or -not $currentButton.TryGetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern,
            [ref]$currentPattern)) {
            return $null
        }
        if (-not (Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementTitle") -and
            ([System.Windows.Automation.TogglePattern]$currentPattern).Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) {
            return $true
        }
        $null
    } 30 | Out-Null
    $togglePattern.Toggle()
    Wait-Until "reopened release announcement after toggle close" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ReleaseAnnouncementTitle"
    } 30 | Out-Null

    $rowsAfter = @(Get-ImageListItems (Get-AutomationRoot $MainHandle)).Count
    if ($rowsAfter -ne $rowsBefore) {
        throw "Opening or dismissing release highlights changed the image list ($rowsBefore to $rowsAfter)."
    }

    [ordered]@{
        scenario = "ReleaseAnnouncement"
        screenshotPath = $capturePath
        title = [string]$title.Current.Name
        dismissalPersisted = $true
        reopenedFromWhatsNew = $true
        closedFromWhatsNew = $true
        imageRowsBefore = $rowsBefore
        imageRowsAfter = $rowsAfter
        inspectionSideEffectFree = ($rowsBefore -eq $rowsAfter)
    }
}

function Invoke-MultiLibraryHybridScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    $registeredTypes = [ordered]@{
        bitmap = "System.Drawing.Bitmap"
    }
    $automaticRegisteredTypes = @(
        "OpenCvSharp.Mat",
        "Emgu.CV.Mat"
    )
    $registeredAutomaticSkipTypes = @(
        "RawBufferVisualizer.Sdk.RawBufferSnapshot",
        "RawBufferVisualizer.Sdk.RawBufferView",
        "System.Drawing.Bitmap"
    )
    $registeredSkipTypes = $registeredAutomaticSkipTypes
    $automaticTypes = @(
        "OpenCvSharp.Mat",
        "Emgu.CV.Mat",
        "RawBufferVisualizer.VisualizerDebuggee.PinnedRawBufferView",
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedBaslerGrabResult",
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedFlirImagePtr",
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedVimbaFrame",
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedIdsPeakIcvImage"
    )
    $automaticTypeCounts = [ordered]@{
        "OpenCvSharp.Mat" = 1
        "Emgu.CV.Mat" = 1
        "RawBufferVisualizer.VisualizerDebuggee.PinnedRawBufferView" = 2
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedBaslerGrabResult" = 1
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedFlirImagePtr" = 1
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedVimbaFrame" = 1
        "RawBufferVisualizer.VisualizerDebuggee.SimulatedIdsPeakIcvImage" = 1
    }

    Show-LocalsWindow $Process.Id
    Show-RawBufferToolWindow $Process.Id
    Start-Sleep -Milliseconds 750

    $automaticBreakState = Wait-Until "automatic registered images opened on Break Mode" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            foreach ($entry in $automaticTypeCounts.GetEnumerator()) {
                $sourceType = [string]$entry.Key
                $expectedCount = [int]$entry.Value
                $matches = @($state.documents | Where-Object {
                    [string]$_.sourceType -eq $sourceType -and
                    [bool]$_.isAutomaticInspection -and
                    -not [bool]$_.isError
                })
                if ($matches.Count -ne $expectedCount) {
                    return $null
                }
            }

            if ([string]$state.automaticScanStatus -notmatch "8 detected: 8 opened, 0 need mapping, 0 failed") {
                return $null
            }

            $state
        }
        catch {
            $null
        }
    } 60

    foreach ($sourceType in $automaticRegisteredTypes) {
        $document = @($automaticBreakState.documents | Where-Object {
            [string]$_.sourceType -eq $sourceType
        })[0]
        if (-not [bool]$document.isAutomaticInspection -or
            [string]$document.sourceMode -ne "live" -or
            [int]$document.width -ne 640 -or
            [int]$document.height -ne 484 -or
            [int]$document.stride -ne 640 -or
            [string]$document.pixelFormat -ne "Mono8") {
            throw "$sourceType was not automatically opened as the expected initialized 640x484 live Mono8 image."
        }
    }

    $scanButton = Wait-Until "Automatic Vision Inspector Scan Now button" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "AutomaticVisionScanNowButton"
    } 30
    $invokePattern = $null
    if (-not $scanButton.TryGetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern,
        [ref]$invokePattern)) {
        throw "Automatic Vision Inspector Scan Now button does not support InvokePattern."
    }
    $beforeManualScanStamp = (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    Wait-Until "Scan Now automatic registered-image refresh" {
        if (-not (Test-Path -LiteralPath $sessionPath) -or
            (Get-Item -LiteralPath $sessionPath).LastWriteTimeUtc -le $beforeManualScanStamp) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            foreach ($entry in $automaticTypeCounts.GetEnumerator()) {
                $sourceType = [string]$entry.Key
                $expectedCount = [int]$entry.Value
                $matches = @($state.documents | Where-Object {
                    [string]$_.sourceType -eq $sourceType -and -not [bool]$_.isError
                })
                if ($matches.Count -ne $expectedCount) {
                    return $null
                }
            }

            $state
        }
        catch {
            $null
        }
    } 60 | Out-Null

    foreach ($entry in $registeredTypes.GetEnumerator()) {
        $variableName = [string]$entry.Key
        $sourceType = [string]$entry.Value
        $treeItem = Wait-Until "$variableName Locals row" {
            Find-LocalsTreeItem (Get-AutomationRoot $MainHandle) $variableName
        } 60
        Click-VisualizerGlyph (Get-AutomationRoot $MainHandle) $treeItem

        Wait-Until "$sourceType registered visualizer handoff" {
            Dismiss-DebuggerEvaluationWarning | Out-Null
            if (-not (Test-Path -LiteralPath $sessionPath)) {
                return $null
            }

            try {
                $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
                $matches = @($state.documents | Where-Object {
                    [string]$_.sourceType -eq $sourceType -and -not [bool]$_.isError
                })
                if ($matches.Count -eq 1) { $state } else { $null }
            }
            catch {
                $null
            }
        } 60 | Out-Null
    }

    $finalState = Wait-Until "hybrid registered and automatic image session" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        try {
            $state = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            foreach ($sourceType in $registeredTypes.Values) {
                $matches = @($state.documents | Where-Object {
                    [string]$_.sourceType -eq $sourceType -and -not [bool]$_.isError
                })
                if ($matches.Count -ne 1) {
                    return $null
                }
            }
            foreach ($entry in $automaticTypeCounts.GetEnumerator()) {
                $sourceType = [string]$entry.Key
                $expectedCount = [int]$entry.Value
                $matches = @($state.documents | Where-Object {
                    [string]$_.sourceType -eq $sourceType -and -not [bool]$_.isError
                })
                if ($matches.Count -ne $expectedCount) {
                    return $null
                }
            }

            $registeredAutomaticRows = @($state.documents | Where-Object {
                [bool]$_.isAutomaticInspection -and
                $registeredSkipTypes -contains [string]$_.sourceType
            })
            if ($registeredAutomaticRows.Count -ne 0) {
                return $null
            }
            if ([int]$state.documentCount -ne 9 -or
                [int]$state.errorCount -ne 0 -or
                [string]$state.automaticScanStatus -notmatch "8 detected: 8 opened, 0 need mapping, 0 failed") {
                return $null
            }

            $state
        }
        catch {
            $null
        }
    } 60

    $capturePath = Join-Path $outputRoot "multi-library-hybrid.png"
    Capture-Window $MainHandle $capturePath

    [ordered]@{
        scenario = "MultiLibraryHybrid"
        screenshotPath = $capturePath
        registeredVisualizerTypes = @($registeredTypes.Values)
        registeredTypesOpenedByAutomaticScan = $automaticRegisteredTypes
        registeredAutomaticSkipTypes = $registeredAutomaticSkipTypes
        automaticInspectorTypes = $automaticTypes
        registeredTypesSkippedByAutomaticScan = $registeredAutomaticSkipTypes
        documentCount = [int]$finalState.documentCount
        errorCount = [int]$finalState.errorCount
        automaticScanStatus = [string]$finalState.automaticScanStatus
    }
}

function Invoke-OpenVariableScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {
    $treeItem = Wait-Until "companyFrameList in Locals" { Find-TreeItem (Get-AutomationRoot $MainHandle) "companyFrameList" } 60
    Click-VisualizerGlyph (Get-AutomationRoot $MainHandle) $treeItem
    Start-Sleep -Milliseconds 500
    Dismiss-DebuggerEvaluationWarning | Out-Null

    # Wait for the docked window to load the error row.
    Wait-Until "unsupported type error row" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        $items = @(Get-ImageListItems (Get-AutomationRoot $MainHandle))
        ($items | Where-Object { [string]$_.Current.Name -like "*CompanyFrame*" } | Select-Object -First 1) -ne $null
    } 30 | Out-Null

    # Open the context menu on the image list and select Open Variable.
    $imageList = Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "ImageList"
    Select-AutomationItem $imageList
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("+{F10}")
    Start-Sleep -Milliseconds 500

    $openVariableItem = $null
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $openVariableItem = Get-ElementsByControlType $desktop ([System.Windows.Automation.ControlType]::MenuItem) |
        Where-Object { [string]$_.Current.Name -eq "Open Variable..." } |
        Select-Object -First 1

    if (-not $openVariableItem) {
        throw "Open Variable... menu item was not found."
    }

    $pattern = $null
    if (-not $openVariableItem.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        throw "Open Variable menu item does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()

    $dialog = Wait-Until "Open Variable dialog" {
        $desktop = [System.Windows.Automation.AutomationElement]::RootElement
        Get-ElementsByControlType $desktop ([System.Windows.Automation.ControlType]::Window) |
            Where-Object { [string]$_.Current.Name -eq "Open Variable" } |
            Select-Object -First 1
    } 30

    $expressionBox = Find-ElementByAutomationId $dialog "OpenVariableExpressionBox"
    if (-not $expressionBox) {
        $expressionBox = (Get-ElementsByControlType $dialog ([System.Windows.Automation.ControlType]::Edit) | Select-Object -First 1)
    }

    if (-not $expressionBox) {
        throw "Expression box was not found in Open Variable dialog."
    }

    $expressionBox.SetFocus() | Out-Null
    [System.Windows.Forms.SendKeys]::SendWait("companyFrame")
    Start-Sleep -Milliseconds 200

    $dialogPath = Join-Path $outputRoot "open-variable-dialog.png"
    Capture-Window $dialog.Current.NativeWindowHandle $dialogPath

    $openButton = Find-ElementByAutomationId $dialog "OpenVariableOpenButton"
    if (-not $openButton) {
        $openButton = (Get-ElementsByControlType $dialog ([System.Windows.Automation.ControlType]::Button) |
            Where-Object { [string]$_.Current.Name -eq "Open" } |
            Select-Object -First 1)
    }

    if (-not $openButton) {
        throw "Open button was not found in Open Variable dialog."
    }

    $pattern = $null
    if (-not $openButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        throw "Open button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()

    Start-Sleep -Milliseconds 1000
    Dismiss-DebuggerEvaluationWarning | Out-Null

    $afterOpenPath = Join-Path $outputRoot "open-variable-after-open.png"
    Capture-Window $MainHandle $afterOpenPath

    [ordered]@{
        scenario = "OpenVariable"
        dialogScreenshotPath = $dialogPath
        afterOpenScreenshotPath = $afterOpenPath
    }
}

Assert-InteractiveDesktop
$executionState = [RawBufferInstalledVsixNative]::ES_CONTINUOUS -bor
    [RawBufferInstalledVsixNative]::ES_SYSTEM_REQUIRED -bor
    [RawBufferInstalledVsixNative]::ES_DISPLAY_REQUIRED
$vsInstance = Find-VisualStudioInstance
$devenvCandidate = [string]$vsInstance.productPath
if ([string]::IsNullOrWhiteSpace($devenvCandidate)) {
    $devenvCandidate = Join-Path ([string]$vsInstance.installationPath) "Common7\IDE\devenv.exe"
}
if (-not (Test-Path -LiteralPath $devenvCandidate)) {
    throw "Visual Studio executable was not found: $devenvCandidate"
}
$devenvPath = (Resolve-Path -LiteralPath $devenvCandidate).Path
$sampleProject = Join-Path $repoRoot "samples\RawBufferVisualizer.VisualizerDebuggee\RawBufferVisualizer.VisualizerDebuggee.csproj"
$debuggeePath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualizerDebuggee\Debug\net472\RawBufferVisualizer.VisualizerDebuggee.exe"

if (-not $NoInstall) {
    $installArguments = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $repoRoot "scripts\Install-VisualStudioExtension.ps1"),
        "-Configuration", $Configuration,
        "-Framework", "net472",
        "-ViewerFramework", "net472",
        "-VisualStudioInstanceId", [string]$vsInstance.instanceId,
        "-Reinstall"
    )
    if ($NoBuild) {
        $installArguments += "-NoBuild"
    }

    $install = Start-Process -FilePath "powershell" -ArgumentList $installArguments -Wait -PassThru -WindowStyle Hidden
    if ($install.ExitCode -ne 0) {
        throw "VSIX installation failed with exit code $($install.ExitCode)."
    }
}

if (-not $NoBuild) {
    & dotnet build $sampleProject -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "VisualizerDebuggee build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $debuggeePath)) {
    throw "VisualizerDebuggee was not found: $debuggeePath"
}
$debuggeePath = (Resolve-Path -LiteralPath $debuggeePath).Path

$testStartedUtc = [DateTime]::UtcNow
$packageLogStartLineCount = if (Test-Path -LiteralPath $packageLogPath) {
    @(Get-Content -LiteralPath $packageLogPath).Count
}
else {
    0
}
$visualStudio = $null
$mainHandle = [IntPtr]::Zero
$completed = $false
$executionStateActive = $false
$debuggingStopped = $false
$userMappingIsolated = $false
$userMappingExisted = $false
$menuContract = $null
$automaticPreferenceIsolated = $false
$automaticPreferenceExisted = $false
$releaseAnnouncementPreferenceIsolated = $false
$releaseAnnouncementPreferenceExisted = $false

$scenarioArgument = switch ($Scenario) {
    "BufferDoctor" { "--buffer-doctor-debug" }
    "SmartTypeMapper" { "--smart-type-mapper-fallback-debug" }
    "OpenVariable" { "--smart-type-mapper-debug" }
    "AutomaticVisionInspector" { "--smart-type-mapper-debug" }
    "AutomaticCollections" { "--automatic-collections-debug" }
    "MultiLibraryHybrid" { "--multi-library-debug" }
    "ReleaseAnnouncement" { "--buffer-doctor-debug" }
    default { "--buffer-doctor-debug" }
}

try {
    if ($Scenario -eq "SmartTypeMapper") {
        $userMappingIsolated = $true
        $userMappingExisted = Test-Path -LiteralPath $userMappingPath
        if ($userMappingExisted) {
            Copy-Item -LiteralPath $userMappingPath -Destination $userMappingBackupPath -Force
        }
        Remove-Item -LiteralPath $userMappingPath -Force -ErrorAction SilentlyContinue
    }
    if ($Scenario -eq "MultiLibraryHybrid" -or $Scenario -eq "AutomaticCollections") {
        $automaticPreferenceIsolated = $true
        $automaticPreferenceExisted = Test-Path -LiteralPath $automaticPreferencePath
        if ($automaticPreferenceExisted) {
            Copy-Item -LiteralPath $automaticPreferencePath -Destination $automaticPreferenceBackupPath -Force
        }

        $automaticPreferenceDirectory = Split-Path -Parent $automaticPreferencePath
        New-Item -ItemType Directory -Path $automaticPreferenceDirectory -Force | Out-Null
        $automaticPreferenceJson = [ordered]@{
            version = 1
            autoScanOnBreak = $true
            includeImageCollections = ($Scenario -eq "AutomaticCollections")
        } | ConvertTo-Json
        [IO.File]::WriteAllText(
            $automaticPreferencePath,
            $automaticPreferenceJson,
            (New-Object Text.UTF8Encoding($false)))
    }
    if ($Scenario -eq "ReleaseAnnouncement") {
        $releaseAnnouncementPreferenceIsolated = $true
        $releaseAnnouncementPreferenceExisted = Test-Path -LiteralPath $releaseAnnouncementPreferencePath
        if ($releaseAnnouncementPreferenceExisted) {
            Copy-Item -LiteralPath $releaseAnnouncementPreferencePath -Destination $releaseAnnouncementPreferenceBackupPath -Force
        }
        Remove-Item -LiteralPath $releaseAnnouncementPreferencePath -Force -ErrorAction SilentlyContinue
    }

    [RawBufferInstalledVsixNative]::SetThreadExecutionState($executionState) | Out-Null
    $executionStateActive = $true
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $devenvPath
    $psi.WorkingDirectory = Split-Path -Parent $debuggeePath
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables["RAWBUFFERVISUALIZER_DOCKED_SESSION_JSON"] = $sessionPath
    $psi.Arguments = @(
        "/NoSplash",
        "/Log", "`"$activityLogPath`"",
        "/debugexe", "`"$debuggeePath`"",
        $scenarioArgument
    ) -join " "
    $visualStudio = [Diagnostics.Process]::Start($psi)

    $mainHandle = Wait-Until "Visual Studio main window" {
        $visualStudio.Refresh()
        if ($visualStudio.HasExited) {
            throw "Visual Studio exited with code $($visualStudio.ExitCode)."
        }

        if ($visualStudio.MainWindowHandle -ne 0 -and $visualStudio.MainWindowTitle -match "VisualizerDebuggee") {
            $visualStudio.MainWindowHandle
        }
        else {
            $null
        }
    } 180

    Focus-Window $mainHandle 1920 1040
    $menuContract = Assert-RawBufferViewMenuContract $visualStudio $mainHandle
    Show-RawBufferToolWindow $visualStudio.Id
    Start-Sleep -Milliseconds 1500
    $toolElement = Wait-Until "Raw Buffer Visualizer tool window element" { Find-RawBufferToolWindowElement $mainHandle } 30
    if (-not $toolElement) {
        throw "Raw Buffer Visualizer tool window element was not found."
    }
    Start-Debugging $visualStudio.Id
    Show-LocalsWindow $visualStudio.Id

    Wait-Until "debuggee break" {
        $root = Get-AutomationRoot $mainHandle
        if ($Scenario -eq "BufferDoctor") {
            (Find-TreeItem $root "badStrideSnapshot") -ne $null
        }
        elseif ($Scenario -eq "SmartTypeMapper") {
            (Find-TreeItem $root "unmappedCompanyFrame") -ne $null
        }
        elseif ($Scenario -eq "MultiLibraryHybrid") {
            (Find-TreeItem $root "badStrideSnapshot") -ne $null
        }
        elseif ($Scenario -eq "AutomaticCollections") {
            (Find-TreeItem $root "partialOpenCvMatList") -ne $null
        }
        elseif ($Scenario -eq "ReleaseAnnouncement") {
            (Find-TreeItem $root "badStrideSnapshot") -ne $null
        }
        else {
            (Find-TreeItem $root "companyFrameList") -ne $null
        }
    } 90 | Out-Null

    $scenarioResult = switch ($Scenario) {
        "BufferDoctor" { Invoke-BufferDoctorScenario $visualStudio $mainHandle }
        "SmartTypeMapper" { Invoke-SmartTypeMapperScenario $visualStudio $mainHandle }
        "OpenVariable" { Invoke-OpenVariableScenario $visualStudio $mainHandle }
        "AutomaticVisionInspector" { Invoke-AutomaticVisionInspectorScenario $visualStudio $mainHandle }
        "AutomaticCollections" { Invoke-AutomaticCollectionsScenario $visualStudio $mainHandle }
        "MultiLibraryHybrid" { Invoke-MultiLibraryHybridScenario $visualStudio $mainHandle }
        "ReleaseAnnouncement" { Invoke-ReleaseAnnouncementScenario $visualStudio $mainHandle }
    }

    Stop-Debugging $visualStudio.Id
    $debuggingStopped = $true
    Start-Sleep -Milliseconds 250

    $newPackageLogLines = if (Test-Path -LiteralPath $packageLogPath) {
        @(Get-Content -LiteralPath $packageLogPath | Select-Object -Skip $packageLogStartLineCount)
    }
    else {
        @()
    }
    $packageProtocolErrors = @($newPackageLogLines | Where-Object {
        $_ -match "Claim error" -or
        $_ -match "Open error" -or
        $_ -match "Rejection marker publish failed" -or
        $_ -match "Automatic Inspector settings were ignored" -or
        $_ -match "Release announcement settings were ignored"
    })
    if ($packageProtocolErrors.Count -gt 0) {
        $packageProtocolErrors |
            Set-Content -LiteralPath (Join-Path $outputRoot "$Scenario-package-protocol-errors.log") -Encoding UTF8
        throw "Raw Buffer Visualizer package log contains $($packageProtocolErrors.Count) protocol or settings error(s) from this smoke session."
    }

    $result = [ordered]@{
        passed = $true
        startedUtc = $testStartedUtc.ToString("o")
        scenario = $Scenario
        visualStudio = [ordered]@{
            instanceId = [string]$vsInstance.instanceId
            version = [string]$vsInstance.installationVersion
            processId = $visualStudio.Id
        }
        monitor = [ordered]@{
            deviceName = $script:TestMonitor.DeviceName
            left = $script:TestMonitor.Bounds.Left
            top = $script:TestMonitor.Bounds.Top
            width = $script:TestMonitor.Bounds.Width
            height = $script:TestMonitor.Bounds.Height
            isPrimary = $script:TestMonitor.IsPrimary
            singleMonitorFallback = $script:TestMonitor.IsSingleMonitorFallback
            verifiedWindowRect = $script:LastWindowRect
        }
        result = $scenarioResult
        viewMenu = $menuContract
        packageLogNewLineCount = $newPackageLogLines.Count
        packageProtocolErrorCount = 0
        resultPath = $resultPath
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    $completed = $true
    Get-Content -LiteralPath $resultPath
}
catch {
    if ($mainHandle -ne [IntPtr]::Zero) {
        try { Capture-Window $mainHandle $failureScreenshotPath } catch { }
    }
    throw
}
finally {
    if (-not $KeepVisualStudio) {
        if ($visualStudio -and -not $visualStudio.HasExited) {
            if (-not $debuggingStopped) {
                try { Stop-Debugging $visualStudio.Id } catch { }
            }
            try { Close-DebugSolutionWithoutSaving $visualStudio.Id } catch { }
            try { $visualStudio.CloseMainWindow() | Out-Null } catch { }
            if (-not $visualStudio.WaitForExit(15000)) {
                Stop-Process -Id $visualStudio.Id -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($executionStateActive) {
        [RawBufferInstalledVsixNative]::SetThreadExecutionState([RawBufferInstalledVsixNative]::ES_CONTINUOUS) | Out-Null
    }

    if ($userMappingIsolated) {
        if ($userMappingExisted -and (Test-Path -LiteralPath $userMappingBackupPath)) {
            $mappingDirectory = Split-Path -Parent $userMappingPath
            New-Item -ItemType Directory -Path $mappingDirectory -Force | Out-Null
            Copy-Item -LiteralPath $userMappingBackupPath -Destination $userMappingPath -Force
            Remove-Item -LiteralPath $userMappingBackupPath -Force
        }
        else {
            Remove-Item -LiteralPath $userMappingPath -Force -ErrorAction SilentlyContinue
        }
    }
    if ($automaticPreferenceIsolated) {
        if ($automaticPreferenceExisted -and (Test-Path -LiteralPath $automaticPreferenceBackupPath)) {
            $automaticPreferenceDirectory = Split-Path -Parent $automaticPreferencePath
            New-Item -ItemType Directory -Path $automaticPreferenceDirectory -Force | Out-Null
            Copy-Item -LiteralPath $automaticPreferenceBackupPath -Destination $automaticPreferencePath -Force
            Remove-Item -LiteralPath $automaticPreferenceBackupPath -Force
        }
        else {
            Remove-Item -LiteralPath $automaticPreferencePath -Force -ErrorAction SilentlyContinue
        }
    }
    if ($releaseAnnouncementPreferenceIsolated) {
        if ($releaseAnnouncementPreferenceExisted -and (Test-Path -LiteralPath $releaseAnnouncementPreferenceBackupPath)) {
            $releaseAnnouncementPreferenceDirectory = Split-Path -Parent $releaseAnnouncementPreferencePath
            New-Item -ItemType Directory -Path $releaseAnnouncementPreferenceDirectory -Force | Out-Null
            Copy-Item -LiteralPath $releaseAnnouncementPreferenceBackupPath -Destination $releaseAnnouncementPreferencePath -Force
            Remove-Item -LiteralPath $releaseAnnouncementPreferenceBackupPath -Force
        }
        else {
            Remove-Item -LiteralPath $releaseAnnouncementPreferencePath -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not $completed) {
    throw "Installed VSIX new-features smoke did not complete."
}

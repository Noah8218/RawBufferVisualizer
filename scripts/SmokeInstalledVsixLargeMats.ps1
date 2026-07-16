[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$VisualStudioInstanceId = "",
    [int]$Width = 8192,
    [int]$Height = 8192,
    [string]$OutputDir = "artifacts\perf\installed-vsix-large-mats",
    [switch]$NoBuild,
    [switch]$NoInstall,
    [switch]$KeepVisualStudio
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$outputRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$readyPath = Join-Path $outputRoot "large-mats.ready"
$sessionPath = Join-Path $outputRoot "large-mats-session.json"
$resultPath = Join-Path $outputRoot "large-mats-installed-vsix.json"
$openCvScreenshotPath = Join-Path $outputRoot "large-opencv-installed-vsix.png"
$finalScreenshotPath = Join-Path $outputRoot "large-mats-installed-vsix.png"
$sourceUnavailableScreenshotPath = Join-Path $outputRoot "large-mats-source-unavailable.png"
$failureScreenshotPath = Join-Path $outputRoot "large-mats-installed-vsix-failure.png"
$activityLogPath = Join-Path $outputRoot "visual-studio-activity-log.xml"
Remove-Item -LiteralPath $readyPath, $sessionPath, $resultPath, $openCvScreenshotPath, $finalScreenshotPath, $sourceUnavailableScreenshotPath, $failureScreenshotPath, $activityLogPath -ErrorAction SilentlyContinue

if ($Width -le 0 -or $Height -le 0) {
    throw "Width and Height must be positive."
}

$expectedBytes = [int64]$Width * [int64]$Height
if ($expectedBytes -lt 64MB) {
    throw "The installed VSIX large-Mat smoke requires at least 64 MB. Requested payload: $expectedBytes bytes."
}

function Assert-NoVisualStudio {
    $running = @(Get-Process -Name devenv -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) {
        return
    }

    $details = ($running | ForEach-Object { "PID $($_.Id): $($_.MainWindowTitle)" }) -join "; "
    throw "Close Visual Studio before running this smoke test. Running instances: $details"
}

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

    $selected = $instances |
        Where-Object { $_.installationVersion -like "17.*" -and $_.isLaunchable -eq $true } |
        Select-Object -First 1
    if (-not $selected) {
        throw "A launchable Visual Studio 2022 instance was not found."
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

function Read-ReadyValues([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $separator = $line.IndexOf("=")
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }

    $values
}

function Get-TempFileSnapshot {
    $root = Join-Path $env:TEMP "RawBufferVisualizer"
    $files = @{}
    if (-not (Test-Path -LiteralPath $root)) {
        return $files
    }

    Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $files[$_.FullName] = [int64]$_.Length
    }
    $files
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

            if (displayName.IndexOf("VisualStudio.DTE.17.0:" + processId, StringComparison.OrdinalIgnoreCase) >= 0) {
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

function Focus-Window([IntPtr]$Handle) {
    [RawBufferInstalledVsixNative]::ShowWindow($Handle, 9) | Out-Null
    [RawBufferInstalledVsixNative]::SetWindowPos($Handle, [RawBufferInstalledVsixNative]::HWND_TOPMOST, 20, 20, 1500, 900, 0x0040) | Out-Null
    [RawBufferInstalledVsixNative]::BringWindowToTop($Handle) | Out-Null
    [RawBufferInstalledVsixNative]::SetForegroundWindow($Handle) | Out-Null
    Start-Sleep -Milliseconds 250
    [RawBufferInstalledVsixNative]::SetWindowPos($Handle, [RawBufferInstalledVsixNative]::HWND_NOTOPMOST, 20, 20, 1500, 900, 0x0040) | Out-Null
}

function Send-Keys([IntPtr]$Handle, [string]$Keys) {
    Focus-Window $Handle
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 500
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

    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height))
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

function Find-DesktopElementNameContaining([string]$Text) {
    $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    for ($index = 0; $index -lt $elements.Count; $index++) {
        $name = [string]$elements.Item($index).Current.Name
        if ($name.IndexOf($Text, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $name
        }
    }

    $null
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

function Click-VisualizerGlyph([System.Windows.Automation.AutomationElement]$TreeItem) {
    $rect = $TreeItem.Current.BoundingRectangle
    if ($rect.Width -lt 220 -or $rect.Height -lt 8) {
        throw "Variable row has invalid bounds: $($rect.Width) x $($rect.Height)"
    }

    Select-AutomationItem $TreeItem
    $x = [int][Math]::Max($rect.Left + 20, $rect.Right - 170)
    $y = [int]($rect.Top + $rect.Height / 2)
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

function Dismiss-VisualizerStatusWindow([Diagnostics.Process]$Process, [IntPtr]$MainHandle) {
    $Process.Refresh()
    if ($Process.MainWindowHandle -ne 0 -and $Process.MainWindowHandle -ne $MainHandle) {
        Send-Keys $Process.MainWindowHandle "{ESC}"
    }
}

function Invoke-LargeMatVisualizer(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle,
    [string]$VariableName,
    [string]$SourceType,
    [int]$ExpectedCount,
    [int]$ExpectedValue,
    [string]$ScreenshotPath) {
    $root = Get-AutomationRoot $MainHandle
    $treeItem = Wait-Until "$VariableName in Locals" { Find-TreeItem (Get-AutomationRoot $MainHandle) $VariableName } 60
    $helpText = [string]$treeItem.Current.HelpText
    if ([string]::IsNullOrWhiteSpace($helpText)) {
        throw "$VariableName does not advertise a debugger visualizer. HelpText is empty."
    }

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $loaded = $false
    for ($attempt = 1; $attempt -le 3 -and -not $loaded; $attempt++) {
        $treeItem = Find-TreeItem (Get-AutomationRoot $MainHandle) $VariableName
        Click-VisualizerGlyph $treeItem
        try {
            Wait-Until "$SourceType image list item" {
                Dismiss-DebuggerEvaluationWarning | Out-Null
                $items = @(Get-ImageListItems (Get-AutomationRoot $MainHandle))
                if ($items.Count -ge $ExpectedCount) { $items } else { $null }
            } 45 750 | Out-Null
            $loaded = $true
        }
        catch {
            Dismiss-DebuggerEvaluationWarning | Out-Null
            Dismiss-VisualizerStatusWindow $Process $MainHandle
            if ($attempt -eq 3) {
                throw
            }
        }
    }
    $stopwatch.Stop()

    Dismiss-DebuggerEvaluationWarning | Out-Null
    Dismiss-VisualizerStatusWindow $Process $MainHandle
    Focus-Window $MainHandle

    $root = Get-AutomationRoot $MainHandle
    $imageItems = @(Get-ImageListItems $root)
    $itemIndex = $ExpectedCount - 1
    if ($itemIndex -lt 0 -or $itemIndex -ge $imageItems.Count) {
        throw "$SourceType image list index $itemIndex is unavailable. Item count: $($imageItems.Count)"
    }
    $matchingItem = $imageItems[$itemIndex]

    Select-AutomationItem $matchingItem
    Start-Sleep -Milliseconds 500
    $imageView = Wait-Until "Raw Buffer image view" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "RawBufferOpenGlImageView"
    } 30
    $bounds = $imageView.Current.BoundingRectangle
    if ($bounds.Width -lt 20 -or $bounds.Height -lt 20) {
        throw "Raw Buffer image view has invalid bounds: $($bounds.Width) x $($bounds.Height)"
    }

    $centerX = [int]($bounds.Left + $bounds.Width / 2)
    $centerY = [int]($bounds.Top + $bounds.Height / 2)
    foreach ($offset in @(0, 4, -4, 8, -8, 2)) {
        [RawBufferInstalledVsixNative]::SetCursorPos($centerX + $offset, $centerY + $offset) | Out-Null
        Start-Sleep -Milliseconds 120
    }

    $hoverProbe = 0
    $pixelText = Wait-Until "$SourceType pixel value GV $ExpectedValue" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        $hoverProbe++
        $probeOffset = ($hoverProbe % 7) - 3
        [RawBufferInstalledVsixNative]::SetCursorPos($centerX + $probeOffset, $centerY - $probeOffset) | Out-Null
        $element = Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "PixelValueText"
        if ($element -and ([string]$element.Current.Name).IndexOf("GV $ExpectedValue", [StringComparison]::Ordinal) -ge 0) {
            [string]$element.Current.Name
        }
        else {
            $null
        }
    } 30 250

    Capture-Window $MainHandle $ScreenshotPath
    [ordered]@{
        variable = $VariableName
        sourceType = $SourceType
        helpText = $helpText
        loadMilliseconds = $stopwatch.ElapsedMilliseconds
        listItemIndex = $itemIndex
        listItemAutomationName = [string]$matchingItem.Current.Name
        pixelValue = $pixelText
        screenshotPath = $ScreenshotPath
    }
}

Assert-NoVisualStudio
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

$tempBefore = Get-TempFileSnapshot
$testStartedUtc = [DateTime]::UtcNow
$visualStudio = $null
$debuggeeProcessId = 0
$mainHandle = [IntPtr]::Zero
$completed = $false
$executionStateActive = $false
$debuggingStopped = $false

try {
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
        "--large-mat-debug",
        "--width", $Width,
        "--height", $Height,
        "--ready-file", "`"$readyPath`""
    ) -join " "
    $visualStudio = [Diagnostics.Process]::Start($psi)

    $mainHandle = Wait-Until "Visual Studio 2022 main window" {
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

    Focus-Window $mainHandle
    Show-RawBufferToolWindow $visualStudio.Id
    Start-Debugging $visualStudio.Id

    Wait-Until "large Mat debuggee ready file" {
        if (Test-Path -LiteralPath $readyPath) { $true } else { $null }
    } 180 | Out-Null
    $ready = Read-ReadyValues $readyPath
    $debuggeeProcessId = [int]$ready.processId
    if ([int64]$ready.openCvBytes -ne $expectedBytes -or [int64]$ready.emguBytes -ne $expectedBytes) {
        throw "Debuggee payload lengths do not match $expectedBytes bytes: OpenCv=$($ready.openCvBytes), Emgu=$($ready.emguBytes)."
    }

    Show-LocalsWindow $visualStudio.Id
    Wait-Until "large Mat Locals" {
        $root = Get-AutomationRoot $mainHandle
        if ((Find-TreeItem $root "largeOpenCvMat") -and (Find-TreeItem $root "largeEmguMat")) { $true } else { $null }
    } 90 | Out-Null

    $openCvResult = Invoke-LargeMatVisualizer $visualStudio $mainHandle "largeOpenCvMat" "OpenCvSharp.Mat" 1 37 $openCvScreenshotPath
    $emguResult = Invoke-LargeMatVisualizer $visualStudio $mainHandle "largeEmguMat" "Emgu.CV.Mat" 2 173 $finalScreenshotPath

    $session = Wait-Until "docked two-image session" {
        if (-not (Test-Path -LiteralPath $sessionPath)) {
            return $null
        }

        $parsed = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
        if ($parsed.documentCount -ge 2 -and ([string]$parsed.status).IndexOf("live 64 tiles", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $parsed
        }
        else {
            $null
        }
    } 60

    $documents = @($session.documents)
    foreach ($sourceType in @("OpenCvSharp.Mat", "Emgu.CV.Mat")) {
        $document = $documents | Where-Object { $_.sourceType -eq $sourceType } | Select-Object -First 1
        if (-not $document) {
            throw "Session did not preserve source type: $sourceType"
        }
        if ($document.width -ne $Width -or $document.height -ne $Height -or $document.sourceMode -ne "live") {
            throw "Invalid $sourceType session metadata: $($document | ConvertTo-Json -Compress)"
        }
    }

    $tempAfter = Get-TempFileSnapshot
    $newTempFiles = @()
    foreach ($entry in $tempAfter.GetEnumerator() | Sort-Object Name) {
        if (-not $tempBefore.ContainsKey($entry.Key)) {
            $newTempFiles += [pscustomobject][ordered]@{ path = $entry.Key; bytes = [int64]$entry.Value }
        }
    }
    $oversizedFiles = @($newTempFiles | Where-Object { $_.bytes -ge $expectedBytes })
    if ($oversizedFiles.Count -gt 0) {
        throw "The visualizer copied a complete large Mat into TEMP: $(($oversizedFiles | ConvertTo-Json -Compress))"
    }

    Stop-Debugging $visualStudio.Id
    $debuggingStopped = $true
    Wait-Until "large Mat debuggee exit" {
        if (-not (Get-Process -Id $debuggeeProcessId -ErrorAction SilentlyContinue)) { $true } else { $null }
    } 30 | Out-Null

    Focus-Window $mainHandle
    $imageView = Wait-Until "Raw Buffer image view after debug stop" {
        Find-ElementByAutomationId (Get-AutomationRoot $mainHandle) "RawBufferOpenGlImageView"
    } 30
    $bounds = $imageView.Current.BoundingRectangle
    $centerX = [int]($bounds.Left + $bounds.Width / 2)
    $centerY = [int]($bounds.Top + $bounds.Height / 2)
    [RawBufferInstalledVsixNative]::SetCursorPos($centerX + 12, $centerY + 12) | Out-Null
    Start-Sleep -Milliseconds 200
    [RawBufferInstalledVsixNative]::SetCursorPos($centerX - 12, $centerY - 12) | Out-Null

    $sourceUnavailableSession = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $fatalDialog = Find-DesktopElementNameContaining "Debuggee image memory is no longer readable"
        if ($fatalDialog) {
            throw "Live-memory shutdown escaped to an unhandled exception dialog: $fatalDialog"
        }

        if (Test-Path -LiteralPath $sessionPath) {
            $candidate = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
            if ($candidate.activeSourceUnavailable -eq $true -and
                ([string]$candidate.status).IndexOf("Live source unavailable", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $sourceUnavailableSession = $candidate
                break
            }
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    if (-not $sourceUnavailableSession) {
        throw "Docked viewer did not enter the controlled live-source-unavailable state after the debuggee exited."
    }

    Start-Sleep -Milliseconds 750
    $fatalDialog = Find-DesktopElementNameContaining "Debuggee image memory is no longer readable"
    if ($fatalDialog) {
        throw "Live-memory shutdown escaped to an unhandled exception dialog: $fatalDialog"
    }
    Capture-Window $mainHandle $sourceUnavailableScreenshotPath

    $result = [ordered]@{
        passed = $true
        startedUtc = $testStartedUtc.ToString("o")
        visualStudio = [ordered]@{
            instanceId = [string]$vsInstance.instanceId
            version = [string]$vsInstance.installationVersion
            processId = $visualStudio.Id
        }
        debuggee = [ordered]@{
            processId = $debuggeeProcessId
            width = $Width
            height = $Height
            bytesPerMat = $expectedBytes
        }
        openCvSharp = $openCvResult
        emguCv = $emguResult
        session = $session
        sourceUnavailableSession = $sourceUnavailableSession
        sourceUnavailableScreenshotPath = $sourceUnavailableScreenshotPath
        newTempFiles = $newTempFiles
        maximumNewTempFileBytes = if ($newTempFiles.Count -gt 0) { [int64](($newTempFiles | Measure-Object -Property bytes -Maximum).Maximum) } else { 0 }
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

        if ($debuggeeProcessId -gt 0) {
            Stop-Process -Id $debuggeeProcessId -Force -ErrorAction SilentlyContinue
        }
    }

    if ($executionStateActive) {
        [RawBufferInstalledVsixNative]::SetThreadExecutionState([RawBufferInstalledVsixNative]::ES_CONTINUOUS) | Out-Null
    }
}

if (-not $completed) {
    throw "Installed VSIX large-Mat smoke did not complete."
}

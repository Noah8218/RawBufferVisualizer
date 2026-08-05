[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$VisualStudioInstanceId = "",
    [switch]$NoBuild,
    [switch]$NoInstall,
    [switch]$KeepVisualStudio
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$outputRoot = Join-Path $repoRoot "artifacts\ui\multi-library-debug"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$sessionPath = Join-Path $outputRoot "multi-library-session.json"
$resultPath = Join-Path $outputRoot "multi-library-result.json"
$failureScreenshotPath = Join-Path $outputRoot "multi-library-failure.png"
$activityLogPath = Join-Path $outputRoot "multi-library-activity-log.xml"
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

if (-not ("RawBufferMultiLibraryNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RawBufferMultiLibraryNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extraInfo);
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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

if (-not ("RawBufferMultiLibraryEnum" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class RawBufferMultiLibraryEnum {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lpszClass, string lpszWindow);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public const uint BM_CLICK = 0x00F5;
}
'@
}

if (-not ("RawBufferMultiLibraryRot" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class RawBufferMultiLibraryRot {
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
public interface IRawBufferMultiLibraryMessageFilter {
    [PreserveSig] int HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr interfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr callee, int tickCount, int rejectType);
    [PreserveSig] int MessagePending(IntPtr callee, int tickCount, int pendingType);
}

public sealed class RawBufferMultiLibraryMessageFilter : IRawBufferMultiLibraryMessageFilter, IDisposable {
    private IRawBufferMultiLibraryMessageFilter previous;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IRawBufferMultiLibraryMessageFilter next, out IRawBufferMultiLibraryMessageFilter previous);

    public static RawBufferMultiLibraryMessageFilter Register() {
        var filter = new RawBufferMultiLibraryMessageFilter();
        IRawBufferMultiLibraryMessageFilter previous;
        CoRegisterMessageFilter(filter, out previous);
        filter.previous = previous;
        return filter;
    }

    public void Dispose() {
        IRawBufferMultiLibraryMessageFilter ignored;
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

function Focus-Window([IntPtr]$Handle, [int]$Width = 1920, [int]$Height = 1040) {
    [RawBufferMultiLibraryNative]::ShowWindow($Handle, 9) | Out-Null
    [RawBufferMultiLibraryNative]::SetWindowPos($Handle, [RawBufferMultiLibraryNative]::HWND_TOPMOST, 20, 20, $Width, $Height, 0x0040) | Out-Null
    [RawBufferMultiLibraryNative]::BringWindowToTop($Handle) | Out-Null
    [RawBufferMultiLibraryNative]::SetForegroundWindow($Handle) | Out-Null
    Start-Sleep -Milliseconds 250
    [RawBufferMultiLibraryNative]::SetWindowPos($Handle, [RawBufferMultiLibraryNative]::HWND_NOTOPMOST, 20, 20, $Width, $Height, 0x0040) | Out-Null
}

function Capture-Window([IntPtr]$Handle, [string]$Path) {
    Focus-Window $Handle
    $rect = New-Object RawBufferMultiLibraryNative+RECT
    [RawBufferMultiLibraryNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
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
    [RawBufferMultiLibraryNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
    [RawBufferMultiLibraryNative]::mouse_event([RawBufferMultiLibraryNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [RawBufferMultiLibraryNative]::mouse_event([RawBufferMultiLibraryNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Dismiss-OpenFileDialog {
    $script:openDialogHandle = [IntPtr]::Zero
    $callback = [RawBufferMultiLibraryEnum+EnumWindowsProc] {
        param([IntPtr]$hWnd, [IntPtr]$lParam)
        $sb = New-Object System.Text.StringBuilder 256
        [RawBufferMultiLibraryEnum]::GetWindowText($hWnd, $sb, 256) | Out-Null
        $title = $sb.ToString()
        if ($title -eq "열기" -or $title -eq "Open") {
            $script:openDialogHandle = $hWnd
            return $false
        }
        return $true
    }

    [RawBufferMultiLibraryEnum]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    if ($script:openDialogHandle -eq [IntPtr]::Zero) {
        return $false
    }

    $cancelButton = [IntPtr]::Zero
    foreach ($name in @("취소", "Cancel")) {
        $cancelButton = [RawBufferMultiLibraryEnum]::FindWindowEx($script:openDialogHandle, [IntPtr]::Zero, "Button", $name)
        if ($cancelButton -ne [IntPtr]::Zero) {
            break
        }
    }

    if ($cancelButton -eq [IntPtr]::Zero) {
        return $false
    }

    [RawBufferMultiLibraryNative]::SetForegroundWindow($script:openDialogHandle) | Out-Null
    Start-Sleep -Milliseconds 200
    [RawBufferMultiLibraryEnum]::SendMessage($cancelButton, [RawBufferMultiLibraryEnum]::BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 500
    return $true
}

function Dismiss-DebuggerEvaluationWarning {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $localizedOk = [string]([char]0xD655) + [string]([char]0xC778)
    $localizedContinue = ([string]([char]0xB514) + [string]([char]0xBC84) + [string]([char]0xAE45) + " " +
        [string]([char]0xACC4) + [string]([char]0xC18D))
    $continueLabels = @("Continue Debugging", "Continue", $localizedContinue)
    $windowCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $windowCondition)
    for ($windowIndex = 0; $windowIndex -lt $windows.Count; $windowIndex++) {
        $window = $windows.Item($windowIndex)
        $windowPattern = $null
        if (-not $window.TryGetCurrentPattern(
            [System.Windows.Automation.WindowPattern]::Pattern,
            [ref]$windowPattern) -or
            -not ([System.Windows.Automation.WindowPattern]$windowPattern).Current.IsModal) {
            continue
        }

        $buttons = @(Get-ElementsByControlType $window ([System.Windows.Automation.ControlType]::Button))
        $targetButton = $buttons |
            Where-Object { $continueLabels -contains [string]$_.Current.Name } |
            Select-Object -First 1

        if (-not $targetButton) {
            $okButton = $buttons |
                Where-Object { @("OK", $localizedOk) -contains [string]$_.Current.Name } |
                Select-Object -First 1
            if ($okButton) {
                $dialogElements = $window.FindAll(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    [System.Windows.Automation.Condition]::TrueCondition)
                for ($elementIndex = 0; $elementIndex -lt $dialogElements.Count; $elementIndex++) {
                    $elementName = [string]$dialogElements.Item($elementIndex).Current.Name
                    if ($elementName.IndexOf("ClrCustomVisualizerDebuggeeHost", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                        $elementName.IndexOf("DebuggerVisualizers.DebuggeeSide", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        $targetButton = $okButton
                        break
                    }
                }
            }
        }

        if ($targetButton) {
            $pattern = $null
            if ($targetButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
                ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
            }
            else {
                $rect = $targetButton.Current.BoundingRectangle
                [RawBufferMultiLibraryNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
                [RawBufferMultiLibraryNative]::mouse_event([RawBufferMultiLibraryNative]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
                [RawBufferMultiLibraryNative]::mouse_event([RawBufferMultiLibraryNative]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
            }

            Start-Sleep -Milliseconds 300
            return $true
        }
    }

    $false
}

function Invoke-Dte([int]$ProcessId, [scriptblock]$Action) {
    $dte = Wait-Until "Visual Studio DTE" { [RawBufferMultiLibraryRot]::GetDte($ProcessId) } 120
    $filter = [RawBufferMultiLibraryMessageFilter]::Register()
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

function Invoke-MultiLibraryScenario(
    [Diagnostics.Process]$Process,
    [IntPtr]$MainHandle) {

    $expectedVariables = @(
        @{ Name = "mono8Owner"; Data = "mono8Owner.View.Buffer"; Width = "mono8Owner.View.Width"; Height = "mono8Owner.View.Height"; Stride = "mono8Owner.View.Stride"; PixelFormat = "mono8Owner.View.PixelFormat" },
        @{ Name = "bgr24Owner"; Data = "bgr24Owner.View.Buffer"; Width = "bgr24Owner.View.Width"; Height = "bgr24Owner.View.Height"; Stride = "bgr24Owner.View.Stride"; PixelFormat = "bgr24Owner.View.PixelFormat" },
        @{ Name = "openCvMat"; Data = "openCvMat.Data"; Width = "openCvMat.Cols"; Height = "openCvMat.Rows"; Stride = "openCvMat.Step"; PixelFormat = "openCvMat.Type" },
        @{ Name = "emguMat"; Data = "emguMat.DataPointer"; Width = "emguMat.Cols"; Height = "emguMat.Rows"; Stride = "emguMat.Step"; PixelFormat = "emguMat.Depth" },
        @{ Name = "paddingAwareFrame"; Data = "paddingAwareFrame.PixelDataPointer"; Width = "paddingAwareFrame.Width"; Height = "paddingAwareFrame.Height"; Stride = $null; PixelFormat = "paddingAwareFrame.PixelTypeValue" },
        @{ Name = "strideAwareFrame"; Data = "strideAwareFrame.DataPtr"; Width = "strideAwareFrame.Width"; Height = "strideAwareFrame.Height"; Stride = "strideAwareFrame.Stride"; PixelFormat = "strideAwareFrame.PixelFormat" },
        @{ Name = "offsetAwareFrame"; Data = "offsetAwareFrame.ImageData"; Width = "offsetAwareFrame.Width"; Height = "offsetAwareFrame.Height"; Stride = $null; PixelFormat = "offsetAwareFrame.PixelFormat" },
        @{ Name = "sizedBufferFrame"; Data = "sizedBufferFrame.Data"; Width = "sizedBufferFrame.Width"; Height = "sizedBufferFrame.Height"; Stride = $null; PixelFormat = "sizedBufferFrame.PixelFormat" }
    )

    # Locals is virtualized and a full Visual Studio UI Automation tree search
    # can time out. Read the current frame through DTE and use UI Automation
    # only for the extension surface that follows.
    Wait-Until "multi-library breakpoint variables in current frame" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        Invoke-Dte $Process.Id {
            param($dte)
            $frame = $dte.Debugger.CurrentStackFrame
            if (-not $frame -or -not $frame.Locals) {
                return $false
            }

            $foundOpenCv = $false
            $foundNeutralFrame = $false
            for ($index = 1; $index -le $frame.Locals.Count; $index++) {
                $name = [string]$frame.Locals.Item($index).Name
                if ($name -eq "openCvMat") {
                    $foundOpenCv = $true
                }
                elseif ($name -eq "paddingAwareFrame") {
                    $foundNeutralFrame = $true
                }
            }

            $foundOpenCv -and $foundNeutralFrame
        }
    } 90 | Out-Null

    Show-RawBufferToolWindow $Process.Id
    Start-Sleep -Milliseconds 750
    $scanButton = Wait-Until "Automatic Vision Inspector Scan Now button" {
        Find-ElementByAutomationId (Get-AutomationRoot $MainHandle) "AutomaticVisionScanNowButton"
    } 30
    $invokePattern = $null
    if (-not $scanButton.TryGetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern,
        [ref]$invokePattern)) {
        throw "Automatic Vision Inspector Scan Now button does not support InvokePattern."
    }
    ([System.Windows.Automation.InvokePattern]$invokePattern).Invoke()

    # Wait for the image list to populate.
    $imageList = Wait-Until "image list with candidates" {
        Dismiss-DebuggerEvaluationWarning | Out-Null
        $items = @(Get-ImageListItems (Get-AutomationRoot $MainHandle))
        $items.Count -ge $expectedVariables.Count
    } 60

    $items = @(Get-ImageListItems (Get-AutomationRoot $MainHandle))
    if ($items.Count -lt $expectedVariables.Count) {
        throw "Expected at least $($expectedVariables.Count) image candidates, but found $($items.Count)."
    }

    $verificationResults = @()
    $overallScreenshotPath = Join-Path $outputRoot "multi-library-overall.png"
    Start-Sleep -Milliseconds 1000
    Dismiss-DebuggerEvaluationWarning | Out-Null
    Capture-Window $MainHandle $overallScreenshotPath

    foreach ($expected in $expectedVariables) {
        $name = $expected.Name
        $foundItem = $items | Where-Object { [string]$_.Current.Name -like "*$name*" } | Select-Object -First 1

        $result = [ordered]@{
            variableName = $name
            found = ($foundItem -ne $null)
            itemName = if ($foundItem) { [string]$foundItem.Current.Name } else { $null }
            dataExpression = $null
            widthExpression = $null
            heightExpression = $null
            strideExpression = $null
            pixelFormatExpression = $null
        }

        if ($foundItem) {
            Select-AutomationItem $foundItem
            Start-Sleep -Milliseconds 500
            Dismiss-DebuggerEvaluationWarning | Out-Null

            $itemScreenshotPath = Join-Path $outputRoot "multi-library-$name.png"
            Capture-Window $MainHandle $itemScreenshotPath
            $result["screenshotPath"] = $itemScreenshotPath

            # The image list item name often contains the recognized expression summary.
            $itemText = [string]$foundItem.Current.Name
            foreach ($key in @("Data", "Width", "Height", "Stride", "PixelFormat")) {
                $expression = $expected[$key]
                if (-not [string]::IsNullOrWhiteSpace($expression)) {
                    $result[(($key.Substring(0, 1).ToLower()) + $key.Substring(1) + "Expression")] = $itemText.Contains($expression)
                }
            }
        }

        $verificationResults += $result
    }

    [ordered]@{
        candidateCount = $items.Count
        overallScreenshotPath = $overallScreenshotPath
        results = $verificationResults
    }
}

Assert-InteractiveDesktop
$executionState = [RawBufferMultiLibraryNative]::ES_CONTINUOUS -bor
    [RawBufferMultiLibraryNative]::ES_SYSTEM_REQUIRED -bor
    [RawBufferMultiLibraryNative]::ES_DISPLAY_REQUIRED
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
$debuggeePath = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.VisualizerDebuggee\$Configuration\net472\RawBufferVisualizer.VisualizerDebuggee.exe"

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
    & dotnet build $sampleProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "VisualizerDebuggee build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $debuggeePath)) {
    throw "VisualizerDebuggee was not found: $debuggeePath"
}
$debuggeePath = (Resolve-Path -LiteralPath $debuggeePath).Path

$testStartedUtc = [DateTime]::UtcNow
$visualStudio = $null
$mainHandle = [IntPtr]::Zero
$completed = $false
$executionStateActive = $false
$debuggingStopped = $false

try {
    [RawBufferMultiLibraryNative]::SetThreadExecutionState($executionState) | Out-Null
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
        "--multi-library-debug"
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

    Focus-Window $mainHandle 1920 1040
    Show-RawBufferToolWindow $visualStudio.Id
    Start-Sleep -Milliseconds 1500
    Dismiss-OpenFileDialog | Out-Null
    $toolRoot = Wait-Until "Raw Buffer Visualizer tool window before debug" {
        Find-RawBufferToolWindowElement $mainHandle
    } 30
    Start-Debugging $visualStudio.Id

    $scenarioResult = Invoke-MultiLibraryScenario $visualStudio $mainHandle

    Stop-Debugging $visualStudio.Id
    $debuggingStopped = $true

    $result = [ordered]@{
        passed = $true
        startedUtc = $testStartedUtc.ToString("o")
        scenario = "MultiLibrary"
        visualStudio = [ordered]@{
            instanceId = [string]$vsInstance.instanceId
            version = [string]$vsInstance.installationVersion
            processId = $visualStudio.Id
        }
        result = $scenarioResult
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
        [RawBufferMultiLibraryNative]::SetThreadExecutionState([RawBufferMultiLibraryNative]::ES_CONTINUOUS) | Out-Null
    }
}

if (-not $completed) {
    throw "Multi-library debug smoke did not complete."
}

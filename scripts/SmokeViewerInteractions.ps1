param(
    [string]$Configuration = "Debug",
    [string]$ViewerFramework = "net472",
    [string]$SampleFramework = "net9.0",
    [string]$OutputDir = "artifacts\ui\viewer-interactions"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
Set-Location $repoRoot

dotnet build .\RawBufferVisualizer.sln --configuration $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

dotnet run --project .\samples\RawBufferVisualizer.Samples\RawBufferVisualizer.Samples.csproj -f $SampleFramework --configuration $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Sample generation failed with exit code $LASTEXITCODE."
}

$viewerExe = Join-Path $repoRoot ".build\bin\RawBufferVisualizer.Wpf\$Configuration\$ViewerFramework\RawBufferVisualizer.Wpf.exe"
if (-not (Test-Path $viewerExe)) {
    throw "Viewer exe not found: $viewerExe"
}

$samplePath = Join-Path $repoRoot "artifacts\samples\mono8-gradient.rbuf.json"
if (-not (Test-Path $samplePath)) {
    throw "Sample file not found: $samplePath"
}

$captureRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ("RawBufferInteractionNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferInteractionNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
}
'@
}

function Wait-Until([string]$description, [scriptblock]$condition, [int]$timeoutSeconds = 10) {
    $deadline = [DateTime]::Now.AddSeconds($timeoutSeconds)
    $lastError = $null
    do {
        try {
            $value = & $condition
            if ($value) {
                return $value
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 200
    } while ([DateTime]::Now -lt $deadline)

    if ($lastError) {
        throw "$description timed out. Last error: $lastError"
    }

    throw "$description timed out."
}

function Minimize-OtherWindows([int]$exceptPid) {
    Get-Process | Where-Object {
        $_.MainWindowHandle -ne 0 -and $_.Id -ne $exceptPid
    } | ForEach-Object {
        [RawBufferInteractionNative]::ShowWindow($_.MainWindowHandle, 6) | Out-Null
    }
}

function Bring-Viewer([IntPtr]$hwnd, [int]$viewerProcessId) {
    Minimize-OtherWindows $viewerProcessId
    [RawBufferInteractionNative]::ShowWindow($hwnd, 9) | Out-Null
    [RawBufferInteractionNative]::SetWindowPos($hwnd, [RawBufferInteractionNative]::HWND_TOPMOST, 8, 8, 1280, 800, 0x0040) | Out-Null
    [RawBufferInteractionNative]::BringWindowToTop($hwnd) | Out-Null
    [RawBufferInteractionNative]::SetForegroundWindow($hwnd) | Out-Null
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object RawBufferInteractionNative+RECT
    [RawBufferInteractionNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid window bounds: $width x $height"
    }

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Measure-Capture([string]$path) {
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $nonDark = 0
        $samples = 0
        for ($y = 80; $y -lt ($bitmap.Height - 40); $y += 8) {
            for ($x = 280; $x -lt ($bitmap.Width - 280); $x += 8) {
                $c = $bitmap.GetPixel($x, $y)
                $gray = [int](($c.R + $c.G + $c.B) / 3)
                if ($gray -gt 20) {
                    $nonDark++
                }

                $samples++
            }
        }

        [pscustomobject]@{
            Samples = $samples
            NonDarkRatio = [Math]::Round($nonDark / [double][Math]::Max($samples, 1), 4)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-Root([IntPtr]$hwnd) {
    [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
}

function Find-Element([System.Windows.Automation.AutomationElement]$root, [string]$automationId, [bool]$required = $true) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
    if ($required -and $null -eq $element) {
        throw "Automation element not found: $automationId"
    }

    $element
}

function Get-ElementName([System.Windows.Automation.AutomationElement]$root, [string]$automationId) {
    $element = Find-Element $root $automationId
    $element.Current.Name
}

function Invoke-Button([System.Windows.Automation.AutomationElement]$root, [string]$automationId) {
    $element = Find-Element $root $automationId
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Set-SliderValue([System.Windows.Automation.AutomationElement]$root, [string]$automationId, [double]$value) {
    $element = Find-Element $root $automationId
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $pattern.SetValue($value)
}

function Get-DescendantsByControlType([System.Windows.Automation.AutomationElement]$root, [string]$automationId, [System.Windows.Automation.ControlType]$controlType) {
    $element = Find-Element $root $automationId
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        $controlType)
    $element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Select-Item([System.Windows.Automation.AutomationElement]$element) {
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
}

function Toggle-CheckBox([System.Windows.Automation.AutomationElement]$root, [string]$automationId) {
    $element = Find-Element $root $automationId
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $pattern.Toggle()
    $pattern.Current.ToggleState
}

function Get-ImageRect([System.Windows.Automation.AutomationElement]$root, [IntPtr]$hwnd) {
    $image = Find-Element $root "ImageView" $false
    if ($image -ne $null) {
        $bounds = $image.Current.BoundingRectangle
        if ($bounds.Width -gt 10 -and $bounds.Height -gt 10) {
            return $bounds
        }
    }

    $rect = New-Object RawBufferInteractionNative+RECT
    [RawBufferInteractionNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    [System.Windows.Rect]::new($rect.Left + 285, $rect.Top + 78, ($rect.Right - $rect.Left) - 585, ($rect.Bottom - $rect.Top) - 80)
}

function Move-MouseToImage([System.Windows.Automation.AutomationElement]$root, [IntPtr]$hwnd, [double]$xRatio = 0.5, [double]$yRatio = 0.5) {
    $bounds = Get-ImageRect $root $hwnd
    $x = [int]($bounds.Left + ($bounds.Width * $xRatio))
    $y = [int]($bounds.Top + ($bounds.Height * $yRatio))
    [RawBufferInteractionNative]::SetCursorPos($x, $y) | Out-Null
}

function Set-DialogPathAndAccept([string]$path) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }

    Set-Clipboard -Value $path
    Start-Sleep -Milliseconds 500
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
}

function Add-Result([System.Collections.Generic.List[object]]$results, [string]$name, [string]$detail) {
    $results.Add([pscustomobject]@{
        Check = $name
        Result = "PASS"
        Detail = $detail
    })
}

$results = New-Object System.Collections.Generic.List[object]
$process = Start-Process -FilePath $viewerExe -ArgumentList @($samplePath, $samplePath) -PassThru
try {
    $deadline = [DateTime]::Now.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $hwnd = $process.MainWindowHandle
    } while ($hwnd -eq 0 -and [DateTime]::Now -lt $deadline)

    if ($hwnd -eq 0) {
        throw "Window handle was not created."
    }

    Bring-Viewer $hwnd $process.Id
    Start-Sleep -Seconds 1
    $root = Get-Root $hwnd

    Wait-Until "status text" {
        $status = Get-ElementName (Get-Root $hwnd) "StatusText"
        $status -match "640 x 480, Mono8"
    } | Out-Null
    Add-Result $results "open sample" (Get-ElementName (Get-Root $hwnd) "StatusText")

    Find-Element (Get-Root $hwnd) "DocumentTabs" | Out-Null
    Find-Element (Get-Root $hwnd) "ImageList" | Out-Null
    Find-Element (Get-Root $hwnd) "LinkViewsBox" | Out-Null
    $initialTabs = Get-DescendantsByControlType (Get-Root $hwnd) "DocumentTabs" ([System.Windows.Automation.ControlType]::TabItem)
    $initialListItems = Get-DescendantsByControlType (Get-Root $hwnd) "ImageList" ([System.Windows.Automation.ControlType]::ListItem)
    if ($initialTabs.Count -lt 2 -or $initialListItems.Count -lt 2) {
        throw "Session UI did not show both startup images."
    }

    Add-Result $results "session UI initial image" "tabs=$($initialTabs.Count); list=$($initialListItems.Count)"

    $capturePath = Join-Path $captureRoot "interaction-open.png"
    Capture-Window $hwnd $capturePath
    $measure = Measure-Capture $capturePath
    if ($measure.NonDarkRatio -lt 0.05) {
        throw "Viewer capture looks blank."
    }
    Add-Result $results "render capture" "NonDarkRatio=$($measure.NonDarkRatio); $capturePath"

    Move-MouseToImage (Get-Root $hwnd) $hwnd 0.5 0.5
    Wait-Until "pixel read" {
        $pixel = Get-ElementName (Get-Root $hwnd) "PixelText"
        $pixel -match "X=\d+, Y=\d+, Value=\d+"
    } | Out-Null
    Add-Result $results "pixel read / GV" (Get-ElementName (Get-Root $hwnd) "PixelText")

    Invoke-Button (Get-Root $hwnd) "ActualSizeButton"
    Wait-Until "1:1 zoom" {
        (Get-ElementName (Get-Root $hwnd) "ZoomText") -match "^100(\.0)?%"
    } | Out-Null
    Add-Result $results "1:1 zoom" (Get-ElementName (Get-Root $hwnd) "ZoomText")

    Invoke-Button (Get-Root $hwnd) "FitButton"
    Wait-Until "fit zoom" {
        $text = Get-ElementName (Get-Root $hwnd) "ZoomText"
        $text -notmatch "^100(\.0)?%" -and $text -match "%"
    } | Out-Null
    Add-Result $results "fit zoom" (Get-ElementName (Get-Root $hwnd) "ZoomText")

    Set-SliderValue (Get-Root $hwnd) "ZoomSlider" 2
    Wait-Until "slider zoom" {
        (Get-ElementName (Get-Root $hwnd) "ZoomText") -match "^200(\.0)?%"
    } | Out-Null
    Add-Result $results "slider zoom" (Get-ElementName (Get-Root $hwnd) "ZoomText")

    Move-MouseToImage (Get-Root $hwnd) $hwnd 0.5 0.5
    $beforeWheel = Get-ElementName (Get-Root $hwnd) "ZoomText"
    [RawBufferInteractionNative]::mouse_event([RawBufferInteractionNative]::MOUSEEVENTF_WHEEL, 0, 0, 120, [UIntPtr]::Zero)
    Wait-Until "mouse wheel zoom" {
        $afterWheel = Get-ElementName (Get-Root $hwnd) "ZoomText"
        $afterWheel -ne $beforeWheel -and $afterWheel -match "%"
    } | Out-Null
    Add-Result $results "mouse wheel zoom" "$beforeWheel -> $(Get-ElementName (Get-Root $hwnd) "ZoomText")"

    $pngPath = Join-Path $captureRoot "interaction-save.png"
    Invoke-Button (Get-Root $hwnd) "SavePngButton"
    Set-DialogPathAndAccept $pngPath
    Wait-Until "save png" { Test-Path -LiteralPath $pngPath } 10 | Out-Null
    Add-Result $results "save png" $pngPath

    $snapshotPath = Join-Path $captureRoot "interaction-snapshot.rbuf.json"
    $rawPath = Join-Path $captureRoot "interaction-snapshot.raw"
    if (Test-Path -LiteralPath $rawPath) {
        Remove-Item -LiteralPath $rawPath -Force
    }

    Bring-Viewer $hwnd $process.Id
    Invoke-Button (Get-Root $hwnd) "SaveSnapshotButton"
    Set-DialogPathAndAccept $snapshotPath
    Wait-Until "save snapshot metadata" { Test-Path -LiteralPath $snapshotPath } 10 | Out-Null
    Wait-Until "save snapshot raw" { Test-Path -LiteralPath $rawPath } 10 | Out-Null
    Add-Result $results "save snapshot" "$snapshotPath; $rawPath"

    $tabsAfterSecondOpen = Get-DescendantsByControlType (Get-Root $hwnd) "DocumentTabs" ([System.Windows.Automation.ControlType]::TabItem)
    Add-Result $results "startup second image tab" "tabs=$($tabsAfterSecondOpen.Count)"

    Select-Item $tabsAfterSecondOpen.Item(0)
    Wait-Until "switch back to first tab" {
        (Get-ElementName (Get-Root $hwnd) "StatusText") -match "640 x 480, Mono8"
    } | Out-Null
    Add-Result $results "switch first tab" (Get-ElementName (Get-Root $hwnd) "StatusText")

    $toggleState = Toggle-CheckBox (Get-Root $hwnd) "LinkViewsBox"
    Wait-Until "link views toggle" {
        $element = Find-Element (Get-Root $hwnd) "LinkViewsBox"
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On
    } | Out-Null
    Add-Result $results "link views toggle" $toggleState.ToString()
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
}

$results | Format-Table -AutoSize
Write-Host "Viewer interaction smoke passed $($results.Count) checks."

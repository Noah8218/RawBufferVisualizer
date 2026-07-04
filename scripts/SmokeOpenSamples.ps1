param(
    [string]$Configuration = "Debug",
    [string]$ViewerFramework = "net472",
    [string]$SampleFramework = "net9.0",
    [string]$OutputDir = "artifacts\ui\smoke-samples"
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

$sampleFiles = Get-ChildItem (Join-Path $repoRoot "artifacts\samples\*.rbuf.json") | Sort-Object Name
if ($sampleFiles.Count -eq 0) {
    throw "No sample .rbuf.json files found."
}

$captureRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null

Add-Type -AssemblyName System.Drawing
if (-not ("RawBufferSmokeNative" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RawBufferSmokeNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
}
'@
}

function Minimize-OtherWindows([int]$exceptPid) {
    Get-Process | Where-Object {
        $_.MainWindowHandle -ne 0 -and $_.Id -ne $exceptPid
    } | ForEach-Object {
        [RawBufferSmokeNative]::ShowWindow($_.MainWindowHandle, 6) | Out-Null
    }
}

function Bring-Viewer([IntPtr]$hwnd) {
    [RawBufferSmokeNative]::ShowWindow($hwnd, 9) | Out-Null
    [RawBufferSmokeNative]::SetWindowPos($hwnd, [RawBufferSmokeNative]::HWND_TOPMOST, 8, 8, 1280, 800, 0x0040) | Out-Null
    [RawBufferSmokeNative]::BringWindowToTop($hwnd) | Out-Null
    [RawBufferSmokeNative]::SetForegroundWindow($hwnd) | Out-Null
}

function Capture-Window([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object RawBufferSmokeNative+RECT
    [RawBufferSmokeNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
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
        $colorful = 0
        $samples = 0
        for ($y = 80; $y -lt ($bitmap.Height - 40); $y += 8) {
            for ($x = 280; $x -lt ($bitmap.Width - 280); $x += 8) {
                $c = $bitmap.GetPixel($x, $y)
                $gray = [int](($c.R + $c.G + $c.B) / 3)
                if ($gray -gt 20) {
                    $nonDark++
                }

                if ([Math]::Abs([int]$c.R - [int]$c.G) -gt 20 -or [Math]::Abs([int]$c.G - [int]$c.B) -gt 20 -or [Math]::Abs([int]$c.R - [int]$c.B) -gt 20) {
                    $colorful++
                }

                $samples++
            }
        }

        [pscustomobject]@{
            Samples = $samples
            NonDarkRatio = [Math]::Round($nonDark / [double][Math]::Max($samples, 1), 4)
            ColorfulRatio = [Math]::Round($colorful / [double][Math]::Max($samples, 1), 4)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$results = New-Object System.Collections.Generic.List[object]
foreach ($sample in $sampleFiles) {
    $safeName = [IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($sample.Name))
    $capturePath = Join-Path $captureRoot ($safeName + ".png")
    $process = Start-Process -FilePath $viewerExe -ArgumentList @($sample.FullName) -PassThru
    try {
        $deadline = [DateTime]::Now.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            $hwnd = $process.MainWindowHandle
        } while ($hwnd -eq 0 -and [DateTime]::Now -lt $deadline)

        if ($hwnd -eq 0) {
            throw "Window handle was not created for $($sample.Name)."
        }

        for ($i = 0; $i -lt 4; $i++) {
            Minimize-OtherWindows $process.Id
            Bring-Viewer $hwnd
            Start-Sleep -Milliseconds 250
        }

        Start-Sleep -Milliseconds 900
        Capture-Window $hwnd $capturePath
        $measure = Measure-Capture $capturePath
        if ($measure.NonDarkRatio -lt 0.05) {
            throw "Capture looks blank for $($sample.Name)."
        }

        $isColorSample = $sample.Name -match "rgb|bgr|bgra|bayer"
        if ($isColorSample -and $measure.ColorfulRatio -lt 0.05) {
            throw "Color capture does not look colorful for $($sample.Name)."
        }

        $results.Add([pscustomobject]@{
            Sample = $sample.Name
            Capture = $capturePath
            NonDarkRatio = $measure.NonDarkRatio
            ColorfulRatio = $measure.ColorfulRatio
        })
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
}

$results | Format-Table -AutoSize
Write-Host "Smoke opened $($results.Count) sample files."

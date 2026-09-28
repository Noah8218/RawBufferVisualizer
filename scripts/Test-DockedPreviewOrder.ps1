[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BuildRoot,
    [Parameter(Mandatory = $true)][string]$VisualStudioRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run this in Windows PowerShell with -STA; the control targets .NET Framework WPF.'
}
$bin = Join-Path $BuildRoot 'bin\RawBufferVisualizer.VisualStudio.Vssdk\Release\net472'
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $output, "$output\temp" | Out-Null
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$control = $null
$requests = @()
try {
    $env:TEMP = "$output\temp"
    $env:TMP = $env:TEMP
    Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Windows.Forms
    [Reflection.Assembly]::LoadFrom((Join-Path $VisualStudioRoot 'Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll')) | Out-Null
    foreach ($name in @('Microsoft.VisualStudio.Imaging.dll', 'Microsoft.VisualStudio.ImageCatalog.dll', 'RawBufferVisualizer.VisualStudio.Vssdk.dll')) {
        [Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) | Out-Null
    }
    $control = New-Object RawBufferVisualizer.VisualStudio.Vssdk.RawBufferToolWindowControl
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    $active = $control.GetType().GetProperty('_activeDocument', $flags)
    $documents = $control.GetType().GetProperty('_documents', $flags)
    $identity = [Guid]::NewGuid().ToString('N')
    $states = @()
    $steps = @(
        @{ Label = 'preview'; Size = 2; Preview = $true; Id = $identity; ExpectedSize = 2; ExpectedPreview = $true; ExpectedCount = 1 },
        @{ Label = 'full'; Size = 8; Preview = $false; Id = $identity; ExpectedSize = 8; ExpectedPreview = $false; ExpectedCount = 1 },
        @{ Label = 'late-preview'; Size = 2; Preview = $true; Id = $identity; ExpectedSize = 8; ExpectedPreview = $false; ExpectedCount = 1 },
        @{ Label = 'different-image-preview'; Size = 2; Preview = $true; Id = $identity + '-other'; ExpectedSize = 2; ExpectedPreview = $true; ExpectedCount = 2 }
    )
    foreach ($step in $steps) {
        $directory = [RawBufferVisualizer.VisualStudio.VisualStudioTempStore]::CreateSnapshotDirectory()
        $metadata = Join-Path $directory 'image.rbuf.json'
        [IO.File]::WriteAllBytes((Join-Path $directory 'image.raw'), (New-Object byte[] ($step.Size * $step.Size)))
        @{ rawFile = 'image.raw'; width = $step.Size; height = $step.Size; stride = $step.Size; pixelFormat = 'Mono8'; validBits = 8; byteOrder = 'LittleEndian' } |
            ConvertTo-Json | Set-Content -LiteralPath $metadata -Encoding UTF8
        $request = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::WriteSnapshotRequest(
            [Diagnostics.Process]::GetCurrentProcess().Id, $metadata, 'Known image', 'Mono8 fixture', $step.Id, $step.Preview)
        $requests += $request
        $processing = ''
        if (-not [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::TryClaimRequest($request, [ref]$processing)) { throw 'Claim failed.' }
        $accepted = $control.OpenClaimedHandoffRequest($request, $processing)
        $terminal = [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::GetRequestState($request).ToString()
        $document = $active.GetValue($control, $null)
        $count = $documents.GetValue($control, $null).Count
        $measureButton = $control.FindName('MeasureButton')
        [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::DataBind)
        $canMeasure = $measureButton.IsEnabled
        $states += [pscustomobject]@{ Case = $step.Label; Width = $document.Descriptor.Width; IsPreview = $document.IsPreview; Documents = $count; Accepted = $accepted; Terminal = $terminal; CanMeasure = $canMeasure; MeasurementToolTip = [string]$measureButton.ToolTip }
        $states | ConvertTo-Json | Set-Content -LiteralPath "$output\results.json" -Encoding UTF8
        if (-not $accepted -or $terminal -ne 'Acknowledged' -or $document.IsError -or
            $document.Descriptor.Width -ne $step.ExpectedSize -or $document.IsPreview -ne $step.ExpectedPreview -or $count -ne $step.ExpectedCount) {
            throw "Unexpected document or handoff state: $($step.Label). See $output\results.json"
        }
        if ($canMeasure -eq $step.ExpectedPreview -or $measureButton.IsChecked -or
            ($step.ExpectedPreview -and ([string]$measureButton.ToolTip -notlike '*sampled previews*' -or -not [Windows.Controls.ToolTipService]::GetShowOnDisabled($measureButton)))) {
            throw "Unexpected measurement availability or preview explanation: $($step.Label)."
        }
        if ($step.Label -eq 'late-preview' -and (Test-Path -LiteralPath $directory)) { throw 'Ignored preview left its owned snapshot directory behind.' }
        [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::CleanupRequestArtifacts($request)
    }
    $states | Format-Table -AutoSize
    Write-Output 'PASS: full resolution survives late preview, ignored preview is acknowledged and cleaned, and a different image can still open a preview.'
    Write-Output 'Scope: in-process WPF control and handoff admission; no desktop window or installed IDE interaction.'
}
finally {
    if ($null -ne $control) { $control.Dispose() }
    foreach ($request in $requests) { [RawBufferVisualizer.VisualStudio.VisualizerHandoffInbox]::CleanupRequestArtifacts($request) }
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}

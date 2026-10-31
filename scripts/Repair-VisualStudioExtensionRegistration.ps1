[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$VisualStudioInstanceId = '',
    [switch]$AllowRunningVisualStudio
)

throw 'Manual registration repair is retired. The verified single hybrid VSIX owns generated VSSDK and Concord registration. Use Install-VisualStudioExtension.ps1 with the exact verified candidate; no registry changes were made.'
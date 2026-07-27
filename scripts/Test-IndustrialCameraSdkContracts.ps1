[CmdletBinding()]
param(
    [string]$BaslerPylonAssembly = "",
    [string]$SpinnakerNetAssembly = "",
    [string]$VimbaNetAssembly = "",
    [string]$IdsPeakIcvAssembly = "",
    [string]$OutputPath = "",
    [switch]$RequireAll
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts\validation\industrial-camera-sdk-contracts.json"
}

if ([string]::IsNullOrWhiteSpace($IdsPeakIcvAssembly)) {
    $idsPackageRoot = Join-Path $env:USERPROFILE ".nuget\packages\idsimaging.peak.icv"
    if (Test-Path -LiteralPath $idsPackageRoot) {
        $IdsPeakIcvAssembly = Get-ChildItem -LiteralPath $idsPackageRoot -Recurse -Filter "IDSImaging.Peak.ICV.dll" -File |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }
}

if (-not [string]::IsNullOrWhiteSpace($IdsPeakIcvAssembly)) {
    $idsCommonRoot = Join-Path $env:USERPROFILE ".nuget\packages\idsimaging.peak.common"
    if (Test-Path -LiteralPath $idsCommonRoot) {
        $idsCommonAssembly = Get-ChildItem -LiteralPath $idsCommonRoot -Recurse -Filter "IDSImaging.Peak.Common.dll" -File |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName -First 1
        if (-not [string]::IsNullOrWhiteSpace($idsCommonAssembly)) {
            [Reflection.Assembly]::LoadFrom($idsCommonAssembly) | Out-Null
        }
    }
}

$contracts = @(
    @{
        Vendor = "Basler"
        Sdk = "pylon .NET"
        AssemblyPath = $BaslerPylonAssembly
        TypeNames = @("Basler.Pylon.IGrabResult")
        RequiredProperties = @("PixelDataPointer", "Width", "Height", "PaddingX", "PayloadSize", "PixelTypeValue")
    },
    @{
        Vendor = "Teledyne FLIR"
        Sdk = "Spinnaker .NET"
        AssemblyPath = $SpinnakerNetAssembly
        TypeNames = @("SpinnakerNET.IManagedImage")
        RequiredProperties = @("DataPtr", "Width", "Height", "Stride", "PixelFormat")
    },
    @{
        Vendor = "Allied Vision"
        Sdk = "Vimba X .NET"
        AssemblyPath = $VimbaNetAssembly
        TypeNames = @("VmbNET.IFrame")
        RequiredProperties = @("Buffer", "BufferSize", "ImageData", "Width", "Height", "PixelFormat")
    },
    @{
        Vendor = "IDS Imaging"
        Sdk = "IDS peak ICV"
        AssemblyPath = $IdsPeakIcvAssembly
        TypeNames = @("IDSImaging.Peak.ICV.Types.Image")
        RequiredProperties = @("Data", "Width", "Height", "PixelFormat", "SizeInBytes")
    }
)

$results = @()
$hasFailure = $false
foreach ($contract in $contracts) {
    $requestedPath = [string]$contract.AssemblyPath
    if ([string]::IsNullOrWhiteSpace($requestedPath)) {
        $results += [ordered]@{
            vendor = $contract.Vendor
            sdk = $contract.Sdk
            status = "NotInstalled"
            assemblyPath = $null
            assemblyVersion = $null
            sha256 = $null
            typeName = $null
            requiredProperties = $contract.RequiredProperties
            missingProperties = @()
        }
        if ($RequireAll) {
            $hasFailure = $true
        }
        continue
    }

    if (-not (Test-Path -LiteralPath $requestedPath)) {
        $hasFailure = $true
        $results += [ordered]@{
            vendor = $contract.Vendor
            sdk = $contract.Sdk
            status = "AssemblyNotFound"
            assemblyPath = $requestedPath
            assemblyVersion = $null
            sha256 = $null
            typeName = $null
            requiredProperties = $contract.RequiredProperties
            missingProperties = $contract.RequiredProperties
        }
        continue
    }

    $resolvedPath = (Resolve-Path -LiteralPath $requestedPath).Path
    try {
        $assembly = [Reflection.Assembly]::LoadFrom($resolvedPath)
        $type = $null
        foreach ($typeName in $contract.TypeNames) {
            $type = $assembly.GetType($typeName, $false, $false)
            if ($type) {
                break
            }
        }

        $missing = @()
        if ($type) {
            foreach ($propertyName in $contract.RequiredProperties) {
                if (-not $type.GetProperty(
                    $propertyName,
                    [Reflection.BindingFlags]"Instance,Public")) {
                    $missing += $propertyName
                }
            }
        }
        else {
            $missing = $contract.RequiredProperties
        }

        $status = if ($type -and $missing.Count -eq 0) { "Passed" } else { "ContractMismatch" }
        if ($status -ne "Passed") {
            $hasFailure = $true
        }

        $results += [ordered]@{
            vendor = $contract.Vendor
            sdk = $contract.Sdk
            status = $status
            assemblyPath = $resolvedPath
            assemblyVersion = $assembly.GetName().Version.ToString()
            sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
            typeName = if ($type) { $type.FullName } else { $null }
            requiredProperties = $contract.RequiredProperties
            missingProperties = $missing
        }
    }
    catch {
        $hasFailure = $true
        $results += [ordered]@{
            vendor = $contract.Vendor
            sdk = $contract.Sdk
            status = "LoadFailed"
            assemblyPath = $resolvedPath
            assemblyVersion = $null
            sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
            typeName = $null
            requiredProperties = $contract.RequiredProperties
            missingProperties = $contract.RequiredProperties
            error = $_.Exception.Message
        }
    }
}

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    evidenceScope = "Assembly metadata only; this does not prove camera acquisition, buffer lifetime, hardware, or driver behavior."
    requireAll = [bool]$RequireAll
    results = $results
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

if ($hasFailure) {
    throw "One or more industrial camera SDK assembly contracts were missing or did not match. Report: $OutputPath"
}

Write-Host "Industrial camera SDK assembly contracts passed for all discovered/provided SDKs."
Write-Host "Report: $OutputPath"

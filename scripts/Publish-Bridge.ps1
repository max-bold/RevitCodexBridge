[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $DeployName = ('v' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string] $AddinsDirectory = "$env:APPDATA\Autodesk\Revit\Addins\2025",
    [switch] $SkipManifest
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'RevitCodexBridge.csproj'
$deployPath = Join-Path (Join-Path $repoRoot 'deploy') $DeployName
$assemblyPath = Join-Path $deployPath 'RevitCodexBridge.dll'

& dotnet publish $projectPath -c $Configuration -o $deployPath | ForEach-Object { Write-Host $_ }

$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$runtimeDependencies = @{
    'Microsoft.CodeAnalysis.dll' = @(
        Join-Path $deployPath 'Microsoft.CodeAnalysis.dll'
        Join-Path $nugetRoot 'microsoft.codeanalysis.common\4.11.0\lib\net8.0\Microsoft.CodeAnalysis.dll'
    )
    'Microsoft.CodeAnalysis.CSharp.dll' = @(
        Join-Path $deployPath 'Microsoft.CodeAnalysis.CSharp.dll'
        Join-Path $nugetRoot 'microsoft.codeanalysis.csharp\4.11.0\lib\net8.0\Microsoft.CodeAnalysis.CSharp.dll'
    )
    'System.Collections.Immutable.dll' = @(
        Join-Path $deployPath 'System.Collections.Immutable.dll'
        Join-Path $nugetRoot 'system.collections.immutable\8.0.0\lib\net8.0\System.Collections.Immutable.dll'
    )
    'System.Reflection.Metadata.dll' = @(
        Join-Path $deployPath 'System.Reflection.Metadata.dll'
        Join-Path $nugetRoot 'system.reflection.metadata\8.0.0\lib\net8.0\System.Reflection.Metadata.dll'
    )
}

foreach ($dependency in $runtimeDependencies.GetEnumerator()) {
    $targetPath = Join-Path $deployPath $dependency.Key
    if (Test-Path -LiteralPath $targetPath) {
        continue
    }

    $sourcePath = $dependency.Value | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $sourcePath) {
        throw "Could not find runtime dependency '$($dependency.Key)'. Run 'dotnet restore' and try again."
    }

    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
}

if (-not $SkipManifest) {
    New-Item -ItemType Directory -Path $AddinsDirectory -Force | Out-Null
    $manifestPath = Join-Path $AddinsDirectory 'RevitCodexBridge.addin'
    $manifest = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit Codex Bridge</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>8C2D37A6-4B54-4A09-8B36-76243248670E</AddInId>
    <FullClassName>RevitCodexBridge.BridgeApplication</FullClassName>
    <VendorId>CODX</VendorId>
    <VendorDescription>Local Codex bridge for Revit API debugging</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8
}

[pscustomobject]@{
    deployPath = $deployPath
    assemblyPath = $assemblyPath
    manifestPath = if ($SkipManifest) { $null } else { Join-Path $AddinsDirectory 'RevitCodexBridge.addin' }
} | ConvertTo-Json -Depth 5

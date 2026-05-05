[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $RevitVersion = '2025',
    [string] $RevitExe,
    [string] $Configuration = 'Release',
    [string] $DeployName = ('v' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch] $ValidateOnly,
    [switch] $SkipManifest
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'RevitCodexBridge.csproj'
$defaultRevitExe = "C:\Program Files\Autodesk\Revit $RevitVersion\Revit.exe"
$resolvedRevitExe = if ([string]::IsNullOrWhiteSpace($RevitExe)) { $defaultRevitExe } else { $RevitExe }
$addinsDirectory = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"

function Get-DotNetSdkVersion {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        return $null
    }

    $sdks = & dotnet --list-sdks
    $sdk = $sdks | Where-Object { $_ -match '^8\.' } | Select-Object -First 1
    return $sdk
}

$checks = [ordered]@{
    repository = Test-Path -LiteralPath $projectPath
    dotnetSdk8 = [bool](Get-DotNetSdkVersion)
    revitExe = Test-Path -LiteralPath $resolvedRevitExe
    addinsParent = Test-Path -LiteralPath (Split-Path -Parent $addinsDirectory)
}

$failed = $checks.GetEnumerator() | Where-Object { -not $_.Value } | Select-Object -ExpandProperty Key
$status = [pscustomobject]@{
    ok = -not $failed
    failed = @($failed)
    repoRoot = $repoRoot
    projectPath = $projectPath
    revitExe = $resolvedRevitExe
    addinsDirectory = $addinsDirectory
    dotnetSdk8 = Get-DotNetSdkVersion
}

if ($ValidateOnly) {
    $status | ConvertTo-Json -Depth 5
    if ($failed) {
        exit 1
    }
    exit 0
}

if ($failed) {
    $status | ConvertTo-Json -Depth 5
    throw "Cannot install RevitCodexBridge. Failed checks: $($failed -join ', ')"
}

if ($PSCmdlet.ShouldProcess($addinsDirectory, "Install RevitCodexBridge for Revit $RevitVersion")) {
    & dotnet restore $projectPath | ForEach-Object { Write-Host $_ }

    $publishJson = & "$PSScriptRoot\Publish-Bridge.ps1" `
        -Configuration $Configuration `
        -DeployName $DeployName `
        -AddinsDirectory $addinsDirectory `
        -SkipManifest:$SkipManifest
    $publish = $publishJson | ConvertFrom-Json

    [pscustomobject]@{
        ok = $true
        revitVersion = $RevitVersion
        revitExe = $resolvedRevitExe
        deployPath = $publish.deployPath
        assemblyPath = $publish.assemblyPath
        manifestPath = $publish.manifestPath
        nextStep = 'Restart Revit so it loads the installed add-in.'
    } | ConvertTo-Json -Depth 5
}

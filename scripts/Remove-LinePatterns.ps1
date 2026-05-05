[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [string] $Prefix,

    [switch] $DryRun,

    [string] $HostName = '127.0.0.1',
    [int] $Port = 17825
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\RevitBridge.psm1" -Force

$isDryRun = [bool] $DryRun
if (-not $isDryRun -and -not $PSCmdlet.ShouldProcess("line patterns starting with '$Prefix'", 'Delete')) {
    return
}

Invoke-RevitBridgeCommand -Command 'delete-line-patterns' -Body @{
    prefix = $Prefix
    dryRun = $isDryRun
} -HostName $HostName -Port $Port | ConvertTo-Json -Depth 20


[CmdletBinding()]
param(
    [string] $Category,
    [int] $Limit = 25,
    [string] $HostName = '127.0.0.1',
    [int] $Port = 17825
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\RevitBridge.psm1" -Force

$body = @{ limit = $Limit }
if (-not [string]::IsNullOrWhiteSpace($Category)) {
    $body['category'] = $Category
}

Invoke-RevitBridgeCommand -Command 'collect' -Body $body -HostName $HostName -Port $Port | ConvertTo-Json -Depth 20


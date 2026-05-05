[CmdletBinding()]
param(
    [string] $HostName = '127.0.0.1',
    [int] $Port = 17825
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\RevitBridge.psm1" -Force

Test-RevitBridge -HostName $HostName -Port $Port | ConvertTo-Json -Depth 10


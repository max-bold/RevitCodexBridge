[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Code,

    [string] $HostName = '127.0.0.1',
    [int] $Port = 17825,
    [int] $TimeoutSec = 120
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\RevitBridge.psm1" -Force

Invoke-RevitCSharp -Code $Code -HostName $HostName -Port $Port -TimeoutSec $TimeoutSec | ConvertTo-Json -Depth 20


[CmdletBinding()]
param(
    [string] $HostName = '127.0.0.1',
    [int] $Port = 17825,
    [int] $TimeoutSec = 600,
    [int] $PollIntervalSec = 10
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\RevitBridge.psm1" -Force

Wait-RevitBridgeDocument -HostName $HostName -Port $Port -TimeoutSec $TimeoutSec -PollIntervalSec $PollIntervalSec |
    ConvertTo-Json -Depth 10


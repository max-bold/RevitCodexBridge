$ErrorActionPreference = 'Stop'

function Invoke-RevitBridgeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,

        [hashtable] $Body = @{},

        [string] $HostName = '127.0.0.1',

        [int] $Port = 17825,

        [int] $TimeoutSec = 120
    )

    $payload = @{} + $Body
    $payload['command'] = $Command

    $json = $payload | ConvertTo-Json -Compress -Depth 20
    $uri = "http://${HostName}:${Port}/command"
    Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' -Body $json -TimeoutSec $TimeoutSec
}

function Test-RevitBridge {
    [CmdletBinding()]
    param(
        [string] $HostName = '127.0.0.1',
        [int] $Port = 17825,
        [int] $TimeoutSec = 5
    )

    Invoke-RestMethod -Uri "http://${HostName}:${Port}/health" -Method Get -TimeoutSec $TimeoutSec
}

function Wait-RevitBridgeDocument {
    [CmdletBinding()]
    param(
        [string] $HostName = '127.0.0.1',
        [int] $Port = 17825,
        [int] $TimeoutSec = 600,
        [int] $PollIntervalSec = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $last = $null

    while ((Get-Date) -lt $deadline) {
        try {
            [void](Test-RevitBridge -HostName $HostName -Port $Port -TimeoutSec 3)
            $response = Invoke-RevitBridgeCommand -Command 'doc-info' -HostName $HostName -Port $Port -TimeoutSec 30
            $last = $response
            if ($response.ok -and $response.data.hasDocument) {
                return $response
            }
        }
        catch {
            $last = $_.Exception.Message
        }

        Start-Sleep -Seconds $PollIntervalSec
    }

    throw "Timed out waiting for an active Revit document. Last response: $($last | ConvertTo-Json -Compress -Depth 10)"
}

function Invoke-RevitCSharp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Code,

        [string] $HostName = '127.0.0.1',

        [int] $Port = 17825,

        [int] $TimeoutSec = 120
    )

    Invoke-RevitBridgeCommand -Command 'run-csharp' -Body @{ code = $Code } -HostName $HostName -Port $Port -TimeoutSec $TimeoutSec
}

Export-ModuleMember -Function Invoke-RevitBridgeCommand, Test-RevitBridge, Wait-RevitBridgeDocument, Invoke-RevitCSharp


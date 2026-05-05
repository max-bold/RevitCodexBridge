[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ModelPath,

    [string] $RevitExe = 'C:\Program Files\Autodesk\Revit 2025\Revit.exe',
    [string] $Language = 'ENU',
    [switch] $KillExisting
)

$ErrorActionPreference = 'Stop'

if ($KillExisting) {
    Get-Process Revit -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

$arguments = "/language $Language `"$ModelPath`""
Start-Process -FilePath $RevitExe -ArgumentList $arguments


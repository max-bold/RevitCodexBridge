[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ModelPath,

    [string] $RevitExe = 'C:\Program Files\Autodesk\Revit 2025\Revit.exe',
    [string] $Language = 'ENU'
)

$ErrorActionPreference = 'Stop'

& "$PSScriptRoot\Start-RevitWithModel.ps1" -ModelPath $ModelPath -RevitExe $RevitExe -Language $Language -KillExisting


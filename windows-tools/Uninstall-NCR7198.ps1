[CmdletBinding()]
param([string]$QueueName = 'NCR 7198 Receipt')

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run Windows PowerShell as Administrator, then run this uninstaller again.'
}

if (Get-Printer -Name $QueueName -ErrorAction SilentlyContinue) {
    Remove-Printer -Name $QueueName
    Write-Host "Removed Windows printer queue '$QueueName'." -ForegroundColor Green
} else {
    Write-Host "The queue '$QueueName' is not installed; no changes were made."
}


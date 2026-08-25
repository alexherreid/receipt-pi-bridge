[CmdletBinding()]
param(
    [string]$QueueName = 'NCR 7198 Receipt',
    [string]$PortName
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run Windows PowerShell as Administrator, then run this installer again.'
    }
}

Assert-Administrator

Write-Host 'NCR 7198 Windows queue installer' -ForegroundColor Cyan
Write-Host 'Supported paths: NonION (PRTR) on USB00x, or EPiC via a signed Edgeport COMx driver.'

if (Get-Printer -Name $QueueName -ErrorAction SilentlyContinue) {
    $existing = Get-Printer -Name $QueueName
    Write-Host "The queue already exists: $QueueName" -ForegroundColor Yellow
    Write-Host "Port: $($existing.PortName)  Driver: $($existing.DriverName)"
    Write-Host 'No changes were made.'
    exit 0
}

$usbPorts = @(Get-PrinterPort | Where-Object { $_.Name -match '^USB\d+$' })

if ($PortName) {
    if ($PortName -match '^COM\d+$') {
        $PortName = $PortName + ':'
    }

    if ($PortName -notmatch '^(USB\d+|COM\d+:)$') {
        throw "Port '$PortName' is not a USB00x or COMx: printer port."
    }

    if ($PortName -match '^COM\d+:$') {
        $comDevice = Get-CimInstance Win32_SerialPort -ErrorAction SilentlyContinue |
            Where-Object { ($_.DeviceID + ':') -ieq $PortName }
        if (-not $comDevice) {
            throw "Serial port '$PortName' is not active. Install the signed NCR/Digi Edgeport driver, reconnect the printer, and check Device Manager."
        }
        Write-Host "Detected serial device: $($comDevice.Name)"
    }

    if (-not (Get-PrinterPort -Name $PortName -ErrorAction SilentlyContinue)) {
        Write-Host "Creating local printer port $PortName..."
        Add-PrinterPort -Name $PortName
    }
} elseif ($usbPorts.Count -eq 1) {
    $PortName = $usbPorts[0].Name
    Write-Host "Detected USB printer port: $PortName"
} elseif ($usbPorts.Count -eq 0) {
    Write-Host ''
    Write-Host 'Detected related Plug-and-Play devices:' -ForegroundColor Yellow
    Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -match 'NCR|7198|Receipt|USB Printing' } |
        Format-Table Status, Class, FriendlyName, InstanceId -AutoSize
    throw 'No USB printer port was found. If Device Manager says EPiC 7198, install the signed Edgeport driver and rerun with -PortName COMx:. Otherwise verify the diagnostic form says NonION (PRTR).'
} else {
    Write-Host 'More than one USB printer port exists:' -ForegroundColor Yellow
    $usbPorts | Format-Table Name, Description -AutoSize
    throw 'Run this script again with -PortName USB00x for the NCR printer.'
}

$driverName = 'Generic / Text Only'
if (-not (Get-PrinterDriver -Name $driverName -ErrorAction SilentlyContinue)) {
    Write-Host "Installing the Windows built-in '$driverName' component..."
    Add-PrinterDriver -Name $driverName
}

Write-Host "Creating '$QueueName' on $PortName..."
Add-Printer -Name $QueueName -DriverName $driverName -PortName $PortName

try {
    Set-Printer -Name $QueueName -PrintProcessor 'winprint'
} catch {
    Write-Warning "Queue created, but the print processor could not be set explicitly: $($_.Exception.Message)"
}

$created = Get-Printer -Name $QueueName
Write-Host ''
Write-Host 'Installation complete.' -ForegroundColor Green
$created | Format-List Name, DriverName, PortName, PrinterStatus
Write-Host 'Run .\Test-NCR7198.ps1 to print a receipt and test the cutter.'

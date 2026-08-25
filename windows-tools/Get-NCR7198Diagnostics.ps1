[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

Write-Host 'NCR 7198 Windows diagnostics' -ForegroundColor Cyan
Write-Host ('Time: {0:yyyy-MM-dd HH:mm:ss}' -f (Get-Date))
Write-Host ('Windows: {0}' -f [Environment]::OSVersion.VersionString)
Write-Host ''

Write-Host 'Related Plug-and-Play devices' -ForegroundColor Cyan
$devices = Get-PnpDevice -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FriendlyName -match 'NCR|7198|EPiC|Edgeport|Receipt|USB Printing' -or
        $_.InstanceId -match 'EPIC|VID_0402|VID_05F9'
    }

if (-not $devices) {
    Write-Host 'No matching devices found.'
} else {
    foreach ($device in $devices) {
        Write-Host ''
        Write-Host ("{0} [{1}]" -f $device.FriendlyName, $device.Status)
        Write-Host ("Class:      {0}" -f $device.Class)
        Write-Host ("InstanceId: {0}" -f $device.InstanceId)
        $hardwareIds = (Get-PnpDeviceProperty -InstanceId $device.InstanceId `
            -KeyName 'DEVPKEY_Device_HardwareIds' -ErrorAction SilentlyContinue).Data
        if ($hardwareIds) {
            Write-Host 'Hardware IDs:'
            $hardwareIds | ForEach-Object { Write-Host "  $_" }
        }
        $problem = (Get-PnpDeviceProperty -InstanceId $device.InstanceId `
            -KeyName 'DEVPKEY_Device_ProblemCode' -ErrorAction SilentlyContinue).Data
        if ($null -ne $problem) { Write-Host "Problem code: $problem" }
    }
}

Write-Host ''
Write-Host 'Active serial ports' -ForegroundColor Cyan
Get-CimInstance Win32_SerialPort -ErrorAction SilentlyContinue |
    Format-Table DeviceID, Name, PNPDeviceID -AutoSize

Write-Host 'Windows printer ports' -ForegroundColor Cyan
Get-PrinterPort -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^(USB\d+|COM\d+:)$' } |
    Format-Table Name, Description -AutoSize

Write-Host 'Installed queues' -ForegroundColor Cyan
Get-Printer -ErrorAction SilentlyContinue |
    Format-Table Name, DriverName, PortName, PrinterStatus -AutoSize

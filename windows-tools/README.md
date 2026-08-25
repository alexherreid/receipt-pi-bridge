# NCR 7198 Windows USB print package

This package creates a normal Windows print queue for an NCR 7198 connected by
USB and provides a raw-print utility for custom receipt software. It supports
both NCR USB personalities after Windows has a working device-level driver:

- `NonION (PRTR)`: native Windows USB printer port (`USB00x`)
- `ION (EPiC)`: NCR/Digi Edgeport virtual serial port (`COMx`)

It deliberately uses Microsoft's signed, built-in **Generic / Text Only**
printer component. It does not install an unsigned kernel driver and does not
change printer firmware.

## Requirements

- Windows 10, 64-bit or 32-bit
- PowerShell 5.1 or later
- Administrator access for installation
- NCR 7198 in `PRTR` mode, or the signed NCR/Digi Edgeport driver installed for
  an `EPiC` printer
- Printer powered separately and connected with a standard USB 2.0 cable

To print the printer's diagnostic form, open the receipt cover, hold the paper
feed button, and close the cover while continuing to hold the button. Check the
`USB Type` line. This package cannot communicate with `NHPI` mode.

## Your printer shows "EPiC 7198" in Device Manager

EPiC is NCR's USB virtual-serial interface, not USB Printer Class. Windows must
first load the signed NCR/Digi Edgeport driver so that Device Manager shows an
EPiC `COM` port. This package cannot safely replace that signed USB kernel
driver.

1. Disconnect the printer's USB cable.
2. Install the archived Digi Edgeport 6.05 driver as Administrator. NCR printer
   documentation refers to this as the **USB Virtual COM Port Driver** or
   **Edgeport Driver**. It is stored at
   `..\vendor-files\digi-edgeport\Edgeport-Family-Driver-6.05.exe`.
3. Reconnect and power on the printer.
4. In Device Manager, expand **Ports (COM & LPT)** and note the assigned `COMx`.
5. Run this package's installer with that port, including the colon:

```powershell
.\Install-NCR7198.ps1 -PortName COM4:
```

If the generic Digi package does not claim the device, use Device Manager >
EPiC 7198 > Update driver > Browse my computer and select the Edgeport Driver
folder supplied by NCR. Run `Get-NCR7198Diagnostics.ps1` and retain its output
when requesting the matching signed driver from NCR support.

## Install

1. Connect and power on the printer.
2. Extract this ZIP.
3. Right-click **Windows PowerShell** and choose **Run as administrator**.
4. Change into the extracted directory.
5. Run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NCR7198.ps1
```

For PRTR mode, the installer automatically uses the USB printer port when
exactly one is available. If several USB printer ports exist, list them and
specify the one for the NCR printer:

```powershell
Get-PrinterPort | Format-Table Name, Description
.\Install-NCR7198.ps1 -PortName USB002
```

Then open **Control Panel > Devices and Printers**, right-click **NCR 7198
Receipt**, choose **Printer properties**, and print a test page if desired.
Windows test pages are formatted by the generic text driver and are less useful
than the included receipt test.

## Receipt test

```powershell
.\Test-NCR7198.ps1
```

To omit the cut command:

```powershell
.\Test-NCR7198.ps1 -NoCut
```

The cut sequence is `GS V 65 0` (`1D 56 41 00`). This exact command was
physically verified on the NCR 7198 used for this project.

## Use from custom software

The Windows queue name is `NCR 7198 Receipt`. Applications can use either:

- normal Windows printing for plain text; or
- a RAW spooler job for NCR receipt commands, cutter commands, barcodes, and
  precise formatting.

`src/Ncr7198RawPrinter.cs` is a reusable C# class showing the RAW spooler API.
It has no NCR or third-party library dependency.

Example:

```csharp
byte[] receipt =
[
    0x10,                         // clear/reset printer state
    .. Encoding.ASCII.GetBytes("Hello from NCR 7198\r\n\r\n\r\n"),
    0x1D, 0x56, 0x41, 0x00        // feed to cutter and cut
];

Ncr7198RawPrinter.Send("NCR 7198 Receipt", receipt, "Receipt");
```

If your program uses Unicode strings, convert receipt text to the printer's
configured code page before sending. Do not send UTF-8 unless the configured
printer emulation explicitly supports it.

## Troubleshooting

### No `USB00x` port is found

Check Device Manager. In PRTR mode the printer should appear under **Printers**,
**Print queues**, or as **USB Printing Support**. If it appears as an EPiC or
HID device, the printer is not in PRTR mode. A cable alone cannot change this.

If it appears as `EPiC 7198`, install the signed Edgeport virtual-COM driver and
then run the installer with `-PortName COMx:` as described above.

### Job remains in the queue

- Confirm the printer is powered and has paper.
- Clear paused/offline status in the Windows queue.
- Try a different direct USB port rather than a hub.
- Verify the selected `USB00x` port belongs to the NCR printer.

### Text prints but cut does not

Run `Test-NCR7198.ps1` rather than printing the script as a document. Receipt
commands must be submitted with spooler datatype `RAW`; a normal application
may escape or transform the bytes.

## Remove

Run PowerShell as administrator:

```powershell
.\Uninstall-NCR7198.ps1
```

This removes only the `NCR 7198 Receipt` queue. It leaves the Microsoft driver
and USB port installed because other printers may use them.

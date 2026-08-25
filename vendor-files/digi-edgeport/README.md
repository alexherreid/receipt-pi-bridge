# Digi Edgeport Windows archive

Archived from Digi's official support site on August 25, 2026 for internal recovery and testing with the NCR 7198 EPiC USB interface.

## Archived package

| Item | Value |
| --- | --- |
| Product | Digi Edgeport Family Drivers |
| Version | 6.05 |
| Vendor date | December 1, 2021 |
| Installer | `Edgeport-Family-Driver-6.05.exe` |
| Installer size | 2,711,864 bytes |
| Supported by release notes | Windows 7/8.1/10/11; Server 2012 R2/2016/2019 |

The executable is the vendor's original self-extracting package. It contains both 32-bit and 64-bit setup programs, drivers, catalog/INF files, and the Edgeport configuration utility. Do not modify or re-sign it.

## Windows installation

1. Disconnect the NCR printer's USB cable from the PC.
2. Sign in with a Windows administrator account.
3. If upgrading an older Edgeport installation, use the existing Edgeport Configuration Utility to save the configuration, uninstall the old driver, and reboot.
4. Right-click `Edgeport-Family-Driver-6.05.exe` and select **Run as administrator**.
5. Complete the Digi setup, then connect the powered printer by USB.
6. Open **Device Manager > Ports (COM & LPT)** and record the assigned COM port.
7. Open **Digi USB > Edgeport Configuration Utility** and confirm that the EPiC/Edgeport device and serial port appear.

The official Digi instructions say to leave the USB device disconnected while beginning installation and require administrator rights. A COM-port reassignment requires a Windows reboot.

## Integrity

Run from PowerShell in this directory:

```powershell
Get-FileHash .\Edgeport-Family-Driver-6.05.exe -Algorithm SHA256
Get-FileHash .\Edgeport-6.05-Release-Notes.pdf -Algorithm SHA256
Get-FileHash .\Edgeport-Installation-Guide.pdf -Algorithm SHA256
```

Compare the results with `CHECKSUMS.sha256`. Windows should also show Digi International as the signer when the installer's digital-signature properties are inspected. Stop if the hash or signature is unexpected.

## Licensing

Digi International retains all rights to the installer and documentation. The files are preserved here unmodified for internal use with Digi/Edgeport hardware. Review the current Digi terms and the package's presented license before installation or redistribution. Keep this archive out of a public repository unless redistribution is explicitly permitted.

## Official sources

- Driver/support page: <https://hub.digi.com/support/products/infrastructure-management/digi-edgeport-usb-to-serial-converters/?path=/support/asset-collection/edgeport-os-specific-drivers/>
- Windows installation article: <https://www.digi.com/support/knowledge-base/how-to-install-the-edgeport-driver-in-windows>
- Installation guide: <https://docs.digi.com/resources/documentation/digidocs/pdfs/90000409.pdf>
- Digi legal terms: <https://www.digi.com/legal/terms>

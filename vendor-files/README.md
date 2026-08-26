# Vendor files

This directory is an internal recovery archive for software and documentation needed to use the NCR 7198's EPiC/Edgeport USB interface from Windows.

Vendor files are kept separate from the application source because they remain the property of their respective vendors and have their own license terms. They are not built into or executed by the Raspberry Pi bridge.

Keep a repository containing these binaries private unless the applicable vendor license or written permission allows public redistribution. When refreshing a file, download it only from the official vendor site, retain the original unmodified package, update its checksum and retrieval date, and review the current license terms.

## Contents

- `digi-edgeport/Edgeport-Family-Driver-6.05.exe`: official Digi Edgeport Windows driver package.
- `digi-edgeport/Edgeport-6.05-Release-Notes.pdf`: Digi release notes listing supported products and operating systems.
- `digi-edgeport/Edgeport-Installation-Guide.pdf`: Digi installation/configuration guide.
- `digi-edgeport/CHECKSUMS.sha256`: SHA-256 hashes for the archived files.
- `digi-edgeport/*.url`: shortcuts to the current official support and installation pages.

The current bridge application uses the udev alias `/dev/ncr7198` on Linux and a safe file-backed transport on Windows. Installing the Edgeport driver creates the Windows COM port, but direct Windows COM printing still requires a serial-port transport to be added to the application.

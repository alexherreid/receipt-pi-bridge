# NCR 7198 Raspberry Pi setup

This guide starts with an unconfigured Raspberry Pi and an NCR 7198 printer. It installs the receipt bridge as a restricted systemd service and gives the printer the stable device name `/dev/ncr7198`.

The verified development printer is NCR `7198-2003-9001`. Its USB interface must be **ION (EPiC)**. In that mode Linux exposes the NCR/Digi Edgeport interface as a serial device; the installer identifies it by USB VID `0404` and PID `0312`.

## 1. Gather the hardware

- Raspberry Pi 4 or newer
- Raspberry Pi power supply
- 16 GB or larger microSD card
- Wired Ethernet, or credentials for a 2.4/5 GHz Wi-Fi network supported by the Pi
- NCR 7198, its power supply, receipt paper, and a USB Type-A to Type-B data cable
- A Windows development PC with Git, the .NET 8 SDK, PowerShell, SSH, and SCP

Keep the Pi and the computers that may print on a trusted LAN. The bridge intentionally has no authentication and must not be forwarded to the public internet.

## 2. Put the printer in ION (EPiC) USB mode

Load paper, connect the printer power supply, and print its diagnostic/configuration form according to the [NCR 7198 Owner's Guide](https://onlinehelp.ncrvoyix.com/Retail/Printers/LandingPage/7198/1736.pdf). Confirm that the USB type is **ION (EPiC)**.

If it is not EPiC, use the printer's paper-feed configuration menu:

1. Power the printer off.
2. On the bottom of the printer, set DIP switch 1 to **ON** and DIP switch 2 to **OFF**.
3. Hold the Receipt Feed button while disconnecting and reconnecting DC power. The printer should print its current configuration and menu.
4. A short Feed click selects/counts; holding Feed for more than one second confirms a selection.
5. Select **Set Communication Interface**. On the verified menu this is three short clicks followed by a long click.
6. Follow the printed prompts to **Set USB Interface Type**, then choose **ION (EPiC)**. On the verified menu this is one short click followed by a long click.
7. Save the new parameters and exit. Treat the counts printed by the printer as authoritative if they differ from this guide.
8. Power off, return both DIP switches to **OFF**, and power on normally.

Do not select NonION (PRTR) or NHPI for this bridge. Those personalities do not create the serial device expected by the application.

## 3. Install Raspberry Pi OS

1. Install and open Raspberry Pi Imager on the Windows PC.
2. Select the Pi model and **Raspberry Pi OS Lite (64-bit)**.
3. In OS customization:
   - Set a hostname, for example `receipt-pi`.
   - Create a non-default username and strong password. This guide uses `pi` as a placeholder.
   - Enable SSH with password or public-key authentication.
   - Configure Wi-Fi, locale, and time zone if needed.
4. Write the image to the microSD card, insert it into the Pi, connect networking, and boot.
5. Find the Pi in the router's client list or try `ping receipt-pi.local`.

Connect over SSH and update the OS:

```bash
ssh pi@receipt-pi.local
sudo apt update
sudo apt full-upgrade -y
sudo apt install -y curl
sudo reboot
```

Reconnect and confirm that the 64-bit ARM architecture is active:

```bash
uname -m
```

The expected value is `aarch64`.

## 4. Connect and verify the printer

Power the NCR 7198 separately, connect its USB cable directly to the Pi, and run:

```bash
lsusb
ls -l /dev/ttyUSB*
sudo dmesg --ctime | tail -n 50
```

The important results are:

- `lsusb` includes device ID `0404:0312`.
- A device such as `/dev/ttyUSB0` exists.
- The kernel log shows the Edgeport/EPiC interface attaching to that tty.

If the USB ID is correct but no tty exists, inspect and load the kernel module:

```bash
lsmod | grep io_edgeport
sudo modprobe io_edgeport
sudo dmesg --ctime | tail -n 50
```

If the USB ID is absent, check printer power, USB mode, the cable, and the Pi USB port before continuing.

## 5. Build the Pi package on Windows

From the repository root in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Publish-Pi.ps1
```

This creates the self-contained ARM64 deployment in `publish\pi-arm64`. The Pi does not need the .NET runtime installed separately. The script stamps the deployment as `YYYY.MM.DD`, adding or incrementing `-1`, `-2`, and so on when publishing more than once that day. The health response and web page display this version.

Create the temporary destination, then copy the contents of the complete publish directory to it. Copying the contents prevents repeated deployments from accidentally creating `/tmp/ncr7198/pi-arm64`:

```powershell
ssh pi@receipt-pi.local "mkdir -p /tmp/ncr7198"
scp -r .\publish\pi-arm64\* pi@receipt-pi.local:/tmp/ncr7198/
```

Windows may not preserve the executable bit during this copy. That is expected; the installer applies mode `0755` when installing the application and startup script.

## 6. Install the bridge and stable USB alias

On the Pi:

```bash
ssh pi@receipt-pi.local
cd /tmp/ncr7198
sudo bash install-on-pi.sh
```

The installer:

1. Finds a connected `/dev/ttyUSB*` device with VID/PID `0404:0312`.
2. Installs `/etc/udev/rules.d/70-ncr7198.rules`.
3. Creates and verifies the stable alias `/dev/ncr7198`.
4. Creates the restricted `ncrprint` service account and grants serial access through `dialout`.
5. Installs the application under `/opt/ncr7198-bridge`.
6. Writes `/etc/ncr7198-bridge.env` with `Bridge__DevicePath=/dev/ncr7198`.
7. Enables and starts `ncr7198-bridge.service`.

The bridge service remains online when the printer is unplugged so `/api/health` and the web interface can report **Pi online** separately from printer availability. The udev rule recreates `/dev/ncr7198` and reapplies raw tty settings whenever the printer reconnects.

Verify the alias and service:

```bash
ls -l /dev/ncr7198
readlink -f /dev/ncr7198
sudo systemctl status ncr7198-bridge --no-pager
```

The resolved alias will commonly be `/dev/ttyUSB0`, but it remains `/dev/ncr7198` to the application even if Linux later enumerates it as `/dev/ttyUSB1`.

## 7. Verify health and use the web page

On the Pi:

```bash
curl --fail --show-error http://127.0.0.1/api/health
```

A fully ready response includes:

```json
{
  "service": "NCR 7198 Raspberry Pi Bridge",
  "transportMode": "Device",
  "transport": "/dev/ncr7198",
  "transportAvailable": true,
  "printerAvailable": true
}
```

From another trusted LAN computer, open:

```text
http://receipt-pi.local/
```

If using the separate local development web page, enter `http://receipt-pi.local` in **Bridge URL** and select **Save and connect**. The status should change to **Pi + printer online**.

Preview a receipt before selecting Print. The Pi may be online while `printerAvailable` is false; in that state the UI permits an attempted print, and the API returns a device error if the printer still cannot be opened.

## 8. Firewall and network address

The service listens on standard HTTP TCP port 80. If a firewall is already enabled on the Pi, allow the port only from the trusted LAN. Substitute the actual subnet before running this example:

```bash
sudo ufw allow from 192.168.1.0/24 to any port 80 proto tcp
sudo ufw status
```

Prefer a DHCP reservation in the router for the Pi. The `.local` hostname normally works through mDNS, but a reservation also provides a predictable fallback IP address.

## 9. Update the installed bridge

Build and copy a fresh `publish\pi-arm64` directory, then rerun the installer from that directory:

```bash
cd /tmp/ncr7198
sudo bash install-on-pi.sh
```

The installer replaces the application and rule, reloads udev, and restarts/enables the service without changing the Pi login or network configuration.

## 10. Troubleshooting

Follow live logs:

```bash
sudo journalctl -u ncr7198-bridge -f
```

Inspect the alias and its USB properties:

```bash
ls -l /dev/ncr7198
udevadm info --query=property --name=/dev/ncr7198 | grep -E 'ID_VENDOR_ID|ID_MODEL_ID'
```

Reload and test the rule after editing or replacing it:

```bash
sudo udevadm control --reload-rules
sudo udevadm trigger --action=add --subsystem-match=tty
sudo udevadm settle
ls -l /dev/ncr7198
```

If the alias is missing, unplug and reconnect the printer, then check `lsusb` and `dmesg` again. If the alias exists but health reports the printer unavailable, restart and inspect the service:

```bash
sudo systemctl restart ncr7198-bridge
sudo systemctl status ncr7198-bridge --no-pager
sudo journalctl -u ncr7198-bridge -n 100 --no-pager
```

Test only the bridge HTTP listener from another computer:

```powershell
Invoke-RestMethod http://receipt-pi.local/api/health
```

Do not expose port 80 through router port forwarding, a public reverse proxy, or an untrusted wireless network unless authentication is added first.

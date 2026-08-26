# NCR 7198 Print Bridge

Internal ASP.NET Core 8 Minimal API and web app for exposing an NCR 7198 receipt printer over a trusted LAN through a Raspberry Pi 4.

The printer model used to develop the bridge is NCR `7198-2003-9001`. It appears as an EPiC/Edgeport serial device; the Pi installer gives it the stable alias `/dev/ncr7198`.

For a new Pi and printer, follow [PI-SETUP.md](PI-SETUP.md) from imaging Raspberry Pi OS through printer configuration, installation, verification, updates, and troubleshooting.

## Features

- Serves a one-page receipt editor from the Pi on standard HTTP port 80 at `http://<pi-address>/`.
- Supports the same options from the web page and JSON API: content or literal lines, one optional top/bottom logo, pre-print feed, post-print feed, wrapping, compressed mode, cutting, copies, and optional `printId`.
- Provides `POST /api/preview` to validate and render a receipt without accessing the printer.
- Provides `POST /api/print` to queue, render, print, feed, and cut a receipt.
- Provides `GET /api/health` so a remote web page can distinguish a reachable Pi from an attached printer.
- Supports standard 44-column and compressed 56-column printing.
- Accepts printable ASCII `U+0020` through `U+007E`; content may also contain CR/LF line breaks.
- Serializes print jobs so bytes from separate requests cannot interleave.
- Limits outstanding unique print requests to three: one active and two waiting.
- Keeps completed `printId` values in memory for 24 hours to prevent accidental duplicate printing.
- Forces a cut after every copy when more than one copy is requested.
- Uses a safe file-backed transport on Windows so the complete web app can be tested without the physical printer.
- Includes the Digi Edgeport Windows driver archive and the PowerShell tools needed to create a normal Windows printer queue for direct PC use.
- Publishes as a self-contained `linux-arm64` application, so the Pi does not need a separately installed .NET runtime.

## Project layout

```text
Ncr7198.PiBridge.sln
PI-SETUP.md                      Ground-up Raspberry Pi and printer setup guide
src/Ncr7198.PiBridge/             ASP.NET Core web app, API, renderer, and transport
src/Ncr7198.PiBridge/wwwroot/     Browser interface
tests/Ncr7198.PiBridge.Tests/     Renderer and printId behavior tests
examples/                         C# and PowerShell API examples
scripts/                          Windows publish and Raspberry Pi install scripts
vendor-files/                     Internal vendor driver/documentation archive
windows-tools/                    Direct Windows queue installer, test, diagnostics, and RAW C# helper
```

The project has no Node.js frontend build. The page is plain HTML, CSS, and JavaScript served directly from `wwwroot`.

`vendor-files` includes the verified Digi Edgeport 6.05 Windows installer, release notes, installation guide, official-source shortcuts, hashes, and license/handling notes. Keep a repository containing the vendor binary private unless public redistribution is authorized.

`windows-tools` is a separate direct-PC path. With the printer connected to Windows by USB, install the archived Edgeport driver, then run `windows-tools\Install-NCR7198.ps1` as Administrator using the detected `COMx:` port. This creates an **NCR 7198 Receipt** queue using Windows' built-in Generic / Text Only driver. See `windows-tools\README.md` for the complete procedure and RAW C# example.

## Local development

Install the .NET 8 SDK, then run the app from the repository root:

```powershell
dotnet run --project .\src\Ncr7198.PiBridge\Ncr7198.PiBridge.csproj --launch-profile "NCR 7198 Web UI"
```

Open `http://localhost:9719` if the browser does not open automatically.

The development launch profile sets `Bridge__Transport=File`. Preview behaves exactly as it does on the Pi, while Print writes the raw NCR byte stream to:

```text
src\Ncr7198.PiBridge\printed-jobs\
```

These `.bin` files are deliberately excluded from Git. Receipt text and `printId` are not retained by the web page. Browser-local display settings are retained.

To use the local web interface with a Pi, enter the Pi bridge origin, for example `http://receipt-pi.local`, in **Bridge URL** and select **Save and connect**. Health, Preview, and Print then use that bridge directly. The address is retained only in browser local storage and can be changed at any time. The status distinguishes development file mode, a reachable Pi without a printer, and a reachable Pi with its printer.

Print is enabled whenever a Device-mode Pi bridge is reachable, even when that Pi currently reports its printer unavailable. It remains disabled while the Pi is offline, health is unknown, or the selected target is the local development file transport. On desktop, opening either preview moves it into a second column; smaller screens retain the stacked layout.

The editor opens in Lines mode with the literal receipt example. Content mode has its own paragraph example and word-wraps without enforcing a width in the editor; switching modes preserves each draft for the current page session. Lines mode preserves each entered row literally and enforces the selected 44- or 56-column printer width. Preview reports an approximate paper length using the printer's default 7.52 text lines per inch and 24-dot logo bands at 203 DPI, and **JSON Body POST Preview** validates then shows the exact request body that can be sent to `POST /api/print`. **Copy JSON** copies that validated body, including on ordinary LAN HTTP through a browser fallback. A failed validation clears and hides the corresponding prior preview.

Run the automated tests with:

```powershell
dotnet test .\Ncr7198.PiBridge.sln
```

## Visual Studio

Open `Ncr7198.PiBridge.sln` in Visual Studio 2022 with the **ASP.NET and web development** workload and .NET 8 SDK installed.

Set `Ncr7198.PiBridge` as the startup project, select the **NCR 7198 Web UI** launch profile, and press F5. Visual Studio opens the web interface at `http://localhost:9719` and uses the file-backed development transport.

Use **Test > Run All Tests** to run `Ncr7198.PiBridge.Tests`.

## API

The service intentionally has no authentication. Both POST endpoints accept `application/json` and the same request body.

### Health

```http
GET /api/health
```

`GET /health` remains available as a compatibility alias. A successful response proves the Pi bridge is reachable; `printerAvailable` separately reports whether the configured device transport can currently access the printer. The response's `version` uses the deployment identifier bundled on that Pi, such as `2026.08.25-1`.

The web page displays both its own bundled version and the version reported by the selected Pi. This makes a version mismatch visible when the interface is hosted independently from the bridge.

### Preview

```http
POST /api/preview
```

### Print

```http
POST /api/print
```

### Request body

```json
{
  "printId": "order-10042-customer",
  "prePrintLines": 0,
  "lines": [
    "MY STORE",
    "Widget                      $12.00"
  ],
  "content": null,
  "postPrintLines": 4,
  "wrap": "none",
  "compressed": false,
  "cut": true,
  "copies": 1,
  "logo": "data:image/png;base64,iVBORw0KGgo...",
  "logoPosition": "top"
}
```

`lines` and `content` are nullable. If `lines` is non-null, it wins and `content` is ignored. If `lines` is null, `content` is required.

`logo` is nullable and carries the image in the request. It accepts either a standard Base64 image data URL such as `data:image/png;base64,...` or raw Base64 image bytes. The JSON example abbreviates the Base64 and must be replaced with the complete value. PNG, JPEG, BMP, TGA, PSD, and GIF files are accepted; animated images use the first frame. The image is composited onto white, converted to monochrome, centered, and proportionally reduced to the 576-dot receipt width when necessary. Smaller images are not enlarged. `logoPosition` accepts `"top"` or `"bottom"` and defaults to `"top"`.

The web interface reads the chosen file into the request automatically. The image itself is not retained in browser storage; choose it again after reloading the page. `logoPosition` is retained with the other browser-local display settings.

Defaults:

| Field | Default |
| --- | --- |
| `prePrintLines` | `0` |
| `postPrintLines` | `4` |
| `wrap` | `"none"` |
| `compressed` | `false` |
| `cut` | `true` |
| `copies` | `1` |
| `logo` | `null` |
| `logoPosition` | `"top"` |

### Preview response

Preview returns only the rendered array. Empty strings represent feed lines and `[CUT]` represents a cut:

```json
[
  "MY STORE",
  "Widget                      $12.00",
  "",
  "",
  "",
  "",
  "[CUT]"
]
```

Preview performs the same validation and wrapping as Print, but it does not use the printer, queue, or `printId` cache.

### Print response

```json
{
  "status": "submitted",
  "printId": "order-10042-customer",
  "copies": 1,
  "requestedCut": true,
  "effectiveCut": true,
  "cutForced": false,
  "bytes": 82
}
```

The response is returned after all bytes for the request have been written to the configured transport. It confirms submission, not that paper physically printed.

Submitting the same effective output with the same `printId` returns `status: "deduplicated"` without printing again. Reusing that ID for different effective output returns HTTP 409. The cache is in memory, expires entries 24 hours after completion, and does not survive a service or Pi restart.

See `examples/CSharpClient.cs` and `examples/test-from-powershell.ps1` for complete calls.

## Validation rules

- `prePrintLines` and `postPrintLines` must each be from 0 through 10.
- `copies` must be from 1 through 3.
- More than one copy forces `cut=true` after every copy.
- `lines` must be a non-empty array when supplied. Entries cannot be null or contain tabs, CR, or LF.
- When `lines` is supplied, `wrap` must be `"none"`; spaces are printed literally.
- `content` must be non-empty when used. Its explicit lines are left-aligned.
- `wrap` accepts only `"none"` or `"word"`.
- `none` rejects a rendered line wider than 44 columns, or 56 columns when compressed.
- `word` is available only with `content`. It wraps at spaces, preserves explicit newlines, normalizes spaces between words, and splits an unbroken word across lines when necessary.
- Printable receipt characters are limited to ASCII `U+0020` through `U+007E`. Tabs, extended ASCII, emoji, smart punctuation, and other Unicode characters are rejected.
- Input receipt content is limited to 16,384 characters.
- Estimated physical output is limited to 8 inches. The calculation uses the printer's default 7.52 text lines per inch and 24-dot logo raster bands at 203 DPI, including explicit feeds and all copies.
- `printId` is optional, trimmed, case-sensitive, and limited to 128 characters.
- `logo` is optional, limited to one Base64-encoded image, 8 MB after Base64 decoding, and 8,192 pixels on either source dimension.
- `logoPosition` must be `"top"` or `"bottom"`, even when no logo is supplied.
- The fourth unique outstanding print request receives HTTP 429. A matching duplicate `printId` is resolved before queue capacity is checked.

Validation failures return HTTP 400 with a JSON `error` property. A reused `printId` conflict returns 409, a full queue returns 429, and a printer/device failure returns 503.

## Configuration

Settings are under `Bridge` in `src/Ncr7198.PiBridge/appsettings.json` and can be overridden with environment variables using ASP.NET Core's double-underscore notation.

| Setting | Pi value | Purpose |
| --- | --- | --- |
| `Bridge__DevicePath` | `/dev/ncr7198` | Stable udev alias for the EPiC/Edgeport serial device |
| `Bridge__ListenUrl` | `http://0.0.0.0:80` | Standard HTTP LAN listener on the Pi |
| `Bridge__Transport` | `Device` | Writes to the real printer; use `File` for development |
| `Bridge__DevelopmentOutputDirectory` | `printed-jobs` | Output directory for file-backed development prints |
| `Bridge__MaxOutstandingJobs` | `3` | Active plus waiting requests |
| `Bridge__PrintIdLifetimeHours` | `24` | In-memory duplicate window |

`GET /api/health` and its compatibility alias `GET /health` return the selected transport mode, whether that transport is available, and whether a real printer device is available.

## Raspberry Pi deployment

Create a self-contained Raspberry Pi 64-bit package from Windows:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Publish-Pi.ps1
```

Publishing stamps `wwwroot/version.txt` with `YYYY.MM.DD`. Additional publishes on the same date increment `-1`, `-2`, and so on. To reproduce or override a specific identifier, pass `-Version 2026.08.25-1`.

The output is written to `publish\pi-arm64`. Create the temporary destination and copy the directory contents to the Pi:

```powershell
ssh piuser@192.168.1.50 "mkdir -p /tmp/ncr7198"
scp -r .\publish\pi-arm64\* piuser@192.168.1.50:/tmp/ncr7198/
```

The installer does not require the copied source binary to retain its executable bit; it installs the application with mode `0755`.

Install it on the Pi:

```bash
ssh piuser@192.168.1.50
cd /tmp/ncr7198
sudo bash install-on-pi.sh
```

The installer verifies USB device `0404:0312`, installs a udev rule for `/dev/ncr7198`, creates the restricted `ncrprint` account, installs the application under `/opt/ncr7198-bridge`, configures the serial device in raw mode, and enables the `ncr7198-bridge` systemd service. The service stays online without the printer; reconnecting the printer recreates the alias and reapplies its raw tty settings. See [PI-SETUP.md](PI-SETUP.md) for the complete ground-up procedure.

Open `http://<pi-address>/` from another machine on the LAN.

## Operations

```bash
sudo systemctl status ncr7198-bridge
sudo systemctl restart ncr7198-bridge
sudo journalctl -u ncr7198-bridge -f
```

Check the device directly with:

```bash
ls -l /dev/ncr7198
readlink -f /dev/ncr7198
```

## Printer protocol notes

These command bytes were verified on the NCR 7198 used for this project:

| Operation | Bytes |
| --- | --- |
| Initialize/clear | `10` |
| Standard pitch, 44 columns | `1B 16 00` |
| Compressed pitch, 56 columns | `1B 16 01` |
| Feed to cutter and cut | `1D 56 41 00` |
| Center justification | `1B 61 01` |
| 24-dot double-density raster band | `1B 2A 21 nL nH ...` |

Each copy is emitted as initialize, pitch selection, pre-feed, receipt lines, post-feed, optional cut, and restore-standard-pitch.

The Linux shell's built-in `printf` may print `\xNN` text literally. For direct printer diagnostics, use `/usr/bin/printf` or POSIX octal escapes.

## Security and current limitations

- The service has intentionally no authentication and allows cross-origin API requests.
- Keep port 80 limited to the trusted LAN. Do not expose or forward it to the public internet.
- The bridge supports the printer's confirmed printable ASCII range, not emoji or arbitrary Unicode.
- `printId` history and queued jobs are not persistent across restarts.
- The printer does not provide reliable confirmation that paper physically printed.
- The Pi installer supports one NCR EPiC device with USB identity `0404:0312` and assigns it `/dev/ncr7198`.

## Development notes

The renderer in `ReceiptRenderer.cs` owns validation, wrapping, preview lines, printer bytes, and effective-job hashing. Keep Preview and Print routed through this single renderer so their behavior cannot drift.

`PrintCoordinator.cs` owns queue capacity, serialization, and the in-memory `printId` cache. Duplicate IDs must be checked before queue capacity, and an in-progress duplicate must await the original task instead of taking another queue slot.

`PrinterTransport.cs` selects the file-backed transport outside Linux when `Transport=Auto`; the Pi installer explicitly selects `Transport=Device`.

When debugging printer output, preserve the confirmed byte commands above and avoid replacing the EPiC serial transport with USB printer-class assumptions.

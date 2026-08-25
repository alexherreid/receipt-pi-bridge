# NCR 7198 Print Bridge

Internal ASP.NET Core 8 Minimal API and web app for exposing an NCR 7198 receipt printer over a trusted LAN through a Raspberry Pi 4.

The printer model used to develop the bridge is NCR `7198-2003-9001`. It appears as an EPiC/Edgeport serial device and is available on the Pi as `/dev/ttyUSB0`.

## Features

- Serves a one-page receipt editor at `http://<pi-address>:9719/`.
- Supports the same options from the web page and JSON API: content or literal lines, pre-print feed, post-print feed, wrapping, compressed mode, cutting, copies, and optional `printId`.
- Provides `POST /api/preview` to validate and render a receipt without accessing the printer.
- Provides `POST /api/print` to queue, render, print, feed, and cut a receipt.
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

Run the automated tests with:

```powershell
dotnet test .\Ncr7198.PiBridge.sln
```

## Visual Studio

Open `Ncr7198.PiBridge.sln` in Visual Studio 2022 with the **ASP.NET and web development** workload and .NET 8 SDK installed.

Set `Ncr7198.PiBridge` as the startup project, select the **NCR 7198 Web UI** launch profile, and press F5. Visual Studio opens the web interface at `http://localhost:9719` and uses the file-backed development transport.

Use **Test > Run All Tests** to run `Ncr7198.PiBridge.Tests`.

## API

The service intentionally has no authentication. Both endpoints accept `application/json` and the same request body.

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
  "copies": 1
}
```

`lines` and `content` are nullable. If `lines` is non-null, it wins and `content` is ignored. If `lines` is null, `content` is required.

Defaults:

| Field | Default |
| --- | --- |
| `prePrintLines` | `0` |
| `postPrintLines` | `4` |
| `wrap` | `"none"` |
| `compressed` | `false` |
| `cut` | `true` |
| `copies` | `1` |

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
- `word` is available only with `content`. It wraps at spaces, preserves explicit newlines, normalizes spaces between words, and rejects a single word wider than the selected mode.
- Printable receipt characters are limited to ASCII `U+0020` through `U+007E`. Tabs, extended ASCII, emoji, smart punctuation, and other Unicode characters are rejected.
- Input receipt content is limited to 16,384 characters.
- Final output is limited to 500 lines after wrapping, feeds, and copies.
- `printId` is optional, trimmed, case-sensitive, and limited to 128 characters.
- The fourth unique outstanding print request receives HTTP 429. A matching duplicate `printId` is resolved before queue capacity is checked.

Validation failures return HTTP 400 with a JSON `error` property. A reused `printId` conflict returns 409, a full queue returns 429, and a printer/device failure returns 503.

## Configuration

Settings are under `Bridge` in `src/Ncr7198.PiBridge/appsettings.json` and can be overridden with environment variables using ASP.NET Core's double-underscore notation.

| Setting | Pi value | Purpose |
| --- | --- | --- |
| `Bridge__DevicePath` | `/dev/ttyUSB0` | EPiC/Edgeport serial device |
| `Bridge__ListenUrl` | `http://0.0.0.0:9719` | LAN listener |
| `Bridge__Transport` | `Device` | Writes to the real printer; use `File` for development |
| `Bridge__DevelopmentOutputDirectory` | `printed-jobs` | Output directory for file-backed development prints |
| `Bridge__MaxOutstandingJobs` | `3` | Active plus waiting requests |
| `Bridge__PrintIdLifetimeHours` | `24` | In-memory duplicate window |

`GET /health` returns the selected transport and whether it is currently available.

## Raspberry Pi deployment

Create a self-contained Raspberry Pi 64-bit package from Windows:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Publish-Pi.ps1
```

The output is written to `publish\pi-arm64`. Copy that directory to the Pi:

```powershell
scp -r .\publish\pi-arm64 piuser@192.168.1.50:/tmp/ncr7198
```

Install it on the Pi:

```bash
ssh piuser@192.168.1.50
cd /tmp/ncr7198
sudo bash install-on-pi.sh
```

The installer verifies the printer device, creates the restricted `ncrprint` account, installs the application under `/opt/ncr7198-bridge`, configures the serial device in raw mode, and enables the `ncr7198-bridge` systemd service.

Open `http://<pi-address>:9719/` from another machine on the LAN.

## Operations

```bash
sudo systemctl status ncr7198-bridge
sudo systemctl restart ncr7198-bridge
sudo journalctl -u ncr7198-bridge -f
```

Check the device directly with:

```bash
ls -l /dev/ttyUSB0
```

## Printer protocol notes

These command bytes were verified on the NCR 7198 used for this project:

| Operation | Bytes |
| --- | --- |
| Initialize/clear | `10` |
| Standard pitch, 44 columns | `1B 16 00` |
| Compressed pitch, 56 columns | `1B 16 01` |
| Feed to cutter and cut | `1D 56 41 00` |

Each copy is emitted as initialize, pitch selection, pre-feed, receipt lines, post-feed, optional cut, and restore-standard-pitch.

The Linux shell's built-in `printf` may print `\xNN` text literally. For direct printer diagnostics, use `/usr/bin/printf` or POSIX octal escapes.

## Security and current limitations

- The service has intentionally no authentication and allows cross-origin API requests.
- Keep port 9719 limited to the trusted LAN. Do not expose or forward it to the public internet.
- The bridge supports the printer's confirmed printable ASCII range, not emoji or arbitrary Unicode.
- `printId` history and queued jobs are not persistent across restarts.
- The printer does not provide reliable confirmation that paper physically printed.
- The default device path assumes the printer remains `/dev/ttyUSB0`. Consider a persistent udev alias if the Pi will use multiple USB serial devices.

## Development notes

The renderer in `ReceiptRenderer.cs` owns validation, wrapping, preview lines, printer bytes, and effective-job hashing. Keep Preview and Print routed through this single renderer so their behavior cannot drift.

`PrintCoordinator.cs` owns queue capacity, serialization, and the in-memory `printId` cache. Duplicate IDs must be checked before queue capacity, and an in-progress duplicate must await the original task instead of taking another queue slot.

`PrinterTransport.cs` selects the file-backed transport outside Linux when `Transport=Auto`; the Pi installer explicitly selects `Transport=Device`.

When debugging printer output, preserve the confirmed byte commands above and avoid replacing the EPiC serial transport with USB printer-class assumptions.

[CmdletBinding()]
param([string]$Runtime = 'linux-arm64')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Ncr7198.PiBridge\Ncr7198.PiBridge.csproj'
$output = Join-Path $root 'publish\pi-arm64'

dotnet publish $project -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Copy-Item (Join-Path $PSScriptRoot 'install-on-pi.sh') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'start-bridge.sh') $output -Force

Write-Host "Pi deployment created at $output" -ForegroundColor Green
Write-Host 'Copy that entire directory to the Pi, then run: sudo bash install-on-pi.sh'


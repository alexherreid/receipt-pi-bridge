[CmdletBinding()]
param(
    [string]$Runtime = 'linux-arm64',
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Ncr7198.PiBridge\Ncr7198.PiBridge.csproj'
$output = Join-Path $root 'publish\pi-arm64'
$versionFile = Join-Path $root 'src\Ncr7198.PiBridge\wwwroot\version.txt'
$today = Get-Date -Format 'yyyy.MM.dd'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $current = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { '' }
    if ($current -eq $today) { $Version = "$today-1" }
    elseif ($current -match "^$([regex]::Escape($today))-(\d+)$") { $Version = "$today-$([int]$Matches[1] + 1)" }
    else { $Version = $today }
}
if ($Version -notmatch '^\d{4}\.\d{2}\.\d{2}(?:-\d+)?$') {
    throw 'Version must use YYYY.MM.DD or YYYY.MM.DD-N.'
}
Set-Content -Path $versionFile -Value $Version -Encoding ascii -NoNewline

dotnet publish $project -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Copy-Item (Join-Path $PSScriptRoot 'install-on-pi.sh') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'start-bridge.sh') $output -Force
Copy-Item (Join-Path $PSScriptRoot '70-ncr7198.rules') $output -Force
Copy-Item (Join-Path $root 'PI-SETUP.md') $output -Force

Write-Host "Pi deployment created at $output" -ForegroundColor Green
Write-Host "Deployment version: $Version" -ForegroundColor Green
Write-Host 'Copy that entire directory to the Pi, then run: sudo bash install-on-pi.sh'


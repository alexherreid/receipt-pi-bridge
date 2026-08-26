param([Parameter(Mandatory)] [string]$PiAddress)

$body = @{
    printId = "network-test-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
    prePrintLines = 0
    lines = @(
        'NCR 7198 NETWORK TEST',
        '--------------------------------------------',
        'Raspberry Pi bridge is working.'
    )
    content = $null
    postPrintLines = 4
    wrap = 'none'
    compressed = $false
    cut = $true
    copies = 1
    logo = $null
    logoPosition = 'top'
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$PiAddress/api/preview" -ContentType 'application/json' -Body $body
Invoke-RestMethod -Method Post -Uri "$PiAddress/api/print" -ContentType 'application/json' -Body $body

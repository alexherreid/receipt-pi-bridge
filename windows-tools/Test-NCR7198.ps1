[CmdletBinding()]
param(
    [string]$QueueName = 'NCR 7198 Receipt',
    [switch]$NoCut
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Printer -Name $QueueName -ErrorAction SilentlyContinue)) {
    throw "Windows printer queue '$QueueName' was not found. Run Install-NCR7198.ps1 first."
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class RawPrinter {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DOC_INFO_1 {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr printer, int level, [In] DOC_INFO_1 docInfo);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printer, byte[] bytes, int count, out int written);

    public static void Send(string printerName, byte[] bytes, string documentName) {
        IntPtr printer;
        if (!OpenPrinter(printerName, out printer, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenPrinter failed");
        try {
            var doc = new DOC_INFO_1 { pDocName = documentName, pDataType = "RAW" };
            if (StartDocPrinter(printer, 1, doc) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter failed");
            try {
                if (!StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter failed");
                try {
                    int written;
                    if (!WritePrinter(printer, bytes, bytes.Length, out written))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter failed");
                    if (written != bytes.Length)
                        throw new InvalidOperationException("Only " + written + " of " + bytes.Length + " bytes were written.");
                } finally { EndPagePrinter(printer); }
            } finally { EndDocPrinter(printer); }
        } finally { ClosePrinter(printer); }
    }
}
'@

$lines = @(
    'NCR 7198 RAW PRINT TEST',
    ('-' * 42),
    ('Computer: {0}' -f $env:COMPUTERNAME),
    ('Time:     {0:yyyy-MM-dd HH:mm:ss}' -f (Get-Date)),
    ('Queue:    {0}' -f $QueueName),
    '',
    'If this is readable, the Windows USB',
    'queue and RAW spooler path are working.',
    '', '', ''
)

$bytes = [Collections.Generic.List[byte]]::new()
$bytes.Add(0x10) # NCR clear printer command
$bytes.AddRange([Text.Encoding]::ASCII.GetBytes(($lines -join "`r`n")))

if (-not $NoCut) {
    $bytes.AddRange([byte[]](0x1D, 0x56, 0x41, 0x00)) # GS V 65 0: feed to cutter and cut
}

[RawPrinter]::Send($QueueName, $bytes.ToArray(), 'NCR 7198 receipt test')
Write-Host "Receipt submitted to '$QueueName'." -ForegroundColor Green

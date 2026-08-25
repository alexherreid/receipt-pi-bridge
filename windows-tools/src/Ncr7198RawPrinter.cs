using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>
/// Sends unmodified bytes to a Windows printer queue using spooler datatype RAW.
/// Suitable for NCR 7198 receipt commands when the queue uses USB PRTR mode.
/// </summary>
public static class Ncr7198RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDataType;
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

    public static void Send(string printerName, byte[] bytes, string documentName = "NCR 7198 receipt")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!OpenPrinter(printerName, out IntPtr printer, IntPtr.Zero))
            ThrowWin32("OpenPrinter failed");

        try
        {
            var doc = new DOC_INFO_1 { pDocName = documentName, pDataType = "RAW" };
            if (StartDocPrinter(printer, 1, doc) == 0)
                ThrowWin32("StartDocPrinter failed");

            try
            {
                if (!StartPagePrinter(printer))
                    ThrowWin32("StartPagePrinter failed");

                try
                {
                    if (!WritePrinter(printer, bytes, bytes.Length, out int written))
                        ThrowWin32("WritePrinter failed");
                    if (written != bytes.Length)
                        throw new InvalidOperationException($"Only {written} of {bytes.Length} bytes were written.");
                }
                finally { EndPagePrinter(printer); }
            }
            finally { EndDocPrinter(printer); }
        }
        finally { ClosePrinter(printer); }
    }

    private static void ThrowWin32(string message) =>
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);
}


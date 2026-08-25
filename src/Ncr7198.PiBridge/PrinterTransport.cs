namespace Ncr7198.PiBridge;

public interface IPrinterTransport
{
    string Description { get; }
    bool IsAvailable();
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

public sealed class PrinterTransport : IPrinterTransport
{
    private readonly BridgeOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PrinterTransport> _logger;

    public PrinterTransport(BridgeOptions options, IWebHostEnvironment environment, ILogger<PrinterTransport> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    private bool UseFileTransport =>
        _options.Transport.Equals("File", StringComparison.OrdinalIgnoreCase) ||
        (_options.Transport.Equals("Auto", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsLinux());

    public string Description => UseFileTransport
        ? $"Development files: {Path.GetFullPath(OutputDirectory)}"
        : _options.DevicePath;

    public bool IsAvailable()
    {
        if (UseFileTransport) return true;
        try
        {
            using var stream = OpenDevice();
            return stream.CanWrite;
        }
        catch { return false; }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (data.IsEmpty) throw new ArgumentException("Print data cannot be empty.", nameof(data));

        if (UseFileTransport)
        {
            Directory.CreateDirectory(OutputDirectory);
            var filename = $"receipt-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.bin";
            var path = Path.Combine(OutputDirectory, filename);
            await File.WriteAllBytesAsync(path, data.ToArray(), cancellationToken);
            _logger.LogInformation("Saved {ByteCount} printer bytes to {Path}", data.Length, path);
            return;
        }

        await using var stream = OpenDevice();
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        _logger.LogInformation("Submitted {ByteCount} bytes to {DevicePath}", data.Length, _options.DevicePath);
    }

    private string OutputDirectory => Path.IsPathRooted(_options.DevelopmentOutputDirectory)
        ? _options.DevelopmentOutputDirectory
        : Path.Combine(_environment.ContentRootPath, _options.DevelopmentOutputDirectory);

    private FileStream OpenDevice() => new(
        _options.DevicePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite,
        bufferSize: 4096, useAsync: true);
}

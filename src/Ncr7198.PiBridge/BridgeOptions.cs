namespace Ncr7198.PiBridge;

public sealed class BridgeOptions
{
    public string DevicePath { get; set; } = "/dev/ttyUSB0";
    public string ListenUrl { get; set; } = "http://0.0.0.0:9719";
    public string Transport { get; set; } = "Auto";
    public string DevelopmentOutputDirectory { get; set; } = "printed-jobs";
    public int MaxOutstandingJobs { get; set; } = 3;
    public int PrintIdLifetimeHours { get; set; } = 24;
}

namespace Ncr7198.PiBridge;

public sealed class BridgeOptions
{
    public string DevicePath { get; set; } = "/dev/ncr7198";
    public string ListenUrl { get; set; } = "http://0.0.0.0:80";
    public string Transport { get; set; } = "Auto";
    public string DevelopmentOutputDirectory { get; set; } = "printed-jobs";
    public int MaxOutstandingJobs { get; set; } = 3;
    public int PrintIdLifetimeHours { get; set; } = 24;
}

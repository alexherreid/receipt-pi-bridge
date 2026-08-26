namespace Ncr7198.PiBridge;

public sealed record PrintRequest
{
    public string? PrintId { get; init; }
    public int PrePrintLines { get; init; }
    public string[]? Lines { get; init; }
    public string? Content { get; init; }
    public int PostPrintLines { get; init; } = 4;
    public string Wrap { get; init; } = "none";
    public bool Compressed { get; init; }
    public bool Cut { get; init; } = true;
    public int Copies { get; init; } = 1;
    public string? Logo { get; init; }
    public string LogoPosition { get; init; } = "top";
}

public sealed record RenderedPrintJob(byte[] Bytes, string[] Preview, string Hash, string? PrintId,
    int Copies, bool RequestedCut, bool EffectiveCut, bool CutForced);

public sealed record PrintResult(string Status, string? PrintId, int Copies,
    bool RequestedCut, bool EffectiveCut, bool CutForced, int Bytes);

public sealed record PrintSubmission(Task<PrintResult> Result, bool IsDuplicate);

public sealed class PrintValidationException(string message) : Exception(message);
public sealed class PrintQueueFullException() : Exception("The print queue is full. Try again shortly.");
public sealed class PrintIdConflictException(string printId)
    : Exception($"printId '{printId}' was already used for a different print job.");

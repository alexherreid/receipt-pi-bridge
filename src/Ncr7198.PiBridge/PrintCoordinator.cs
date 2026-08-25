using System.Threading.Channels;

namespace Ncr7198.PiBridge;

public sealed class PrintCoordinator : BackgroundService
{
    private sealed record QueuedJob(RenderedPrintJob Job, TaskCompletionSource<PrintResult> Completion);
    private sealed class CacheEntry(string hash, Task<PrintResult> task)
    {
        public string Hash { get; } = hash;
        public Task<PrintResult> Task { get; } = task;
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    private readonly object _gate = new();
    private readonly Channel<QueuedJob> _queue = Channel.CreateUnbounded<QueuedJob>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly IPrinterTransport _transport;
    private readonly BridgeOptions _options;
    private readonly ILogger<PrintCoordinator> _logger;
    private int _outstanding;

    public PrintCoordinator(IPrinterTransport transport, BridgeOptions options, ILogger<PrintCoordinator> logger)
    {
        _transport = transport;
        _options = options;
        _logger = logger;
    }

    public PrintSubmission Submit(RenderedPrintJob job)
    {
        lock (_gate)
        {
            CleanupExpiredEntries();
            if (job.PrintId is not null && _cache.TryGetValue(job.PrintId, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing.Hash, job.Hash)) throw new PrintIdConflictException(job.PrintId);
                return new PrintSubmission(existing.Task, true);
            }
            if (_outstanding >= _options.MaxOutstandingJobs) throw new PrintQueueFullException();

            _outstanding++;
            var completion = new TaskCompletionSource<PrintResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (job.PrintId is not null) _cache.Add(job.PrintId, new CacheEntry(job.Hash, completion.Task));
            if (!_queue.Writer.TryWrite(new QueuedJob(job, completion)))
            {
                _outstanding--;
                if (job.PrintId is not null) _cache.Remove(job.PrintId);
                throw new InvalidOperationException("The print queue is unavailable.");
            }
            return new PrintSubmission(completion.Task, false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var queued in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _transport.WriteAsync(queued.Job.Bytes, stoppingToken);
                queued.Completion.TrySetResult(new PrintResult("submitted", queued.Job.PrintId, queued.Job.Copies,
                    queued.Job.RequestedCut, queued.Job.EffectiveCut, queued.Job.CutForced, queued.Job.Bytes.Length));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                queued.Completion.TrySetCanceled(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Printer write failed for printId {PrintId}", queued.Job.PrintId);
                queued.Completion.TrySetException(exception);
            }
            finally
            {
                lock (_gate)
                {
                    _outstanding--;
                    if (queued.Job.PrintId is not null && _cache.TryGetValue(queued.Job.PrintId, out var cached))
                        cached.ExpiresAt = DateTimeOffset.UtcNow.AddHours(_options.PrintIdLifetimeHours);
                }
            }
        }
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _cache
                     .Where(pair => pair.Value.ExpiresAt is { } expiration && expiration <= now)
                     .Select(pair => pair.Key).ToArray())
            _cache.Remove(key);
    }
}

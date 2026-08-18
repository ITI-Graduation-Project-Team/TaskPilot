using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Models.Telemetry;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.BackgroundJobs;

public sealed class AiTelemetryPersistenceWorker : BackgroundService
{
    private readonly IAiTelemetryQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiTelemetryPersistenceWorker> _logger;

    public AiTelemetryPersistenceWorker(
        IAiTelemetryQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AiTelemetryPersistenceWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = new List<AiUsageRecord>(100);
                while (batch.Count < 100 && _queue.Reader.TryRead(out var record))
                    batch.Add(record);

                await PersistWithRetryAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // StopAsync completes and drains the channel below.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        await base.StopAsync(cancellationToken);

        while (_queue.Reader.TryRead(out var first))
        {
            var batch = new List<AiUsageRecord>(100) { first };
            while (batch.Count < 100 && _queue.Reader.TryRead(out var next))
                batch.Add(next);
            await PersistWithRetryAsync(batch, CancellationToken.None);
        }
    }

    private async Task PersistWithRetryAsync(
        IReadOnlyCollection<AiUsageRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAiTelemetryService>();
                await service.LogTelemetryBatchAsync(batch, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "AI telemetry persistence attempt {Attempt} failed.", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI telemetry batch could not be persisted after retries.");
            }
        }
    }
}

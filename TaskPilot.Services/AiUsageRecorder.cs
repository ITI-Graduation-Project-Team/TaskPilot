using Microsoft.Extensions.Logging;
using TaskPilot.AI.Models.Telemetry;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public sealed class AiUsageRecorder : IAiUsageRecorder
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAiPricingService _pricing;
    private readonly IAiTelemetryQueue _queue;
    private readonly IAiTelemetryService _telemetryService;
    private readonly ILogger<AiUsageRecorder> _logger;

    public AiUsageRecorder(
        ICurrentUserService currentUser,
        IAiPricingService pricing,
        IAiTelemetryQueue queue,
        IAiTelemetryService telemetryService,
        ILogger<AiUsageRecorder> logger)
    {
        _currentUser = currentUser;
        _pricing = pricing;
        _queue = queue;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public Task RecordFromMetadataAsync(
        IReadOnlyDictionary<string, object?>? metadata,
        string operationType,
        string modelName,
        long responseTimeMs,
        string status,
        string? errorMessage = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (!AiTokenUsage.TryCreate(metadata, out var usage))
        {
            return EnqueueAsync(
                new AiTokenUsage(0, 0, 0),
                operationType,
                modelName,
                responseTimeMs,
                status,
                "UsageUnavailable",
                errorMessage,
                projectId);
        }

        return RecordUsageAsync(
            usage,
            operationType,
            modelName,
            responseTimeMs,
            status,
            errorMessage,
            projectId,
            cancellationToken);
    }

    public Task RecordUsageAsync(
        AiTokenUsage usage,
        string operationType,
        string modelName,
        long responseTimeMs,
        string status = "Success",
        string? errorMessage = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var priced = _pricing.TryCalculateCost(
            modelName,
            usage.InputTokens,
            usage.CachedInputTokens,
            usage.OutputTokens,
            out var canonicalModel,
            out var cost);

        return EnqueueAsync(
            usage,
            operationType,
            canonicalModel,
            responseTimeMs,
            status,
            priced ? "Calculated" : "UnpricedModel",
            errorMessage,
            projectId,
            cost);
    }

    private async Task EnqueueAsync(
        AiTokenUsage usage,
        string operationType,
        string modelName,
        long responseTimeMs,
        string status,
        string calculationStatus,
        string? errorMessage,
        Guid? projectId,
        decimal cost = 0m)
    {
        var userId = _currentUser.UserId ?? AiTelemetryContext.CurrentUserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            _logger.LogWarning(
                "AI usage could not be attributed: Operation={Operation} Model={Model}",
                operationType,
                modelName);
            return;
        }

        var record = new AiUsageRecord(
            userId.Value,
            projectId ?? AiTelemetryContext.CurrentProjectId,
            operationType,
            modelName,
            usage.InputTokens,
            usage.CachedInputTokens,
            usage.OutputTokens,
            cost,
            responseTimeMs,
            status,
            calculationStatus,
            errorMessage);

        if (_queue.TryEnqueue(record))
            return;

        _logger.LogWarning("AI telemetry queue is full; persisting synchronously.");
        try
        {
            await _telemetryService.LogTelemetryBatchAsync([record], CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI telemetry could not be persisted after the queue filled.");
        }
    }
}

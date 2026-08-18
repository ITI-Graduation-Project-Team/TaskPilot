using System.Collections.Generic;
using TaskPilot.AI.Models.Telemetry;

namespace TaskPilot.AI.Services.Interfaces;

public interface IAiUsageRecorder
{
    Task RecordFromMetadataAsync(
        IReadOnlyDictionary<string, object?>? metadata,
        string operationType,
        string modelName,
        long responseTimeMs,
        string status,
        string? errorMessage = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        AiTokenUsage usage,
        string operationType,
        string modelName,
        long responseTimeMs,
        string status = "Success",
        string? errorMessage = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);
}

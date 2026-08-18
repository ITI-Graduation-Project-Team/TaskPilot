namespace TaskPilot.AI.Models.Telemetry;

public sealed record AiUsageRecord(
    Guid UserId,
    Guid? ProjectId,
    string OperationType,
    string ModelName,
    int PromptTokens,
    int CachedPromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    long ResponseTimeMs,
    string Status,
    string CalculationStatus,
    string? ErrorMessage = null);

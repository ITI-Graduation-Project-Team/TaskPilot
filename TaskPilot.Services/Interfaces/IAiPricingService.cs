namespace TaskPilot.Services.Interfaces;

public interface IAiPricingService
{
    bool TryCalculateCost(
        string modelName,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        out string canonicalModelName,
        out decimal costUsd);

    bool IsSupportedModel(string modelName);
}

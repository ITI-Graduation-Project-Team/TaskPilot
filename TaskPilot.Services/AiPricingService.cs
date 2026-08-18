using TaskPilot.AI.Constants;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public sealed class AiPricingService : IAiPricingService
{
    private sealed record Price(decimal Input, decimal CachedInput, decimal Output);

    private static readonly IReadOnlyDictionary<string, Price> Prices =
        new Dictionary<string, Price>(StringComparer.OrdinalIgnoreCase)
        {
            [ModelConstants.MorePowerfulModel] = new(2.00m, 0.50m, 8.00m),
            [ModelConstants.PowerfulModel] = new(0.40m, 0.10m, 1.60m),
            [ModelConstants.CheapModel] = new(0.15m, 0.075m, 0.60m),
            [ModelConstants.EmbeddingModel] = new(0.02m, 0.02m, 0m),
            [ModelConstants.GeminiFast] = new(0.30m, 0.03m, 2.50m)
        };

    public bool TryCalculateCost(
        string modelName,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        out string canonicalModelName,
        out decimal costUsd)
    {
        canonicalModelName = NormalizeModelName(modelName);
        costUsd = 0m;
        if (!Prices.TryGetValue(canonicalModelName, out var price))
            return false;

        var safeInput = Math.Max(0, inputTokens);
        var safeCached = Math.Clamp(cachedInputTokens, 0, safeInput);
        var safeOutput = Math.Max(0, outputTokens);
        var uncachedInput = safeInput - safeCached;

        costUsd = ((uncachedInput * price.Input)
            + (safeCached * price.CachedInput)
            + (safeOutput * price.Output)) / 1_000_000m;
        return true;
    }

    public bool IsSupportedModel(string modelName) =>
        Prices.ContainsKey(NormalizeModelName(modelName));

    private static string NormalizeModelName(string modelName)
    {
        var value = modelName.Trim();
        foreach (var knownModel in Prices.Keys.OrderByDescending(x => x.Length))
        {
            if (value.Equals(knownModel, StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(knownModel + "-", StringComparison.OrdinalIgnoreCase))
            {
                return knownModel;
            }
        }

        return value.ToLowerInvariant();
    }
}

using TaskPilot.AI.Constants;
using TaskPilot.Services;
using Xunit;

namespace TaskPilot.Tests;

public class AiPricingServiceTests
{
    private readonly AiPricingService _service = new();

    [Fact]
    public void EveryDeclaredModelHasPricing()
    {
        var modelNames = typeof(ModelConstants)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.All(modelNames, modelName =>
            Assert.True(_service.IsSupportedModel(modelName), $"Missing price for {modelName}"));
    }

    [Fact]
    public void Gpt41_UsesSeparateCachedInputPrice()
    {
        var priced = _service.TryCalculateCost(
            "gpt-4.1",
            inputTokens: 1_000,
            cachedInputTokens: 200,
            outputTokens: 500,
            out var canonicalModel,
            out var cost);

        Assert.True(priced);
        Assert.Equal("gpt-4.1", canonicalModel);
        Assert.Equal(0.0057m, cost);
    }

    [Fact]
    public void SnapshotName_UsesBaseModelPrice()
    {
        var priced = _service.TryCalculateCost(
            "gpt-4.1-mini-2025-04-14",
            inputTokens: 1_000_000,
            cachedInputTokens: 0,
            outputTokens: 1_000_000,
            out var canonicalModel,
            out var cost);

        Assert.True(priced);
        Assert.Equal(ModelConstants.PowerfulModel, canonicalModel);
        Assert.Equal(2.00m, cost);
    }

    [Fact]
    public void UnknownModel_IsNotReportedAsFree()
    {
        var priced = _service.TryCalculateCost(
            "unknown-model",
            100,
            0,
            100,
            out var canonicalModel,
            out var cost);

        Assert.False(priced);
        Assert.Equal("unknown-model", canonicalModel);
        Assert.Equal(0m, cost);
    }
}

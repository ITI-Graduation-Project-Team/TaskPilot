using TaskPilot.AI.Models.Telemetry;
using Xunit;

namespace TaskPilot.Tests;

public class AiTokenUsageTests
{
    [Fact]
    public void TryCreate_ReadsOpenAiUsageAndCachedTokens()
    {
        IReadOnlyDictionary<string, object?> metadata = new Dictionary<string, object?>
        {
            ["Usage"] = new OpenAiUsage
            {
                InputTokenCount = 120,
                OutputTokenCount = 30,
                InputTokenDetails = new InputDetails { CachedTokenCount = 20 }
            }
        };

        var captured = AiTokenUsage.TryCreate(metadata, out var usage);

        Assert.True(captured);
        Assert.Equal(120, usage.InputTokens);
        Assert.Equal(20, usage.CachedInputTokens);
        Assert.Equal(30, usage.OutputTokens);
        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public void TryCreate_ReadsGeminiUsage()
    {
        IReadOnlyDictionary<string, object?> metadata = new Dictionary<string, object?>
        {
            ["Usage"] = new GeminiUsage
            {
                PromptTokenCount = 90,
                CachedContentTokenCount = 10,
                CandidatesTokenCount = 15
            }
        };

        var captured = AiTokenUsage.TryCreate(metadata, out var usage);

        Assert.True(captured);
        Assert.Equal(new AiTokenUsage(90, 10, 15), usage);
    }

    [Fact]
    public void TryCreate_DoesNotInventFallbackUsage()
    {
        var captured = AiTokenUsage.TryCreate(
            new Dictionary<string, object?>(),
            out var usage);

        Assert.False(captured);
        Assert.Equal(new AiTokenUsage(0, 0, 0), usage);
    }

    private sealed class OpenAiUsage
    {
        public int InputTokenCount { get; init; }
        public int OutputTokenCount { get; init; }
        public InputDetails InputTokenDetails { get; init; } = new();
    }

    private sealed class InputDetails
    {
        public int CachedTokenCount { get; init; }
    }

    private sealed class GeminiUsage
    {
        public int PromptTokenCount { get; init; }
        public int CachedContentTokenCount { get; init; }
        public int CandidatesTokenCount { get; init; }
    }
}

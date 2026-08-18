using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Telemetry;
using TaskPilot.Services;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;
using Xunit;

namespace TaskPilot.Tests;

public class AiUsageRecorderTests
{
    [Fact]
    public async Task RecordUsage_AttributesAndQueuesExactCalculatedCost()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(userId);
        var persistence = new Mock<IAiTelemetryService>();
        var queue = new AiTelemetryQueue();
        var recorder = new AiUsageRecorder(
            currentUser.Object,
            new AiPricingService(),
            queue,
            persistence.Object,
            NullLogger<AiUsageRecorder>.Instance);

        using (AiTelemetryContext.SetProjectId(projectId))
        {
            await recorder.RecordUsageAsync(
                new AiTokenUsage(1_000, 200, 500),
                "TestOperation",
                ModelConstants.MorePowerfulModel,
                25);
        }

        Assert.True(queue.Reader.TryRead(out var record));
        Assert.Equal(userId, record.UserId);
        Assert.Equal(projectId, record.ProjectId);
        Assert.Equal(200, record.CachedPromptTokens);
        Assert.Equal(0.0057m, record.EstimatedCostUsd);
        Assert.Equal("Calculated", record.CalculationStatus);
        persistence.Verify(
            service => service.LogTelemetryBatchAsync(
                It.IsAny<IReadOnlyCollection<AiUsageRecord>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MissingUsage_IsStoredAsUnavailableWithoutInventedTokens()
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(userId);
        var queue = new AiTelemetryQueue();
        var recorder = new AiUsageRecorder(
            currentUser.Object,
            new AiPricingService(),
            queue,
            Mock.Of<IAiTelemetryService>(),
            NullLogger<AiUsageRecorder>.Instance);

        await recorder.RecordFromMetadataAsync(
            null,
            "MissingUsage",
            ModelConstants.CheapModel,
            10,
            "Success");

        Assert.True(queue.Reader.TryRead(out var record));
        Assert.Equal(0, record.PromptTokens);
        Assert.Equal(0, record.CompletionTokens);
        Assert.Equal(0m, record.EstimatedCostUsd);
        Assert.Equal("UsageUnavailable", record.CalculationStatus);
    }
}

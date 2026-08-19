using Microsoft.EntityFrameworkCore;
using Moq;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Tests;

public class AiTelemetryServiceTests
{
    [Fact]
    public async Task GetManagedProjectsSummaryAsync_AggregatesOnlyCalculatedLogsForManagedActiveProjects()
    {
        await using var context = CreateContext();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var managedProject = CreateProject(managerId);
        var deletedProject = CreateProject(managerId, isDeleted: true);
        var otherProject = CreateProject(otherManagerId);

        context.Projects.AddRange(managedProject, deletedProject, otherProject);
        await context.SaveChangesAsync();

        context.AiTelemetryLogs.AddRange(
            CreateLog(managedProject.Id, totalTokens: 100, cost: 0.10m, responseTimeMs: 1_000),
            CreateLog(managedProject.Id, totalTokens: 300, cost: 0.30m, responseTimeMs: 3_000),
            CreateLog(managedProject.Id, totalTokens: 999, cost: 9.99m, responseTimeMs: 9_999, calculationStatus: "Legacy"),
            CreateLog(deletedProject.Id, totalTokens: 999, cost: 9.99m, responseTimeMs: 9_999),
            CreateLog(otherProject.Id, totalTokens: 999, cost: 9.99m, responseTimeMs: 9_999));
        await context.SaveChangesAsync();

        var result = await new AiTelemetryService(context).GetManagedProjectsSummaryAsync(managerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalOperations);
        Assert.Equal(400, result.Value.TotalTokens);
        Assert.Equal(0.40m, result.Value.TotalCostUsd);
        Assert.Equal(2_000, result.Value.AverageResponseTimeMs);
    }

    [Fact]
    public async Task GetManagedProjectsSummaryAsync_ReturnsZerosWhenManagerHasNoUsage()
    {
        await using var context = CreateContext();

        var result = await new AiTelemetryService(context)
            .GetManagedProjectsSummaryAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalOperations);
        Assert.Equal(0, result.Value.TotalTokens);
        Assert.Equal(0m, result.Value.TotalCostUsd);
        Assert.Equal(0, result.Value.AverageResponseTimeMs);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, Mock.Of<ICurrentUserService>());
    }

    private static Project CreateProject(Guid managerId, bool isDeleted = false) => new()
    {
        NameEn = Guid.NewGuid().ToString(),
        NameAr = "مشروع",
        ManagerId = managerId,
        CompanyId = Guid.NewGuid(),
        IsDeleted = isDeleted
    };

    private static AiTelemetryLog CreateLog(
        Guid projectId,
        int totalTokens,
        decimal cost,
        long responseTimeMs,
        string calculationStatus = "Calculated") => new()
    {
        UserId = Guid.NewGuid(),
        ProjectId = projectId,
        OperationType = "Test",
        ModelName = "test-model",
        TotalTokens = totalTokens,
        EstimatedCostUsd = cost,
        ResponseTimeMs = responseTimeMs,
        Status = "Success",
        CalculationStatus = calculationStatus
    };
}

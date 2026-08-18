using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;

namespace TaskPilot.Tests;

public sealed class WbsSkillEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichProjectTasksAsync_PersistsSuccessesAndReportsTechnicalFailures()
    {
        var projectId = Guid.NewGuid();
        var successful = TechnicalTask(projectId, "Successful");
        var failed = TechnicalTask(projectId, "Failed");
        var existing = TechnicalTask(projectId, "Existing");
        existing.RequiredSkills.Add(new TaskRequiredSkill());
        var nonTechnical = TechnicalTask(projectId, "Non technical");
        nonTechnical.Type = TaskType.NonTechnical;

        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichAsync("Successful", It.IsAny<string>(), "Technical", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<GeneratedRequiredSkill>
            {
                new() { SkillName = "C#", RequiredLevel = "Intermediate" }
            }));
        agent.Setup(x => x.EnrichAsync("Failed", It.IsAny<string>(), "Technical", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<GeneratedRequiredSkill>>(new Error("REQUIRED_SKILLS_NO_VALID_RESULT")));

        var fixture = CreateService(projectId, [successful, failed, existing, nonTechnical], agent.Object);

        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TasksProcessed);
        Assert.Equal(2, result.Value.TasksEnriched);
        Assert.Equal(1, result.Value.TasksSkipped);
        Assert.Single(result.Value.Warnings);
        Assert.Single(fixture.SavedRequiredSkills);
        agent.Verify(x => x.EnrichAsync("Non technical", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrichProjectTasksAsync_RetryProcessesOnlyTasksStillMissingSkillsAndKeepsCumulativeCounts()
    {
        var projectId = Guid.NewGuid();
        var existing = TechnicalTask(projectId, "Existing");
        existing.RequiredSkills.Add(new TaskRequiredSkill());
        var pending = TechnicalTask(projectId, "Pending");

        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichAsync("Pending", It.IsAny<string>(), "Technical", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<GeneratedRequiredSkill>
            {
                new() { SkillName = "React", RequiredLevel = "Advanced" }
            }));

        var fixture = CreateService(projectId, [existing, pending], agent.Object);

        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TasksProcessed);
        Assert.Equal(2, result.Value.TasksEnriched);
        Assert.Equal(0, result.Value.TasksSkipped);
        Assert.Single(fixture.SavedRequiredSkills);
        agent.Verify(x => x.EnrichAsync("Existing", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TaskItem TechnicalTask(Guid projectId, string title) => new()
    {
        TitleEn = title,
        Type = TaskType.Technical,
        UserStory = new UserStory { ProjectId = projectId }
    };

    private static Mock<RequiredSkillsEnrichmentAgent> CreateAgentMock() => new(
        Mock.Of<IAiKernelService>(),
        Mock.Of<IPromptLoaderService>(),
        NullLogger<RequiredSkillsEnrichmentAgent>.Instance,
        Mock.Of<ITelemetryAccumulator>());

    private static ServiceFixture CreateService(Guid projectId, List<TaskItem> tasks, RequiredSkillsEnrichmentAgent agent)
    {
        var projectRepository = new Mock<IRepository<Project>>();
        projectRepository.Setup(x => x.GetByIdAsync(projectId)).ReturnsAsync(new Project());

        var taskRepository = new Mock<IRepository<TaskItem>>();
        taskRepository.Setup(x => x.FindAsync(
                It.IsAny<Expression<Func<TaskItem, bool>>>(),
                It.IsAny<Expression<Func<TaskItem, object>>[]>()))
            .ReturnsAsync(tasks);

        var skillRepository = new Mock<IRepository<Skill>>();
        skillRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Skill>());

        var savedRequiredSkills = new List<TaskRequiredSkill>();
        var requiredSkillRepository = new Mock<IRepository<TaskRequiredSkill>>();
        requiredSkillRepository.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<TaskRequiredSkill>>()))
            .Callback<IEnumerable<TaskRequiredSkill>>(items => savedRequiredSkills.AddRange(items))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new WbsSkillEnrichmentService(
            projectRepository.Object,
            taskRepository.Object,
            skillRepository.Object,
            requiredSkillRepository.Object,
            agent,
            unitOfWork.Object,
            NullLogger<WbsSkillEnrichmentService>.Instance);

        return new ServiceFixture(service, savedRequiredSkills);
    }

    private sealed record ServiceFixture(WbsSkillEnrichmentService Service, List<TaskRequiredSkill> SavedRequiredSkills);
}

using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
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

        var calls = new ConcurrentBag<IReadOnlyCollection<SkillEnrichmentTaskInput>>();
        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichBatchAsync(
                It.IsAny<IReadOnlyCollection<SkillEnrichmentTaskInput>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<SkillEnrichmentTaskInput> inputs, IReadOnlyCollection<string> _, CancellationToken _) =>
            {
                calls.Add(inputs);
                var results = inputs
                    .Where(input => input.TaskId == successful.Id)
                    .Select(SuccessResult)
                    .ToList();
                return Result.Success(results);
            });

        var fixture = CreateService(projectId, [successful, failed, existing, nonTechnical], agent.Object);
        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TasksProcessed);
        Assert.Equal(2, result.Value.TasksEnriched);
        Assert.Equal(1, result.Value.TasksSkipped);
        Assert.Single(result.Value.Warnings);
        Assert.Single(fixture.SavedRequiredSkills);
        Assert.Equal(3, calls.Count);
        Assert.Equal(1, calls.Sum(call => call.Count(input => input.TaskId == successful.Id)));
        Assert.Equal(3, calls.Sum(call => call.Count(input => input.TaskId == failed.Id)));
        Assert.DoesNotContain(calls.SelectMany(call => call), input => input.TaskId == existing.Id || input.TaskId == nonTechnical.Id);
    }

    [Fact]
    public async Task EnrichProjectTasksAsync_RetriesOnlyMissingTasks()
    {
        var projectId = Guid.NewGuid();
        var first = TechnicalTask(projectId, "First");
        var second = TechnicalTask(projectId, "Second");
        var third = TechnicalTask(projectId, "Third");
        var calls = new List<IReadOnlyCollection<SkillEnrichmentTaskInput>>();
        var callNumber = 0;

        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichBatchAsync(
                It.IsAny<IReadOnlyCollection<SkillEnrichmentTaskInput>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<SkillEnrichmentTaskInput> inputs, IReadOnlyCollection<string> _, CancellationToken _) =>
            {
                calls.Add(inputs);
                callNumber++;
                var results = callNumber == 1
                    ? inputs.Where(input => input.TaskId != third.Id).Select(SuccessResult).ToList()
                    : inputs.Select(SuccessResult).ToList();
                return Result.Success(results);
            });

        var fixture = CreateService(projectId, [first, second, third], agent.Object);
        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TasksEnriched);
        Assert.Empty(result.Value.Warnings);
        Assert.Equal(2, calls.Count);
        Assert.Equal(3, calls[0].Count);
        Assert.Single(calls[1]);
        Assert.Equal(third.Id, calls[1].Single().TaskId);
    }

    [Fact]
    public async Task EnrichProjectTasksAsync_UsesFiveBatchesFor116Tasks()
    {
        var projectId = Guid.NewGuid();
        var tasks = Enumerable.Range(1, 116)
            .Select(index => TechnicalTask(projectId, $"Task {index}"))
            .ToList();
        var batchSizes = new ConcurrentBag<int>();

        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichBatchAsync(
                It.IsAny<IReadOnlyCollection<SkillEnrichmentTaskInput>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<SkillEnrichmentTaskInput> inputs, IReadOnlyCollection<string> _, CancellationToken _) =>
            {
                batchSizes.Add(inputs.Count);
                return Result.Success(inputs.Select(SuccessResult).ToList());
            });

        var fixture = CreateService(projectId, tasks, agent.Object);
        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(116, result.Value.TasksEnriched);
        Assert.Equal(5, batchSizes.Count);
        Assert.All(batchSizes, size => Assert.InRange(size, 1, 25));
        Assert.Equal(116, fixture.SavedRequiredSkills.Count);
    }

    [Fact]
    public async Task EnrichProjectTasksAsync_NeverRunsMoreThanFiveBatchesConcurrently()
    {
        var projectId = Guid.NewGuid();
        var tasks = Enumerable.Range(1, 126)
            .Select(index => TechnicalTask(projectId, $"Task {index}"))
            .ToList();
        var activeCalls = 0;
        var peakCalls = 0;

        var agent = CreateAgentMock();
        agent.Setup(x => x.EnrichBatchAsync(
                It.IsAny<IReadOnlyCollection<SkillEnrichmentTaskInput>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyCollection<SkillEnrichmentTaskInput> inputs, IReadOnlyCollection<string> _, CancellationToken cancellationToken) =>
            {
                var active = Interlocked.Increment(ref activeCalls);
                int observedPeak;
                do
                {
                    observedPeak = peakCalls;
                    if (active <= observedPeak) break;
                } while (Interlocked.CompareExchange(ref peakCalls, active, observedPeak) != observedPeak);

                await Task.Delay(50, cancellationToken);
                Interlocked.Decrement(ref activeCalls);
                return Result.Success(inputs.Select(SuccessResult).ToList());
            });

        var fixture = CreateService(projectId, tasks, agent.Object);
        var result = await fixture.Service.EnrichProjectTasksAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.InRange(peakCalls, 2, 5);
    }

    private static GeneratedTaskRequiredSkills SuccessResult(SkillEnrichmentTaskInput input) => new()
    {
        TaskId = input.TaskId,
        Skills = [new GeneratedRequiredSkill { SkillName = "C#", RequiredLevel = "Intermediate" }]
    };

    private static TaskItem TechnicalTask(Guid projectId, string title) => new TestTaskItem(Guid.NewGuid())
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
        projectRepository.Setup(repository => repository.GetByIdAsync(projectId)).ReturnsAsync(new Project());

        var taskRepository = new Mock<IRepository<TaskItem>>();
        taskRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<TaskItem, bool>>>(),
                It.IsAny<Expression<Func<TaskItem, object>>[]>()))
            .ReturnsAsync(tasks);

        var skillRepository = new Mock<IRepository<Skill>>();
        skillRepository.Setup(repository => repository.GetAllAsync()).ReturnsAsync(Array.Empty<Skill>());

        var savedRequiredSkills = new List<TaskRequiredSkill>();
        var requiredSkillRepository = new Mock<IRepository<TaskRequiredSkill>>();
        requiredSkillRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<TaskRequiredSkill>>()))
            .Callback<IEnumerable<TaskRequiredSkill>>(items => savedRequiredSkills.AddRange(items))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

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

    private sealed class TestTaskItem : TaskItem
    {
        public TestTaskItem(Guid id) => Id = id;
    }
}

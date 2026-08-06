using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Assignment;
using Xunit;

namespace TaskPilot.Tests.Services;

public class AssignmentScoringServiceTests
{
    private readonly Mock<ITeamSnapshotService> _teamSnapshotServiceMock;
    private readonly Mock<ICapacityValidationService> _capacityValidationServiceMock;
    private readonly Mock<IOptions<ScoringWeights>> _weightsOptionsMock;
    private readonly List<IScoreCalculator> _calculators;
    private readonly AssignmentScoringService _service;

    public AssignmentScoringServiceTests()
    {
        _teamSnapshotServiceMock = new Mock<ITeamSnapshotService>();
        _capacityValidationServiceMock = new Mock<ICapacityValidationService>();
        _weightsOptionsMock = new Mock<IOptions<ScoringWeights>>();

        var weights = new ScoringWeights
        {
            SkillWeight = 50,
            AvailabilityWeight = 30,
            VelocityWeight = 10,
            ExperienceWeight = 10
        };
        _weightsOptionsMock.Setup(x => x.Value).Returns(weights);

        _capacityValidationServiceMock.Setup(x => x.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        _calculators = new List<IScoreCalculator>
        {
            new SkillScoreCalculator(),
            new AvailabilityScoreCalculator(),
            new VelocityScoreCalculator(),
            new ExperienceScoreCalculator()
        };

        _service = new AssignmentScoringService(
            _teamSnapshotServiceMock.Object,
            _capacityValidationServiceMock.Object,
            _weightsOptionsMock.Object,
            _calculators
        );
    }

    private SprintAssignmentSnapshotDto CreateSnapshot(int numDevelopers, int numTasks, double baseCapacity, int taskHours)
    {
        var devs = new List<DeveloperSnapshotDto>();
        for (int i = 0; i < numDevelopers; i++)
        {
            devs.Add(new DeveloperSnapshotDto
            {
                EmployeeId = Guid.NewGuid(),
                FullName = $"Dev {i}",
                RemainingHours = baseCapacity,
                Skills = new List<DeveloperSkillDto> { new DeveloperSkillDto { SkillId = 1, Level = (TaskPilot.Models.Enums.SkillLevel)5 } }
            });
        }

        var tasks = new List<TaskSnapshotDto>();
        for (int i = 0; i < numTasks; i++)
        {
            tasks.Add(new TaskSnapshotDto
            {
                TaskId = Guid.NewGuid(),
                TitleEn = $"Task {i}",
                EstimatedHours = taskHours,
                RequiredSkills = new List<TaskRequiredSkillDto> { new TaskRequiredSkillDto { SkillId = 1, RequiredLevel = (TaskPilot.Models.Enums.SkillLevel)3 } }
            });
        }

        return new SprintAssignmentSnapshotDto
        {
            Team = new TeamSnapshotDto { Developers = devs },
            UnassignedTasks = tasks
        };
    }

    [Fact]
    public async Task ScoreAsync_DominantDeveloper_ShouldDecayAndAlternate()
    {
        // Arrange: Dev 0 is a rockstar, Dev 1 is average
        var snapshot = CreateSnapshot(2, 5, 20, 10);
        snapshot.Team.Developers[0].HasHistoricalData = true;
        snapshot.Team.Developers[0].HistoricalVelocity = 100.0; // Rockstar
        snapshot.Team.Developers[1].HasHistoricalData = true;
        snapshot.Team.Developers[1].HistoricalVelocity = 10.0;

        _teamSnapshotServiceMock.Setup(x => x.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(Result.Success(snapshot));

        // Act
        var result = await _service.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        // Assert
        Assert.True(result.IsSuccess);
        var tasks = result.Value.TaskScores;
        Assert.Equal(5, tasks.Count);

        // Task 1: Rockstar wins
        Assert.Equal(snapshot.Team.Developers[0].EmployeeId, tasks[0].RankedDevelopers.First().EmployeeId);
        // Task 2: Rockstar still wins (remaining hours decay from 20 -> 10, but still enough to beat average dev with velocity multiplier)
        Assert.Equal(snapshot.Team.Developers[0].EmployeeId, tasks[1].RankedDevelopers.First().EmployeeId);
        // Task 3: Rockstar has 0 hours left. Excluded! Average dev wins.
        Assert.Equal(snapshot.Team.Developers[1].EmployeeId, tasks[2].RankedDevelopers.First().EmployeeId);
        // Task 4: Average dev wins.
        Assert.Equal(snapshot.Team.Developers[1].EmployeeId, tasks[3].RankedDevelopers.First().EmployeeId);
        // Task 5: Both have 0 hours. Both excluded!
        Assert.Empty(tasks[4].RankedDevelopers);
    }

    [Fact]
    public async Task ScoreAsync_TasksExceedCapacity_ShouldGracefullyLeaveEmptyCandidates()
    {
        // Arrange: 1 Developer with 10 hours capacity. 3 Tasks of 10 hours each. Total demand 30h.
        var snapshot = CreateSnapshot(1, 3, 10, 10);

        _teamSnapshotServiceMock.Setup(x => x.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(Result.Success(snapshot));

        // Act
        var result = await _service.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        // Assert
        Assert.True(result.IsSuccess);
        var tasks = result.Value.TaskScores;
        Assert.Equal(3, tasks.Count);

        // Task 1: Dev assigned
        Assert.Single(tasks[0].RankedDevelopers);
        // Task 2: Dev excluded (hours=0) -> Empty
        Assert.Empty(tasks[1].RankedDevelopers);
        // Task 3: Dev excluded -> Empty
        Assert.Empty(tasks[2].RankedDevelopers);
    }

    [Fact]
    public async Task ScoreAsync_DeveloperGoesNegative_IsExcludedNextTime()
    {
        // Arrange: 1 Developer with 5 hours capacity. First task is 15 hours. 
        var snapshot = CreateSnapshot(1, 2, 5, 15);

        _teamSnapshotServiceMock.Setup(x => x.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(Result.Success(snapshot));

        // Act
        var result = await _service.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        // Assert
        Assert.True(result.IsSuccess);
        var tasks = result.Value.TaskScores;
        Assert.Equal(2, tasks.Count);

        // Task 1: Dev is assigned. Capacity drops 5 -> -10.
        Assert.Single(tasks[0].RankedDevelopers);
        // Task 2: Dev is excluded because capacity is <= 0.
        Assert.Empty(tasks[1].RankedDevelopers);
    }

    [Fact]
    public async Task ScoreAsync_EquallyCapable_ShouldAlternate()
    {
        // Arrange: 2 Developers, completely identical. 4 tasks of 5 hours each. 
        var snapshot = CreateSnapshot(2, 4, 20, 5);
        snapshot.Team.Developers[0].EmployeeId = Guid.NewGuid();
        snapshot.Team.Developers[1].EmployeeId = Guid.NewGuid();

        _teamSnapshotServiceMock.Setup(x => x.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(Result.Success(snapshot));

        // Act
        var result = await _service.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        // Assert
        Assert.True(result.IsSuccess);
        var tasks = result.Value.TaskScores;
        
        var dev0Id = snapshot.Team.Developers[0].EmployeeId;
        var dev1Id = snapshot.Team.Developers[1].EmployeeId;

        // Since they are identical, OrderByDescending will favor one deterministically (often stable sort based on original order, or Guid).
        // If Dev 0 wins task 1, Dev 0 capacity goes to 15. Dev 1 is at 20. So Dev 1 should win task 2.
        var winner1 = tasks[0].RankedDevelopers.First().EmployeeId;
        var winner2 = tasks[1].RankedDevelopers.First().EmployeeId;
        var winner3 = tasks[2].RankedDevelopers.First().EmployeeId;
        var winner4 = tasks[3].RankedDevelopers.First().EmployeeId;

        Assert.NotEqual(winner1, winner2);
        Assert.Equal(winner1, winner3);
        Assert.Equal(winner2, winner4);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Assignment;
using Xunit;

namespace TaskPilot.Tests.Assignment;

public class AssignmentScoringServiceTests
{
    private readonly Mock<ITeamSnapshotService> _teamSnapshotServiceMock;
    private readonly Mock<ICapacityValidationService> _capacityValidationServiceMock;
    private readonly IOptions<ScoringWeights> _weightsOptions;
    private readonly List<IScoreCalculator> _calculators;
    private readonly AssignmentScoringService _sut;

    public AssignmentScoringServiceTests()
    {
        _teamSnapshotServiceMock = new Mock<ITeamSnapshotService>();
        _capacityValidationServiceMock = new Mock<ICapacityValidationService>();

        var weights = new ScoringWeights { SkillWeight = 40, AvailabilityWeight = 30, VelocityWeight = 20, ExperienceWeight = 10 };
        _weightsOptions = Options.Create(weights);

        _calculators = new List<IScoreCalculator>
        {
            new SkillScoreCalculator(),
            new AvailabilityScoreCalculator(),
            new VelocityScoreCalculator(),
            new ExperienceScoreCalculator()
        };

        _sut = new AssignmentScoringService(
            _teamSnapshotServiceMock.Object,
            _capacityValidationServiceMock.Object,
            _weightsOptions,
            _calculators);
    }

    [Fact]
    public async Task ScoreAsync_WhenProjectDoesNotExist_ReturnsInvalidProject()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _teamSnapshotServiceMock
            .Setup(s => s.GetSnapshotAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SprintAssignmentSnapshotDto>(AssignmentErrors.ProjectNotFound));

        var result = await _sut.ScoreAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.InvalidProject.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_WhenSprintDoesNotExist_ReturnsInvalidSprint()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _teamSnapshotServiceMock
            .Setup(s => s.GetSnapshotAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SprintAssignmentSnapshotDto>(AssignmentErrors.SprintNotFound));

        var result = await _sut.ScoreAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.InvalidSprint.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_WhenCapacityValidationFails_ReturnsCapacityValidationFailed()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _teamSnapshotServiceMock
            .Setup(s => s.GetSnapshotAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SprintAssignmentSnapshotDto()));

        var capacityResult = new CapacityValidationResult { CanProceed = false };
        _capacityValidationServiceMock
            .Setup(s => s.ValidateAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(capacityResult));

        var result = await _sut.ScoreAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.CapacityValidationFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_WhenSnapshotNotFound_ReturnsSnapshotNotFound()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _teamSnapshotServiceMock
            .Setup(s => s.GetSnapshotAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<SprintAssignmentSnapshotDto>(null!));

        var result = await _sut.ScoreAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.SnapshotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_WhenConfigurationInvalid_ReturnsScoringConfigurationInvalid()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        var badWeightsOptions = Options.Create(new ScoringWeights { SkillWeight = 90, AvailabilityWeight = 30 }); // Total != 100
        var sut = new AssignmentScoringService(
            _teamSnapshotServiceMock.Object,
            _capacityValidationServiceMock.Object,
            badWeightsOptions,
            _calculators);

        _teamSnapshotServiceMock
            .Setup(s => s.GetSnapshotAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SprintAssignmentSnapshotDto()));
        _capacityValidationServiceMock
            .Setup(s => s.ValidateAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await sut.ScoreAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.ScoringConfigurationInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_SkillScore_DeveloperHasAllRequiredSkills_ScoreIs100()
    {
        var task = new TaskSnapshotDto
        {
            RequiredSkills = new List<TaskRequiredSkillDto>
            {
                new TaskRequiredSkillDto { SkillName = "C#", RequiredLevel = SkillLevel.Intermediate }
            }
        };

        var dev = new DeveloperSnapshotDto
        {
            Skills = new List<DeveloperSkillDto>
            {
                new DeveloperSkillDto { SkillName = "C#", Level = SkillLevel.Advanced } // Advanced > Intermediate
            }
        };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        Assert.Equal(100, score.SkillScore);
        Assert.Empty(score.SkillGaps);
    }

    [Fact]
    public async Task ScoreAsync_PartialSkillMatch_ScoreDecreasesAndGapGenerated()
    {
        var task = new TaskSnapshotDto
        {
            RequiredSkills = new List<TaskRequiredSkillDto>
            {
                new TaskRequiredSkillDto { SkillName = "C#", RequiredLevel = SkillLevel.Intermediate },
                new TaskRequiredSkillDto { SkillName = "Docker", RequiredLevel = SkillLevel.Beginner }
            }
        };

        var dev = new DeveloperSnapshotDto
        {
            Skills = new List<DeveloperSkillDto>
            {
                new DeveloperSkillDto { SkillName = "C#", Level = SkillLevel.Beginner } // Beginner < Intermediate, score = 50
                // Missing Docker -> Score 0
            }
        };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        
        Assert.Equal(25, score.SkillScore); // (50 + 0) / 2
        Assert.Equal(2, score.SkillGaps.Count);
        Assert.Contains(score.SkillGaps, g => g.SkillName == "C#");
        Assert.Contains(score.SkillGaps, g => g.SkillName == "Docker");
    }

    [Fact]
    public async Task ScoreAsync_AvailabilityScore_CalculatedCorrectly()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 8 };
        var dev = new DeveloperSnapshotDto { RemainingHours = 4 };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        Assert.Equal(50, score.AvailabilityScore); // 4 / 8 * 100
    }

    [Fact]
    public async Task ScoreAsync_VelocityScore_NoHistoricalData_Returns50()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 8 };
        var dev = new DeveloperSnapshotDto { HasHistoricalData = false };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        Assert.Equal(50, score.VelocityScore);
    }

    [Fact]
    public async Task ScoreAsync_ExperienceScore_MapsCorrectly()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 8 };
        var devJunior = new DeveloperSnapshotDto { FullName = "Junior", SeniorityLevel = SeniorityLevel.Junior };
        var devMid = new DeveloperSnapshotDto { FullName = "Mid", SeniorityLevel = SeniorityLevel.MidLevel };
        var devSenior = new DeveloperSnapshotDto { FullName = "Senior", SeniorityLevel = SeniorityLevel.Senior };
        var devLead = new DeveloperSnapshotDto { FullName = "Lead", SeniorityLevel = SeniorityLevel.Lead };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { devJunior, devMid, devSenior, devLead } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var developers = result.Value!.TaskScores.First().RankedDevelopers;

        Assert.Equal(25, developers.First(d => d.FullName == "Junior").ExperienceScore);
        Assert.Equal(60, developers.First(d => d.FullName == "Mid").ExperienceScore);
        Assert.Equal(85, developers.First(d => d.FullName == "Senior").ExperienceScore);
        Assert.Equal(100, developers.First(d => d.FullName == "Lead").ExperienceScore);
    }

    [Fact]
    public async Task ScoreAsync_FinalWeightedScore_IsCorrect()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 10, RequiredSkills = new List<TaskRequiredSkillDto> { new TaskRequiredSkillDto { SkillName = "C#", RequiredLevel = SkillLevel.Beginner } } };
        var dev = new DeveloperSnapshotDto
        {
            RemainingHours = 10, // Availability = 100
            Skills = new List<DeveloperSkillDto> { new DeveloperSkillDto { SkillName = "C#", Level = SkillLevel.Beginner } }, // Skill = 100
            HasHistoricalData = true,
            HistoricalVelocity = 100, // Velocity = 100
            SeniorityLevel = SeniorityLevel.Lead // Experience = 100
        };

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        Assert.Equal(100, score.FinalScore);
    }

    [Fact]
    public async Task ScoreAsync_Ranking_DevelopersOrderedCorrectly()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 10 };
        var dev1 = new DeveloperSnapshotDto { FullName = "Dev1", RemainingHours = 10, HasHistoricalData = false, SeniorityLevel = SeniorityLevel.Lead }; // High experience
        var dev2 = new DeveloperSnapshotDto { FullName = "Dev2", RemainingHours = 0, HasHistoricalData = false, SeniorityLevel = SeniorityLevel.Junior }; // Low everything

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev2, dev1 } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ranked = result.Value!.TaskScores.First().RankedDevelopers;
        Assert.Equal("Dev1", ranked[0].FullName); // Should be ranked higher
        Assert.Equal("Dev2", ranked[1].FullName);
    }

    [Fact]
    public async Task ScoreAsync_DeterministicOutput_SameInputReturnsSameOutput()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 10, TaskId = Guid.NewGuid() };
        var dev = new DeveloperSnapshotDto { FullName = "Dev1", RemainingHours = 10, SeniorityLevel = SeniorityLevel.Lead }; 

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result1 = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        var result2 = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var score1 = result1.Value!.TaskScores.First().RankedDevelopers.First().FinalScore;
        var score2 = result2.Value!.TaskScores.First().RankedDevelopers.First().FinalScore;
        
        Assert.Equal(score1, score2);
    }

    [Fact]
    public async Task ScoreAsync_ValidSnapshot_ReturnsSuccess()
    {
        var task = new TaskSnapshotDto { EstimatedHours = 10 };
        var dev = new DeveloperSnapshotDto { FullName = "Dev1", JobTitle = "Senior Backend Developer", RemainingHours = 10 }; 

        var snapshot = new SprintAssignmentSnapshotDto
        {
            UnassignedTasks = new List<TaskSnapshotDto> { task },
            Team = new TeamSnapshotDto { Developers = new List<DeveloperSnapshotDto> { dev } }
        };

        _teamSnapshotServiceMock.Setup(s => s.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(snapshot));
        _capacityValidationServiceMock.Setup(s => s.ValidateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CapacityValidationResult { CanProceed = true }));

        var result = await _sut.ScoreAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var score = result.Value!.TaskScores.First().RankedDevelopers.First();
        Assert.Equal("Senior Backend Developer", score.JobTitle);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Assignment;
using TaskPilot.AI.Agents.Assignment;
using Xunit;

namespace TaskPilot.Tests.Assignment;

public class AssignmentExplanationServiceTests
{
    private readonly Mock<IAssignmentScoringService> _scoringServiceMock;
    private readonly Mock<IAssignmentExplanationAgent> _explanationAgentMock;
    private readonly AssignmentExplanationService _sut;

    public AssignmentExplanationServiceTests()
    {
        _scoringServiceMock = new Mock<IAssignmentScoringService>();
        _explanationAgentMock = new Mock<IAssignmentExplanationAgent>();
        
        _sut = new AssignmentExplanationService(
            _scoringServiceMock.Object,
            _explanationAgentMock.Object);
    }

    [Fact]
    public async Task GenerateAsync_WhenScoringFails_ReturnsSameFailure()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _scoringServiceMock
            .Setup(s => s.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ScoredAssignmentDto>(AssignmentErrors.InvalidProject));

        var result = await _sut.GenerateAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.InvalidProject.Code, result.Error!.Code);
    }

    [Fact]
    public async Task GenerateAsync_WhenScoreResultIsEmpty_ReturnsInvalidExplanationInput()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();

        _scoringServiceMock
            .Setup(s => s.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<ScoredAssignmentDto>(null!));

        var result = await _sut.GenerateAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.InvalidExplanationInput.Code, result.Error!.Code);
    }

    [Fact]
    public async Task GenerateAsync_AI_ReturnsValidExplanations_MapsToTop3()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        
        var scoredDto = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskScores = new List<TaskScoringResultDto>
            {
                new TaskScoringResultDto
                {
                    Task = new TaskSnapshotDto { TitleEn = "Task 1" },
                    RankedDevelopers = new List<DeveloperScoreDto>
                    {
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FinalScore = 90 },
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FinalScore = 80 },
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FinalScore = 70 },
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FinalScore = 60 }
                    }
                }
            }
        };

        _scoringServiceMock
            .Setup(s => s.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(scoredDto));

        var aiReasons = new List<(string ReasonEn, string ReasonAr)>
        {
            ("Reason 1 EN", "Reason 1 AR"),
            ("Reason 2 EN", "Reason 2 AR"),
            ("Reason 3 EN", "Reason 3 AR")
        };

        _explanationAgentMock
            .Setup(a => a.GenerateExplanationsAsync(It.IsAny<ExplanationContextDto>()))
            .ReturnsAsync(Result.Success(aiReasons));

        var result = await _sut.GenerateAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var explainedTask = result.Value!.TaskScores.First();
        Assert.Equal(4, explainedTask.RankedDevelopers.Count);
        
        Assert.Equal("Reason 1 EN", explainedTask.RankedDevelopers[0].ReasonEn);
        Assert.Equal("Reason 2 EN", explainedTask.RankedDevelopers[1].ReasonEn);
        Assert.Equal("Reason 3 EN", explainedTask.RankedDevelopers[2].ReasonEn);
        Assert.Equal("Explanation not generated (not in top 3).", explainedTask.RankedDevelopers[3].ReasonEn);
        
        // Ensure numeric values remained unchanged
        Assert.Equal(90, explainedTask.RankedDevelopers[0].FinalScore);
    }

    [Fact]
    public async Task GenerateAsync_AI_Fails_ReturnsFailure()
    {
        var projectId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        
        var scoredDto = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskScores = new List<TaskScoringResultDto>
            {
                new TaskScoringResultDto
                {
                    Task = new TaskSnapshotDto { TitleEn = "Task 1" },
                    RankedDevelopers = new List<DeveloperScoreDto>
                    {
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FinalScore = 90 }
                    }
                }
            }
        };

        _scoringServiceMock
            .Setup(s => s.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(scoredDto));

        _explanationAgentMock
            .Setup(a => a.GenerateExplanationsAsync(It.IsAny<ExplanationContextDto>()))
            .ReturnsAsync(Result.Failure<List<(string ReasonEn, string ReasonAr)>>(AssignmentErrors.ExplanationGenerationFailed));

        var result = await _sut.GenerateAsync(projectId, sprintId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssignmentErrors.ExplanationGenerationFailed.Code, result.Error!.Code);
    }
}

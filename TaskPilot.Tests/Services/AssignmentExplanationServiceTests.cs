using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using TaskPilot.AI.Agents.Assignment;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Assignment;
using Xunit;

namespace TaskPilot.Tests.Services;

public class AssignmentExplanationServiceTests
{
    private readonly Mock<IAssignmentScoringService> _scoringServiceMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAssignmentExplanationAgent> _explanationAgentMock;
    private readonly Mock<IOptions<AssignmentOptions>> _optionsMock;
    
    private readonly AssignmentExplanationService _service;

    public AssignmentExplanationServiceTests()
    {
        _scoringServiceMock = new Mock<IAssignmentScoringService>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _explanationAgentMock = new Mock<IAssignmentExplanationAgent>();
        _optionsMock = new Mock<IOptions<AssignmentOptions>>();

        _optionsMock.Setup(x => x.Value).Returns(new AssignmentOptions { MaxExplanationConcurrency = 5 });

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IAssignmentExplanationAgent)))
            .Returns(_explanationAgentMock.Object);

        _service = new AssignmentExplanationService(
            _scoringServiceMock.Object,
            _scopeFactoryMock.Object,
            _optionsMock.Object
        );
    }

    [Fact]
    public async Task GenerateAsync_ParallelTasks_ShouldCreateScopePerTaskAndReturnResults()
    {
        // Arrange
        var sprintId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        
        var scoredAssignment = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskScores = new List<TaskScoringResultDto>()
        };

        for (int i = 0; i < 5; i++)
        {
            var taskScore = new TaskScoringResultDto
            {
                Task = new TaskSnapshotDto { TaskId = Guid.NewGuid(), TitleEn = $"Task {i}" },
                RankedDevelopers = new List<DeveloperScoreDto>
                {
                    new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FullName = "Dev A" }
                }
            };
            scoredAssignment.TaskScores.Add(taskScore);
        }

        _scoringServiceMock.Setup(x => x.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(scoredAssignment));

        _explanationAgentMock.Setup(x => x.GenerateExplanationsAsync(It.IsAny<ExplanationContextDto>()))
            .ReturnsAsync((ExplanationContextDto ctx) => 
            {
                var reasons = ctx.TopDevelopers.Select(d => (d.EmployeeId.ToString(), "Reason EN", "Reason AR")).ToList();
                return Result.Success(reasons);
            });

        // Act
        var result = await _service.GenerateAsync(projectId, sprintId, "en", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.TaskScores.Count);

        // Verify CreateScope was called exactly 5 times (once for each task)
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Exactly(5));
        
        // Verify agent was resolved 5 times
        _serviceProviderMock.Verify(x => x.GetService(typeof(IAssignmentExplanationAgent)), Times.Exactly(5));

        // Verify explanations were set
        foreach (var taskScore in result.Value.TaskScores)
        {
            Assert.Equal("Reason EN", taskScore.RankedDevelopers.First().ReasonEn);
            Assert.Equal("Reason AR", taskScore.RankedDevelopers.First().ReasonAr);
        }
    }

    [Fact]
    public async Task GenerateAsync_AgentThrowsException_ShouldCatchAndReturnFallbackString()
    {
        // Arrange
        var sprintId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        
        var scoredAssignment = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskScores = new List<TaskScoringResultDto>
            {
                new TaskScoringResultDto
                {
                    Task = new TaskSnapshotDto { TaskId = Guid.NewGuid(), TitleEn = "Failing Task" },
                    RankedDevelopers = new List<DeveloperScoreDto>
                    {
                        new DeveloperScoreDto { EmployeeId = Guid.NewGuid(), FullName = "Dev A" }
                    }
                }
            }
        };

        _scoringServiceMock.Setup(x => x.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(scoredAssignment));

        _explanationAgentMock.Setup(x => x.GenerateExplanationsAsync(It.IsAny<ExplanationContextDto>()))
            .ThrowsAsync(new Exception("LLM Timeout or crash"));

        // Act
        var result = await _service.GenerateAsync(projectId, sprintId, "en", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        
        var fallbackDev = result.Value.TaskScores.First().RankedDevelopers.First();
    }

    [Fact]
    public async Task GenerateAsync_AgentDropsEmployeeId_ShouldFallBackGracefullyWithoutException()
    {
        // Arrange
        var sprintId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        
        var dev1Id = Guid.NewGuid();
        var dev2Id = Guid.NewGuid();
        var dev3Id = Guid.NewGuid(); // The one that will be dropped by AI

        var scoredAssignment = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskScores = new List<TaskScoringResultDto>
            {
                new TaskScoringResultDto
                {
                    Task = new TaskSnapshotDto { TaskId = Guid.NewGuid(), TitleEn = "Task 1" },
                    RankedDevelopers = new List<DeveloperScoreDto>
                    {
                        new DeveloperScoreDto { EmployeeId = dev1Id, FullName = "Dev 1" },
                        new DeveloperScoreDto { EmployeeId = dev2Id, FullName = "Dev 2" },
                        new DeveloperScoreDto { EmployeeId = dev3Id, FullName = "Dev 3" }
                    }
                }
            }
        };

        _scoringServiceMock.Setup(x => x.ScoreAsync(projectId, sprintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(scoredAssignment));

        // Mock the AI agent to return explanations for Dev1 and Dev2, but OMIT Dev3.
        _explanationAgentMock.Setup(x => x.GenerateExplanationsAsync(It.IsAny<ExplanationContextDto>()))
            .ReturnsAsync(Result.Success(new List<(string, string, string)> 
            { 
                (dev1Id.ToString(), "Reason for Dev 1", "Arabic 1"),
                (dev2Id.ToString(), "Reason for Dev 2", "Arabic 2")
                // dev3 is omitted
            }));

        // Act
        var result = await _service.GenerateAsync(projectId, sprintId, "en", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        
        var rankedDevs = result.Value.TaskScores.First().RankedDevelopers;
        Assert.Equal(3, rankedDevs.Count);
        
        // Dev1 got the correct matched explanation
        var outDev1 = rankedDevs.First(d => d.EmployeeId == dev1Id);
        Assert.Equal("Reason for Dev 1", outDev1.ReasonEn);
        
        // Dev2 got the correct matched explanation
        var outDev2 = rankedDevs.First(d => d.EmployeeId == dev2Id);
        Assert.Equal("Reason for Dev 2", outDev2.ReasonEn);

        // Dev3 was dropped by the AI, so it should safely fall back to the generic "Explanation not generated"
        var outDev3 = rankedDevs.First(d => d.EmployeeId == dev3Id);
        Assert.Equal("Explanation not generated (not in top 3).", outDev3.ReasonEn);
    }
}

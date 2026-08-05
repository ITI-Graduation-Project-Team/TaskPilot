using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Common;
using MockQueryable.Moq;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class SprintPlanningServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IUserStoryRepository> _userStoryRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<IRepository<SprintRetrospective>> _retrospectiveRepoMock;
        private readonly Mock<SprintSuggestionAgent> _agentMock;
        private readonly Mock<ILogger<SprintPlanningService>> _loggerMock;
        private readonly Mock<ICapacityCalculationService> _capacityCalculationServiceMock;
        private readonly Mock<ISprintSelectionService> _sprintSelectionServiceMock;
        private readonly SprintPlanningService _service;

        private readonly Mock<IRepository<Sprint>> _sprintRepoMock;

        public SprintPlanningServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _userStoryRepoMock = new Mock<IUserStoryRepository>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _retrospectiveRepoMock = new Mock<IRepository<SprintRetrospective>>();
            _sprintRepoMock = new Mock<IRepository<Sprint>>();
            _loggerMock = new Mock<ILogger<SprintPlanningService>>();
            _agentMock = new Mock<SprintSuggestionAgent>(null!, null!);
            _sprintSelectionServiceMock = new Mock<ISprintSelectionService>();

            _retrospectiveRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<SprintRetrospective>().BuildMockDbSet().Object);
            _sprintRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<Sprint>().BuildMockDbSet().Object);

            _capacityCalculationServiceMock = new Mock<ICapacityCalculationService>();
            
            _capacityCalculationServiceMock.Setup(x => x.CalculateTargetSprintHoursAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TaskPilot.Models.Common.Results.Result.Success(new SprintCapacityResult { TargetSprintHours = 100m, ExplanationEn = "En", ExplanationAr = "Ar" }));

            _service = new SprintPlanningService(
                _projectRepoMock.Object,
                _userStoryRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _retrospectiveRepoMock.Object,
                _sprintRepoMock.Object,
                null!, // _dataCollectionService is not easily mockable without interface, assuming null is safe for retro-less tests
                _agentMock.Object,
                _capacityCalculationServiceMock.Object,
                _sprintSelectionServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task GenerateSprintSuggestionAsync_NoEmployees_ReturnsNoEmployeesAssignedError()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { NameEn = "Test Project" };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(project);

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());

            var result = await _service.GenerateSprintSuggestionAsync(projectId);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.NoEmployeesAssigned.Code, result.Error!.Code);
        }

        [Fact]
        public async Task GenerateSprintSuggestionAsync_OrchestratesCorrectly_AndMergesAiOutput()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var project = new Project { NameEn = "Test Project", SprintDurationInDays = 14 };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);
            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { Guid.NewGuid() });

            var story1Id = Guid.NewGuid();
            var story2Id = Guid.NewGuid(); // Critical priority, excluded
            var story3Id = Guid.NewGuid(); // Low priority, excluded

            var story1 = new UserStory { TitleEn = "Story 1", Priority = StoryPriority.High };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(story1, story1Id);
            
            var story2 = new UserStory { TitleEn = "Story 2", Priority = StoryPriority.Critical };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(story2, story2Id);

            var story3 = new UserStory { TitleEn = "Story 3", Priority = StoryPriority.Low };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(story3, story3Id);

            var unassignedStories = new List<UserStory> { story1, story2, story3 };
            _userStoryRepoMock.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(unassignedStories);

            // Mock Selection Service to return:
            // - Selected: Story 1 (50 hours)
            // - Excluded: Story 2 (CapacityExceeded), Story 3 (CapacityExceeded)
            var selectionResult = new SprintSelectionResult
            {
                UtilizedHours = 50,
                TargetHours = 100,
                SelectedStories = new List<SuggestedStoryDto>
                {
                    new SuggestedStoryDto { StoryId = story1Id, TitleEn = "Story 1", EstimatedHours = 50, PriorityScore = 300 }
                },
                ExcludedStories = new List<ExcludedStoryDto>
                {
                    new ExcludedStoryDto { StoryId = story2Id, TitleEn = "Story 2", Reason = "Excluded due to sprint capacity limits." },
                    new ExcludedStoryDto { StoryId = story3Id, TitleEn = "Story 3", Reason = "Excluded due to sprint capacity limits." }
                }
            };

            _sprintSelectionServiceMock.Setup(x => x.SelectStories(It.IsAny<List<UserStory>>(), It.IsAny<List<Guid>>(), It.IsAny<SprintSelectionOptions>()))
                .Returns(selectionResult);

            string capturedExcludedStoriesJson = null;

            // Mock AI Agent to return narrative and narrative-only fields, deliberately trying to "override" EstimatedHours and StoryId to prove merge logic works
            var aiSuggestion = new SprintSuggestionDto
            {
                SprintNumber = 1,
                SprintTitleEn = "Awesome Sprint",
                SprintGoalEn = "Goal",
                RisksEn = new List<string> { "Risk 1" },
                TotalEstimatedHours = 999, // Should be ignored
                Stories = new List<SuggestedStoryDto>
                {
                    new SuggestedStoryDto 
                    { 
                        StoryId = Guid.NewGuid(), // Malicious/hallucinated ID, should be ignored during merge
                        TitleEn = "Story 1 AI Override", // Ignored
                        EstimatedHours = 999, // Ignored
                        ReasonEn = "AI Rationale EN", // KEPT
                        ReasonAr = "AI Rationale AR"  // KEPT
                    }
                }
            };

            // But wait, the merge logic maps by StoryId. We must ensure the AI returns the correct StoryId for the rationale to map!
            aiSuggestion.Stories[0].StoryId = story1Id;

            _agentMock.Setup(a => a.SuggestSprintAsync(projectId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, string, int, decimal, decimal, string, string, string, int, CancellationToken>((pid, pn, sd, tsh, uh, sel, exc, rc, sn, ct) => 
                {
                    capturedExcludedStoriesJson = exc;
                })
                .ReturnsAsync(aiSuggestion);

            // Act
            var result = await _service.GenerateSprintSuggestionAsync(projectId);

            // Assert
            Assert.True(result.IsSuccess);
            var finalSuggestion = result.Value!;

            // 1. Verify merge logic overrides AI's hallucinated TotalEstimatedHours
            Assert.Equal(50, finalSuggestion.TotalEstimatedHours); // From selectionResult, NOT aiSuggestion
            
            // 2. Verify merge logic preserves algorithm's exact story stats, but pulls in AI's reasoning
            Assert.Single(finalSuggestion.Stories);
            Assert.Equal(story1Id, finalSuggestion.Stories[0].StoryId);
            Assert.Equal(50, finalSuggestion.Stories[0].EstimatedHours); // From selectionResult
            Assert.Equal("AI Rationale EN", finalSuggestion.Stories[0].ReasonEn); // From AI

            // 3. Verify narrative fields are populated from AI
            Assert.Equal("Awesome Sprint", finalSuggestion.SprintTitleEn);
            Assert.Equal("Goal", finalSuggestion.SprintGoalEn);
            Assert.Contains("Risk 1", finalSuggestion.RisksEn);

            // 4. Verify ExcludedStories were passed through correctly
            Assert.Equal(2, finalSuggestion.ExcludedStories.Count);
            
            // 5. Verify highlight/summary splitting logic
            Assert.NotNull(capturedExcludedStoriesJson);
            // Story 2 is Critical, so it should be highlighted
            Assert.Contains(story2Id.ToString(), capturedExcludedStoriesJson);
            // Story 3 is Low, so it should be summarized (counted), its ID should NOT be in the JSON
            Assert.DoesNotContain(story3Id.ToString(), capturedExcludedStoriesJson);
            Assert.Contains("1 additional lower-priority stories excluded", capturedExcludedStoriesJson);
        }
    }
}
